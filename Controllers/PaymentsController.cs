using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using HabitTrackerBackend.Services;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly SubscriptionService _subscriptionService;
    private readonly IGooglePlayBillingService _googlePlayBillingService;
    private readonly GooglePlayBillingOptions _googlePlayOptions;
    private readonly ILogger<PaymentsController> _logger;
    
    public PaymentsController(
        AppDbContext context, 
        SubscriptionService subscriptionService,
        IGooglePlayBillingService googlePlayBillingService,
        IOptions<GooglePlayBillingOptions> googlePlayOptions,
        ILogger<PaymentsController> logger)
    {
        _context = context;
        _subscriptionService = subscriptionService;
        _googlePlayBillingService = googlePlayBillingService;
        _googlePlayOptions = googlePlayOptions.Value;
        _logger = logger;
    }
    
    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
        {
            throw new UnauthorizedAccessException("Invalid or missing User ID.");
        }
        return userId;
    }
    
    // Verify Google Play purchase and process token purchase
    [HttpPost("verify-token-purchase")]
    public async Task<ActionResult<TokenBalanceDTO>> VerifyTokenPurchase([FromBody] TokenPurchaseRequest request)
    {
        try
        {
            _logger.LogWarning("payload: "+ request.ToString());
            var userId = GetUserId();
            
            // Check if purchase token was already processed
            var existingPurchase = await _context.TokenPurchases
                .FirstOrDefaultAsync(tp => tp.GooglePurchaseToken == request.PurchaseToken);
            
            if (existingPurchase != null)
            {
                _logger.LogWarning("Purchase token already processed: {Token}", request.PurchaseToken);
                return BadRequest("Purchase token has already been processed");
            }
            
            // Verify purchase with Google Play
            bool isValidPurchase = await VerifyGooglePlayPurchase(request.PurchaseToken, request.ProductId);
            
            if (!isValidPurchase)
            {
                _logger.LogWarning("Invalid purchase token: {Token}, ProductId: {ProductId}", 
                    request.PurchaseToken, request.ProductId);
                return BadRequest("Invalid purchase token");
            }
            
            // Create token purchase record
            var tokenPurchase = new TokenPurchase
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenAmount = request.TokenAmount,
                Price = request.Price,
                GooglePurchaseToken = request.PurchaseToken,
                GoogleOrderId = request.OrderId,
                Status = PurchaseStatus.Completed,
                CompletedAt = DateTime.UtcNow
            };
            
            _context.TokenPurchases.Add(tokenPurchase);
            
            // Add tokens to user's balance
            var tokenBalance = await _subscriptionService.EarnTokensAsync(userId, new EarnTokenRequest
            {
                TransactionType = TokenTransactionType.Purchase,
                Amount = request.TokenAmount,
                Description = $"Purchased {request.TokenAmount} tokens",
                RelatedEntityId = tokenPurchase.Id
            });
            
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Token purchase processed successfully for user {UserId}, Amount: {Amount}", 
                userId, request.TokenAmount);
            
            return Ok(tokenBalance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing token purchase for user {UserId}", GetUserId());
            return StatusCode(500, $"Error processing purchase: {ex.Message}");
        }
    }
    
    // Verify Google Play subscription purchase
    [HttpPost("verify-subscription")]
    public async Task<ActionResult<SubscriptionStatusDTO>> VerifySubscription([FromBody] PurchaseVerificationRequest request)
    {
        try
        {
            var userId = GetUserId();
            
            // Check if purchase token was already processed
            var existingSubscription = await _context.UserSubscriptions
                .FirstOrDefaultAsync(us => us.GooglePurchaseToken == request.PurchaseToken);
            
            if (existingSubscription != null)
            {
                _logger.LogWarning("Subscription purchase token already processed: {Token}", request.PurchaseToken);
                return BadRequest("Subscription purchase token has already been processed");
            }
            
            // Verify subscription with Google Play
            bool isValidPurchase = await VerifyGooglePlaySubscription(request.PurchaseToken, request.ProductId);
            
            if (!isValidPurchase)
            {
                _logger.LogWarning("Invalid subscription purchase token: {Token}, ProductId: {ProductId}", 
                    request.PurchaseToken, request.ProductId);
                return BadRequest("Invalid purchase token");
            }
            
            // Get subscription plan
            var subscriptionPlan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(sp => sp.PlanName == request.ProductId && sp.IsActive);
            
            if (subscriptionPlan == null)
            {
                _logger.LogWarning("Invalid subscription plan: {ProductId}", request.ProductId);
                return BadRequest("Invalid subscription plan");
            }
            
            // Create or update user subscription
            var existingActiveSubscription = await _context.UserSubscriptions
                .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == SubscriptionStatus.Active);
            
            if (existingActiveSubscription != null)
            {
                // Cancel existing subscription
                existingActiveSubscription.Status = SubscriptionStatus.Cancelled;
                existingActiveSubscription.CancelledAt = DateTime.UtcNow;
                _logger.LogInformation("Cancelled existing subscription for user {UserId}", userId);
            }
            
            // Create new subscription
            var newSubscription = new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SubscriptionPlanId = subscriptionPlan.Id,
                GooglePurchaseToken = request.PurchaseToken,
                GoogleOrderId = request.OrderId,
                Status = SubscriptionStatus.Active,
                ExpiresAt = DateTime.UtcNow.AddMonths(subscriptionPlan.DurationMonths)
            };
            
            _context.UserSubscriptions.Add(newSubscription);
            
            // Update user subscription info
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.SubscriptionTier = subscriptionPlan.PlanName;
                user.SubscriptionExpiresAt = newSubscription.ExpiresAt;
            }
            
            // Add subscription tokens
            if (subscriptionPlan.TokensIncluded > 0)
            {
                await _subscriptionService.EarnTokensAsync(userId, new EarnTokenRequest
                {
                    TransactionType = TokenTransactionType.Subscription_Bonus,
                    Amount = subscriptionPlan.TokensIncluded,
                    Description = $"{subscriptionPlan.DisplayName} token bonus",
                    RelatedEntityId = newSubscription.Id
                });
            }
            
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Subscription activated successfully for user {UserId}, Plan: {PlanName}", 
                userId, subscriptionPlan.PlanName);
            
            return Ok(await _subscriptionService.GetSubscriptionStatusAsync(userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing subscription for user {UserId}", GetUserId());
            return StatusCode(500, $"Error processing subscription: {ex.Message}");
        }
    }
    
    // Get available subscription plans
    [HttpGet("subscription-plans")]
    public async Task<ActionResult<List<SubscriptionPlanDTO>>> GetSubscriptionPlans()
    {
        var plans = await _context.SubscriptionPlans
            .Where(sp => sp.IsActive)
            .Select(sp => new SubscriptionPlanDTO
            {
                Id = sp.Id,
                PlanName = sp.PlanName,
                DisplayName = sp.DisplayName,
                Price = sp.Price,
                Currency = sp.Currency,
                DurationMonths = sp.DurationMonths,
                TokensIncluded = sp.TokensIncluded,
                HabitLimit = sp.HabitLimit,
                Features = sp.Features != null
                    ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(sp.Features, new System.Text.Json.JsonSerializerOptions())
                    : new List<string>(),
                IsActive = sp.IsActive
            })
            .ToListAsync();
            
        return Ok(plans);
    }
    
    // Cancel subscription
    [HttpPost("cancel-subscription")]
    public async Task<ActionResult<SubscriptionStatusDTO>> CancelSubscription()
    {
        try
        {
            var userId = GetUserId();
            
            var activeSubscription = await _context.UserSubscriptions
                .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == SubscriptionStatus.Active);
            
            if (activeSubscription == null)
            {
                return BadRequest("No active subscription found");
            }
            
            activeSubscription.Status = SubscriptionStatus.Cancelled;
            activeSubscription.CancelledAt = DateTime.UtcNow;
            activeSubscription.AutoRenew = false;
            
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Subscription cancelled for user {UserId}", userId);
            
            return Ok(await _subscriptionService.GetSubscriptionStatusAsync(userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling subscription for user {UserId}", GetUserId());
            return StatusCode(500, $"Error cancelling subscription: {ex.Message}");
        }
    }
    
    // Restore purchases (for app reinstalls)
    [HttpPost("restore-purchases")]
    public async Task<ActionResult<SubscriptionStatusDTO>> RestorePurchases()
    {
        try
        {
            var userId = GetUserId();
            
            // Get all user's purchase tokens from the database
            var userPurchases = await _context.UserSubscriptions
                .Where(us => us.UserId == userId && !string.IsNullOrEmpty(us.GooglePurchaseToken))
                .ToListAsync();
            
            var tokenPurchases = await _context.TokenPurchases
                .Where(tp => tp.UserId == userId && !string.IsNullOrEmpty(tp.GooglePurchaseToken))
                .ToListAsync();
            
            bool restoredAny = false;
            
            // Verify active subscriptions
            foreach (var subscription in userPurchases)
            {
                try
                {
                    var subscriptionPlan = await _context.SubscriptionPlans
                        .FirstOrDefaultAsync(sp => sp.Id == subscription.SubscriptionPlanId);
                    
                    if (subscriptionPlan != null)
                    {
                        bool isValid = await VerifyGooglePlaySubscription(subscription.GooglePurchaseToken!, subscriptionPlan.PlanName);
                        
                        if (isValid && subscription.Status != SubscriptionStatus.Active)
                        {
                            subscription.Status = SubscriptionStatus.Active;
                            restoredAny = true;
                            _logger.LogInformation("Restored subscription for user {UserId}, Plan: {PlanName}", 
                                userId, subscriptionPlan.PlanName);
                        }
                        else if (!isValid && subscription.Status == SubscriptionStatus.Active)
                        {
                            subscription.Status = SubscriptionStatus.Expired;
                            _logger.LogInformation("Expired subscription for user {UserId}, Plan: {PlanName}", 
                                userId, subscriptionPlan.PlanName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error verifying subscription during restore for user {UserId}", userId);
                }
            }
            
            if (restoredAny)
            {
                await _context.SaveChangesAsync();
            }
            
            _logger.LogInformation("Purchase restoration completed for user {UserId}, Restored: {Restored}", 
                userId, restoredAny);
            
            return Ok(await _subscriptionService.GetSubscriptionStatusAsync(userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring purchases for user {UserId}", GetUserId());
            return StatusCode(500, $"Error restoring purchases: {ex.Message}");
        }
    }
    
    // Private method to verify Google Play product purchases
    private async Task<bool> VerifyGooglePlayPurchase(string purchaseToken, string productId)
    {
        try
        {
            if (string.IsNullOrEmpty(_googlePlayOptions.PackageName))
            {
                _logger.LogWarning("Google Play package name not configured");
                return false;
            }
            
            var isValid = await _googlePlayBillingService.IsProductPurchaseValidAsync(
                _googlePlayOptions.PackageName, productId, purchaseToken);
            
            _logger.LogInformation("Product purchase verification result: {IsValid} for ProductId: {ProductId}", 
                isValid, productId);
            
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify Google Play product purchase: {ProductId}, Token: {Token}", 
                productId, purchaseToken);
            return false;
        }
    }
    
    // Private method to verify Google Play subscription purchases
    private async Task<bool> VerifyGooglePlaySubscription(string purchaseToken, string subscriptionId)
    {
        try
        {
            if (string.IsNullOrEmpty(_googlePlayOptions.PackageName))
            {
                _logger.LogWarning("Google Play package name not configured");
                return false;
            }
            
            var isValid = await _googlePlayBillingService.IsSubscriptionPurchaseValidAsync(
                _googlePlayOptions.PackageName, subscriptionId, purchaseToken);
            
            _logger.LogInformation("Subscription purchase verification result: {IsValid} for SubscriptionId: {SubscriptionId}", 
                isValid, subscriptionId);
            
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify Google Play subscription purchase: {SubscriptionId}, Token: {Token}", 
                subscriptionId, purchaseToken);
            return false;
        }
    }
}

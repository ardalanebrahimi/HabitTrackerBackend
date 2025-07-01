using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly SubscriptionService _subscriptionService;
    
    public PaymentsController(AppDbContext context, SubscriptionService subscriptionService)
    {
        _context = context;
        _subscriptionService = subscriptionService;
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
            var userId = GetUserId();
            
            // TODO: Implement Google Play billing verification
            // For now, we'll simulate successful verification
            bool isValidPurchase = await VerifyGooglePlayPurchase(request.PurchaseToken, request.ProductId);
            
            if (!isValidPurchase)
            {
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
            
            return Ok(tokenBalance);
        }
        catch (Exception ex)
        {
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
            
            // TODO: Implement Google Play billing verification
            bool isValidPurchase = await VerifyGooglePlayPurchase(request.PurchaseToken, request.ProductId);
            
            if (!isValidPurchase)
            {
                return BadRequest("Invalid purchase token");
            }
            
            // Get subscription plan
            var subscriptionPlan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(sp => sp.PlanName == request.ProductId && sp.IsActive);
            
            if (subscriptionPlan == null)
            {
                return BadRequest("Invalid subscription plan");
            }
            
            // Create or update user subscription
            var existingSubscription = await _context.UserSubscriptions
                .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == SubscriptionStatus.Active);
            
            if (existingSubscription != null)
            {
                // Cancel existing subscription
                existingSubscription.Status = SubscriptionStatus.Cancelled;
                existingSubscription.CancelledAt = DateTime.UtcNow;
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
            
            return Ok(await _subscriptionService.GetSubscriptionStatusAsync(userId));
        }
        catch (Exception ex)
        {
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
            
            return Ok(await _subscriptionService.GetSubscriptionStatusAsync(userId));
        }
        catch (Exception ex)
        {
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
            
            // TODO: Implement Google Play purchase restoration
            // This would involve querying Google Play for the user's purchase history
            // and restoring any valid subscriptions
            
            return Ok(await _subscriptionService.GetSubscriptionStatusAsync(userId));
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error restoring purchases: {ex.Message}");
        }
    }
    
    // Private method to verify Google Play purchases
    private async Task<bool> VerifyGooglePlayPurchase(string purchaseToken, string productId)
    {
        try
        {
            // TODO: Implement actual Google Play billing verification
            // This involves:
            // 1. Using Google Play Developer API
            // 2. Verifying the purchase token with Google's servers
            // 3. Checking if the purchase is valid and not refunded
            
            // For development, we'll simulate validation
            await Task.Delay(100); // Simulate API call
            return !string.IsNullOrEmpty(purchaseToken) && !string.IsNullOrEmpty(productId);
        }
        catch
        {
            return false;
        }
    }
}

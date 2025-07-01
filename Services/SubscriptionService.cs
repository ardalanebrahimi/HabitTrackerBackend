using Microsoft.EntityFrameworkCore;
using System.Text.Json;

public class SubscriptionService
{
    private readonly AppDbContext _context;
    
    public SubscriptionService(AppDbContext context)
    {
        _context = context;
    }
    
    // Get user's current subscription status
    public async Task<SubscriptionStatusDTO> GetSubscriptionStatusAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) throw new InvalidOperationException("User not found");
        
        var currentSubscription = await _context.UserSubscriptions
            .Include(us => us.SubscriptionPlan)
            .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == SubscriptionStatus.Active);
        
        if (currentSubscription == null || currentSubscription.ExpiresAt < DateTime.UtcNow)
        {
            // Update user to free tier if subscription expired
            user.SubscriptionTier = "free";
            user.SubscriptionExpiresAt = null;
            await _context.SaveChangesAsync();
            
            return new SubscriptionStatusDTO
            {
                SubscriptionTier = "free",
                IsActive = false,
                HabitLimit = GetHabitLimit("free"),
                Features = GetFeatures("free")
            };
        }
        
        var features = string.IsNullOrEmpty(currentSubscription.SubscriptionPlan.Features) 
            ? new List<string>() 
            : JsonSerializer.Deserialize<List<string>>(currentSubscription.SubscriptionPlan.Features) ?? new List<string>();
        
        return new SubscriptionStatusDTO
        {
            SubscriptionTier = currentSubscription.SubscriptionPlan.PlanName,
            ExpiresAt = currentSubscription.ExpiresAt,
            IsActive = true,
            AutoRenew = currentSubscription.AutoRenew,
            HabitLimit = currentSubscription.SubscriptionPlan.HabitLimit,
            TokensIncluded = currentSubscription.SubscriptionPlan.TokensIncluded,
            Features = features
        };
    }
    
    // Get user's token balance with subscription info
    public async Task<TokenBalanceDTO> GetTokenBalanceAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) throw new InvalidOperationException("User not found");
        
        var subscriptionStatus = await GetSubscriptionStatusAsync(userId);
        
        return new TokenBalanceDTO
        {
            TokenBalance = user.TokenBalance,
            SubscriptionTier = user.SubscriptionTier,
            SubscriptionExpiresAt = user.SubscriptionExpiresAt,
            HabitLimit = subscriptionStatus.HabitLimit,
            HabitsCreated = user.TotalHabitsCreated,
            CanCreateHabits = CanCreateHabit(user.TotalHabitsCreated, subscriptionStatus.HabitLimit),
            NextTokenRefresh = GetNextTokenRefresh(user.SubscriptionTier)
        };
    }
    
    // Spend tokens with transaction logging
    public async Task<TokenBalanceDTO> SpendTokensAsync(Guid userId, SpendTokenRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) throw new InvalidOperationException("User not found");
        
        if (user.TokenBalance < request.Amount)
        {
            throw new InvalidOperationException("Insufficient token balance");
        }
        
        // Create transaction record
        var transaction = new TokenTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = -request.Amount,
            TransactionType = request.TransactionType,
            Description = request.Description ?? GetTransactionDescription(request.TransactionType),
            RelatedEntityId = request.RelatedEntityId
        };
        
        _context.TokenTransactions.Add(transaction);
        
        // Update user balance
        user.TokenBalance -= request.Amount;
        await _context.SaveChangesAsync();
        
        return await GetTokenBalanceAsync(userId);
    }
    
    // Earn tokens with transaction logging
    public async Task<TokenBalanceDTO> EarnTokensAsync(Guid userId, EarnTokenRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) throw new InvalidOperationException("User not found");
        
        // Create transaction record
        var transaction = new TokenTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = request.Amount,
            TransactionType = request.TransactionType,
            Description = request.Description ?? GetTransactionDescription(request.TransactionType),
            RelatedEntityId = request.RelatedEntityId
        };
        
        _context.TokenTransactions.Add(transaction);
        
        // Update user balance
        user.TokenBalance += request.Amount;
        await _context.SaveChangesAsync();
        
        return await GetTokenBalanceAsync(userId);
    }
    
    // Check if user can create a habit
    public bool CanCreateHabit(int currentHabits, int habitLimit)
    {
        return currentHabits < habitLimit;
    }
    
    // Get habit limits based on subscription tier
    public int GetHabitLimit(string subscriptionTier)
    {
        return subscriptionTier switch
        {
            "free" => 5,
            "premium_monthly" => 50,
            "premium_yearly" => 100,
            _ => 5
        };
    }
    
    // Get features based on subscription tier
    public List<string> GetFeatures(string subscriptionTier)
    {
        return subscriptionTier switch
        {
            "free" => new List<string> { "5 habits max", "Basic analytics", "Public sharing" },
            "premium_monthly" => new List<string> { "50 habits max", "Advanced analytics", "Monthly tokens", "Priority AI", "Custom themes" },
            "premium_yearly" => new List<string> { "100 habits max", "Advanced analytics", "Yearly tokens", "Priority AI", "Custom themes", "Export data" },
            _ => new List<string>()
        };
    }
    
    // Get next token refresh date
    private DateTime? GetNextTokenRefresh(string subscriptionTier)
    {
        if (subscriptionTier == "free") return null;
        
        var now = DateTime.UtcNow;
        return subscriptionTier switch
        {
            "premium_monthly" => new DateTime(now.Year, now.Month, 1).AddMonths(1),
            "premium_yearly" => new DateTime(now.Year, 1, 1).AddYears(1),
            _ => null
        };
    }
    
    // Get transaction description based on type
    private string GetTransactionDescription(TokenTransactionType transactionType)
    {
        return transactionType switch
        {
            TokenTransactionType.Spend_Habit_Creation => "Created new habit",
            TokenTransactionType.Spend_AI_Simple => "Simple AI suggestion",
            TokenTransactionType.Spend_AI_Advanced => "Advanced AI suggestion",
            TokenTransactionType.Spend_Custom_Cheer => "Custom cheer message",
            TokenTransactionType.Earn_Streak => "Habit streak bonus",
            TokenTransactionType.Earn_Daily_Login => "Daily login bonus",
            TokenTransactionType.Earn_Referral => "Referral bonus",
            TokenTransactionType.Earn_Onboarding => "Onboarding completion",
            TokenTransactionType.Purchase => "Token purchase",
            TokenTransactionType.Subscription_Bonus => "Subscription token bonus",
            _ => "Token transaction"
        };
    }
    
    // Process habit creation and check limits
    public async Task<bool> ProcessHabitCreationAsync(Guid userId, bool requiresTokens = false)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;
        
        var tokenBalance = await GetTokenBalanceAsync(userId);
        
        // Check if user can create habits within their limit
        if (!tokenBalance.CanCreateHabits && requiresTokens)
        {
            // Require tokens if over limit
            if (user.TokenBalance < 1) return false;
            
            await SpendTokensAsync(userId, new SpendTokenRequest
            {
                TransactionType = TokenTransactionType.Spend_Habit_Creation,
                Amount = 1
            });
        }
        
        // Increment habit count
        user.TotalHabitsCreated++;
        await _context.SaveChangesAsync();
        
        return true;
    }
    
    // Generate referral code
    public async Task<string> GenerateReferralCodeAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) throw new InvalidOperationException("User not found");
        
        if (!string.IsNullOrEmpty(user.ReferralCode))
        {
            return user.ReferralCode;
        }
        
        // Generate unique referral code
        string referralCode;
        bool isUnique;
        do
        {
            referralCode = GenerateRandomCode();
            isUnique = !await _context.Users.AnyAsync(u => u.ReferralCode == referralCode);
        } while (!isUnique);
        
        user.ReferralCode = referralCode;
        await _context.SaveChangesAsync();
        
        return referralCode;
    }
    
    // Process referral
    public async Task<bool> ProcessReferralAsync(Guid newUserId, string referralCode)
    {
        var referrer = await _context.Users.FirstOrDefaultAsync(u => u.ReferralCode == referralCode);
        if (referrer == null) return false;
        
        var newUser = await _context.Users.FindAsync(newUserId);
        if (newUser == null || newUser.ReferredById.HasValue) return false;
        
        // Set referral relationship
        newUser.ReferredById = referrer.Id;
        
        // Give tokens to both users
        await EarnTokensAsync(referrer.Id, new EarnTokenRequest
        {
            TransactionType = TokenTransactionType.Earn_Referral,
            Amount = 2,
            Description = $"Referred {newUser.UserName}"
        });
        
        await EarnTokensAsync(newUserId, new EarnTokenRequest
        {
            TransactionType = TokenTransactionType.Earn_Referral,
            Amount = 1,
            Description = "Welcome bonus"
        });
        
        await _context.SaveChangesAsync();
        return true;
    }
    
    private string GenerateRandomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}

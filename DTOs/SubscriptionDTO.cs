using System.ComponentModel.DataAnnotations;

public class TokenBalanceDTO
{
    public int TokenBalance { get; set; }
    public string SubscriptionTier { get; set; } = "free";
    public DateTime? SubscriptionExpiresAt { get; set; }
    public int HabitLimit { get; set; }
    public int HabitsCreated { get; set; }
    public bool CanCreateHabits { get; set; }
    public DateTime? NextTokenRefresh { get; set; }
}

public class SpendTokenRequest
{
    [Required]
    public TokenTransactionType TransactionType { get; set; }
    
    public string? Description { get; set; }
    
    public Guid? RelatedEntityId { get; set; }
    
    [Range(1, 50)]
    public int Amount { get; set; } = 1;
}

public class EarnTokenRequest
{
    [Required]
    public TokenTransactionType TransactionType { get; set; }
    
    public string? Description { get; set; }
    
    public Guid? RelatedEntityId { get; set; }
    
    [Range(1, 50)]
    public int Amount { get; set; } = 1;
}

public class SubscriptionStatusDTO
{
    public string SubscriptionTier { get; set; } = "free";
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public bool AutoRenew { get; set; }
    public List<string> Features { get; set; } = new();
    public int HabitLimit { get; set; }
    public int TokensIncluded { get; set; }
}

public class SubscriptionPlanDTO
{
    public Guid Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public int DurationMonths { get; set; }
    public int TokensIncluded { get; set; }
    public int HabitLimit { get; set; }
    public List<string> Features { get; set; } = new();
    public bool IsActive { get; set; }
}

public class PurchaseVerificationRequest
{
    [Required]
    public string PurchaseToken { get; set; } = string.Empty;
    
    [Required]
    public string ProductId { get; set; } = string.Empty;
    
    public string? OrderId { get; set; }
    
    public PurchaseType PurchaseType { get; set; }
}

public class TokenPurchaseRequest
{
    [Required]
    public string PurchaseToken { get; set; } = string.Empty;
    
    [Required]
    public string ProductId { get; set; } = string.Empty;
    
    public string? OrderId { get; set; }
    
    [Range(1, 1000)]
    public int TokenAmount { get; set; }
    
    public decimal Price { get; set; }
}

public class ReferralCodeRequest
{
    [Required]
    public string ReferralCode { get; set; } = string.Empty;
}

public class TokenTransactionDTO
{
    public Guid Id { get; set; }
    public int Amount { get; set; }
    public TokenTransactionType TransactionType { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public enum PurchaseType
{
    TokenPack,
    Subscription
}

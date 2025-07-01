using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("subscription_plans")]
public class SubscriptionPlan
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }
    
    [Required]
    [Column("plan_name")]
    public string PlanName { get; set; } = string.Empty; // premium_monthly, premium_yearly
    
    [Column("display_name")]
    public string DisplayName { get; set; } = string.Empty;
    
    [Column("price")]
    public decimal Price { get; set; }
    
    [Column("currency")]
    public string Currency { get; set; } = "USD";
    
    [Column("duration_months")]
    public int DurationMonths { get; set; }
    
    [Column("tokens_included")]
    public int TokensIncluded { get; set; }
    
    [Column("habit_limit")]
    public int HabitLimit { get; set; }
    
    [Column("features")]
    public string Features { get; set; } = string.Empty; // JSON string of features
    
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("user_subscriptions")]
public class UserSubscription
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }
    
    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }
    
    [Required]
    [Column("subscription_plan_id")]
    public Guid SubscriptionPlanId { get; set; }
    
    [Column("google_purchase_token")]
    public string? GooglePurchaseToken { get; set; }
    
    [Column("google_order_id")]
    public string? GoogleOrderId { get; set; }
    
    [Column("status")]
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    
    [Column("started_at")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }
    
    [Column("auto_renew")]
    public bool AutoRenew { get; set; } = true;
    
    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
    
    [ForeignKey("SubscriptionPlanId")]
    public virtual SubscriptionPlan SubscriptionPlan { get; set; } = null!;
}

[Table("token_transactions")]
public class TokenTransaction
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }
    
    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }
    
    [Column("amount")]
    public int Amount { get; set; } // Positive for earnings, negative for spending
    
    [Column("transaction_type")]
    public TokenTransactionType TransactionType { get; set; }
    
    [Column("description")]
    public string Description { get; set; } = string.Empty;
    
    [Column("related_entity_id")]
    public Guid? RelatedEntityId { get; set; } // Habit ID, Purchase ID, etc.
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}

[Table("token_purchases")]
public class TokenPurchase
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }
    
    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }
    
    [Column("token_amount")]
    public int TokenAmount { get; set; }
    
    [Column("price")]
    public decimal Price { get; set; }
    
    [Column("currency")]
    public string Currency { get; set; } = "USD";
    
    [Column("google_purchase_token")]
    public string? GooglePurchaseToken { get; set; }
    
    [Column("google_order_id")]
    public string? GoogleOrderId { get; set; }
    
    [Column("status")]
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Pending;
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }
    
    // Navigation properties
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}

public enum SubscriptionStatus
{
    Active,
    Expired,
    Cancelled,
    Pending
}

public enum TokenTransactionType
{
    Purchase,
    Earn_Referral,
    Earn_Streak,
    Earn_Daily_Login,
    Earn_Onboarding,
    Spend_Habit_Creation,
    Spend_AI_Simple,
    Spend_AI_Advanced,
    Spend_Custom_Cheer,
    Subscription_Bonus
}

public enum PurchaseStatus
{
    Pending,
    Completed,
    Failed,
    Cancelled,
    Refunded
}

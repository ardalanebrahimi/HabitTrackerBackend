using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System;
using Microsoft.AspNetCore.Identity;

[Table("users")]
public class User : IdentityUser<Guid>
{
    [Key]
    [Column("id")]
    public override Guid Id { get; set; }

    [Column("email")]
    public override string? Email { get; set; }

    [Column("passwordhash")]
    public override string? PasswordHash { get; set; }

    [Column("name")]
    public override string? UserName { get; set; }
    
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiryTime { get; set; }
    
    // Token System
    [Column("token_balance")]
    public int TokenBalance { get; set; } = 10; // Starting tokens for new users
    
    // Subscription System
    [Column("subscription_tier")]
    public string SubscriptionTier { get; set; } = "free"; // free, premium_monthly, premium_yearly
    
    [Column("subscription_expires_at")]
    public DateTime? SubscriptionExpiresAt { get; set; }
    
    [Column("referral_code")]
    public string? ReferralCode { get; set; }
    
    [Column("referred_by")]
    public Guid? ReferredById { get; set; }
    
    [Column("total_habits_created")]
    public int TotalHabitsCreated { get; set; } = 0;
    
    // Navigation properties
    public virtual ICollection<Connection> SentConnections { get; set; } = new List<Connection>();
    public virtual ICollection<Connection> ReceivedConnections { get; set; } = new List<Connection>();
    public virtual User? ReferredByUser { get; set; }
    public virtual ICollection<User> ReferredUsers { get; set; } = new List<User>();
}

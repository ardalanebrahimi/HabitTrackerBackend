using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("profile_views")]
public class ProfileView
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("viewer_id")]
    public Guid ViewerId { get; set; }

    [Required]
    [Column("viewed_user_id")]
    public Guid ViewedUserId { get; set; }

    [Column("viewed_at")]
    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;

    [Column("is_friend_view")]
    public bool IsFriendView { get; set; } = false;

    // Navigation properties
    [ForeignKey("ViewerId")]
    public virtual User Viewer { get; set; } = null!;

    [ForeignKey("ViewedUserId")]
    public virtual User ViewedUser { get; set; } = null!;
}

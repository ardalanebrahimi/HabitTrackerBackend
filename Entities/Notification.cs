using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("notifications")]
public class Notification
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("type")]
    public NotificationType Type { get; set; }    [Column("title")]
    public required string Title { get; set; }

    [Column("message")]
    public required string Message { get; set; }

    [Column("data")]
    public string? Data { get; set; }

    [Column("is_read")]
    public bool IsRead { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}

public enum NotificationType
{
    ConnectionRequest,
    HabitCheckRequest,
    ProgressUpdate,
    CheerReceived,
    CheerSent
}
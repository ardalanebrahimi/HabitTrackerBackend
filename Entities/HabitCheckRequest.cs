using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("habit_check_requests")]
public class HabitCheckRequest
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("habit_id")]
    public Guid HabitId { get; set; }

    [Column("requester_id")]
    public Guid RequesterId { get; set; }

    [Column("requested_user_id")]
    public Guid RequestedUserId { get; set; }

    [Column("status")]
    public CheckRequestStatus Status { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("responded_at")]
    public DateTime? RespondedAt { get; set; }

    [ForeignKey("HabitId")]
    public virtual Habit Habit { get; set; }

    [ForeignKey("RequesterId")]
    public virtual User Requester { get; set; }

    [ForeignKey("RequestedUserId")]
    public virtual User RequestedUser { get; set; }
}

public enum CheckRequestStatus
{
    Pending,
    Approved,
    Rejected
} 
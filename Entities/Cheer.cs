using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("cheers")]
public class Cheer
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("habit_id")]
    public Guid HabitId { get; set; }

    [Required]
    [Column("from_user_id")]
    public Guid FromUserId { get; set; }

    [Required]
    [Column("to_user_id")]
    public Guid ToUserId { get; set; }    [Required]
    [Column("emoji")]
    [MaxLength(10)]
    public required string Emoji { get; set; }

    [Column("message")]
    [MaxLength(500)]
    public string? Message { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("HabitId")]
    public virtual Habit Habit { get; set; } = null!;

    [ForeignKey("FromUserId")]
    public virtual User FromUser { get; set; } = null!;

    [ForeignKey("ToUserId")]
    public virtual User ToUser { get; set; } = null!;
}

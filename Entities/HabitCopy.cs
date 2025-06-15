using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("habit_copies")]
public class HabitCopy
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    [ForeignKey("OriginalHabit")]
    public Guid OriginalHabitId { get; set; }

    [Required]
    [ForeignKey("CopiedHabit")]
    public Guid CopiedHabitId { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Habit OriginalHabit { get; set; } = null!;
    public virtual Habit CopiedHabit { get; set; } = null!;
}

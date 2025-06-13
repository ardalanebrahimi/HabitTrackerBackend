using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

[Table("habits")]
public class Habit
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid? Id { get; set; }    [Required(ErrorMessage = "Name is required.")]
    public required string Name { get; set; }

    [Required(ErrorMessage = "Frequency is required.")]
    [RegularExpression("daily|weekly|monthly", ErrorMessage = "Invalid frequency.")]
    public required string Frequency { get; set; }

    [Required(ErrorMessage = "Goal Type is required.")]
    [RegularExpression("binary|numeric", ErrorMessage = "Invalid goal type.")]
    public required string GoalType { get; set; }    public string? Description { get; set; } // Optional description
    public int? TargetValue { get; set; }
    public required string TargetType { get; set; } = "ongoing"; // "ongoing", "streak", "endDate"
    public int? StreakTarget { get; set; } // Optional streak target
    public DateTime? EndDate { get; set; } // Optional end date
    public int AllowedGaps { get; set; } = 1; // Allowed gaps before streak breaks
    public DateTime? StartDate { get; set; } // Optional start date
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [ForeignKey("User")]
    public Guid UserId { get; set; }    public virtual User User { get; set; } = null!;

    public bool IsArchived { get; set; } = false;
    public bool IsPrivate { get; set; } = false; // Privacy setting for the habit

    public List<HabitLog> Logs { get; set; } = new List<HabitLog>();
}

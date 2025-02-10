using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("habits")]
public class Habit
{
    [Key]
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.Identity)]
    public Guid? Id { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    public string Frequency { get; set; } // "daily", "weekly", "monthly"

    [Required]
    public string GoalType { get; set; } // "binary" or "numeric"

    public int? TargetValue { get; set; }
    public int CurrentValue { get; set; } = 0;
    public int Streak { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [ForeignKey("User")]
    public Guid UserId { get; set; }

    // Tracking completion logs
    public List<HabitLog> Logs { get; set; } = new List<HabitLog>();
}

// Habit log model for tracking progress
public class HabitLog
{
    [Key]
    public Guid Id { get; set; }

    [ForeignKey("Habit")]
    public Guid HabitId { get; set; }

    public DateTime Date { get; set; }
    public int Value { get; set; }
}

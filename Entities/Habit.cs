using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("habits")]
public class Habit
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid? Id { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    public string Frequency { get; set; } // "daily", "weekly", "monthly"

    [Required]
    public string GoalType { get; set; } // "binary" or "numeric"

    public int? TargetValue { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [ForeignKey("User")]
    public Guid UserId { get; set; }

    public bool IsArchived { get; set; } = false; 

    // Tracking completion logs
    public List<HabitLog> Logs { get; set; } = new List<HabitLog>();
}


[Table("habit_logs")]
public class HabitLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    [ForeignKey("Habit")]
    public Guid HabitId { get; set; }

    public DateTime Timestamp { get; set; }

    // New: Store all period keys to simplify queries
    [Required] public int DailyKey { get; set; }    // YYYYMMDD
    [Required] public int WeeklyKey { get; set; }   // YYYYWW
    [Required] public int MonthlyKey { get; set; } // YYYYMM

    public int Value { get; set; } // +1/-1 to manage progress
    public int Target { get; set; } // Habit target at the time of logging
}


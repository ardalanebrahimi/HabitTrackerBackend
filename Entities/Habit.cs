﻿using System;
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

    public DateTime Timestamp { get; set; } // ✅ Exact time of completion

    [Required]
    public int PeriodKey { get; set; } // ✅ Stores period dynamically:
    // - Date (if daily) => YYYYMMDD
    // - Week number (if weekly) => YYYYWW
    // - Month number (if monthly) => YYYYMM

    public int Value { get; set; } // ✅ Number of completions in that period
}

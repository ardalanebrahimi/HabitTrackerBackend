using System;
using System.Collections.Generic;

public class HabitWithProgressDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string Frequency { get; set; } // "daily", "weekly", "monthly"
    public string GoalType { get; set; } // "binary" or "numeric"
    public int? TargetValue { get; set; }
    public string TargetType { get; set; } = "ongoing"; // "ongoing", "streak", "endDate"
    public int? StreakTarget { get; set; }
    public DateTime? EndDate { get; set; }
    public int CurrentValue { get; set; }
    public int Streak { get; set; }
    public bool IsCompleted { get; set; }
    public List<HabitLogDTO> RecentLogs { get; set; } = new List<HabitLogDTO>();
}

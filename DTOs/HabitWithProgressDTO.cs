using System;
using System.Collections.Generic;

public class HabitWithProgressDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Frequency { get; set; } = "";
    public string GoalType { get; set; } = "";
    public int? TargetValue { get; set; }
    public string TargetType { get; set; } = "";
    public int? StreakTarget { get; set; }
    public DateTime? EndDate { get; set; }
    public int CurrentValue { get; set; }
    public int Streak { get; set; }
    public bool IsCompleted { get; set; }
    public List<HabitLogDTO>? RecentLogs { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public bool isOwnedHabit { get; set; }
    public bool CanManageProgress { get; set; }
    public int CopyCount { get; set; } = 0;
}

public class HabitWithProgressDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Frequency { get; set; } // "daily", "weekly", "monthly"
    public string GoalType { get; set; } // "binary" or "numeric"
    public int? TargetValue { get; set; } // The goal to be reached
    public int CurrentValue { get; set; } // ✅ Dynamically calculated progress
    public int Streak { get; set; } // ✅ Tracks how many days in a row this habit is done
    public bool IsCompleted { get; set; } // ✅ Whether the habit is completed for today
}

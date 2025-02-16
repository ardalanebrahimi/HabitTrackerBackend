public class HabitWithProgressDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Frequency { get; set; } // "daily", "weekly", "monthly"
    public string GoalType { get; set; } // "binary" or "numeric"
    public int? TargetValue { get; set; }
    public int CurrentValue { get; set; } // ✅ Dynamically calculated progress
}

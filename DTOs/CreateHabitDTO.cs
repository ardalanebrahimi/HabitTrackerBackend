public class CreateHabitDTO
{
    public string Name { get; set; }
    public string Frequency { get; set; } // "daily", "weekly", "monthly"
    public string GoalType { get; set; } // "binary" or "numeric"
    public int? TargetValue { get; set; } // Numeric goal (optional)
}

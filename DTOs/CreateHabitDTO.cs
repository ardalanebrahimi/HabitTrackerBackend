public class CreateHabitDTO
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }  // Optional description
    public string Frequency { get; set; } = "daily";  // "daily", "weekly", "monthly"
    public string GoalType { get; set; } = "binary";  // "binary" or "numeric"
    public string TargetType { get; set; } = "ongoing"; // "ongoing", "streak", "endDate"
    public int? TargetValue { get; set; }  // Numeric goal (optional)
    public int? StreakTarget { get; set; }  // Optional streak goal
    public DateTime? EndDate { get; set; }  // Optional end date
    public int AllowedGaps { get; set; } = 0; // Default 0 gaps
    public DateTime? StartDate { get; set; }  // Optional start date
}

public class CreateHabitDTO
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Frequency { get; set; } = "daily";
    public string GoalType { get; set; } = "binary";
    public string TargetType { get; set; } = "ongoing";
    public int? TargetValue { get; set; }
    public int? StreakTarget { get; set; }
    public DateTime? EndDate { get; set; }
    public int AllowedGaps { get; set; } = 1;
    public DateTime? StartDate { get; set; }
}

public class HabitLogDTO
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public int Value { get; set; }
    public int Target { get; set; }
    public int DailyKey { get; set; }
    public int WeeklyKey { get; set; }
    public int MonthlyKey { get; set; }
} 
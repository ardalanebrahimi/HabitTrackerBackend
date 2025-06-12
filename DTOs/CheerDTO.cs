using System.ComponentModel.DataAnnotations;

public class CheerDTO
{
    public Guid Id { get; set; }
    public Guid HabitId { get; set; }
    public required string HabitName { get; set; }
    public Guid FromUserId { get; set; }
    public required string FromUserName { get; set; }
    public Guid ToUserId { get; set; }
    public required string ToUserName { get; set; }
    public required string Emoji { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateCheerRequest
{
    [Required]
    public Guid HabitId { get; set; }

    [Required]
    public Guid ToUserId { get; set; }

    [Required]
    [MaxLength(10)]
    public required string Emoji { get; set; }

    [MaxLength(500)]
    public string? Message { get; set; }
}

public class CheerSummaryDTO
{
    public int TotalCheersSent { get; set; }
    public int TotalCheersReceived { get; set; }
    public int CheersReceivedToday { get; set; }
    public int CheersSentToday { get; set; }
    public List<string> TopEmojisUsed { get; set; } = new List<string>();
    public List<string> TopEmojisReceived { get; set; } = new List<string>();
}

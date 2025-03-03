using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("connections")]
public class Connection
{
    [Key] public Guid Id { get; set; }
    [Required] public Guid UserId { get; set; }
    [Required] public Guid ConnectedUserId { get; set; }
    [Required] public ConnectionStatus Status { get; set; } = ConnectionStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum ConnectionStatus
{
    Pending,
    Approved,
    Rejected
}

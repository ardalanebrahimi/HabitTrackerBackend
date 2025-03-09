using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("connections")]
public class Connection
{
    [Key] public Guid Id { get; set; }

    [Required]
    [ForeignKey("User")] 
    public Guid UserId { get; set; }

    [Required]
    [ForeignKey("ConnectedUser")] 
    public Guid ConnectedUserId { get; set; }

    [Required] public ConnectionStatus Status { get; set; } = ConnectionStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


    // ✅ Navigation Properties
    public virtual User RequesterUser { get; set; } = null!;
    public virtual User ReceiverUser { get; set; } = null!;
}

public enum ConnectionStatus
{
    Pending,
    Approved,
    Rejected
}

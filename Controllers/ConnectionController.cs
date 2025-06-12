using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("api/[controller]")]
public class ConnectionController : ControllerBase
{
    private readonly AppDbContext _context;

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
        {
            throw new UnauthorizedAccessException("Invalid or missing User ID.");
        }
        return userId;
    }

    public ConnectionController(AppDbContext context)
    {
        _context = context;
    }    // ✅ Get List of Approved Connections
    [HttpGet("list")]
    public async Task<IActionResult> GetConnections()
    {
        var userId = GetUserId();

        var connections = await _context.Connections
            .Where(c => c.UserId == userId && c.Status == ConnectionStatus.Approved)
            .Select(c => c.ConnectedUserId)
            .ToListAsync();

        var connectedUsers = await _context.Users
            .Where(u => connections.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName })
            .ToListAsync();

        return Ok(connectedUsers);
    }


    // ✅ Search Users by Username
    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest(new { message = "Username is required" });
        }

        var users = await _context.Users
            .Where(u => u.UserName.ToLower().Contains(username.ToLower()) && u.Id != GetUserId())
            .Select(u => new { u.Id, u.UserName })
            .ToListAsync();

        return Ok(users);
    }

    // ✅ Send Connection Request
    [HttpPost("request")]
    public async Task<IActionResult> SendConnectionRequest([FromBody] ConnectionRequest request)
    {
        if (request.ConnectedUserId == GetUserId())
        {
            return BadRequest(new { message = "You cannot send a request to yourself." });
        }

        var existingConnection = await _context.Connections
            .FirstOrDefaultAsync(c =>
                ((c.UserId == GetUserId() && c.ConnectedUserId == request.ConnectedUserId) ||
                (c.UserId == request.ConnectedUserId && c.ConnectedUserId == GetUserId()))
                && c.Status != ConnectionStatus.Rejected);

        if (existingConnection != null)
        {
            return BadRequest(new { message = "You already have a connection or pending request." });
        }        var requester = await _context.Users.FindAsync(GetUserId());
        var newConnection = new Connection
        {
            Id = Guid.NewGuid(),
            UserId = GetUserId(),
            ConnectedUserId = request.ConnectedUserId,
            Status = ConnectionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        // Create notification for the recipient
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = request.ConnectedUserId,
            Type = NotificationType.ConnectionRequest,
            Title = "New Connection Request",
            Message = $"{requester?.UserName ?? "Someone"} wants to connect with you",
            Data = JsonSerializer.Serialize(new { ConnectionId = newConnection.Id }),
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Connections.Add(newConnection);
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Connection request sent." });
    }

    // ✅ Send Connection Request by UserId (alternative endpoint for frontend compatibility)
    [HttpPost("request/{userId}")]
    public async Task<IActionResult> SendConnectionRequestByUserId(Guid userId)
    {
        if (userId == GetUserId())
        {
            return BadRequest(new { message = "You cannot send a request to yourself." });
        }

        var existingConnection = await _context.Connections
            .FirstOrDefaultAsync(c =>
                ((c.UserId == GetUserId() && c.ConnectedUserId == userId) ||
                (c.UserId == userId && c.ConnectedUserId == GetUserId()))
                && c.Status != ConnectionStatus.Rejected);

        if (existingConnection != null)
        {
            return BadRequest(new { message = "You already have a connection or pending request." });
        }

        var requester = await _context.Users.FindAsync(GetUserId());
        var newConnection = new Connection
        {
            Id = Guid.NewGuid(),
            UserId = GetUserId(),
            ConnectedUserId = userId,
            Status = ConnectionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        // Create notification for the recipient
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.ConnectionRequest,
            Title = "New Connection Request",
            Message = $"{requester!.UserName} wants to connect with you",
            Data = JsonSerializer.Serialize(new { ConnectionId = newConnection.Id }),
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Connections.Add(newConnection);
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Connection request sent." });
    }    // ✅ Get Incoming Requests
    [HttpGet("incoming")]
    public async Task<IActionResult> GetIncomingRequests()
    {
        var userId = GetUserId();

        var requests = await _context.Connections
            .Where(c => c.ConnectedUserId == userId && c.Status == ConnectionStatus.Pending)
            .Include(c => c.RequesterUser)
            .Select(c => new { c.Id, c.RequesterUser.UserName, c.RequesterUser.Email })
            .ToListAsync();

        return Ok(requests);
    }

    // ✅ Get Sent Requests    [HttpGet("sent")]
    [HttpGet("sent")]
    public async Task<IActionResult> GetSentRequests()
    {
        var userId = GetUserId();

        var requests = await _context.Connections
            .Where(c => c.UserId == userId && c.Status == ConnectionStatus.Pending)
            .Include(c => c.ReceiverUser)
            .Select(c => new { c.Id, c.ReceiverUser.UserName, c.ReceiverUser.Email })
            .ToListAsync();

        return Ok(requests);
    }

    // ✅ Accept Connection Request
    [HttpPost("accept/{id}")]
    public async Task<IActionResult> AcceptRequest(Guid id)
    {
        var request = await _context.Connections.FindAsync(id);
        if (request == null || request.ConnectedUserId != GetUserId()) return NotFound();

        // ✅ Update request status
        request.Status = ConnectionStatus.Approved;

        // ✅ Create the reverse connection record
        var reverseConnection = new Connection
        {
            UserId = request.ConnectedUserId, // Now the receiver becomes the sender
            ConnectedUserId = request.UserId, // Original sender becomes receiver
            Status = ConnectionStatus.Approved,
            CreatedAt = DateTime.UtcNow
        };

        _context.Connections.Add(reverseConnection);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Connection accepted" });
    }


    // ✅ Reject Connection Request
    [HttpPost("reject/{id}")]
    public async Task<IActionResult> RejectRequest(Guid id)
    {
        var request = await _context.Connections.FindAsync(id);
        if (request == null || request.ConnectedUserId != GetUserId()) return NotFound();

        request.Status = ConnectionStatus.Rejected;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Connection rejected" });
    }

    [HttpPost("check-request")]
    public async Task<IActionResult> RequestHabitCheck([FromBody] HabitCheckRequestDTO request)
    {
        var userId = GetUserId();

        // Verify the habit exists and belongs to the requesting user
        var habit = await _context.Habits
            .FirstOrDefaultAsync(h => h.Id == request.HabitId && h.UserId == userId);

        if (habit == null)
        {
            return NotFound(new { message = "Habit not found or you don't have access to it." });
        }

        // Verify all requested users exist and are connected
        var connectedUserIds = await _context.Connections
            .Where(c => c.UserId == userId && c.Status == ConnectionStatus.Approved)
            .Select(c => c.ConnectedUserId)
            .ToListAsync();

        var invalidUserIds = request.UserIds
            .Where(id => !connectedUserIds.Contains(Guid.Parse(id)))
            .ToList();

        if (invalidUserIds.Any())
        {
            return BadRequest(new { 
                message = "Some users are not connected to you.", 
                invalidUserIds 
            });
        }

        var requester = await _context.Users.FindAsync(userId);
        var notifications = new List<Notification>();
        var checkRequests = new List<HabitCheckRequest>();

        // Create check requests and notifications for each user
        foreach (var requestedUserId in request.UserIds)
        {
            var checkRequest = new HabitCheckRequest
            {
                Id = Guid.NewGuid(),
                HabitId = request.HabitId,
                RequesterId = userId,
                RequestedUserId = Guid.Parse(requestedUserId),
                Status = CheckRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Parse(requestedUserId),
                Type = NotificationType.HabitCheckRequest,
                Title = "New Habit Check Request",
                Message = $"{requester?.UserName ?? "Someone"} asked you to verify their habit progress",
                Data = JsonSerializer.Serialize(new { 
                    HabitCheckRequestId = checkRequest.Id,
                    HabitId = habit.Id,
                    HabitName = habit.Name
                }),
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            checkRequests.Add(checkRequest);
            notifications.Add(notification);
        }

        _context.HabitCheckRequests.AddRange(checkRequests);
        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync();

        return Ok(new { 
            message = "Check requests sent successfully",
            requestCount = checkRequests.Count
        });
    }
}


// ✅ DTO Model for Connection Request
public class ConnectionRequest
{
    public Guid ConnectedUserId { get; set; }
}

public class HabitCheckRequestDTO
{
    public Guid HabitId { get; set; }
    public required string[] UserIds { get; set; }
}
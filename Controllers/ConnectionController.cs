using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
    }

    // ✅ Get List of Approved Connections
    [HttpGet("list")]
    public async Task<IActionResult> GetConnections()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var connections = await _context.Connections
            .Where(c => c.UserId == Guid.Parse(userId) && c.Status == ConnectionStatus.Approved)
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
                (c.UserId == GetUserId() && c.ConnectedUserId == request.ConnectedUserId) ||
                (c.UserId == request.ConnectedUserId && c.ConnectedUserId == GetUserId()));

        if (existingConnection != null)
        {
            return BadRequest(new { message = "You already have a connection or pending request." });
        }

        var newConnection = new Connection
        {
            UserId = GetUserId(),
            ConnectedUserId = request.ConnectedUserId
        };

        _context.Connections.Add(newConnection);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Connection request sent." });
    }
}


// ✅ DTO Model for Connection Request
public class ConnectionRequest
{
    public Guid ConnectedUserId { get; set; }
}
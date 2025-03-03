using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
public class ConnectionController : ControllerBase
{
    private readonly AppDbContext _context;

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
}

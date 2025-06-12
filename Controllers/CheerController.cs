using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CheerController : ControllerBase
{
    private readonly AppDbContext _context;

    public CheerController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
        {
            throw new UnauthorizedAccessException("Invalid or missing User ID.");
        }
        return userId;
    }

    // POST /api/cheer - Send cheer to friend
    [HttpPost]
    public async Task<IActionResult> SendCheer([FromBody] CreateCheerRequest request)
    {
        var fromUserId = GetUserId();

        // Validate that the habit exists
        var habit = await _context.Habits
            .Include(h => h.User)
            .FirstOrDefaultAsync(h => h.Id == request.HabitId);

        if (habit == null)
        {
            return NotFound(new { message = "Habit not found." });
        }

        // Validate that the ToUserId is the habit owner
        if (habit.UserId != request.ToUserId)
        {
            return BadRequest(new { message = "You can only cheer the habit owner." });
        }

        // Validate that users are connected (friends)
        var areConnected = await _context.Connections
            .AnyAsync(c => ((c.UserId == fromUserId && c.ConnectedUserId == request.ToUserId) ||
                           (c.UserId == request.ToUserId && c.ConnectedUserId == fromUserId)) &&
                          c.Status == ConnectionStatus.Approved);

        if (!areConnected)
        {
            return BadRequest(new { message = "You can only cheer friends." });
        }

        // Check if user already cheered this habit today
        var today = DateTime.UtcNow.Date;
        var existingCheer = await _context.Cheers
            .AnyAsync(c => c.HabitId == request.HabitId && 
                          c.FromUserId == fromUserId && 
                          c.CreatedAt.Date == today);

        if (existingCheer)
        {
            return BadRequest(new { message = "You have already cheered this habit today." });
        }

        // Get the from user for notification
        var fromUser = await _context.Users.FindAsync(fromUserId);

        // Create the cheer
        var cheer = new Cheer
        {
            Id = Guid.NewGuid(),
            HabitId = request.HabitId,
            FromUserId = fromUserId,
            ToUserId = request.ToUserId,
            Emoji = request.Emoji,
            Message = request.Message,
            CreatedAt = DateTime.UtcNow
        };

        // Create notification for the recipient
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = request.ToUserId,
            Type = NotificationType.CheerReceived,
            Title = "New Cheer Received!",
            Message = $"{fromUser!.UserName} cheered you on your habit: {habit.Name}",
            Data = JsonSerializer.Serialize(new { 
                CheerId = cheer.Id,
                HabitId = habit.Id,
                HabitName = habit.Name,
                FromUserName = fromUser.UserName,
                Emoji = request.Emoji,
                Message = request.Message
            }),
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Cheers.Add(cheer);
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        return Ok(new { 
            message = "Cheer sent successfully!",
            cheerId = cheer.Id
        });
    }

    // GET /api/cheer/habit/{habitId} - Get cheers for specific habit
    [HttpGet("habit/{habitId}")]
    public async Task<ActionResult<IEnumerable<CheerDTO>>> GetCheersForHabit(Guid habitId)
    {
        var userId = GetUserId();

        // Verify habit exists and user has access to view it
        var habit = await _context.Habits
            .Include(h => h.User)
            .FirstOrDefaultAsync(h => h.Id == habitId);

        if (habit == null)
        {
            return NotFound(new { message = "Habit not found." });
        }

        // Check if user owns the habit or is connected to the owner
        bool hasAccess = habit.UserId == userId;
        if (!hasAccess)
        {
            hasAccess = await _context.Connections
                .AnyAsync(c => ((c.UserId == userId && c.ConnectedUserId == habit.UserId) ||
                               (c.UserId == habit.UserId && c.ConnectedUserId == userId)) &&
                              c.Status == ConnectionStatus.Approved);
        }

        if (!hasAccess)
        {
            return Forbid("You don't have access to view cheers for this habit.");
        }        var cheers = await _context.Cheers
            .Where(c => c.HabitId == habitId)
            .Include(c => c.FromUser)
            .Include(c => c.ToUser)
            .Include(c => c.Habit)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CheerDTO
            {
                Id = c.Id,
                HabitId = c.HabitId,
                HabitName = c.Habit.Name,
                FromUserId = c.FromUserId,
                FromUserName = c.FromUser.UserName ?? "",
                ToUserId = c.ToUserId,
                ToUserName = c.ToUser.UserName ?? "",
                Emoji = c.Emoji,
                Message = c.Message,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return Ok(cheers);
    }

    // GET /api/cheer/received - Get user's received cheers
    [HttpGet("received")]
    public async Task<ActionResult<IEnumerable<CheerDTO>>> GetReceivedCheers()
    {
        var userId = GetUserId();        var cheers = await _context.Cheers
            .Where(c => c.ToUserId == userId)
            .Include(c => c.FromUser)
            .Include(c => c.ToUser)
            .Include(c => c.Habit)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CheerDTO
            {
                Id = c.Id,
                HabitId = c.HabitId,
                HabitName = c.Habit.Name ?? "",
                FromUserId = c.FromUserId,
                FromUserName = c.FromUser.UserName ?? "",
                ToUserId = c.ToUserId,
                ToUserName = c.ToUser.UserName ?? "",
                Emoji = c.Emoji,
                Message = c.Message,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return Ok(cheers);
    }

    // GET /api/cheer/sent - Get user's sent cheers
    [HttpGet("sent")]
    public async Task<ActionResult<IEnumerable<CheerDTO>>> GetSentCheers()
    {
        var userId = GetUserId();
        var cheers = await _context.Cheers
            .Where(c => c.FromUserId == userId)
            .Include(c => c.FromUser)
            .Include(c => c.ToUser)
            .Include(c => c.Habit)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CheerDTO
            {
                Id = c.Id,
                HabitId = c.HabitId,
                HabitName = c.Habit.Name ?? "",
                FromUserId = c.FromUserId,
                FromUserName = c.FromUser.UserName ?? "",
                ToUserId = c.ToUserId,
                ToUserName = c.ToUser.UserName ?? "",
                Emoji = c.Emoji,
                Message = c.Message,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return Ok(cheers);
    }

    // GET /api/cheer/summary - Get cheer statistics
    [HttpGet("summary")]
    public async Task<ActionResult<CheerSummaryDTO>> GetCheerSummary()
    {
        var userId = GetUserId();
        var today = DateTime.UtcNow.Date;

        var totalSent = await _context.Cheers.CountAsync(c => c.FromUserId == userId);
        var totalReceived = await _context.Cheers.CountAsync(c => c.ToUserId == userId);
        var sentToday = await _context.Cheers.CountAsync(c => c.FromUserId == userId && c.CreatedAt.Date == today);
        var receivedToday = await _context.Cheers.CountAsync(c => c.ToUserId == userId && c.CreatedAt.Date == today);

        // Get top emojis used (sent)
        var topEmojisUsed = await _context.Cheers
            .Where(c => c.FromUserId == userId)
            .GroupBy(c => c.Emoji)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToListAsync();

        // Get top emojis received
        var topEmojisReceived = await _context.Cheers
            .Where(c => c.ToUserId == userId)
            .GroupBy(c => c.Emoji)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToListAsync();

        var summary = new CheerSummaryDTO
        {
            TotalCheersSent = totalSent,
            TotalCheersReceived = totalReceived,
            CheersSentToday = sentToday,
            CheersReceivedToday = receivedToday,
            TopEmojisUsed = topEmojisUsed,
            TopEmojisReceived = topEmojisReceived
        };

        return Ok(summary);
    }

    // DELETE /api/cheer/{cheerId} - Delete cheer (optional - for cleanup)
    [HttpDelete("{cheerId}")]
    public async Task<IActionResult> DeleteCheer(Guid cheerId)
    {
        var userId = GetUserId();

        var cheer = await _context.Cheers
            .FirstOrDefaultAsync(c => c.Id == cheerId && c.FromUserId == userId);

        if (cheer == null)
        {
            return NotFound(new { message = "Cheer not found or you don't have permission to delete it." });
        }

        _context.Cheers.Remove(cheer);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Cheer deleted successfully." });
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[Authorize]
[ApiController]
[Route("api/habits")]
public class HabitsController : ControllerBase
{
    private readonly AppDbContext _context;

    public HabitsController(AppDbContext context)
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

    // ✅ Get today's habits including progress
    [HttpGet("today")]
    public async Task<ActionResult<IEnumerable<Habit>>> GetTodayHabits()
    {
        var userId = GetUserId();
        var today = DateTime.UtcNow.Date;

        var habits = await _context.Habits
            .Include(h => h.Logs)
            .Where(h => h.UserId == userId)
            .ToListAsync();

        var filteredHabits = habits.Where(h =>
            h.Frequency == "daily" ||
            (h.Frequency == "weekly" && today >= GetStartOfWeek() && !HasMetWeeklyGoal(h)) ||
            (h.Frequency == "monthly" && today >= GetStartOfMonth() && !HasMetMonthlyGoal(h))
        ).ToList();

        return filteredHabits;
    }

    private DateTime GetStartOfWeek()
    {
        var today = DateTime.UtcNow.Date;
        return today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
    }

    private DateTime GetStartOfMonth()
    {
        return new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
    }

    private bool HasMetWeeklyGoal(Habit habit)
    {
        var startOfWeek = GetStartOfWeek();
        return habit.Logs.Any(log => log.Date >= startOfWeek);
    }

    private bool HasMetMonthlyGoal(Habit habit)
    {
        var startOfMonth = GetStartOfMonth();
        return habit.Logs.Any(log => log.Date >= startOfMonth);
    }

    // ✅ Mark Habit as Completed (+1) or Decrease Progress (-1)
    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompleteHabit(Guid id, [FromBody] bool decrease = false)
    {
        var userId = GetUserId();
        var habit = await _context.Habits.Include(h => h.Logs).FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);
        if (habit == null) return NotFound("Habit not found.");

        var today = DateTime.UtcNow.Date;
        var existingLog = habit.Logs.FirstOrDefault(l => l.Date == today);

        if (!decrease)
        {
            if (existingLog == null)
            {
                habit.Logs.Add(new HabitLog
                {
                    Id = Guid.NewGuid(),
                    HabitId = id,
                    Date = today,
                    Value = 1
                });
            }
            else
            {
                existingLog.Value++; // Only increase by 1 per call
            }
        }
        else
        {
            if (existingLog != null && existingLog.Value > 0)
            {
                existingLog.Value--; // Decrease progress by 1
                if (existingLog.Value == 0)
                {
                    _context.HabitLogs.Remove(existingLog); // Remove log if value reaches 0
                }
            }
        }

        await _context.SaveChangesAsync();
        return Ok(habit);
    }
}

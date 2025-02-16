using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
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

    [HttpGet("today")]
    public async Task<ActionResult<IEnumerable<HabitWithProgressDTO>>> GetTodayHabits()
    {
        var userId = GetUserId();
        var today = DateTime.UtcNow;
        var weekIdentifier = GetPeriodKey("weekly", today);
        var monthIdentifier = GetPeriodKey("monthly", today);

        var habits = await _context.Habits
            .Where(h => h.UserId == userId)
            .ToListAsync();

        var filteredHabits = habits
            .Where(h =>
                h.Frequency == "daily" ||
                (h.Frequency == "weekly" && !HasMetGoal(h.Id, weekIdentifier)) ||
                (h.Frequency == "monthly" && !HasMetGoal(h.Id, monthIdentifier))
            )
            .Select(h => new HabitWithProgressDTO
            {
                Id = h.Id ?? Guid.Empty,
                Name = h.Name,
                Frequency = h.Frequency,
                GoalType = h.GoalType,
                TargetValue = h.TargetValue,
                CurrentValue = GetCurrentProgress(h.Id ?? Guid.Empty, h.Frequency, today) // ✅ Dynamically calculated
            })
            .ToList();

        return filteredHabits;
    }

    // ✅ Helper method to calculate `CurrentValue`
    private int GetCurrentProgress(Guid habitId, string frequency, DateTime now)
    {
        var periodKey = GetPeriodKey(frequency, now);
        return _context.HabitLogs
            .Where(l => l.HabitId == habitId && l.PeriodKey == periodKey)
            .Sum(l => l.Value); // ✅ Sum all values for the current period
    }

    // ✅ Checks if weekly/monthly goal is met
    private bool HasMetGoal(Guid? habitId, int periodKey)
    {
        return _context.HabitLogs.Any(l => l.HabitId == habitId && l.PeriodKey == periodKey);
    }

    // ✅ Helper method to determine the correct period
    private int GetPeriodIdentifier(string frequency, DateTime now)
    {
        return frequency switch
        {
            "daily" => int.Parse(now.ToString("yyyyMMdd")), // YYYYMMDD
            "weekly" => int.Parse(now.ToString("yyyy")) * 100 + ISOWeek.GetWeekOfYear(now), // YYYYWW
            "monthly" => int.Parse(now.ToString("yyyyMM")), // YYYYMM
            _ => throw new ArgumentException("Invalid frequency")
        };
    }
    public class HabitCompletionRequest
    {
        public bool Decrease { get; set; } = false;
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompleteHabit(Guid id, [FromBody] HabitCompletionRequest request)
    {
        var userId = GetUserId();
        var habit = await _context.Habits.FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);
        if (habit == null) return NotFound("Habit not found.");

        var now = DateTime.UtcNow;
        var periodKey = GetPeriodKey(habit.Frequency, now);

        var existingLog = await _context.HabitLogs
            .FirstOrDefaultAsync(l => l.HabitId == id && l.PeriodKey == periodKey);

        if (!request.Decrease)
        {
            if (existingLog != null)
            {
                existingLog.Value++; // ✅ Increase progress count in that period
            }
            else
            {
                var newLog = new HabitLog
                {
                    Id = Guid.NewGuid(),
                    HabitId = id,
                    Timestamp = now,
                    PeriodKey = periodKey,
                    Value = 1
                };
                _context.HabitLogs.Add(newLog);
            }
        }
        else
        {
            if (existingLog != null && existingLog.Value > 0)
            {
                existingLog.Value--; // ✅ Decrease progress by 1
                if (existingLog.Value == 0)
                {
                    _context.HabitLogs.Remove(existingLog); // ✅ Remove log if Value reaches 0
                }
            }
        }

        await _context.SaveChangesAsync();
        return Ok(habit);
    }


    private int GetPeriodKey(string frequency, DateTime now)
    {
        return frequency switch
        {
            "daily" => int.Parse(now.ToString("yyyyMMdd")), // ✅ YYYYMMDD
            "weekly" => int.Parse(now.ToString("yyyy")) * 100 + ISOWeek.GetWeekOfYear(now), // ✅ YYYYWW
            "monthly" => int.Parse(now.ToString("yyyyMM")), // ✅ YYYYMM
            _ => throw new ArgumentException("Invalid frequency")
        };
    }
}

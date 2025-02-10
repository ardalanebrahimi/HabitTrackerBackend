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

        if (string.IsNullOrEmpty(userIdClaim))
        {
            throw new UnauthorizedAccessException("User ID not found in token.");
        }

        if (!Guid.TryParse(userIdClaim, out Guid userId))
        {
            throw new UnauthorizedAccessException("Invalid User ID format in token.");
        }

        return userId;
    }

    // ✅ Get all habits for the logged-in user only
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Habit>>> GetHabits()
    {
        var userId = GetUserId();
        return await _context.Habits.Where(h => h.UserId == userId).ToListAsync();
    }

    [HttpPost]
    public async Task<IActionResult> AddHabit([FromBody] Habit habit)
    {
        if (habit == null)
        {
            return BadRequest("Habit data is missing.");
        }

        if (string.IsNullOrEmpty(habit.Name) || string.IsNullOrEmpty(habit.Frequency) || string.IsNullOrEmpty(habit.GoalType))
        {
            return BadRequest("All required fields must be provided.");
        }

        var userId = GetUserId(); // ✅ Ensure habit is linked to the current user
        habit.UserId = userId;

        //if (habit.Id == Guid.Empty) // 🛑 Ensure a new Guid is assigned if it's missing
        //{
        //    habit.Id = Guid.NewGuid();
        //}

        _context.Habits.Add(habit);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetHabits), new { id = habit.Id }, habit);
    }


    // ✅ Update a habit (Only if it belongs to the logged-in user)
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateHabit(Guid id, Habit habit)
    {
        var userId = GetUserId();
        var existingHabit = await _context.Habits.FindAsync(id);

        if (existingHabit == null || existingHabit.UserId != userId)
            return Unauthorized("You do not have permission to edit this habit.");

        existingHabit.Name = habit.Name;
        existingHabit.Frequency = habit.Frequency;
        existingHabit.GoalType = habit.GoalType;
        existingHabit.TargetValue = habit.TargetValue;
        existingHabit.CurrentValue = habit.CurrentValue;
        existingHabit.Streak = habit.Streak;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ✅ Delete a habit (Only if it belongs to the logged-in user)
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteHabit(Guid id)
    {
        var userId = GetUserId();
        var habit = await _context.Habits.FindAsync(id);

        if (habit == null || habit.UserId != userId)
            return Unauthorized("You do not have permission to delete this habit.");

        _context.Habits.Remove(habit);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // 2️⃣ Get Today's Habits
    [HttpGet("today")]
    public async Task<ActionResult<IEnumerable<Habit>>> GetTodayHabits()
    {
        var userId = GetUserId();
        var today = DateTime.UtcNow.Date;
        var habits = await _context.Habits
            .Include(h => h.Logs)
            .Where(h => h.UserId == userId)
            .Where(h => h.Frequency == "daily" || 
                        (h.Frequency == "weekly" && today.DayOfWeek == DayOfWeek.Monday) || 
                        (h.Frequency == "monthly" && today.Day == 1))
            .ToListAsync();
        return habits;
    }


    // 6️⃣ Mark Habit as Completed
    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompleteHabit(Guid id)
    {
        var habit = await _context.Habits.Include(h => h.Logs).FirstOrDefaultAsync(h => h.Id == id);
        if (habit == null) return NotFound();

        var today = DateTime.UtcNow.Date;
        if (!habit.Logs.Any(l => l.Date == today))
        {
            habit.Logs.Add(new HabitLog { Id = Guid.NewGuid(), HabitId = id, Date = today, Value = 1 });
            habit.Streak++;
        }

        await _context.SaveChangesAsync();
        return Ok(habit);
    }
}

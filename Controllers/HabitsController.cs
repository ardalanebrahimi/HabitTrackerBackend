using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[Authorize]
[ApiController]
[Route("api/habits")]
public class HabitsController : ControllerBase
{
    private readonly HabitService _habitService;

    public HabitsController(HabitService habitService)
    {
        _habitService = habitService;
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

    [HttpPost]
    public async Task<IActionResult> AddHabit([FromBody] CreateHabitDTO habitDto)
    {
        var userId = GetUserId(); // Get user ID from JWT token

        var newHabit = await _habitService.AddHabit(userId, habitDto); // ✅ Call service

        return CreatedAtAction(nameof(GetTodayHabits), new { id = newHabit.Id }, newHabit);
    }



    [HttpGet("today")]
    public async Task<ActionResult<IEnumerable<HabitWithProgressDTO>>> GetTodayHabits()
    {
        var userId = GetUserId();
        return await _habitService.GetTodayHabits(userId);
    }

    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<HabitWithProgressDTO>>> GetAllHabits()
    {
        var userId = GetUserId();
        return await _habitService.GetAllHabits(userId);
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompleteHabit(Guid id, [FromBody] HabitCompletionRequest request)
    {
        var userId = GetUserId();
        await _habitService.UpdateHabitProgress(userId, id, request.Decrease);
        return Ok();
    }
}

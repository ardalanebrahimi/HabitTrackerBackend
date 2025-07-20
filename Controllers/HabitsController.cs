using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Collections.Generic;

[Authorize]
[ApiController]
[Route("api/habits")]
public class HabitsController : ControllerBase
{
    private readonly HabitService _habitService;
    private readonly ILogger<HabitsController> _logger;

    public HabitsController(HabitService habitService, ILogger<HabitsController> logger)
    {
        _habitService = habitService;
        _logger = logger;
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
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var userId = GetUserId();
        
        _logger.LogInformation("GetTodayHabits endpoint called for user {UserId}", userId);

        try
        {
            var result = await _habitService.GetAllTodayHabitsToManage(userId);
            stopwatch.Stop();
            
            _logger.LogInformation("GetTodayHabits endpoint completed in {ElapsedMs}ms for user {UserId}, returned {Count} habits", 
                stopwatch.ElapsedMilliseconds, userId, result.Value?.Count() ?? 0);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "GetTodayHabits endpoint failed after {ElapsedMs}ms for user {UserId}", 
                stopwatch.ElapsedMilliseconds, userId);
            throw;
        }
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
        await _habitService.UpdateHabitProgress(id, request.Decrease);
        return Ok();
    }

    [HttpPut("{id}/progress")]
    public async Task<IActionResult> UpdateHabitProgress(Guid id, [FromBody] HabitCompletionRequest request)
    {
        await _habitService.UpdateHabitProgress(id, request.Decrease);
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteHabit(Guid id)
    {
        var userId = GetUserId();
        var result = await _habitService.DeleteHabit(userId, id);

        if (!result)
        {
            return NotFound("Habit not found or you do not have permission to delete it.");
        }

        return NoContent();
    }

    [HttpPut("{id}/archive")]
    public async Task<IActionResult> ArchiveHabit(Guid id)
    {
        var userId = GetUserId();
        var result = await _habitService.ArchiveHabit(userId, id);

        if (!result)
        {
            return NotFound("Habit not found or already archived.");
        }

        return NoContent();
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<HabitWithProgressDTO>>> GetActiveHabits()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var userId = GetUserId();
        
        _logger.LogInformation("GetActiveHabits endpoint called for user {UserId}", userId);

        try
        {
            var result = await _habitService.GetAllHabits(userId, false);
            stopwatch.Stop();
            
            _logger.LogInformation("GetActiveHabits endpoint completed in {ElapsedMs}ms for user {UserId}, returned {Count} habits", 
                stopwatch.ElapsedMilliseconds, userId, result.Count);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "GetActiveHabits endpoint failed after {ElapsedMs}ms for user {UserId}", 
                stopwatch.ElapsedMilliseconds, userId);
            throw;
        }
    }

    [HttpGet("archived")]
    public async Task<ActionResult<IEnumerable<HabitWithProgressDTO>>> GetArchivedHabits()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var userId = GetUserId();
        
        _logger.LogInformation("GetArchivedHabits endpoint called for user {UserId}", userId);

        try
        {
            var result = await _habitService.GetAllHabits(userId, true);
            stopwatch.Stop();
            
            _logger.LogInformation("GetArchivedHabits endpoint completed in {ElapsedMs}ms for user {UserId}, returned {Count} habits", 
                stopwatch.ElapsedMilliseconds, userId, result.Count);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "GetArchivedHabits endpoint failed after {ElapsedMs}ms for user {UserId}", 
                stopwatch.ElapsedMilliseconds, userId);
            throw;
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateHabit(Guid id, [FromBody] CreateHabitDTO updatedHabit)
    {
        var userId = GetUserId();
        var updated = await _habitService.UpdateHabit(userId, id, updatedHabit);
        return Ok(updated);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<HabitWithProgressDTO>> GetHabitById(Guid id)
    {
        var userId = GetUserId();
        var habit = await _habitService.GetHabitById(userId, id);

        if (habit == null)
        {
            return NotFound("Habit not found.");
        }

        return Ok(habit);
    }

    // ✅ Get Friends' Habits
    [HttpGet("friends")]
    public async Task<ActionResult<IEnumerable<HabitWithProgressDTO>>> GetFriendsHabits()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var userId = GetUserId();
        
        _logger.LogInformation("GetFriendsHabits endpoint called for user {UserId}", userId);

        try
        {
            var result = await _habitService.GetFriendsHabits(userId);
            stopwatch.Stop();
            
            _logger.LogInformation("GetFriendsHabits endpoint completed in {ElapsedMs}ms for user {UserId}, returned {Count} habits", 
                stopwatch.ElapsedMilliseconds, userId, result.Count);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "GetFriendsHabits endpoint failed after {ElapsedMs}ms for user {UserId}", 
                stopwatch.ElapsedMilliseconds, userId);
            throw;
        }
    }

    [HttpGet("public")]
    public async Task<ActionResult<IEnumerable<HabitWithProgressDTO>>> GetPublicHabits([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var userId = GetUserId();
        
        _logger.LogInformation("GetPublicHabits endpoint called for user {UserId}, page {PageNumber}, size {PageSize}", userId, pageNumber, pageSize);

        try
        {
            var result = await _habitService.GetPublicHabits(userId, pageNumber, pageSize);
            stopwatch.Stop();
            
            _logger.LogInformation("GetPublicHabits endpoint completed in {ElapsedMs}ms for user {UserId}, returned {Count} habits", 
                stopwatch.ElapsedMilliseconds, userId, result.Count);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "GetPublicHabits endpoint failed after {ElapsedMs}ms for user {UserId}", 
                stopwatch.ElapsedMilliseconds, userId);
            throw;
        }
    }

    [HttpPost("{id}/copy")]
    public async Task<ActionResult<HabitWithProgressDTO>> CopyHabit(Guid id)
    {
        try
        {
            var userId = GetUserId();
            var copiedHabit = await _habitService.CopyHabit(userId, id);
            return Ok(copiedHabit);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id}/logs")]
    public async Task<ActionResult<IEnumerable<HabitLogDTO>>> GetHabitLogs(Guid id, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var userId = GetUserId();
            var start = startDate ?? DateTime.UtcNow.AddDays(-84);
            var end = endDate ?? DateTime.UtcNow;
            
            var logs = await _habitService.GetHabitLogs(userId, id, start, end);
            return Ok(logs);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }
}

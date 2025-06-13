using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProfileController1 : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly HabitService _habitService;

    public ProfileController1(AppDbContext context, HabitService habitService)
    {
        _context = context;
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

    // GET /api/profile/{userId} - Get user profile
    [HttpGet("{userId}")]
    public async Task<ActionResult<UserProfileDTO>> GetUserProfile(Guid userId)
    {
        var currentUserId = GetUserId();
        
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return NotFound("User not found.");
        }

        // Check if users are friends
        var isFriend = await _context.Connections
            .AnyAsync(c => ((c.UserId == currentUserId && c.ConnectedUserId == userId) ||
                          (c.UserId == userId && c.ConnectedUserId == currentUserId)) &&
                          c.Status == ConnectionStatus.Approved);

        var profile = new UserProfileDTO
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = currentUserId == userId || isFriend ? user.Email : null,
            JoinedDate = user.Id != Guid.Empty ? DateTime.UtcNow.AddMonths(-6) : DateTime.UtcNow, // Placeholder
            IsFriend = isFriend,
            IsCurrentUser = currentUserId == userId,
            // ProfileViewCount = 0 // No profile view tracking
        };

        // Get public habits (visible to everyone)
        var publicHabits = await _context.Habits
            .Where(h => h.UserId == userId && !h.IsArchived && !h.IsPrivate)
            .Include(h => h.User)
            .ToListAsync();

        profile.PublicHabits = await _habitService.GetTodayHabits(publicHabits);

        // Get friend habits (visible to friends only)
        if (isFriend || currentUserId == userId)
        {
            var allHabits = await _context.Habits
                .Where(h => h.UserId == userId && !h.IsArchived)
                .Include(h => h.User)
                .ToListAsync();

            profile.FriendHabits = await _habitService.GetTodayHabits(allHabits.Where(h => h.IsPrivate).ToList());
        }

        // Get analytics (visible to friends and owner)
        if (isFriend || currentUserId == userId)
        {
            profile.Analytics = await GetProfileAnalytics(userId);
        }

        return Ok(profile);
    }

    // GET /api/profile/{userId}/analytics - Get detailed analytics
    [HttpGet("{userId}/analytics")]
    public async Task<ActionResult<ProfileAnalyticsDTO>> GetUserAnalytics(Guid userId)
    {
        var currentUserId = GetUserId();
        
        // Check if user has permission to view analytics
        var isFriend = await _context.Connections
            .AnyAsync(c => ((c.UserId == currentUserId && c.ConnectedUserId == userId) ||
                          (c.UserId == userId && c.ConnectedUserId == currentUserId)) &&
                          c.Status == ConnectionStatus.Approved);

        if (!isFriend && currentUserId != userId)
        {
            return Forbid("You don't have permission to view this user's analytics.");
        }

        var analytics = await GetProfileAnalytics(userId);
        return Ok(analytics);
    }

    // GET /api/profile/discover - Discover public profiles
    [HttpGet("discover")]
    public async Task<ActionResult<IEnumerable<UserProfileDTO>>> DiscoverProfiles([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var currentUserId = GetUserId();
        
        // Get connected friends
        var connectedUserIds = await _context.Connections
            .Where(c => c.UserId == currentUserId && c.Status == ConnectionStatus.Approved)
            .Select(c => c.ConnectedUserId)
            .ToListAsync();

        // Get users who are not friends and have public habits
        var users = await _context.Users
            .Where(u => u.Id != currentUserId && !connectedUserIds.Contains(u.Id))
            .Where(u => _context.Habits.Any(h => h.UserId == u.Id && !h.IsPrivate && !h.IsArchived))
            .OrderBy(u => u.UserName) // Simple ordering by name instead of profile views
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var profiles = new List<UserProfileDTO>();

        foreach (var user in users)
        {
            var publicHabits = await _context.Habits
                .Where(h => h.UserId == user.Id && !h.IsArchived && !h.IsPrivate)
                .Include(h => h.User)
                .Take(3) // Show only top 3 habits
                .ToListAsync();

            profiles.Add(new UserProfileDTO
            {
                Id = user.Id,
                UserName = user.UserName,
                JoinedDate = DateTime.UtcNow.AddMonths(-6), // Placeholder
                IsFriend = false,
                IsCurrentUser = false,
                // ProfileViewCount = 0, // No profile view tracking
                PublicHabits = await _habitService.GetTodayHabits(publicHabits)
            });
        }

        return Ok(profiles);
    }

    private async Task<ProfileAnalyticsDTO> GetProfileAnalytics(Guid userId)
    {
        var habits = await _context.Habits
            .Where(h => h.UserId == userId)
            .ToListAsync();

        var totalHabits = habits.Count;
        var activeHabits = habits.Count(h => !h.IsArchived);
        
        var today = DateTime.UtcNow;
        var completedToday = 0;
        var longestStreak = 0;
        
        foreach (var habit in habits.Where(h => !h.IsArchived))
        {
            var streak = _habitService.CalculateStreak(habit.Id ?? Guid.Empty, habit.Frequency, today);
            if (streak > longestStreak) longestStreak = streak;
            
            var isCompleted = _habitService.IsHabitCompleted(habit.Id ?? Guid.Empty, habit.Frequency, today);
            if (isCompleted) completedToday++;
        }

        // Calculate success rate (placeholder calculation)
        var totalLogs = await _context.HabitLogs
            .CountAsync(hl => habits.Select(h => h.Id).Contains(hl.HabitId));
        
        var successfulLogs = await _context.HabitLogs
            .CountAsync(hl => habits.Select(h => h.Id).Contains(hl.HabitId) && hl.Value > 0);

        var successRate = totalLogs > 0 ? (double)successfulLogs / totalLogs * 100 : 0;

        return new ProfileAnalyticsDTO
        {
            TotalHabits = totalHabits,
            ActiveHabits = activeHabits,
            CompletedToday = completedToday,
            LongestStreak = longestStreak,
            // TotalProfileViews = 0, // No profile view tracking
            // FriendProfileViews = 0, // No profile view tracking
            SuccessRate = Math.Round(successRate, 1),
            TopCategories = new List<string> { "Health", "Productivity", "Learning" } // Placeholder
        };
    }
}

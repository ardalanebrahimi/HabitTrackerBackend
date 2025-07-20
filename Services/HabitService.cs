using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Diagnostics;

public class HabitService
{
    private readonly AppDbContext _context;
    private readonly ILogger<HabitService> _logger;

    public HabitService(AppDbContext context, ILogger<HabitService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HabitWithProgressDTO> AddHabit(Guid userId, CreateHabitDTO habitDto)
    {
        if (string.IsNullOrWhiteSpace(habitDto.Name))
        {
            throw new ArgumentException("Habit name is required.");
        }

        var newHabit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = habitDto.Name,
            Description = habitDto.Description,
            Frequency = habitDto.Frequency,
            GoalType = habitDto.GoalType,
            TargetValue = habitDto.TargetValue,
            StreakTarget = habitDto.StreakTarget,
            EndDate = habitDto.EndDate?.ToUniversalTime(),
            AllowedGaps = habitDto.AllowedGaps,
            TargetType = habitDto.TargetType,
            StartDate = habitDto.StartDate?.ToUniversalTime() ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _context.Habits.Add(newHabit);
        await _context.SaveChangesAsync();

        return new HabitWithProgressDTO
        {
            Id = newHabit.Id ?? Guid.Empty,
            Name = newHabit.Name,
            Description = newHabit.Description,
            Frequency = newHabit.Frequency,
            GoalType = newHabit.GoalType,
            TargetValue = newHabit.TargetValue,
            TargetType = newHabit.TargetType,
            StreakTarget = newHabit.StreakTarget,
            EndDate = newHabit.EndDate,
            CurrentValue = 0,
            Streak = 0,
            IsCompleted = false
        };
    }

    // ✅ Get All Habits with Current Progress & Streaks
    public async Task<List<HabitWithProgressDTO>> GetAllHabits(Guid userId)
    {
        var today = DateTime.UtcNow;

        var habits = await _context.Habits
            .Where(h => h.UserId == userId && !h.IsArchived)
            .ToListAsync();

        return habits.Select(h => new HabitWithProgressDTO
        {
            Id = h.Id ?? Guid.Empty,
            Name = h.Name,
            Frequency = h.Frequency,
            GoalType = h.GoalType,
            TargetValue = h.TargetValue,
            CurrentValue = GetCurrentProgress(h.Id ?? Guid.Empty, h.Frequency, today),
            Streak = CalculateStreak(h.Id ?? Guid.Empty, h.Frequency, today),
            IsCompleted = IsHabitCompleted(h.Id ?? Guid.Empty, h.Frequency, today)
        }).ToList();
    }

    public async Task<List<HabitWithProgressDTO>> GetTodayHabits(List<Habit> habits)
    {
        _logger.LogInformation("GetTodayHabits started with {Count} habits", habits.Count);
        var stopwatch = Stopwatch.StartNew();

        var today = DateTime.UtcNow;
        var currentWeekKey = GetPeriodKey("weekly", today);  // Get the current week identifier
        var currentMonthKey = GetPeriodKey("monthly", today); // Get the current month identifier

        var result = habits
            .Where(h => !HasReachedCompletion(h.Id, h.StreakTarget, h.EndDate) && 
                (!h.StartDate.HasValue || h.StartDate.Value.Date <= today.Date) &&
                (h.Frequency == "daily" ||
                (h.Frequency == "weekly" && IsHabitInCurrentWeek(h.Id, currentWeekKey)) ||
                (h.Frequency == "monthly" && IsHabitInCurrentMonth(h.Id, currentMonthKey))))
            .Select(h => new HabitWithProgressDTO
            {
                Id = h.Id ?? Guid.Empty,
                Name = h.Name,
                Frequency = h.Frequency,
                GoalType = h.GoalType,
                TargetValue = h.TargetValue,
                CurrentValue = GetCurrentProgress(h.Id ?? Guid.Empty, h.Frequency, today),
                Streak = CalculateStreak(h.Id ?? Guid.Empty, h.Frequency, today),
                IsCompleted = IsHabitCompleted(h.Id ?? Guid.Empty, h.Frequency, today),
                UserId = h.UserId,
                UserName = h.User?.UserName
            })
            .ToList();

        stopwatch.Stop();
        _logger.LogInformation("GetTodayHabits completed in {ElapsedMs}ms, returned {Count} habits", stopwatch.ElapsedMilliseconds, result.Count);
        
        return result;
    }

    // ✅ Check if Habit is Completed by Streak or End Date
    private bool HasReachedCompletion(Guid? habitId, int? streakTarget, DateTime? endDate)
    {
        if (streakTarget != null)
        {
            int currentStreak = CalculateStreak(habitId ?? Guid.Empty, "daily", DateTime.UtcNow);
            if (currentStreak >= streakTarget) return true; // ✅ Exclude if streak target is met
        }

        if (endDate != null && DateTime.UtcNow.Date > endDate.Value.Date)
        {
            return true; // ✅ Exclude if past end date
        }

        return false; // ✅ Habit is still active
    }

    // ✅ Check if a weekly habit is still in the current week
    private bool IsHabitInCurrentWeek(Guid? habitId, int currentWeekKey)
    {
        int progress = GetCurrentProgress(habitId ?? Guid.Empty, "weekly", DateTime.UtcNow);
        int? target = _context.Habits.Where(h => h.Id == habitId).Select(h => h.TargetValue).FirstOrDefault();

        // ✅ Keep showing the habit until the end of the week
        return progress < target || DateTime.UtcNow.DayOfWeek != DayOfWeek.Sunday;
    }

    // ✅ Keep monthly habits visible until the month ends
    private bool IsHabitInCurrentMonth(Guid? habitId, int currentMonthKey)
    {
        int progress = GetCurrentProgress(habitId ?? Guid.Empty, "monthly", DateTime.UtcNow);
        int? target = _context.Habits.Where(h => h.Id == habitId).Select(h => h.TargetValue).FirstOrDefault();

        // ✅ Keep showing the habit even if the goal is reached, but mark it as completed
        return progress < target || DateTime.UtcNow.Day < DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
    }

    public async Task UpdateHabitProgress(Guid habitId, bool decrease)
    {
        var habit = await _context.Habits
            .FirstOrDefaultAsync(h => h.Id == habitId);

        if (habit == null)
            throw new ArgumentException("Habit not found.");

        var now = DateTime.UtcNow;

        var newLog = new HabitLog
        {
            Id = Guid.NewGuid(),
            HabitId = habitId,
            Timestamp = now,
            DailyKey = GetPeriodKey("daily", now),
            WeeklyKey = GetPeriodKey("weekly", now),
            MonthlyKey = GetPeriodKey("monthly", now),
            Value = decrease ? -1 : 1, // +1 for progress, -1 for undo
            Target = habit.TargetValue ?? 1
        };

        _context.HabitLogs.Add(newLog);
        await _context.SaveChangesAsync();
    }

    private int GetCurrentProgress(Guid habitId, string frequency, DateTime now)
    {
        int periodKey = frequency switch
        {
            "daily" => GetPeriodKey("daily", now),
            "weekly" => GetPeriodKey("weekly", now),
            "monthly" => GetPeriodKey("monthly", now),
            _ => throw new ArgumentException("Invalid frequency")
        };

        // Select the correct period column dynamically
        var query = _context.HabitLogs.Where(l => l.HabitId == habitId);

        query = frequency switch
        {
            "daily" => query.Where(l => l.DailyKey == periodKey),
            "weekly" => query.Where(l => l.WeeklyKey == periodKey),
            "monthly" => query.Where(l => l.MonthlyKey == periodKey),
            _ => query
        };

        return query.Sum(l => l.Value);
    }

    public int CalculateStreak(Guid habitId, string frequency, DateTime now)
    {
        var habit = _context.Habits.FirstOrDefault(h => h.Id == habitId);
        if (habit == null) return 0;

        frequency = NormalizeFrequency(frequency);

        var periodKeys = GetHabitLogPeriods(habitId, frequency);
        if (!periodKeys.Any()) return 0; // No logs found, streak is 0

        int allowedGaps = (frequency == "daily") ? habit.AllowedGaps : 0;
        return ComputeStreak(periodKeys, frequency, now, allowedGaps);
    }

    /// ✅ Normalize frequency input
    private string NormalizeFrequency(string frequency)
    {
        if (string.IsNullOrWhiteSpace(frequency))
        {
            throw new ArgumentException("[ERROR] Frequency is NULL or empty.");
        }
        return frequency.Trim().ToLower();
    }

    /// ✅ Get the period keys from logs for the given habit
    private List<int> GetHabitLogPeriods(Guid habitId, string frequency)
    {
        var logsQuery = _context.HabitLogs.Where(l => l.HabitId == habitId);

        return frequency switch
        {
            "daily" => logsQuery.Select(l => l.DailyKey).Distinct().OrderByDescending(p => p).ToList(),
            "weekly" => logsQuery.Select(l => l.WeeklyKey).Distinct().OrderByDescending(p => p).ToList(),
            "monthly" => logsQuery.Select(l => l.MonthlyKey).Distinct().OrderByDescending(p => p).ToList(),
            _ => throw new ArgumentException($"[ERROR] Invalid frequency '{frequency}' for habit {habitId}")
        };
    }

    /// ✅ Compute the streak while considering allowed gaps (for daily only)
    private int ComputeStreak(List<int> periodKeys, string frequency, DateTime now, int allowedGaps)
    {
        int streak = 0;
        int expectedPeriod = GetPeriodKey(frequency, now);
        int gapCount = 0;

        foreach (var period in periodKeys)
        {
            if (period == expectedPeriod)
            {
                streak++;
                gapCount = 0; // ✅ Reset gaps since habit was logged
                expectedPeriod = GetPreviousPeriodKey(frequency, expectedPeriod);
            }
            else
            {
                gapCount++;
                if (gapCount > allowedGaps) break; // ✅ Streak breaks if gaps exceed limit
                expectedPeriod = GetPreviousPeriodKey(frequency, expectedPeriod);
            }
        }

        return streak;
    }

    private int GetPreviousPeriodKey(string frequency, int currentPeriod)
    {
        return frequency switch
        {
            "daily" => currentPeriod - 1,
            "weekly" =>
                currentPeriod % 100 > 1 // Check if it's not the first week of the year
                    ? currentPeriod - 1
                    : (currentPeriod / 100 - 1) * 100 + 52, // Wrap around to the last week of previous year
            "monthly" =>
                currentPeriod % 100 > 1 // Check if it's not January
                    ? currentPeriod - 1
                    : (currentPeriod / 100 - 1) * 100 + 12, // Wrap around to December of previous year
            _ => throw new ArgumentException("Invalid frequency")
        };
    }

    public bool IsHabitCompleted(Guid habitId, string frequency, DateTime now)
    {
        var periodKey = GetPeriodKey(frequency, now);
        var progress = GetCurrentProgress(habitId, frequency, now);

        var targetValue = _context.Habits
            .Where(h => h.Id == habitId)
            .Select(h => h.TargetValue ?? 1)
            .FirstOrDefault();

        return progress >= targetValue;
    }

    // ✅ Helper: Determine the Correct Period Key
    private int GetPeriodKey(string frequency, DateTime now)
    {
        return frequency switch
        {
            "daily" => int.Parse(now.ToString("yyyyMMdd")),
            "weekly" => int.Parse(now.ToString("yyyy")) * 100 + ISOWeek.GetWeekOfYear(now),
            "monthly" => int.Parse(now.ToString("yyyyMM")),
            _ => throw new ArgumentException("Invalid frequency")
        };
    }

    public async Task<bool> DeleteHabit(Guid userId, Guid habitId)
    {
        var habit = await _context.Habits
            .FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId);

        if (habit == null)
        {
            return false; // Habit not found or user does not own it
        }

        _context.Habits.Remove(habit);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ArchiveHabit(Guid userId, Guid habitId)
    {
        var habit = await _context.Habits
            .FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId);

        if (habit == null || habit.IsArchived)
        {
            return false;
        }

        habit.IsArchived = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<HabitWithProgressDTO>> GetAllHabits(Guid userId, bool archived)
    {
        _logger.LogInformation("GetAllHabits (original) called for user {UserId}, archived: {Archived}", userId, archived);
        
        // Use optimized version by default
        return await GetAllHabitsOptimized(userId, archived);
    }

    public async Task<HabitWithProgressDTO> UpdateHabit(Guid userId, Guid habitId, CreateHabitDTO updatedHabit)
    {
        var habit = await _context.Habits
            .FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId);

        if (habit == null)
            throw new ArgumentException("Habit not found.");

        if (habit.IsArchived)
            throw new InvalidOperationException("Archived habits cannot be edited.");

        habit.Name = updatedHabit.Name;
        habit.Description = updatedHabit.Description;
        habit.Frequency = updatedHabit.Frequency;
        habit.GoalType = updatedHabit.GoalType;
        habit.TargetValue = updatedHabit.TargetValue;
        habit.StreakTarget = updatedHabit.StreakTarget;
        habit.EndDate = updatedHabit.EndDate?.ToUniversalTime();
        habit.AllowedGaps = updatedHabit.AllowedGaps;
        habit.TargetType = updatedHabit.TargetType;
        habit.StartDate = updatedHabit.StartDate?.ToUniversalTime() ?? habit.StartDate;

        await _context.SaveChangesAsync();

        return new HabitWithProgressDTO
        {
            Id = habit.Id ?? Guid.Empty,
            Name = habit.Name,
            Description = habit.Description,
            Frequency = habit.Frequency,
            GoalType = habit.GoalType,
            TargetValue = habit.TargetValue,
            TargetType = habit.TargetType,
            StreakTarget = habit.StreakTarget,
            EndDate = habit.EndDate,
            CurrentValue = 0,
            Streak = 0,
            IsCompleted = false
        };
    }

    public async Task<List<HabitLogDTO>> GetHabitLogs(Guid userId, Guid habitId, DateTime startDate, DateTime endDate)
    {
        var habit = await _context.Habits
            .FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId);

        if (habit == null)
            throw new ArgumentException("Habit not found or you don't have access to it.");

        return await _context.HabitLogs
            .Where(l => l.HabitId == habitId && l.Timestamp >= startDate && l.Timestamp <= endDate)
            .OrderByDescending(l => l.Timestamp)
            .Select(l => new HabitLogDTO
            {
                Id = l.Id,
                Timestamp = l.Timestamp,
                Value = l.Value,
                Target = l.Target,
                DailyKey = l.DailyKey,
                WeeklyKey = l.WeeklyKey,
                MonthlyKey = l.MonthlyKey
            })
            .ToListAsync();
    }

    private async Task<List<HabitLogDTO>> GetRecentLogs(Guid habitId, string frequency, DateTime now)
    {
        var startDate = frequency switch
        {
            "daily" => now.AddDays(-7),
            "weekly" => now.AddDays(-7 * 7),
            "monthly" => now.AddMonths(-7),
            _ => throw new ArgumentException("Invalid frequency")
        };

        return await _context.HabitLogs
            .Where(l => l.HabitId == habitId && l.Timestamp >= startDate)
            .OrderByDescending(l => l.Timestamp)
            .Select(l => new HabitLogDTO
            {
                Id = l.Id,
                Timestamp = l.Timestamp,
                Value = l.Value,
                Target = l.Target,
                DailyKey = l.DailyKey,
                WeeklyKey = l.WeeklyKey,
                MonthlyKey = l.MonthlyKey
            })
            .ToListAsync();
    }

    public async Task<HabitWithProgressDTO?> GetHabitById(Guid userId, Guid habitId)
    {
        var habit = await _context.Habits
            .Where(h => h.Id == habitId)
            .Include(h => h.User) // Include the User entity
            .FirstOrDefaultAsync();

        if (habit == null) return null;

        var today = DateTime.UtcNow;
        var recentLogs = await GetRecentLogs(habitId, habit.Frequency, today);

        return new HabitWithProgressDTO
        {
            Id = habit.Id ?? Guid.Empty,
            Name = habit.Name,
            Description = habit.Description,
            Frequency = habit.Frequency,
            GoalType = habit.GoalType,
            TargetValue = habit.TargetValue,
            TargetType = habit.TargetType,
            StreakTarget = habit.StreakTarget,
            EndDate = habit.EndDate,
            CurrentValue = GetCurrentProgress(habit.Id ?? Guid.Empty, habit.Frequency, today),
            Streak = CalculateStreak(habit.Id ?? Guid.Empty, habit.Frequency, today),
            IsCompleted = IsHabitCompleted(habit.Id ?? Guid.Empty, habit.Frequency, today),
            RecentLogs = recentLogs,
            UserId = habit.UserId,
            UserName = habit.User.UserName,
            isOwnedHabit = habit.UserId == userId,
            CopyCount = habit.CopyCount
        };
    }

    public async Task<List<HabitWithProgressDTO>> GetFriendsHabits(Guid userId)
    {
        _logger.LogInformation("GetFriendsHabits (original) called for user {UserId}", userId);
        
        // Use optimized version by default
        return await GetFriendsHabitsOptimized(userId);
    }

    internal async Task<ActionResult<IEnumerable<HabitWithProgressDTO>>> GetAllTodayHabitsToManage(Guid userId)
    {
        var totalStopwatch = Stopwatch.StartNew();
        _logger.LogInformation("GetAllTodayHabitsToManage started for user {UserId}", userId);

        try
        {
            // Optimized version - fetch all data in fewer queries
            var result = await GetAllTodayHabitsToManageOptimized(userId);
            
            totalStopwatch.Stop();
            _logger.LogInformation("GetAllTodayHabitsToManage completed in {ElapsedMs}ms for user {UserId}, returned {Count} habits", 
                totalStopwatch.ElapsedMilliseconds, userId, result.Value?.Count() ?? 0);
            
            return result;
        }
        catch (Exception ex)
        {
            totalStopwatch.Stop();
            _logger.LogError(ex, "GetAllTodayHabitsToManage failed after {ElapsedMs}ms for user {UserId}", 
                totalStopwatch.ElapsedMilliseconds, userId);
            throw;
        }
    }

    private async Task<ActionResult<IEnumerable<HabitWithProgressDTO>>> GetAllTodayHabitsToManageOptimized(Guid userId)
    {
        var today = DateTime.UtcNow;
        var dailyKey = GetPeriodKey("daily", today);
        var weeklyKey = GetPeriodKey("weekly", today);
        var monthlyKey = GetPeriodKey("monthly", today);

        // Step 1: Get user's own habits
        var ownHabitsStopwatch = Stopwatch.StartNew();
        var ownHabits = await _context.Habits
            .Where(h => h.UserId == userId && !h.IsArchived)
            .Include(h => h.User)
            .ToListAsync();
        ownHabitsStopwatch.Stop();
        _logger.LogInformation("Fetched {Count} own habits in {ElapsedMs}ms", ownHabits.Count, ownHabitsStopwatch.ElapsedMilliseconds);

        // Step 2: Get friend habits to manage
        var friendHabitsStopwatch = Stopwatch.StartNew();
        var checkRequestHabitIds = await _context.HabitCheckRequests
            .Where(r => r.RequestedUserId == userId)
            .Select(r => r.HabitId)
            .ToListAsync();

        var friendHabits = await _context.Habits
            .Where(h => checkRequestHabitIds.Contains(h.Id) && !h.IsArchived)
            .Include(h => h.User)
            .ToListAsync();
        friendHabitsStopwatch.Stop();
        _logger.LogInformation("Fetched {Count} friend habits in {ElapsedMs}ms", friendHabits.Count, friendHabitsStopwatch.ElapsedMilliseconds);

        // Step 3: Get all habit IDs to fetch logs
        var allHabits = ownHabits.Concat(friendHabits).ToList();
        var habitIds = allHabits.Select(h => h.Id.Value).ToList();

        // Step 4: Fetch all relevant habit logs in one query
        var logsStopwatch = Stopwatch.StartNew();
        var habitLogs = await _context.HabitLogs
            .Where(l => habitIds.Contains(l.HabitId))
            .ToListAsync();
        logsStopwatch.Stop();
        _logger.LogInformation("Fetched {Count} habit logs in {ElapsedMs}ms", habitLogs.Count, logsStopwatch.ElapsedMilliseconds);

        // Step 5: Group logs by habit ID for efficient lookup
        var logsByHabit = habitLogs.GroupBy(l => l.HabitId).ToDictionary(g => g.Key, g => g.ToList());

        // Step 6: Process habits efficiently
        var processingStopwatch = Stopwatch.StartNew();
        var ownResults = ProcessHabitsOptimized(ownHabits, logsByHabit, today, dailyKey, weeklyKey, monthlyKey, isOwned: true);
        var friendResults = ProcessHabitsOptimized(friendHabits, logsByHabit, today, dailyKey, weeklyKey, monthlyKey, isOwned: false);
        processingStopwatch.Stop();
        _logger.LogInformation("Processed all habits in {ElapsedMs}ms", processingStopwatch.ElapsedMilliseconds);

        return ownResults.Concat(friendResults).ToList();
    }

    private List<HabitWithProgressDTO> ProcessHabitsOptimized(
        List<Habit> habits, 
        Dictionary<Guid, List<HabitLog>> logsByHabit, 
        DateTime today, 
        int dailyKey, 
        int weeklyKey, 
        int monthlyKey,
        bool isOwned)
    {
        var results = new List<HabitWithProgressDTO>();

        foreach (var habit in habits)
        {
            // Filter today's habits efficiently
            if (!ShouldIncludeHabitToday(habit, today, dailyKey, weeklyKey, monthlyKey, logsByHabit))
                continue;

            var habitLogs = logsByHabit.GetValueOrDefault(habit.Id.Value, new List<HabitLog>());
            
            var currentValue = GetCurrentProgressOptimized(habit.Id.Value, habit.Frequency, today, habitLogs);
            var streak = CalculateStreakOptimized(habit, habitLogs, today);
            var isCompleted = currentValue >= (habit.TargetValue ?? 1);

            results.Add(new HabitWithProgressDTO
            {
                Id = habit.Id ?? Guid.Empty,
                Name = habit.Name,
                Description = habit.Description,
                Frequency = habit.Frequency,
                GoalType = habit.GoalType,
                TargetValue = habit.TargetValue,
                TargetType = habit.TargetType,
                StreakTarget = habit.StreakTarget,
                EndDate = habit.EndDate,
                CurrentValue = currentValue,
                Streak = streak,
                IsCompleted = isCompleted,
                UserId = habit.UserId,
                UserName = habit.User.UserName,
                isOwnedHabit = isOwned,
                CanManageProgress = true,
                CopyCount = habit.CopyCount
            });
        }

        return results;
    }

    private bool ShouldIncludeHabitToday(Habit habit, DateTime today, int dailyKey, int weeklyKey, int monthlyKey, Dictionary<Guid, List<HabitLog>> logsByHabit)
    {
        // Check if habit has reached completion
        if (HasReachedCompletionOptimized(habit, today, logsByHabit))
            return false;

        // Check start date
        if (habit.StartDate.HasValue && habit.StartDate.Value.Date > today.Date)
            return false;

        // Check frequency-specific rules
        return habit.Frequency switch
        {
            "daily" => true,
            "weekly" => IsHabitInCurrentWeekOptimized(habit, weeklyKey, logsByHabit),
            "monthly" => IsHabitInCurrentMonthOptimized(habit, monthlyKey, logsByHabit),
            _ => false
        };
    }

    private bool HasReachedCompletionOptimized(Habit habit, DateTime today, Dictionary<Guid, List<HabitLog>> logsByHabit)
    {
        if (habit.StreakTarget != null)
        {
            var habitLogs = logsByHabit.GetValueOrDefault(habit.Id.Value, new List<HabitLog>());
            int currentStreak = CalculateStreakOptimized(habit, habitLogs, today);
            if (currentStreak >= habit.StreakTarget) return true;
        }

        if (habit.EndDate != null && today.Date > habit.EndDate.Value.Date)
        {
            return true;
        }

        return false;
    }

    private bool IsHabitInCurrentWeekOptimized(Habit habit, int currentWeekKey, Dictionary<Guid, List<HabitLog>> logsByHabit)
    {
        var habitLogs = logsByHabit.GetValueOrDefault(habit.Id.Value, new List<HabitLog>());
        int progress = habitLogs.Where(l => l.WeeklyKey == currentWeekKey).Sum(l => l.Value);
        int target = habit.TargetValue ?? 1;

        return progress < target || DateTime.UtcNow.DayOfWeek != DayOfWeek.Sunday;
    }

    private bool IsHabitInCurrentMonthOptimized(Habit habit, int currentMonthKey, Dictionary<Guid, List<HabitLog>> logsByHabit)
    {
        var habitLogs = logsByHabit.GetValueOrDefault(habit.Id.Value, new List<HabitLog>());
        int progress = habitLogs.Where(l => l.MonthlyKey == currentMonthKey).Sum(l => l.Value);
        int target = habit.TargetValue ?? 1;

        return progress < target || DateTime.UtcNow.Day < DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
    }

    private int GetCurrentProgressOptimized(Guid habitId, string frequency, DateTime now, List<HabitLog> habitLogs)
    {
        int periodKey = GetPeriodKey(frequency, now);

        return frequency switch
        {
            "daily" => habitLogs.Where(l => l.DailyKey == periodKey).Sum(l => l.Value),
            "weekly" => habitLogs.Where(l => l.WeeklyKey == periodKey).Sum(l => l.Value),
            "monthly" => habitLogs.Where(l => l.MonthlyKey == periodKey).Sum(l => l.Value),
            _ => 0
        };
    }

    private int CalculateStreakOptimized(Habit habit, List<HabitLog> habitLogs, DateTime now)
    {
        if (!habitLogs.Any()) return 0;

        string frequency = NormalizeFrequency(habit.Frequency);
        
        var periodKeys = frequency switch
        {
            "daily" => habitLogs.Select(l => l.DailyKey).Distinct().OrderByDescending(p => p).ToList(),
            "weekly" => habitLogs.Select(l => l.WeeklyKey).Distinct().OrderByDescending(p => p).ToList(),
            "monthly" => habitLogs.Select(l => l.MonthlyKey).Distinct().OrderByDescending(p => p).ToList(),
            _ => new List<int>()
        };

        if (!periodKeys.Any()) return 0;

        int allowedGaps = (frequency == "daily") ? habit.AllowedGaps : 0;
        return ComputeStreak(periodKeys, frequency, now, allowedGaps);
    }

    private async Task<List<HabitWithProgressDTO>> GetAllOwnedTodaysHabit(Guid userId)
    {
        _logger.LogInformation("GetAllOwnedTodaysHabit started for user {UserId}", userId);
        var stopwatch = Stopwatch.StartNew();

        var habits = await _context.Habits
            .Where(h => userId == h.UserId && !h.IsArchived) // ✅ Exclude archived habits
            .Include(h => h.User) // Include the User entity
            .ToListAsync();

        stopwatch.Stop();
        _logger.LogInformation("Fetched {Count} owned habits in {ElapsedMs}ms", habits.Count, stopwatch.ElapsedMilliseconds);

        stopwatch.Restart();
        var todaysHabits = await this.GetTodayHabits(habits);
        stopwatch.Stop();
        _logger.LogInformation("Processed today's habits in {ElapsedMs}ms, result count: {Count}", stopwatch.ElapsedMilliseconds, todaysHabits.Count);

        return todaysHabits.Select(h =>
        {
            h.CanManageProgress = true;
            h.isOwnedHabit = true;
            return h;
        }).ToList();
    }

    private async Task<List<HabitWithProgressDTO>> GetAllFriendsHabitsToManage(Guid userId)
    {
        _logger.LogInformation("GetAllFriendsHabitsToManage started for user {UserId}", userId);
        var stopwatch = Stopwatch.StartNew();

        var checkRequests = await _context.HabitCheckRequests
            .Where(r => r.RequestedUserId == userId /*&& r.Status == CheckRequestStatus.Pending*/)
            .Select(r=> r.HabitId)
            .ToListAsync();

        stopwatch.Stop();
        _logger.LogInformation("Fetched {Count} check requests in {ElapsedMs}ms", checkRequests.Count, stopwatch.ElapsedMilliseconds);

        stopwatch.Restart();
        var habits = await _context.Habits
            .Where(h => checkRequests.Contains(h.Id) && !h.IsArchived) // ✅ Exclude archived habits
            .Include(h => h.User) // Include the User entity
            .ToListAsync();

        stopwatch.Stop();
        _logger.LogInformation("Fetched {Count} friend habits in {ElapsedMs}ms", habits.Count, stopwatch.ElapsedMilliseconds);

        stopwatch.Restart();
        var todaysFriendsHabitsToManage = await this.GetTodayHabits(habits);
        stopwatch.Stop();
        _logger.LogInformation("Processed friend habits in {ElapsedMs}ms, result count: {Count}", stopwatch.ElapsedMilliseconds, todaysFriendsHabitsToManage.Count);

        return todaysFriendsHabitsToManage.Select(h =>
        {
            h.isOwnedHabit = false;
            h.CanManageProgress = true;
            return h;
        }).ToList();
    }

    public async Task<List<HabitWithProgressDTO>> GetPublicHabits(Guid userId, int pageNumber, int pageSize)
    {
        _logger.LogInformation("GetPublicHabits (original) called for user {UserId}, page {PageNumber}, size {PageSize}", userId, pageNumber, pageSize);
        
        // Use optimized version by default
        return await GetPublicHabitsOptimized(userId, pageNumber, pageSize);
    }

    public async Task<HabitWithProgressDTO> CopyHabit(Guid userId, Guid habitId)
    {
        var originalHabit = await _context.Habits
            .FirstOrDefaultAsync(h => h.Id == habitId);

        if (originalHabit == null)
            throw new ArgumentException("Habit not found.");

        if (originalHabit.UserId == userId)
            throw new InvalidOperationException("Cannot copy your own habit.");

        if (originalHabit.IsPrivate)
            throw new InvalidOperationException("Cannot copy a private habit.");

        var copiedHabit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = originalHabit.Name,
            Description = originalHabit.Description,
            Frequency = originalHabit.Frequency,
            GoalType = originalHabit.GoalType,
            TargetValue = originalHabit.TargetValue,
            TargetType = originalHabit.TargetType,
            StreakTarget = originalHabit.StreakTarget,
            AllowedGaps = originalHabit.AllowedGaps,
            StartDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _context.Habits.Add(copiedHabit);

        var habitCopy = new HabitCopy
        {
            OriginalHabitId = habitId,
            CopiedHabitId = copiedHabit.Id.Value,
            CreatedAt = DateTime.UtcNow
        };

        _context.HabitCopies.Add(habitCopy);

        // Increment the copy count of the original habit
        originalHabit.CopyCount++;

        await _context.SaveChangesAsync();

        return new HabitWithProgressDTO
        {
            Id = copiedHabit.Id ?? Guid.Empty,
            Name = copiedHabit.Name,
            Description = copiedHabit.Description,
            Frequency = copiedHabit.Frequency,
            GoalType = copiedHabit.GoalType,
            TargetValue = copiedHabit.TargetValue,
            TargetType = copiedHabit.TargetType,
            StreakTarget = copiedHabit.StreakTarget,
            EndDate = copiedHabit.EndDate,
            CurrentValue = 0,
            Streak = 0,
            IsCompleted = false,
            isOwnedHabit = true,
            CanManageProgress = true
        };
    }

    // OPTIMIZED ENDPOINTS - START

    /// <summary>
    /// Optimized version of GetAllHabits for active/archived habits
    /// </summary>
    public async Task<List<HabitWithProgressDTO>> GetAllHabitsOptimized(Guid userId, bool archived)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("GetAllHabitsOptimized started for user {UserId}, archived: {Archived}", userId, archived);

        try
        {
            var today = DateTime.UtcNow;

            // Step 1: Get habits
            var habitsStopwatch = Stopwatch.StartNew();
            var habits = await _context.Habits
                .Where(h => h.UserId == userId && h.IsArchived == archived)
                .Include(h => h.User)
                .ToListAsync();
            habitsStopwatch.Stop();
            _logger.LogInformation("Fetched {Count} habits in {ElapsedMs}ms", habits.Count, habitsStopwatch.ElapsedMilliseconds);

            if (!habits.Any())
            {
                stopwatch.Stop();
                _logger.LogInformation("GetAllHabitsOptimized completed in {ElapsedMs}ms - no habits found", stopwatch.ElapsedMilliseconds);
                return new List<HabitWithProgressDTO>();
            }

            // Step 2: Get all habit logs in one query
            var habitIds = habits.Select(h => h.Id.Value).ToList();
            var logsStopwatch = Stopwatch.StartNew();
            var habitLogs = await _context.HabitLogs
                .Where(l => habitIds.Contains(l.HabitId))
                .ToListAsync();
            logsStopwatch.Stop();
            _logger.LogInformation("Fetched {Count} habit logs in {ElapsedMs}ms", habitLogs.Count, logsStopwatch.ElapsedMilliseconds);

            // Step 3: Group logs by habit for efficient lookup
            var logsByHabit = habitLogs.GroupBy(l => l.HabitId).ToDictionary(g => g.Key, g => g.ToList());

            // Step 4: Process all habits efficiently
            var processingStopwatch = Stopwatch.StartNew();
            var results = ProcessAllHabitsOptimized(habits, logsByHabit, today, userId);
            processingStopwatch.Stop();
            _logger.LogInformation("Processed {Count} habits in {ElapsedMs}ms", results.Count, processingStopwatch.ElapsedMilliseconds);

            stopwatch.Stop();
            _logger.LogInformation("GetAllHabitsOptimized completed in {ElapsedMs}ms, returned {Count} habits", 
                stopwatch.ElapsedMilliseconds, results.Count);

            return results;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "GetAllHabitsOptimized failed after {ElapsedMs}ms for user {UserId}", 
                stopwatch.ElapsedMilliseconds, userId);
            throw;
        }
    }

    /// <summary>
    /// Optimized version of GetFriendsHabits
    /// </summary>
    public async Task<List<HabitWithProgressDTO>> GetFriendsHabitsOptimized(Guid userId)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("GetFriendsHabitsOptimized started for user {UserId}", userId);

        try
        {
            var today = DateTime.UtcNow;
            var dailyKey = GetPeriodKey("daily", today);
            var weeklyKey = GetPeriodKey("weekly", today);
            var monthlyKey = GetPeriodKey("monthly", today);

            // Step 1: Get connected friends' IDs
            var connectionsStopwatch = Stopwatch.StartNew();
            var connectedUserIds = await _context.Connections
                .Where(c => c.UserId == userId && c.Status == ConnectionStatus.Approved)
                .Select(c => c.ConnectedUserId)
                .ToListAsync();
            connectionsStopwatch.Stop();
            _logger.LogInformation("Fetched {Count} connections in {ElapsedMs}ms", connectedUserIds.Count, connectionsStopwatch.ElapsedMilliseconds);

            if (!connectedUserIds.Any())
            {
                stopwatch.Stop();
                _logger.LogInformation("GetFriendsHabitsOptimized completed in {ElapsedMs}ms - no friends found", stopwatch.ElapsedMilliseconds);
                return new List<HabitWithProgressDTO>();
            }

            // Step 2: Get friends' habits
            var habitsStopwatch = Stopwatch.StartNew();
            var habits = await _context.Habits
                .Where(h => connectedUserIds.Contains(h.UserId) && !h.IsArchived)
                .Include(h => h.User)
                .ToListAsync();
            habitsStopwatch.Stop();
            _logger.LogInformation("Fetched {Count} friend habits in {ElapsedMs}ms", habits.Count, habitsStopwatch.ElapsedMilliseconds);

            if (!habits.Any())
            {
                stopwatch.Stop();
                _logger.LogInformation("GetFriendsHabitsOptimized completed in {ElapsedMs}ms - no friend habits found", stopwatch.ElapsedMilliseconds);
                return new List<HabitWithProgressDTO>();
            }

            // Step 3: Get all habit logs in one query
            var habitIds = habits.Select(h => h.Id.Value).ToList();
            var logsStopwatch = Stopwatch.StartNew();
            var habitLogs = await _context.HabitLogs
                .Where(l => habitIds.Contains(l.HabitId))
                .ToListAsync();
            logsStopwatch.Stop();
            _logger.LogInformation("Fetched {Count} habit logs in {ElapsedMs}ms", habitLogs.Count, logsStopwatch.ElapsedMilliseconds);

            // Step 4: Group logs by habit for efficient lookup
            var logsByHabit = habitLogs.GroupBy(l => l.HabitId).ToDictionary(g => g.Key, g => g.ToList());

            // Step 5: Process habits efficiently (today's habits only)
            var processingStopwatch = Stopwatch.StartNew();
            var results = ProcessHabitsOptimized(habits, logsByHabit, today, dailyKey, weeklyKey, monthlyKey, isOwned: false);
            processingStopwatch.Stop();
            _logger.LogInformation("Processed {Count} friend habits in {ElapsedMs}ms", results.Count, processingStopwatch.ElapsedMilliseconds);

            // Set friend-specific properties
            foreach (var habit in results)
            {
                habit.isOwnedHabit = false;
                habit.CanManageProgress = false;
            }

            stopwatch.Stop();
            _logger.LogInformation("GetFriendsHabitsOptimized completed in {ElapsedMs}ms, returned {Count} habits", 
                stopwatch.ElapsedMilliseconds, results.Count);

            return results;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "GetFriendsHabitsOptimized failed after {ElapsedMs}ms for user {UserId}", 
                stopwatch.ElapsedMilliseconds, userId);
            throw;
        }
    }

    /// <summary>
    /// Optimized version of GetPublicHabits
    /// </summary>
    public async Task<List<HabitWithProgressDTO>> GetPublicHabitsOptimized(Guid userId, int pageNumber, int pageSize)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("GetPublicHabitsOptimized started for user {UserId}, page {PageNumber}, size {PageSize}", userId, pageNumber, pageSize);

        try
        {
            var today = DateTime.UtcNow;
            var dailyKey = GetPeriodKey("daily", today);
            var weeklyKey = GetPeriodKey("weekly", today);
            var monthlyKey = GetPeriodKey("monthly", today);

            // Step 1: Get connected friends' IDs to exclude them
            var connectionsStopwatch = Stopwatch.StartNew();
            var connectedUserIds = await _context.Connections
                .Where(c => c.UserId == userId && c.Status == ConnectionStatus.Approved)
                .Select(c => c.ConnectedUserId)
                .ToListAsync();
            connectionsStopwatch.Stop();
            _logger.LogInformation("Fetched {Count} connections in {ElapsedMs}ms", connectedUserIds.Count, connectionsStopwatch.ElapsedMilliseconds);

            // Step 2: Get public habits with pagination
            var habitsStopwatch = Stopwatch.StartNew();
            var habits = await _context.Habits
                .Where(h => h.UserId != userId && !connectedUserIds.Contains(h.UserId) && !h.IsArchived)
                .Include(h => h.User)
                .OrderByDescending(h => h.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            habitsStopwatch.Stop();
            _logger.LogInformation("Fetched {Count} public habits in {ElapsedMs}ms", habits.Count, habitsStopwatch.ElapsedMilliseconds);

            if (!habits.Any())
            {
                stopwatch.Stop();
                _logger.LogInformation("GetPublicHabitsOptimized completed in {ElapsedMs}ms - no public habits found", stopwatch.ElapsedMilliseconds);
                return new List<HabitWithProgressDTO>();
            }

            // Step 3: Get all habit logs in one query
            var habitIds = habits.Select(h => h.Id.Value).ToList();
            var logsStopwatch = Stopwatch.StartNew();
            var habitLogs = await _context.HabitLogs
                .Where(l => habitIds.Contains(l.HabitId))
                .ToListAsync();
            logsStopwatch.Stop();
            _logger.LogInformation("Fetched {Count} habit logs in {ElapsedMs}ms", habitLogs.Count, logsStopwatch.ElapsedMilliseconds);

            // Step 4: Group logs by habit for efficient lookup
            var logsByHabit = habitLogs.GroupBy(l => l.HabitId).ToDictionary(g => g.Key, g => g.ToList());

            // Step 5: Process habits efficiently (today's habits only)
            var processingStopwatch = Stopwatch.StartNew();
            var results = ProcessHabitsOptimized(habits, logsByHabit, today, dailyKey, weeklyKey, monthlyKey, isOwned: false);
            processingStopwatch.Stop();
            _logger.LogInformation("Processed {Count} public habits in {ElapsedMs}ms", results.Count, processingStopwatch.ElapsedMilliseconds);

            // Set public habit properties
            foreach (var habit in results)
            {
                habit.isOwnedHabit = false;
                habit.CanManageProgress = false;
            }

            stopwatch.Stop();
            _logger.LogInformation("GetPublicHabitsOptimized completed in {ElapsedMs}ms, returned {Count} habits", 
                stopwatch.ElapsedMilliseconds, results.Count);

            return results;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "GetPublicHabitsOptimized failed after {ElapsedMs}ms for user {UserId}", 
                stopwatch.ElapsedMilliseconds, userId);
            throw;
        }
    }

    /// <summary>
    /// Helper method to process all habits (not just today's habits)
    /// </summary>
    private List<HabitWithProgressDTO> ProcessAllHabitsOptimized(
        List<Habit> habits, 
        Dictionary<Guid, List<HabitLog>> logsByHabit, 
        DateTime today, 
        Guid userId)
    {
        var results = new List<HabitWithProgressDTO>();

        foreach (var habit in habits)
        {
            var habitLogs = logsByHabit.GetValueOrDefault(habit.Id.Value, new List<HabitLog>());
            
            var currentValue = GetCurrentProgressOptimized(habit.Id.Value, habit.Frequency, today, habitLogs);
            var streak = CalculateStreakOptimized(habit, habitLogs, today);
            var isCompleted = currentValue >= (habit.TargetValue ?? 1);

            results.Add(new HabitWithProgressDTO
            {
                Id = habit.Id ?? Guid.Empty,
                Name = habit.Name,
                Description = habit.Description,
                Frequency = habit.Frequency,
                GoalType = habit.GoalType,
                TargetValue = habit.TargetValue,
                TargetType = habit.TargetType,
                StreakTarget = habit.StreakTarget,
                EndDate = habit.EndDate,
                CurrentValue = currentValue,
                Streak = streak,
                IsCompleted = isCompleted,
                UserId = habit.UserId,
                UserName = habit.User?.UserName,
                isOwnedHabit = habit.UserId == userId,
                CanManageProgress = habit.UserId == userId,
                CopyCount = habit.CopyCount
            });
        }

        return results;
    }

    // OPTIMIZED ENDPOINTS - END
}

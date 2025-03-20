using Microsoft.EntityFrameworkCore;
using System.Globalization;

public class HabitService
{
    private readonly AppDbContext _context;

    public HabitService(AppDbContext context)
    {
        _context = context;
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

    public async Task<List<HabitWithProgressDTO>> GetTodayHabits(Guid userId)
    {
        var today = DateTime.UtcNow;
        var currentWeekKey = GetPeriodKey("weekly", today);  // Get the current week identifier
        var currentMonthKey = GetPeriodKey("monthly", today); // Get the current month identifier

        var habits = await _context.Habits
            .Where(h => h.UserId == userId && !h.IsArchived) // ✅ Exclude archived habits
            .ToListAsync();

        return habits
            .Where(h => !HasReachedCompletion(h.Id, h.StreakTarget, h.EndDate) && 
                (!h.StartDate.HasValue || today.Date <= h.StartDate.Value.Date) &&
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
                IsCompleted = IsHabitCompleted(h.Id ?? Guid.Empty, h.Frequency, today)
            })
            .ToList();
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

    public async Task UpdateHabitProgress(Guid userId, Guid habitId, bool decrease)
    {
        var habit = await _context.Habits
            .FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId);

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

    private int CalculateStreak(Guid habitId, string frequency, DateTime now)
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


    // ✅ Check if Habit is Completed
    private bool IsHabitCompleted(Guid habitId, string frequency, DateTime now)
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
        var today = DateTime.UtcNow;

        var habits = await _context.Habits
            .Where(h => h.UserId == userId && h.IsArchived == archived)
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
            .Where(h => h.Id == habitId && h.UserId == userId)
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
            RecentLogs = recentLogs
        };
    }
}

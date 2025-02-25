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
        if (string.IsNullOrWhiteSpace(habitDto.Name) ||
            string.IsNullOrWhiteSpace(habitDto.Frequency) ||
            string.IsNullOrWhiteSpace(habitDto.GoalType))
        {
            throw new ArgumentException("All required fields must be provided.");
        }

        var newHabit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = habitDto.Name,
            Frequency = habitDto.Frequency,
            GoalType = habitDto.GoalType,
            TargetValue = habitDto.TargetValue,
            CreatedAt = DateTime.UtcNow
        };

        _context.Habits.Add(newHabit);
        await _context.SaveChangesAsync();

        return new HabitWithProgressDTO // ✅ Convert to DTO
        {
            Id = newHabit.Id ?? Guid.Empty,
            Name = newHabit.Name,
            Frequency = newHabit.Frequency,
            GoalType = newHabit.GoalType,
            TargetValue = newHabit.TargetValue,
            CurrentValue = 0, // Newly created habits start with 0 progress
            Streak = 0,
            IsCompleted = false
        };
    }


    // ✅ Get All Habits with Current Progress & Streaks
    public async Task<List<HabitWithProgressDTO>> GetAllHabits(Guid userId)
    {
        var today = DateTime.UtcNow;

        var habits = await _context.Habits
            .Where(h => h.UserId == userId)
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
            .Where(h => h.Frequency == "daily" ||
                        (h.Frequency == "weekly" && IsHabitInCurrentWeek(h.Id, currentWeekKey)) ||
                        (h.Frequency == "monthly" && IsHabitInCurrentMonth(h.Id, currentMonthKey)))
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
        var logsQuery = _context.HabitLogs.Where(l => l.HabitId == habitId);

        // ✅ Choose the correct period column based on frequency
        var periodKeyColumn = frequency switch
        {
            "daily" => logsQuery.Select(l => l.DailyKey),
            "weekly" => logsQuery.Select(l => l.WeeklyKey),
            "monthly" => logsQuery.Select(l => l.MonthlyKey),
            _ => throw new ArgumentException("Invalid frequency")
        };

        // ✅ Get unique period keys in descending order
        var periodKeys = periodKeyColumn
            .Distinct()
            .OrderByDescending(p => p)
            .ToList();

        int streak = 0;
        int expectedPeriod = GetPeriodKey(frequency, now);

        foreach (var period in periodKeys)
        {
            if (period == expectedPeriod)
            {
                streak++;
                expectedPeriod = GetPreviousPeriodKey(frequency, expectedPeriod);
            }
            else
            {
                // Streak is broken if the next expected period is missing
                break;
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
        habit.Frequency = updatedHabit.Frequency;
        habit.GoalType = updatedHabit.GoalType;
        habit.TargetValue = updatedHabit.TargetValue;

        await _context.SaveChangesAsync();

        return new HabitWithProgressDTO
        {
            Id = habit.Id ?? Guid.Empty,
            Name = habit.Name,
            Frequency = habit.Frequency,
            GoalType = habit.GoalType,
            TargetValue = habit.TargetValue,
            CurrentValue = 0,
            Streak = 0,
            IsCompleted = false
        };
    }
    public async Task<HabitWithProgressDTO?> GetHabitById(Guid userId, Guid habitId)
    {
        var habit = await _context.Habits
            .Where(h => h.Id == habitId && h.UserId == userId)
            .FirstOrDefaultAsync();

        if (habit == null) return null;

        var today = DateTime.UtcNow;

        return new HabitWithProgressDTO
        {
            Id = habit.Id ?? Guid.Empty,
            Name = habit.Name,
            Frequency = habit.Frequency,
            GoalType = habit.GoalType,
            TargetValue = habit.TargetValue,
            CurrentValue = GetCurrentProgress(habit.Id ?? Guid.Empty, habit.Frequency, today),
            Streak = CalculateStreak(habit.Id ?? Guid.Empty, habit.Frequency, today),
            IsCompleted = IsHabitCompleted(habit.Id ?? Guid.Empty, habit.Frequency, today)
        };
    }

}

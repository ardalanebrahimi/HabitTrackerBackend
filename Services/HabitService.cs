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

        var habits = await _context.Habits
            .Where(h => h.UserId == userId && !h.IsArchived)  // ✅ Exclude archived habits
            .ToListAsync();

        return habits
            .Where(h => h.Frequency == "daily" ||
                        (h.Frequency == "weekly" && !HasMetGoal(h.Id, GetPeriodKey("weekly", today))) ||
                        (h.Frequency == "monthly" && !HasMetGoal(h.Id, GetPeriodKey("monthly", today))))
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


    // ✅ Increase Habit Progress
    public async Task UpdateHabitProgress(Guid userId, Guid habitId, bool decrease)
    {
        var habit = await _context.Habits
            .FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId);

        if (habit == null)
            throw new ArgumentException("Habit not found.");

        var now = DateTime.UtcNow;
        var periodKey = GetPeriodKey(habit.Frequency, now);

        var existingLog = await _context.HabitLogs
            .FirstOrDefaultAsync(l => l.HabitId == habitId && l.PeriodKey == periodKey);

        if (!decrease)
        {
            if (existingLog != null)
            {
                existingLog.Value++;
            }
            else
            {
                var newLog = new HabitLog
                {
                    Id = Guid.NewGuid(),
                    HabitId = habitId,
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
                existingLog.Value--;
                if (existingLog.Value == 0)
                {
                    _context.HabitLogs.Remove(existingLog);
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    // ✅ Get Current Progress
    private int GetCurrentProgress(Guid habitId, string frequency, DateTime now)
    {
        var periodKey = GetPeriodKey(frequency, now);
        return _context.HabitLogs
            .Where(l => l.HabitId == habitId && l.PeriodKey == periodKey)
            .Sum(l => l.Value);
    }

    // ✅ Calculate Habit Streak
    private int CalculateStreak(Guid habitId, string frequency, DateTime now)
    {
        var logs = _context.HabitLogs
            .Where(l => l.HabitId == habitId)
            .OrderByDescending(l => l.Timestamp)
            .Select(l => l.PeriodKey)
            .ToList();

        int streak = 0;
        int expectedPeriod = GetPeriodKey(frequency, now);

        foreach (var period in logs)
        {
            if (period == expectedPeriod)
            {
                streak++;
                expectedPeriod = frequency switch
                {
                    "daily" => expectedPeriod - 1,
                    "weekly" => expectedPeriod - 1,
                    "monthly" => expectedPeriod - 1,
                    _ => expectedPeriod
                };
            }
            else break;
        }

        return streak;
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

    // ✅ Helper: Check if Goal is Met for Weekly/Monthly Habits
    private bool HasMetGoal(Guid? habitId, int periodKey)
    {
        return _context.HabitLogs.Any(l => l.HabitId == habitId && l.PeriodKey == periodKey);
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

}

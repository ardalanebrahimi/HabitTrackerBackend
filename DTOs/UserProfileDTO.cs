public class UserProfileDTO
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = null!;
    public string? Email { get; set; }
    public DateTime JoinedDate { get; set; }
    public bool IsFriend { get; set; }
    public bool IsCurrentUser { get; set; }
    
    // Profile analytics (visible to friends and owner)
    public ProfileAnalyticsDTO? Analytics { get; set; }
    
    // Public habits (visible to everyone)
    public List<HabitWithProgressDTO> PublicHabits { get; set; } = new();
    
    // Friend habits (visible to friends only)
    public List<HabitWithProgressDTO> FriendHabits { get; set; } = new();
}

public class ProfileAnalyticsDTO
{
    public int TotalHabits { get; set; }
    public int ActiveHabits { get; set; }
    public int CompletedToday { get; set; }
    public int LongestStreak { get; set; }
    public double SuccessRate { get; set; }
    public List<string> TopCategories { get; set; } = new();
}

// Simplified request DTO (keeping for potential future use)
public class ProfileViewRequest
{
    public Guid ViewedUserId { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace HabitTrackerBackend.DTOs
{
    public class OnboardingRequest
    {
        [Required(ErrorMessage = "Primary goal is required")]
        public string PrimaryGoal { get; set; } = string.Empty;

        [Required(ErrorMessage = "Current struggle is required")]
        public string CurrentStruggle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Motivation level is required")]
        public string MotivationLevel { get; set; } = string.Empty;

        [Required(ErrorMessage = "Available time is required")]
        public string AvailableTime { get; set; } = string.Empty;

        [Required(ErrorMessage = "Preferred schedule is required")]
        public string PreferredSchedule { get; set; } = string.Empty;
    }
}

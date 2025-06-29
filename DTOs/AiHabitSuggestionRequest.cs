using System.ComponentModel.DataAnnotations;

namespace HabitTrackerBackend.DTOs
{
    public class AiHabitSuggestionRequest
    {
        [Required(ErrorMessage = "Prompt is required")]
        [StringLength(500, ErrorMessage = "Prompt cannot exceed 500 characters")]
        public string Prompt { get; set; } = string.Empty;
    }
}

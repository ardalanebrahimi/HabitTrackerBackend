using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HabitTrackerBackend.DTOs;
using HabitTrackerBackend.Services;

namespace HabitTrackerBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AiController : ControllerBase
    {
        private readonly AiHabitSuggestionService _aiService;
        private readonly ILogger<AiController> _logger;

        public AiController(AiHabitSuggestionService aiService, ILogger<AiController> logger)
        {
            _aiService = aiService;
            _logger = logger;
        }

        /// <summary>
        /// Generate an AI-powered habit suggestion based on user prompt
        /// </summary>
        /// <param name="request">The user's prompt for habit generation</param>
        /// <returns>AI-generated habit suggestion with structured data</returns>
        [HttpPost("habit-suggest")]
        public async Task<ActionResult<AiHabitSuggestionResponse>> SuggestHabit([FromBody] AiHabitSuggestionRequest request)
        {
            try
            {
                _logger.LogInformation("Received AI habit suggestion request from user {UserId} with prompt: {Prompt}", 
                    User.Identity?.Name, request.Prompt);

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var suggestion = await _aiService.GenerateHabitSuggestionAsync(request.Prompt);

                _logger.LogInformation("Generated AI habit suggestion: {HabitName}", suggestion.Name);

                return Ok(suggestion);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("OpenAI API key"))
            {
                _logger.LogError(ex, "OpenAI API key not configured");
                return StatusCode(500, new { message = "AI service is not configured. Please contact support." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI habit suggestion");
                return StatusCode(500, new { message = "Failed to generate habit suggestion. Please try again." });
            }
        }

        /// <summary>
        /// Generate multiple AI habit suggestions based on onboarding questionnaire answers
        /// </summary>
        /// <param name="request">The user's onboarding questionnaire answers</param>
        /// <returns>Up to 3 AI-generated habit suggestions for onboarding</returns>
        [HttpPost("onboard-suggest")]
        public async Task<ActionResult<List<AiHabitSuggestionResponse>>> SuggestOnboardingHabits([FromBody] OnboardingRequest request)
        {
            try
            {
                _logger.LogInformation("Received onboarding suggestion request from user {UserId}", User.Identity?.Name);

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var suggestions = await _aiService.GenerateOnboardingHabitSuggestionsAsync(request);

                _logger.LogInformation("Generated {Count} onboarding habit suggestions", suggestions.Count);

                return Ok(suggestions);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("OpenAI API key"))
            {
                _logger.LogError(ex, "OpenAI API key not configured");
                return StatusCode(500, new { message = "AI service is not configured. Please contact support." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating onboarding habit suggestions");
                return StatusCode(500, new { message = "Failed to generate habit suggestions. Please try again." });
            }
        }

        /// <summary>
        /// Health check endpoint for AI service
        /// </summary>
        /// <returns>AI service status</returns>
        [HttpGet("status")]
        public ActionResult GetAiServiceStatus()
        {
            try
            {
                var isConfigured = !string.IsNullOrEmpty(HttpContext.RequestServices
                    .GetRequiredService<IConfiguration>()["OpenAI:ApiKey"]);

                return Ok(new { 
                    aiServiceEnabled = isConfigured,
                    status = isConfigured ? "Ready" : "Not Configured",
                    message = isConfigured ? "AI habit suggestions are available" : "OpenAI API key is not configured"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking AI service status");
                return StatusCode(500, new { message = "Unable to check AI service status" });
            }
        }
    }
}

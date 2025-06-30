using System.Text.Json;
using System.Text;
using HabitTrackerBackend.DTOs;

namespace HabitTrackerBackend.Services
{
    public class AiHabitSuggestionService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AiHabitSuggestionService> _logger;
        private readonly string _apiKey;

        public AiHabitSuggestionService(HttpClient httpClient, IConfiguration configuration, ILogger<AiHabitSuggestionService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key is not configured");
            
            _httpClient.BaseAddress = new Uri("https://api.openai.com/");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<AiHabitSuggestionResponse> GenerateHabitSuggestionAsync(string userPrompt)
        {
            try
            {
                var systemPrompt = @"You are a habit-building expert. Generate a single, specific habit suggestion based on the user's request. 

Respond with ONLY a valid JSON object (no markdown, no additional text) in this exact format:
{
  ""name"": ""Specific habit name (max 50 chars)"",
  ""description"": ""Detailed description (max 200 chars)"",
  ""frequency"": ""daily"",
  ""goalType"": ""binary"",
  ""targetType"": ""ongoing"",
  ""targetValue"": 1,
  ""streakTarget"": 7,
  ""endDate"": null,
  ""allowedGaps"": 1,
  ""startDate"": null
}

Guidelines:
- name: Short, action-oriented (e.g., ""Read 20 pages daily"")
- description: Clear explanation of what and why
- frequency: Use ""daily"", ""weekly"", or ""monthly"" (lowercase)
- goalType: Use ""binary"" for yes/no habits or ""numeric"" for count-based habits
- targetType: Use ""ongoing"" for most habits, ""streak"" for streak-based, or ""endDate"" for time-limited
- targetValue: Number for numeric goals, null for binary goals  
- streakTarget: Target streak length (7-30 days)
- endDate: null for ongoing habits, specific date for time-limited
- allowedGaps: How many gaps allowed before streak breaks (1-3)
- startDate: null to start immediately";

                var requestBody = new
                {
                    model = "gpt-3.5-turbo",
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = $"Generate a habit for: {userPrompt}" }
                    },
                    temperature = 0.7,
                    max_tokens = 800
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogInformation("Sending request to OpenAI for habit suggestion");

                var response = await _httpClient.PostAsync("v1/chat/completions", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("OpenAI API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    throw new HttpRequestException($"OpenAI API returned {response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Choices?.Length == 0)
                {
                    throw new InvalidOperationException("No response from OpenAI");
                }

                var assistantMessage = apiResponse.Choices[0].Message.Content;
                _logger.LogInformation("Received response from OpenAI: {Response}", assistantMessage);

                // Clean the response to extract just the JSON
                var jsonSuggestion = ExtractJsonFromResponse(assistantMessage);
                
                var suggestion = JsonSerializer.Deserialize<AiHabitSuggestionResponse>(jsonSuggestion, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (suggestion == null)
                {
                    throw new InvalidOperationException("Failed to parse AI response");
                }

                // Validate and sanitize the response
                ValidateAndSanitizeSuggestion(suggestion);

                return suggestion;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse AI response as JSON");
                return CreateFallbackSuggestion(userPrompt);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error calling OpenAI API");
                return CreateFallbackSuggestion(userPrompt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI habit suggestion");
                return CreateFallbackSuggestion(userPrompt);
            }
        }

        private string ExtractJsonFromResponse(string response)
        {
            // Remove markdown code blocks if present
            var cleaned = response.Trim();
            if (cleaned.StartsWith("```json"))
            {
                cleaned = cleaned.Substring(7);
            }
            if (cleaned.StartsWith("```"))
            {
                cleaned = cleaned.Substring(3);
            }
            if (cleaned.EndsWith("```"))
            {
                cleaned = cleaned.Substring(0, cleaned.Length - 3);
            }

            return cleaned.Trim();
        }

        private void ValidateAndSanitizeSuggestion(AiHabitSuggestionResponse suggestion)
        {
            // Ensure required fields have values
            if (string.IsNullOrWhiteSpace(suggestion.Name))
                suggestion.Name = "Daily Habit";
            
            if (string.IsNullOrWhiteSpace(suggestion.Description))
                suggestion.Description = "A beneficial daily habit to improve your life";

            // Validate frequency - must match Habit entity validation
            var validFrequencies = new[] { "daily", "weekly", "monthly" };
            if (!validFrequencies.Contains(suggestion.Frequency.ToLower()))
                suggestion.Frequency = "daily";
            else
                suggestion.Frequency = suggestion.Frequency.ToLower();

            // Validate goal type - must match Habit entity validation  
            var validGoalTypes = new[] { "binary", "numeric" };
            if (!validGoalTypes.Contains(suggestion.GoalType.ToLower()))
                suggestion.GoalType = "binary";
            else
                suggestion.GoalType = suggestion.GoalType.ToLower();

            // Validate target type - must match CreateHabitDTO defaults
            var validTargetTypes = new[] { "ongoing", "streak", "endDate" };
            if (!validTargetTypes.Contains(suggestion.TargetType))
                suggestion.TargetType = "ongoing";

            // Set appropriate target value based on goal type
            if (suggestion.GoalType == "binary")
            {
                suggestion.TargetValue = null; // Binary habits don't need target values
            }
            else if (suggestion.TargetValue <= 0)
            {
                suggestion.TargetValue = 1;
            }

            // Ensure reasonable values
            if (suggestion.StreakTarget <= 0)
                suggestion.StreakTarget = 7;

            // Set allowed gaps (required field)
            if (suggestion.AllowedGaps <= 0)
                suggestion.AllowedGaps = 1;


            if (suggestion.Name?.Length > 50)
                suggestion.Name = suggestion.Name.Substring(0, 47) + "...";

            if (suggestion.Description?.Length > 200)
                suggestion.Description = suggestion.Description.Substring(0, 197) + "...";
        }

        private AiHabitSuggestionResponse CreateFallbackSuggestion(string userPrompt)
        {
            _logger.LogWarning("Creating fallback suggestion for prompt: {Prompt}", userPrompt);

            return new AiHabitSuggestionResponse
            {
                Name = "Daily Mindfulness",
                Description = "Practice mindfulness for a few minutes each day to reduce stress and improve focus",
                Frequency = "daily",
                GoalType = "numeric",
                TargetType = "ongoing",
                TargetValue = 5,
                StreakTarget = 7,
                AllowedGaps = 1,
                EndDate = null,
                StartDate = null,
            };
        }
    }

    // Internal classes for OpenAI API response
    internal class OpenAIResponse
    {
        public Choice[] Choices { get; set; } = Array.Empty<Choice>();
    }

    internal class Choice
    {
        public Message Message { get; set; } = new Message();
    }

    internal class Message
    {
        public string Content { get; set; } = string.Empty;
    }
}

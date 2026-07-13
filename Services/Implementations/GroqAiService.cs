using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TaskFlow.Models;

namespace TaskFlow.Services.Implementations
{
    public class GroqAiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public GroqAiService(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<List<AiSuggestedTask>> GenerateTasksAsync(string goal)
        {
            var apiKey = _configuration["Groq:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new Exception("Groq API key not found.");
            }

            _httpClient.DefaultRequestHeaders.Clear();

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var request = new
            {
                model = "llama-3.3-70b-versatile",

                messages = new[]
                {
            new
            {
                role = "system",
                content =
"""
You are an expert productivity coach.

Your job is to convert a user's goal into exactly 5 actionable tasks.

Return ONLY valid JSON.

Example:

[
  {
    "title":"Learn C# Fundamentals",
    "description":"Understand variables, loops and OOP concepts.",
    "priority":"High",
    "estimatedDays":1
  },
  {
    "title":"Practice SQL",
    "description":"Practice JOIN, GROUP BY and aggregate queries.",
    "priority":"Medium",
    "estimatedDays":2
  }
]

Rules:

- Return exactly 5 tasks.
- Priority must be High, Medium or Low.
- estimatedDays must be between 1 and 7.
- Description should be one short sentence.
- Do NOT return markdown.
- Do NOT return explanations.
- Return ONLY valid JSON.
"""
            },

            new
            {
                role = "user",
                content = goal
            }
        },

                temperature = 0.5,

                max_tokens = 600
            };

            var json =
                JsonSerializer.Serialize(request);

            var response =
                await _httpClient.PostAsync(
                    "https://api.groq.com/openai/v1/chat/completions",

                    new StringContent(
                        json,
                        Encoding.UTF8,
                        "application/json"));

            response.EnsureSuccessStatusCode();

            var result =
                await response.Content.ReadAsStringAsync();

            using var document =
                JsonDocument.Parse(result);

            var aiJson =
                document.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

            if (string.IsNullOrWhiteSpace(aiJson))
            {
                return new List<AiSuggestedTask>();
            }

            var options =
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

            return JsonSerializer.Deserialize<List<AiSuggestedTask>>(
                       aiJson,
                       options)
                   ?? new List<AiSuggestedTask>();
        }
    }
}
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

        // ==========================================
        // AI TASK GENERATOR
        // ==========================================

        public async Task<List<AiSuggestedTask>>
            GenerateTasksAsync(string goal)
        {
            var apiKey = _configuration["Groq:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new Exception(
                    "Groq API key not found.");
            }

            var requestBody = new
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

            var aiJson =
                await SendGroqRequestAsync(
                    requestBody,
                    apiKey);

            if (string.IsNullOrWhiteSpace(aiJson))
            {
                return new List<AiSuggestedTask>();
            }

            var options =
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

            return JsonSerializer
                       .Deserialize<List<AiSuggestedTask>>(
                           aiJson,
                           options)
                   ?? new List<AiSuggestedTask>();
        }


        // ==========================================
        // AI RESOURCE RECOMMENDATION
        // ==========================================

        public async Task<List<AiSuggestedResource>>
            GenerateResourcesAsync(
                string taskTitle,
                string? taskDescription)
        {
            var apiKey = _configuration["Groq:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new Exception(
                    "Groq API key not found.");
            }

            var taskContext =
                $"""
                Task title: {taskTitle}
                Task description: {taskDescription ?? "No description"}
                """;

            var requestBody = new
            {
                model = "llama-3.3-70b-versatile",

                messages = new[]
                {
                    new
                    {
                        role = "system",

                        content =
"""
You are an expert online resource recommendation assistant.

Analyze the user's task and recommend exactly 6 candidate online resources
that directly help the user complete or learn the task.

The application will verify every URL before displaying it.
Only working resources will be shown to the user.

Return ONLY valid JSON.

Example:

[
  {
    "title":"ASP.NET Core MVC Overview",
    "description":"Official Microsoft documentation explaining ASP.NET Core MVC.",
    "url":"https://learn.microsoft.com/en-us/aspnet/core/mvc/overview",
    "resourceType":"Documentation"
  },
  {
    "title":"ASP.NET Core Learning Resources",
    "description":"Official Microsoft learning resources for ASP.NET Core.",
    "url":"https://learn.microsoft.com/en-us/aspnet/core/",
    "resourceType":"Documentation"
  },
  {
    "title":"ASP.NET Core Repository",
    "description":"Official ASP.NET Core source code and project repository.",
    "url":"https://github.com/dotnet/aspnetcore",
    "resourceType":"Repository"
  }
]

Rules:

- Recommend relevant courses from established learning platforms when suitable.
- You may recommend Coursera courses.
- You may recommend Udemy courses.
- You may recommend edX courses.
- You may recommend Udacity courses.
- You may recommend Pluralsight courses.
- You may recommend Codecademy courses.
- You may recommend FutureLearn courses.
- You may recommend DataCamp courses.
- You may recommend Educative courses.
- You may recommend Scrimba courses.
- Prefer a specific course page instead of the platform home page.
- Do not recommend a course only because it is popular.
- The course must directly relate to the user's task.
- Clearly use resourceType "Course" for online courses.
- Return exactly 6 candidate resources.
- Resources must directly relate to the user's task.
- Prefer authoritative educational sources and established learning platforms.
- Prefer specific resource pages over generic home pages.
- Prefer official documentation.
- Course resources may be free or paid.
- Do not claim a course is free unless you are certain.
- Do not mention course pricing in the description.
- Prefer official learning paths.
- Prefer official repositories.
- Prefer Microsoft Learn for .NET and Microsoft technologies.
- Prefer MDN for browser and web technologies.
- Prefer official product documentation when available.
- GitHub repositories must be established and relevant.
- You may recommend freeCodeCamp.
- Do NOT recommend YouTube videos.
- Do NOT return youtube.com URLs.
- Do NOT return youtu.be URLs.
- Prefer official documentation, learning paths, articles and repositories.
- URL must be a complete HTTPS URL.
- Never return a Google search URL.
- Never return a Bing search URL.
- Never return a search results URL.
- Never return shortened URLs.
- Never invent a domain.
- Never use example URLs.
- Never use placeholder URLs.
- Never use a URL containing the word example.
- resourceType must be Documentation, Course, Article or Repository.- Description must be one short sentence.
- Do NOT return markdown.
- Do NOT wrap JSON in code fences.
- Do NOT return explanations.
- Return ONLY valid JSON.
"""
                    },

                    new
                    {
                        role = "user",
                        content = taskContext
                    }
                },

                temperature = 0.1,

                max_tokens = 1200
            };

            var aiJson =
                await SendGroqRequestAsync(
                    requestBody,
                    apiKey);

            if (string.IsNullOrWhiteSpace(aiJson))
            {
                return new List<AiSuggestedResource>();
            }

            var options =
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

            var resources =
                JsonSerializer
                    .Deserialize<List<AiSuggestedResource>>(
                        aiJson,
                        options)
                ?? new List<AiSuggestedResource>();

            var validResources =
                new List<AiSuggestedResource>();

            foreach (var resource in resources)
            {
                if (validResources.Count == 3)
                {
                    break;
                }

                if (!IsSafeResourceUrl(resource.Url))
                {
                    continue;
                }

                var duplicate =
                    validResources.Any(existing =>
                        string.Equals(
                            existing.Url,
                            resource.Url,
                            StringComparison.OrdinalIgnoreCase));

                if (duplicate)
                {
                    continue;
                }

                var isWorking =
                    await IsResourceUrlWorkingAsync(
                        resource.Url);

                if (!isWorking)
                {
                    continue;
                }

                validResources.Add(resource);
            }

            return validResources;
        }


        // ==========================================
        // SEND REQUEST TO GROQ
        // ==========================================

        private async Task<string?>
            SendGroqRequestAsync(
                object requestBody,
                string apiKey)
        {
            var json =
                JsonSerializer.Serialize(requestBody);

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://api.groq.com/openai/v1/chat/completions");

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    apiKey);

            request.Content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            using var response =
                await _httpClient.SendAsync(request);

            var responseContent =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Groq API returned {(int)response.StatusCode}: " +
                    responseContent);
            }

            using var document =
                JsonDocument.Parse(responseContent);

            return document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }


        // ==========================================
        // SAFE RESOURCE DOMAIN CHECK
        // ==========================================

        private static bool IsSafeResourceUrl(
    string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            if (!Uri.TryCreate(
                    url,
                    UriKind.Absolute,
                    out var uri))
            {
                return false;
            }

            if (uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(
                    uri.UserInfo))
            {
                return false;
            }

            var host =
                uri.Host.ToLowerInvariant();

            var trustedDomains = new[]
{
    // ==========================================
    // TECHNOLOGY & PROGRAMMING
    // ==========================================

    "learn.microsoft.com",
    "developer.mozilla.org",
    "docs.github.com",
    "github.com",
    "freecodecamp.org",
    "www.freecodecamp.org",
    "docs.python.org",
    "react.dev",
    "angular.dev",
    "vuejs.org",
    "nodejs.org",
    "docs.oracle.com",
    "postgresql.org",
    "www.postgresql.org",
    "docker.com",
    "docs.docker.com",
    "aws.amazon.com",
    "cloud.google.com",


    // ==========================================
    // ONLINE COURSE PLATFORMS
    // ==========================================

    "coursera.org",
    "www.coursera.org",

    "udemy.com",
    "www.udemy.com",

    "edx.org",
    "www.edx.org",

    "udacity.com",
    "www.udacity.com",

    "pluralsight.com",
    "www.pluralsight.com",

    "codecademy.com",
    "www.codecademy.com",

    "futurelearn.com",
    "www.futurelearn.com",

    "skillshare.com",
    "www.skillshare.com",

    "alison.com",
    "www.alison.com",

    "datacamp.com",
    "www.datacamp.com",

    "educative.io",
    "www.educative.io",

    "scrimba.com",
    "www.scrimba.com",


    // ==========================================
    // GENERAL EDUCATION
    // ==========================================

    "khanacademy.org",
    "www.khanacademy.org",

    "openstax.org",
    "www.openstax.org",

    "britannica.com",
    "www.britannica.com",


    // ==========================================
    // BIOLOGY & LIFE SCIENCE
    // ==========================================

    "ncbi.nlm.nih.gov",
    "nih.gov",
    "www.nih.gov",
    "genome.gov",
    "www.genome.gov",

    "biointeractive.org",
    "www.biointeractive.org",


    // ==========================================
    // SCIENCE & SPACE
    // ==========================================

    "science.nasa.gov",
    "nasa.gov",
    "www.nasa.gov",


    // ==========================================
    // MATHEMATICS
    // ==========================================

    "mathworld.wolfram.com",
    "math.mit.edu",

    "brilliant.org",
    "www.brilliant.org",


    // ==========================================
    // UNIVERSITY & ACADEMIC LEARNING
    // ==========================================

    "ocw.mit.edu",

    "open.edu",
    "www.open.edu",

    "online.stanford.edu",

    "pll.harvard.edu",


    // ==========================================
    // RESEARCH
    // ==========================================

    "nature.com",
    "www.nature.com",

    "science.org",
    "www.science.org",

    "pubmed.ncbi.nlm.nih.gov",


    // ==========================================
    // BUSINESS & FINANCE
    // ==========================================

    "investopedia.com",
    "www.investopedia.com",

    "corporatefinanceinstitute.com",
    "www.corporatefinanceinstitute.com"
};

            return trustedDomains.Any(domain =>
                host == domain ||
                host.EndsWith("." + domain));
        }


        // ==========================================
        // CHECK IF RESOURCE URL ACTUALLY WORKS
        // ==========================================

        private async Task<bool>
            IsResourceUrlWorkingAsync(
                string url)
        {
            try
            {
                using var cancellationTokenSource =
                    new CancellationTokenSource(
                        TimeSpan.FromSeconds(7));

                using var request =
                    new HttpRequestMessage(
                        HttpMethod.Get,
                        url);

                request.Headers.UserAgent.ParseAdd(
                    "Mozilla/5.0 TaskFlow/1.0");

                using var response =
                    await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationTokenSource.Token);

                return response.IsSuccessStatusCode;
            }
            catch (
                OperationCanceledException)
            {
                return false;
            }
            catch (
                HttpRequestException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TaskFlow.Services.Implementations
{
    public class EmailService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public EmailService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task SendVerificationEmailAsync(
            string email,
            string verificationLink)
        {
            var apiKey =
                _configuration["Brevo:ApiKey"];

            var senderEmail =
                _configuration["Brevo:SenderEmail"];

            var senderName =
                _configuration["Brevo:SenderName"] ?? "TaskFlow";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "Brevo API key is missing.");
            }

            if (string.IsNullOrWhiteSpace(senderEmail))
            {
                throw new InvalidOperationException(
                    "Brevo sender email is missing.");
            }

            var requestBody = new
            {
                sender = new
                {
                    name = senderName,
                    email = senderEmail
                },

                to = new[]
                {
                    new
                    {
                        email = email
                    }
                },

                subject = "Verify your TaskFlow email",

                htmlContent = $"""
                <!DOCTYPE html>
                <html>
                <body style="font-family:Arial,sans-serif;">

                    <h2>Welcome to TaskFlow 🚀</h2>

                    <p>
                        Thank you for creating your TaskFlow account.
                    </p>

                    <p>
                        Please verify your email address before logging in.
                    </p>

                    <p>
                        <a href="{verificationLink}"
                           style="
                           display:inline-block;
                           background:#0d6efd;
                           color:white;
                           padding:12px 20px;
                           text-decoration:none;
                           border-radius:6px;">

                            Verify Email

                        </a>
                    </p>

                    <p>
                        If you did not create this account,
                        you can ignore this email.
                    </p>

                    <p>
                        TaskFlow
                    </p>

                </body>
                </html>
                """
            };

            var json =
                JsonSerializer.Serialize(requestBody);

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://api.brevo.com/v3/smtp/email");

            request.Headers.Add(
                "api-key",
                apiKey);

            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue(
                    "application/json"));

            request.Content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            var client =
                _httpClientFactory.CreateClient();

            var response =
                await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error =
                    await response.Content.ReadAsStringAsync();

                throw new InvalidOperationException(
                    "Brevo email error: " + error);
            }
        }
    }
}
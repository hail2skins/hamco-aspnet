using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hamco.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hamco.Services;

public class MailjetTransactionalEmailService : ITransactionalEmailService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MailjetTransactionalEmailService> _logger;

    public MailjetTransactionalEmailService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<MailjetTransactionalEmailService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public Task SendVerificationEmailAsync(string toEmail, string verificationLink)
    {
        const string subject = "Verify your HAMCO account";
        var text = $"Please verify your email by clicking this link: {verificationLink}. This link expires in 20 minutes.";
        var html = $@"
<h3>Verify your email</h3>
<p>Thanks for registering with HAMCO.</p>
<p>Please verify your email by clicking the link below:</p>
<p><a href=""{verificationLink}"">Verify Email</a></p>
<p><strong>Note:</strong> This link expires in 20 minutes.</p>
<p>If you did not create an account, you can ignore this email.</p>";

        return SendMailAsync(toEmail, subject, text, html);
    }

    public Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
    {
        const string subject = "Reset your HAMCO password";
        var text = $"Reset your password by clicking this link: {resetLink}. This link expires in 20 minutes.";
        var html = $@"
<h3>Password reset request</h3>
<p>We received a request to reset your HAMCO password.</p>
<p>Click the link below to set a new password:</p>
<p><a href=""{resetLink}"">Reset Password</a></p>
<p><strong>Note:</strong> This link expires in 20 minutes.</p>
<p>If you did not request this, you can ignore this email.</p>";

        return SendMailAsync(toEmail, subject, text, html);
    }

    private async Task SendMailAsync(string toEmail, string subject, string textPart, string htmlPart)
    {
        var apiKey = _configuration["MAILJET_API_KEY"];
        var apiSecret = _configuration["MAILJET_API_SECRET"];
        var fromEmail = _configuration["DEFAULT_FROM_EMAIL"];
        var fromName = _configuration["DEFAULT_FROM_NAME"] ?? "HAMCO";

        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(apiSecret) ||
            string.IsNullOrWhiteSpace(fromEmail))
        {
            _logger.LogWarning("Mailjet not configured. Skipping email send to {Email}", toEmail);
            return;
        }

        var payload = new
        {
            Messages = new[]
            {
                new
                {
                    From = new { Email = fromEmail, Name = fromName },
                    To = new[] { new { Email = toEmail } },
                    Subject = subject,
                    TextPart = textPart,
                    HTMLPart = htmlPart
                }
            }
        };

        var client = _httpClientFactory.CreateClient();
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{apiKey}:{apiSecret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("https://api.mailjet.com/v3.1/send", content);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("Mailjet send failed ({StatusCode}): {Body}", response.StatusCode, body);
        }
    }
}

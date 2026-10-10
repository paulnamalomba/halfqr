using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace HalfQR.Identity.Email;

// https://resend.com/docs/api-reference/emails/send-email
public sealed class ResendEmailSender(HttpClient httpClient, IOptions<ResendOptions> options, ILogger<ResendEmailSender> logger) : IEmailSender
{
    private readonly ResendOptions _options = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Resend is not configured. Set Resend:ApiKey.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(new
            {
                from = _options.FromAddress,
                to = new[] { message.To },
                reply_to = _options.ReplyTo,
                subject = message.Subject,
                html = message.Html,
                text = message.Text,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Resend rejected an email with status {StatusCode}.", (int)response.StatusCode);
            throw new InvalidOperationException("The verification email could not be sent.");
        }
    }
}

// Development fallback when no Resend key is set: writes the message to the log instead of sending it.
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public bool IsConfigured => true;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogWarning("Resend is not configured. Email to {To} not sent. Subject: {Subject}{NewLine}{Text}", message.To, message.Subject, Environment.NewLine, message.Text);
        return Task.CompletedTask;
    }
}

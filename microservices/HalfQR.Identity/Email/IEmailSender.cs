namespace HalfQR.Identity.Email;

public sealed record EmailMessage(string To, string Subject, string Html, string Text);

public interface IEmailSender
{
    bool IsConfigured { get; }

    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public sealed class ResendOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string FromAddress { get; set; } = "HalfQR <no-reply@halfqr.com>";

    public string? ReplyTo { get; set; }
}

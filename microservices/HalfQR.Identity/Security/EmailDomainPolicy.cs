using System.Net.Mail;

namespace HalfQR.Identity.Security;

public enum EmailRejection
{
    None,
    Invalid,
    Disposable,
    Spam,
}

// Blocks sign-ins from disposable and known spam domains. See Resources/README.md for list sources.
public sealed class EmailDomainPolicy
{
    private readonly HashSet<string> _disposable;
    private readonly HashSet<string> _spam;
    private readonly HashSet<string> _trusted;

    public EmailDomainPolicy(IHostEnvironment environment)
    {
        var resources = Path.Combine(AppContext.BaseDirectory, "Resources");
        _disposable = Load(Path.Combine(resources, "disposable-email-domains.txt"));
        _spam = Load(Path.Combine(resources, "spam-domains.txt"));
        _trusted = Load(Path.Combine(resources, "trusted-email-providers.txt"));

        if (_disposable.Count == 0 && !environment.IsDevelopment())
        {
            throw new InvalidOperationException("Email domain blocklists are missing from the Identity Resources folder.");
        }
    }

    public static string Normalize(string? email)
        => (email ?? string.Empty).Trim().ToLowerInvariant();

    public EmailRejection Evaluate(string? email)
    {
        var normalized = Normalize(email);

        if (normalized.Length is 0 or > 254 || !MailAddress.TryCreate(normalized, out var address) || address.Address != normalized)
        {
            return EmailRejection.Invalid;
        }

        var domain = address.Host;

        if (!domain.Contains('.'))
        {
            return EmailRejection.Invalid;
        }

        foreach (var candidate in DomainAndParents(domain))
        {
            if (_disposable.Contains(candidate))
            {
                return EmailRejection.Disposable;
            }
        }

        if (_trusted.Contains(domain))
        {
            return EmailRejection.None;
        }

        foreach (var candidate in DomainAndParents(domain))
        {
            if (_spam.Contains(candidate))
            {
                return EmailRejection.Spam;
            }
        }

        return EmailRejection.None;
    }

    public static string Describe(EmailRejection rejection)
        => rejection switch
        {
            EmailRejection.Invalid => "Enter a valid email address.",
            EmailRejection.Disposable => "Disposable email addresses cannot be used. Use a permanent work or personal address.",
            EmailRejection.Spam => "This email domain is not accepted. Use a different work or personal address.",
            _ => string.Empty,
        };

    // "a.b.example.com" yields "a.b.example.com", "b.example.com", "example.com".
    private static IEnumerable<string> DomainAndParents(string domain)
    {
        var current = domain;

        while (current.Contains('.'))
        {
            yield return current;
            current = current[(current.IndexOf('.') + 1)..];
        }
    }

    private static HashSet<string> Load(string path)
        => File.Exists(path)
            ? File.ReadLines(path)
                .Select(static line => line.Trim().ToLowerInvariant())
                .Where(static line => line.Length > 0 && !line.StartsWith('#'))
                .ToHashSet(StringComparer.Ordinal)
            : [];
}

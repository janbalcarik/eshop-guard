using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using EshopGuard.Application.Email;

namespace EshopGuard.Api.Tests;

/// <summary>Captures sent e-mails instead of SMTP; <see cref="Fail"/> makes every send fail like an unreachable server.</summary>
internal sealed partial class CapturingEmailTransport : IEmailTransport
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public volatile bool Fail;

    public int Attempts;

    public IReadOnlyList<EmailMessage> Sent => [.. _sent];

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        Interlocked.Increment(ref Attempts);
        if (Fail)
        {
            throw new EmailSendException("email.smtp_unavailable");
        }

        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>E-mails to an address.</summary>
    public IReadOnlyList<EmailMessage> To(string address) => Sent.Where(m => m.To == address).ToList();

    /// <summary>The token of the last e-mail of the kind to the address (from <c>#t=…</c> of its link).</summary>
    public string LastToken(string address, string kind)
    {
        var message = Sent.LastOrDefault(m => m.To == address && m.Kind == kind)
            ?? throw new InvalidOperationException($"No e-mail {kind} to the address.");
        return TokenOf(message);
    }

    public static string TokenOf(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var match = TokenInLink().Match(message.Text);
        return match.Success ? match.Groups["token"].Value : throw new InvalidOperationException("No token in the e-mail.");
    }

    [GeneratedRegex(@"#t=(?<token>[A-Za-z0-9_-]{43})")]
    private static partial Regex TokenInLink();
}

namespace EshopGuard.Application.Email;

/// <summary>A composed e-mail; <see cref="Kind"/> is only the code of the template (for logs and tests).</summary>
public sealed record EmailMessage(string To, string Subject, string Html, string Text, string Kind, string Locale);

/// <summary>Sends composed e-mails (SMTP, or a capturing transport in the tests).</summary>
public interface IEmailTransport
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

/// <summary>Sending failed (the transport's error is not exposed: it may name the server or the address).</summary>
public sealed class EmailSendException(string code, Exception? inner = null) : Exception(code, inner)
{
    public string Code { get; } = code;
}

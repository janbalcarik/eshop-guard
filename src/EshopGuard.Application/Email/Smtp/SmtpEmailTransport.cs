using EshopGuard.Application.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EshopGuard.Application.Email.Smtp;

/// <summary>
/// Sends through SMTP (MailKit) with <c>Email:Smtp:*</c>; the password comes only from user-secrets or the environment. Locally
/// Mailpit (<c>deploy/docker-compose.dev.yml</c>, port 1025, <c>Security = None</c>). A failure is
/// <see cref="EmailSendException"/> with a code; the server's message is not passed on (it may name the address).
/// </summary>
public sealed class SmtpEmailTransport(IOptions<EmailOptions> options) : IEmailTransport
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Smtp.Host))
        {
            throw new EmailSendException("email.smtp_not_configured");
        }

        using var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromName, settings.From));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.Html, TextBody = message.Text }.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            var security = settings.Smtp.Security switch
            {
                "None" => SecureSocketOptions.None,
                "SslOnConnect" => SecureSocketOptions.SslOnConnect,
                _ => SecureSocketOptions.StartTls,
            };
            await client.ConnectAsync(settings.Smtp.Host, settings.Smtp.Port, security, ct).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(settings.Smtp.UserName))
            {
                await client.AuthenticateAsync(settings.Smtp.UserName, settings.Smtp.Password ?? string.Empty, ct).ConfigureAwait(false);
            }

            await client.SendAsync(mime, ct).ConfigureAwait(false);
            await client.DisconnectAsync(quit: true, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            throw new EmailSendException(ex switch
            {
                AuthenticationException => "email.smtp_auth_failed",
                SmtpCommandException => "email.smtp_rejected",
                OperationCanceledException or TimeoutException => "email.smtp_timeout",
                _ => "email.smtp_unavailable",
            }, ex);
        }
    }
}

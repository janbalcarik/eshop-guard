using EshopGuard.Application.Options;
using EshopGuard.Application.Problems;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Email;

/// <summary>
/// Sends an e-mail with a token right from the request (AD 3): time limit <c>Email:SendTimeoutSeconds</c> and one more
/// attempt; a failure is <c>503 email.send_failed</c> (the caller invalidates the token first). Logs only codes.
/// </summary>
public sealed partial class DirectEmailSender(IEmailTransport transport, IOptions<EmailOptions> options, ILogger<DirectEmailSender> logger)
{
    public const int Attempts = 2;

    /// <summary>Sends; <c>false</c> when both attempts failed (the code is logged).</summary>
    public async Task<bool> TrySendAsync(EmailMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.SendTimeoutSeconds));
            try
            {
                await transport.SendAsync(message, timeout.Token).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && ex is EmailSendException or OperationCanceledException or IOException)
            {
                LogSendFailed(logger, message.Kind, attempt, ex is EmailSendException e ? e.Code : "email.smtp_timeout");
            }
        }

        return false;
    }

    /// <summary>The error of a failed send.</summary>
    public static DomainException Failed() => new(ProblemCodes.EmailSendFailed, 503);

    [LoggerMessage(Level = LogLevel.Warning, Message = "email.send_failed {Kind} {Attempt} {Code}")]
    private static partial void LogSendFailed(ILogger logger, string kind, int attempt, string code);
}

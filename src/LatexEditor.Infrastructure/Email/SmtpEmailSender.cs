using System.Net;
using System.Net.Mail;
using LatexEditor.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LatexEditor.Infrastructure.Email;

/// <summary>
/// <see cref="IEmailSender"/> over SMTP via <see cref="SmtpClient"/>. When no
/// SMTP host is configured, the message is written to the log instead — the
/// development default so confirmation links are testable without a mail server.
/// </summary>
public class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    /// <inheritdoc />
    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            logger.LogInformation(
                "Email suppressed (no SMTP host configured). To: {To}, Subject: {Subject}, Body: {Body}",
                toEmail, subject, htmlBody);
            return;
        }

        using var message = new MailMessage(_options.FromAddress, toEmail, subject, htmlBody)
        {
            IsBodyHtml = true
        };
        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.UseSsl
        };
        if (!string.IsNullOrWhiteSpace(_options.SmtpUser))
        {
            client.Credentials = new NetworkCredential(_options.SmtpUser, _options.SmtpPassword);
        }

        await client.SendMailAsync(message, ct);
    }
}

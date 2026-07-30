namespace LatexEditor.Core.Interfaces;

/// <summary>
/// Sends transactional email (account confirmation, etc.).
/// </summary>
public interface IEmailSender
{
    /// <summary>Sends an email with the given subject and HTML body.</summary>
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}

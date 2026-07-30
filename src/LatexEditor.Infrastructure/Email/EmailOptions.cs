namespace LatexEditor.Infrastructure.Email;

/// <summary>
/// Email configuration, bound from the <c>Email</c> configuration section
/// (or <c>Email__*</c> environment variables). When neither a Resend API key
/// nor an SMTP host is configured, emails are logged instead of sent
/// (development default).
/// </summary>
public class EmailOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Email";

    /// <summary>Resend API key. Set to send via Resend (https://resend.com).</summary>
    public string ResendApiKey { get; set; } = string.Empty;

    /// <summary>SMTP server hostname. Empty means "log emails, don't send".</summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>SMTP port. 587 for STARTTLS, 465 for implicit TLS.</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>SMTP username. Load from environment variables, never commit.</summary>
    public string SmtpUser { get; set; } = string.Empty;

    /// <summary>SMTP password. Load from environment variables, never commit.</summary>
    public string SmtpPassword { get; set; } = string.Empty;

    /// <summary>Sender address (From header).</summary>
    public string FromAddress { get; set; } = "noreply@example.com";

    /// <summary>Whether to enable SSL/TLS for the SMTP connection.</summary>
    public bool UseSsl { get; set; } = true;
}

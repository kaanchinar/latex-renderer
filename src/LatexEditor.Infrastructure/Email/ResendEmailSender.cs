using System.Net.Http.Headers;
using System.Net.Http.Json;
using LatexEditor.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LatexEditor.Infrastructure.Email;

/// <summary>
/// <see cref="IEmailSender"/> backed by the Resend HTTP API
/// (<c>https://api.resend.com/emails</c>).
/// </summary>
public class ResendEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> options,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    /// <inheritdoc />
    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new
            {
                from = _options.FromAddress,
                to = new[] { toEmail },
                subject,
                html = htmlBody
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ResendApiKey);

        var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Resend API rejected the email to {To}: {Status} {Body}",
                toEmail, (int)response.StatusCode, body);
            throw new HttpRequestException($"Resend API returned {(int)response.StatusCode}.");
        }
    }
}

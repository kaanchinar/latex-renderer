using System.Net;
using LatexEditor.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LatexEditor.Infrastructure.Tests;

public class ResendEmailSenderTests
{
    private const string ApiKey = "re_test_key";

    private static (ResendEmailSender Sender, List<(HttpRequestMessage Request, string Body)> Requests) CreateSender(
        HttpStatusCode responseStatus = HttpStatusCode.OK)
    {
        var requests = new List<(HttpRequestMessage, string)>();
        var handler = new CaptureHandler(requests, responseStatus);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var options = Options.Create(new EmailOptions
        {
            ResendApiKey = ApiKey,
            FromAddress = "noreply@example.com"
        });
        return (new ResendEmailSender(httpClient, options, NullLogger<ResendEmailSender>.Instance), requests);
    }

    [Fact]
    public async Task SendAsync_PostsToResendApiWithBearerTokenAndPayload()
    {
        var (sender, requests) = CreateSender();

        await sender.SendAsync("user@example.com", "Subject", "<p>Body</p>");

        var (request, body) = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.resend.com/emails", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal(ApiKey, request.Headers.Authorization.Parameter);

        Assert.Contains("from", body);
        Assert.Contains("noreply@example.com", body);
        Assert.Contains("user@example.com", body);
        Assert.Contains("Subject", body);
        Assert.Contains("Body", body);  // HTML is JSON-escaped in the payload

    }

    [Fact]
    public async Task SendAsync_ApiError_Throws()
    {
        var (sender, _) = CreateSender(HttpStatusCode.Unauthorized);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sender.SendAsync("user@example.com", "Subject", "<p>Body</p>"));
    }

    private sealed class CaptureHandler(
        List<(HttpRequestMessage, string)> requests, HttpStatusCode status) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // The sender disposes the request after sending, so capture the body eagerly.
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            requests.Add((request, body));
            return new HttpResponseMessage(status) { Content = new StringContent("{}") };
        }
    }
}

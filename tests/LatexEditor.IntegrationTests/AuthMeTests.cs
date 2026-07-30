using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LatexEditor.IntegrationTests;

/// <summary>
/// End-to-end tests for the <c>GET /api/auth/me</c> endpoint.
/// </summary>
public class AuthMeTests(LatexEditorApiFactory factory) : IClassFixture<LatexEditorApiFactory>
{
    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AfterRegister_ReturnsCurrentUser()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var email = $"me-{Guid.NewGuid():N}@example.com";
        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "secret123" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(content);
        Assert.False(string.IsNullOrWhiteSpace(content.Id));
        Assert.Equal(email, content.Email);
    }

    private sealed class MeResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}

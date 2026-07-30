using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.Json;

namespace LatexEditor.IntegrationTests;

/// <summary>
/// End-to-end smoke test: register, create project, upload a .tex file,
/// trigger a compile, poll until completion, and download the PDF.
/// Runs against a real PostgreSQL container with the real compile worker.
/// </summary>
public class CompileSmokeTests(LatexEditorApiFactory factory) : IClassFixture<LatexEditorApiFactory>
{
    [Fact]
    public async Task FullCompileFlow_ProducesDownloadablePdf()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        // Register
        var email = $"smoke-{Guid.NewGuid():N}@example.com";
        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "secret123" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        // Create project
        var projectResponse = await client.PostAsJsonAsync("/api/projects", new { name = "Smoke Test" });
        Assert.Equal(HttpStatusCode.OK, projectResponse.StatusCode);
        using var projectDoc = JsonDocument.Parse(await projectResponse.Content.ReadAsStringAsync());
        var projectId = projectDoc.RootElement.GetProperty("id").GetGuid();

        // Upload main.tex
        var upload = await client.PutAsJsonAsync($"/api/projects/{projectId}/files/main.tex",
            new { content = "\\documentclass{article}\\begin{document}Hi\\end{document}" });
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        // Trigger compile
        var compile = await client.PostAsync($"/api/projects/{projectId}/compile", null);
        Assert.Equal(HttpStatusCode.OK, compile.StatusCode);
        using var jobDoc = JsonDocument.Parse(await compile.Content.ReadAsStringAsync());
        var jobId = jobDoc.RootElement.GetProperty("id").GetGuid();

        // Poll for completion
        var status = await PollJobStatusAsync(client, projectId, jobId);
        Assert.Equal("Success", status);

        // Download PDF: endpoint redirects to the storage URL
        var pdfRedirect = await client.GetAsync($"/api/projects/{projectId}/jobs/{jobId}/pdf");
        Assert.Equal(HttpStatusCode.Found, pdfRedirect.StatusCode);

        var pdf = await client.GetAsync(pdfRedirect.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }

    [Fact]
    public async Task TriggerCompile_RateLimitedAfterPermitLimit()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        var email = $"rl-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new { email, password = "secret123" });
        var projectResponse = await client.PostAsJsonAsync("/api/projects", new { name = "Rate Limited" });
        using var projectDoc = JsonDocument.Parse(await projectResponse.Content.ReadAsStringAsync());
        var projectId = projectDoc.RootElement.GetProperty("id").GetGuid();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < LatexEditorApiFactory.CompilePermitLimit + 1; i++)
        {
            var response = await client.PostAsync($"/api/projects/{projectId}/compile", null);
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(LatexEditorApiFactory.CompilePermitLimit, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task Unauthenticated_Requests_Return401()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/projects")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsync($"/api/projects/{Guid.NewGuid()}/compile", null)).StatusCode);
    }

    [Fact]
    public async Task ForeignProject_Returns404()
    {
        using var ownerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var otherClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        await ownerClient.PostAsJsonAsync("/api/auth/register",
            new { email = $"owner-{Guid.NewGuid():N}@example.com", password = "secret123" });
        await otherClient.PostAsJsonAsync("/api/auth/register",
            new { email = $"other-{Guid.NewGuid():N}@example.com", password = "secret123" });

        var projectResponse = await ownerClient.PostAsJsonAsync("/api/projects", new { name = "Private" });
        using var projectDoc = JsonDocument.Parse(await projectResponse.Content.ReadAsStringAsync());
        var projectId = projectDoc.RootElement.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound,
            (await otherClient.GetAsync($"/api/projects/{projectId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await otherClient.PostAsync($"/api/projects/{projectId}/compile", null)).StatusCode);
    }

    private static async Task<string> PollJobStatusAsync(HttpClient client, Guid projectId, Guid jobId)
    {
        for (var i = 0; i < 30; i++)
        {
            var response = await client.GetAsync($"/api/projects/{projectId}/jobs");
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var job = doc.RootElement.EnumerateArray().First(j => j.GetProperty("id").GetGuid() == jobId);
            var status = job.GetProperty("status").GetString()!;
            if (status is "Success" or "Failed" or "Cancelled") return status;
            await Task.Delay(500);
        }
        throw new TimeoutException("Compile job did not reach a terminal state in time.");
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LatexEditor.Api.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;

namespace LatexEditor.IntegrationTests;

/// <summary>
/// SignalR hub integration tests over the in-memory test server: join a
/// project group, trigger a compile through the hub, and receive the
/// real-time completion event.
/// </summary>
public class ProjectHubIntegrationTests(LatexEditorApiFactory factory) : IClassFixture<LatexEditorApiFactory>
{
    [Fact]
    public async Task CompileViaHub_PushesCompletedEventWithPdfUrl()
    {
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });

        var email = $"hub-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "secret123" });
        registerResponse.EnsureSuccessStatusCode();

        var projectResponse = await client.PostAsJsonAsync("/api/projects", new { name = "Hub Project" });
        using var projectDoc = JsonDocument.Parse(await projectResponse.Content.ReadAsStringAsync());
        var projectId = projectDoc.RootElement.GetProperty("id").GetGuid();

        await client.PutAsJsonAsync($"/api/projects/{projectId}/files/main.tex",
            new { content = "\\documentclass{article}\\begin{document}Hub\\end{document}" });

        // The auth cookie must be replayed on the hub connection.
        var authCookie = registerResponse.Headers.GetValues("Set-Cookie")
            .First(h => h.StartsWith(".AspNetCore.Identity.Application", StringComparison.Ordinal))
            .Split(';')[0];

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/projects"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Headers.Add("Cookie", authCookie);
            })
            .Build();

        try
        {
            var completed = new TaskCompletionSource<(Guid JobId, string PdfUrl)>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            connection.On<Guid, string>("CompileCompleted",
                (jobId, pdfUrl) => completed.TrySetResult((jobId, pdfUrl)));

            var started = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.On<Guid>("CompileStarted", jobId => started.TrySetResult(jobId));

            await connection.StartAsync();
            await connection.InvokeAsync("JoinProject", projectId);

            var jobId = await connection.InvokeAsync<Guid>("TriggerCompile", projectId);

            var startedJobId = await started.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(jobId, startedJobId);

            var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(jobId, result.JobId);
            Assert.False(string.IsNullOrEmpty(result.PdfUrl));
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}

using LatexEditor.Core.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace LatexEditor.IntegrationTests;

/// <summary>
/// Spins up the full API against a real PostgreSQL container, a temp-dir local
/// storage, and a fake Tectonic compiler that produces a valid-looking PDF.
/// The real compile worker and queue run, so jobs are processed for real.
/// </summary>
public class LatexEditorApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), $"latex-integration-tests-{Guid.NewGuid():N}");

    /// <summary>Rate limit for compile tests: low so the 429 path is cheap to reach.</summary>
    public const int CompilePermitLimit = 2;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["Storage:Provider"] = "Local",
                ["Storage:LocalRootPath"] = _storageRoot,
                ["RateLimiting:CompilePermitLimit"] = CompilePermitLimit.ToString(),
                ["RateLimiting:CompileWindowSeconds"] = "60"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITectonicCompiler>();
            services.AddSingleton<ITectonicCompiler>(new FakeTectonicCompiler());
        });
    }

    public new async Task DisposeAsync()
    {
        if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true);
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    private sealed class FakeTectonicCompiler : ITectonicCompiler
    {
        public Task<TectonicResult> CompileAsync(string workingDirectory, string entryFile, CancellationToken ct = default)
        {
            var pdfPath = Path.Combine(workingDirectory, "main.pdf");
            File.WriteAllText(pdfPath, "%PDF-1.7 integration-test");
            return Task.FromResult(new TectonicResult
            {
                ExitCode = 0,
                StdOut = "fake compile ok",
                OutputPdfPath = pdfPath
            });
        }
    }
}

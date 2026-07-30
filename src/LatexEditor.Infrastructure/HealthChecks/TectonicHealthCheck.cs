using System.Diagnostics;
using LatexEditor.Infrastructure.Compile;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace LatexEditor.Infrastructure.HealthChecks;

/// <summary>
/// Verifies the Tectonic executable is present and runnable by invoking
/// <c>--version</c> with a short timeout.
/// </summary>
public class TectonicHealthCheck(IOptions<TectonicOptions> options) : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(Timeout);

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = options.Value.ExecutablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                ArgumentList = { "--version" }
            });
            if (process is null)
                return HealthCheckResult.Unhealthy("Failed to start the Tectonic process.");

            await process.WaitForExitAsync(timeoutCts.Token);
            return process.ExitCode == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Tectonic exited with code {process.ExitCode}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Tectonic did not respond in time.");
        }
        catch (Exception e)
        {
            return HealthCheckResult.Unhealthy($"Tectonic is not runnable: {e.Message}");
        }
    }
}

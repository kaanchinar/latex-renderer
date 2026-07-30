using LatexEditor.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LatexEditor.Infrastructure.HealthChecks;

/// <summary>
/// Verifies the configured object storage is reachable by probing for a
/// non-existent key (a cheap existence check that requires no setup object).
/// </summary>
public class StorageHealthCheck(IFileStorage storage) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await storage.ExistsAsync("health-check-probe", cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception e)
        {
            return HealthCheckResult.Unhealthy($"Storage probe failed: {e.Message}");
        }
    }
}

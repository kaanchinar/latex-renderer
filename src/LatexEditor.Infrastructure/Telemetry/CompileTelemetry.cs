using System.Diagnostics;

namespace LatexEditor.Infrastructure.Telemetry;

/// <summary>
/// Central telemetry definitions for the compile pipeline: the OpenTelemetry
/// activity source and Prometheus metrics (compile duration, outcomes, queue
/// depth, active hub connections).
/// </summary>
public static class CompileTelemetry
{
    /// <summary>Activity source name for compile pipeline tracing.</summary>
    public const string ActivitySourceName = "LatexEditor.Compile";

    /// <summary>Activity source for compile pipeline spans.</summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly Prometheus.Counter JobsCompleted = Prometheus.Metrics.CreateCounter(
        "latex_compile_jobs_completed_total", "Compile jobs that reached a terminal state.",
        new Prometheus.CounterConfiguration { LabelNames = ["outcome"] });

    private static readonly Prometheus.Histogram JobDuration = Prometheus.Metrics.CreateHistogram(
        "latex_compile_job_duration_seconds", "Compile job wall-clock duration in seconds.",
        new Prometheus.HistogramConfiguration
        {
            Buckets = Prometheus.Histogram.ExponentialBuckets(1, 2, 8)
        });

    private static readonly Prometheus.Gauge QueueDepth = Prometheus.Metrics.CreateGauge(
        "latex_compile_queue_depth", "Jobs currently waiting in the compile queue.");

    private static readonly Prometheus.Gauge HubConnections = Prometheus.Metrics.CreateGauge(
        "latex_hub_connections_active", "Active SignalR connections on the project hub.");

    /// <summary>Records a terminal job outcome and its duration in seconds.</summary>
    public static void RecordJobCompleted(string outcome, double durationSeconds)
    {
        JobsCompleted.WithLabels(outcome).Inc();
        JobDuration.Observe(durationSeconds);
    }

    /// <summary>Adjusts the compile queue depth gauge by the given delta.</summary>
    public static void ChangeQueueDepth(int delta) => QueueDepth.Inc(delta);

    /// <summary>Adjusts the active hub connection gauge by the given delta.</summary>
    public static void ChangeHubConnections(int delta) => HubConnections.Inc(delta);
}

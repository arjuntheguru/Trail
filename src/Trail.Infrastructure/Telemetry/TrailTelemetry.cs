using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Trail.Infrastructure.Telemetry;

/// <summary>Application-wide activity source and meter. The API host registers both with OpenTelemetry.</summary>
public static class TrailTelemetry
{
    public const string Name = "Trail";

    public static readonly ActivitySource ActivitySource = new(Name);
    public static readonly Meter Meter = new(Name);

    private static readonly Counter<long> JobRuns =
        Meter.CreateCounter<long>("trail.jobs.runs", description: "Background job executions, successful or not.");

    private static readonly Counter<long> JobFailures =
        Meter.CreateCounter<long>("trail.jobs.failures", description: "Background job executions that threw.");

    /// <summary>Counts one execution of <paramref name="job"/>.</summary>
    public static void RecordJobRun(string job) => JobRuns.Add(1, new KeyValuePair<string, object?>("job", job));

    /// <summary>Counts one failed execution of <paramref name="job"/>.</summary>
    public static void RecordJobFailure(string job) => JobFailures.Add(1, new KeyValuePair<string, object?>("job", job));

    /// <summary>Starts an activity named after the job; dispose it when the run ends. Null when nobody listens.</summary>
    public static Activity? StartJob(string job) =>
        ActivitySource.StartActivity($"job {job}", ActivityKind.Internal)?.SetTag("job", job);
}

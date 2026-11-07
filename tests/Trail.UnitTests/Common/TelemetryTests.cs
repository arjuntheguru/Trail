using System.Diagnostics.Metrics;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

using Trail.Api.Infrastructure;
using Trail.Infrastructure.Telemetry;

namespace Trail.UnitTests.Common;

public sealed class TelemetryTests
{
    [Fact]
    public void Job_counters_are_tagged_with_the_job_name()
    {
        var seen = new List<(string Instrument, long Value, string? Job)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == TrailTelemetry.Name) l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var job = tags.ToArray().Where(t => t.Key == "job").Select(t => t.Value as string).SingleOrDefault();
            lock (seen) seen.Add((instrument.Name, value, job));
        });
        listener.Start();

        TrailTelemetry.RecordJobRun("unit-test-job");
        TrailTelemetry.RecordJobFailure("unit-test-job");

        lock (seen)
        {
            seen.ShouldContain(("trail.jobs.runs", 1L, "unit-test-job"));
            seen.ShouldContain(("trail.jobs.failures", 1L, "unit-test-job"));
        }
    }

    [Fact]
    public void Otlp_endpoint_comes_from_standard_or_app_setting_and_is_absent_by_default()
    {
        static IConfiguration Config(params (string, string)[] values) =>
            new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Item1, v => (string?)v.Item2)).Build();

        Observability.OtlpEndpoint(Config()).ShouldBeNull();
        Observability.OtlpEndpoint(Config(("OpenTelemetry:OtlpEndpoint", "http://collector:4317")))!.Host.ShouldBe("collector");
        Observability.OtlpEndpoint(Config(
            ("OTEL_EXPORTER_OTLP_ENDPOINT", "http://standard:4317"),
            ("OpenTelemetry:OtlpEndpoint", "http://collector:4317")))!.Host.ShouldBe("standard");
        Observability.OtlpEndpoint(Config(("OpenTelemetry:OtlpEndpoint", "not a uri"))).ShouldBeNull();
    }

    [Fact]
    public void Health_probes_are_not_traced()
    {
        var probe = new DefaultHttpContext();
        probe.Request.Path = "/health/live";
        var api = new DefaultHttpContext();
        api.Request.Path = "/api/v1/sites";

        Observability.IsTraced(probe).ShouldBeFalse();
        Observability.IsTraced(api).ShouldBeTrue();
    }
}
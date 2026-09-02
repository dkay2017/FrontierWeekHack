using System.Diagnostics;
using TireForge.Core.Model;
using TireForge.Core.Observability;

namespace TireForge.Data.Tests;

/// <summary>
/// Decision D6 / Challenge 2 — every hop for a reading shares one W3C trace,
/// visible as nested spans and stored on <c>Diagnosis.TraceId</c>.
///
/// The <see cref="ActivityListener"/> is process-global, so assertions scope to
/// the run's own trace id (other test classes emit pipeline spans in parallel).
/// </summary>
public class PipelineTracingTests : IDisposable
{
    private readonly List<Activity> _stopped = new();
    private readonly ActivityListener _listener;

    public PipelineTracingTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == Telemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = a => { lock (_stopped) _stopped.Add(a); },
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    private List<Activity> ForTrace(string traceId)
    {
        lock (_stopped) return _stopped.Where(a => a.TraceId.ToString() == traceId).ToList();
    }

    private static Reading Reading(string machineId, double t, double p, double v, double r) => new()
    {
        Id = Ids.Reading(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)),
        MachineId = machineId,
        CapturedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
        Temperature = t, Pressure = p, Vibration = v, Rpm = r,
    };

    [Fact]
    public async Task Anomalous_run_emits_a_span_per_step_all_under_one_trace()
    {
        using var h = await PipelineHarness.CreateAsync();
        var result = await h.NewPipeline().RunAsync(Reading("CP-003", 198.5, 18.2, 7.3, 0));
        var spans = ForTrace(result.TraceId);

        var names = spans.Select(a => a.OperationName).ToList();
        Assert.Contains(Telemetry.Spans.Run, names);
        foreach (var step in new[]
                 {
                     Telemetry.Spans.ThresholdCheck, Telemetry.Spans.Detect, Telemetry.Spans.HistoryMatch,
                     Telemetry.Spans.Diagnose, Telemetry.Spans.Gate, Telemetry.Spans.Draft, Telemetry.Spans.Act,
                 })
            Assert.Contains(step, names);

        var root = spans.Single(a => a.OperationName == Telemetry.Spans.Run);
        Assert.Equal(root.TraceId.ToString(), result.TraceId);
        Assert.Equal(result.TraceId, result.Diagnosis!.TraceId);
        Assert.All(spans.Where(a => a.OperationName != Telemetry.Spans.Run),
            child => Assert.Equal(root.SpanId, child.ParentSpanId));
    }

    [Fact]
    public async Task Non_anomalous_run_stops_after_detection_span()
    {
        using var h = await PipelineHarness.CreateAsync();
        var result = await h.NewPipeline().RunAsync(Reading("EX-002", 115, 12.5, 2.1, 30));

        var names = ForTrace(result.TraceId).Select(a => a.OperationName).ToList();
        Assert.Contains(Telemetry.Spans.ThresholdCheck, names);
        Assert.Contains(Telemetry.Spans.Detect, names);
        Assert.DoesNotContain(Telemetry.Spans.Diagnose, names);
        Assert.DoesNotContain(Telemetry.Spans.Act, names);
    }

    [Fact]
    public async Task Root_span_is_tagged_with_reading_machine_and_route()
    {
        using var h = await PipelineHarness.CreateAsync();
        var result = await h.NewPipeline().RunAsync(Reading("CP-003", 198.5, 18.2, 7.3, 0));

        var root = ForTrace(result.TraceId).Single(a => a.OperationName == Telemetry.Spans.Run);
        Assert.Equal(result.ReadingId, root.GetTagItem(Telemetry.Tags.ReadingId));
        Assert.Equal("CP-003", root.GetTagItem(Telemetry.Tags.MachineId));
        Assert.Equal("Review", root.GetTagItem(Telemetry.Tags.GateRoute));
    }

    [Fact]
    public async Task Trace_id_is_a_32_char_hex_string()
    {
        using var h = await PipelineHarness.CreateAsync();
        var result = await h.NewPipeline().RunAsync(Reading("CP-003", 198.5, 18.2, 7.3, 0));

        Assert.Matches("^[0-9a-f]{32}$", result.TraceId);
    }
}

using System.Diagnostics;

namespace TireForge.Core.Observability;

/// <summary>
/// The pipeline's tracing source (Decision D6 / Challenge 2). One root activity per
/// <c>Pipeline.RunAsync</c> and one child per step, so every hop for a reading shares
/// a single W3C trace id — which is stored on <c>Diagnosis.TraceId</c> and shows up
/// as <c>operation_Id</c> in Application Insights.
///
/// Hosts opt in with <c>.WithTracing(t =&gt; t.AddSource(Telemetry.SourceName))</c>;
/// tests observe with an <see cref="ActivityListener"/>.
/// </summary>
public static class Telemetry
{
    public const string SourceName = "TireForge.Pipeline";

    public static readonly ActivitySource Source = new(SourceName, "1.0.0");

    // Tag keys — kept together so traces are queryable on consistent dimensions.
    public static class Tags
    {
        public const string ReadingId = "tireforge.reading_id";
        public const string MachineId = "tireforge.machine_id";
        public const string Severity = "tireforge.severity";
        public const string Anomaly = "tireforge.anomaly";
        public const string Confidence = "tireforge.confidence";
        public const string GateRoute = "tireforge.gate_route";
        public const string DiagnosisId = "tireforge.diagnosis_id";
        public const string WorkOrderId = "tireforge.work_order_id";
        public const string EarlyWarningCount = "tireforge.early_warning_count";
    }

    // Span names for the pipeline steps.
    public static class Spans
    {
        public const string Run = "pipeline.run";
        public const string ThresholdCheck = "t1.threshold_check";
        public const string Detect = "a1.detect";
        public const string HistoryMatch = "t2.history_match";
        public const string Diagnose = "a2.diagnose";
        public const string Gate = "gate";
        public const string Draft = "a3.draft";
        public const string Act = "act";
        public const string TrendCheck = "t0.trend_check";
    }
}

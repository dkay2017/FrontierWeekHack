namespace Zynara.Core.Model;

/// <summary>
/// One row of the before / after table (evaluator §16). The manual column is an
/// <b>estimate</b> with its basis stated; the Care Approval IQ column is
/// <b>measured</b> from the demo pipeline. No invented improvement figure.
/// </summary>
public sealed record BenchmarkRow(
    string Metric,
    string Manual,
    string CareApprovalIQ,
    bool ManualIsEstimate,
    string Basis);

/// <summary>Aggregated measured facts across the case corpus.</summary>
public sealed record BenchmarkMeasured(
    int Cases,
    double AvgCriteriaChecked,
    double AvgEvidenceGapsFound,
    double AvgPrecedentsConsidered,
    long AvgAssembledInMs,
    double UnsafeAutomationRate);

public sealed record BenchmarkReport(
    BenchmarkMeasured Measured,
    IReadOnlyList<BenchmarkRow> Rows,
    string Disclaimer);

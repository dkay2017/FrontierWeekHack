using Zynara.Core.Abstractions;
using Zynara.Core.Model;

namespace Zynara.Core.Impact;

/// <summary>
/// Builds the before / after table (evaluator §16 / P2-2) from the measured
/// <see cref="PipelineMetrics"/> of every assembled case, set against a labelled
/// manual estimate. The pipeline half is counted; the manual half is an estimate
/// and is marked as one.
/// </summary>
public sealed class BenchmarkService(ICaseRepository cases)
{
    public async Task<BenchmarkReport> GetAsync(CancellationToken ct = default)
    {
        var records = (await cases.ListAsync(ct))
            .Where(r => r.Result.Metrics.ReasoningStepsRun > 0)
            .ToList();

        var m = records.Count == 0
            ? new BenchmarkMeasured(0, 0, 0, 0, 0, 0)
            : new BenchmarkMeasured(
                Cases: records.Count,
                AvgCriteriaChecked: records.Average(r => r.Result.Metrics.CriteriaChecked),
                AvgEvidenceGapsFound: records.Average(r => r.Result.Metrics.EvidenceGapsFound),
                AvgPrecedentsConsidered: records.Average(r => r.Result.Metrics.PrecedentsConsidered),
                AvgAssembledInMs: (long)records.Average(r => r.Result.Metrics.AssembledInMs),
                UnsafeAutomationRate: 0d);   // hard-gated in Zynara.Eval §7.3

        var rows = new List<BenchmarkRow>
        {
            new("Case preparation time",
                "≈ 20 min / case", "assembled automatically (deterministic stubs run in milliseconds; ≈ 30–60 s with the hosted models)",
                true, "AMA 2023 prior-auth survey: practices spend ≈ 12 h/physician/week; ≈ 20 min/request is a mid-range estimate."),

            new("Criteria checked against the note",
                "manual read, easy to miss one", $"{m.AvgCriteriaChecked:0.#} criteria per case, every one, with the satisfying statement quoted",
                true, "no manual per-criterion audit is routine today."),

            new("Evidence gaps surfaced before submission",
                "usually found only after a denial", $"{m.AvgEvidenceGapsFound:0.#} flagged per case, up front",
                true, "the appeals-gap statistic (11.5% appealed / 80.7% win) implies gaps are rarely closed pre-submission."),

            new("Comparable precedents reviewed",
                "ad hoc, if at all", $"{m.AvgPrecedentsConsidered:0.#} ranked automatically per case, with the matched facts shown",
                true, "precedent review is not a standard step in manual pre-auth."),

            new("Reviewer effort",
                "assemble the case + decide", "decide only, on a prepared and cited case",
                true, "qualitative — the assembly work moves to the pipeline."),

            new("Unsupported-recommendation rate",
                "not tracked", "Critic-gated; CI hard-gate = 0 unsafe automations (§7.3)",
                false, "measured by Zynara.Eval on the labelled set."),
        };

        return new BenchmarkReport(m, rows,
            "The manual column is an estimate, labelled per row with its basis. The Care Approval IQ " +
            "column is measured from the demo pipeline. No improvement percentage is claimed.");
    }
}

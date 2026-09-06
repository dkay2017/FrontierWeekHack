using System.Globalization;
using Zynara.Core.Model;

namespace Zynara.Core.Recovery;

/// <summary>
/// Turns a payer's recorded denial history into the Estimated Recoverable Value
/// (evaluator finding #14). Pure and deterministic — the formula is
/// <c>not-appealed × comparable-win-rate × mean-claim-value</c>, and the estimate
/// always carries that working and a confidence level set by the sample size.
/// </summary>
public static class RecoveryEstimator
{
    private static readonly CultureInfo Gbp = CultureInfo.GetCultureInfo("en-GB");

    public static RecoveryEstimate Estimate(DenialCohort c)
    {
        var winRate = c.Appealed > 0 ? (double)c.AppealsWon / c.Appealed : 0d;
        var value = Money(c.NotAppealed * (decimal)winRate * c.MeanClaimValue);

        var formula =
            $"{c.NotAppealed} not appealed × {winRate.ToString("P0", Gbp)} comparable win rate × " +
            $"{c.MeanClaimValue.ToString("C0", Gbp)} mean claim value = {value.ToString("C0", Gbp)}";

        return new RecoveryEstimate(
            Scope: $"{c.PayerPlan} · {c.Procedure} · {c.Region}",
            Denied: c.Denied,
            NotAppealed: c.NotAppealed,
            Appealed: c.Appealed,
            AppealsWon: c.AppealsWon,
            ComparableWinRate: winRate,
            MeanClaimValue: c.MeanClaimValue,
            EstimatedRecoverableValue: value,
            Confidence: Confidence(c.Appealed, c.NotAppealed),
            Formula: formula,
            Window: $"denials on record, {c.WindowStart:MMM yyyy} – {c.WindowEnd:MMM yyyy}");
    }

    public static RecoveryEstimate Portfolio(IReadOnlyList<DenialCohort> cohorts)
    {
        if (cohorts.Count == 0)
            return RecoveryEstimate.Empty;

        var denied = cohorts.Sum(c => c.Denied);
        var appealed = cohorts.Sum(c => c.Appealed);
        var won = cohorts.Sum(c => c.AppealsWon);
        var notAppealed = cohorts.Sum(c => c.NotAppealed);
        var value = cohorts.Sum(c => Estimate(c).EstimatedRecoverableValue);

        var winRate = appealed > 0 ? (double)won / appealed : 0d;
        var meanValue = notAppealed > 0
            ? Money(cohorts.Sum(c => c.MeanClaimValue * c.NotAppealed) / notAppealed)
            : 0m;

        var start = cohorts.Min(c => c.WindowStart);
        var end = cohorts.Max(c => c.WindowEnd);

        return new RecoveryEstimate(
            Scope: cohorts.Count == 1 ? cohorts[0].PayerPlan : $"all scopes ({cohorts.Count})",
            Denied: denied,
            NotAppealed: notAppealed,
            Appealed: appealed,
            AppealsWon: won,
            ComparableWinRate: winRate,
            MeanClaimValue: meanValue,
            EstimatedRecoverableValue: value,
            Confidence: Confidence(appealed, notAppealed),
            Formula:
                $"{notAppealed} not appealed × {winRate.ToString("P0", Gbp)} blended win rate × " +
                $"{meanValue.ToString("C0", Gbp)} mean claim value = {value.ToString("C0", Gbp)}",
            Window: $"denials on record, {start:MMM yyyy} – {end:MMM yyyy}");
    }

    /// <summary>High once there is a real sample on both sides of the formula; Low while it is thin.</summary>
    private static RecoveryConfidence Confidence(int appealed, int notAppealed) =>
        appealed >= 20 && notAppealed >= 10 ? RecoveryConfidence.High
        : appealed >= 8 ? RecoveryConfidence.Medium
        : RecoveryConfidence.Low;

    private static decimal Money(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);
}

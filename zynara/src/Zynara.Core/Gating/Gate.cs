using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Core.Gating;

/// <summary>Gate thresholds (Architecture §8 / TDD §3). Bound at startup; overridable per profile later.</summary>
public sealed record GateOptions
{
    /// <summary>Estimated claim value at or below which an auto-submit is allowed. Default £500.</summary>
    public decimal AutoLimit { get; init; } = 500m;
}

/// <summary>
/// The deterministic seam between reasoning and action. Instead of one averaged
/// readiness number, the Gate weighs a structured decision model and picks one of
/// four routes (evaluator finding #3):
///
///   • any <b>mandatory</b> criterion not Documented   → HumanReview  (never averaged away)
///   • a contradiction in the clinical evidence        → HumanReview  (never silently resolved)
///   • Low evidence quality + Weak/None precedent      → Abstain      (the system declines to advise)
///   • estimated value over the auto-limit             → HumanReview
///   • all mandatory met, no contradiction, but some
///     supporting criterion undocumented or evidence
///     only Medium                                     → Strengthen   (fixable — back to the clinician)
///   • otherwise                                       → AutoSubmit
///
/// The verdict always carries the model it weighed.
/// </summary>
public sealed class Gate(GateOptions options)
{
    public Gate() : this(new GateOptions()) { }

    public GateDecision Evaluate(
        EvidenceGapResult gap, AppealMatchResult? appeal, decimal? estimatedValue,
        CriticReview? critic = null)
    {
        var support = appeal?.Support ?? PrecedentSupport.None;

        var model = new DecisionModel(
            MandatoryPass: gap.MandatoryPass,
            MandatoryTotal: gap.MandatoryTotal,
            SupportingDocumented: gap.SupportingDocumented,
            SupportingPartial: gap.SupportingPartial,
            SupportingMissing: gap.SupportingMissing,
            EvidenceQuality: gap.Quality,
            ContradictionDetected: gap.ContradictionDetected,
            PrecedentSupport: support,
            UnmetMandatory: gap.UnmetMandatory);

        var facts =
            $"mandatory {(gap.MandatoryPass ? "all met" : $"{gap.UnmetMandatory.Count}/{gap.MandatoryTotal} unmet")}; " +
            $"supporting {gap.SupportingDocumented} documented / {gap.SupportingPartial} partial / {gap.SupportingMissing} missing; " +
            $"evidence {gap.Quality}; contradiction {(gap.ContradictionDetected ? "detected" : "none")}; " +
            $"precedent support {support}";

        // The Critic can force a human or an abstention regardless of the numbers.
        if (critic?.Verdict == CriticVerdict.Abstain)
            return Route(GateRoute.Abstain, model,
                $"the Critic cannot support the recommendation ({CriticNote(critic)}) — the system abstains", facts);
        if (critic?.Verdict == CriticVerdict.Block)
            return Route(GateRoute.HumanReview, model,
                $"the Critic raised a material concern ({CriticNote(critic)}) — a reviewer must decide", facts);

        if (!gap.MandatoryPass)
            return Route(GateRoute.HumanReview, model,
                $"mandatory criterion unmet ({string.Join(", ", gap.UnmetMandatory)}) — a reviewer must decide", facts);

        if (gap.ContradictionDetected)
            return Route(GateRoute.HumanReview, model,
                "contradictory clinical evidence — routed to a reviewer, not auto-resolved", facts);

        if (gap.Quality == EvidenceQuality.Low && support is PrecedentSupport.Weak or PrecedentSupport.None)
            return Route(GateRoute.Abstain, model,
                "insufficient reliable evidence to make a recommendation — the system abstains", facts);

        if (estimatedValue is { } v && v > options.AutoLimit)
            return Route(GateRoute.HumanReview, model,
                $"estimated value {v:C0} over the auto-limit {options.AutoLimit:C0}", facts);

        // The Critic's minor concerns stop an auto-submit but do not by themselves force a human.
        var criticConcerns = critic?.Verdict == CriticVerdict.Concerns;

        if (gap.SupportingMissing > 0 || gap.Quality == EvidenceQuality.Medium || criticConcerns)
            return Route(GateRoute.Strengthen, model,
                criticConcerns
                    ? $"the Critic flagged concerns ({CriticNote(critic!)}) — strengthen before submitting"
                    : $"{gap.SupportingMissing} supporting criterion(a) undocumented / evidence {gap.Quality} — strengthen with the clinician",
                facts);

        return Route(GateRoute.AutoSubmit, model, "gap-checked, Critic-cleared and ready — auto-submit the draft", facts);
    }

    private static GateDecision Route(GateRoute route, DecisionModel model, string reason, string facts) =>
        new(route, model, $"{reason}. [{facts}]");

    private static string CriticNote(CriticReview critic) =>
        critic.Flags.Count > 0 ? critic.Flags[0].Concern : critic.Summary;
}

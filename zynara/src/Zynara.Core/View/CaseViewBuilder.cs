using Zynara.Core.Agents;
using Zynara.Core.Authority;
using Zynara.Core.Model;

namespace Zynara.Core.View;

/// <summary>
/// Projects a <see cref="PipelineResult"/> (plus the request and the written
/// criteria it ran against) into the <see cref="CaseView"/> the reviewer sees.
/// Pure — no I/O, fully unit-tested — so the same projection serves the live API
/// and the pre-computed demo playback.
/// </summary>
public static class CaseViewBuilder
{
    public static CaseView Build(
        PipelineResult result, Request request, Criteria? criteria,
        IReadOnlyList<AuditEntry>? audit = null)
    {
        var policyRef = result.NeedsAuth.PolicyRef ?? criteria?.PolicyRef ?? "unknown";
        var policyVersion = criteria?.Version ?? "unknown";

        var status = ToStatus(result);
        var criteriaView = BuildCriteria(result, criteria, policyRef, policyVersion);
        var precedents = BuildPrecedents(result);
        var critic = result.Critic is { } c
            ? new CriticView(c.Verdict, c.Flags.Select(f => new CriticFlagView(f.Check, f.Concern)).ToList(), c.Summary)
            : null;
        var conflicts = BuildConflicts(criteriaView, result.Critic, result.Contradiction);
        var gate = result.Gate is { } g ? BuildGate(g) : null;

        return new CaseView(
            RequestId: result.RequestId,
            Procedure: request.Procedure,
            PayerPlan: request.PayerPlan,
            Region: request.Region.ToString(),
            Status: status,
            Headline: Headline(status, result, conflicts),
            AiAction: AiAction(status, result),
            Auth: new NeedsAuthView(
                result.NeedsAuth.AuthRequired, result.NeedsAuth.RequiredCode,
                result.NeedsAuth.PolicyRef, result.NeedsAuth.Ambiguous, result.NeedsAuth.Note),
            Criteria: criteriaView,
            Precedents: precedents,
            Critic: critic,
            Conflicts: conflicts,
            Confidence: Confidence(status, result),
            Gate: gate,
            DraftBody: result.Draft?.Body,
            AppealDraft: result.Appeal?.Recommendation.AppealDraft,
            Controls: Controls(status, result),
            ApproveAuthority: ApproveAuthority(result, request).ToString(),
            Audit: (audit ?? Array.Empty<AuditEntry>())
                .Select(a => new AuditView(a.Kind, a.Actor, a.At.ToString("u"), a.Detail)).ToList());
    }

    private static ReviewerRole ApproveAuthority(PipelineResult result, Request request)
    {
        var route = result.Gate?.Route ?? GateRoute.HumanReview;
        var need = Authority.ApprovalAuthority.RequiredToApprove(route, request.EstimatedValue);
        if (!string.IsNullOrWhiteSpace(request.DenialLetter) && need < ReviewerRole.SeniorReviewer)
            need = ReviewerRole.SeniorReviewer;
        return need;
    }

    private static CaseStatus ToStatus(PipelineResult r)
    {
        if (r.StoppedEarly) return CaseStatus.NotRequired;
        return r.Gate?.Route switch
        {
            GateRoute.ReadyToSubmit => CaseStatus.ReadyToSubmit,
            GateRoute.Strengthen => CaseStatus.NeedsStrengthening,
            GateRoute.Abstain => CaseStatus.SystemAbstained,
            _ => CaseStatus.NeedsHumanReview,
        };
    }

    private static IReadOnlyList<CriterionEvidenceView> BuildCriteria(
        PipelineResult result, Criteria? criteria, string policyRef, string policyVersion)
    {
        if (criteria is null) return Array.Empty<CriterionEvidenceView>();

        var findings = result.Gap?.Assessment.Findings.ToDictionary(f => f.CriterionId)
                       ?? new Dictionary<string, CriterionFinding>();

        return criteria.Items.Select(item =>
        {
            findings.TryGetValue(item.Id, out var f);
            var statement = f?.Evidence;
            var source = statement is { Length: > 0 } ? "clinical note" : "not found in the clinical note";
            return new CriterionEvidenceView(
                Id: item.Id,
                Text: item.Text,
                Mandatory: item.Mandatory,
                Status: f?.Status ?? CriterionStatus.Missing,
                EvidenceStatement: statement,
                Source: source,
                PolicyRef: policyRef,
                PolicyVersion: policyVersion);
        }).ToList();
    }

    private static IReadOnlyList<PrecedentView> BuildPrecedents(PipelineResult result)
    {
        if (result.Appeal is not { } appeal) return Array.Empty<PrecedentView>();

        var cited = appeal.Recommendation.CitedPrecedentIds.ToHashSet();

        return appeal.Shortlist
            .Take(3)
            .Select(m => new PrecedentView(
                CaseId: m.Precedent.CaseId,
                Similarity: m.Similarity,
                MatchedFacts: m.MatchedFacts,
                InitiallyDenied: !m.Precedent.InitiallyApproved,
                WonOnAppeal: m.Precedent.AppealOutcome == AppealOutcome.AppealWon,
                ClausesCited: m.Precedent.ClausesCited,
                DecidedOn: m.Precedent.DecidedOn,
                Narrative: m.Precedent.FactPattern,
                DroveRecommendation: cited.Contains(m.Precedent.CaseId)))
            .ToList();
    }

    private static IReadOnlyList<string> BuildConflicts(
        IReadOnlyList<CriterionEvidenceView> criteria, CriticReview? critic, ContradictionResult contradiction)
    {
        var conflicts = criteria
            .Where(c => c.Status == CriterionStatus.Contradicted)
            .Select(c => $"The clinical note contradicts \"{c.Text}\"" +
                         (c.EvidenceStatement is { Length: > 0 } s ? $": {s}" : "."))
            .ToList();

        // Intra-evidence contradictions (P2-3) — surfaced, never silently resolved.
        conflicts.AddRange(contradiction.Conflicts.Select(x =>
            $"The clinical note contradicts itself on {x.Subject}: “{x.Affirmed.Text}” vs “{x.Denied.Text}”"));

        if (critic?.Verdict == CriticVerdict.Block)
            conflicts.AddRange(critic.Flags
                .Where(f => f.Check != "contradiction" || contradiction.Conflicts.Count == 0)
                .Select(f => $"Critic — {f.Concern}"));

        return conflicts.Distinct().ToList();
    }

    private static GateView BuildGate(GateDecision g) => new(
        Route: g.Route,
        Reason: g.Reason,
        MandatoryPass: g.Model.MandatoryPass,
        UnmetMandatory: g.Model.UnmetMandatory,
        SupportingDocumented: g.Model.SupportingDocumented,
        SupportingPartial: g.Model.SupportingPartial,
        SupportingMissing: g.Model.SupportingMissing,
        EvidenceQuality: g.Model.EvidenceQuality,
        ContradictionDetected: g.Model.ContradictionDetected,
        PrecedentSupport: g.Model.PrecedentSupport);

    private static string Headline(CaseStatus status, PipelineResult r, IReadOnlyList<string> conflicts) => status switch
    {
        CaseStatus.NotRequired =>
            "No prior authorisation is required for this procedure.",
        CaseStatus.ReadyToSubmit =>
            "The clinical record meets the plan's criteria and the case is ready to submit.",
        CaseStatus.NeedsStrengthening =>
            $"The mandatory criteria are met, but {r.Gap?.SupportingMissing ?? 0} supporting criterion(a) " +
            "need more evidence before this is ready.",
        CaseStatus.NeedsHumanReview when conflicts.Count > 0 =>
            $"A reviewer must decide: {conflicts[0]}",
        CaseStatus.NeedsHumanReview when r.Critic?.Verdict == CriticVerdict.Concerns
            && r.Critic.Flags.Count > 0 =>
            $"The Critic challenged the recommendation — {r.Critic.Flags[0].Concern}. A reviewer decides.",
        CaseStatus.NeedsHumanReview when r.Appeal?.Recommendation.AppealDraft is { Length: > 0 } =>
            $"This is an appeal — a reviewer signs off before it is filed. " +
            $"{r.Appeal!.Shortlist.Count(m => m.Precedent.AppealOutcome == AppealOutcome.AppealWon)} " +
            "comparable case(s) won on appeal.",
        CaseStatus.NeedsHumanReview =>
            "A reviewer must decide — see the Gate reason below.",
        CaseStatus.SystemAbstained =>
            "The system does not have enough reliable evidence to make a recommendation.",
        _ => "Case assembled.",
    };

    private static string AiAction(CaseStatus status, PipelineResult r) => status switch
    {
        CaseStatus.NotRequired => "Stopped after the needs-auth check — nothing to submit.",
        CaseStatus.ReadyToSubmit => "Assembled the submission draft and recommends sending it on approval.",
        CaseStatus.NeedsStrengthening => "Assembled a draft and listed the evidence gaps for the clinician.",
        CaseStatus.NeedsHumanReview => "Assembled the case and routed it to a human — it did not act.",
        CaseStatus.SystemAbstained => "Declined to advise and routed the case to a human.",
        _ => "Assembled the case.",
    };

    private static ConfidenceLevel Confidence(CaseStatus status, PipelineResult r)
    {
        if (status == CaseStatus.NotRequired) return ConfidenceLevel.NotApplicable;
        if (status == CaseStatus.SystemAbstained) return ConfidenceLevel.Low;

        var q = r.Gap?.Quality ?? EvidenceQuality.Low;
        var contradiction = r.Gap?.ContradictionDetected ?? false;
        var criticClear = r.Critic is null || r.Critic.Verdict == CriticVerdict.Clear;

        if (status == CaseStatus.ReadyToSubmit && q == EvidenceQuality.High && !contradiction && criticClear)
            return ConfidenceLevel.High;
        if (q == EvidenceQuality.Low || contradiction)
            return ConfidenceLevel.Low;
        return ConfidenceLevel.Medium;
    }

    private static IReadOnlyList<HumanControl> Controls(CaseStatus status, PipelineResult r)
    {
        var edit = new HumanControl("edit", "Edit the draft", false);
        var reject = new HumanControl("reject", "Reject", false);
        var requestEvidence = new HumanControl("request-evidence", "Request more evidence", false);
        var approve = new HumanControl("approve-send", "Approve & send", false);

        // The Critic's challenge (or Low-quality evidence) steers the reviewer toward
        // asking for more evidence rather than signing off.
        var criticUneasy = r.Critic?.Verdict is CriticVerdict.Concerns or CriticVerdict.Block;
        var thinEvidence = (r.Gap?.Quality ?? EvidenceQuality.Low) == EvidenceQuality.Low;

        return status switch
        {
            CaseStatus.NotRequired => new[] { new HumanControl("acknowledge", "Acknowledge", true) },

            CaseStatus.ReadyToSubmit => new[]
            {
                approve with { Primary = true }, edit, requestEvidence, reject,
            },

            CaseStatus.NeedsStrengthening => new[]
            {
                requestEvidence with { Primary = true }, edit,
                approve with { Label = "Approve & send anyway" }, reject,
            },

            CaseStatus.NeedsHumanReview when criticUneasy || thinEvidence => new[]
            {
                requestEvidence with { Primary = true }, edit, approve, reject with { Primary = true },
            },

            CaseStatus.NeedsHumanReview => new[]
            {
                approve with { Primary = true }, reject with { Primary = true }, edit, requestEvidence,
            },

            CaseStatus.SystemAbstained => new[]
            {
                requestEvidence with { Primary = true }, reject with { Primary = true },
                edit with { Label = "Edit and decide manually" },
            },

            _ => Array.Empty<HumanControl>(),
        };
    }
}

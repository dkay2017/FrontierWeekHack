using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents;
using Zynara.Core;
using Zynara.Core.Abstractions;
using Zynara.Core.Agents;
using Zynara.Core.Model;
using Zynara.Core.Pipeline;

namespace Zynara.Eval;

/// <summary>
/// Runs the labelled case set through the pipeline (deterministic stub agents, so
/// CI is repeatable) and scores the metric suite in <see cref="EvalReport"/>.
/// </summary>
public static class EvalRunner
{
    public static async Task<EvalReport> RunAsync(
        IReadOnlyList<EvalCase>? cases = null, CancellationToken ct = default)
    {
        cases ??= EvalCase.LoadAll();

        // evidence extraction confusion counts ("Documented" = positive)
        int evTp = 0, evFp = 0, evFn = 0;

        // mandatory
        int mandUnmet = 0, mandFn = 0, mandMet = 0, mandFp = 0;

        // grounding
        int citeCases = 0, citeCorrect = 0, hallucinated = 0;
        int policyCiteCases = 0, policyCiteCorrect = 0;
        int verdictCases = 0, verdictAgree = 0;

        // decisions
        int routeAgree = 0, absExpected = 0, absTaken = 0, unsafeAuto = 0, notAutoExpected = 0;

        // baseline
        int baseAgree = 0, baseUnsafe = 0;

        var caseLines = new List<string>();

        foreach (var c in cases)
        {
            ct.ThrowIfCancellationRequested();

            var store = c.ToStore();
            var pipeline = BuildPipeline(store);
            var result = await pipeline.RunAsync(c.ToRequest(), ct);

            var criteria = c.ToCriteria();
            var findings = result.Gap?.Assessment.Findings.ToDictionary(f => f.CriterionId, f => f.Status)
                           ?? new Dictionary<string, CriterionStatus>();

            foreach (var cr in c.Criteria)
            {
                var predicted = findings.GetValueOrDefault(cr.Id, CriterionStatus.Missing);
                var truth = c.GroundTruth.StatusOf(cr.Id);

                var predDoc = predicted == CriterionStatus.Documented;
                var truthDoc = truth == CriterionStatus.Documented;

                if (predDoc && truthDoc) evTp++;
                else if (predDoc && !truthDoc) evFp++;
                else if (!predDoc && truthDoc) evFn++;

                if (cr.Mandatory)
                {
                    if (!truthDoc) { mandUnmet++; if (predDoc) mandFn++; }   // UNSAFE: said "met" when it wasn't
                    else { mandMet++; if (!predDoc) mandFp++; }
                }
            }

            // precedent citation accuracy
            var expectedCites = c.GroundTruth.ExpectedCitedPrecedents.ToHashSet();
            if (expectedCites.Count > 0 || (result.Appeal?.Recommendation.CitedPrecedentIds.Count ?? 0) > 0)
            {
                citeCases++;
                var actual = (result.Appeal?.Recommendation.CitedPrecedentIds ?? Array.Empty<string>()).ToHashSet();
                if (actual.SetEquals(expectedCites)) citeCorrect++;

                var known = c.Precedents.Select(p => p.CaseId).ToHashSet();
                hallucinated += actual.Count(id => !known.Contains(id));
            }
            hallucinated += findings.Keys.Count(id => criteria.Items.All(x => x.Id != id));

            // policy-citation: the assembled draft must name the governing policy ref
            var expectedPolicy = c.GroundTruth.ExpectedPolicyRef ?? c.Rules.FirstOrDefault()?.PolicyRef;
            if (!string.IsNullOrWhiteSpace(expectedPolicy) && result.Draft is { } d)
            {
                policyCiteCases++;
                var text = d.Body + " " + (result.Appeal?.Recommendation.AppealDraft ?? "");
                if (result.NeedsAuth.PolicyRef == expectedPolicy && text.Contains(expectedPolicy))
                    policyCiteCorrect++;
            }

            // clause hallucination: a "under/clause/citing x.y" clause in the appeal draft that no
            // shortlisted precedent cited (similarity scores etc. are not matched)
            if (result.Appeal?.Recommendation.AppealDraft is { } appealDraft)
            {
                var knownClauses = c.Precedents.SelectMany(p => p.ClausesCited).ToHashSet();
                hallucinated += System.Text.RegularExpressions.Regex
                    .Matches(appealDraft, @"(?:under|clause|citing)\s+(\d+(?:\.\d+)+)",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                    .Select(m => m.Groups[1].Value)
                    .Count(x => !knownClauses.Contains(x));
            }

            // appeal-recommendation agreement (review note #3)
            if (c.GroundTruth.AppealVerdict is { } wantVerdict)
            {
                verdictCases++;
                if (result.Appeal?.Recommendation.Verdict == wantVerdict) verdictAgree++;
            }

            // route
            var actualRoute = result.Gate?.Route ?? GateRoute.HumanReview; // early-stop → treat as review
            if (actualRoute == c.GroundTruth.Route) routeAgree++;

            if (c.GroundTruth.ExpertAbstains)
            {
                absExpected++;
                if (actualRoute == GateRoute.Abstain) absTaken++;
            }

            if (c.GroundTruth.Route != GateRoute.AutoSubmit)
            {
                notAutoExpected++;
                if (actualRoute == GateRoute.AutoSubmit) unsafeAuto++;
            }

            // baseline
            var baseRoute = GeneralistBaseline.Route(c);
            if (baseRoute == c.GroundTruth.Route) baseAgree++;
            if (c.GroundTruth.Route != GateRoute.AutoSubmit && baseRoute == GateRoute.AutoSubmit) baseUnsafe++;

            var mark = actualRoute == c.GroundTruth.Route ? "ok  " : "MISS";
            caseLines.Add($"{mark} {c.Id,-26} expected {c.GroundTruth.Route,-11} got {actualRoute,-11} (baseline {baseRoute})");
            if (mark == "MISS")
                caseLines.Add($"       critic={result.Critic?.Verdict} q={result.Gap?.Quality} contra={result.Gap?.ContradictionDetected} :: {result.Gate?.Reason}");
        }

        return new EvalReport(
            Cases: cases.Count,
            EvidencePrecision: Ratio(evTp, evTp + evFp),
            EvidenceRecall: Ratio(evTp, evTp + evFn),
            MandatoryUnmetTotal: mandUnmet,
            MandatoryFalseNegatives: mandFn,
            MandatoryFalseNegativeRate: Ratio(mandFn, mandUnmet),
            MandatoryMetTotal: mandMet,
            MandatoryFalsePositives: mandFp,
            PrecedentCitationAccuracy: Ratio(citeCorrect, citeCases),
            PolicyCitationAccuracy: Ratio(policyCiteCorrect, policyCiteCases),
            AppealVerdictAgreement: Ratio(verdictAgree, verdictCases),
            AppealVerdictCases: verdictCases,
            HallucinatedReferences: hallucinated,
            RouteAgreement: Ratio(routeAgree, cases.Count),
            AbstainExpected: absExpected,
            AbstainTaken: absTaken,
            SafeAbstentionRate: Ratio(absTaken, absExpected),
            UnsafeAutomations: unsafeAuto,
            UnsafeAutomationRate: Ratio(unsafeAuto, notAutoExpected),
            BaselineRouteAgreement: Ratio(baseAgree, cases.Count),
            BaselineUnsafeAutomations: baseUnsafe,
            CaseLines: caseLines);
    }

    private static AuthPipeline BuildPipeline(IZynaraStore store) =>
        new ServiceCollection()
            .AddSingleton(store)
            .AddZynaraAgents()
            .AddZynaraCore()
            .BuildServiceProvider()
            .GetRequiredService<AuthPipeline>();

    private static double Ratio(int n, int d) => d == 0 ? 1.0 : (double)n / d;
}

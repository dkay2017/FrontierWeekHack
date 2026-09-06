using Zynara.Core.Model;

namespace Zynara.Core.Authority;

/// <summary>
/// Reviewer roles, ranked. Real deployments map these to Entra ID app roles;
/// the demo passes the role in a header.
/// </summary>
public enum ReviewerRole
{
    Coordinator = 0,
    Reviewer = 1,
    SeniorReviewer = 2,
    MedicalDirector = 3,
}

/// <summary>The outcome of an authority check — always carries the reason and the role that would suffice.</summary>
public sealed record AuthorityCheck(bool Allowed, string Reason, ReviewerRole Required);

/// <summary>
/// Approval authority tiered by <b>route</b> and by <b>financial risk</b>
/// (evaluator §13 / review note #4). The required authority is the higher of the
/// two; the Gate's auto-submit limit is simply the bottom tier — above it, a case
/// escalates to a more senior reviewer rather than to a bigger number.
/// </summary>
public static class ApprovalAuthority
{
    public static decimal AutoSubmitLimit => 500m;

    private static ReviewerRole ByRoute(GateRoute route) => route switch
    {
        GateRoute.AutoSubmit => ReviewerRole.Coordinator,
        GateRoute.Strengthen => ReviewerRole.Coordinator,
        GateRoute.HumanReview => ReviewerRole.Reviewer,
        GateRoute.Abstain => ReviewerRole.SeniorReviewer,
        _ => ReviewerRole.Reviewer,
    };

    private static ReviewerRole ByValue(decimal? value) => value switch
    {
        null => ReviewerRole.Coordinator,
        <= 500m => ReviewerRole.Coordinator,
        <= 5_000m => ReviewerRole.Reviewer,
        <= 25_000m => ReviewerRole.SeniorReviewer,
        _ => ReviewerRole.MedicalDirector,
    };

    /// <summary>The role needed to <b>approve &amp; send</b> a case on this route at this value.</summary>
    public static ReviewerRole RequiredToApprove(GateRoute route, decimal? estimatedValue) =>
        (ReviewerRole)Math.Max((int)ByRoute(route), (int)ByValue(estimatedValue));

    public static bool IsAppeal(bool hasDenialLetter) => hasDenialLetter;

    /// <summary>Can this role take this action on this case?</summary>
    public static AuthorityCheck Check(
        ReviewerRole role, string action, GateRoute route, decimal? estimatedValue, bool isAppeal)
    {
        switch (action)
        {
            case "approve-send":
            {
                var need = RequiredToApprove(route, estimatedValue);
                if (isAppeal && need < ReviewerRole.SeniorReviewer)
                    need = ReviewerRole.SeniorReviewer;   // an appeal is always at least a Senior sign-off

                return role >= need
                    ? new AuthorityCheck(true, $"{role} may approve this case.", need)
                    : new AuthorityCheck(false,
                        $"Approving this case ({route}" +
                        (estimatedValue is { } v ? $", {v:C0}" : "") +
                        (isAppeal ? ", appeal" : "") +
                        $") requires {need}.", need);
            }

            case "reject":
                return role >= ReviewerRole.Reviewer
                    ? new AuthorityCheck(true, $"{role} may reject.", ReviewerRole.Reviewer)
                    : new AuthorityCheck(false, "Rejecting a case requires Reviewer.", ReviewerRole.Reviewer);

            // request-evidence, edit, acknowledge — open to any reviewer
            default:
                return new AuthorityCheck(true, $"{role} may {action.Replace('-', ' ')}.", ReviewerRole.Coordinator);
        }
    }
}

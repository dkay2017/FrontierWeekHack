using Zynara.Core.Authority;
using Zynara.Core.Model;

namespace Zynara.Core.Tests;

/// <summary>Approval authority tiered by route and by financial risk (P1-3 / evaluator §13).</summary>
public class AuthorityTests
{
    [Theory]
    [InlineData(GateRoute.AutoSubmit, 400, ReviewerRole.Coordinator)]
    [InlineData(GateRoute.HumanReview, 400, ReviewerRole.Reviewer)]
    [InlineData(GateRoute.HumanReview, 9000, ReviewerRole.SeniorReviewer)]
    [InlineData(GateRoute.Abstain, 400, ReviewerRole.SeniorReviewer)]
    [InlineData(GateRoute.HumanReview, 40000, ReviewerRole.MedicalDirector)]
    public void Required_role_is_the_higher_of_route_and_value(GateRoute route, decimal value, ReviewerRole expected) =>
        Assert.Equal(expected, ApprovalAuthority.RequiredToApprove(route, value));

    [Fact]
    public void An_appeal_is_at_least_a_senior_sign_off()
    {
        var low = ApprovalAuthority.Check(ReviewerRole.Reviewer, "approve-send", GateRoute.HumanReview, 300m, isAppeal: true);
        Assert.False(low.Allowed);
        Assert.Equal(ReviewerRole.SeniorReviewer, low.Required);

        var ok = ApprovalAuthority.Check(ReviewerRole.SeniorReviewer, "approve-send", GateRoute.HumanReview, 300m, isAppeal: true);
        Assert.True(ok.Allowed);
    }

    [Fact]
    public void A_coordinator_can_request_evidence_but_not_reject()
    {
        Assert.True(ApprovalAuthority.Check(ReviewerRole.Coordinator, "request-evidence", GateRoute.Strengthen, 200m, false).Allowed);
        Assert.False(ApprovalAuthority.Check(ReviewerRole.Coordinator, "reject", GateRoute.HumanReview, 200m, false).Allowed);
    }
}

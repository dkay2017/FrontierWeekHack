using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Core.Tests;

public class DomainTests
{
    private static Criteria Criteria() => new(
        "Bupa/Comprehensive", "MRI lumbar spine", "CCSD-MRI-LS-v3", "v3",
        new Criterion[] { new("c1", "physiotherapy"), new("c2", "neurological deficit") });

    [Fact]
    public void EvidenceGapAssessment_Validate_rejects_an_unknown_criterion_id()
    {
        var a = new EvidenceGapAssessment(
            new[] { new CriterionFinding("c9", CriterionStatus.Missing, null) }, EvidenceQuality.Low, "x");
        Assert.Throws<ArgumentException>(() => a.Validate(Criteria()));
    }

    [Fact]
    public void EvidenceGapAssessment_Validate_rejects_a_criterion_classified_twice()
    {
        var a = new EvidenceGapAssessment(
            new[]
            {
                new CriterionFinding("c1", CriterionStatus.Documented, null),
                new CriterionFinding("c1", CriterionStatus.Missing, null),
            }, EvidenceQuality.Low, "x");
        Assert.Throws<ArgumentException>(() => a.Validate(Criteria()));
    }

    [Fact]
    public void EvidenceGapAssessment_Validate_requires_every_criterion_to_be_classified()
    {
        var a = new EvidenceGapAssessment(
            new[] { new CriterionFinding("c1", CriterionStatus.Documented, null) }, EvidenceQuality.Low, "x");
        Assert.Throws<ArgumentException>(() => a.Validate(Criteria())); // c2 unclassified
    }

    [Fact]
    public void NullAgentCallRecorder_records_nothing()
    {
        var task = new NullAgentCallRecorder().RecordAsync(
            new AgentCallUsage("needs-auth", "gpt-5.4", 100, 20, 0, "r1", "trace-1"));
        Assert.True(task.IsCompletedSuccessfully);
    }
}

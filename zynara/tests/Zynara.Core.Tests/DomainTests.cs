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
        var a = new EvidenceGapAssessment(new[] { "c1", "c9" }, Array.Empty<string>(), Array.Empty<string>(), "x");
        Assert.Throws<ArgumentException>(() => a.Validate(Criteria()));
    }

    [Fact]
    public void EvidenceGapAssessment_Validate_rejects_a_criterion_classified_twice()
    {
        var a = new EvidenceGapAssessment(new[] { "c1" }, new[] { "c1" }, Array.Empty<string>(), "x");
        Assert.Throws<ArgumentException>(() => a.Validate(Criteria()));
    }

    [Fact]
    public void NullAgentCallRecorder_records_nothing()
    {
        var task = new NullAgentCallRecorder().RecordAsync(
            new AgentCallUsage("needs-auth", "gpt-5.4", 100, 20, 0, "r1", "trace-1"));
        Assert.True(task.IsCompletedSuccessfully);
    }
}

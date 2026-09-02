using TireForge.Agents.Diagnosis;
using TireForge.Core.Model;

namespace TireForge.Agents.Tests;

/// <summary>Build Plan Stage F step 1 — "schema validates".</summary>
public class FaultVerdictTests
{
    private static FaultVerdict Make(double confidence, string fault = "bearing wear") => new()
    {
        Fault = fault, Severity = Severity.Warn, Confidence = confidence,
        Text = "…", Cites = new[] { "rdg-1" },
    };

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    [InlineData(double.NaN)]
    public void Confidence_outside_zero_to_one_is_rejected(double confidence) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Make(confidence).Validate());

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.7)]
    [InlineData(1.0)]
    public void Confidence_within_range_is_accepted(double confidence) =>
        Assert.Equal(confidence, Make(confidence).Validate().Confidence);

    [Fact]
    public void Empty_fault_is_rejected() =>
        Assert.Throws<ArgumentException>(() => Make(0.8, fault: "  ").Validate());

    [Fact]
    public void Missing_citations_are_rejected()
    {
        var v = new FaultVerdict
        {
            Fault = "bearing wear", Severity = Severity.Warn, Confidence = 0.8,
            Text = "…", Cites = Array.Empty<string>(),
        };
        Assert.Throws<ArgumentException>(() => v.Validate());
    }
}

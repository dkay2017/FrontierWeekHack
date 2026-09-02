using TireForge.Core.Gating;
using TireForge.Core.Model;

namespace TireForge.Core.Tests;

/// <summary>Build Plan Stage G — the Gate truth table (invariant 1.3).</summary>
public class GateTests
{
    [Theory]
    // severity, confidence, expected route
    [InlineData(Severity.Info, 0.95, GateRoute.Auto)]
    [InlineData(Severity.Warn, 0.70, GateRoute.Auto)]   // exactly 0.70 → auto
    [InlineData(Severity.Warn, 0.6999, GateRoute.Review)]
    [InlineData(Severity.Warn, 0.50, GateRoute.Review)]
    [InlineData(Severity.Crit, 0.99, GateRoute.Review)] // critical always → review
    [InlineData(Severity.Crit, 0.40, GateRoute.Review)]
    public void Route_follows_the_rule(Severity severity, double confidence, GateRoute expected) =>
        Assert.Equal(expected, Gate.Evaluate(severity, confidence).Route);

    [Fact]
    public void Auto_reason_states_the_confidence_and_severity()
    {
        var d = Gate.Evaluate(Severity.Warn, 0.82);
        Assert.Equal(GateRoute.Auto, d.Route);
        Assert.Contains("0.82", d.Reason);
        Assert.Contains("auto-issue", d.Reason);
    }

    [Fact]
    public void Review_reason_lists_every_trigger()
    {
        var d = Gate.Evaluate(Severity.Crit, 0.55);
        Assert.Contains("severity Crit", d.Reason);
        Assert.Contains("0.55 < 0.70", d.Reason);
    }

    [Fact]
    public void Review_reason_for_low_confidence_only()
    {
        var d = Gate.Evaluate(Severity.Warn, 0.61);
        Assert.DoesNotContain("severity", d.Reason);
        Assert.Contains("0.61 < 0.70", d.Reason);
    }

    [Fact]
    public void Apply_records_route_and_reason_on_the_diagnosis()
    {
        var dx = new Diagnosis
        {
            Id = "dx-1", ReadingId = "rdg-1", MachineId = "CP-003",
            Fault = "platen bearing failure", Severity = Severity.Crit, Confidence = 0.55,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var decision = Gate.Apply(dx);

        Assert.Equal(GateRoute.Review, dx.Route);
        Assert.Equal(decision.Reason, dx.GateReason);
        Assert.NotEqual("", dx.GateReason);
    }
}

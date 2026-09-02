using TireForge.Agents;
using TireForge.Core.Agents;
using TireForge.Core.Model;

namespace TireForge.Agents.Tests;

/// <summary>Build Plan Stage H checks (A3 stub).</summary>
public class StubWorkOrderDrafterTests
{
    private readonly IWorkOrderDrafter _a3 = new StubWorkOrderDrafter();

    private static Diagnosis Dx(Severity severity) => new()
    {
        Id = "dx-1", ReadingId = "rdg-1", MachineId = "CP-003",
        Fault = "platen bearing failure", Severity = severity, Confidence = 0.55,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Draft_carries_the_diagnosis_fields_and_cites_the_reading()
    {
        var draft = await _a3.DraftAsync(Dx(Severity.Crit), Fx.CuringPress());

        Assert.Equal("CP-003", draft.MachineId);
        Assert.Equal("platen bearing failure", draft.Fault);
        Assert.Equal(Severity.Crit, draft.Severity);
        Assert.Equal("rdg-1", draft.ReadingId);
        Assert.Contains("rdg-1", draft.ActionText);
        Assert.Contains("dx-1", draft.ActionText);
    }

    [Theory]
    [InlineData(Severity.Crit, "IMMEDIATE")]
    [InlineData(Severity.Warn, "WITHIN 24H")]
    [InlineData(Severity.Info, "MONITOR")]
    public async Task Action_urgency_tracks_severity(Severity severity, string marker)
    {
        var draft = await _a3.DraftAsync(Dx(severity), Fx.CuringPress());
        Assert.Contains(marker, draft.ActionText);
    }
}

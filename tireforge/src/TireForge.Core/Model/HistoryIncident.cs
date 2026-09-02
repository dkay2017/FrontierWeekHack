namespace TireForge.Core.Model;

/// <summary>
/// A past fault on a machine, used by HistoryMatch / T2 (Build Plan Stage E) to
/// ground a diagnosis in documented precedent. Seeded (~8 rows).
/// </summary>
public class HistoryIncident
{
    /// <summary>Incident id: <c>inc-NNN</c>. Cited by the Fault Diagnosis agent.</summary>
    public required string Id { get; set; }

    public required string MachineId { get; set; }
    public Machine? Machine { get; set; }

    public DateOnly OccurredOn { get; set; }

    /// <summary>Fault signature, e.g. <c>vib-high+temp-high</c> — matched against the
    /// signature derived from a current reading's threshold breaches.</summary>
    public required string Signature { get; set; }

    /// <summary>The diagnosed fault, e.g. <c>bearing wear</c>.</summary>
    public required string Fault { get; set; }

    public Severity Severity { get; set; }

    /// <summary>What resolved it, e.g. <c>replaced main drive bearing, re-greased</c>.</summary>
    public string Resolution { get; set; } = "";
}

namespace TireForge.Core.Model;

/// <summary>
/// The Fault Diagnosis agent's verdict on an anomalous reading, plus the full
/// upstream trace and the Gate decision (Build Plan Stages F / G).
/// </summary>
public class Diagnosis
{
    /// <summary>Diagnosis id: <c>dx-&lt;ticks&gt;-&lt;rand&gt;</c>.</summary>
    public required string Id { get; set; }

    public required string ReadingId { get; set; }
    public Reading? Reading { get; set; }

    public required string MachineId { get; set; }

    public required string Fault { get; set; }
    public Severity Severity { get; set; }

    /// <summary>Agent confidence, 0–1. Drives the Gate (invariant 1.3).</summary>
    public double Confidence { get; set; }

    public GateRoute Route { get; set; }

    /// <summary>Human-readable Gate reason, e.g. <c>confidence 0.62 &lt; 0.70</c>.</summary>
    public string GateReason { get; set; } = "";

    public DiagnosisStatus Status { get; set; } = DiagnosisStatus.Pending;

    // --- Trace (one line per pipeline step, each citing its source record) ---
    public string DetectText { get; set; } = "";
    public string MatchText { get; set; } = "";
    public string DiagnoseText { get; set; } = "";

    /// <summary>
    /// The Work Order agent's drafted action (Build Plan Stage H). Recorded on
    /// <b>every</b> route (D7) — on the Review route it is the "prepared, not issued"
    /// text the reviewer sees; on the Auto route it also becomes the work order.
    /// </summary>
    public string DraftActionText { get; set; } = "";

    /// <summary>Incident ids the diagnosis cites (from HistoryMatch), comma-separated.</summary>
    public string IncidentCites { get; set; } = "";

    /// <summary>Correlated trace id shared across every hop for this reading.</summary>
    public string TraceId { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public WorkOrder? WorkOrder { get; set; }
}

namespace TireForge.Core.Model;

/// <summary>The four sensors every TireForge machine reports.</summary>
public enum SensorKind
{
    Temperature,
    Pressure,
    Vibration,
    Rpm,
}

/// <summary>Severity of a threshold breach or a diagnosed fault (Build Plan Stage C / F).</summary>
public enum Severity
{
    Info,
    Warn,
    Crit,
}

/// <summary>Reading generator mode (Build Plan Stage B).</summary>
public enum ReadingMode
{
    Normal,
    Warn,
    Crit,
}

/// <summary>Where the Gate sends a diagnosis (Build Plan Stage G, invariant 1.3).</summary>
public enum GateRoute
{
    /// <summary>confidence &gt;= 0.70 AND severity != Crit — straight to the Adapter.</summary>
    Auto,

    /// <summary>confidence &lt; 0.70 OR severity == Crit — human reviewer first.</summary>
    Review,
}

/// <summary>Lifecycle of a <see cref="Diagnosis"/> row.</summary>
public enum DiagnosisStatus
{
    /// <summary>Review route — waiting on a human. No work order yet.</summary>
    Pending,

    /// <summary>Auto route — work order issued without review.</summary>
    AutoIssued,

    /// <summary>Reviewer approved; work order issued by the reviewer.</summary>
    Approved,

    /// <summary>Reviewer rejected; audit row written, no active work order.</summary>
    Rejected,
}

/// <summary>Lifecycle of a <see cref="WorkOrder"/> row (Build Plan Stages I / K).</summary>
public enum WorkOrderStatus
{
    Issued,
    Approved,
    Rejected,
    Closed,
}

/// <summary>Lifecycle of an <see cref="EarlyWarning"/> row (T0 — predictive, not reactive).</summary>
public enum EarlyWarningStatus
{
    /// <summary>Raised, not yet looked at.</summary>
    Open,

    /// <summary>A reviewer has seen it and is tracking it — no action taken yet.</summary>
    Acknowledged,

    /// <summary>A reviewer judged it a false alarm (noise, planned test run, etc.).</summary>
    Dismissed,

    /// <summary>The trend resolved itself, or maintenance already addressed it.</summary>
    Resolved,
}

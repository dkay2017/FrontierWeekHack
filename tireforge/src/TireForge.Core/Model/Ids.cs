namespace TireForge.Core.Model;

/// <summary>
/// Sortable, unique id generation. The timestamp component is zero-padded ticks so
/// ids sort chronologically as plain strings (Build Plan Stage B step 2).
/// </summary>
public static class Ids
{
    private static string Stamp(DateTimeOffset ts) => ts.UtcTicks.ToString("D19");

    private static string Rand() => Random.Shared.Next(0x10000).ToString("x4");

    /// <summary><c>rdg-&lt;ticks&gt;-&lt;rand&gt;</c></summary>
    public static string Reading(DateTimeOffset ts) => $"rdg-{Stamp(ts)}-{Rand()}";

    /// <summary><c>dx-&lt;ticks&gt;-&lt;rand&gt;</c></summary>
    public static string Diagnosis(DateTimeOffset ts) => $"dx-{Stamp(ts)}-{Rand()}";

    /// <summary><c>WO-&lt;ticks&gt;-&lt;rand&gt;</c></summary>
    public static string WorkOrder(DateTimeOffset ts) => $"WO-{Stamp(ts)}-{Rand()}";

    /// <summary>Correlated trace id shared across every hop for one reading.</summary>
    public static string Trace() => Guid.NewGuid().ToString("n");
}

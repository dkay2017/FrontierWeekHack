using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Core.History;

/// <summary>
/// A canonical fault signature derived from a T1 report's breaches, e.g.
/// <c>temperature-high+vibration-high</c> — tokens are <c>&lt;sensor&gt;-&lt;high|low&gt;</c>,
/// lowercased, sorted, joined by <c>+</c> (Build Plan Stage E step 1). Seed
/// <c>History</c> rows use the same form so exact matches line up.
/// </summary>
public static class FaultSignature
{
    public static string From(ThresholdReport t1) => From(t1.Sensors);

    public static string From(IEnumerable<SensorEvaluation> sensors)
    {
        var tokens = sensors
            .Where(s => !s.InSpec)
            .Select(s => $"{s.Sensor.Slug()}-{(s.Status == SensorStatus.High ? "high" : "low")}")
            .OrderBy(x => x, StringComparer.Ordinal);
        return string.Join("+", tokens);
    }

    public static IReadOnlyCollection<string> Tokens(string signature) =>
        string.IsNullOrEmpty(signature)
            ? Array.Empty<string>()
            : signature.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Count of tokens the two signatures share.</summary>
    public static int Overlap(string a, string b)
    {
        var set = Tokens(a).ToHashSet(StringComparer.Ordinal);
        return Tokens(b).Count(set.Contains);
    }
}

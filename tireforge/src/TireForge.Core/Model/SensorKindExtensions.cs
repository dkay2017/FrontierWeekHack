namespace TireForge.Core.Model;

public static class SensorKindExtensions
{
    /// <summary>Lowercase name used in traces and fault signatures.</summary>
    public static string Slug(this SensorKind kind) => kind switch
    {
        SensorKind.Temperature => "temperature",
        SensorKind.Pressure => "pressure",
        SensorKind.Vibration => "vibration",
        SensorKind.Rpm => "rpm",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace TireForge.ApiProxy;

/// <summary>
/// The one JSON shape every endpoint returns: camelCase properties, enums as
/// camelCase strings (<c>Severity.Crit</c> → <c>"crit"</c>), so the dashboard's
/// mock <c>api</c> object maps straight onto the responses.
/// </summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = Build();

    private static JsonSerializerOptions Build()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(o);
        return o;
    }

    public static void Configure(JsonSerializerOptions o)
    {
        o.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }
}

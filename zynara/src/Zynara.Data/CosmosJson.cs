using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Zynara.Data;

/// <summary>
/// Cosmos stores JSON with a mandatory <c>id</c> string. We serialise the domain
/// records straight through (camelCase, string enums — same shape the API and the
/// dashboard use) and just splice <c>id</c> in, so there are no wrapper types to
/// keep in sync.
/// </summary>
internal static class CosmosJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static JsonObject WithId<T>(T value, string id, params (string key, string val)[] extra)
    {
        var node = JsonSerializer.SerializeToNode(value, Options)!.AsObject();
        node["id"] = id;
        foreach (var (k, v) in extra)
            node[k] = v;
        return node;
    }

    public static T? To<T>(JsonObject? node) => node is null ? default : node.Deserialize<T>(Options);
}

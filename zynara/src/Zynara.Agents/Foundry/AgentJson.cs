using System.Text.Json;

namespace Zynara.Agents.Foundry;

/// <summary>Lenient JSON extraction for the two agents that return a structured object.</summary>
internal static class AgentJson
{
    /// <summary>Parse the first <c>{ … }</c> block in the model's reply; null if there isn't one or it's malformed.</summary>
    public static JsonElement? FirstObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string[] StringArray(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Array
            ? el.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()!)
                .ToArray()
            : Array.Empty<string>();

    public static string String(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString() ?? ""
            : "";
}

using System.Text.Json;
using System.Text.Json.Nodes;
using OpenAI.Responses;
using TireForge.Core.Thresholds;

namespace TireForge.Agents.Foundry;

/// <summary>
/// The <c>check_thresholds</c> function tool for the anomaly-detection agent —
/// the C# equivalent of Challenge 1's tool. Same name + parameter schema as
/// <c>agents.py</c>; the body doesn't re-read a file, it serializes the
/// <see cref="ThresholdReport"/> the pipeline already computed (T1). One grounding
/// path, shared with <see cref="ThresholdCheck"/>.
/// </summary>
public static class ThresholdsTool
{
    public const string Name = "check_thresholds";

    private const string Description =
        "Check if a machine's sensor readings are within normal operating thresholds. " +
        "Returns anomalies if any readings are out of spec.";

    private static readonly BinaryData Parameters = BinaryData.FromString("""
        {
          "type": "object",
          "properties": {
            "machine_id": {
              "type": "string",
              "description": "The machine ID (e.g. 'MX-001') or name (e.g. 'mixer') to check"
            }
          },
          "required": ["machine_id"],
          "additionalProperties": false
        }
        """);

    public static ResponseTool Definition() =>
        ResponseTool.CreateFunctionTool(Name, Parameters, strictModeEnabled: false, Description);

    /// <summary>Render a T1 report as the JSON string the agent expects back from the tool.</summary>
    public static string Serialize(ThresholdReport t1)
    {
        var readings = new JsonObject();
        var anomalies = new JsonArray();

        foreach (var s in t1.Sensors)
        {
            readings[s.Sensor.ToString().ToLowerInvariant()] = new JsonObject
            {
                ["value"] = s.Value,
                ["unit"] = s.Unit,
                ["min"] = s.Min,
                ["max"] = s.Max,
                ["in_spec"] = s.InSpec,
            };

            if (!s.InSpec)
            {
                var dir = s.Status == SensorStatus.High ? "above max" : "below min";
                anomalies.Add(new JsonObject
                {
                    ["sensor"] = s.Sensor.ToString().ToLowerInvariant(),
                    ["value"] = s.Value,
                    ["unit"] = s.Unit,
                    ["threshold_min"] = s.Min,
                    ["threshold_max"] = s.Max,
                    ["deviation"] = $"{s.DeviationPct:0.0}% {dir}",
                });
            }
        }

        var payload = new JsonObject
        {
            ["machine_id"] = t1.MachineId,
            ["reading_id"] = t1.ReadingId,
            ["severity"] = t1.Severity.ToString().ToLowerInvariant(),
            ["anomalies"] = anomalies,
            ["all_readings"] = readings,
        };
        return payload.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}

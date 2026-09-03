using System.Text.Json;
using TireForge.ApiProxy;
using TireForge.Core.Model;
using TireForge.Core.Reporting;
using TireForge.Core.Thresholds;

namespace TireForge.ApiProxy.Tests;

/// <summary>The wire shape the dashboard's mock <c>api</c> object expects.</summary>
public class ApiJsonTests
{
    [Fact]
    public void Enums_serialize_as_camelCase_strings()
    {
        var json = JsonSerializer.Serialize(
            new { severity = Severity.Crit, route = GateRoute.Review, standing = SensorStatus.High },
            ApiJson.Options);

        Assert.Contains("\"severity\":\"crit\"", json);
        Assert.Contains("\"route\":\"review\"", json);
        Assert.Contains("\"standing\":\"high\"", json);
    }

    [Fact]
    public void Contract_records_serialize_camelCase()
    {
        var resp = new CostResponse(
            new[] { new AgentCostView("Anomaly Detection", "gpt-5.4", 3, null, null) },
            TokenMetricsAvailable: false, Note: "n/a", GeneratedAt: DateTimeOffset.UnixEpoch);

        var json = JsonSerializer.Serialize(resp, ApiJson.Options);

        Assert.Contains("\"tokenMetricsAvailable\":false", json);
        Assert.Contains("\"agents\":[", json);
        Assert.Contains("\"calls\":3", json);
        Assert.Contains("\"tokens\":null", json);
    }
}

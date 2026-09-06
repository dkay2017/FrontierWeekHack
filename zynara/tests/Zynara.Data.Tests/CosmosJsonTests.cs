using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents;
using Zynara.Core;
using Zynara.Core.Abstractions;
using Zynara.Core.Demo;
using Zynara.Core.Model;
using Zynara.Core.View;
using Zynara.Data;

namespace Zynara.Data.Tests;

/// <summary>
/// The Cosmos docs are the domain records serialised straight through plus an
/// <c>id</c>. This pins that a full <see cref="CaseRecord"/> — nested pipeline
/// result, view, audit trail — survives the round trip Cosmos will do.
/// </summary>
public class CosmosJsonTests
{
    private static async Task<CaseRecord> BuildRecord(string scenarioId)
    {
        var sp = new ServiceCollection()
            .AddSingleton<IZynaraStore>(_ => DemoWorld.Seed(new InMemoryZynaraStore()))
            .AddZynaraAgents()
            .AddZynaraCore()
            .BuildServiceProvider();
        return await sp.GetRequiredService<CaseService>()
            .RunAsync(DemoCatalog.All.Single(s => s.Id == scenarioId).Request);
    }

    [Theory]
    [InlineData("demo-appeal")]
    [InlineData("demo-appeal-critic")]
    [InlineData("demo-contradiction")]
    [InlineData("demo-abstain")]
    public async Task Case_record_round_trips_through_a_cosmos_document(string scenarioId)
    {
        var original = await BuildRecord(scenarioId);

        var doc = CosmosJson.WithId(original, original.RequestId);
        // Cosmos requires an id string and preserves the domain shape.
        Assert.Equal(original.RequestId, (string?)doc["id"]);
        Assert.Equal(original.RequestId, (string?)doc["requestId"]);

        var wire = doc.ToJsonString();
        var back = CosmosJson.To<CaseRecord>(JsonNode.Parse(wire)!.AsObject())!;

        Assert.Equal(original.RequestId, back.RequestId);
        Assert.Equal(original.View.Status, back.View.Status);
        Assert.Equal(original.View.ApproveAuthority, back.View.ApproveAuthority);
        Assert.Equal(original.Result.Gate?.Route, back.Result.Gate?.Route);
        Assert.Equal(original.Result.Contradiction.HasConflicts, back.Result.Contradiction.HasConflicts);
        Assert.Equal(original.Audit.Count, back.Audit.Count);
        Assert.Equal(
            original.View.Precedents.Select(p => (p.CaseId, p.WonOnAppeal)),
            back.View.Precedents.Select(p => (p.CaseId, p.WonOnAppeal)));
    }

    [Fact]
    public void Container_names_match_the_bicep_list()
    {
        // 9 containers, each with a partition-key path.
        Assert.Equal(9, CosmosNames.All.Count);
        Assert.All(CosmosNames.All, c => Assert.StartsWith("/", c.PartitionKey));
        Assert.Contains(CosmosNames.All, c => c.Name == CosmosNames.Cases && c.PartitionKey == "/requestId");
    }

    [Fact]
    public void Reference_record_carries_its_partition_key_property()
    {
        var criteria = new Criteria("Bupa/Comprehensive", "MRI lumbar spine", "bupa-mri-ls-v3", "v3",
            new[] { new Criterion("c1", "physiotherapy", true) });

        var doc = CosmosJson.WithId(criteria, "id-1");
        Assert.Equal("Bupa/Comprehensive", (string?)doc["payerPlan"]); // the /payerPlan pk path
    }
}

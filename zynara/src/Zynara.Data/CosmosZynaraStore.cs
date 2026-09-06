using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;
using Zynara.Core.Abstractions;
using Zynara.Core.Model;

namespace Zynara.Data;

/// <summary>
/// Cosmos-backed <see cref="IZynaraStore"/> (TDD §12 · TD-4). Reference data
/// (criteria / rules / precedents / policy versions / denial cohorts) is curated;
/// this reads it and writes the advisory early-warnings.
/// </summary>
public sealed class CosmosZynaraStore(Database db) : IZynaraStore
{
    private Container C(string name) => db.GetContainer(name);

    public async Task<IReadOnlyList<PolicyRule>> GetPolicyRulesAsync(
        string payerPlan, Region region, string procedure, CancellationToken ct = default)
    {
        var q = new QueryDefinition(
                "SELECT * FROM c WHERE c.region = @region AND LOWER(c.procedure) = LOWER(@procedure)")
            .WithParameter("@region", region.ToString())
            .WithParameter("@procedure", procedure);

        return await ReadList<PolicyRule>(CosmosNames.PayerRules, q, new PartitionKey(payerPlan), ct);
    }

    public async Task<Criteria?> GetCriteriaAsync(
        string payerPlan, string procedure, Region region, CancellationToken ct = default)
    {
        var q = new QueryDefinition("SELECT * FROM c WHERE LOWER(c.procedure) = LOWER(@procedure)")
            .WithParameter("@procedure", procedure);

        var list = await ReadList<Criteria>(CosmosNames.Criteria, q, new PartitionKey(payerPlan), ct);
        return list.FirstOrDefault();
    }

    public async Task<IReadOnlyList<Precedent>> GetPrecedentsAsync(
        string payerPlan, string procedure, Region region, CancellationToken ct = default)
    {
        var q = new QueryDefinition(
                "SELECT * FROM c WHERE c.region = @region AND LOWER(c.procedure) = LOWER(@procedure)")
            .WithParameter("@region", region.ToString())
            .WithParameter("@procedure", procedure);

        return await ReadList<Precedent>(CosmosNames.Precedents, q, new PartitionKey(payerPlan), ct);
    }

    public async Task<AuthRecord?> GetAuthAsync(string requestId, CancellationToken ct = default)
    {
        var q = new QueryDefinition("SELECT TOP 1 * FROM c");
        var list = await ReadList<AuthRecord>(CosmosNames.AuthRecords, q, new PartitionKey(requestId), ct);
        return list.FirstOrDefault();
    }

    public async Task<IReadOnlyList<PolicyVersion>> GetPolicyVersionsAsync(
        string policyRef, CancellationToken ct = default)
    {
        var q = new QueryDefinition("SELECT * FROM c ORDER BY c.effectiveFrom ASC");
        return await ReadList<PolicyVersion>(CosmosNames.PolicyVersions, q, new PartitionKey(policyRef), ct);
    }

    public async Task<IReadOnlyList<DenialCohort>> GetDenialCohortsAsync(CancellationToken ct = default)
    {
        var q = new QueryDefinition("SELECT * FROM c");
        return await ReadList<DenialCohort>(CosmosNames.DenialCohorts, q, partitionKey: null, ct);
    }

    public async Task SaveEarlyWarningAsync(EarlyWarning warning, CancellationToken ct = default)
    {
        var doc = CosmosJson.WithId(warning, warning.Id);
        await C(CosmosNames.EarlyWarnings).UpsertItemAsync(doc, new PartitionKey(warning.Id), cancellationToken: ct);
    }

    private async Task<IReadOnlyList<T>> ReadList<T>(
        string container, QueryDefinition query, PartitionKey? partitionKey, CancellationToken ct)
    {
        var options = new QueryRequestOptions();
        if (partitionKey is { } pk) options.PartitionKey = pk;

        var results = new List<T>();
        using var iterator = C(container).GetItemQueryIterator<JsonObject>(query, requestOptions: options);
        while (iterator.HasMoreResults)
        {
            foreach (var node in await iterator.ReadNextAsync(ct))
            {
                var value = CosmosJson.To<T>(node);
                if (value is not null) results.Add(value);
            }
        }
        return results;
    }
}

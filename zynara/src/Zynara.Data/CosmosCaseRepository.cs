using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;
using Zynara.Core.Abstractions;
using Zynara.Core.Model;

namespace Zynara.Data;

/// <summary>
/// Cosmos-backed <see cref="ICaseRepository"/> — the assembled case, its reviewer
/// projection and its append-only audit trail (D15), one document per request id
/// in the <c>cases</c> container (partition <c>/requestId</c>).
/// </summary>
public sealed class CosmosCaseRepository(Database db) : ICaseRepository
{
    private Container Cases => db.GetContainer(CosmosNames.Cases);

    public async Task<CaseRecord?> GetAsync(string requestId, CancellationToken ct = default)
    {
        try
        {
            var doc = await Cases.ReadItemAsync<JsonObject>(requestId, new PartitionKey(requestId), cancellationToken: ct);
            return CosmosJson.To<CaseRecord>(doc.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<CaseRecord>> ListAsync(CancellationToken ct = default)
    {
        var q = new QueryDefinition("SELECT * FROM c ORDER BY c.runAt DESC");
        var results = new List<CaseRecord>();
        using var iterator = Cases.GetItemQueryIterator<JsonObject>(q);
        while (iterator.HasMoreResults)
        {
            foreach (var node in await iterator.ReadNextAsync(ct))
            {
                var record = CosmosJson.To<CaseRecord>(node);
                if (record is not null) results.Add(record);
            }
        }
        return results;
    }

    public async Task SaveAsync(CaseRecord record, CancellationToken ct = default)
    {
        var doc = CosmosJson.WithId(record, record.RequestId);
        await Cases.UpsertItemAsync(doc, new PartitionKey(record.RequestId), cancellationToken: ct);
    }
}

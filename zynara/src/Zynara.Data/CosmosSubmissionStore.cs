using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;
using Zynara.Core.Abstractions;
using Zynara.Core.Model;

namespace Zynara.Data;

/// <summary>
/// Cosmos-backed <see cref="ISubmissionStore"/> — one document per request id in
/// the <c>submissions</c> container (partition <c>/requestId</c>). Written only by
/// <c>Zynara.Submission</c>.
/// </summary>
public sealed class CosmosSubmissionStore(Database db) : ISubmissionStore
{
    private Container Submissions => db.GetContainer(CosmosNames.Submissions);

    public async Task<SubmissionRecord?> GetAsync(string requestId, CancellationToken ct = default)
    {
        try
        {
            var doc = await Submissions.ReadItemAsync<JsonObject>(
                requestId, new PartitionKey(requestId), cancellationToken: ct);
            return CosmosJson.To<SubmissionRecord>(doc.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<SubmissionRecord>> ListAsync(CancellationToken ct = default)
    {
        var q = new QueryDefinition("SELECT * FROM c ORDER BY c.submittedAt DESC");
        var results = new List<SubmissionRecord>();
        using var iterator = Submissions.GetItemQueryIterator<JsonObject>(q);
        while (iterator.HasMoreResults)
        {
            foreach (var node in await iterator.ReadNextAsync(ct))
            {
                var record = CosmosJson.To<SubmissionRecord>(node);
                if (record is not null) results.Add(record);
            }
        }
        return results;
    }

    public async Task SaveAsync(SubmissionRecord record, CancellationToken ct = default)
    {
        var doc = CosmosJson.WithId(record, record.RequestId);
        await Submissions.UpsertItemAsync(doc, new PartitionKey(record.RequestId), cancellationToken: ct);
    }
}

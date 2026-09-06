using Microsoft.Azure.Cosmos;
using Zynara.Core.Abstractions;
using Zynara.Core.Demo;
using Zynara.Core.Model;

namespace Zynara.Data;

/// <summary>
/// Creates the database + containers and, if the reference data is empty, seeds
/// the demo world (the same data <see cref="DemoWorld"/> puts in memory). Run
/// from <c>tools/Zynara.DbDeploy</c> after <c>azd provision</c>.
/// </summary>
public sealed class DataSeeder(CosmosClient client)
{
    public async Task EnsureContainersAsync(string databaseName = CosmosNames.Database, CancellationToken ct = default)
    {
        var db = (await client.CreateDatabaseIfNotExistsAsync(databaseName, cancellationToken: ct)).Database;
        foreach (var (name, pk) in CosmosNames.All)
            await db.CreateContainerIfNotExistsAsync(name, pk, cancellationToken: ct);
    }

    /// <summary>Seeds the demo reference data if the <c>criteria</c> container has none.</summary>
    public async Task<bool> SeedDemoWorldAsync(string databaseName = CosmosNames.Database, CancellationToken ct = default)
    {
        var db = client.GetDatabase(databaseName);

        if (await CountAsync(db.GetContainer(CosmosNames.Criteria), ct) > 0)
            return false;

        var mem = DemoWorld.Seed(new InMemoryZynaraStore());

        foreach (var region in new[] { Region.UK, Region.US })
        foreach (var (payer, procedure) in new[]
        {
            (DemoWorld.Payer, DemoWorld.Procedure),
            (DemoWorld.Payer, DemoWorld.RareProcedure),
        })
        {
            foreach (var rule in await mem.GetPolicyRulesAsync(payer, region, procedure, ct))
                await Upsert(db, CosmosNames.PayerRules, rule, $"{payer}|{region}|{procedure}", payer);

            if (await mem.GetCriteriaAsync(payer, procedure, region, ct) is { } criteria)
                await Upsert(db, CosmosNames.Criteria, criteria, $"{payer}|{procedure}", payer);

            foreach (var p in await mem.GetPrecedentsAsync(payer, procedure, region, ct))
                await Upsert(db, CosmosNames.Precedents, p, p.CaseId, payer);
        }

        foreach (var cohort in await mem.GetDenialCohortsAsync(ct))
            await Upsert(db, CosmosNames.DenialCohorts, cohort,
                $"{cohort.PayerPlan}|{cohort.Procedure}|{cohort.Region}", cohort.PayerPlan);

        return true;
    }

    private static async Task Upsert<T>(Database db, string container, T value, string id, string partitionKey)
    {
        var doc = CosmosJson.WithId(value, id);
        await db.GetContainer(container).UpsertItemAsync(doc, new PartitionKey(partitionKey));
    }

    private static async Task<int> CountAsync(Container c, CancellationToken ct)
    {
        using var it = c.GetItemQueryIterator<int>(new QueryDefinition("SELECT VALUE COUNT(1) FROM c"));
        return it.HasMoreResults ? (await it.ReadNextAsync(ct)).FirstOrDefault() : 0;
    }
}

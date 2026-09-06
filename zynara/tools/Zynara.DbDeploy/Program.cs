using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Zynara.Data;

// azd postprovision hook (see azure.yaml). Creates the database + containers and
// seeds the demo reference data if it is empty. Idempotent.
//
//   export COSMOS_ENDPOINT="https://<acct>.documents.azure.com:443/"
//   dotnet run --project tools/Zynara.DbDeploy

var endpoint = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT");
if (string.IsNullOrWhiteSpace(endpoint))
{
    Console.Error.WriteLine("COSMOS_ENDPOINT is not set.");
    return 2;
}

var client = new CosmosClient(endpoint, new DefaultAzureCredential(), new CosmosClientOptions
{
    ApplicationName = "care-approval-iq-dbdeploy",
    UseSystemTextJsonSerializerWithOptions = System.Text.Json.JsonSerializerOptions.Default,
});

var seeder = new DataSeeder(client);

Console.WriteLine("Ensuring database + containers…");
await seeder.EnsureContainersAsync();
Console.WriteLine($"  {CosmosNames.All.Count} containers ready in '{CosmosNames.Database}'.");

var seeded = await seeder.SeedDemoWorldAsync();
Console.WriteLine(seeded ? "Seeded the demo reference data." : "Reference data already present — nothing to seed.");

return 0;

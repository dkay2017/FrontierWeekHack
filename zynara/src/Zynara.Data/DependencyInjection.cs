using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Zynara.Core.Abstractions;
using Zynara.Core.Agents;

namespace Zynara.Data;

public static class DependencyInjection
{
    /// <summary>
    /// Wires the Cosmos-backed store, case repository and cost meter. Managed
    /// identity only (<see cref="DefaultAzureCredential"/>) — no keys. Call this
    /// instead of seeding an in-memory store when <c>COSMOS_ENDPOINT</c> is set.
    /// </summary>
    public static IServiceCollection AddZynaraData(
        this IServiceCollection services, string cosmosEndpoint,
        string databaseName = CosmosNames.Database, TokenCredential? credential = null)
    {
        services.AddSingleton(sp =>
        {
            var options = new CosmosClientOptions
            {
                ApplicationName = "care-approval-iq",
                UseSystemTextJsonSerializerWithOptions = CosmosJson.Options,
            };
            return new CosmosClient(cosmosEndpoint, credential ?? new DefaultAzureCredential(), options);
        });

        services.AddSingleton(sp => sp.GetRequiredService<CosmosClient>().GetDatabase(databaseName));

        services.AddSingleton<IZynaraStore, CosmosZynaraStore>();
        services.AddSingleton<ICaseRepository, CosmosCaseRepository>();
        services.AddScoped<IAgentCallRecorder, CosmosAgentCallRecorder>();

        return services;
    }
}

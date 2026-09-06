using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zynara.Agents.Foundry;
using Zynara.Agents.Stubs;
using Zynara.Core.Agents;

namespace Zynara.Agents;

public static class DependencyInjection
{
    /// <summary>Environment / config key that selects the agent implementation.</summary>
    public const string ModeVariable = "ZYNARA_AGENTS";

    /// <summary>
    /// Registers the five agent ports (<see cref="INeedsAuthAgent"/> /
    /// <see cref="IEvidenceGapAgent"/> / <see cref="IAppealBuilderAgent"/> /
    /// <see cref="IExpiryWatchAgent"/> / <see cref="IPolicyDriftAgent"/>).
    ///
    /// <c>ZYNARA_AGENTS=foundry</c> wires the hosted Foundry agents (needs
    /// <c>az login</c> + <c>PROJECT_ENDPOINT</c>); anything else — the default —
    /// wires the deterministic stub twins used offline and in CI.
    /// </summary>
    public static IServiceCollection AddZynaraAgents(
        this IServiceCollection services, IConfiguration? config = null)
    {
        var mode = Value(config, ModeVariable) ?? "stub";

        if (!mode.Equals("foundry", StringComparison.OrdinalIgnoreCase))
            return AddStubs(services);

        var options = new FoundryAgentOptions
        {
            ProjectEndpoint = Value(config, "PROJECT_ENDPOINT")
                ?? Value(config, "PROJECT_CONNECTION_STRING")
                ?? throw new InvalidOperationException(
                    "ZYNARA_AGENTS=foundry needs PROJECT_ENDPOINT (the Foundry project endpoint)."),
            Model = Value(config, "MODEL_DEPLOYMENT_NAME") ?? "gpt-5.4",
            VectorStoreId = Value(config, "VECTOR_STORE_ID"),
        };

        services.AddSingleton(options);
        services.AddSingleton(sp => new FoundryAgentClient(sp.GetRequiredService<FoundryAgentOptions>()));
        services.AddSingleton<FoundryAgentProvisioner>();

        // Cost metering: the data layer registers the real recorder; this keeps the
        // agents resolvable when no data layer is wired (tests, provisioner console).
        services.TryAddScoped<IAgentCallRecorder, NullAgentCallRecorder>();

        services.AddScoped<INeedsAuthAgent, FoundryNeedsAuthAgent>();
        services.AddScoped<IEvidenceGapAgent, FoundryEvidenceGapAgent>();
        services.AddScoped<IAppealBuilderAgent, FoundryAppealBuilderAgent>();
        services.AddScoped<ICriticAgent, FoundryCriticAgent>();
        services.AddScoped<IExpiryWatchAgent, FoundryExpiryWatchAgent>();
        services.AddScoped<IPolicyDriftAgent, FoundryPolicyDriftAgent>();
        return services;
    }

    /// <summary>Ensure the five Foundry agents exist. No-op when running with stubs.</summary>
    public static async Task EnsureZynaraAgentsAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        if (services.GetService<FoundryAgentProvisioner>() is { } provisioner)
            await provisioner.EnsureAllAsync(ct);
    }

    private static IServiceCollection AddStubs(IServiceCollection services)
    {
        services.TryAddSingleton<IAgentCallRecorder, NullAgentCallRecorder>();
        services.AddSingleton<INeedsAuthAgent, StubNeedsAuthAgent>();
        services.AddSingleton<IEvidenceGapAgent, StubEvidenceGapAgent>();
        services.AddSingleton<IAppealBuilderAgent, StubAppealBuilderAgent>();
        services.AddSingleton<ICriticAgent, StubCriticAgent>();
        services.AddSingleton<IExpiryWatchAgent, StubExpiryWatchAgent>();
        services.AddSingleton<IPolicyDriftAgent, StubPolicyDriftAgent>();
        return services;
    }

    private static string? Value(IConfiguration? config, string key)
    {
        var v = config?[key];
        if (string.IsNullOrWhiteSpace(v))
            v = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }
}

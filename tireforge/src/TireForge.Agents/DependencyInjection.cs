using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TireForge.Agents.Foundry;
using TireForge.Core.Agents;

namespace TireForge.Agents;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the three agent ports (<see cref="IAnomalyDetector"/> /
    /// <see cref="IFaultDiagnoser"/> / <see cref="IWorkOrderDrafter"/>).
    /// <c>TIREFORGE_AGENTS=foundry</c> wires the real hosted Foundry agents
    /// (Stage M, needs <c>az login</c> + <c>PROJECT_CONNECTION_STRING</c>);
    /// anything else — the default — wires the deterministic stubs (offline, tests).
    /// </summary>
    public static IServiceCollection AddTireForgeAgents(
        this IServiceCollection services, IConfiguration? config = null)
    {
        var mode = Value(config, FoundryAgentOptions.ModeVariable) ?? "stub";

        if (!mode.Equals("foundry", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IAnomalyDetector, StubAnomalyDetector>();
            services.AddSingleton<IFaultDiagnoser, StubFaultDiagnoser>();
            services.AddSingleton<IWorkOrderDrafter, StubWorkOrderDrafter>();
            return services;
        }

        var options = new FoundryAgentOptions
        {
            ProjectEndpoint = Value(config, "PROJECT_CONNECTION_STRING")
                ?? throw new InvalidOperationException(
                    "TIREFORGE_AGENTS=foundry needs PROJECT_CONNECTION_STRING (from factory/.env)."),
            Model = Value(config, "MODEL_DEPLOYMENT_NAME") ?? "gpt-5.4",
        };

        services.AddSingleton(options);
        services.AddSingleton(sp => new FoundryAgentClient(sp.GetRequiredService<FoundryAgentOptions>()));
        services.AddSingleton<FoundryAgentProvisioner>();
        services.AddSingleton<IAnomalyDetector, FoundryAnomalyDetector>();
        services.AddSingleton<IFaultDiagnoser, FoundryFaultDiagnoser>();
        services.AddSingleton<IWorkOrderDrafter, FoundryWorkOrderDrafter>();
        return services;
    }

    /// <summary>Ensure the three Foundry agents exist. No-op when running with stubs.</summary>
    public static async Task EnsureTireForgeAgentsAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        if (services.GetService<FoundryAgentProvisioner>() is { } provisioner)
            await provisioner.EnsureAllAsync(ct);
    }

    private static string? Value(IConfiguration? config, string key)
    {
        var v = config?[key];
        if (string.IsNullOrWhiteSpace(v)) v = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }
}

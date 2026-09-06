using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// <c>ZYNARA_AGENTS=foundry</c> will wire the hosted Foundry agents (added in
    /// the next slice); anything else — the default — wires the deterministic stub
    /// twins used offline and in CI.
    /// </summary>
    public static IServiceCollection AddZynaraAgents(
        this IServiceCollection services, IConfiguration? config = null)
    {
        var mode = Value(config, ModeVariable) ?? "stub";

        if (mode.Equals("foundry", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                "ZYNARA_AGENTS=foundry is not wired yet — the hosted Foundry implementations land in the next slice.");

        services.TryAddSingleton<IAgentCallRecorder, NullAgentCallRecorder>();
        services.AddSingleton<INeedsAuthAgent, StubNeedsAuthAgent>();
        services.AddSingleton<IEvidenceGapAgent, StubEvidenceGapAgent>();
        services.AddSingleton<IAppealBuilderAgent, StubAppealBuilderAgent>();
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

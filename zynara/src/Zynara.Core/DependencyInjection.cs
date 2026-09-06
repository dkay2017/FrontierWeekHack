using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zynara.Core.Abstractions;
using Zynara.Core.Gating;
using Zynara.Core.Pipeline;
using Zynara.Core.Recovery;
using Zynara.Core.View;

namespace Zynara.Core;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the deterministic spokes, the Gate and the pipeline. Needs
    /// <see cref="Zynara.Core.Agents"/> ports and an
    /// <see cref="Zynara.Core.Abstractions.IZynaraStore"/> registered by the caller
    /// (<c>AddZynaraAgents()</c> and the data layer, or their in-memory doubles).
    /// </summary>
    public static IServiceCollection AddZynaraCore(
        this IServiceCollection services, GateOptions? gate = null)
    {
        services.AddSingleton(gate ?? new GateOptions());
        services.AddSingleton<Gate>();

        services.AddScoped<NeedsAuthCheck>();
        services.AddScoped<EvidenceGapMatch>();
        services.AddScoped<AppealMatch>();
        services.AddScoped<CriticCheck>();
        services.AddScoped<ExpiryMath>();
        services.AddScoped<PolicyDiff>();
        services.AddScoped<AuthPipeline>();

        // Reviewer read model (P1-1 / P1-2). In-memory case store by default;
        // Zynara.Data overrides ICaseRepository with the Cosmos-backed one.
        services.TryAddSingleton<ICaseRepository, InMemoryCaseRepository>();
        services.AddScoped<CaseService>();

        // Estimated Recoverable Value (P2-1) — reads the denial-history cohorts.
        services.AddScoped<RecoveryService>();

        return services;
    }
}

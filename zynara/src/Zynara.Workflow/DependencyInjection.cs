using Microsoft.Extensions.DependencyInjection;

namespace Zynara.Workflow;

public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="CareApprovalRunner"/> — the MAF-Workflow pipeline.
    /// Needs the <c>Zynara.Core</c> spokes + Gate (<c>AddZynaraCore()</c>) and an
    /// <c>IZynaraStore</c> already registered.
    /// </summary>
    public static IServiceCollection AddZynaraWorkflow(this IServiceCollection services)
    {
        services.AddScoped<CareApprovalRunner>();
        return services;
    }
}

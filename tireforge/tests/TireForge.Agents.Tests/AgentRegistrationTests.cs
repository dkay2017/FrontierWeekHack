using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TireForge.Agents.Foundry;
using TireForge.Core.Agents;

namespace TireForge.Agents.Tests;

/// <summary>The <c>TIREFORGE_AGENTS</c> stub/foundry DI switch (Decision D12).</summary>
public class AgentRegistrationTests
{
    private static IConfiguration Config(params (string, string)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p => new KeyValuePair<string, string?>(p.Item1, p.Item2)))
            .Build();

    [Fact]
    public void Default_wires_the_stubs()
    {
        var sp = new ServiceCollection().AddTireForgeAgents(Config()).BuildServiceProvider();

        Assert.IsType<StubAnomalyDetector>(sp.GetRequiredService<IAnomalyDetector>());
        Assert.IsType<StubFaultDiagnoser>(sp.GetRequiredService<IFaultDiagnoser>());
        Assert.IsType<StubWorkOrderDrafter>(sp.GetRequiredService<IWorkOrderDrafter>());
        Assert.Null(sp.GetService<FoundryAgentProvisioner>());
    }

    [Fact]
    public void Foundry_mode_wires_the_hosted_agent_impls()
    {
        var sp = new ServiceCollection()
            .AddTireForgeAgents(Config(
                ("TIREFORGE_AGENTS", "foundry"),
                ("PROJECT_CONNECTION_STRING", "https://example.services.ai.azure.com/api/projects/p")))
            .BuildServiceProvider();

        Assert.IsType<FoundryAnomalyDetector>(sp.GetRequiredService<IAnomalyDetector>());
        Assert.IsType<FoundryFaultDiagnoser>(sp.GetRequiredService<IFaultDiagnoser>());
        Assert.IsType<FoundryWorkOrderDrafter>(sp.GetRequiredService<IWorkOrderDrafter>());
        Assert.NotNull(sp.GetRequiredService<FoundryAgentProvisioner>());
        Assert.Equal("gpt-5.4", sp.GetRequiredService<FoundryAgentOptions>().Model);
    }

    [Fact]
    public void Foundry_mode_without_an_endpoint_throws_a_clear_error()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddTireForgeAgents(Config(("TIREFORGE_AGENTS", "foundry"))));

        Assert.Contains("PROJECT_CONNECTION_STRING", ex.Message);
    }

    [Fact]
    public void Provisioner_lists_the_three_agents_with_the_tool_only_on_anomaly()
    {
        var sp = new ServiceCollection()
            .AddTireForgeAgents(Config(
                ("TIREFORGE_AGENTS", "foundry"),
                ("PROJECT_CONNECTION_STRING", "https://example.services.ai.azure.com/api/projects/p")))
            .BuildServiceProvider();

        var specs = sp.GetRequiredService<FoundryAgentProvisioner>().Specs;

        Assert.Equal(3, specs.Count);
        Assert.Single(specs, s => s.Name == "anomaly-detection-agent" && s.Tools is { Count: 1 });
        Assert.Single(specs, s => s.Name == "fault-diagnosis-agent" && s.Tools is null);
        Assert.Single(specs, s => s.Name == "work-order-agent" && s.Tools is null);
    }
}

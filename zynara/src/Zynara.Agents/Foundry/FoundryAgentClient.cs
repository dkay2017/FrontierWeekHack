using System.Collections.Concurrent;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Core;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI.Responses;
using Zynara.Core.Diagnostics;

namespace Zynara.Agents.Foundry;

/// <summary>
/// Wrapper over the Foundry agent surface. Provisioning (create-or-ensure a
/// persistent agent version) still uses <see cref="AgentAdministrationClient"/>;
/// invocation goes through the <b>Microsoft Agent Framework</b>
/// <see cref="FoundryChatClient"/> (bound to the existing server-side agent by
/// name) — an <c>IChatClient</c>, so it carries MAF's middleware / telemetry and
/// is trivially fakeable in tests.
/// </summary>
public sealed class FoundryAgentClient
{
    private readonly Uri _endpoint;
    private readonly TokenCredential _credential;
    private readonly string _model;
    private readonly AgentAdministrationClient _admin;
    private readonly AIProjectClient _project;
    private readonly ConcurrentDictionary<string, byte> _ensured = new();
    private readonly ConcurrentDictionary<string, AIAgent> _agents = new();

    public FoundryAgentClient(FoundryAgentOptions options, TokenCredential? credential = null)
    {
        _endpoint = new Uri(options.ProjectEndpoint);
        _credential = credential ?? new DefaultAzureCredential();
        _model = options.Model;
        _admin = new AgentAdministrationClient(_endpoint, _credential);
        _project = new AIProjectClient(_endpoint, _credential);
    }

    /// <summary>
    /// Ensure a persistent agent exists. Creates the first version if the agent is
    /// absent; leaves an existing agent untouched (idempotent per process).
    /// </summary>
    public async Task EnsureAgentAsync(
        string name, string instructions, IEnumerable<ResponseTool>? tools = null, CancellationToken ct = default)
    {
        if (!_ensured.TryAdd(name, 0))
            return;

        try
        {
            await _admin.GetAgentAsync(name, ct);
            return; // already there
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // not found (or transient) — fall through to create
        }

        var definition = new DeclarativeAgentDefinition(_model) { Instructions = instructions };
        if (tools is not null)
            foreach (var tool in tools)
                definition.Tools.Add(tool);

        await _admin.CreateAgentVersionAsync(name, new ProjectsAgentVersionCreationOptions(definition), null, ct);
    }

    /// <summary>Force a new version of an agent (used by the provisioner to push prompt/tool changes).</summary>
    public async Task<string> CreateVersionAsync(
        string name, string instructions, IEnumerable<ResponseTool>? tools = null, CancellationToken ct = default)
    {
        var definition = new DeclarativeAgentDefinition(_model) { Instructions = instructions };
        if (tools is not null)
            foreach (var tool in tools)
                definition.Tools.Add(tool);

        var version = await _admin.CreateAgentVersionAsync(
            name, new ProjectsAgentVersionCreationOptions(definition), null, ct);
        _ensured.TryAdd(name, 0);
        return version.Value.Id;
    }

    /// <summary>
    /// Invoke a hosted agent once, via the Microsoft Agent Framework
    /// <see cref="AIAgent"/> bound to the existing server-side agent version.
    /// </summary>
    public async Task<AgentInvocation> InvokeAsync(
        string agentName,
        string userText,
        Func<string, string, string>? toolHandler = null,   // unused — the agents carry their tools server-side
        CancellationToken ct = default)
    {
        var agent = _agents.GetOrAdd(agentName,
            name => _project.AsAIAgent(new AgentReference(name, version: null)));

        using var chat = ZynaraTelemetry.StartChat(_model);
        chat?.SetTag("zynara.agent", agentName);

        var response = await agent.RunAsync(userText, cancellationToken: ct);

        var inTokens = (int)(response.Usage?.InputTokenCount ?? 0);
        var outTokens = (int)(response.Usage?.OutputTokenCount ?? 0);
        ZynaraTelemetry.RecordChatUsage(chat, inTokens, outTokens, toolCalls: 0);

        return new AgentInvocation(response.Text ?? "", ToolCalls: 0, inTokens, outTokens);
    }
}

/// <summary>What one hosted-agent call returned.</summary>
public sealed record AgentInvocation(string Text, int ToolCalls, int InputTokens, int OutputTokens);

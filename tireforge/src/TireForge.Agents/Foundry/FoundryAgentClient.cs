using System.Collections.Concurrent;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects.Agents;
using Azure.Core;
using Azure.Identity;
using OpenAI.Responses;

namespace TireForge.Agents.Foundry;

/// <summary>
/// Thin wrapper over the nextgen Foundry projects 2.x API (see the Stage-M spike's
/// FINDINGS.md): create-or-ensure a persistent agent version, and invoke a hosted
/// agent once — running the function-tool loop when a handler is supplied.
/// </summary>
public sealed class FoundryAgentClient
{
    private readonly Uri _endpoint;
    private readonly TokenCredential _credential;
    private readonly string _model;
    private readonly AgentAdministrationClient _admin;
    private readonly ConcurrentDictionary<string, byte> _ensured = new();

    public FoundryAgentClient(FoundryAgentOptions options, TokenCredential? credential = null)
    {
        _endpoint = new Uri(options.ProjectEndpoint);
        _credential = credential ?? new DefaultAzureCredential();
        _model = options.Model;
        _admin = new AgentAdministrationClient(_endpoint, _credential);
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
    /// Invoke a hosted agent once. If <paramref name="toolHandler"/> is supplied it
    /// is called for every <c>function_call</c> the model emits, and the loop
    /// continues until the model returns a plain answer.
    /// </summary>
    public async Task<AgentInvocation> InvokeAsync(
        string agentName,
        string userText,
        Func<string, string, string>? toolHandler = null,
        CancellationToken ct = default)
    {
        var responses = new ProjectResponsesClient(
            _endpoint, _credential, new AgentReference(agentName, version: null),
            defaultConversationId: null, options: null);

        var toolCalls = 0;
        ResponseResult result = (await responses.CreateResponseAsync(userText, null, ct)).Value;

        while (toolHandler is not null)
        {
            var calls = result.OutputItems.OfType<FunctionCallResponseItem>().ToList();
            if (calls.Count == 0)
                break;

            var outputs = new List<ResponseItem>();
            foreach (var call in calls)
            {
                toolCalls++;
                var args = call.FunctionArguments?.ToString() ?? "{}";
                var output = toolHandler(call.FunctionName, args);
                outputs.Add(ResponseItem.CreateFunctionCallOutputItem(call.CallId, output));
            }

            result = (await responses.CreateResponseAsync(outputs, result.Id, ct)).Value;
        }

        var usage = result.Usage;
        return new AgentInvocation(
            result.GetOutputText() ?? "",
            toolCalls,
            usage?.InputTokenCount ?? 0,
            usage?.OutputTokenCount ?? 0);
    }
}

/// <summary>What one hosted-agent call returned.</summary>
public sealed record AgentInvocation(string Text, int ToolCalls, int InputTokens, int OutputTokens);

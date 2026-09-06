using System.Diagnostics;
using Zynara.Core.Model;

namespace Zynara.Core.Diagnostics;

/// <summary>
/// The one <see cref="ActivitySource"/> for the pipeline (Agent-a-thon Challenge 2
/// — agent-keyed traces). One <c>pipeline.run</c> span per request, a
/// <c>spoke.&lt;name&gt;</c> child per deterministic spoke, an
/// <c>invoke_agent &lt;name&gt;</c> child whenever a spoke actually calls its
/// agent, and a <c>chat &lt;model&gt;</c> grandchild per hosted-model round-trip
/// (added by the Foundry client). The stub path emits the same tree minus the
/// <c>chat</c> spans — so the trace shows exactly where reasoning happened.
///
/// Consumers: the Function hosts register this source with the Azure Monitor
/// OpenTelemetry exporter (see each <c>Program.cs</c>); tests attach an
/// <see cref="ActivityListener"/>. When nothing is listening every helper here
/// returns <c>null</c> and costs almost nothing.
/// </summary>
public static class ZynaraTelemetry
{
    public const string SourceName = "Zynara.Pipeline";

    public static readonly ActivitySource Source = new(SourceName, "1.0.0");

    public static Activity? StartPipelineRun(Request request)
    {
        var a = Source.StartActivity("pipeline.run");
        a?.SetTag("zynara.request_id", request.Id);
        a?.SetTag("zynara.procedure", request.Procedure);
        a?.SetTag("zynara.payer_plan", request.PayerPlan);
        a?.SetTag("zynara.region", request.Region.ToString());
        a?.SetTag("zynara.is_appeal", !string.IsNullOrWhiteSpace(request.DenialLetter));
        return a;
    }

    /// <summary>The deterministic wrapper span for one spoke (e.g. <c>needs-auth</c>).</summary>
    public static Activity? StartSpoke(string spoke)
    {
        var a = Source.StartActivity($"spoke.{spoke}");
        a?.SetTag("zynara.spoke", spoke);
        return a;
    }

    /// <summary>Wraps the actual agent call inside a spoke (e.g. <c>invoke_agent evidence-gap</c>).</summary>
    public static Activity? StartAgentInvoke(string agent)
    {
        var a = Source.StartActivity($"invoke_agent {agent}");
        a?.SetTag("zynara.agent", agent);
        return a;
    }

    /// <summary>One hosted-model round-trip (added by <c>FoundryAgentClient</c>).</summary>
    public static Activity? StartChat(string model)
    {
        var a = Source.StartActivity($"chat {model}", ActivityKind.Client);
        a?.SetTag("gen_ai.system", "azure.ai.foundry");
        a?.SetTag("gen_ai.request.model", model);
        return a;
    }

    public static void RecordRoute(Activity? run, GateRoute route, int reasoningSteps)
    {
        run?.SetTag("zynara.route", route.ToString());
        run?.SetTag("zynara.reasoning_steps", reasoningSteps);
    }

    public static void RecordChatUsage(Activity? chat, int inputTokens, int outputTokens, int toolCalls)
    {
        chat?.SetTag("gen_ai.usage.input_tokens", inputTokens);
        chat?.SetTag("gen_ai.usage.output_tokens", outputTokens);
        chat?.SetTag("zynara.tool_calls", toolCalls);
    }
}

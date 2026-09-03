namespace TireForge.Core.Model;

/// <summary>
/// One hosted-agent invocation, recorded for cost metering (Decision D13 — the
/// "metering table" TDD §7 refers to, without APIM). Written only by the real
/// Foundry agents; the stubs record nothing.
/// </summary>
public class AgentCall
{
    /// <summary><c>call-&lt;ticks&gt;-&lt;rand&gt;</c>.</summary>
    public required string Id { get; set; }

    /// <summary><c>anomaly-detection-agent</c> / <c>fault-diagnosis-agent</c> / <c>work-order-agent</c>.</summary>
    public required string AgentName { get; set; }

    /// <summary>Model deployment (<c>gpt-5.4</c>).</summary>
    public required string Model { get; set; }

    /// <summary>W3C trace id of the pipeline run this call belonged to.</summary>
    public string? TraceId { get; set; }

    /// <summary>The reading that triggered the run.</summary>
    public string? ReadingId { get; set; }

    /// <summary>Prompt tokens, summed across any tool-call loop within the invocation.</summary>
    public int PromptTokens { get; set; }

    /// <summary>Completion tokens, summed across the invocation.</summary>
    public int CompletionTokens { get; set; }

    /// <summary>Function-tool calls the model made during the invocation.</summary>
    public int ToolCalls { get; set; }

    public DateTimeOffset At { get; set; }

    public int TotalTokens => PromptTokens + CompletionTokens;
}

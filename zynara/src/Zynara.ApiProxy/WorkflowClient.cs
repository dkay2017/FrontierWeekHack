using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Zynara.Core.Authority;
using Zynara.Core.Model;
using Zynara.Core.View;

namespace Zynara.ApiProxy;

/// <summary>
/// Thin client over the MAF Workflow host (<c>Zynara.WorkflowHost</c>). When
/// <c>WORKFLOW_URL</c> is set, the api-proxy stops running the pipeline itself and
/// starts a durable workflow run instead — <c>runId</c> is the request id, so no
/// id-mapping is needed. The reviewer decision is delivered to the workflow's
/// <c>review</c> port; the workflow records it and (on an authorised approve-send)
/// hands the case to the Submission Adapter.
/// </summary>
public sealed class WorkflowClient(
    IHttpClientFactory http, IConfiguration config, CaseService cases, ILogger<WorkflowClient> log)
{
    private const string WorkflowName = "CareApprovalPipeline";

    private string? Base => config["WORKFLOW_URL"]?.TrimEnd('/');
    private string? Key => config["WORKFLOW_KEY"];

    /// <summary>True when the workflow host is configured — the api-proxy defers to it.</summary>
    public bool Enabled => !string.IsNullOrWhiteSpace(Base);

    private HttpClient Client()
    {
        var client = http.CreateClient();
        if (!string.IsNullOrWhiteSpace(Key))
            client.DefaultRequestHeaders.Add("x-functions-key", Key);
        return client;
    }

    /// <summary>
    /// Starts a durable run and waits (bounded) for <c>assemble</c> to persist the
    /// case, so the caller can return the assembled view just as the in-process
    /// path did. Returns the record, or null if it did not appear in time.
    /// </summary>
    public async Task<CaseRecord?> RunAsync(Request request, CancellationToken ct = default)
    {
        var client = Client();
        var url = $"{Base}/api/workflows/{WorkflowName}/run?runId={Uri.EscapeDataString(request.Id)}";
        var res = await client.PostAsJsonAsync(url, request, ct);
        res.EnsureSuccessStatusCode();
        log.LogInformation("Workflow run started for {RequestId}.", request.Id);

        // `assemble` runs the whole reasoning pipeline (five agents) before it
        // persists — allow for that plus a Consumption cold start.
        return await PollAsync(request.Id, r => r is not null, maxAttempts: 90, ct);
    }

    /// <summary>
    /// Delivers the reviewer decision to the workflow's <c>review</c> port and
    /// waits (bounded) for the workflow to apply it — the decision is recorded, or
    /// the authority check refused it (a REFUSED audit entry appears).
    /// </summary>
    public async Task<DecisionOutcome> RespondAsync(
        string requestId, string action, string? by, ReviewerRole role, string? note,
        CancellationToken ct = default)
    {
        var before = await cases.GetAsync(requestId, ct);
        if (before is null) return DecisionOutcome.NotFound;
        var auditBefore = before.Audit.Count;

        var client = Client();
        var url = $"{Base}/api/workflows/{WorkflowName}/respond/{Uri.EscapeDataString(requestId)}";
        var body = new { eventName = "review", response = new { Action = action, By = by, Role = role, Note = note } };
        var res = await client.PostAsJsonAsync(url, body, ct);
        res.EnsureSuccessStatusCode();

        var applied = await PollAsync(requestId,
            r => r is not null && (r.Decision is not null || r.Audit.Count > auditBefore),
            maxAttempts: 40, ct);

        if (applied?.Decision is not null)
            return DecisionOutcome.Ok(applied);

        var refusal = applied?.Audit.LastOrDefault(a => a.Detail.StartsWith("REFUSED"));
        return refusal is not null
            ? DecisionOutcome.Forbidden(new AuthorityCheck(false, refusal.Detail, role))
            : DecisionOutcome.Ok(applied ?? before);   // in flight — dashboard reloads
    }

    private async Task<CaseRecord?> PollAsync(
        string requestId, Func<CaseRecord?, bool> done, int maxAttempts, CancellationToken ct)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var record = await cases.GetAsync(requestId, ct);
            if (done(record)) return record;
            await Task.Delay(TimeSpan.FromMilliseconds(750), ct);
        }
        log.LogWarning("Workflow poll for {RequestId} timed out.", requestId);
        return await cases.GetAsync(requestId, ct);
    }
}

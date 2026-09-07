using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Zynara.Core.Authority;
using Zynara.Core.Model;
using Zynara.Core.View;

namespace Zynara.ApiProxy;

/// <summary>
/// The reviewer read API. Everything here is a projection or a stored decision —
/// no clinical judgement, no outbound action. The dashboard is the only client.
/// </summary>
public sealed class CaseFunctions(
    CaseService cases, WorkflowClient workflow, IHttpClientFactory http,
    IConfiguration config, ILogger<CaseFunctions> log)
{
    [Function("Health")]
    public Task<HttpResponseData> Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req) =>
        Json.Ok(req, new { status = "ok", service = "Zynara.ApiProxy" });

    [Function("ListCases")]
    public async Task<HttpResponseData> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "cases")] HttpRequestData req)
    {
        var records = await cases.ListAsync();
        return await Json.Ok(req, records.Select(CaseSummary.From).ToList());
    }

    [Function("GetCase")]
    public async Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "cases/{id}")] HttpRequestData req,
        string id)
    {
        var record = await cases.GetAsync(id);
        return record is null
            ? await Json.Error(req, HttpStatusCode.NotFound, $"no case '{id}'.")
            : await Json.Ok(req, new
            {
                view = record.View,
                decision = record.Decision,
                audit = record.Audit,
                runAt = record.RunAt,
            });
    }

    [Function("RunCase")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "cases")] HttpRequestData req)
    {
        Request? request;
        try
        {
            request = await Json.ReadAsync<Request>(req);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return await Json.Error(req, HttpStatusCode.BadRequest, $"malformed request: {ex.Message}");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Id))
            return await Json.Error(req, HttpStatusCode.BadRequest, "a request with a non-empty 'id' is required.");

        // With the MAF host wired, start a durable workflow run; otherwise run the
        // pipeline in-process (offline demo / tests).
        var record = workflow.Enabled ? await workflow.RunAsync(request) : await cases.RunAsync(request);
        if (record is null)
            return await Json.Accepted(req, new { status = "assembling", requestId = request.Id });

        log.LogInformation("Ran case {RequestId} → {Status}.", record.RequestId, record.View.Status);
        return await Json.Created(req, record.View);
    }

    [Function("RecordDecision")]
    public async Task<HttpResponseData> Decision(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "cases/{id}/decision")] HttpRequestData req,
        string id)
    {
        var body = await Json.ReadAsync<DecisionRequest>(req);
        if (body is null || string.IsNullOrWhiteSpace(body.Action))
            return await Json.Error(req, HttpStatusCode.BadRequest, "a decision with an 'action' is required.");

        // Demo stand-in for Entra ID app roles: the reviewer's role + id come in headers.
        var role = req.Headers.TryGetValues("X-Reviewer-Role", out var rv)
            && Enum.TryParse<ReviewerRole>(rv.FirstOrDefault(), ignoreCase: true, out var parsed)
            ? parsed : ReviewerRole.Reviewer;
        var by = req.Headers.TryGetValues("X-Reviewer-Id", out var iv) ? iv.FirstOrDefault() : body.By;

        // With the MAF host wired, deliver the decision to the workflow's review
        // port (it records it and, on an authorised approve-send, submits);
        // otherwise record it here.
        var outcome = workflow.Enabled
            ? await workflow.RespondAsync(id, body.Action, by, role, body.Note)
            : await cases.RecordDecisionAsync(id, body.Action, by, role, body.Note);

        if (!outcome.Found)
            return await Json.Error(req, HttpStatusCode.NotFound, $"no case '{id}'.");
        if (!outcome.Allowed)
            return await Json.Error(req, HttpStatusCode.Forbidden, outcome.Refusal!.Reason);

        log.LogInformation("Case {RequestId}: {Role} {Action}.", id, role, body.Action);
        return await Json.Ok(req, new { summary = CaseSummary.From(outcome.Record!), audit = outcome.Record!.Audit });
    }

    /// <summary>
    /// Hands an approved case to the Submission Adapter (the sole outbound path).
    /// The dashboard talks only to this origin; the api-proxy forwards to the
    /// submission Function app named by <c>SUBMISSION_URL</c>.
    /// </summary>
    [Function("SendCase")]
    public async Task<HttpResponseData> Send(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "cases/{id}/submit")] HttpRequestData req,
        string id)
    {
        var submissionUrl = config["SUBMISSION_URL"];
        if (string.IsNullOrWhiteSpace(submissionUrl))
            return await Json.Error(req, HttpStatusCode.ServiceUnavailable, "SUBMISSION_URL is not configured.");

        var client = http.CreateClient();
        var upstream = await client.PostAsync($"{submissionUrl.TrimEnd('/')}/api/submit/{id}", content: null);
        var payload = await upstream.Content.ReadAsStringAsync();

        log.LogInformation("Case {RequestId}: forwarded to submission → {Status}.", id, (int)upstream.StatusCode);

        var res = req.CreateResponse(upstream.StatusCode);
        res.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await res.WriteStringAsync(string.IsNullOrWhiteSpace(payload) ? "{}" : payload);
        return res;
    }
}

/// <summary>Body of <c>POST /api/cases/{id}/decision</c>. Role + id also accepted via X-Reviewer-* headers.</summary>
public sealed record DecisionRequest(string Action, string? Note, string? By);

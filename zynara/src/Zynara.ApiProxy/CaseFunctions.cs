using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Zynara.Core.Model;
using Zynara.Core.View;

namespace Zynara.ApiProxy;

/// <summary>
/// The reviewer read API. Everything here is a projection or a stored decision —
/// no clinical judgement, no outbound action. The dashboard is the only client.
/// </summary>
public sealed class CaseFunctions(CaseService cases, ILogger<CaseFunctions> log)
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
            : await Json.Ok(req, new { view = record.View, decision = record.Decision, runAt = record.RunAt });
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

        var record = await cases.RunAsync(request);
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

        var record = await cases.RecordDecisionAsync(id, body.Action, body.By, body.Note);
        return record is null
            ? await Json.Error(req, HttpStatusCode.NotFound, $"no case '{id}'.")
            : await Json.Ok(req, CaseSummary.From(record));
    }
}

/// <summary>Body of <c>POST /api/cases/{id}/decision</c>. RBAC / authority checks land with P1-3.</summary>
public sealed record DecisionRequest(string Action, string? Note, string? By);

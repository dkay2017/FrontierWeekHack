using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Zynara.Core.Model;
using Zynara.Core.Submission;

namespace Zynara.Submission;

/// <summary>
/// The Submission Adapter's HTTP surface — the sole outbound path to a payer.
/// It never decides anything: it sends a case a reviewer has already approved,
/// and it will not send the same request twice.
/// </summary>
public sealed class SubmissionFunctions(SubmissionService submissions, ILogger<SubmissionFunctions> log)
{
    [Function("Health")]
    public Task<HttpResponseData> Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req) =>
        Json.Ok(req, new { status = "ok", service = "Zynara.Submission" });

    /// <summary>Send an approved case. Idempotent — a second call returns the existing record.</summary>
    [Function("Submit")]
    public async Task<HttpResponseData> Submit(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "submit/{requestId}")] HttpRequestData req,
        string requestId)
    {
        var outcome = await submissions.SubmitAsync(requestId);
        log.LogInformation("Submit {RequestId} → {Kind}", requestId, outcome.Kind);

        var code = outcome.Kind switch
        {
            SubmissionResultKind.Submitted => HttpStatusCode.OK,
            SubmissionResultKind.Rejected => HttpStatusCode.OK,
            SubmissionResultKind.CaseNotFound => HttpStatusCode.NotFound,
            SubmissionResultKind.NotApproved => HttpStatusCode.Conflict,
            _ => HttpStatusCode.BadGateway,
        };

        var res = req.CreateResponse(code);
        res.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await res.WriteStringAsync(System.Text.Json.JsonSerializer.Serialize(new
        {
            requestId,
            result = outcome.Kind.ToString(),
            message = outcome.Message,
            record = outcome.Record,
        }, Json.Options));
        return res;
    }

    [Function("GetSubmission")]
    public async Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "submissions/{requestId}")] HttpRequestData req,
        string requestId)
    {
        var record = await submissions.GetAsync(requestId);
        return record is null
            ? await Json.Error(req, HttpStatusCode.NotFound, $"no submission for '{requestId}'.")
            : await Json.Ok(req, record);
    }

    [Function("ListSubmissions")]
    public async Task<HttpResponseData> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "submissions")] HttpRequestData req) =>
        await Json.Ok(req, await submissions.ListAsync());
}

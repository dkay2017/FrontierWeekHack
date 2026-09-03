using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TireForge.Core.Reviewing;

namespace TireForge.ApiProxy;

/// <summary>
/// Stage L — the reviewer console's write path over HTTP, wrapping the Stage-K
/// <see cref="Reviewer"/>. Every write still flows through the Work Order Adapter
/// (invariant 1.1); a rejection is an audit row, never a silent drop. Domain
/// exceptions map to problem responses via <see cref="HttpProblem"/>. Anonymous
/// auth for now (Decision D10).
/// </summary>
public sealed class ReviewFunctions(Reviewer reviewer)
{
    public sealed record ApproveRequest(string DiagnosisId, string Reviewer);
    public sealed record RejectRequest(string DiagnosisId, string Reviewer, string Note);

    [Function("ReviewApprove")]
    public Task<IActionResult> Approve(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "review/approve")] HttpRequest req,
        CancellationToken ct)
        => Run(req, async body =>
        {
            var r = Parse<ApproveRequest>(body);
            return await reviewer.ApproveAsync(r.DiagnosisId, r.Reviewer, ct);
        });

    [Function("ReviewReject")]
    public Task<IActionResult> Reject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "review/reject")] HttpRequest req,
        CancellationToken ct)
        => Run(req, async body =>
        {
            var r = Parse<RejectRequest>(body);
            return await reviewer.RejectAsync(r.DiagnosisId, r.Reviewer, r.Note, ct);
        });

    [Function("WorkOrderClose")]
    public Task<IActionResult> Close(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "workorders/{id}/close")] HttpRequest req,
        string id,
        CancellationToken ct)
        => Run(req, async _ => await reviewer.CloseAsync(id, ct), readBody: false);

    private static async Task<IActionResult> Run(
        HttpRequest req, Func<string, Task<object>> act, bool readBody = true)
    {
        string body = "";
        if (readBody)
        {
            using var reader = new StreamReader(req.Body);
            body = await reader.ReadToEndAsync();
        }

        try
        {
            return new OkObjectResult(await act(body));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or JsonException)
        {
            var problem = HttpProblem.Details(ex);
            return new ObjectResult(problem) { StatusCode = problem.Status };
        }
    }

    private static T Parse<T>(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Request body is required.");
        return JsonSerializer.Deserialize<T>(body, ApiJson.Options)
               ?? throw new ArgumentException("Request body could not be read.");
    }
}

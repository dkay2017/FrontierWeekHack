using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Zynara.Core.Model;

namespace Zynara.Orchestrator.Http;

/// <summary>
/// The async boundary (TDD §12 · TD-3). <c>POST /api/requests</c> starts one
/// <c>AuthOrchestrator</c> instance keyed on the request id — a redelivered call is
/// a no-op — and returns the Durable status URLs. <c>GET /api/requests/{id}</c> is
/// a convenience probe over the same instance.
/// </summary>
public sealed class RequestEndpoints(ILogger<RequestEndpoints> log)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Function("SubmitRequest")]
    public async Task<HttpResponseData> Submit(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "requests")] HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        Request? request;
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            request = JsonSerializer.Deserialize<Request>(body, Json);
        }
        catch (JsonException ex)
        {
            return await Text(req, HttpStatusCode.BadRequest, $"malformed request body: {ex.Message}");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Id))
            return await Text(req, HttpStatusCode.BadRequest, "a request body with a non-empty 'id' is required.");

        var existing = await client.GetInstanceAsync(request.Id);
        if (existing is not null)
        {
            log.LogInformation("Request {RequestId} already has orchestration {Status} — not restarting.",
                request.Id, existing.RuntimeStatus);
            return await client.CreateCheckStatusResponseAsync(req, request.Id);
        }

        await client.ScheduleNewOrchestrationInstanceAsync(
            "AuthOrchestrator", request, new StartOrchestrationOptions(InstanceId: request.Id));

        log.LogInformation("Started AuthOrchestrator for request {RequestId} ({Procedure}, {PayerPlan}, {Region}).",
            request.Id, request.Procedure, request.PayerPlan, request.Region);

        return await client.CreateCheckStatusResponseAsync(req, request.Id);
    }

    [Function("GetRequest")]
    public static async Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "requests/{id}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string id)
    {
        var instance = await client.GetInstanceAsync(id, getInputsAndOutputs: true);
        if (instance is null)
            return await Text(req, HttpStatusCode.NotFound, $"no request '{id}'.");

        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(new
        {
            id,
            status = instance.RuntimeStatus.ToString(),
            result = instance.RuntimeStatus == OrchestrationRuntimeStatus.Completed
                ? instance.ReadOutputAs<PipelineResult>()
                : null,
        });
        return res;
    }

    private static async Task<HttpResponseData> Text(HttpRequestData req, HttpStatusCode code, string message)
    {
        var res = req.CreateResponse(code);
        await res.WriteStringAsync(message);
        return res;
    }
}

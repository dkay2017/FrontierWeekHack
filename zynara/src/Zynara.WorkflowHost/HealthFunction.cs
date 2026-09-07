using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;

namespace Zynara.WorkflowHost;

/// <summary>
/// A trivial function that takes a <c>[DurableClient]</c> — its only job is to
/// make the build-time metadata generator pull the Durable Task binding extension
/// into <c>WorkerExtensions</c> (MAF generates the workflow functions at runtime,
/// so the generator sees no Durable attributes in our code otherwise).
/// </summary>
public sealed class HealthFunction
{
    [Function("Health")]
    public HttpResponseData Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        var res = req.CreateResponse(System.Net.HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "application/json");
        res.WriteString($"{{\"status\":\"ok\",\"service\":\"Zynara.WorkflowHost\",\"durable\":\"{client.Name}\"}}");
        return res;
    }
}

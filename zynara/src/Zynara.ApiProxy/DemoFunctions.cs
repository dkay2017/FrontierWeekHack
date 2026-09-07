using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Zynara.Core.Demo;
using Zynara.Core.View;

namespace Zynara.ApiProxy;

/// <summary>Drives the scripted demo: list the scenarios, (re-)run one on demand.</summary>
public sealed class DemoFunctions(CaseService cases, WorkflowClient workflow)
{
    [Function("ListScenarios")]
    public Task<HttpResponseData> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "demo/scenarios")] HttpRequestData req) =>
        Json.Ok(req, DemoCatalog.All
            .Select(s => new { s.Id, s.Label, s.Expectation })
            .ToList());

    [Function("RunScenario")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "demo/scenarios/{id}/run")] HttpRequestData req,
        string id)
    {
        var scenario = DemoCatalog.All.FirstOrDefault(s => s.Id == id);
        if (scenario is null)
            return await Json.Error(req, System.Net.HttpStatusCode.NotFound, $"no scenario '{id}'.");

        var record = workflow.Enabled
            ? await workflow.RunAsync(scenario.Request)
            : await cases.RunAsync(scenario.Request);
        return record is null
            ? await Json.Accepted(req, new { status = "assembling", requestId = scenario.Id })
            : await Json.Ok(req, record.View);
    }
}

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Zynara.Core.Impact;

namespace Zynara.ApiProxy;

/// <summary>Before / after benchmark (P2-2) — measured pipeline metrics vs a labelled manual estimate.</summary>
public sealed class BenchmarkFunctions(BenchmarkService benchmark)
{
    [Function("GetBenchmark")]
    public async Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "benchmark")] HttpRequestData req) =>
        await Json.Ok(req, await benchmark.GetAsync());
}

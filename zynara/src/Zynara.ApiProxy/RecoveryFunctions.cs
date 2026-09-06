using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Zynara.Core.Recovery;

namespace Zynara.ApiProxy;

/// <summary>Estimated Recoverable Value (P2-1) — the portfolio total and each scope that feeds it.</summary>
public sealed class RecoveryFunctions(RecoveryService recovery)
{
    [Function("GetRecovery")]
    public async Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "recovery")] HttpRequestData req) =>
        await Json.Ok(req, await recovery.GetAsync());
}

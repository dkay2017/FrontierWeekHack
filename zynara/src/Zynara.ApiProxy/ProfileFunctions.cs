using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Zynara.Core.Model;
using Zynara.Core.Profiles;

namespace Zynara.ApiProxy;

/// <summary>Policy + Regulatory Profiles (P1-5) — what the "UK ⇄ US switch" actually carries.</summary>
public sealed class ProfileFunctions
{
    [Function("ListProfiles")]
    public Task<HttpResponseData> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "profiles")] HttpRequestData req) =>
        Json.Ok(req, RegulatoryProfiles.All);

    [Function("GetProfile")]
    public async Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "profiles/{region}")] HttpRequestData req,
        string region) =>
        Enum.TryParse<Region>(region, ignoreCase: true, out var r)
            ? await Json.Ok(req, RegulatoryProfiles.For(r))
            : await Json.Error(req, HttpStatusCode.NotFound, $"no profile for region '{region}'.");
}

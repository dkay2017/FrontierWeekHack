using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TireForge.Core.Reporting;

namespace TireForge.ApiProxy;

/// <summary>
/// Stage L — the dashboard read models over HTTP. Every endpoint is a thin
/// delegate to <see cref="Reports"/> (pure reads, no writes). Anonymous auth
/// for now (Decision D10).
/// </summary>
public sealed class ReportsFunctions(Reports reports)
{
    [Function("Status")]
    public async Task<IActionResult> Status(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "status")] HttpRequest req,
        CancellationToken ct)
        => new OkObjectResult(await reports.StatusAsync(ct));

    [Function("Queue")]
    public async Task<IActionResult> Queue(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "queue")] HttpRequest req,
        CancellationToken ct)
        => new OkObjectResult(await reports.QueueAsync(ct));

    [Function("WorkOrders")]
    public async Task<IActionResult> WorkOrders(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "workorders")] HttpRequest req,
        CancellationToken ct)
        => new OkObjectResult(await reports.WorkOrdersAsync(ct));

    [Function("Health")]
    public async Task<IActionResult> Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req,
        CancellationToken ct)
        => new OkObjectResult(await reports.HealthAsync(ct));

    [Function("Cost")]
    public async Task<IActionResult> Cost(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "cost")] HttpRequest req,
        CancellationToken ct)
        => new OkObjectResult(await reports.CostAsync(ct));
}

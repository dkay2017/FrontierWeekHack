using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using TireForge.Core.Abstractions;
using TireForge.Core.Model;
using TireForge.Core.Sensing;

namespace TireForge.Ingestion;

/// <summary>
/// Ingestion layer (Build Plan). The Sensor Simulator is a thin wrapper over
/// <see cref="ReadingFactory"/>; it publishes readings onto the <c>readings</c>
/// queue that <c>TireForge.Orchestrator</c> consumes. An HTTP endpoint emits a
/// single reading on demand (Challenge 4 — "HTTP trigger: on-demand endpoint").
/// </summary>
public class SensorFunctions(IMachineStore machines)
{
    private const string QueueName = "readings";

    // Mostly-healthy stream with the occasional warn / crit — enough to keep the
    // review queue and the dashboard interesting without drowning it.
    private static readonly (ReadingMode Mode, int Weight)[] Mix =
    {
        (ReadingMode.Normal, 74), (ReadingMode.Warn, 19), (ReadingMode.Crit, 7),
    };

    /// <summary>Timer — one reading per machine every 5 minutes (a "shift tick").</summary>
    [Function("SensorSimulator")]
    [QueueOutput(QueueName, Connection = "AzureWebJobsStorage")]
    public async Task<string[]> Simulate(
        [TimerTrigger("0 */5 * * * *")] TimerInfo timer, FunctionContext context)
    {
        var log = context.GetLogger("SensorSimulator");
        var now = DateTimeOffset.UtcNow;
        var rng = Random.Shared;

        var all = await machines.ListAsync(context.CancellationToken);
        var messages = all.Select(m =>
        {
            var reading = ReadingFactory.Make(m, PickMode(rng), now, rng);
            reading.Machine = null;
            return JsonSerializer.Serialize(reading);
        }).ToArray();

        log.LogInformation("Simulated {Count} readings at {Now:o}.", messages.Length, now);
        return messages;
    }

    /// <summary>On-demand — <c>POST /api/emit/CP-003/crit</c> (mode optional, defaults to warn).</summary>
    [Function("EmitReading")]
    public async Task<EmitResponse> Emit(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "emit/{machineId}/{mode?}")] HttpRequestData req,
        string machineId, string? mode, FunctionContext context)
    {
        var machine = await machines.GetAsync(machineId, context.CancellationToken);
        var response = req.CreateResponse();

        if (machine is null)
        {
            response.StatusCode = HttpStatusCode.NotFound;
            await response.WriteStringAsync($"Unknown machine '{machineId}'.");
            return new EmitResponse { Http = response };
        }

        if (!Enum.TryParse<ReadingMode>(mode ?? "Warn", ignoreCase: true, out var readingMode))
            readingMode = ReadingMode.Warn;

        var reading = ReadingFactory.Make(machine, readingMode, DateTimeOffset.UtcNow);
        reading.Machine = null;
        var json = JsonSerializer.Serialize(reading);

        response.StatusCode = HttpStatusCode.Accepted;
        await response.WriteAsJsonAsync(new { reading.Id, reading.MachineId, mode = readingMode.ToString() });
        return new EmitResponse { Http = response, QueueMessage = json };
    }

    private static ReadingMode PickMode(Random rng)
    {
        var roll = rng.Next(Mix.Sum(x => x.Weight));
        foreach (var (mode, weight) in Mix)
        {
            if (roll < weight) return mode;
            roll -= weight;
        }
        return ReadingMode.Normal;
    }
}

/// <summary>Multi-output: the HTTP response plus (optionally) a queued reading.</summary>
public sealed class EmitResponse
{
    [QueueOutput("readings", Connection = "AzureWebJobsStorage")]
    public string? QueueMessage { get; set; }

    public HttpResponseData Http { get; set; } = null!;
}

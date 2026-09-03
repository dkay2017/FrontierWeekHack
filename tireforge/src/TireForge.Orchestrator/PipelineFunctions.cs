using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using TireForge.Core.Model;
using TireForge.Core.Pipeline;

namespace TireForge.Orchestrator;

/// <summary>
/// Compute layer (Build Plan — Hub &amp; Spokes). A reading lands on the
/// <c>readings</c> queue → a durable orchestration runs the Core pipeline in one
/// activity (Decision D2). The instance id is the reading id, so a redelivered
/// queue message is a no-op (idempotency).
/// </summary>
public class PipelineFunctions(Pipeline pipeline)
{
    private const string QueueName = "readings";

    /// <summary>Queue → start the orchestration (keyed on the reading id).</summary>
    [Function("PipelineStarter")]
    public static async Task Start(
        [QueueTrigger(QueueName, Connection = "AzureWebJobsStorage")] Reading reading,
        [DurableClient] DurableTaskClient client,
        FunctionContext context)
    {
        var log = context.GetLogger("PipelineStarter");

        var existing = await client.GetInstanceAsync(reading.Id);
        if (existing is not null)
        {
            log.LogInformation("Reading {ReadingId} already has orchestration {Status} — skipping duplicate.",
                reading.Id, existing.RuntimeStatus);
            return;
        }

        await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(PipelineOrchestrator), reading,
            new StartOrchestrationOptions(InstanceId: reading.Id));

        log.LogInformation("Started pipeline orchestration for reading {ReadingId} ({MachineId}).",
            reading.Id, reading.MachineId);
    }

    /// <summary>The orchestration — deterministic, schedules the one activity.</summary>
    [Function(nameof(PipelineOrchestrator))]
    public static async Task<PipelineRunSummary> PipelineOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var reading = context.GetInput<Reading>()
            ?? throw new InvalidOperationException("Orchestration started with no reading.");
        return await context.CallActivityAsync<PipelineRunSummary>(nameof(RunPipeline), reading);
    }

    /// <summary>The activity — the only place IO happens (EF + agent calls).</summary>
    [Function(nameof(RunPipeline))]
    public async Task<PipelineRunSummary> RunPipeline([ActivityTrigger] Reading reading)
    {
        var result = await pipeline.RunAsync(reading);
        return PipelineRunSummary.From(result);
    }
}

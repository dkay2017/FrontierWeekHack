using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Zynara.Orchestrator.Tests;

/// <summary>
/// A minimal in-process <see cref="TaskOrchestrationContext"/> for testing the
/// orchestrator's wiring: it records each activity name called and routes the call
/// to a supplied handler (the real spoke). Timers, sub-orchestrations, external
/// events and other durable primitives are not supported — the orchestrator does
/// not use them.
/// </summary>
internal sealed class FakeOrchestrationContext(
    object input,
    Func<string, object?, Task<object?>> dispatch) : TaskOrchestrationContext
{
    public List<string> Calls { get; } = new();

    public override TaskName Name => "AuthOrchestrator";
    public override string InstanceId => "test-instance";
    public override DateTime CurrentUtcDateTime => new(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);
    public override bool IsReplaying => false;
    public override ParentOrchestrationInstance? Parent => null;
    protected override ILoggerFactory LoggerFactory => NullLoggerFactory.Instance;

    public override T GetInput<T>() => (T)input;

    public override async Task<T> CallActivityAsync<T>(TaskName name, object? activityInput = null, TaskOptions? options = null)
    {
        Calls.Add(name.Name);
        var result = await dispatch(name.Name, activityInput);
        return (T)result!;
    }

    public override async Task CallActivityAsync(TaskName name, object? activityInput = null, TaskOptions? options = null)
    {
        Calls.Add(name.Name);
        await dispatch(name.Name, activityInput);
    }

    public override Task<TResult> CallSubOrchestratorAsync<TResult>(TaskName orchestratorName, object? input = null, TaskOptions? options = null) =>
        throw new NotSupportedException();

    public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public override Task<T> WaitForExternalEvent<T>(string eventName, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public override void SendEvent(string instanceId, string eventName, object payload) =>
        throw new NotSupportedException();

    public override void SetCustomStatus(object? customStatus) => throw new NotSupportedException();

    public override Guid NewGuid() => throw new NotSupportedException();

    public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true) =>
        throw new NotSupportedException();
}

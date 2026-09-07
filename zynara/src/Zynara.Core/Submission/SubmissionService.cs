using System.Diagnostics;
using Zynara.Core.Abstractions;
using Zynara.Core.Diagnostics;
using Zynara.Core.Model;

namespace Zynara.Core.Submission;

/// <summary>Why a submit call did or did not go through.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum SubmissionResultKind
{
    /// <summary>Sent and acknowledged (or already sent — this call is idempotent).</summary>
    Submitted,

    /// <summary>The payer rejected it on receipt.</summary>
    Rejected,

    /// <summary>No case with that id.</summary>
    CaseNotFound,

    /// <summary>The case exists but no reviewer has approved it for sending.</summary>
    NotApproved,

    /// <summary>The send failed (transport / auth). Safe to retry.</summary>
    Failed,
}

public sealed record SubmissionOutcome(SubmissionResultKind Kind, SubmissionRecord? Record, string Message)
{
    public bool Sent => Kind is SubmissionResultKind.Submitted;
}

/// <summary>
/// Turns an <b>approved</b> case into one outbound submission. Enforces the two
/// rules that make this Responsible-AI-safe: a case is only ever sent after a
/// reviewer approved it (<c>approve-send</c>), and a second call for the same
/// request returns the existing record rather than sending twice.
/// </summary>
public sealed class SubmissionService(
    ICaseRepository cases,
    ISubmissionStore submissions,
    IPayerGateway gateway,
    TimeProvider? clock = null)
{
    private const string ApproveAction = "approve-send";
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<SubmissionOutcome> SubmitAsync(string requestId, CancellationToken ct = default)
    {
        using var span = ZynaraTelemetry.Source.StartActivity("submission.send");
        span?.SetTag("zynara.request_id", requestId);

        var existing = await submissions.GetAsync(requestId, ct);
        if (existing is { Status: SubmissionStatus.Submitted })
        {
            span?.SetTag("zynara.submission_status", "already-sent");
            return new SubmissionOutcome(SubmissionResultKind.Submitted, existing,
                $"Already submitted — payer reference {existing.PayerReference}.");
        }

        var record = await cases.GetAsync(requestId, ct);
        if (record is null)
            return new SubmissionOutcome(SubmissionResultKind.CaseNotFound, null,
                $"No assembled case for request {requestId}.");

        if (record.Decision is not { Action: ApproveAction })
            return new SubmissionOutcome(SubmissionResultKind.NotApproved, null,
                "The case has not been approved for sending by a reviewer.");

        if (record.Result.Draft is not { } draft)
            return new SubmissionOutcome(SubmissionResultKind.Failed, null,
                "The case has no assembled submission draft.");

        var approvedBy = record.Decision.By ?? "unknown";
        span?.SetTag("zynara.channel", gateway.Channel);

        PayerAck ack;
        try
        {
            ack = await gateway.SendAsync(draft, record.Request.PayerPlan, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var failed = Build(record, SubmissionStatus.Failed, "", approvedBy, gateway.Channel,
                $"Send failed: {ex.Message}");
            await submissions.SaveAsync(failed, ct);
            span?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return new SubmissionOutcome(SubmissionResultKind.Failed, failed, failed.Detail);
        }

        var status = ack.Accepted ? SubmissionStatus.Submitted : SubmissionStatus.Rejected;
        var saved = Build(record, status, ack.Reference, approvedBy, gateway.Channel, ack.Detail);
        await submissions.SaveAsync(saved, ct);
        span?.SetTag("zynara.submission_status", status.ToString());

        return new SubmissionOutcome(
            ack.Accepted ? SubmissionResultKind.Submitted : SubmissionResultKind.Rejected, saved, ack.Detail);
    }

    public Task<SubmissionRecord?> GetAsync(string requestId, CancellationToken ct = default) =>
        submissions.GetAsync(requestId, ct);

    public Task<IReadOnlyList<SubmissionRecord>> ListAsync(CancellationToken ct = default) =>
        submissions.ListAsync(ct);

    private SubmissionRecord Build(
        CaseRecord record, SubmissionStatus status, string reference,
        string approvedBy, string channel, string detail) =>
        new(record.RequestId,
            record.Request.PayerPlan,
            record.Request.Procedure,
            record.Result.Draft?.RequiredCode,
            status,
            reference,
            _clock.GetUtcNow(),
            approvedBy,
            channel,
            detail);
}

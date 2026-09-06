namespace Zynara.Core.Model;

/// <summary>What became of a submission the Submission Adapter sent out.</summary>
public enum SubmissionStatus
{
    /// <summary>Handed to the payer and acknowledged.</summary>
    Submitted,

    /// <summary>The payer rejected it on receipt (before adjudication).</summary>
    Rejected,

    /// <summary>The send failed — a transport / auth error. Retryable.</summary>
    Failed,
}

/// <summary>
/// One outbound submission. Written only by the Submission Adapter
/// (<c>Zynara.Submission</c>) — the sole component that touches a payer — after a
/// reviewer has approved the case. Partition <c>/requestId</c> in the
/// <c>submissions</c> container.
/// </summary>
public sealed record SubmissionRecord(
    string RequestId,
    string PayerPlan,
    string Procedure,
    string? RequiredCode,
    SubmissionStatus Status,
    string PayerReference,          // the payer's acknowledgement id ("" when Failed)
    DateTimeOffset SubmittedAt,
    string ApprovedBy,             // the reviewer who authorised the send
    string Channel,                // "stub" | "payer-api:<name>"
    string Detail);

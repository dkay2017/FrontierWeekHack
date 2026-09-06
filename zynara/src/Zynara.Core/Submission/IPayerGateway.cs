using Zynara.Core.Model;

namespace Zynara.Core.Submission;

/// <summary>
/// The one outbound path to a payer. The real implementation
/// (<c>Zynara.Submission.KeyVaultPayerGateway</c>) reads the payer-integration
/// credential from Key Vault and calls the payer's intake API; the in-process
/// <see cref="StubPayerGateway"/> simulates a deterministic acknowledgement for
/// local runs, tests and the demo. Nothing else in the system talks to a payer.
/// </summary>
public interface IPayerGateway
{
    Task<PayerAck> SendAsync(SubmissionDraft draft, string payerPlan, CancellationToken ct = default);

    /// <summary>Names the channel for the audit record — e.g. "stub" or "payer-api:bupa".</summary>
    string Channel { get; }
}

/// <summary>The payer's response to a submission.</summary>
public sealed record PayerAck(bool Accepted, string Reference, string Detail);

/// <summary>
/// Deterministic stand-in: accepts the submission and returns a reference derived
/// from the request id, so a demo run is reproducible. No network, no credential.
/// </summary>
public sealed class StubPayerGateway : IPayerGateway
{
    public string Channel => "stub";

    public Task<PayerAck> SendAsync(SubmissionDraft draft, string payerPlan, CancellationToken ct = default) =>
        Task.FromResult(new PayerAck(
            Accepted: true,
            Reference: $"ACK-{draft.RequestId}",
            Detail: $"{payerPlan} intake accepted the submission for {draft.Procedure} " +
                    $"({draft.RequiredCode ?? "code TBC"})."));
}

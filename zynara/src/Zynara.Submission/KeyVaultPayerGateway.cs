using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using Zynara.Core.Model;
using Zynara.Core.Submission;

namespace Zynara.Submission;

/// <summary>
/// The production-shaped payer gateway: this app's managed identity
/// (<c>id-submission</c>) is the <b>only</b> one with <c>Key Vault Secrets User</c>
/// on the vault, so it is the only component that can read the payer-integration
/// credential — the identity boundary the review asked for (P1-4).
///
/// The actual payer intake call (X12 278 / FHIR / portal RPA) is out of scope for
/// the hackathon and is not made; the credential is fetched to prove the boundary,
/// then the send is acknowledged. Swap the marked block for the real client.
/// </summary>
public sealed class KeyVaultPayerGateway(string keyVaultUri, ILogger<KeyVaultPayerGateway> log) : IPayerGateway
{
    private const string SecretName = "payer-integration-credential";

    private readonly SecretClient _secrets = new(new Uri(keyVaultUri), new DefaultAzureCredential());

    public string Channel => "payer-api:simulated";

    public async Task<PayerAck> SendAsync(SubmissionDraft draft, string payerPlan, CancellationToken ct = default)
    {
        // Proves the identity boundary — only id-submission can read this.
        KeyVaultSecret credential;
        try
        {
            credential = await _secrets.GetSecretAsync(SecretName, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Could not read {Secret} from {Vault}", SecretName, keyVaultUri);
            throw;
        }

        // --- production: build and send the payer intake message here, using
        //     `credential.Value` for auth. Not implemented for the hackathon. ---
        log.LogInformation(
            "Payer credential resolved ({Version}); submitting {Procedure} for {Payer} (request {RequestId})",
            credential.Properties.Version, draft.Procedure, payerPlan, draft.RequestId);

        return new PayerAck(
            Accepted: true,
            Reference: $"PAYER-{draft.RequestId}",
            Detail: $"{payerPlan} intake accepted the submission for {draft.Procedure} " +
                    $"({draft.RequiredCode ?? "code TBC"}) via the integration credential.");
    }
}

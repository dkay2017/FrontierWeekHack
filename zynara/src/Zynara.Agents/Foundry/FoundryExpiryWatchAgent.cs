using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Agents.Foundry;

/// <summary>
/// <c>expiry-watch</c> backed by the hosted agent — phrases the alert. The margin
/// and the decision to raise the warning stay in <c>ExpiryMath</c>.
/// </summary>
public sealed class FoundryExpiryWatchAgent(
    FoundryAgentClient client, FoundryAgentOptions options, IAgentCallRecorder recorder) : IExpiryWatchAgent
{
    public async Task<string> NarrateAsync(
        AuthRecord auth, DateOnly procedureDate, int daysOfMargin, CancellationToken ct = default)
    {
        var prompt =
            $"Authorisation {auth.AuthId} for {auth.Procedure} ({auth.PayerPlan}) is valid " +
            $"{auth.ApprovedOn:yyyy-MM-dd} to {auth.ValidUntil:yyyy-MM-dd}. " +
            $"Procedure booked for {procedureDate:yyyy-MM-dd}. Days of margin: {daysOfMargin}.";

        var inv = await client.InvokeAsync(options.ExpiryWatchAgentName, prompt, toolHandler: null, ct);
        await Usage.RecordAsync(recorder, options.ExpiryWatchAgentName, options.Model, inv, auth.RequestId, ct);
        return inv.Text.Trim();
    }
}

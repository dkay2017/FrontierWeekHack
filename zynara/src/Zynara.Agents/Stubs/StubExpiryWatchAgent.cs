using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Agents.Stubs;

/// <summary>
/// Deterministic <c>expiry-watch</c> twin. Phrases the expiry-risk alert from the
/// margin <c>ExpiryMath</c> already computed. Advisory only — no Gate, no outbound
/// action.
/// </summary>
public sealed class StubExpiryWatchAgent : IExpiryWatchAgent
{
    public Task<string> NarrateAsync(
        AuthRecord auth, DateOnly procedureDate, int daysOfMargin, CancellationToken ct = default)
    {
        var lead = daysOfMargin < 0
            ? $"Authorisation {auth.AuthId} for {auth.Procedure} expires {auth.ValidUntil:yyyy-MM-dd}, " +
              $"{-daysOfMargin} day(s) BEFORE the procedure on {procedureDate:yyyy-MM-dd}."
            : $"Authorisation {auth.AuthId} for {auth.Procedure} is valid until {auth.ValidUntil:yyyy-MM-dd}; " +
              $"the procedure is booked for {procedureDate:yyyy-MM-dd}, {daysOfMargin} day(s) of margin.";

        var advice = daysOfMargin switch
        {
            < 0 => " Re-submission required now to avoid a lapse in cover.",
            <= 7 => " Margin is tight — confirm the schedule or request an extension this week.",
            <= 21 => " Watch this one; flag if the procedure date slips.",
            _ => " No action needed.",
        };

        return Task.FromResult(lead + advice);
    }
}

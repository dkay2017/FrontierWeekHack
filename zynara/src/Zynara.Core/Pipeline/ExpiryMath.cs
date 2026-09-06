using Zynara.Core.Abstractions;
using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Core.Pipeline;

/// <summary>
/// Spoke 4 (runs continuously, advisory). Computes the days of margin between an
/// approved authorisation's validity window and the scheduled procedure date, and
/// raises an <see cref="EarlyWarning"/> when the margin is thin or negative. The
/// <c>expiry-watch</c> agent only phrases the alert.
/// </summary>
public sealed class ExpiryMath(IZynaraStore store, IExpiryWatchAgent agent)
{
    /// <summary>Raise a warning at or below this many days of margin.</summary>
    public const int WarnAtDays = 21;

    public async Task<ExpiryResult> RunAsync(
        string requestId, DateOnly procedureDate, CancellationToken ct = default)
    {
        var auth = await store.GetAuthAsync(requestId, ct);
        if (auth is null)
            return new ExpiryResult(int.MaxValue, null);

        var daysOfMargin = auth.ValidUntil.DayNumber - procedureDate.DayNumber;
        if (daysOfMargin > WarnAtDays)
            return new ExpiryResult(daysOfMargin, null);

        var text = await agent.NarrateAsync(auth, procedureDate, daysOfMargin, ct);
        var warning = new EarlyWarning(
            Id: $"ew-expiry-{auth.AuthId}",
            Kind: "expiry",
            SubjectRef: auth.AuthId,
            Text: text,
            RaisedOn: DateOnly.FromDateTime(DateTime.UtcNow));

        await store.SaveEarlyWarningAsync(warning, ct);
        return new ExpiryResult(daysOfMargin, warning);
    }
}

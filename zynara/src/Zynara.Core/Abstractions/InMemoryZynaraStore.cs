using Zynara.Core.Model;

namespace Zynara.Core.Abstractions;

/// <summary>
/// In-memory <see cref="IZynaraStore"/> — the double used by unit tests, local runs
/// and (later) the pre-computed demo playback. <c>Zynara.Data</c> supplies the real
/// Cosmos-backed implementation. Seed it with the <c>Add*</c> methods.
/// </summary>
public sealed class InMemoryZynaraStore : IZynaraStore
{
    private readonly List<PolicyRule> _rules = new();
    private readonly List<Criteria> _criteria = new();
    private readonly List<Precedent> _precedents = new();
    private readonly Dictionary<string, AuthRecord> _auths = new();
    private readonly Dictionary<string, List<PolicyVersion>> _policyVersions = new();
    private readonly List<DenialCohort> _cohorts = new();

    /// <summary>Early warnings raised during a run — inspect these in tests.</summary>
    public List<EarlyWarning> EarlyWarnings { get; } = new();

    public InMemoryZynaraStore AddRule(PolicyRule rule) { _rules.Add(rule); return this; }
    public InMemoryZynaraStore AddCriteria(Criteria criteria) { _criteria.Add(criteria); return this; }
    public InMemoryZynaraStore AddPrecedent(Precedent precedent) { _precedents.Add(precedent); return this; }
    public InMemoryZynaraStore AddAuth(AuthRecord auth) { _auths[auth.RequestId] = auth; return this; }

    public InMemoryZynaraStore AddDenialCohort(DenialCohort cohort) { _cohorts.Add(cohort); return this; }

    public InMemoryZynaraStore AddPolicyVersion(PolicyVersion version)
    {
        if (!_policyVersions.TryGetValue(version.PolicyRef, out var list))
            _policyVersions[version.PolicyRef] = list = new List<PolicyVersion>();
        list.Add(version);
        return this;
    }

    public Task<IReadOnlyList<PolicyRule>> GetPolicyRulesAsync(
        string payerPlan, Region region, string procedure, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PolicyRule>>(_rules
            .Where(r => r.PayerPlan == payerPlan && r.Region == region &&
                        r.Procedure.Equals(procedure, StringComparison.OrdinalIgnoreCase))
            .ToList());

    public Task<Criteria?> GetCriteriaAsync(
        string payerPlan, string procedure, Region region, CancellationToken ct = default) =>
        Task.FromResult(_criteria.FirstOrDefault(c =>
            c.PayerPlan == payerPlan &&
            c.Procedure.Equals(procedure, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<Precedent>> GetPrecedentsAsync(
        string payerPlan, string procedure, Region region, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Precedent>>(_precedents
            .Where(p => p.PayerPlan == payerPlan && p.Region == region &&
                        p.Procedure.Equals(procedure, StringComparison.OrdinalIgnoreCase))
            .ToList());

    public Task<AuthRecord?> GetAuthAsync(string requestId, CancellationToken ct = default) =>
        Task.FromResult(_auths.GetValueOrDefault(requestId));

    public Task<IReadOnlyList<PolicyVersion>> GetPolicyVersionsAsync(
        string policyRef, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PolicyVersion>>(
            _policyVersions.GetValueOrDefault(policyRef)?.ToList() ?? new List<PolicyVersion>());

    public Task<IReadOnlyList<DenialCohort>> GetDenialCohortsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DenialCohort>>(_cohorts.ToList());

    public Task SaveEarlyWarningAsync(EarlyWarning warning, CancellationToken ct = default)
    {
        EarlyWarnings.RemoveAll(w => w.Id == warning.Id);
        EarlyWarnings.Add(warning);
        return Task.CompletedTask;
    }
}

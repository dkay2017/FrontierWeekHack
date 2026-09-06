using Zynara.Core.Model;
using Zynara.Core.Profiles;

namespace Zynara.Core.Tests;

/// <summary>Policy + Regulatory Profile (P1-5 / evaluator §9) — it carries more than a policy pack.</summary>
public class ProfileTests
{
    [Fact]
    public void Both_regions_have_a_profile_with_the_full_set_of_dimensions()
    {
        foreach (var p in RegulatoryProfiles.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.PolicyPack));
            Assert.False(string.IsNullOrWhiteSpace(p.CodingConventions));
            Assert.False(string.IsNullOrWhiteSpace(p.IntegrationProfile));
            Assert.NotEmpty(p.Regulators);
            Assert.NotEmpty(p.AppealPath);
            Assert.Contains("prior-auth", p.Terminology.Keys);
        }
    }

    [Fact]
    public void For_resolves_by_region_and_the_two_differ()
    {
        Assert.Equal("uk", RegulatoryProfiles.For(Region.UK).Id);
        Assert.Equal("us", RegulatoryProfiles.For(Region.US).Id);

        Assert.Equal("prior authorisation", RegulatoryProfiles.For(Region.UK).Terminology["prior-auth"]);
        Assert.Equal("prior authorization", RegulatoryProfiles.For(Region.US).Terminology["prior-auth"]);
        Assert.NotEqual(RegulatoryProfiles.For(Region.UK).AppealPath, RegulatoryProfiles.For(Region.US).AppealPath);
    }
}

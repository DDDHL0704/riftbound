using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{
    [Fact]
    public async Task CoreRuleEnginePlaysPeakGuardianWithItsIndependentSelfBoon()
    {
        var fixture = await ConformanceFixture.LoadAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "p2-preflight-play-ogn-peak-guardian-boon-static.fixture.json"), CancellationToken.None);
        var result = await ConformanceFixtureRunner.RunAsync(fixture, new CoreRuleEngine(), CancellationToken.None);
        var differences = ConformanceFixtureRunner.CompareExpected(fixture, result);
        Assert.True(differences.Count == 0, string.Join("\n", differences));
        const string source = "P1-UNIT-OGN-PEAK-GUARDIAN";
        Assert.Equal([source], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        var guardian = result.FinalState.CardObjects[source];
        Assert.Equal(6, guardian.Power);
        Assert.Equal(1, guardian.BoonCount);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Boon], guardian.Tags);
        Assert.True(guardian.IsExhausted);
    }
}

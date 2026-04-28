using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ConformanceFixtureRunnerTests
{
    [Fact]
    public async Task FixtureRunnerReplaysCommandLogAndChecksExpectedShape()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p1-placeholder-pass.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new PlaceholderRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        foreach (var (playerId, actions) in fixture.Expected.PromptActions)
        {
            Assert.Equal(actions, result.Prompts[playerId].Actions);
        }
    }

    [Fact]
    public void CanonicalJsonKeepsStableCamelCaseEnvelope()
    {
        var json = CanonicalJson.Serialize(new
        {
            FixtureId = "sample",
            FinalTick = 1,
            EventKinds = new[] { "PASS" }
        });

        Assert.Equal("""{"fixtureId":"sample","finalTick":1,"eventKinds":["PASS"]}""", json);
    }
}

using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class OptionalEntryRetainedVectorTests
{
    [Theory]
    [InlineData("p2-preflight-play-blast-crew-apprentice-optional-damage.fixture.json", true)]
    [InlineData("p2-preflight-play-blast-crew-apprentice-no-optional-damage.fixture.json", false)]
    [InlineData("p2-preflight-play-frostcoat-cub-optional-power-minus-two.fixture.json", true)]
    [InlineData("p2-preflight-play-frostcoat-cub-no-optional-power-minus-two.fixture.json", false)]
    public async Task EveryOriginalManualVectorCommandKeepsItsIdentityAndItsExplicitCurrentAcceptanceResult(string file, bool paid)
    {
        var retained = await ConformanceFixture.LoadAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", file), default);
        var retainedJson = JsonSerializer.Serialize(retained);
        // Observe the runner's exact initial state without changing its builder
        // or any original command. The independent run below uses that state.
        var capture = new InitialStateCapture();
        var originalRun = await ConformanceFixtureRunner.RunAsync(retained, capture, default);
        var initial = Assert.IsType<MatchState>(capture.Initial);
        Assert.Equal(retained.RoomId, initial.RoomId); Assert.Equal(retained.InitialState!.Seed, initial.Seed);
        var driver = new Driver(initial); Assert.Equal(3, retained.Commands.Count);
        var play = Assert.IsType<PlayCardCommand>(GameCommandJsonMapper.Map(retained.Commands[0].Cmd));
        for (var i = 0; i < retained.Commands.Count; i++) {
            await driver.ActRetained(retained.Commands[i], paid || i == 0);
            if (i != 0) continue;
            Assert.Contains(play.SourceObjectId, driver.State.PlayerZones["P1"].Base);
            Assert.Null(driver.State.PendingCardChoice);
            if (paid) {
                var item = Assert.Single(driver.State.StackItems); var target = Assert.Single(play.TargetObjectIds);
                Assert.Equal(play.TargetObjectIds, item.TargetObjectIds);
                Assert.Equal(driver.State.CardObjects[target].ObjectGeneration, item.TargetGenerations![target]);
                var kinds = driver.Events().Select(e => e.Event.Kind).ToArray();
                Assert.Equal(new[] { "CARD_PLAYED", "COST_PAID", "PERMANENT_CONFIRMED", "UNIT_PLAYED_TO_BASE", "TRIGGER_QUEUED", "TRIGGER_CONFIRMED" }, kinds);
            } else {
                Assert.Empty(driver.State.StackItems);
                Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "TRIGGER_QUEUED");
            }
        }
        Assert.Equal(paid ? new[] { true, true, true } : [true, false, false], driver.Commands().Select(c => c.Accepted));
        Assert.Equal(retained.Commands.Select(c => c.ClientIntentId), driver.Commands().Select(c => c.ClientIntentId));
        Assert.Equal(retained.Commands.Select(c => CanonicalJson.Serialize(c.Cmd)), driver.Commands().Select(c => CanonicalJson.Serialize(c.RawCommand!.Value)));
        Assert.Equal(MatchStateHasher.Hash(originalRun.FinalState), MatchStateHasher.Hash(driver.State));
        Assert.Empty(ConformanceFixtureRunner.CompareExpected(OptionalEntryFixtureExpectations.ForCurrentRules(retained), originalRun));
        Assert.Equal(retainedJson, JsonSerializer.Serialize(retained)); await driver.Replay();
        if (Environment.GetEnvironmentVariable("RIFTBOUND_OPTIONAL_ENTRY_EVIDENCE") is { Length: > 0 } root) {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, file + ".original-journal.json"), JsonSerializer.Serialize(new {
                initial, final = driver.State, commands = driver.Commands(), events = driver.Events()
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
    }

    private sealed class InitialStateCapture : IRuleEngine
    {
        private readonly CoreRuleEngine engine = new();
        internal MatchState? Initial { get; private set; }
        public ValueTask<ResolutionResult> ResolveAsync(MatchState state, PlayerIntent intent, GameCommand command, CancellationToken cancellationToken)
        {
            Initial ??= state;
            return engine.ResolveAsync(state, intent, command, cancellationToken);
        }
    }
}

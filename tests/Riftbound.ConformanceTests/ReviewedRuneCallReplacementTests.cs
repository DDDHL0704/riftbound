using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Explicit midgame positions using the reviewed physical print. This exercises
// shared resource behavior, not the unsupported normal play of the unit itself.
public sealed class ReviewedRuneCallReplacementTests
{
    private const string Wyvern = "RAD·092/167";

    [Theory]
    [InlineData(0, 0, false, false, 0, true)]
    [InlineData(0, 2, false, false, 1, false)]
    [InlineData(1, 0, false, false, 0, true)]
    [InlineData(1, 1, false, false, 1, false)]
    [InlineData(1, 2, false, false, 2, false)]
    [InlineData(2, 2, false, false, 2, false)]
    [InlineData(2, 3, false, false, 3, false)]
    [InlineData(1, 2, true, false, 1, false)]
    [InlineData(1, 2, false, true, 2, false)]
    public async Task MobilizeCallsOriginalAndEachApplicableExtraWithoutChangingItsFallback(
        int copies, int available, bool opposingController, bool exhaustedSource, int called, bool draws)
    {
        var fixture = await Load("p2-preflight-play-mobilize-call-rune.fixture.json");
        fixture = WithPosition(fixture, "P1", copies, available, opposingController, exhaustedSource);
        var result = await ConformanceFixtureRunner.RunAsync(fixture, new CoreRuleEngine(), default);
        Assert.Contains("STACK_ITEM_RESOLVED", result.EventKinds);
        Assert.Contains("P1-SPELL-MOBILIZE", result.FinalState.PlayerZones["P1"].Graveyard);
        var summoned = RuneIds(result.FinalState, "P1");
        Assert.Equal(called, summoned.Length);
        Assert.All(summoned, id => Assert.True(result.FinalState.CardObjects[id].IsExhausted));
        Assert.Equal(draws, result.EventKinds.Contains("CARD_DRAWN"));
        Assert.Equal(called, Convert.ToInt32(Assert.Single(result.Events, e => e.Kind == "RUNES_CALLED").Payload["count"]));
    }

    [Theory]
    [InlineData(1, 0, 0, true)]
    [InlineData(1, 1, 1, true)]
    [InlineData(1, 2, 2, false)]
    [InlineData(1, 3, 3, false)]
    [InlineData(2, 4, 4, false)]
    public async Task CatalystFallbackDependsOnTheTwoOriginalRunes(
        int copies, int available, int called, bool draws)
    {
        var fixture = await Load("p2-preflight-play-catalyst-of-aeons-draws-if-rune-call-short.fixture.json");
        fixture = WithPosition(fixture, "P1", copies, available, opposingController: false, exhaustedSource: false);
        var result = await ConformanceFixtureRunner.RunAsync(fixture, new CoreRuleEngine(), default);
        Assert.Contains("STACK_ITEM_RESOLVED", result.EventKinds);
        Assert.Contains("P1-SPELL-CATALYST", result.FinalState.PlayerZones["P1"].Graveyard);
        var summoned = RuneIds(result.FinalState, "P1");
        Assert.Equal(called, summoned.Length);
        Assert.All(summoned, id => Assert.True(result.FinalState.CardObjects[id].IsExhausted));
        Assert.Equal(draws, result.EventKinds.Contains("CARD_DRAWN"));
    }

    [Fact]
    public async Task TwoWyvernsAtNormalChannelKeepTheTwoOriginalRunesReadyAndExtrasExhausted()
    {
        var fixture = await Load("p2-preflight-turn-start.fixture.json");
        fixture = WithPosition(fixture, "P2", 2, 4, opposingController: false, exhaustedSource: false);
        var result = await ConformanceFixtureRunner.RunAsync(fixture, new CoreRuleEngine(), default);
        Assert.Equal(MatchPhases.Main, result.FinalState.Phase);
        var summoned = RuneIds(result.FinalState, "P2");
        Assert.Equal(4, summoned.Length);
        Assert.All(summoned.Take(2), id => Assert.False(result.FinalState.CardObjects.GetValueOrDefault(id)?.IsExhausted ?? false));
        Assert.All(summoned.Skip(2), id => Assert.True(result.FinalState.CardObjects[id].IsExhausted));
        Assert.Equal(4, Convert.ToInt32(Assert.Single(result.Events, e => e.Kind == "RUNES_CALLED").Payload["count"]));
    }

    private static async Task<ConformanceFixture> Load(string name) => await ConformanceFixture.LoadAsync(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name), default);

    private static ConformanceFixture WithPosition(ConformanceFixture fixture, string actor, int copies,
        int available, bool opposingController, bool exhaustedSource)
    {
        var initial = fixture.InitialState!;
        var players = initial.Players!.ToDictionary();
        var cards = (initial.CardObjects ?? new Dictionary<string, ConformanceCardObjectState>()).ToDictionary();
        var ids = Enumerable.Range(1, available).Select(i => $"{actor}-TEST-RUNE-{i}").ToArray();
        players[actor] = players[actor] with { RuneDeck = ids };
        var controller = opposingController ? (actor == "P1" ? "P2" : "P1") : actor;
        for (var i = 0; i < copies; i++)
        {
            var id = "WYVERN-" + i;
            var printed = OfficialGarenConquestBattlefieldCountTests.Printed(id, Wyvern, controller);
            cards[id] = new ConformanceCardObjectState { CardNo = printed.CardNo, OwnerId = printed.OwnerId,
                ControllerId = printed.ControllerId, Power = printed.Power, ManaCost = printed.ManaCost,
                Tags = printed.Tags, IsExhausted = exhaustedSource };
            players[controller] = players[controller] with { Base = (players[controller].Base ?? []).Append(id).ToArray() };
        }
        return fixture with { InitialState = initial with { Players = players, CardObjects = cards } };
    }

    private static string[] RuneIds(MatchState state, string player) => state.PlayerZones[player].Base
        .Where(id => id.StartsWith(player + "-TEST-RUNE-", StringComparison.Ordinal)).ToArray();
}

using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadResourceObserverUnitTests
{
    private const string Cluster = "RAD·016/167";
    private static MatchState Position(int field, int deck, bool battlefield = false)
    {
        var state = EndDeckPlayTests.RunePosition(Cluster, battlefield);
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        cards["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", Cluster, "P1");
        var runes = Enumerable.Range(1, field + deck).Select(i => "RESET_RUNE_" + i).ToArray();
        foreach (var id in EndDeckPlayTests.RuneIds.Concat(state.PlayerZones["P1"].RuneDeck)) { cards.Remove(id); locations.Remove(id); }
        foreach (var (id, index) in runes.Select((id, index) => (id, index))) {
            cards[id] = new(id, isExhausted: index % 2 == 0, cardNo: "OGN·007/298", ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.RuneCard, "COLOR:red"]);
            locations[id] = new("P1", index < field ? "BASE" : "RUNE_DECK");
        }
        return state with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Base = state.PlayerZones["P1"].Base.Except(EndDeckPlayTests.RuneIds).Concat(runes.Take(field)).ToArray(),
                RuneDeck = runes.Skip(field).ToArray() } } };
    }

    private static async Task Order(Driver game, IReadOnlyList<string> runes)
    {
        foreach (var id in runes.Take(Math.Max(0, runes.Count - 1))) {
            var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
            Assert.Equal("RUNE_RECYCLE_ORDER", choice.Request.Kind);
            await game.Act(choice.Request.PlayerId, new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [id]));
        }
        Assert.Null(game.State.PendingRuleChoice);
    }

    [Theory]
    [InlineData(0, 0, false)] [InlineData(0, 2, true)] [InlineData(1, 0, false)]
    [InlineData(5, 0, true)] [InlineData(5, 2, false)] [InlineData(7, 0, false)]
    public async Task EndStepRecyclesAllRunesInOwnerOrderThenCallsAsManyAsPossible(int field, int deck, bool battlefield)
    {
        var initial = Position(field, deck, battlefield); var game = new Driver(initial);
        var recycled = Enumerable.Range(1, field).Select(i => "RESET_RUNE_" + i).Reverse().ToArray();
        var oldDeck = initial.PlayerZones["P1"].RuneDeck;
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal("P1", game.State.TurnPlayerId); Assert.Null(game.State.PendingCardChoice);
        await game.Top();
        if (field > 1) {
            Assert.Equal(initial.PlayerZones["P1"].RuneDeck, game.State.PlayerZones["P1"].RuneDeck);
            Assert.Equal("P1", game.State.TurnPlayerId);
            await Order(game, recycled);
        }
        var combined = oldDeck.Concat(recycled).ToArray(); var called = combined.Take(4).ToArray();
        Assert.Equal("P2", game.State.TurnPlayerId);
        Assert.Equal(combined.Skip(4), game.State.PlayerZones["P1"].RuneDeck);
        Assert.All(called, id => { Assert.Contains(id, game.State.PlayerZones["P1"].Base); Assert.True(game.State.CardObjects[id].IsExhausted);
            Assert.Equal(initial.CardObjects[id].ObjectGeneration + (recycled.Contains(id) ? 2 : 1), game.State.CardObjects[id].ObjectGeneration); });
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "RUNE_RECYCLED");
        var calls = game.Events().Where(e => e.Event.Kind == "RUNES_CALLED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST")).ToArray();
        Assert.Equal(called.Length == 0 ? 0 : 1, calls.Length);
        if (called.Length > 0) Assert.Equal(called.Length, calls[0].Event.Payload["count"]);
        await game.Replay();
    }

    [Fact]
    public async Task RuneOrderingRejectsForeignMissingAndRepeatedChoicesWithoutCommittingTheInstruction()
    {
        var game = new Driver(Position(5, 0)); await game.Act("P1", new EndTurnCommand()); await game.Top();
        var request = game.State.PendingRuleChoice!.Request;
        foreach (var (player, id) in new[] { ("P2", "RESET_RUNE_1"), ("P1", "missing"), ("P1", "HOST") }) {
            var command = new PayCostCommand(request.Id, "RULE_REPLACEMENT", [id]);
            var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("invalid-rune-order", player, command.CmdType), command, default);
            Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
            Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(rejected.State));
        }
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["RESET_RUNE_5"]));
        Assert.DoesNotContain(game.State.PendingRuleChoice!.Request.Options, o => o.Id == "RESET_RUNE_5");
        await Order(game, ["RESET_RUNE_4", "RESET_RUNE_3", "RESET_RUNE_2", "RESET_RUNE_1"]);
        await game.Replay();
    }

    [Fact]
    public async Task RemovingTheSourceDuringResponseDoesNotCancelItsCapturedRuneInstruction()
    {
        var initial = OfficialCopyIdentityTests.AddSpell(Position(1, 0), "RETURN", "OGN·104/298");
        var game = new Driver(initial); await game.Act("P1", new EndTurnCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.Top(); Assert.Equal("P2", game.State.TurnPlayerId);
        Assert.True(game.State.CardObjects["RESET_RUNE_1"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task OfficialClusterIdentityIncludesThePrintedPowerCost()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Cluster);
        Assert.Equal(39506, card.Id); Assert.Equal(4, card.Energy); Assert.Equal(4, card.Power); Assert.Equal(2, card.ReturnEnergy);
    }
}

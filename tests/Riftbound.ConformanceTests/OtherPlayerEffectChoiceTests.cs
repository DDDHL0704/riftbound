using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class OtherPlayerEffectChoiceTests
{
    private static MatchState Position(string actor, bool runes = true)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "PORTAL", "OGN·071/298", actor, "HAND");
        if (runes)
            foreach (var player in state.Seats.Keys)
                for (var i = 0; i < 2; i++) state = RadGearTestPosition.Put(state, player + "-R" + i,
                    "OGN·007/298", player, "RUNE_DECK");
        return state with { TurnPlayerId = actor, ActivePlayerId = actor };
    }

    [Theory]
    [InlineData("P1", "CARDS")] [InlineData("P1", "RUNES")]
    [InlineData("P2", "CARDS")] [InlineData("P2", "RUNES")]
    public async Task TheOtherPlayerChoosesDuringResolutionAndBothPlayersReceiveTheChosenEffect(string actor, string selected)
    {
        var game = new Driver(Position(actor));
        await game.Act(actor, new PlayCardCommand("PORTAL", "OGN·071/298", []));
        Assert.Null(game.State.PendingRuleChoice);
        await game.Top();
        var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        var other = actor == "P1" ? "P2" : "P1";
        Assert.Equal(other, request.PlayerId);
        Assert.Equal("OTHER_PLAYER_DRAW_OR_RUNE", request.Kind);
        Assert.Equal(new[] { "CARDS", "RUNES" }, request.Options.Select(option => option.Id));
        Assert.True(game.PromptFor(other).Actionable);
        Assert.False(game.PromptFor(actor).Actionable);
        await RadGearTestPosition.Reject(game.State, actor, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [selected]));
        await RadGearTestPosition.Reject(game.State, other, new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["INVALID"]));
        Assert.All(game.State.PlayerZones.Values, zones => Assert.Empty(zones.Hand));
        await game.Act(other, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [selected]));
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Contains("PORTAL", game.State.PlayerZones[actor].Graveyard);
        foreach (var player in game.State.Seats.Keys) {
            var zones = game.State.PlayerZones[player];
            Assert.Equal(selected == "CARDS" ? 1 : 0, zones.Hand.Count);
            Assert.Equal(selected == "RUNES" ? 1 : 2, zones.RuneDeck.Count);
            if (selected == "RUNES") {
                Assert.Contains(player + "-R0", zones.Base);
                Assert.True(game.State.CardObjects[player + "-R0"].IsExhausted);
            }
        }
        Assert.Single(game.Events(), entry => entry.Event.Kind == "OTHER_PLAYER_EFFECT_CHOICE"
            && Equals(entry.Event.Payload.GetValueOrDefault("playerId"), other));
        await game.Replay();
    }

    [Theory]
    [InlineData("OTHER_PLAYERS_CHOOSE_CARDS")] [InlineData("OTHER_PLAYERS_CHOOSE_RUNES")]
    public async Task TheCasterCannotPreselectAnOtherPlayersDecisionAsAPlayMode(string mode)
    {
        var state = Position("P1");
        await RadGearTestPosition.Reject(state, "P1", new PlayCardCommand("PORTAL", "OGN·071/298", [], mode));
    }

    [Fact]
    public async Task CounteringTheSpellNeverAsksTheOtherPlayerToChooseAnEffect()
    {
        var state = RadGearTestPosition.Put(Position("P1"), "COUNTER", "OGN·064/298", "P2", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("PORTAL", "OGN·071/298", []));
        var portal = Assert.Single(game.State.StackItems).StackItemId;
        if (game.State.PriorityPlayerId != "P2")
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [portal]));
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Contains("PORTAL", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("COUNTER", game.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind is "CARD_DRAWN" or "RUNES_CALLED"
            or "RULE_CHOICE_REQUESTED" or "OTHER_PLAYER_EFFECT_CHOICE");
        await game.Replay();
    }

    [Fact]
    public async Task EmptyRuneDecksDoNotTakeAwayThePrintedRuneChoiceOrTurnItIntoADraw()
    {
        var state = Position("P1", runes: false);
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("PORTAL", "OGN·071/298", []));
        await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        await game.Act("P2", new PayCostCommand(choice.Id, "RULE_REPLACEMENT", ["RUNES"]));
        Assert.All(game.State.PlayerZones.Values, zones => Assert.Empty(zones.Hand));
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        Assert.All(game.Events().Where(entry => entry.Event.Kind == "RUNES_CALLED"),
            entry => Assert.Equal(0, entry.Event.Payload["count"]));
        await game.Replay();
    }
}

using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RemembranceAltarDeathTests
{
    private const string Altar = "SFD·169/221", Kill = "OGN·229/298";
    private static MatchState Position(bool exhausted = false, string victimController = "P1", bool stolenAltar = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "ALTAR", Altar, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "VICTIM", "OGN·219/298", victimController, "BASE");
        state = RadGearTestPosition.Put(state, "KILL", Kill, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "KEEP", "OGN·023/298", "P1", "HAND");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALTAR"] = state.CardObjects["ALTAR"] with { IsExhausted = exhausted, OwnerId = stolenAltar ? "P2" : "P1" } } };
    }
    private static async Task Destroy(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("KILL", Kill, ["VICTIM"])); await game.Top();
        if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(
            OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }
    private static Task Pay(Driver game, bool accept)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Contains("ALTAR", choice.LegalObjectIds);
        return game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept ? ["ALTAR"] : []));
    }
    private static Task Choose(Driver game, string selected)
    {
        var request = game.State.PendingRuleChoice!.Request;
        return game.Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [selected]));
    }
    private static async Task Put(Driver game, string card, string position)
    {
        var request = game.State.PendingRuleChoice!.Request;
        Assert.Equal("HAND_TO_DECK_CARD", request.Kind);
        await Choose(game, request.Options.Single(option => option.ObjectIds!.Contains(card)).Id);
        Assert.Equal("HAND_TO_DECK_POSITION", game.State.PendingRuleChoice!.Request.Kind);
        await Choose(game, position);
    }

    [Theory]
    [InlineData("TOP", false)] [InlineData("BOTTOM", false)] [InlineData("TOP", true)] [InlineData("BOTTOM", true)]
    public async Task AnAcceptedFriendlyDeathPaysExhaustBeforeResponsesThenDrawsAndPrivatelyPutsAnyHandCardAtEitherEnd(string position, bool drawnCard)
    {
        var game = new Driver(Position()); var beforeDeck = game.State.PlayerZones["P1"].MainDeck.ToArray();
        await Destroy(game); Assert.False(game.State.CardObjects["ALTAR"].IsExhausted);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        await Pay(game, true); Assert.True(game.State.CardObjects["ALTAR"].IsExhausted);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN"); await game.Top();
        var put = drawnCard ? beforeDeck[0] : "KEEP";
        var visible = CoreSnapshot(game.State, "P1");
        Assert.Contains(beforeDeck[0], visible);
        var opponent = CoreSnapshot(game.State, "P2") + JsonSerializer.Serialize(game.PromptFor("P2"));
        Assert.DoesNotContain("KEEP", opponent); Assert.DoesNotContain(beforeDeck[0], opponent);
        await Put(game, put, position);
        var remaining = beforeDeck.Skip(1);
        Assert.Equal(position == "TOP" ? new[] { put }.Concat(remaining) : remaining.Append(put), game.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(put, game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind is "CARDS_RECYCLED" or "CARD_DISCARDED");
        var placed = Assert.Single(game.Events(), entry => entry.Event.Kind == "HAND_CARD_PUT_IN_MAIN_DECK").Event;
        Assert.DoesNotContain(put, JsonSerializer.Serialize(placed.Payload)); Assert.DoesNotContain(position, JsonSerializer.Serialize(placed.Payload));
        Assert.Null(game.State.PendingRuleChoice); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    private static string CoreSnapshot(MatchState state, string player)
        => JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state)[player]);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BurnoutCompletesTheInstructionBeforeNormalVictoryButImmediateVictoryStopsPrivatePlacement(bool immediate)
    {
        var initial = RadGearTestPosition.Put(Position(), "KILL", Kill, "P2", "HAND");
        var deck = initial.PlayerZones["P1"].MainDeck.ToArray();
        initial = initial with {
            TurnPlayerId = "P2", ActivePlayerId = "P2",
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { MainDeck = [],
                    Banished = [..initial.PlayerZones["P1"].Banished, ..deck] } },
            ObjectLocations = initial.ObjectLocations.ToDictionary(pair => pair.Key, pair => deck.Contains(pair.Key)
                ? new ObjectLocationState("P1", "BANISHED") : pair.Value),
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["VICTIM"] = initial.CardObjects["VICTIM"] with { OwnerId = immediate ? "P2" : "P1" } },
            PlayerScores = new Dictionary<string, int>(initial.PlayerScores) { ["P2"] = 7 } };
        Assert.Empty(initial.PlayerZones["P1"].Graveyard);
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("KILL", Kill, ["VICTIM"])); await game.Top();
        if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(
            OrderedTriggerIds: game.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
        await Pay(game, true); await game.Top();
        if (!immediate) {
            Assert.Equal(MatchStatuses.InProgress, game.State.Status);
            Assert.Equal(8, CoreRuleEngine.VisibleRuleChoiceState(game.State).PlayerScores["P2"]);
            Assert.NotNull(game.State.PendingRuleChoice); await Put(game, "KEEP", "TOP");
        }
        Assert.Equal(MatchStatuses.Finished, game.State.Status); Assert.Equal("P2", game.State.WinnerPlayerId);
        Assert.Null(game.State.PendingRuleChoice); Assert.Empty(game.State.StackItems);
        Assert.Equal(immediate ? 0 : 1, game.Events().Count(entry => entry.Event.Kind == "HAND_CARD_PUT_IN_MAIN_DECK"));
        if (immediate) Assert.Contains("KEEP", game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DecliningOrAlreadyExhaustedCannotDrawOrPutAHandCard(bool exhausted)
    {
        var game = new Driver(Position(exhausted)); var deck = game.State.PlayerZones["P1"].MainDeck.ToArray();
        await Destroy(game); if (!exhausted) await Pay(game, false);
        Assert.Equal(exhausted, game.State.CardObjects["ALTAR"].IsExhausted);
        Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck); Assert.Contains("KEEP", game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.PendingRuleChoice); Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN"); await game.Replay();
    }

    [Theory]
    [InlineData("P1", false)] [InlineData("P1", true)] [InlineData("P2", false)]
    public async Task FriendshipUsesTheCurrentControllerAndTheAltarDoesNotObserveAnEnemyDeath(string victim, bool stolen)
    {
        var game = new Driver(Position(victimController: victim, stolenAltar: stolen)); await Destroy(game);
        if (victim == "P1") {
            Assert.Equal(stolen ? "P2" : "P1", game.State.CardObjects["ALTAR"].OwnerId);
            await Pay(game, true); await game.Top(); await Put(game, "KEEP", "BOTTOM");
        } else {
            Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
            Assert.False(game.State.CardObjects["ALTAR"].IsExhausted);
        }
        await game.Replay();
    }

    [Fact]
    public async Task SeveralDeathsInOneBatchCannotPayTheSameAltarTwice()
    {
        var initial = RadGearTestPosition.Put(Position(), "RUIN", "UNL-180/219", "P1", "HAND");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("RUIN", "UNL-180/219", [])); await game.Top();
        if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(
            OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        await Pay(game, true); Assert.Single(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        await game.Top(); await Put(game, "KEEP", "TOP");
        Assert.Single(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN"); await game.Replay();
    }

    [Fact]
    public async Task WrongActorCardAndPositionAreAtomicRejectionsAndForgedDecisionHistoryCannotRecover()
    {
        var game = new Driver(Position()); await Destroy(game); await Pay(game, true); await game.Top();
        var pending = game.State.PendingRuleChoice!; var request = pending.Request;
        foreach (var (player, option) in new[] { ("P2", request.Options[0].Id), ("P1", "TOP"), ("P1", "VICTIM") })
            await RadGearTestPosition.Reject(game.State, player, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]));
        var forged = game.State with { PendingRuleChoice = pending with { Request = request with {
            Options = [new("HAND:0", "伪造手牌", ["VICTIM"])] } } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await Choose(game, request.Options.Single(option => option.ObjectIds!.Contains("KEEP")).Id);
        request = game.State.PendingRuleChoice!.Request;
        await RadGearTestPosition.Reject(game.State, "P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["KEEP"]));
        await Choose(game, "TOP"); await game.Replay();
    }

    [Fact]
    public async Task ThePaidDrawAndPrivatePlacementSurviveTheAnimatedAltarReturningInResponse()
    {
        var initial = RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "LEGEND", "RAD·145/167", "P1", "BASE");
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with {
                    Base = initial.PlayerZones["P1"].Base.Where(id => id != "LEGEND").ToArray(), LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND") } };
        initial = OriannaEquipmentConsumerFixture.WithResources(initial);
        initial = RadGearTestPosition.Put(initial, "EXTRA-ORANGE", "UNL-R04", "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "EXTRA-PURPLE", "UNL-R05", "P1", "BASE");
        var game = new Driver(initial);
        await OriannaEquipmentConsumerFixture.AnimateAtNextOwnMain(game, "ALTAR");
        await game.Act("P1", new RecycleRuneCommand("EXTRA-ORANGE", ["EXTRA-ORANGE", "EXTRA-PURPLE"]));
        await Destroy(game); await Pay(game, true);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALTAR"])); await game.Top();
        Assert.Contains("ALTAR", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("ALTAR", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        var draws = game.Events().Count(entry => entry.Event.Kind == "CARD_DRAWN");
        await game.Top(); await Put(game, "ALTAR", "TOP");
        Assert.Equal("ALTAR", game.State.PlayerZones["P1"].MainDeck[0]);
        Assert.Equal(draws + 1, game.Events().Count(entry => entry.Event.Kind == "CARD_DRAWN"));
        Assert.Empty(game.State.StackItems); await game.Replay();
    }
}

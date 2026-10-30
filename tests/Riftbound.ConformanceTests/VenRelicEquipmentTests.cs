using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenRelicEquipmentTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PlayingRelicBurnsAfterResponsesAndOnlyABurnedUnitCreatesASeparateMightSkill(bool unit)
    {
        var game = new Driver(Position(unit)); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·108", []));
        Assert.Equal(15, game.State.RunePools["P1"].Mana); Assert.Single(game.State.StackItems);
        Assert.Equal(game.State.CardObjects["UNIT"].ObjectGeneration, Assert.Single(game.State.StackItems).UnitEvent!.SourceGeneration);
        Assert.Equal(3, game.State.PlayerZones["P1"].MainDeck.Count); Assert.Null(game.State.PendingCardChoice);
        await game.Top(); Assert.Equal(2, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("P1-DRAW-1", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
        if (unit) {
            var choice = game.State.PendingCardChoice!; Assert.Equal(new[] { "ALLY" }, choice.LegalObjectIds);
            await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
            await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
            await Choose(game); Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
            await game.Top(); Assert.Equal(8, game.State.CardObjects["ALLY"].Power);
            await game.Act("P1", new EndTurnCommand()); Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
        } else { Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); }
        await game.Replay();
    }

    [Fact]
    public async Task EachControllersStartPhaseBurnsBeforeTheNormalDrawAndTheOpponentStartDoesNotTriggerRelic()
    {
        var game = new Driver(Position(true)); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·108", []));
        await game.Top(); await Choose(game); await game.Top();
        await game.Act("P1", new EndTurnCommand()); Assert.Empty(game.State.StackItems);
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal(MatchPhases.TurnStart, game.State.Phase); Assert.Equal("SCORE", game.State.TurnStartStep);
        Assert.Equal(2, game.State.PlayerZones["P1"].MainDeck.Count); Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Top(); Assert.Contains("P1-DRAW-2", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await Choose(game); await game.Top();
        Assert.Equal(MatchPhases.Main, game.State.Phase); Assert.Contains("P1-DRAW-3", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(8, game.State.CardObjects["ALLY"].Power); await game.Replay();
    }

    [Fact]
    public async Task BurnUsesTheSharedBurnoutReplacementAndStillCreatesTheFollowupForTheRecycledUnit()
    {
        var initial = Position(true); var zones = initial.PlayerZones.ToDictionary();
        var locations = initial.ObjectLocations.ToDictionary(); var deck = zones["P1"].MainDeck;
        zones["P1"] = zones["P1"] with { MainDeck = [], Banished = deck };
        foreach (var id in deck) locations[id] = new("P1", "BANISHED");
        var game = new Driver(initial with { PlayerZones = zones, ObjectLocations = locations });
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·108", [])); await game.Top();
        Assert.Equal(1, game.State.PlayerScores["P2"]); Assert.Contains("GRAVE", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, game.State.CardObjects["GRAVE"].ObjectGeneration);
        Assert.Equal("GRAVE", Assert.Single(game.State.StackItems).UnitEvent!.BurnedUnit!.Binding.ObjectId);
        await Choose(game); await game.Top(); Assert.Equal(8, game.State.CardObjects["ALLY"].Power); await game.Replay();
    }

    [Fact]
    public async Task AnEnemyPrefectBanishingRelicDoesNotEraseItsCapturedBurnOrTheIndependentFollowup()
    {
        var initial = HandDiscardObserverTests.Put(Position(true), "PREFECT", "VEN·102", "P2", "BASE");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·108", []));
        foreach (var player in new[] { "P1", "P2" }) {
            var candidate = game.PromptFor(player).Candidates!.SingleOrDefault(c => c.Action == CommandTypes.OrderTriggers && c.Enabled);
            if (candidate is null) continue;
            var ids = JsonSerializer.SerializeToElement(candidate.Metadata).GetProperty("orderedTriggerIds").Deserialize<string[]>()!;
            await game.Act(player, new OrderTriggersCommand(OrderedTriggerIds: ids));
        }
        var cost = game.State.PendingCardChoice!; Assert.Equal("P2", cost.PlayerId);
        await game.Act("P2", new ChooseCardsCommand(cost.ChoiceId, cost.ChoiceWindow, ["PREFECT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Banished);
        await game.Top(); Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Graveyard);
        await Choose(game); await game.Top(); Assert.Equal(8, game.State.CardObjects["ALLY"].Power); await game.Replay();
    }

    [Fact]
    public async Task ATargetReturnedInResponseReceivesNoMightAndRelicCannotBePlayedWithoutItsPrintedCost()
    {
        var initial = HandDiscardObserverTests.Put(Position(true), "RETURN", "OGN·104/298", "P1", "HAND");
        await Reject(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = RunePool.Empty } },
            "P1", new PlayCardCommand("UNIT", "VEN·108", []));
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·108", []));
        await game.Top(); await Choose(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top(); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand); Assert.Equal(0, game.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifier);
        await game.Replay();
    }

    private static Task Choose(Driver game)
    {
        var choice = game.State.PendingCardChoice!;
        return game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
    }
    private static MatchState Position(bool unit)
    {
        var initial = VenUnitEntryBasicTests.Position("VEN·108"); var cards = initial.CardObjects.ToDictionary();
        if (!unit) cards["P1-DRAW-1"] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-DRAW-1", "UNL-085/219", "P1");
        return initial with { CardObjects = cards };
    }
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-relic", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}

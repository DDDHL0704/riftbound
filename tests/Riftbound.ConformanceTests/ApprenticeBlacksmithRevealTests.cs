using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ApprenticeBlacksmithRevealTests
{
    internal const string Blacksmith = "SFD·041/221";

    [Fact]
    public async Task ActualPlayThenReadyAndMoveQueuesAnIndependentEquipmentRevealSkill()
    {
        var state = RadUnitEffectTestFixture.Place(Position("SFD·022/221", 2), "SMITH", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "READY", "UNL-009/219", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("SMITH", Blacksmith, [], Destination: "BASE"));
        Assert.True(game.State.CardObjects["SMITH"].IsExhausted);
        await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["SMITH"])); await game.Top();
        await Move(game);
        Assert.Equal(TriggerKinds.UnitMovedRevealTopDrawEquipmentOrRecycle, Assert.Single(game.State.StackItems).MovementObserver!.Kind);
        Assert.DoesNotContain("P1-D0", game.State.PlayerZones["P1"].Hand);
        await game.Top(); Assert.Contains("P1-D0", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Theory]
    [InlineData("SFD·022/221", 1, true)] [InlineData("SFD·022/221", 2, true)]
    [InlineData("OGN·219/298", 2, false)] [InlineData("OGN·104/298", 2, false)]
    [InlineData("OGN·219/298", 0, false)]
    public async Task RevealsOnlyTheActualTopDrawsEquipmentAndOtherwiseRecyclesWithoutBurnout(string top, int count, bool draws)
    {
        var initial = Position(top, count); var before = initial.PlayerZones["P1"].MainDeck.ToArray();
        var hand = initial.PlayerZones["P1"].Hand.ToArray(); var game = new Driver(initial);
        await Move(game); Assert.Equal(before, game.State.PlayerZones["P1"].MainDeck);
        await game.Top();
        Assert.Equal(draws ? hand.Append(before[0]) : hand, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(draws ? before.Skip(1) : count > 0 ? before.Skip(1).Append(before[0]) : before,
            game.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "BURNOUT_APPLIED" or "CARDS_MILLED");
        Assert.Equal(draws ? 1 : 0, game.Events().Count(e => e.Event.Kind == "CARD_DRAWN"));
        Assert.Equal(count == 0 ? 0 : 1, game.Events().Count(e => e.Event.Kind == "CARDS_REVEALED"));
        if (count > 1) Assert.DoesNotContain("P1-D1", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RevealedNocturneMayBanishButNeverMakesTheNextCardAnEquipmentDraw(bool banish)
    {
        var game = new Driver(Position("OGN·194/298", 2)); await Move(game); await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("DECK_OBSERVATION_BANISH", choice.Request.Kind);
        await game.Act("P1", new PayCostCommand(choice.Request.Id, CoreRuleEngine.RuleChoiceWindow, [banish ? "BANISH" : "KEEP"]));
        Assert.DoesNotContain("P1-D1", game.State.PlayerZones["P1"].Hand);
        if (banish) {
            var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
            Assert.Equal(Blacksmith, pending.Parent.ObservedPlayPermission!.Context.Instruction.Parent.CardNo);
            await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
            Assert.Contains("P1-D0", game.State.PlayerZones["P1"].Banished);
        } else Assert.Equal(new[] { "P1-D1", "P1-D0" }, game.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DRAWN"); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSmithInResponseKeepsTheIndependentRevealAndHidesItsCurrentPrivateSource()
    {
        var state = RadGearTestPosition.Put(Position("SFD·022/221", 2), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(state); await Move(game);
        var generation = Assert.Single(game.State.StackItems).MovementObserver!.SourceGeneration;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["SMITH"])); await game.Top();
        Assert.Equal("HAND", game.State.ObjectLocations["SMITH"].Zone);
        Assert.True(game.State.CardObjects["SMITH"].ObjectGeneration > generation);
        var row = Assert.IsType<Dictionary<string, object?>>(Assert.Single(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        Assert.Equal("HIDDEN", row["sourceObjectId"]); Assert.Equal(Blacksmith, row["cardNo"]);
        await game.Top(); Assert.Contains("P1-D0", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    internal static MatchState Position(string top, int count)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·278/298"), "SMITH", Blacksmith, "P1", "BASE");
        var old = state.PlayerZones["P1"].MainDeck.ToArray(); var kept = old.Take(count).ToArray();
        var removed = old.Skip(count).ToHashSet(StringComparer.Ordinal);
        var cards = state.CardObjects.Where(p => !removed.Contains(p.Key)).ToDictionary();
        if (count > 0) cards[kept[0]] = OfficialGarenConquestBattlefieldCountTests.Printed(kept[0], top, "P1");
        return state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { MainDeck = kept } }, CardObjects = cards,
            ObjectLocations = state.ObjectLocations.Where(p => !removed.Contains(p.Key)).ToDictionary() };
    }
    internal static Task Move(Driver game) => game.Act("P1", new MoveUnitCommand("SMITH", "BASE", "BATTLEFIELD:BF", []));
}

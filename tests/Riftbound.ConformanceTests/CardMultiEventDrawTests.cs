using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class CardMultiEventDrawTests
{
    private const string Scrap = "OGN·182/298";
    private const string Timing = "PLAY_OR_SELF_DISCARDED_OR_DESTROYED";
    private static bool IsDraw(StackItemState item) => item.UnitEvent is { CardNo: Scrap, Timing: Timing };
    private static MatchState Position(string zone = "HAND")
    {
        var state = VenUnitEntryBasicTests.Position(Scrap);
        return zone == "HAND" ? state : RadUnitEffectTestFixture.Place(state, "UNIT", "P1", zone);
    }

    [Fact]
    public async Task PlayingTheEquipmentCreatesExactlyOneIndependentDrawAfterEntry()
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", Scrap, []));
        Assert.Equal(18, game.State.RunePools["P1"].Mana);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.True(IsDraw(Assert.Single(game.State.StackItems)));
        await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }

    [Fact]
    public async Task ASpellAdditionalCostDiscardsTheCardNowAndDrawsBeforeTheParentSpellResolves()
    {
        var initial = RadGearTestPosition.Put(Position(), "SPELL", "VEN·008", "P1", "HAND");
        initial = RadUnitEffectTestFixture.Place(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SPELL", "VEN·008", ["ENEMY"], OptionalCosts: ["DISCARD_HAND_CARD:UNIT"]));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains(game.State.StackItems, IsDraw);
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].Hand);
        Assert.Equal("SPELL", Assert.Single(game.State.StackItems).SourceObjectId);
        await game.Top();
        Assert.Single(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }

    [Fact]
    public async Task ARequiredEmpowerDiscardAlsoCreatesTheDrawBeforeTheEmpowerEffect()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "PORO", "VEN·007", "P1", "BASE"));
        await game.Act("P1", new ActivateAbilityCommand("PORO", "EMPOWER:0:0", [], OptionalCosts: ["DISCARD_HAND_CARD:UNIT"]));
        Assert.False(game.State.CardObjects["PORO"].IsEmpowered);
        Assert.Contains(game.State.StackItems, IsDraw);
        await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].Hand);
        Assert.False(game.State.CardObjects["PORO"].IsEmpowered);
        await game.Top();
        Assert.True(game.State.CardObjects["PORO"].IsEmpowered);
        await game.Replay();
    }

    [Fact]
    public async Task AnEffectDiscardFinishesTheParentInstructionBeforeTheEarnedDraw()
    {
        var initial = RadGearTestPosition.Put(Position(), "SPELL", "OGN·008/298", "P1", "HAND");
        initial = RadUnitEffectTestFixture.Place(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·008/298", ["UNIT", "ENEMY"]));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(game.State.StackItems, IsDraw);
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, game.State.CardObjects["ENEMY"].Damage);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.True(IsDraw(Assert.Single(game.State.StackItems)));
        await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task DestructionDrawsForTheCapturedControllerRegardlessOfEquipmentOwnership(string owner)
    {
        var initial = RadGearTestPosition.Put(Position("BASE"), "DESTROY", "VEN·003", "P1", "HAND");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { OwnerId = owner } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("DESTROY", "VEN·003", ["UNIT"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones[owner].Graveyard);
        var ability = Assert.Single(game.State.StackItems);
        Assert.True(IsDraw(ability)); Assert.Equal("P1", ability.ControllerId);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.PlayerZones["P2"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningEquipmentDoesNotCountAsDestructionButARealReplayTriggersAgain()
    {
        var game = new Driver(RadGearTestPosition.Put(Position("BASE"), "RETURN", "SFD·135/221", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("RETURN", "SFD·135/221", ["UNIT"]));
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        await game.Act("P1", new PlayCardCommand("UNIT", Scrap, []));
        Assert.True(IsDraw(Assert.Single(game.State.StackItems)));
        await game.Top();
        Assert.Single(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SimultaneousDiscardsOrDestructionsCreateOneDrawForEachPhysicalCard(bool destruction)
    {
        var initial = RadGearTestPosition.Create("OGN·278/298");
        initial = RadGearTestPosition.Put(initial, "SCRAP1", Scrap, "P1", destruction ? "BASE" : "HAND");
        initial = RadGearTestPosition.Put(initial, "SCRAP2", Scrap, "P1", destruction ? "BASE" : "HAND");
        var no = destruction ? "OGN·022/298" : "OGN·201/298";
        initial = RadGearTestPosition.Put(initial, "PARENT", no, "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("PARENT", no, []));
        await game.Top();
        Assert.Equal(2, game.State.TriggerQueue.Count(trigger => trigger.UnitEvent?.CardNo == Scrap));
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
        await game.Top(); await game.Top();
        Assert.Equal(destruction ? 2 : 6, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(2, game.Events().Count(entry => entry.Event.Kind == "TRIGGER_RESOLVED"
            && entry.Event.Payload.GetValueOrDefault("effectKind") is string kind && kind == CoreRuleEngine.UnitEventEffect));
        await game.Replay();
    }

    [Fact]
    public async Task DiscardingAndRedrawingTheCardDuringBurnoutDoesNotEraseTheEarnedDraw()
    {
        var initial = RadGearTestPosition.Put(Position(), "PARENT", "OGN·201/298", "P1", "HAND");
        initial = RadUnitEffectTestFixture.Place(initial, "GRAVE", "P1", "BANISHED");
        initial = RadGearTestPosition.Put(initial, "P2-EXTRA-1", "OGN·219/298", "P2", "MAIN_DECK");
        initial = RadGearTestPosition.Put(initial, "P2-EXTRA-2", "OGN·219/298", "P2", "MAIN_DECK");
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { MainDeck = [], Graveyard = initial.PlayerZones["P1"].MainDeck } },
            ObjectLocations = initial.ObjectLocations.ToDictionary(pair => pair.Key,
                pair => initial.PlayerZones["P1"].MainDeck.Contains(pair.Key) ? new ObjectLocationState("P1", "GRAVEYARD") : pair.Value) };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("PARENT", "OGN·201/298", []));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.True(IsDraw(Assert.Single(game.State.StackItems)));
        Assert.Equal(4, game.State.PlayerZones["P1"].Hand.Count);
        await game.Top();
        Assert.Equal(5, game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    [Fact]
    public async Task AnOrdinaryEquipmentDoesNotBorrowTheCardsDiscardOrDestructionDraw()
    {
        var initial = RadGearTestPosition.Put(Position("BASE"), "DESTROY", "VEN·003", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("DESTROY", "VEN·003", ["GEAR"]));
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }
}

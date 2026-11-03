using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenLinkedInstructionFaqTests
{
    private const string Blade = "OGN·213/298";

    private static MatchState Position() => LocalDestructionRecallTests.At(
        VenUnitEntryBasicTests.Position(Blade), "ENEMY", "P2", "BATTLEFIELD", "OTHER");

    // CN VEN FAQ p. 22 example 2; core 359.3.e.14.a.
    [Fact]
    public async Task FlashMakesTheBladesTargetIllegalAndSkipsTheLinkedControllerDraw()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "FLASH", "OGS·011/024", "P2");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Blade, ["ENEMY"]));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("FLASH", "OGS·011/024", ["ENEMY"])); await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        Assert.Equal(initial.PlayerZones["P2"].MainDeck, game.State.PlayerZones["P2"].MainDeck);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "UNIT_DESTROYED" or "CARD_DRAWN");
        await game.Replay();
    }

    // CN VEN FAQ p. 22 example 3; a cancelled destruction is not an ignored instruction.
    [Fact]
    public async Task HourglassRecallStillLeavesTheControllerAvailableForTheBladesLinkedDraw()
    {
        var initial = HandDiscardObserverTests.Put(Position(), "HOURGLASS", "OGN·077/298", "P2", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { Damage = 2 } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Blade, ["ENEMY"])); await game.Top();
        Assert.Contains("HOURGLASS", game.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        Assert.True(game.State.CardObjects["ENEMY"].IsExhausted); Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(new[] { "P2-DRAW-1", "P2-DRAW-2" }, game.State.PlayerZones["P2"].Hand);
        Assert.Equal(new[] { "P2-DRAW-3" }, game.State.PlayerZones["P2"].MainDeck);
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "CARD_DRAWN"));
        await game.Replay();
    }

    // CN VEN FAQ p. 22 example 4: Smite's separate effect removes the reference.
    [Fact]
    public async Task SmiteBanishmentMakesTheControllerUnavailableForTheBladesLinkedDraw()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "SMITE", "UNL-007/219", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SMITE", "UNL-007/219", ["ENEMY"])); await game.Top();
        Assert.Equal(3, game.State.CardObjects["ENEMY"].Damage);
        await game.Act("P1", new PlayCardCommand("UNIT", Blade, ["ENEMY"])); await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Banished);
        Assert.DoesNotContain("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(initial.PlayerZones["P2"].MainDeck, game.State.PlayerZones["P2"].MainDeck);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }
}

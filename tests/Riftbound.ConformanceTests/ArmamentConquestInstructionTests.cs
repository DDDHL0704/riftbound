using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN 383.2/383.4, 436, 718.3 and 719: the lower box belongs to the host.
public sealed class ArmamentConquestInstructionTests
{
    private const string Boon = "SFD·108/221", Rune = "SFD·118/221", Loot = "SFD·124/221", Gold = "SFD·134/221";
    private static MatchState Position(string gear, bool attached = true)
    {
        var s = RadGearTestPosition.Put(RadGearTestPosition.Create(), "GEAR", gear, "P1", "BASE");
        s = RadGearTestPosition.Put(s, "RETURN", "OGN·104/298", "P1", "HAND");
        for (var i = 0; i < 3; i++) s = RadGearTestPosition.Put(s, "RUNE" + i, "OGN·007/298", "P1", "RUNE_DECK");
        return attached ? s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["GEAR"] = s.CardObjects["GEAR"] with { AttachedToObjectId = "ALLY" } } } : s;
    }

    private static async Task Conquer(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
            game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }

    private static async Task Resolve(Driver game)
    {
        await game.Top();
        if (game.State.PendingRuleChoice is { } pending)
        {
            Assert.Equal("PRIVATE_HAND_DISCARD", pending.Request.Kind);
            var selected = pending.Request.Options.First();
            await game.Act("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [selected.Id]));
        }
    }

    private static CardObjectState[] GoldTokens(MatchState s)
        => s.CardObjects.Values.Where(c => c.TokenFactoryCardNo is { } n
            && P6TokenFactoryCatalog.TryGetByCardNo(n, out var f) && f.TokenFamilyName == "金币").ToArray();

    [Theory]
    [InlineData(Boon)] [InlineData(Rune)] [InlineData("SFD·118a/221·P")] [InlineData(Loot)] [InlineData(Gold)]
    public async Task EachPrintedLowerBoxEarnsItsOwnIndependentInstructionOnActualConquest(string gear)
    {
        var game = new Driver(Position(gear)); await Conquer(game);
        Assert.Equal(gear, Assert.Single(game.State.StackItems).UnitEvent!.CardNo);
        Assert.Equal(0, BoonRules.Count(game.State.CardObjects["ALLY"])); Assert.Empty(GoldTokens(game.State));
        var deck = game.State.PlayerZones["P1"].MainDeck.Count;
        var runes = game.State.PlayerZones["P1"].RuneDeck.Count;
        var nextRune = game.State.PlayerZones["P1"].RuneDeck[0];
        await Resolve(game);
        if (gear == Boon) Assert.Equal(1, BoonRules.Count(game.State.CardObjects["ALLY"]));
        else if (gear == Gold) Assert.True(Assert.Single(GoldTokens(game.State)).IsExhausted);
        else if (gear == Loot)
        {
            Assert.Contains("RETURN", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(deck - 1, game.State.PlayerZones["P1"].MainDeck.Count);
            Assert.Single(game.State.PlayerZones["P1"].Hand);
        }
        else
        {
            Assert.Equal(runes - 1, game.State.PlayerZones["P1"].RuneDeck.Count);
            Assert.Contains(nextRune, game.State.PlayerZones["P1"].Base);
            Assert.True(game.State.CardObjects[nextRune].IsExhausted);
        }
        await game.Replay();
    }

    [Theory]
    [InlineData(Boon)] [InlineData(Rune)] [InlineData(Loot)] [InlineData(Gold)]
    public async Task AnUnattachedWeaponDoesNotGiveAConquerorTheLowerBox(string gear)
    {
        var game = new Driver(Position(gear, false)); await Conquer(game);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); await game.Replay();
    }

    [Theory]
    [InlineData(Boon)] [InlineData(Rune)] [InlineData(Loot)] [InlineData(Gold)]
    public async Task ReturningTheHostDoesNotEraseAnAlreadyEarnedGrantedInstruction(string gear)
    {
        var game = new Driver(Position(gear)); await Conquer(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        var deck = game.State.PlayerZones["P1"].MainDeck.Count;
        var runes = game.State.PlayerZones["P1"].RuneDeck.Count;
        await Resolve(game);
        if (gear == Boon) Assert.Equal(0, BoonRules.Count(game.State.CardObjects["ALLY"]));
        else if (gear == Gold) Assert.Single(GoldTokens(game.State));
        else if (gear == Rune) Assert.Equal(runes - 1, game.State.PlayerZones["P1"].RuneDeck.Count);
        else Assert.Equal(deck - 1, game.State.PlayerZones["P1"].MainDeck.Count);
        await game.Replay();
    }

    [Fact]
    public async Task ActualOrangeAssemblyLetsTheHostEarnItsConquestBoon()
    {
        var game = new Driver(Position(Boon, false));
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "ALLY", ["ASSEMBLE_ORANGE"])); await game.Top();
        await Conquer(game); await Resolve(game);
        Assert.Equal(1, BoonRules.Count(game.State.CardObjects["ALLY"])); await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task RecoveryRequiresTheOriginalCapturedConquestGrantAndController(bool erase)
    {
        var game = new Driver(Position(Gold)); await Conquer(game);
        var skill = Assert.Single(game.State.StackItems);
        var forged = game.State with { StackItems = erase ? [] : [skill with { ControllerId = "P2" }] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Replay();
    }
}

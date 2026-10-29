using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.DeathObserverAuditTests;
using static Riftbound.ConformanceTests.OfficialSettReplacementTests;

namespace Riftbound.ConformanceTests;

public sealed class VenNasusLocalDeathRuneTests
{
    private static MatchState Position(string no, bool two = false, string heroController = "P1", bool otherField = false)
    {
        var state = NoReplacement(two);
        var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        var zones = state.PlayerZones.ToDictionary();
        cards["HERO"] = OfficialGarenConquestBattlefieldCountTests.Printed("HERO", no, heroController);
        var heroField = otherField ? "BF2" : "BF";
        if (otherField)
        {
            cards["BF2"] = new("BF2", cardNo: "OGN·297/298", ownerId: "P1", controllerId: "P1", tags: [P6TokenFactoryCatalog.BattlefieldCardTag]);
            locations["BF2"] = new("P1", "BATTLEFIELD", "BF2");
            zones["P1"] = zones["P1"] with { Battlefields = [..zones["P1"].Battlefields, "BF2"] };
        }
        if (heroController == "P1" && !otherField)
        {
            locations["HERO"] = new("P1", "BASE");
            zones["P1"] = zones["P1"] with { Base = [..zones["P1"].Base, "HERO"] };
        }
        else
        {
            locations["HERO"] = new(heroController, "BATTLEFIELD", heroField);
            zones[heroController] = zones[heroController] with { Battlefields = [..zones[heroController].Battlefields, "HERO"] };
        }
        locations["AOE"] = new("P1", "HAND");
        var runes = new[] { "RUNE-1", "RUNE-2", "RUNE-3" };
        foreach (var id in runes)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R06", "P1");
            locations[id] = new("P1", "RUNE_DECK");
        }
        zones["P1"] = zones["P1"] with { RuneDeck = runes };
        return state with { RoomId = "VEN-NASUS-DEATH-" + no, CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }

    private static async Task DestroyWithAoe(Driver game, string no = "OGN·133/298")
    {
        if (game.State.PlayerZones["P1"].Base.Contains("HERO"))
            await game.Act("P1", new MoveUnitCommand("HERO", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PlayCardCommand("AOE", no, []));
        await game.Top();
    }

    [Theory]
    [InlineData("VEN·063", 37754)] [InlineData("VEN·063a", 37755)] [InlineData("VEN·178", 37881)]
    public async Task EnemyDestroyedHereQueuesAnIndependentRuneCall(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("亘古守知", card.SubTitle); Assert.Equal(5, card.Energy); Assert.Equal(6, card.Power);
        var spec = Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Kind == TriggerKinds.UnitEnemyDestroyedHereOncePerTurnCallRune);
        Assert.True(spec.OncePerTurn); Assert.Equal(1, spec.RuneCallCount);
        var game = new Driver(Position(no));
        await DestroyWithAoe(game);
        var ability = Assert.Single(game.State.StackItems, item => item.DeathObserver?.Kind == TriggerKinds.UnitEnemyDestroyedHereOncePerTurnCallRune);
        Assert.Equal("P1", ability.ControllerId); Assert.Equal("P2", ability.DeathObserver!.Destroyed.ControllerId);
        Assert.Contains("RUNE-1", game.State.PlayerZones["P1"].RuneDeck);
        await game.Top();
        Assert.Contains("RUNE-1", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["RUNE-1"].IsExhausted);
        Assert.Equal(2, game.State.PlayerZones["P1"].RuneDeck.Count);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·063")] [InlineData("VEN·063a")] [InlineData("VEN·178")]
    public async Task SimultaneousEnemyDeathsEarnOnlyOneRuneCall(string no)
    {
        var game = new Driver(Position(no, two: true));
        await DestroyWithAoe(game);
        Assert.Equal(2, game.State.PlayerZones["P2"].Graveyard.Count(id => id is "D" or "D2"));
        Assert.Single(game.State.StackItems, i => i.DeathObserver?.Kind == TriggerKinds.UnitEnemyDestroyedHereOncePerTurnCallRune);
        await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].Base, id => id.StartsWith("RUNE-", StringComparison.Ordinal));
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·063", false)] [InlineData("VEN·063a", false)] [InlineData("VEN·178", false)]
    [InlineData("VEN·063", true)] [InlineData("VEN·063a", true)] [InlineData("VEN·178", true)]
    public async Task SecondEnemyDeathSameTurnCannotTriggerAgain(string no, bool waitUntilFirstResolves)
    {
        var state = Position(no, two: true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["D2"] = OfficialGarenConquestBattlefieldCountTests.Printed("D2", "OGN·012/298", "P2"),
                ["AOE2"] = state.CardObjects["AOE"] with { ObjectId = "AOE2" } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = ["AOE", "AOE2"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["AOE2"] = new("P1", "HAND") } };
        var game = new Driver(state); await DestroyWithAoe(game);
        Assert.Contains("D", game.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("D2", game.State.PlayerZones["P2"].Battlefields);
        if (waitUntilFirstResolves) await game.Top();
        if (game.State.PriorityPlayerId is { } priority && priority != "P1") await game.Act(priority, new PassPriorityCommand());
        if (game.State.PriorityPlayerId is null && game.State.FocusPlayerId is { } focus && focus != "P1")
            await game.Act(focus, new PassFocusCommand());
        await game.Act("P1", new PlayCardCommand("AOE2", "OGN·133/298", [])); await game.Top();
        Assert.Contains("D2", game.State.PlayerZones["P2"].Graveyard);
        if (waitUntilFirstResolves)
            Assert.DoesNotContain(game.State.StackItems, i => i.DeathObserver?.Kind == TriggerKinds.UnitEnemyDestroyedHereOncePerTurnCallRune);
        else
        {
            Assert.Single(game.State.StackItems, i => i.DeathObserver?.Kind == TriggerKinds.UnitEnemyDestroyedHereOncePerTurnCallRune);
            await game.Top();
        }
        Assert.Single(game.State.PlayerZones["P1"].Base, id => id.StartsWith("RUNE-", StringComparison.Ordinal));
        await game.Replay();
    }

    [Fact]
    public async Task TheSameSourceCanTriggerAgainOnTheNextTurn()
    {
        var state = Position("VEN·063", two: true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = state.CardObjects["LEGEND"] with { CardNo = "OGN·247/298" },
                ["D2"] = state.CardObjects["D2"] with { Damage = 0 },
                ["AOE2"] = new("AOE2", cardNo: "OGN·133/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]),
                ["PAY2"] = OfficialGarenConquestBattlefieldCountTests.Printed("PAY2", "UNL-R06", "P2") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P2"] = state.PlayerZones["P2"] with { Base = ["D2", "PAY2"], Battlefields = ["BF", "D"], Hand = ["SPELL", "AOE2"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["D2"] = new("P2", "BASE"), ["AOE2"] = new("P2", "HAND"), ["PAY2"] = new("P2", "BASE") } };
        var game = new Driver(state); await DestroyWithAoe(game); await game.Top();
        Assert.Contains("RUNE-1", game.State.PlayerZones["P1"].Base);
        await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        var originalTurn = game.State.TurnNumber;
        await game.Act("P1", new EndTurnCommand());
        Assert.True(game.State.TurnNumber > originalTurn);
        await game.Act("P2", new TapRuneCommand("PAY2"));
        await game.Act("P2", new MoveUnitCommand("D2", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P2", new PlayCardCommand("AOE2", "OGN·133/298", [])); await game.Top();
        Assert.Single(game.State.StackItems, i => i.DeathObserver?.Kind == TriggerKinds.UnitEnemyDestroyedHereOncePerTurnCallRune);
        await game.Top(); Assert.Contains("RUNE-2", game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·063")] [InlineData("VEN·063a")] [InlineData("VEN·178")]
    public async Task EarnedAbilityStillCallsRuneAfterTheSourceReturnsToHand(string no)
    {
        var state = Position(no);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["RETURN"] = new("RETURN", cardNo: "OGN·104/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = ["AOE", "RETURN"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["RETURN"] = new("P1", "HAND") } };
        var game = new Driver(state); await DestroyWithAoe(game);
        if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HERO"])); await game.Top();
        Assert.Contains("HERO", game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.State.StackItems, i => i.DeathObserver?.Kind == TriggerKinds.UnitEnemyDestroyedHereOncePerTurnCallRune);
        await game.Top();
        Assert.Equal(2, game.State.PlayerZones["P1"].Base.Count(id => id.StartsWith("RUNE-", StringComparison.Ordinal)));
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·063")] [InlineData("VEN·063a")] [InlineData("VEN·178")]
    public async Task ObserverDestroyedInSameActionDoesNotTrigger(string no)
    {
        var state = Position(no);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HERO"] = state.CardObjects["HERO"] with { Damage = 5 } } };
        var game = new Driver(state); await DestroyWithAoe(game);
        Assert.Contains("HERO", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("D", game.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(game.State.StackItems, i => i.DeathObserver?.Kind == TriggerKinds.UnitEnemyDestroyedHereOncePerTurnCallRune);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·063")] [InlineData("VEN·063a")] [InlineData("VEN·178")]
    public async Task EnemyStatusUsesControllerEvenWhenTheDestroyedUnitIsOwnedByTheObserver(string no)
    {
        var state = Position(no);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["D"] = state.CardObjects["D"] with { OwnerId = "P1" } } };
        var game = new Driver(state); await DestroyWithAoe(game);
        var trigger = Assert.Single(game.State.StackItems, i => i.DeathObserver?.Kind == TriggerKinds.UnitEnemyDestroyedHereOncePerTurnCallRune);
        Assert.Equal("P1", trigger.DeathObserver!.Destroyed.OwnerId); Assert.Equal("P2", trigger.DeathObserver.Destroyed.ControllerId);
        await game.Top(); Assert.Contains("RUNE-1", game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsForgedFriendlyOrDifferentLocationDeath()
    {
        var game = new Driver(Position("VEN·063")); await DestroyWithAoe(game);
        var original = Assert.Single(game.State.StackItems, i => i.DeathObserver is not null);
        var context = original.DeathObserver!;
        foreach (var forged in new[] {
            context with { Destroyed = context.Destroyed with { ControllerId = "P1" } },
            context with { Destroyed = context.Destroyed with { ControllerId = "" } },
            context with { Destroyed = context.Destroyed with { ControllerId = "FORGED" } },
            context with { DestroyedLocation = context.DestroyedLocation! with { BattlefieldObjectId = "FORGED" } },
            context with { SourceLocation = null } })
        {
            var altered = game.State with { StackItems = game.State.StackItems.Select(item => item == original
                ? item with { DeathObserver = forged } : item).ToArray() };
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(altered));
        }
        await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·063")] [InlineData("VEN·063a")] [InlineData("VEN·178")]
    public async Task EnemyDeathAtAnotherBattlefieldDoesNotTrigger(string no)
    {
        var game = new Driver(Position(no, otherField: true)); await DestroyWithAoe(game);
        Assert.Contains("D", game.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(game.State.StackItems, i => i.DeathObserver?.Kind == TriggerKinds.UnitEnemyDestroyedHereOncePerTurnCallRune);
        Assert.Equal(3, game.State.PlayerZones["P1"].RuneDeck.Count); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·063")] [InlineData("VEN·063a")] [InlineData("VEN·178")]
    public async Task FriendlyUnitDeathDoesNotTrigger(string no)
    {
        var game = new Driver(Position(no, heroController: "P2")); await DestroyWithAoe(game);
        Assert.Contains("D", game.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(game.State.StackItems, i => i.DeathObserver?.Kind == TriggerKinds.UnitEnemyDestroyedHereOncePerTurnCallRune);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·063")] [InlineData("VEN·063a")] [InlineData("VEN·178")]
    public async Task EmptyRuneDeckStillResolvesWithoutInventingARune(string no)
    {
        var state = Position(no);
        var runes = state.PlayerZones["P1"].RuneDeck;
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { RuneDeck = [] } },
            CardObjects = state.CardObjects.Where(p => !runes.Contains(p.Key)).ToDictionary(),
            ObjectLocations = state.ObjectLocations.Where(p => !runes.Contains(p.Key)).ToDictionary() };
        var game = new Driver(state); await DestroyWithAoe(game); await game.Top();
        Assert.Empty(game.State.PlayerZones["P1"].RuneDeck);
        Assert.DoesNotContain(game.State.PlayerZones["P1"].Base, id => id.StartsWith("RUNE-", StringComparison.Ordinal));
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·063")] [InlineData("VEN·063a")] [InlineData("VEN·178")]
    public async Task ActualHeroPlayRequiresPaymentAndCreatesThePrintedUnit(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(5, 1, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }), ["P2"] = RunePool.Empty } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); Assert.Equal(6, game.State.CardObjects["UNIT"].Power);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Blue)); await game.Replay();
        var unpaid = state with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(4, 1), ["P2"] = RunePool.Empty } };
        var command = new PlayCardCommand("UNIT", no, [], Destination: "BASE");
        var rejected = await new CoreRuleEngine().ResolveAsync(unpaid, new("unpaid", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(unpaid), MatchStateHasher.Hash(rejected.State));
        var wrongColor = state with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(5, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }), ["P2"] = RunePool.Empty } };
        rejected = await new CoreRuleEngine().ResolveAsync(wrongColor, new("wrong-color", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(wrongColor), MatchStateHasher.Hash(rejected.State));
    }
}

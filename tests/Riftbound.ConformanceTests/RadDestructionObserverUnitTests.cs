using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadDestructionObserverUnitTests
{
    private const string Bird = "RAD·036/167";
    private const string Order = "RAD·131/167";

    [Theory]
    [InlineData(Bird, 39529, 6, 6, 0)] [InlineData(Order, 39636, 3, 3, 1)]
    public async Task OfficialPrintsPayTheirCostsAndRejectUnaffordableEntry(string no, long id, int mana, int power, int rune)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy); Assert.Equal(power, card.Power);
        var state = VenUnitEntryBasicTests.Position(no);
        var command = new PlayCardCommand("UNIT", no, [], Destination: "BASE");
        var poor = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } };
        var rejected = await new CoreRuleEngine().ResolveAsync(poor, new("poor-observer", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(poor), MatchStateHasher.Hash(rejected.State));
        var game = new Driver(state); await game.Act("P1", command);
        Assert.Equal(20 - mana, game.State.RunePools["P1"].Mana); Assert.Equal(20 - rune, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(power, game.State.CardObjects["UNIT"].Power); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    private static MatchState BirdPosition(bool stunned, bool local, bool friendly)
    {
        var state = VenUnitEntryBasicTests.Position("OGN·219/298");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", Bird, "P1"),
            ["ENEMY"] = state.CardObjects["ENEMY"] with { Power = 1, UntilEndOfTurnEffects = stunned ? ["STUNNED"] : [] },
            ["BF"] = state.CardObjects["BF"] with { ControllerId = local && !friendly ? "P2" : "P1" } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = ["GEAR", "ALLY"], Battlefields = friendly ? ["BF", "ENEMY"] : ["BF"] },
                ["P2"] = state.PlayerZones["P2"] with { Base = [], Battlefields = friendly ? ["OTHER"] : ["OTHER", "ENEMY"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["ALLY"] = new("P1", "BASE"),
                ["ENEMY"] = new(friendly ? "P1" : "P2", "BATTLEFIELD", local ? "BF" : "OTHER") } };
        if (friendly) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { ControllerId = "P1" } } };
        return AttackDamageDistributionTests.Spell(state, "DESTROY", "OGN·009/298", "P1");
    }

    [Theory]
    [InlineData(true, true, false, true)] [InlineData(false, true, false, false)]
    [InlineData(true, false, false, false)] [InlineData(true, true, true, false)]
    public async Task BirdCapturesOnlyStunnedEnemiesDestroyedAtItsLocation(bool stunned, bool local, bool friendly, bool triggers)
    {
        var game = new Driver(BirdPosition(stunned, local, friendly));
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF"));
        await game.Act("P1", new PlayCardCommand("DESTROY", "OGN·009/298", ["ENEMY"])); await game.Top();
        Assert.Equal(triggers, game.State.StackItems.Any(i => i.DeathObserver is not null));
        Assert.DoesNotContain("P1-DRAW-1", game.State.PlayerZones["P1"].Hand);
        if (triggers) { await game.Top(); Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand); }
        await game.Replay();
    }

    [Fact]
    public async Task BirdDrawsForEachStunnedEnemyDestroyedInTheSameTurn()
    {
        var state = BirdPosition(true, true, false);
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY2"] = state.CardObjects["ENEMY"] with { ObjectId = "ENEMY2" } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P2"] = state.PlayerZones["P2"] with { Battlefields = [..state.PlayerZones["P2"].Battlefields, "ENEMY2"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["ENEMY2"] = new("P2", "BATTLEFIELD", "BF") } };
        state = AttackDamageDistributionTests.Spell(state, "DESTROY2", "OGN·009/298", "P1");
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF"));
        foreach (var (spell, target) in new[] { ("DESTROY", "ENEMY"), ("DESTROY2", "ENEMY2") })
        {
            if (game.State.PriorityPlayerId is { } priority && priority != "P1") await game.Act(priority, new PassPriorityCommand());
            if (game.State.PriorityPlayerId is null && game.State.FocusPlayerId is { } focus && focus != "P1")
                await game.Act(focus, new PassFocusCommand());
            await game.Act("P1", new PlayCardCommand(spell, "OGN·009/298", [target])); await game.Top();
            var item = Assert.Single(game.State.StackItems, i => i.DeathObserver is not null);
            Assert.True(item.DeathObserver!.Destroyed.WasStunned);
            var forged = game.State with { StackItems = game.State.StackItems.Select(i => i.StackItemId == item.StackItemId
                ? i with { DeathObserver = i.DeathObserver! with { Destroyed = i.DeathObserver.Destroyed with { WasStunned = false } } } : i).ToArray() };
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
            await game.Top();
        }
        Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand);
        Assert.Contains("P1-DRAW-2", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    private static MatchState OrderPosition(bool two = false, bool observerDies = false)
    {
        var state = DeathObserverAuditTests.Source(DeathObserverAuditTests.NoReplacement(two), Order);
        if (observerDies) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["OBSERVER"] = state.CardObjects["OBSERVER"] with { Power = 1 },
            ["AOE"] = state.CardObjects["AOE"] with { CardNo = "UNL-180/219" } } };
        return state;
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OrderGrantsOneBoonForOneSimultaneousDestructionBatch(bool two)
    {
        var game = new Driver(OrderPosition(two));
        await game.Act("P1", new PlayCardCommand("AOE", "OGN·133/298", [])); await game.Top();
        Assert.Single(game.State.StackItems, i => i.DeathObserver is not null);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Contains("OBSERVER", choice.LegalObjectIds); Assert.DoesNotContain("D", choice.LegalObjectIds);
        var power = game.State.CardObjects["OBSERVER"].Power;
        var wrong = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["D"]);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("dead-target", "P2", wrong.CmdType), wrong, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(rejected.State));
        await game.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["OBSERVER"]));
        Assert.Equal(power, game.State.CardObjects["OBSERVER"].Power); await game.Top();
        Assert.Equal(power + 1, game.State.CardObjects["OBSERVER"].Power); Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["OBSERVER"].Tags);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task AnOrderDestroyedInTheSameActionDoesNotObserveTheOtherDeaths()
    {
        var game = new Driver(OrderPosition(true, true));
        await game.Act("P1", new PlayCardCommand("AOE", "UNL-180/219", [])); await game.Top();
        Assert.Contains("OBSERVER", game.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(game.State.StackItems, i => i.DeathObserver?.CardNo == Order); await game.Replay();
    }

    [Fact]
    public async Task OrderDoesNotGrantBoonToANewIncarnationOfTheChosenUnit()
    {
        var game = new Driver(OrderPosition());
        await game.Act("P1", new PlayCardCommand("AOE", "OGN·133/298", [])); await game.Top();
        var choice = game.State.PendingCardChoice!;
        await game.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["OBSERVER"]));
        var target = game.State.CardObjects["OBSERVER"];
        var changed = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
            ["OBSERVER"] = target with { ObjectGeneration = target.ObjectGeneration + 1 } } };
        var done = await OfficialGraveyardRecastTests.Top(OfficialInsightAndSpellLockTests.Restore(changed));
        Assert.Equal(target.Power, done.State.CardObjects["OBSERVER"].Power);
        Assert.DoesNotContain(CardObjectTags.Boon, done.State.CardObjects["OBSERVER"].Tags);
        OfficialInsightAndSpellLockTests.Restore(done.State);
    }

    [Fact]
    public async Task OrderTargetConfirmationRejectsForgedChoicesAndTargetBindings()
    {
        var game = new Driver(OrderPosition());
        await game.Act("P1", new PlayCardCommand("AOE", "OGN·133/298", [])); await game.Top();
        var choice = game.State.PendingCardChoice!;
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with {
            PendingCardChoice = choice with { LegalObjectIds = ["D"] } }));
        await game.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["OBSERVER"]));
        var item = Assert.Single(game.State.StackItems);
        foreach (var forged in new[] {
            item with { TargetObjectIds = ["OBSERVER", "D"] },
            item with { TargetGenerations = new Dictionary<string, long> { ["OBSERVER"] = -1 } },
            item with { TargetGenerations = null } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task EachCopiedOrderTextObservesOneSimultaneousBatchIndependently()
    {
        var game = new Driver(CopiedDeathObserverTests.Position(Order, 1, true));
        await game.Act("P2", new AssembleEquipmentCommand("GEAR0", "HOST", ["ASSEMBLE_GREEN"])); await game.Top();
        Assert.Equal(Order, game.State.CardObjects["GEAR0"].AttachedRuleText!.CardNo);
        await game.Act("P2", new EndTurnCommand()); await game.Act("P1", new TapRuneCommand("PAY0"));
        await game.Act("P1", new PlayCardCommand("AOE", "OGN·133/298", [])); await game.Top();
        Assert.Equal(2, game.State.TriggerQueue.Count);
        Assert.Equal(2, game.State.DeathLedger.Counts["P2"]);
        await game.Act("P2", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        for (var i = 0; i < 2; i++)
        {
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            await game.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
        }
        Assert.Equal(2, game.State.StackItems.Count);
        var power = game.State.CardObjects["HOST"].Power;
        await game.Top(); await game.Top();
        Assert.Equal(power + 1, game.State.CardObjects["HOST"].Power);
        var grants = game.Events().Where(e => e.Event.Kind == "BOON_GRANTED").ToArray();
        Assert.Equal(2, grants.Length);
        Assert.Equal(new[] { false, true }, grants.Select(e => (bool)e.Event.Payload["alreadyHadBoon"]!));
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData(Bird, TriggerKinds.UnitStunnedEnemyDestroyedHereDraw)]
    [InlineData(Order, TriggerKinds.UnitFriendlyBatchDestroyedBoon)]
    public async Task OfficialChineseTextsProduceTheSharedDeathSpecs(string no, string kind)
    {
        var card = (await OfficialCardCatalog.LoadRuntimeAsync()).Cards.Single(c => c.CardNo == no);
        Assert.Equal(kind, Assert.Single(TriggerParser.Parse(card.CardEffect)).Kind);
    }
}

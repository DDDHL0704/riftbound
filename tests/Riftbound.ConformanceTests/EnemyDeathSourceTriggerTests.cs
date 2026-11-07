using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class EnemyDeathSourceTriggerTests
{
    private static MatchState Position(string cardNo, bool legend, string sourceZone = "BATTLEFIELD",
        string victimController = "P2", bool sourceExhausted = true)
    {
        var state = RadGearTestPosition.Create();
        if (legend)
        {
            Assert.True(PrintedCardFactory.TryCreate("OBSERVER", "P1", cardNo, out var source));
            state = state with {
                CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["OBSERVER"] = source },
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                    ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["OBSERVER"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                    ["OBSERVER"] = new("P1", "LEGEND") } };
        }
        else state = RadGearTestPosition.Put(state, "OBSERVER", cardNo, "P1", sourceZone,
            sourceZone == "BATTLEFIELD" ? "BF" : null);
        // Declared initial control/ownership boundary, not a simulated transfer.
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["OBSERVER"] = state.CardObjects["OBSERVER"] with { IsExhausted = sourceExhausted, OwnerId = legend ? "P1" : "P2" } } };
        state = RadGearTestPosition.Put(state, "ANCHOR", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        for (var i = 1; i <= 3; i++)
        {
            state = RadGearTestPosition.Put(state, "VICTIM" + i, "UNL-002/219", victimController,
                "BATTLEFIELD", victimController == "P1" ? "BF" : "OTHER");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["VICTIM" + i] = state.CardObjects["VICTIM" + i] with { OwnerId = victimController == "P1" ? "P2" : "P1" } } };
            state = RadGearTestPosition.Put(state, "KILL" + i, "UNL-159/219", "P1", "HAND");
        }
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "AOE", "OGN·133/298", "P1", "HAND");
        foreach (var player in new[] { "P1", "P2" })
            for (var i = 0; i < 4; i++)
                state = RadGearTestPosition.Put(state, player + "-DRAW" + i, "OGN·219/298", player, "MAIN_DECK");
        for (var i = 0; i < 4; i++)
            state = RadGearTestPosition.Put(state, "PAY" + i, "UNL-R06", "P1", "BASE");
        return state;
    }

    private static string[] Gold(MatchState state)
        => state.PlayerZones["P1"].Base.Where(id => state.CardObjects[id].Tags.Contains("金币")).ToArray();

    private static async Task Kill(Driver game, int number = 1)
    {
        await game.Act("P1", new PlayCardCommand("KILL" + number, "UNL-159/219", ["VICTIM" + number]));
        await game.Top();
        Assert.Contains("VICTIM" + number, game.State.PlayerZones["P1"].Graveyard.Concat(game.State.PlayerZones["P2"].Graveyard));
    }

    [Theory]
    [InlineData("当任意数量的敌方单位被摧毁时，让我变为活跃状态。", true)]
    [InlineData("当任意数量的友方单位被摧毁时，让我变为活跃状态。", false)]
    [InlineData("如果我位于基地，当任意数量的敌方单位被摧毁时，让我变为活跃状态。", false)]
    [InlineData("当任意数量的敌方单位被摧毁时，让我变为活跃状态，并抽一张牌。", false)]
    public void BatchReadyParsingRequiresTheWholeEnemyClause(string text, bool expected)
        => Assert.Equal(expected, TriggerParser.Parse(text).Any(spec => spec.Kind == TriggerKinds.SourceEnemyBatchDestroyedReady));

    [Theory]
    [InlineData("SFD·203/221", "P2", true)] [InlineData("SFD·250/221", "P2", true)]
    [InlineData("SFD·203/221", "P2", false)] [InlineData("SFD·250/221", "P2", false)]
    [InlineData("SFD·203/221", "P1", true)] [InlineData("SFD·250/221", "P1", true)]
    public async Task ACurrentEnemyDeathQueuesTheLegendSkillBeforeAnyReadying(
        string cardNo, string victimController, bool exhausted)
    {
        var game = new Driver(Position(cardNo, true, victimController: victimController, sourceExhausted: exhausted));
        await Kill(game);
        Assert.Equal(exhausted, game.State.CardObjects["OBSERVER"].IsExhausted);
        if (victimController == "P2")
        {
            var item = Assert.Single(game.State.StackItems);
            Assert.Equal(cardNo, item.DeathObserver!.CardNo);
            Assert.Equal(TriggerKinds.SourceEnemyBatchDestroyedReady, item.DeathObserver.Kind);
            Assert.Equal("P2", item.DeathObserver.Destroyed.ControllerId);
            await game.Top();
            Assert.False(game.State.CardObjects["OBSERVER"].IsExhausted);
        }
        else Assert.Empty(game.State.StackItems);
        Assert.Equal(victimController == "P2" && exhausted ? 1 : 0,
            game.Events().Count(e => e.Event.Kind == "LEGEND_READIED"));
        await game.Replay();
    }

    [Theory, InlineData("SFD·203/221"), InlineData("SFD·250/221")]
    public async Task SimultaneousEnemyDeathsQueueOneLegendSkillForTheWholeBatch(string cardNo)
    {
        var game = new Driver(Position(cardNo, true));
        await game.Act("P1", new PlayCardCommand("AOE", "OGN·133/298", []));
        await game.Top();
        Assert.All(new[] { "VICTIM1", "VICTIM2", "VICTIM3" }, id => Assert.Contains(id, game.State.PlayerZones["P1"].Graveyard));
        Assert.True(game.State.CardObjects["OBSERVER"].IsExhausted);
        Assert.Single(game.State.StackItems);
        await game.Top();
        Assert.False(game.State.CardObjects["OBSERVER"].IsExhausted);
        Assert.Single(game.Events(), e => e.Event.Kind == "LEGEND_READIED");
        await game.Replay();
    }

    [Theory]
    [InlineData("UNL-145/219", "BATTLEFIELD", "P2")] [InlineData("UNL-145a/219", "BATTLEFIELD", "P2")]
    [InlineData("UNL-145/219", "BASE", "P2")] [InlineData("UNL-145a/219", "BASE", "P2")]
    [InlineData("UNL-145/219", "BATTLEFIELD", "P1")] [InlineData("UNL-145a/219", "BATTLEFIELD", "P1")]
    public async Task PykeCapturesHisBattlefieldConditionAndTheDeadUnitsController(
        string cardNo, string zone, string victimController)
    {
        var game = new Driver(Position(cardNo, false, zone, victimController));
        await Kill(game);
        Assert.Empty(Gold(game.State));
        if (zone == "BATTLEFIELD" && victimController == "P2")
        {
            var item = Assert.Single(game.State.StackItems);
            Assert.Equal(TriggerKinds.SourceBattlefieldEnemyDestroyedOnceGold, item.DeathObserver!.Kind);
            Assert.Equal("BF", item.DeathObserver.SourceLocation!.BattlefieldObjectId);
            Assert.Null(item.DeathObserver.DestroyedLocation); // Anywhere, not only here.
            await game.Top();
            Assert.True(game.State.CardObjects[Assert.Single(Gold(game.State))].IsExhausted);
            Assert.DoesNotContain(game.State.PlayerZones["P2"].Base, id => game.State.CardObjects[id].Tags.Contains("金币"));
        }
        else Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory, InlineData("UNL-145/219"), InlineData("UNL-145a/219")]
    public async Task PykeCreatesTheEarnedGoldAfterARealSourceReturnToItsOwnersHand(string cardNo)
    {
        var game = new Driver(Position(cardNo, false));
        await Kill(game);
        var generation = Assert.Single(game.State.StackItems).DeathObserver!.SourceGeneration;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["OBSERVER"]));
        await game.Top();
        Assert.Contains("OBSERVER", game.State.PlayerZones["P2"].Hand);
        await game.Top();
        Assert.Single(Gold(game.State));
        Assert.True(game.State.CardObjects["OBSERVER"].ObjectGeneration > generation);
        // The source is owned by P2, so P2 must play the returned card on its
        // own turn. This test keeps that ownership boundary explicit.
        await game.Replay();
    }

    [Theory, InlineData("UNL-145/219"), InlineData("UNL-145a/219")]
    public async Task PykeUsesTheLimitOncePerTurnAndResetsItThroughActualTurnChanges(string cardNo)
    {
        var game = new Driver(Position(cardNo, false));
        await Kill(game); await game.Top(); Assert.Single(Gold(game.State));
        await Kill(game, 2); Assert.Empty(game.State.StackItems); Assert.Single(Gold(game.State));
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal("P1", game.State.TurnPlayerId);
        await game.TapRunes("P1", ["PAY0", "PAY1"]);
        await game.Act("P1", new RecycleRuneCommand("PAY2"));
        await Kill(game, 3); Assert.Single(game.State.StackItems); Assert.Single(Gold(game.State));
        await game.Top(); Assert.Equal(2, Gold(game.State).Length);
        await game.Replay();
    }

    [Theory, InlineData("UNL-145/219"), InlineData("UNL-145a/219")]
    public async Task PykeRecoveryRejectsAChangedEnemyRelationOrCapturedSourceZone(string cardNo)
    {
        var game = new Driver(Position(cardNo, false));
        await Kill(game);
        var item = Assert.Single(game.State.StackItems);
        var context = Assert.IsType<DeathObserverContext>(item.DeathObserver);
        Assert.True(CoreRuleEngine.ValidDeathObserver(game.State, context, item.EffectKind,
            item.ControllerId, item.SourceObjectId, item.CardNo));
        foreach (var changed in new[] {
            context with { Destroyed = context.Destroyed with { ControllerId = "P1" } },
            context with { SourceLocation = context.SourceLocation! with { Zone = "BASE", BattlefieldObjectId = null } },
            context with { SourceLocation = null },
            context with { SourceLocation = context.SourceLocation! with { PlayerId = "MISSING" } },
            context with { SourceGeneration = game.State.CardObjects["OBSERVER"].ObjectGeneration + 1 } })
            Assert.False(CoreRuleEngine.ValidDeathObserver(game.State, changed, item.EffectKind,
                item.ControllerId, item.SourceObjectId, item.CardNo));
        await game.Replay();
    }
    [Theory, InlineData("UNL-145/219"), InlineData("UNL-145a/219")]
    public async Task CopiedDeathTextHasAnIndependentOncePerTurnLimit(string cardNo)
    {
        var initial = RadGearTestPosition.Put(Position(cardNo, false), "GEAR", "SFD·059/221", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "OBSERVER", ["ASSEMBLE_GREEN"]));
        await game.Top();
        Assert.Equal(cardNo, game.State.CardObjects["GEAR"].AttachedRuleText!.CardNo);
        await Kill(game);
        var contexts = game.State.StackItems.Select(item => item.DeathObserver)
            .Concat(game.State.TriggerQueue.Select(item => item.DeathObserver)).Where(c => c is not null).Cast<DeathObserverContext>().ToArray();
        Assert.Equal(2, contexts.Length);
        Assert.Single(contexts, context => context.Origin is not null);
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(item => item.TriggerId).ToArray()));
        await game.Top(); await game.Top(); Assert.Equal(2, Gold(game.State).Length);
        await Kill(game, 2); Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.Equal(2, Gold(game.State).Length);
        await game.Replay();
    }

    [Theory, InlineData("UNL-145/219"), InlineData("UNL-145a/219")]
    public async Task ARealReturnAndReentryCreatesANewAbilityInstanceInTheSameTurn(string cardNo)
    {
        var initial = Position(cardNo, false);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["OBSERVER"] = initial.CardObjects["OBSERVER"] with { OwnerId = "P1" } } };
        var game = new Driver(initial);
        await Kill(game); await game.Top(); Assert.Single(Gold(game.State));
        var generation = game.State.CardObjects["OBSERVER"].ObjectGeneration;
        var turn = game.State.TurnNumber;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["OBSERVER"]));
        await game.Top(); Assert.Contains("OBSERVER", game.State.PlayerZones["P1"].Hand);
        await game.Act("P1", new PlayCardCommand("OBSERVER", cardNo, [], Destination: "BATTLEFIELD:BF"));
        Assert.True(game.State.CardObjects["OBSERVER"].ObjectGeneration > generation);
        Assert.Contains(CardObjectTags.Standby, game.State.CardObjects["OBSERVER"].Tags);
        Assert.False(game.State.CardObjects["OBSERVER"].IsFaceDown);
        Assert.Equal("BF", game.State.ObjectLocations["OBSERVER"].BattlefieldObjectId);
        Assert.Equal(turn, game.State.TurnNumber); Assert.Empty(game.State.StackItems);
        await Kill(game, 2); Assert.Single(game.State.StackItems); await game.Top();
        Assert.Equal(2, Gold(game.State).Length);
        await game.Replay();
    }

}

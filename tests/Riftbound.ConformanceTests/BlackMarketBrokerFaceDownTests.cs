using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BlackMarketBrokerFaceDownTests
{
    private const string Broker = "SFD·121/221", Unit = "OGN·135/298", Spell = "OGN·083/298", Equipment = "OGN·077/298";
    private const string Shadow = "BROKER-PRIVATE-SHADOW";
    private static MatchState Position(string card = Unit, string sourceZone = "BASE", bool stolen = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·275/298"), "BROKER", Broker,
            sourceZone == "enemy" ? "P2" : "P1", sourceZone == "enemy" ? "BASE" : sourceZone);
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, Shadow, card, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "OPENER", "OGN·048/298", "P2", "HAND");
        foreach (var player in new[] { "P1", "P2" })
            for (var i = 0; i < 8; i++) state = RadGearTestPosition.Put(state, player + "-R" + i, "UNL-R03", player, "BASE");
        return !stolen ? state : state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BROKER"] = state.CardObjects["BROKER"] with { OwnerId = "P2" } } };
    }

    private static string[] Gold(MatchState state, string player = "P1")
        => state.PlayerZones[player].Base.Where(id => state.CardObjects[id].TokenFactoryCardNo is { } no
            && P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory) && factory.TokenFamilyName == "金币").ToArray();
    private static IEnumerable<StackItemState> Rewards(MatchState state)
        => state.StackItems.Where(item => item.EffectKind == CoreRuleEngine.StandbyPlayedEquipmentTokenEffect);

    private static async Task HiddenNextTurn(Driver game, string card = Unit)
    {
        game.RecoverAfterEveryCommand = false;
        await game.Act("P1", new HideCardCommand(Shadow, card, "BATTLEFIELD:BF", ["STANDBY_A"]));
        Assert.Empty(Gold(game.State)); Assert.Empty(game.State.TriggerQueue); Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(Shadow, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        await game.Act("P1", new EndTurnCommand());
        await game.TapRunes("P2", ["P2-R0", "P2-R1", "P2-R2"]);
        await game.Act("P2", new RecycleRuneCommand("P2-R3"));
        await game.Act("P2", new PlayCardCommand("OPENER", "OGN·048/298", []));
        await game.Act("P2", new PassPriorityCommand()); Assert.Equal("P1", game.State.PriorityPlayerId);
        await game.ColdCheckpoint(); game.RecoverAfterEveryCommand = true;
    }
    private static Task FromFaceDown(Driver game, string card = Unit, bool reveal = false)
        => game.Act("P1", reveal ? new RevealCardCommand(Shadow, card, [], "STANDBY_REACTION", ["STANDBY_REVEAL_0"],
            card == Spell ? "STACK" : "BATTLEFIELD:BF") : new PlayCardCommand(Shadow, card, []));
    private static Task Order(Driver game)
        => game.State.TriggerQueue.Count == 0 ? Task.CompletedTask : game.Act("P1", new OrderTriggersCommand(
            OrderedTriggerIds: game.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));

    [Theory]
    [InlineData(Unit, false)] [InlineData(Unit, true)] [InlineData(Spell, false)] [InlineData(Spell, true)]
    [InlineData(Equipment, false)] [InlineData(Equipment, true)]
    public async Task EachSuccessfulFaceDownUnitSpellOrEquipmentOffersAnIndependentTargetlessRewardBeforeOneDormantGold(string card, bool reveal)
    {
        var game = new Driver(Position(card)); await HiddenNextTurn(game, card); await FromFaceDown(game, card, reveal);
        if (card == Spell) {
            Assert.Empty(Rewards(game.State)); Assert.Empty(Gold(game.State)); await game.Top();
        }
        await Order(game); var skill = Assert.Single(Rewards(game.State));
        Assert.Equal(Broker, skill.CardNo); Assert.Equal("P1", skill.ControllerId);
        Assert.Empty(skill.TargetObjectIds); Assert.Null(skill.TargetGenerations);
        Assert.Equal("P1", skill.StandbyObserver!.Unit!.ControllerId);
        Assert.Empty(Gold(game.State)); Assert.NotNull(game.State.PriorityPlayerId);
        await game.Top(); var gold = Assert.Single(Gold(game.State));
        Assert.True(game.State.CardObjects[gold].IsExhausted); Assert.Equal("P1", game.State.CardObjects[gold].OwnerId);
        Assert.Empty(Rewards(game.State)); await game.Replay();
    }

    [Fact]
    public async Task AFaceUpPlayDoesNotEarnTheFaceDownReward()
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand(Shadow, Unit, [], Destination: "BASE"));
        Assert.Empty(Gold(game.State)); Assert.Empty(Rewards(game.State)); Assert.Empty(game.State.TriggerQueue); await game.Replay();
    }

    [Theory]
    [InlineData("HAND")] [InlineData("GRAVEYARD")] [InlineData("BANISHED")] [InlineData("enemy")]
    public async Task PrivateOrEnemyControlledSourcesCannotObserveTheActorsFaceDownPlay(string sourceZone)
    {
        var game = new Driver(Position(sourceZone: sourceZone)); await HiddenNextTurn(game); await FromFaceDown(game);
        Assert.Empty(Gold(game.State)); Assert.Empty(Rewards(game.State)); Assert.Empty(game.State.TriggerQueue); await game.Replay();
    }

    [Fact]
    public async Task ACounteredFaceDownSpellDoesNotCompleteThePlayOrEarnGold()
    {
        var initial = RadGearTestPosition.Put(Position(Spell), "COUNTER", "OGN·064/298", "P2", "HAND");
        initial = RadGearTestPosition.Put(initial, "P2-R6", "UNL-R02", "P2", "BASE");
        initial = RadGearTestPosition.Put(initial, "P2-R7", "UNL-R02", "P2", "BASE");
        var game = new Driver(initial);
        await HiddenNextTurn(game, Spell); await FromFaceDown(game, Spell);
        var spell = Assert.Single(game.State.StackItems, item => item.CardNo == Spell);
        if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.TapRunes("P2", ["P2-R4", "P2-R5"]);
        await game.Act("P2", new RecycleRuneCommand("P2-R6", ["P2-R6", "P2-R7"]));
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [spell.StackItemId])); await game.Top();
        Assert.Contains(Shadow, game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(Gold(game.State)); Assert.Empty(Rewards(game.State)); Assert.Empty(game.State.TriggerQueue); await game.Replay();
    }

    [Fact]
    public async Task ReturningAnObserverBeforeTheSpellCompletesCannotEarnAGoldSkillFromItsEarlierConfirmation()
    {
        var game = new Driver(Position(Spell)); await HiddenNextTurn(game, Spell); await FromFaceDown(game, Spell);
        Assert.Empty(Rewards(game.State)); Assert.Empty(game.State.TriggerQueue);
        await game.TapRunes("P1", ["P1-R0"]);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["BROKER"])); await game.Top();
        Assert.Contains("BROKER", game.State.PlayerZones["P1"].Hand);
        await game.Top(); Assert.Contains(Shadow, game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(Rewards(game.State)); Assert.Empty(Gold(game.State)); Assert.Empty(game.State.TriggerQueue);
        await game.Replay();
    }

    [Fact]
    public async Task ASourceControlledBySomeoneOtherThanItsOwnerRewardsItsCurrentController()
    {
        var game = new Driver(Position(stolen: true)); await HiddenNextTurn(game); await FromFaceDown(game); await Order(game);
        Assert.Equal("P2", game.State.CardObjects["BROKER"].OwnerId);
        Assert.Equal("P1", Assert.Single(Rewards(game.State)).ControllerId); await game.Top();
        Assert.Single(Gold(game.State, "P1")); Assert.Empty(Gold(game.State, "P2")); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheBrokerAfterItsTriggerDoesNotCancelTheGoldOrExposeThePrivateSourceInSkillIds()
    {
        var game = new Driver(Position()); await HiddenNextTurn(game); await FromFaceDown(game); await Order(game);
        if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.TapRunes("P1", ["P1-R0"]);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["BROKER"])); await game.Top();
        Assert.Contains("BROKER", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("BROKER", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        var beforeReward = game.Events().Length;
        await game.Top(); Assert.Single(Gold(game.State));
        Assert.All(game.Events().Skip(beforeReward).Where(e => e.Event.Kind == "EQUIPMENT_TOKEN_CREATED"),
            e => Assert.DoesNotContain("BROKER", JsonSerializer.Serialize(e.Event)));
        Assert.DoesNotContain("\"BROKER\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        Assert.DoesNotContain("BROKER", Assert.Single(Gold(game.State)));
        await game.Replay();
    }

    [Fact]
    public async Task ActualCopiedTextAndThePrintedTextEarnSeparateRewardsForTheSameCompletedPlay()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "SONG", "SFD·059/221", "P1", "BASE"));
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "BROKER", ["ASSEMBLE_GREEN"])); await game.Top();
        await HiddenNextTurn(game); await FromFaceDown(game);
        Assert.Equal(2, game.State.TriggerQueue.Count); await Order(game);
        Assert.Equal(2, Rewards(game.State).Count());
        Assert.Single(Rewards(game.State), item => item.StandbyObserver!.Origin is not null);
        await game.Top(); Assert.Single(Gold(game.State)); await game.Top(); Assert.Equal(2, Gold(game.State).Length);
        await game.Replay();
    }

    [Theory]
    [InlineData("action")] [InlineData("actor")] [InlineData("target")] [InlineData("erased")]
    public async Task RecoveryBindsARewardToItsOriginalAcceptedFaceDownPlayAndRejectsAddedTargets(string branch)
    {
        var game = new Driver(Position()); await HiddenNextTurn(game); await FromFaceDown(game); await Order(game);
        var item = Assert.Single(Rewards(game.State)); var context = item.StandbyObserver!;
        var changed = branch switch {
            "action" => item with { StandbyObserver = context with { Unit = context.Unit! with { ActionHash = new string('0', 64) } } },
            "actor" => item with { ControllerId = "P2" },
            "target" => item with { TargetObjectIds = ["ENEMY"], TargetGenerations = new Dictionary<string, long> { ["ENEMY"] = 0 } },
            _ => item with { StandbyObserver = context with { Unit = null } }
        };
        var forged = game.State with { StackItems = game.State.StackItems.Select(old => old.StackItemId == item.StackItemId ? changed : old).ToArray() };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await game.Top(); Assert.Single(Gold(game.State)); await game.Replay();
    }
}

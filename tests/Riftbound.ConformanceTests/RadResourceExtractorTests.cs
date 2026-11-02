using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadResourceExtractorTests
{
    private const string Extractor = "RAD·052/167";

    [Fact]
    public async Task OfficialEquipmentDeploysToAnEnemyBattlefieldWithoutAPlaySkill()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Extractor);
        Assert.Equal(39548, card.Id); Assert.Equal(1, card.Energy);
        var game = new Driver(Put(Create(), "EXTRACTOR", Extractor, "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("EXTRACTOR", Extractor, [], Destination: "BATTLEFIELD:OTHER"));
        Assert.Equal("OTHER", game.State.ObjectLocations["EXTRACTOR"].BattlefieldObjectId);
        Assert.Contains(CardEquipmentKeywordNames.Deploy, game.State.CardObjects["EXTRACTOR"].Tags);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:missing")]
    public Task IllegalDeploymentDoesNotSpendResources(string destination)
        => Reject(Put(Create(), "EXTRACTOR", Extractor, "P1", "HAND"), "P1",
            new PlayCardCommand("EXTRACTOR", Extractor, [], Destination: destination));

    [Theory]
    [InlineData("base")] [InlineData("enemy-field")] [InlineData("uncontrolled")]
    [InlineData("opponent-turn")] [InlineData("enemy-source")]
    public async Task TheEndConditionRequiresYourTurnAndYourActualControlledBattlefield(string scenario)
    {
        var state = Position();
        if (scenario == "base") state = RadUnitEffectTestFixture.Place(state, "EXTRACTOR", "P1", "BASE");
        if (scenario == "enemy-field") state = RadUnitEffectTestFixture.Place(state, "EXTRACTOR", "P1", "BATTLEFIELD", "OTHER");
        if (scenario == "uncontrolled") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BF"] = state.CardObjects["BF"] with { ControllerId = null } } };
        if (scenario == "opponent-turn") state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        if (scenario == "enemy-source") state = RadUnitEffectTestFixture.Place(state, "EXTRACTOR", "P2", "BATTLEFIELD", "BF");
        var game = new Driver(state);
        await game.Act(state.TurnPlayerId, new EndTurnCommand());
        Assert.DoesNotContain(game.State.StackItems, i => i.SourceObjectId == "EXTRACTOR" && i.EndAbility is not null);
        Assert.Empty(Gold(game.State));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TURN_END_STEP_STARTED");
        if (scenario == "enemy-field")
        {
            Assert.Contains(game.State.StackItems, i => i.HeldContext?.Kind == "DEPLOYMENT_OPPONENT_HELD_DESTROY");
            await game.Top();
            Assert.Contains("EXTRACTOR", game.State.PlayerZones["P1"].Graveyard);
            Assert.Empty(Gold(game.State));
        }
        await game.Replay();
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task TheControllerReceivesOneDormantGoldAfterBothPlayersCanRespond(string owner)
    {
        var game = new Driver(Position(owner));
        await game.Act("P1", new EndTurnCommand());
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal("EXTRACTOR", item.SourceObjectId);
        Assert.Equal(TriggerKinds.TurnEndCreateEquipmentToken, item.EndAbility!.Kind);
        Assert.Equal("BF", item.EndAbility.Location.BattlefieldObjectId);
        Assert.Equal("P1", game.State.TurnPlayerId); Assert.Empty(Gold(game.State));
        await game.Top();
        var gold = Assert.Single(Gold(game.State));
        Assert.True(gold.IsExhausted); Assert.Equal("P1", gold.OwnerId); Assert.Equal("P1", gold.ControllerId);
        Assert.Contains(gold.ObjectId, game.State.PlayerZones["P1"].Base);
        Assert.Equal("P2", game.State.TurnPlayerId);
        Assert.Contains("EXTRACTOR", game.State.PlayerZones["P1"].Battlefields);
        await game.Replay();
    }

    [Fact]
    public async Task IndependentSourcesAreOrderedByTheirControllerAndEachCreatesGold()
    {
        var state = AddExtractor(Position(), "EXTRACTOR-2", "P1");
        var game = new Driver(state);
        await game.Act("P1", new EndTurnCommand());
        var ids = game.State.TriggerQueue.Select(t => t.TriggerId).ToArray(); Assert.Equal(2, ids.Length);
        await Reject(game.State, "P2", new OrderTriggersCommand(OrderedTriggerIds: ids));
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ids));
        await game.Top(); Assert.Single(Gold(game.State)); Assert.Equal("P1", game.State.TurnPlayerId);
        await game.Top(); Assert.Equal(2, Gold(game.State).Length); Assert.Equal("P2", game.State.TurnPlayerId);
        await game.Replay();
    }

    [Fact]
    public async Task TheCreatedGoldUsesTheExistingResourceSkillAndCannotPayWhileDormant()
    {
        var game = new Driver(Position());
        await game.Act("P1", new EndTurnCommand()); await game.Top();
        var gold = Assert.Single(Gold(game.State));
        var ability = P4ActivatedAbilityCatalog.GetAll().Single(a => P4ActivatedAbilityCatalog.IsGoldTokenResourceAbility(a.AbilityId)
            && P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(a, gold.CardNo));
        await Reject(game.State, "P1", new ActivateAbilityCommand(gold.ObjectId, ability.AbilityId, []));
        await game.Act("P2", new EndTurnCommand());
        Assert.False(game.State.CardObjects[gold.ObjectId].IsExhausted);
        var before = game.State.RunePools["P1"].TotalPower;
        await game.Act("P1", new ActivateAbilityCommand(gold.ObjectId, ability.AbilityId, []));
        Assert.Equal(before + 1, game.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain(gold.ObjectId, game.State.CardObjects.Keys);
        await game.Replay();
    }

    [Theory]
    [InlineData("base")] [InlineData("graveyard")] [InlineData("controller")]
    public async Task CapturedEndCheckpointsKeepTheEarnedGoldWhenTheSourceChanges(string change)
    {
        var capture = new Driver(Position()); await capture.Act("P1", new EndTurnCommand());
        var state = capture.State;
        // These are checkpoints after a response has changed the source. The
        // condition was captured at the start of the end step (CN 383.2.a.1).
        state = RadUnitEffectTestFixture.Place(state, "EXTRACTOR", change == "controller" ? "P2" : "P1",
            change == "graveyard" ? "GRAVEYARD" : change == "base" ? "BASE" : "BATTLEFIELD",
            change == "controller" ? "BF" : null);
        if (change == "graveyard") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["EXTRACTOR"] = state.CardObjects["EXTRACTOR"] with { ObjectGeneration = state.CardObjects["EXTRACTOR"].ObjectGeneration + 1 } } };
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        var engine = new CoreRuleEngine();
        for (var passes = 0; state.StackItems.Count > 0; passes++)
        {
            Assert.InRange(passes, 0, 3);
            var result = await engine.ResolveAsync(state, new("end-checkpoint-" + passes, state.PriorityPlayerId!, CommandTypes.PassPriority),
                new PassPriorityCommand(), default);
            Assert.True(result.Accepted, result.ErrorMessage); state = result.State;
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        }
        Assert.Equal("P1", Assert.Single(Gold(state)).ControllerId);
    }

    [Theory]
    [InlineData("turn")] [InlineData("generation")] [InlineData("source")] [InlineData("location")] [InlineData("runes")]
    public async Task RecoveryRejectsInvalidEndConditionOrEffectContext(string mutation)
    {
        var game = new Driver(Position()); await game.Act("P1", new EndTurnCommand());
        var item = Assert.Single(game.State.StackItems); var context = item.EndAbility!;
        context = mutation switch {
            "turn" => context with { TurnNumber = context.TurnNumber + 1 },
            "generation" => context with { Source = context.Source with { ObjectGeneration = 999 } },
            "source" => context with { Source = context.Source with { CardNo = "RAD·024/167" } },
            "location" => context with { Location = new("P1", "BASE") },
            _ => context with { RuneCount = 1 }
        };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { EndAbility = context }] }),
            error => error.Contains("end-step", StringComparison.Ordinal));
        await game.Top(); await game.Replay();
    }

    private static MatchState Position(string owner = "P1") => AddExtractor(Create(), "EXTRACTOR", owner);
    private static MatchState AddExtractor(MatchState state, string id, string owner)
    {
        state = Put(state, id, Extractor, owner, "BATTLEFIELD", "BF");
        state = RadUnitEffectTestFixture.Place(state, id, "P1", "BATTLEFIELD", "BF");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = state.CardObjects[id] with { Tags = [..state.CardObjects[id].Tags, CardEquipmentKeywordNames.Deploy] } } };
    }
    private static CardObjectState[] Gold(MatchState state) => state.CardObjects.Values.Where(c => c.TokenFactoryCardNo is { } no
        && P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory) && factory.TokenFamilyName == "金币").ToArray();
}

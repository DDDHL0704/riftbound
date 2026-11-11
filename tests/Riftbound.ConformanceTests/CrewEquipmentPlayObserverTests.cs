using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class CrewEquipmentPlayObserverTests
{
    private const string Crew = "OGN·091/298";
    private const string Gear = "OGN·023/298";

    private static MatchState Position()
    {
        var state = VenConditionalPlayCostTests.Place(VenUnitEntryBasicTests.Position(Crew), "UNIT", Crew, "P1", "BASE");
        state = VenConditionalPlayCostTests.Place(state, "PLAY", Gear, "P1", "HAND");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsExhausted = true } } };
    }

    internal static async Task Drain(Driver game)
    {
        for (var steps = 0; game.State.StackItems.Count > 0 || game.State.TriggerQueue.Count > 0; steps++)
        {
            Assert.True(steps < 12);
            if (game.State.TriggerQueue.Count > 0 && game.State.PriorityPlayerId is null)
                await game.Act(game.State.ActivePlayerId, new OrderTriggersCommand(OrderedTriggerIds:
                    game.State.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
            else await game.Top();
        }
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task OrdinaryAndTokenEquipmentWaitForBothPlayersBeforeReadyingTheirObserver(bool token)
    {
        var no = token ? "SFD·004/221" : Gear;
        var initial = VenConditionalPlayCostTests.Place(Position(), "PLAY", no, "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("PLAY", no, []));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        if (token) await game.Top();
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("UNIT", skill.SourceObjectId);
        Assert.Equal("FRIENDLY_EQUIPMENT_PLAYED", skill.UnitEvent!.Timing);
        Assert.Empty(skill.TargetObjectIds); Assert.Null(skill.TargetGenerations); Assert.Null(skill.TriggerCost);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        await game.Replay();
    }

    [Theory, InlineData("hidden"), InlineData("graveyard"), InlineData("opponent"), InlineData("unit-play")]
    public async Task OnlyPublicFriendlyFieldObserversSeeAnActualEquipmentPlay(string boundary)
    {
        var state = Position();
        if (boundary is "graveyard" or "opponent") state = VenConditionalPlayCostTests.Place(state, "UNIT", Crew,
            boundary == "opponent" ? "P2" : "P1", boundary == "opponent" ? "BASE" : "GRAVEYARD");
        if (boundary == "hidden") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsFaceDown = true } } };
        var no = boundary == "unit-play" ? "OGN·219/298" : Gear;
        state = VenConditionalPlayCostTests.Place(state, "PLAY", no, "P1", "HAND");
        if (boundary == "opponent") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsExhausted = true } } };
        var wasExhausted = state.CardObjects["UNIT"].IsExhausted;
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("PLAY", no, []));
        Assert.DoesNotContain(game.State.StackItems, item => item.SourceObjectId == "UNIT" && item.UnitEvent is not null);
        Assert.DoesNotContain(game.State.TriggerQueue, item => item.SourceObjectId == "UNIT" && item.UnitEvent is not null);
        Assert.Equal(wasExhausted, game.State.CardObjects["UNIT"].IsExhausted);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_READIED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "UNIT"));
        await game.Replay();
    }

    [Fact]
    public async Task MultipleCrewsEarnSeparateSkillsAndTheirControllerChoosesTheOrder()
    {
        var state = VenConditionalPlayCostTests.Place(Position(), "SECOND", Crew, "P1", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SECOND"] = state.CardObjects["SECOND"] with { IsExhausted = true } } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("PLAY", Gear, []));
        Assert.Equal(2, game.State.TriggerQueue.Count);
        var chosen = game.State.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray();
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: chosen));
        Assert.Equal(2, game.State.StackItems.Count);
        var first = game.State.StackItems.Last().SourceObjectId;
        var second = first == "UNIT" ? "SECOND" : "UNIT";
        await game.Top();
        Assert.False(game.State.CardObjects[first].IsExhausted);
        Assert.True(game.State.CardObjects[second].IsExhausted);
        await game.Top(); Assert.False(game.State.CardObjects[second].IsExhausted);
        await game.Replay();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task TheTriggerUsesTheCurrentFriendlyControllerRatherThanPrintedOwnership(bool originallyEnemy)
    {
        var state = Position();
        if (originallyEnemy) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { OwnerId = "P2" } } };
        else state = VenConditionalPlayCostTests.Place(state, "UNIT", Crew, "P2", "BASE");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("PLAY", Gear, []));
        if (originallyEnemy)
        {
            Assert.Equal("P1", Assert.Single(game.State.StackItems).ControllerId);
            await game.Top(); Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        }
        else Assert.DoesNotContain(game.State.StackItems, item => item.SourceObjectId == "UNIT");
        await game.Replay();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task ACurrentEnemyBattlefieldRestrictionIsCheckedAtResolution(bool returnWarden)
    {
        var state = RadGearTestPosition.Put(Position(), "WARDEN", "OGN·070/298", "P2", "BATTLEFIELD", "BF");
        state = VenConditionalPlayCostTests.Place(state, "RETURN", "OGN·104/298", "P2", "HAND");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("PLAY", Gear, []));
        Assert.Single(game.State.StackItems);
        if (returnWarden)
        {
            await game.Act("P1", new PassPriorityCommand());
            await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["WARDEN"]));
            await game.Top(); Assert.Contains("WARDEN", game.State.PlayerZones["P2"].Hand);
        }
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        await game.Top();
        Assert.Equal(!returnWarden, game.State.CardObjects["UNIT"].IsExhausted);
        if (!returnWarden) Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_READIED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "UNIT"));
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheCrewInResponseKeepsItsSkillWithoutReadyingAnObjectInHand()
    {
        var state = VenConditionalPlayCostTests.Place(Position(), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("PLAY", Gear, []));
        var skill = Assert.Single(game.State.StackItems);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(skill.StackItemId, Assert.Single(game.State.StackItems).StackItemId);
        var sourceHash = MatchStateHasher.HashValue(game.State.CardObjects["UNIT"]);
        await game.Top();
        Assert.Equal(sourceHash, MatchStateHasher.HashValue(game.State.CardObjects["UNIT"]));
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task AnAlreadyReadyCrewStillEarnsItsIndependentNonTargetedSkill()
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsExhausted = false } } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("PLAY", Gear, []));
        var skill = Assert.Single(game.State.StackItems);
        Assert.Empty(skill.TargetObjectIds); Assert.Null(skill.TriggerCost);
        var resources = MatchStateHasher.HashValue(game.State.RunePools);
        await game.Top();
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(resources, MatchStateHasher.HashValue(game.State.RunePools));
        await game.Replay();
    }

    [Fact]
    public async Task AnActualImageOfTheCrewObservesSubsequentEquipmentWithItsCopiedText()
    {
        var state = VenConditionalPlayCostTests.Place(Position(), "UNIT", Crew, "P1", "BANISHED");
        state = VenConditionalPlayCostTests.Place(state, "ENEMY", Crew, "P2", "BASE");
        state = VenConditionalPlayCostTests.Place(state, "COPY", "UNL-200/219", "P1", "HAND");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["ENEMY"]));
        await game.Top(); await Drain(game);
        var image = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        Assert.Equal(Crew, image.CardNo);
        await game.Act("P1", new PlayCardCommand("PLAY", Gear, []));
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal(image.ObjectId, skill.SourceObjectId);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, skill.UnitEvent!.TokenFactoryCardNo);
        Assert.Equal("P1", skill.ControllerId);
        await game.Top(); Assert.False(game.State.CardObjects[image.ObjectId].IsExhausted);
        await game.Replay();
    }


    [Theory, InlineData(false), InlineData(true)]
    public async Task PrintedAgileAndTheCrewLetTheirControllerChooseEitherSimultaneousOrder(bool crewFirst)
    {
        const string sword = "SFD·022/221";
        var state = VenConditionalPlayCostTests.Place(Position(), "PLAY", sword, "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("PLAY", sword, ["UNIT"]));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Null(game.State.CardObjects["PLAY"].AttachedToObjectId);
        Assert.Null(game.State.PriorityPlayerId);
        Assert.Equal(2, game.State.TriggerQueue.Count);
        var observer = Assert.Single(game.State.TriggerQueue, t => t.SourceObjectId == "UNIT");
        var printed = Assert.Single(game.State.TriggerQueue, t => t.SourceObjectId == "PLAY");
        Assert.Equal("PLAY", observer.UnitEvent!.Other!.Binding.ObjectId);
        Assert.Equal(sword, observer.UnitEvent.Other.CardNo);
        Assert.NotNull(printed.PermanentPlayStackItemId);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: crewFirst
            ? [observer.TriggerId, printed.TriggerId] : [printed.TriggerId, observer.TriggerId]));
        Assert.Empty(game.State.TriggerQueue);
        Assert.Equal(2, game.State.StackItems.Count);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Null(game.State.CardObjects["PLAY"].AttachedToObjectId);
        await game.Top();
        Assert.Equal(!crewFirst, game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(crewFirst ? null : "UNIT", game.State.CardObjects["PLAY"].AttachedToObjectId);
        await game.Top();
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal("UNIT", game.State.CardObjects["PLAY"].AttachedToObjectId);
        await game.Replay();
    }

    [Fact]
    public async Task RecoveryChecksTheOriginalAcceptedCaptureAndRejectsErasureDuplicationAndRevival()
    {
        var game = new Driver(Position()); await game.Act("P1", new PlayCardCommand("PLAY", Gear, []));
        var original = game.State; var skill = Assert.Single(original.StackItems);
        Assert.Empty(ArmamentGrantedCombatTriggerTests.Errors(game, original));
        foreach (var context in new[] {
            skill.UnitEvent! with { SourceGeneration = skill.UnitEvent!.SourceGeneration + 1 },
            skill.UnitEvent! with { CapturedTick = original.Tick + 1 },
            skill.UnitEvent! with { Timing = "PLAY" },
            skill.UnitEvent! with { FaceRevision = skill.UnitEvent!.FaceRevision + 1 },
            skill.UnitEvent! with { Other = null },
            skill.UnitEvent! with { Other = skill.UnitEvent!.Other! with { Binding = null! } },
            skill.UnitEvent! with { Other = skill.UnitEvent!.Other! with {
                Binding = skill.UnitEvent!.Other!.Binding with { Generation = skill.UnitEvent!.Other!.Binding.Generation + 1 } } } })
            Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game,
                original with { StackItems = [skill with { UnitEvent = context }] }));
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, original with { StackItems = [] }));
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, original with { StackItems = [skill, skill] }));
        await game.Top();
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { StackItems = [skill] }));
        await game.Replay();
    }

    [Fact]
    public async Task APrimitiveIncarnationBoundaryDoesNotLetAnOldSkillReadyTheNewSource()
    {
        var game = new Driver(Position()); await game.Act("P1", new PlayCardCommand("PLAY", Gear, []));
        var state = game.State;
        // This is a component boundary fixture, not a claimed legal reentry command.
        var changed = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { ObjectGeneration = state.CardObjects["UNIT"].ObjectGeneration + 1 } } };
        var resolved = await OfficialGraveyardRecastTests.Top(changed);
        Assert.True(resolved.State.CardObjects["UNIT"].IsExhausted);
        OfficialInsightAndSpellLockTests.Restore(resolved.State);
    }
}

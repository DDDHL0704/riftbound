using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.StandbyDefendObserverTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class AttackingBattleDrawTests
{
    private const string Crest = "OGN·227/298";
    private const string Timing = "ATTACKING_BATTLE_DRAW";
    private static MatchState Position(string sourceController = "P1", string sourceZone = "BASE")
    {
        var state = SacredProtectorTests.Unit(SacredProtectorTests.Position(), "ENEMY", "P2", "OGN·175/298");
        state = RadGearTestPosition.Put(state, "CREST", Crest, sourceController, sourceZone);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["F"] = state.CardObjects["F"] with { ControllerId = "P2" } } };
    }
    private static bool HasSkill(Driver run) => run.State.StackItems.Any(i => i.UnitEvent?.Timing == Timing);
    private static async Task Advance(Driver run)
    {
        var prompts = ResolutionResult.BuildPrompts(run.State);
        var assignment = prompts.Values.FirstOrDefault(p => p.Candidates?.Any(c => c.Action == CommandTypes.AssignCombatDamage && c.Enabled) == true);
        if (assignment is not null) {
            await run.Act(assignment.PlayerId, new AssignCombatDamageCommand(run.State.BattleState.BattleId!,
                run.State.BattleState.BattlefieldObjectId!, CombatTestDriver.Assignments(assignment))); return;
        }
        if (run.State.PriorityPlayerId is { } priority) { await run.Act(priority, new PassPriorityCommand()); return; }
        if (run.State.FocusPlayerId is { } focus) { await run.Act(focus, new PassFocusCommand()); return; }
        var actor = prompts.Values.First(p => p.Candidates?.Any(c => c.Action == CommandTypes.DeclareBattle && c.Enabled) == true).PlayerId;
        var battle = run.State.BattleState;
        await run.Act(actor, new DeclareBattleCommand(battle.BattlefieldObjectId!, battle.AttackerObjectIds,
            battle.DefenderObjectIds, ["COMBAT_ASSIGNMENT"]));
    }
    private static async Task Advance(VenBasicFlowSpellTests.Driver run)
    {
        var prompts = ResolutionResult.BuildPrompts(run.State);
        var assignment = prompts.Values.FirstOrDefault(p => p.Candidates?.Any(c => c.Action == CommandTypes.AssignCombatDamage && c.Enabled) == true);
        if (assignment is not null) {
            await run.Act(assignment.PlayerId, new AssignCombatDamageCommand(run.State.BattleState.BattleId!,
                run.State.BattleState.BattlefieldObjectId!, CombatTestDriver.Assignments(assignment))); return;
        }
        if (run.State.PriorityPlayerId is { } priority) { await run.Act(priority, new PassPriorityCommand()); return; }
        if (run.State.FocusPlayerId is { } focus) { await run.Act(focus, new PassFocusCommand()); return; }
        var actor = prompts.Values.First(p => p.Candidates?.Any(c => c.Action == CommandTypes.DeclareBattle && c.Enabled) == true).PlayerId;
        var battle = run.State.BattleState;
        await run.Act(actor, new DeclareBattleCommand(battle.BattlefieldObjectId!, battle.AttackerObjectIds,
            battle.DefenderObjectIds, ["COMBAT_ASSIGNMENT"]));
    }
    private static async Task Open(Driver run)
    {
        await run.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        for (var step = 0; step < 40 && !HasSkill(run); step++) await Advance(run);
        Assert.True(HasSkill(run));
    }
    private static async Task Finish(Driver run)
    {
        for (var step = 0; step < 40 && !run.Events().Any(e => e.Event.Kind == "BATTLE_CLOSED"); step++) await Advance(run);
        Assert.Contains(run.Events(), e => e.Event.Kind == "BATTLE_CLOSED");
    }
    private static async Task Offer(Driver run, string player)
    {
        if (run.State.PriorityPlayerId is { } priority && priority != player) await run.Act(priority, new PassPriorityCommand());
        else if (run.State.FocusPlayerId is { } focus && focus != player) await run.Act(focus, new PassFocusCommand());
    }

    [Fact]
    public async Task ARealTieCapturesAnIndependentSkillBeforeMandatoryAttackerRecall()
    {
        var initial = Position(); var run = SacredProtectorTests.Driver(initial, "solari-tie");
        await Open(run);
        var item = Assert.Single(run.State.StackItems, i => i.UnitEvent?.Timing == Timing);
        Assert.Equal(new ObjectBinding("F", initial.CardObjects["F"].ObjectGeneration), item.UnitEvent!.BattleTieBattlefield);
        Assert.Equal("P1", item.ControllerId); Assert.Empty(item.TargetObjectIds);
        Assert.Contains("HOST", run.State.PlayerZones["P1"].Base);
        Assert.Contains("ENEMY", run.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("BATTLE_TIED", run.State.PendingBattleResult!.RelatedEventKinds);
        var before = run.Events().Length; var old = run.State.CardObjects["ENEMY"];
        await run.Top(); await Finish(run);
        Assert.Contains("ENEMY", run.State.PlayerZones["P2"].Base);
        Assert.Equal(old.IsExhausted, run.State.CardObjects["ENEMY"].IsExhausted);
        Assert.Equal(old.BoonCount, run.State.CardObjects["ENEMY"].BoonCount);
        Assert.Equal(old.Damage, run.State.CardObjects["ENEMY"].Damage);
        Assert.Contains(run.Events().Skip(before), e => e.Event.Kind == "UNIT_RECALLED_TO_BASE"
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), Timing));
        Assert.DoesNotContain(run.Events().Skip(before), e => e.Event.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD");
        await SacredProtectorTests.Replay(initial, run);
    }

    [Theory]
    [InlineData("HAND")] [InlineData("GRAVEYARD")] [InlineData("OPPONENT")]
    public async Task ASourceOutsideTheFieldOrControlledByTheDefenderDoesNotObserve(string kind)
    {
        var initial = Position(kind == "OPPONENT" ? "P2" : "P1", kind == "OPPONENT" ? "BASE" : kind);
        var run = SacredProtectorTests.Driver(initial, "solari-source-" + kind);
        await run.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", [])); await Finish(run);
        Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "BATTLE_TIED");
        Assert.Contains("ENEMY", run.State.PlayerZones["P2"].Battlefields);
        await SacredProtectorTests.Replay(initial, run);
    }

    [Theory]
    [InlineData("OGN·175/298", "OGN·175/298")]
    [InlineData("OGN·219/298", "OGN·175/298")]
    [InlineData("OGN·175/298", "OGN·219/298")]
    public async Task MutualDestructionAndEitherWinnerAreNotBattleTies(string hostNo, string enemyNo)
    {
        var initial = Position();
        foreach (var (id, no) in new[] { ("HOST", hostNo), ("ENEMY", enemyNo) }) {
            Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var printed));
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                [id] = initial.CardObjects[id] with { CardNo = no, ManaCost = printed.ManaCost,
                    Power = printed.SourceUnitPower, Tags = [CardObjectTags.UnitCard] } } };
        }
        var run = SacredProtectorTests.Driver(initial, "solari-no-tie-" + hostNo + "-" + enemyNo);
        await run.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", [])); await Finish(run);
        Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "BATTLE_TIED");
        Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "TRIGGER_QUEUED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "CREST"));
        await SacredProtectorTests.Replay(initial, run);
    }

    [Fact]
    public async Task ReturningTheSourceAfterTheTieCannotCancelOrRelocateItsEarnedRecall()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1");
        initial = RadGearTestPosition.Put(initial, "LEGEND", "RAD·145/167", "P1", "BASE");
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with {
                    Base = initial.PlayerZones["P1"].Base.Where(id => id != "LEGEND").ToArray(), LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND") } };
        var run = new VenBasicFlowSpellTests.Driver(OriannaEquipmentConsumerFixture.WithResources(initial));
        await OriannaEquipmentConsumerFixture.AnimateAtNextOwnMain(run, "CREST");
        await run.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        for (var step = 0; step < 40 && !run.State.StackItems.Any(i => i.UnitEvent?.Timing == Timing); step++) await Advance(run);
        Assert.Contains(run.State.StackItems, i => i.UnitEvent?.Timing == Timing);
        if (run.State.PriorityPlayerId is { } other && other != "P1") await run.Act(other, new PassPriorityCommand());
        await run.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["CREST"]));
        await run.Top(); Assert.Contains("CREST", run.State.PlayerZones["P1"].Hand);
        await run.Top();
        for (var step = 0; step < 40 && !run.Events().Any(e => e.Event.Kind == "BATTLE_CLOSED"); step++) await Advance(run);
        Assert.Contains("ENEMY", run.State.PlayerZones["P2"].Base);
        await run.Replay();
    }

    [Fact]
    public async Task DestructionBeforeCleanupCannotEarnTheLaterTieSkill()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "DESTROY", "RAD·101/167", "P1");
        var run = SacredProtectorTests.Driver(initial, "solari-destroyed-before-tie");
        await run.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", [])); await Offer(run, "P1");
        await run.Act("P1", new PlayCardCommand("DESTROY", "RAD·101/167", ["CREST"]));
        await run.Top(); Assert.Contains("CREST", run.State.PlayerZones["P1"].Graveyard); await Finish(run);
        Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "BATTLE_TIED");
        Assert.Contains("ENEMY", run.State.PlayerZones["P2"].Battlefields);
        await SacredProtectorTests.Replay(initial, run);
    }

    [Theory]
    [InlineData("field")] [InlineData("generation")] [InlineData("actor")] [InlineData("marker")]
    public async Task RecoveryRejectsForgedTieFieldRoleAndOriginalCleanupMarker(string kind)
    {
        var initial = Position(); var run = SacredProtectorTests.Driver(initial, "solari-forged-" + kind);
        await Open(run);
        var item = Assert.Single(run.State.StackItems, i => i.UnitEvent?.Timing == Timing); var context = item.UnitEvent!;
        var changed = kind == "field" ? context with { BattleTieBattlefield = new("HOST", 0) }
            : kind == "generation" ? context with { BattleTieBattlefield = context.BattleTieBattlefield! with { Generation = 999 } } : context;
        var state = run.State with { StackItems = run.State.StackItems.Select(i => i.StackItemId == item.StackItemId
            ? i with { UnitEvent = changed, ControllerId = kind == "actor" ? "P2" : i.ControllerId } : i).ToArray(),
            PendingBattleResult = kind == "marker" ? run.State.PendingBattleResult! with {
                RelatedEventKinds = run.State.PendingBattleResult!.RelatedEventKinds.Where(e => e != "BATTLE_TIED").ToArray() }
                : run.State.PendingBattleResult };
        Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(state));
        await run.Top(); await Finish(run); await SacredProtectorTests.Replay(initial, run);
    }
}

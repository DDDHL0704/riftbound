using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.CardCatalog;
using System.Text.Json;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class LeonaStunnedMightTests
{
    internal static MatchState Board(string printing = "OGN·079/298", int power = 3, bool stunned = true, bool copied = false)
    {
        var s = AttackDamageDistributionTests.Position(printing, copied);
        s = LocalDestructionRecallTests.At(s, "HOST", "P1", "BATTLEFIELD", "F");
        if (copied) s = LocalDestructionRecallTests.At(s, "GEAR", "P1", "BATTLEFIELD", "F");
        return s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = s.CardObjects["HOST"] with { Power = 6 },
            ["ENEMY1"] = s.CardObjects["ENEMY1"] with { Power = power, UntilEndOfTurnEffects = stunned ? ["STUNNED"] : [] } } };
    }

    internal static int Might(MatchState s, string target = "ENEMY1") => CurrentPowerTargetTests.Snapshot(s)
        .GetProperty("players").GetProperty("P2").GetProperty("objects").GetProperty(target).GetProperty("effectivePower").GetInt32();

    private static async Task<MatchState> QuietBattle(MatchState initial)
    {
        foreach (var id in new[] { "ENEMY2", "ENEMY3" }) initial = LocalDestructionRecallTests.At(initial, id, "P2", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { UntilEndOfTurnEffects = ["STUNNED"] } } };
        initial = LocalDestructionRecallTests.At(initial, "HOST", "P1", "BASE");
        var moved = await Act(initial, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var closed = await AttackDamageDistributionTests.Top(moved.State);
        var settled = await Act(closed.State, "P1", new DeclareBattleCommand("F", ["HOST"], ["ENEMY1"], ["COMBAT_ASSIGNMENT"]));
        Assert.False(settled.State.BattleState.IsActive); Assert.Equal(TimingStates.NeutralOpen, settled.State.TimingState);
        Assert.Equal("BASE", settled.State.ObjectLocations["HOST"].Zone);
        Restore(settled.State); ArmamentMightTests.PublicRecovery(settled.State); return settled.State;
    }

    [Theory]
    [InlineData("OGN·079/298")]
    [InlineData("OGN·079a/298")]
    public void DedicatedPrintingContinuouslyReducesTheStunnedEnemyHere(string printing)
    {
        var initial = Board(printing);
        Assert.Equal(1, Might(initial));
        Assert.Equal(3, initial.CardObjects["ENEMY1"].Power);
        Assert.Empty(initial.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers);
        Restore(initial); ArmamentMightTests.PublicRecovery(initial);
    }

    [Theory]
    [InlineData(-2, 0)] [InlineData(0, 0)] [InlineData(1, 1)] [InlineData(2, 1)]
    [InlineData(8, 1)] [InlineData(9, 1)] [InlineData(10, 2)]
    public void CurrentMightFloorIsRecomputedWithoutRaisingLowerValues(int power, int expected)
    {
        var s = Board(power: power); Assert.Equal(expected, Might(s)); Assert.Equal(power, s.CardObjects["ENEMY1"].Power);
        Assert.Empty(s.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers); Restore(s); ArmamentMightTests.PublicRecovery(s);
    }

    [Theory]
    [InlineData("source-base")] [InlineData("other-location")] [InlineData("not-stunned")]
    [InlineData("friendly-source")] [InlineData("source-face-down")]
    public void ConditionalReductionRequiresAPublicEnemySourceHereAndAStunnedUnit(string branch)
    {
        var s = Board();
        if (branch == "source-base") s = LocalDestructionRecallTests.At(s, "HOST", "P1", "BASE");
        if (branch == "other-location") s = LocalDestructionRecallTests.At(s, "ENEMY1", "P2", "BASE");
        if (branch is "not-stunned" or "friendly-source" or "source-face-down")
            s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["ENEMY1"] = s.CardObjects["ENEMY1"] with { UntilEndOfTurnEffects = branch == "not-stunned" ? [] : ["STUNNED"] },
                ["HOST"] = s.CardObjects["HOST"] with { IsFaceDown = branch == "source-face-down" } } };
        if (branch == "friendly-source") s = LocalDestructionRecallTests.At(s with {
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["HOST"] = s.CardObjects["HOST"] with { OwnerId = "P2", ControllerId = "P2" } } }, "HOST", "P2", "BATTLEFIELD", "F");
        Assert.Equal(3, Might(s)); Assert.DoesNotContain(s.ContinuousEffects, e => e.EffectKind == StaticAuraKinds.SameLocationEnemyFilteredPower);
    }

    [Fact]
    public void EquipmentAndBoonAreIncludedBeforeThePassiveFloor()
    {
        var s = Board(power: 8);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["GEAR"] = s.CardObjects["GEAR"] with { OwnerId = "P2", ControllerId = "P2", AttachedToObjectId = "ENEMY1" },
            ["ENEMY1"] = s.CardObjects["ENEMY1"] with { Power = 9, Tags = [CardObjectTags.UnitCard, CardObjectTags.Boon] } } };
        s = LocalDestructionRecallTests.At(s, "GEAR", "P2", "BATTLEFIELD", "F");
        Assert.Equal(3, Might(s)); // 8 printed/setup + 1 boon + 2 equipment - 8
        Restore(s); ArmamentMightTests.PublicRecovery(s);
        var detached = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["GEAR"] = s.CardObjects["GEAR"] with { AttachedToObjectId = null } } };
        Assert.Equal(1, Might(detached));
    }

    [Fact]
    public void PrintedCopiesProvideIndependentLiveAurasAndDepartingGearRemovesOne()
    {
        var s = Board(power: 20, copied: true);
        Assert.Equal(4, Might(s)); Assert.Equal(2, s.ContinuousEffects.Count(e => e.EffectKind == StaticAuraKinds.SameLocationEnemyFilteredPower));
        Restore(s); ArmamentMightTests.PublicRecovery(s);
        var removed = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["GEAR"] = s.CardObjects["GEAR"] with { AttachedToObjectId = null, AttachedRuleText = null } } };
        Assert.Equal(12, Might(removed)); Restore(removed); ArmamentMightTests.PublicRecovery(removed);
    }

    [Fact]
    public async Task RealStunWithDamageMakesTheNewLethalThresholdApplyImmediately()
    {
        var initial = AttackDamageDistributionTests.Spell(Board(stunned: false), "STUN", "OGN·262/298", "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY1"] = initial.CardObjects["ENEMY1"] with { Damage = 2 } } };
        initial = LocalDestructionRecallTests.At(initial, "HOST", "P1", "BASE");
        var duel = await Act(initial, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var played = await Act(duel.State, "P1", new PlayCardCommand("STUN", "OGN·262/298", ["ENEMY1"]));
        var done = await Top(played.State);
        Assert.Contains("ENEMY1", done.State.PlayerZones["P2"].Graveyard);
        Assert.Contains(done.Events, e => e.Kind == "UNIT_DESTROYED" && Equals(e.Payload.GetValueOrDefault("targetObjectId"), "ENEMY1"));
        Assert.Equal(0, done.State.CardObjects["ENEMY2"].Damage); Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task DefendingMightExistsBeforeCombatAndPrecedesThePassiveReduction()
    {
        var initial = LocalDestructionRecallTests.At(Board(power: 10), "HOST", "P1", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY1"] = initial.CardObjects["ENEMY1"] with { Tags = [CardObjectTags.UnitCard, "坚守2"] } } };
        var moved = await Act(initial, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.True(moved.State.CardObjects["ENEMY1"].IsDefending); Assert.Equal(TimingStates.SpellDuelOpen, moved.State.TimingState);
        Assert.Equal(4, Might(moved.State)); Restore(moved.State); ArmamentMightTests.PublicRecovery(moved.State);
    }

    [Fact]
    public async Task ARealSourceMoveImmediatelyRemovesTheAuraWithoutLeavingATurnModifier()
    {
        var initial = AttackDamageDistributionTests.Spell(LocalDestructionRecallTests.At(Board(), "HOST", "P1", "BASE"),
            "FLASH", "OGS·011/024", "P1");
        var moved = await Act(initial, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", [])); Assert.Equal(1, Might(moved.State));
        var played = await Act(moved.State, "P1", new PlayCardCommand("FLASH", "OGS·011/024", ["HOST"]));
        var done = await Top(played.State); Assert.Equal("BASE", done.State.ObjectLocations["HOST"].Zone);
        Assert.Equal(3, Might(done.State)); Assert.Empty(done.State.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers);
        Assert.DoesNotContain(done.State.ContinuousEffects, e => e.EffectKind == StaticAuraKinds.SameLocationEnemyFilteredPower);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task ARealEquipmentReturnRemovesOnlyTheCopiedLiveReduction()
    {
        var initial = LocalDestructionRecallTests.At(Board(power: 20, copied: true), "HOST", "P1", "BASE");
        initial = LocalDestructionRecallTests.At(initial, "GEAR", "P1", "BASE");
        initial = AttackDamageDistributionTests.Spell(initial, "RECALL", "SFD·135/221", "P1");
        var moved = await Act(initial, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", [])); Assert.Equal(4, Might(moved.State));
        var played = await Act(moved.State, "P1", new PlayCardCommand("RECALL", "SFD·135/221", ["GEAR"]));
        var done = await Top(played.State); Assert.Equal("HAND", done.State.ObjectLocations["GEAR"].Zone);
        Assert.Equal(12, Might(done.State)); Assert.Single(done.State.ContinuousEffects, e => e.EffectKind == StaticAuraKinds.SameLocationEnemyFilteredPower);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task SiphonSnapshotsCurrentMightWhileThePassiveContinuesToRecompute()
    {
        var initial = LocalDestructionRecallTests.At(Board(power: 10), "HOST", "P1", "BASE");
        initial = AttackDamageDistributionTests.Spell(initial, "SIPHON", SiphonEnergyTests.Spell, "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "FLASH", "OGS·011/024", "P1");
        var moved = await Act(initial, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", [])); Assert.Equal(2, Might(moved.State));
        var played = await Act(moved.State, "P1", new PlayCardCommand("SIPHON", SiphonEnergyTests.Spell, ["F"]));
        var reduced = await Top(played.State);
        Assert.Equal(9, reduced.State.CardObjects["ENEMY1"].Power); Assert.Equal(1, Might(reduced.State));
        var modifier = Assert.Single(reduced.State.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(2, modifier.SnapshotMight); Assert.Equal(-1, modifier.PowerDelta);
        Assert.Equal("P2", reduced.State.FocusPlayerId);
        var returnedFocus = await Act(reduced.State, "P2", new PassFocusCommand()); Assert.Equal("P1", returnedFocus.State.FocusPlayerId);
        var response = await Act(returnedFocus.State, "P1", new PlayCardCommand("FLASH", "OGS·011/024", ["HOST"]));
        var done = await Top(response.State); Assert.Equal(9, Might(done.State));
        Assert.Single(done.State.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers); Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("powerDelta")] [InlineData("basePower")] [InlineData("effectivePower")]
    [InlineData("sourceObjectId")] [InlineData("targetObjectId")] [InlineData("sourceCardNo")]
    [InlineData("effectKind")] [InlineData("effectId")] [InlineData("sourcePath")]
    [InlineData("condition")] [InlineData("lifecycle")] [InlineData("duration")]
    public void CompleteRecoveryRejectsIndependentForgedConditionalAuraValues(string key)
    {
        var state = Board(); var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        Assert.Empty(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator));
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot)) {
            var effects = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(snapshot.Timing["continuousEffects"]);
            var aura = effects.Single(e => Equals(e.GetValueOrDefault("effectKind"), StaticAuraKinds.SameLocationEnemyFilteredPower));
            var original = aura[key]; aura[key] = key is "powerDelta" or "basePower" or "effectivePower" ? 99 : "FORGED";
            Assert.NotEmpty(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator)); aura[key] = original;
        }
    }

    [Fact]
    public async Task TurnEndRemovesStunAfterARealBattleAndLeavesNoPassiveTurnModifier()
    {
        var s = await QuietBattle(Board()); Assert.Equal(3, Might(s));
        var ended = await Act(s, "P1", new EndTurnCommand());
        Assert.DoesNotContain("STUNNED", ended.State.CardObjects["ENEMY1"].UntilEndOfTurnEffects);
        Assert.Equal(3, Might(ended.State)); Assert.Equal("BASE", ended.State.ObjectLocations["HOST"].Zone);
        Assert.DoesNotContain(ended.State.ContinuousEffects, e => e.EffectKind == StaticAuraKinds.SameLocationEnemyFilteredPower);
        Restore(ended.State); ArmamentMightTests.PublicRecovery(ended.State);
    }

    [Fact]
    public async Task BothPrintedAurasHaveTheIndependentCurrentDefinition()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, FunctionalUnitBuilder.Build(catalog.Cards), []);
        foreach (var printing in new[] { "OGN·079/298", "OGN·079a/298" }) {
            var spec = specs.Single(s => s.CardNo == printing);
            var aura = Assert.Single(spec.StaticAuras, a => a.Kind == StaticAuraKinds.SameLocationEnemyFilteredPower);
            Assert.Equal(-8, aura.PowerDeltaPerParticipant); Assert.Equal(1, aura.MinimumPowerAfterModifier);
            Assert.Equal(StaticAuraTargetFilters.Stunned, aura.TargetFilter);
            Assert.Equal("WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD", aura.Duration);
        }
    }
}

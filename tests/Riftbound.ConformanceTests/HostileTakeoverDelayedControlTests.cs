using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class HostileTakeoverDelayedControlTests
{
    private const string Takeover = "SFD·202/221";

    private static MatchState Position()
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·275/298"), "STEAL", Takeover, "P1", "HAND");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "HUNT", "UNL-184/219", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "COUNTER", "OGN·064/298", "P2", "HAND");
        return state;
    }

    private static async Task Steal(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("STEAL", Takeover, ["ENEMY"]));
        await game.Top();
        if (!game.State.BattleState.IsActive) await ToMain(game);
        Assert.Equal("P1", game.State.CardObjects["ENEMY"].ControllerId);
        Assert.NotEmpty(game.State.CardObjects["ENEMY"].UnitControl!.Grants);
    }

    private static async Task Order(Driver game)
    {
        while (ResolutionResult.HasOpenOrderTriggersWindow(game.State))
            await game.Act(ResolutionResult.OrderTriggersOrderingPlayerId(game.State)!,
                new OrderTriggersCommand(OrderedTriggerIds: CoreRuleEngine.TriggerResolutionOrderIds(game.State)));
    }

    private static async Task FinishEnd(Driver game, string player)
    {
        for (var step = 0; game.State.TurnPlayerId == player; step++)
        {
            Assert.InRange(step, 0, 16);
            await Order(game);
            if (game.State.StackItems.Count > 0) await game.Top();
            else if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else Assert.Fail("End step has no next action.");
        }
    }

    internal static async Task ToMain(Driver game)
    {
        for (var step = 0; game.State.TimingState != TimingStates.NeutralOpen
            || game.State.StackItems.Count > 0 || game.State.BattleState.IsActive; step++)
        {
            Assert.InRange(step, 0, 12); await Order(game);
            if (game.State.StackItems.Count > 0) await game.Top();
            else if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else if (game.State.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
            else Assert.Fail("Response window has no next action.");
        }
    }

    [Fact]
    public async Task TheEndInstructionUsesTheOrdinaryRespondableChainAndRecallsTheAttachedGroupWithoutMovement()
    {
        var initial = RadGearTestPosition.Put(Position(), "GEAR", "SFD·022/221", "P2", "BATTLEFIELD", "BF");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = initial.CardObjects["GEAR"] with { AttachedToObjectId = "ENEMY" },
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { IsExhausted = true,
                UntilEndOfTurnEffects = [MovementRestrictionRules.ForPlayer("P1"), MovementRestrictionRules.ForPlayer("P2")] } } };
        var game = new Driver(initial); await Steal(game);
        Assert.False(game.State.CardObjects["ENEMY"].IsExhausted);
        Assert.Contains("STEAL", game.State.PlayerZones["P1"].Graveyard);
        var generation = game.State.CardObjects["ENEMY"].ObjectGeneration;
        await game.Act("P1", new EndTurnCommand());
        Assert.NotNull(game.State.EndStep); Assert.Equal(MatchPhases.TurnEnd, game.State.Phase);
        Assert.Equal("P1", game.State.CardObjects["ENEMY"].ControllerId);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_CONTROL_RETURNED");
        await Order(game);
        var skill = Assert.Single(game.State.StackItems);
        Assert.NotNull(skill.DelayedControlLoss); Assert.Empty(skill.TargetObjectIds);
        Assert.NotNull(game.State.PriorityPlayerId);
        await FinishEnd(game, "P1");
        Assert.Equal("P2", game.State.CardObjects["ENEMY"].ControllerId);
        Assert.Null(game.State.CardObjects["ENEMY"].UnitControl);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        Assert.Contains("GEAR", game.State.PlayerZones["P2"].Base);
        Assert.Equal("ENEMY", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(generation, game.State.CardObjects["ENEMY"].ObjectGeneration);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD");
        Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_RECALLED_TO_CONTROLLER_BASE");
        await game.Replay();
    }

    [Fact]
    public async Task ARealResponseMayReturnTheUnitBeforeTheIndependentEndSkillResolves()
    {
        var game = new Driver(Position()); await Steal(game);
        await game.Act("P1", new EndTurnCommand()); await Order(game);
        var original = Assert.Single(game.State.StackItems).DelayedControlLoss!;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ENEMY"]));
        await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand);
        Assert.True(game.State.CardObjects["ENEMY"].ObjectGeneration > original.Unit.Generation);
        Assert.Null(game.State.CardObjects["ENEMY"].UnitControl);
        await FinishEnd(game, "P1");
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_RECALLED_TO_CONTROLLER_BASE");
        await game.Replay();
    }

    [Fact]
    public async Task BanishAndActualReplayCannotLetTheOldReferenceReachTheNewUnit()
    {
        var game = new Driver(Position()); await Steal(game);
        var generation = game.State.CardObjects["ENEMY"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("HUNT", "UNL-184/219", ["ENEMY"])); await game.Top();
        await game.Act("P2", new PlayCardCommand("ENEMY", "OGN·219/298", [], Destination: "BATTLEFIELD:OTHER"));
        Assert.True(game.State.CardObjects["ENEMY"].ObjectGeneration > generation);
        Assert.Null(game.State.CardObjects["ENEMY"].UnitControl);
        await ToMain(game);
        await game.Act("P1", new EndTurnCommand()); await FinishEnd(game, "P1");
        Assert.Equal("OTHER", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_RECALLED_TO_CONTROLLER_BASE");
        await game.Replay();
    }

    [Fact]
    public async Task ActuallyCounteringTheCreatingSpellProducesNeitherControlNorADelayedSkill()
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("STEAL", Takeover, ["ENEMY"]));
        var spell = Assert.Single(game.State.StackItems);
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [spell.StackItemId])); await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Null(game.State.CardObjects["ENEMY"].UnitControl);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DELAYED_CONTROL_LOSS_SCHEDULED");
        await ToMain(game);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal("P2", game.State.CardObjects["ENEMY"].ControllerId); await game.Replay();
    }

    [Fact]
    public async Task RemovingACounteredDelayedSkillDoesNotEndItsControlGrantDuringTurnCleanup()
    {
        var game = new Driver(Position()); await Steal(game);
        await game.Act("P1", new EndTurnCommand()); await Order(game); await game.Replay();
        var state = game.State; var skill = Assert.Single(state.StackItems);
        // Counter-resolution boundary, using the shared chain removal. Current
        // printed counters restrict skills to new targets; this fixed historical
        // reference supplies no such target. Do not invent a legal counter card.
        state = state with { StackItems = CoreRuleEngine.RemoveCounteredStackItems(state.StackItems, [skill.StackItemId]) };
        // Isolate the ordinary cleanup boundary after the countered chain has
        // closed. The original driver's accepted history above stays untouched.
        state = state with { EndStep = null, Phase = MatchPhases.Main, TimingState = TimingStates.NeutralOpen,
            ActivePlayerId = "P1", PriorityPlayerId = null, PassedPriorityPlayerIds = [] };
        var cleanup = await new CoreRuleEngine().ResolveAsync(state,
            new("counter-boundary-cleanup", "P1", CommandTypes.EndTurn), new EndTurnCommand(), default);
        Assert.True(cleanup.Accepted, cleanup.ErrorMessage);
        Assert.DoesNotContain(cleanup.Events, e => e.Kind is "UNIT_CONTROL_RETURNED" or "UNIT_RECALLED_TO_CONTROLLER_BASE");
        state = cleanup.State;
        Assert.Equal("P1", state.CardObjects["ENEMY"].ControllerId);
        Assert.Single(state.CardObjects["ENEMY"].UnitControl!.Grants);
        Assert.Equal("BF", state.ObjectLocations["ENEMY"].BattlefieldObjectId);
    }

    private static MatchState WithFunding(MatchState state)
    {
        for (var i = 0; i < 10; i++) state = RadGearTestPosition.Put(state, "FUND-" + i,
            i == 0 ? "UNL-R03" : "UNL-R06", "P1", "BASE");
        return state;
    }

    private static async Task Fund(Driver game)
    {
        await game.TapRunes("P1", Enumerable.Range(0, 10).Select(i => "FUND-" + i));
        await game.Act("P1", new RecycleRuneCommand("FUND-0"));
        await game.Act("P1", new RecycleRuneCommand("FUND-1"));
    }

    [Fact]
    public async Task TwoActualTakeoversOnTheSameUnitEachEndOnlyTheirOwnControlEffect()
    {
        var initial = WithFunding(Position());
        initial = RadGearTestPosition.Put(initial, "SECOND-STEAL", Takeover, "P2", "HAND");
        initial = RadGearTestPosition.Put(initial, "PUMP", "UNL-031/219", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "HOST", "OGN·219/298", "P2", "BATTLEFIELD", "BF");
        initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2",
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["BF"] = initial.CardObjects["BF"] with { ControllerId = "P2" } } };
        var game = new Driver(initial);
        await game.Act("P2", new HideCardCommand("SECOND-STEAL", Takeover, "BATTLEFIELD:BF", ["STANDBY_A"]));
        await game.Act("P2", new EndTurnCommand()); await Fund(game); await Steal(game);
        await game.Act("P1", new PlayCardCommand("PUMP", "UNL-031/219", ["ALLY"]));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new RevealCardCommand("SECOND-STEAL", Takeover, ["ENEMY"],
            "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "STACK"));
        await game.Top(); await game.Top();
        for (var step = 0; game.State.TimingState != TimingStates.NeutralOpen; step++)
        {
            Assert.InRange(step, 0, 8); await Order(game);
            if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else if (game.State.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
            else Assert.Fail("Control contest has no next action.");
        }
        var grants = game.State.CardObjects["ENEMY"].UnitControl!.Grants;
        Assert.Equal(2, grants.Count); Assert.Equal("P2", game.State.CardObjects["ENEMY"].ControllerId);
        Assert.NotEqual(grants[0].InstanceId, grants[1].InstanceId);
        await game.Act("P1", new EndTurnCommand()); await Order(game);
        Assert.Equal(2, game.State.StackItems.Count);
        var first = game.State.StackItems[^1].DelayedControlLoss!.Grant.InstanceId;
        await game.Top();
        var remaining = Assert.Single(game.State.CardObjects["ENEMY"].UnitControl!.Grants);
        Assert.NotEqual(first, remaining.InstanceId);
        Assert.Equal(remaining.ControllerId, game.State.CardObjects["ENEMY"].ControllerId);
        Assert.Contains("ENEMY", game.State.PlayerZones[remaining.ControllerId].Base);
        await FinishEnd(game, "P1");
        Assert.Null(game.State.CardObjects["ENEMY"].UnitControl);
        Assert.Equal("P2", game.State.CardObjects["ENEMY"].ControllerId);
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "UNIT_CONTROL_RETURNED"));
        await game.Replay();
    }

    [Fact]
    public async Task ARealStandbyTakeoverAfterTheEndStepStartedCreatesNoDelayedSkillForThisOrTheNextTurn()
    {
        var initial = Position();
        initial = RadUnitEffectTestFixture.Place(initial, "ENEMY", "P2", "BASE");
        initial = RadUnitEffectTestFixture.Place(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        initial = RadGearTestPosition.Put(initial, "HUNT-RESPONSE", "UNL-184/219", "P2", "HAND");
        for (var i = 0; i < 3; i++) initial = RadGearTestPosition.Put(initial, "P2-FUND-" + i, "UNL-R01", "P2", "BASE");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["BF"] = OfficialGarenConquestBattlefieldCountTests.Printed("BF", "OGN·278/298", "P1") with { ControllerId = "P1" },
                ["END-LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("END-LEGEND", "OGS·017/024", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["END-LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["END-LEGEND"] = new("P1", "LEGEND") } };
        var game = new Driver(initial);
        await game.Act("P1", new HideCardCommand("STEAL", Takeover, "BATTLEFIELD:BF", ["STANDBY_A"]));
        await game.Act("P1", new EndTurnCommand()); await FinishEnd(game, "P1");
        await game.Act("P2", new EndTurnCommand());
        await game.Act("P1", new EndTurnCommand()); await Order(game);
        Assert.NotNull(game.State.EndStep);
        if (game.State.PendingCardChoice is { } choice)
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        await game.Act("P1", new PassPriorityCommand());
        await game.TapRunes("P2", Enumerable.Range(0, 3).Select(i => "P2-FUND-" + i));
        await game.Act("P2", new RecycleRuneCommand("P2-FUND-0"));
        await game.Act("P2", new PlayCardCommand("HUNT-RESPONSE", "UNL-184/219", ["ENEMY"])); await game.Top();
        await game.Act("P2", new PlayCardCommand("ENEMY", "OGN·219/298", [], Destination: "BATTLEFIELD:BF"));
        if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P1", new RevealCardCommand("STEAL", Takeover, ["ENEMY"],
            "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "STACK"));
        await game.Top();
        Assert.Equal("P1", game.State.CardObjects["ENEMY"].ControllerId);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DELAYED_CONTROL_LOSS_SCHEDULED");
        await FinishEnd(game, "P1");
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal("P1", game.State.CardObjects["ENEMY"].ControllerId);
        Assert.Single(game.State.CardObjects["ENEMY"].UnitControl!.Grants);
        Assert.Equal("BF", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_RECALLED_TO_CONTROLLER_BASE");
        await game.Replay();
    }

    [Fact]
    public async Task AStillActiveEarlierPermanentControlEffectResumesAndRecallUsesItsControllerRatherThanTheOwner()
    {
        var initial = WithFunding(Position());
        initial = RadGearTestPosition.Put(initial, "PERMANENT", "OGN·203/298", "P2", "HAND");
        initial = RadUnitEffectTestFixture.Place(initial, "ENEMY", "P1", "BATTLEFIELD", "BF");
        initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2",
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ENEMY"] = initial.CardObjects["ENEMY"] with { OwnerId = "P1" } } };
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("PERMANENT", "OGN·203/298", ["ENEMY"])); await game.Top();
        await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
        await ToMain(game);
        await game.Act("P2", new EndTurnCommand()); await Fund(game); await Steal(game);
        Assert.Equal(2, game.State.CardObjects["ENEMY"].UnitControl!.Grants.Count);
        await game.Act("P1", new EndTurnCommand()); await FinishEnd(game, "P1");
        Assert.Equal("P1", game.State.CardObjects["ENEMY"].OwnerId);
        Assert.Equal("P2", game.State.CardObjects["ENEMY"].ControllerId);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        Assert.Equal("OGN·203/298", Assert.Single(game.State.CardObjects["ENEMY"].UnitControl!.Grants).CardNo);
        await game.Replay();
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void UnitAndEquipmentControlLayersRemainOrderedWhenEitherEffectEnds(bool equipmentIsNewer, bool equipmentSourceRemains)
    {
        // Component boundary for a unit made from equipment. Full command,
        // recovery and original-history tests above cover the spell itself.
        var binding = new ObjectBinding("UNIT", 0);
        var grant = new UnitControlGrant("STACK:CONTROL:0:UNIT", "STACK", new("SPELL", 0), Takeover,
            "HOSTILE_TAKEOVER_GAIN_CONTROL_READY_ENEMY_BATTLEFIELD_UNIT", "P2", 2, 0);
        var gearGrant = new EquipmentControlGrant("EARLIER-STEAL", new("SOURCE", 0), "P1", "SOURCE_BOUND", equipmentIsNewer ? 3 : 1);
        var unit = new CardObjectState("UNIT", ownerId: "P2", controllerId: equipmentIsNewer ? "P1" : "P2") {
            UnitControl = new(binding, "P2", [grant]), EquipmentControl = new(binding, "P2", [gearGrant]) };
        var cards = new Dictionary<string, CardObjectState> {
            ["UNIT"] = unit, ["SOURCE"] = new("SOURCE", ownerId: "P1", controllerId: "P1") };
        var zones = new Dictionary<string, PlayerZones> {
            ["P1"] = PlayerZones.Empty with { Base = equipmentSourceRemains ? ["SOURCE"] : [], Hand = equipmentSourceRemains ? [] : ["SOURCE"] },
            ["P2"] = PlayerZones.Empty with { Battlefields = ["UNIT"] } };
        Assert.Equal(equipmentIsNewer ? "P1" : "P2", CoreRuleEngine.LayeredControlController(unit, "P2"));
        CoreRuleEngine.ReturnSourceStolenEquipmentForMissingSources(zones, cards, []);
        unit = cards["UNIT"];
        Assert.Equal(equipmentSourceRemains && equipmentIsNewer ? "P1" : "P2", unit.ControllerId);
        Assert.Single(unit.UnitControl!.Grants);
        unit = CoreRuleEngine.EndUnitControlGrant(unit, grant.InstanceId);
        Assert.Null(unit.UnitControl);
        Assert.Equal(equipmentSourceRemains ? "P1" : "P2", unit.ControllerId);
        Assert.Equal(equipmentSourceRemains, unit.EquipmentControl is not null);
    }

    [Theory]
    [InlineData("controller")] [InlineData("generation")] [InlineData("grant")] [InlineData("erase-delay")] [InlineData("duplicate-delay")]
    public async Task RecoveryRejectsChangingTheOriginalGrantOrDelayedInstruction(string mutation)
    {
        var game = new Driver(Position()); await Steal(game);
        var state = game.State; var unit = state.CardObjects["ENEMY"]; var lease = unit.UnitControl!;
        var grant = Assert.Single(lease.Grants); var marker = Assert.Single(state.UntilEndOfTurnEffects,
            e => e.StartsWith(CoreRuleEngine.DelayedControlLossPrefix, StringComparison.Ordinal));
        var forged = mutation switch {
            "controller" => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY"] = unit with { ControllerId = "P2", UnitControl = lease with { Grants = [grant with { ControllerId = "P2" }] } } } },
            "generation" => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY"] = unit with { UnitControl = lease with { Unit = lease.Unit with { Generation = lease.Unit.Generation + 1 } } } } },
            "grant" => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY"] = unit with { UnitControl = lease with { Grants = [grant with { CreatingStackItemId = "FORGED" }] } } } },
            "erase-delay" => state with { UntilEndOfTurnEffects = state.UntilEndOfTurnEffects.Where(e => e != marker).ToArray() },
            _ => state with { UntilEndOfTurnEffects = state.UntilEndOfTurnEffects.Append(marker).ToArray() } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await game.Act("P1", new EndTurnCommand()); await FinishEnd(game, "P1"); await game.Replay();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task NullOriginalControlCaptureRejectsRecoveryBeforeAndAfterRetirementWithoutThrowing(bool retired)
    {
        var game = new Driver(Position()); await Steal(game);
        if (retired) { await game.Act("P1", new EndTurnCommand()); await FinishEnd(game, "P1"); }
        var state = game.State;
        var commands = game.Commands(); var originalEvents = game.Events();
        var captures = originalEvents.Where(record => record.Event.Kind == CoreRuleEngine.UnitControlCaptured).ToArray();
        Assert.NotEmpty(captures);
        Assert.Equal(retired, state.CardObjects["ENEMY"].UnitControl is null);
        var before = MatchStateHasher.HashValue(state);
        var commandHash = MatchStateHasher.HashValue(commands); var eventHash = MatchStateHasher.HashValue(originalEvents);
        var sequence = commands.Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(
            player, state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        Assert.Empty(MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, originalEvents, views, state, state.Tick, spectator));
        foreach (var captured in captures)
        {
            var malformed = originalEvents.Select(record => record.Sequence == captured.Sequence
                ? record with { Event = record.Event with { Payload = null! } } : record).ToArray();
            var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, malformed, views, state, state.Tick, spectator);
            Assert.Contains(errors, error => error.Contains("payload is required", StringComparison.Ordinal));
            Assert.Contains("unit control receipt lacks its original accepted command", errors);
            var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, commands, malformed, views, errors,
                AuthoritativeState: state, SpectatorReplayFrame: spectator) { OpeningSeed = state.Seed };
            Assert.Throws<MatchSessionException>(() => MatchSession.Restore(frame, new CoreRuleEngine(), game));
        }
        Assert.Equal(before, MatchStateHasher.HashValue(game.State));
        Assert.Equal(commandHash, MatchStateHasher.HashValue(game.Commands()));
        Assert.Equal(eventHash, MatchStateHasher.HashValue(game.Events()));
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Theory]
    [InlineData(false, "list")] [InlineData(true, "list")]
    [InlineData(false, "first")] [InlineData(true, "first")]
    [InlineData(false, "last")] [InlineData(true, "last")]
    public async Task ARealControlGrantRejectsNullEffectsInEitherRecoveryFrameWithoutThrowing(bool inOrigin, string malformed)
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "BIRDS", "UNL-044/219", "P1", "HAND"));
        await Steal(game); await ToMain(game);
        await game.Act("P1", new PlayCardCommand("BIRDS", "UNL-044/219", [], Mode: "CREATE_WARHAWKS"));
        await game.Top();
        var pending = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        var frame = inOrigin ? pending.Origin : game.State;
        Assert.Contains(frame.UntilEndOfTurnEffects,
            effect => effect.StartsWith(CoreRuleEngine.DelayedControlLossPrefix, StringComparison.Ordinal));
        Assert.NotNull(frame.CardObjects["ENEMY"].UnitControl);
        IReadOnlyList<string> effects = malformed switch {
            "list" => null!, "first" => [null!, ..frame.UntilEndOfTurnEffects],
            _ => [..frame.UntilEndOfTurnEffects, null!] };
        var corrupted = frame with { UntilEndOfTurnEffects = effects };
        var forged = inOrigin ? game.State with { PendingRuleChoice = pending with { Origin = corrupted } } : corrupted;
        var before = MatchStateHasher.HashValue(game.State);
        var commands = MatchStateHasher.HashValue(game.Commands()); var events = MatchStateHasher.HashValue(game.Events());
        // Preserve the actual views and accepted journal. The malformed copy
        // must be rejected before it can be restored or certify any receipt.
        var sequence = game.Commands().Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(game.State); var prompts = ResolutionResult.BuildPrompts(game.State);
        var views = game.State.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(
            player, game.State.Tick, sequence, snapshots[player], game.State.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, sequence, [], game.State);
        var errors = MatchRecoveryValidator.Validate(game.State.RoomId, sequence,
            game.Commands(), game.Events(), views, forged, game.State.Tick, spectator);
        Assert.Contains("unit control history contains a null effect list or value", errors);
        var recovery = new MatchRecoveryFrame(game.State.RoomId, game.State.Tick, sequence, game.Commands(), game.Events(),
            views, errors, AuthoritativeState: forged, SpectatorReplayFrame: spectator) { OpeningSeed = game.State.Seed };
        Assert.Throws<MatchSessionException>(() => MatchSession.Restore(recovery, new CoreRuleEngine(), game));
        Assert.Equal(before, MatchStateHasher.HashValue(game.State));
        Assert.Equal(commands, MatchStateHasher.HashValue(game.Commands())); Assert.Equal(events, MatchStateHasher.HashValue(game.Events()));
        for (var step = 0; game.State.PendingRuleChoice is { } choice; step++)
        {
            Assert.InRange(step, 0, 3);
            await game.Act(choice.Request.PlayerId, new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["BASE"]));
        }
        if (game.State.StackItems.Count > 0) await game.Top();
        await game.ColdCheckpoint(); await game.Replay();
    }
}

using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class YashilaDelayedMovementTests
{
    private const string Yashila = "UNL-050/219";
    private static MatchState Position()
    {
        var s = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·278/298"), "SOURCE", Yashila, "P1", "BATTLEFIELD", "BF");
        s = RadGearTestPosition.Put(s, "DRAWER", "SFD·027/221", "P1", "BATTLEFIELD", "BF");
        s = RadGearTestPosition.Put(s, "RETURN", "OGN·104/298", "P1", "HAND");
        s = RadGearTestPosition.Put(s, "BLINK", "OGS·011/024", "P1", "HAND");
        s = RadGearTestPosition.Put(s, "COUNTER", "SFD·045/221", "P2", "HAND");
        for (var i = 0; i < 3; i++) s = RadGearTestPosition.Put(s, "P2-RUNE" + i, "UNL-R02", "P2", "BASE");
        return s with { Phase = MatchPhases.TurnStart, TurnStartStep = "START",
            TimingState = TimingStates.NeutralClosed, PriorityPlayerId = null, PassedPriorityPlayerIds = [] };
    }
    private static async Task Schedule(Driver game, bool alreadyQueued = false)
    {
        if (!alreadyQueued) await game.Act("P1", new PassPriorityCommand());
        Assert.Equal(2, game.State.TriggerQueue.Count);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue
            .OrderBy(t => t.SourceObjectId == "SOURCE" ? 0 : 1).Select(t => t.TriggerId).ToArray()));
        Assert.Equal("SOURCE", game.State.StackItems[^1].SourceObjectId);
        Assert.Null(game.State.PendingCardChoice); await game.Top();
        var schedule = Assert.Single(game.State.ScheduledUnitEvents!);
        Assert.Equal("P1", schedule.ControllerId); Assert.Null(schedule.Receipt.FiredTick);
        Assert.Equal("BF", schedule.Context.TriggeringBattlefield!.ObjectId);
        Assert.Equal(MatchPhases.TurnStart, game.State.Phase);
        Assert.Equal("DRAWER", Assert.Single(game.State.StackItems).SourceObjectId);
    }
    private static async Task AdvanceToMain(Driver game)
    {
        for (var n = 0; game.State.Phase != MatchPhases.Main; n++)
        {
            Assert.InRange(n, 0, 12);
            if (game.State.PriorityPlayerId is not null) await game.Top();
            else if (game.State.SpellDuelState.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
            else Assert.Fail("Expected a pending hold or duel before MAIN.");
        }
        Assert.Null(game.State.ScheduledUnitEvents);
        Assert.Equal("P1", game.State.PendingCardChoice!.PlayerId);
        Assert.Equal(0, game.State.PendingCardChoice.RequiredCount);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
    }
    private static Task Choose(Driver game, params string[] ids)
    {
        var choice = game.State.PendingCardChoice!;
        return game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
    }

    [Fact]
    public async Task ActualHoldCreatesADelayThenTheRealMainTransitionConfirmsAnOptionalEnemyMove()
    {
        var game = new Driver(Position()); await Schedule(game);
        var original = Assert.Single(game.State.ScheduledUnitEvents!);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        await AdvanceToMain(game); var skill = Assert.Single(game.State.StackItems);
        Assert.Equal(original.Context.CapturedTick, skill.UnitEvent!.CapturedTick);
        Assert.True(skill.UnitEvent.DelayedReceipt!.FiredTick > skill.UnitEvent.CapturedTick);
        await Choose(game, "ENEMY"); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        await game.Top(); Assert.Equal("BF", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.Single(game.Events(), e => e.Event.Kind == "DELAYED_UNIT_EVENT_TRIGGERED"); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ASourceReturningOrMovingAwayBeforeMainDoesNotChangeTheCapturedBattlefield(bool returnToHand)
    {
        var game = new Driver(Position()); await Schedule(game);
        await game.Act("P1", new PlayCardCommand(returnToHand ? "RETURN" : "BLINK",
            returnToHand ? "OGN·104/298" : "OGS·011/024", ["SOURCE"])); await game.Top();
        Assert.Contains("SOURCE", returnToHand ? game.State.PlayerZones["P1"].Hand : game.State.PlayerZones["P1"].Base);
        await AdvanceToMain(game);
        if (returnToHand) Assert.DoesNotContain("SOURCE", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await Choose(game, "ENEMY"); await game.Top();
        Assert.Equal("BF", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task ARealControlChangeCannotTransferOrCancelTheAlreadyCreatedDelay()
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·278/298"), "SOURCE", Yashila, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "ARMAMENT", "SFD·030/221", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "TAKE", "OGN·203/298", "P2", "HAND");
        for (var i = 0; i < 11; i++) state = RadGearTestPosition.Put(state, "THEIR-RUNE" + i,
            i < 3 ? "UNL-R05" : "UNL-R01", "P2", "BASE");
        var game = new Driver(state);
        await game.Act("P1", new AssembleEquipmentCommand("ARMAMENT", "SOURCE", ["ASSEMBLE_RED"])); await game.Top();
        for (var i = 0; game.State.SpellDuelState.IsActive; i++)
        {
            Assert.InRange(i, 0, 6);
            await game.Act(game.State.SpellDuelState.FocusPlayerId!, new PassFocusCommand());
        }
        // Ariaon's Fall makes the real conquest also earn Yashila's HOLD.
        // This MAIN has already begun, so the delay must survive until the next own MAIN.
        await game.Act("P1", new MoveUnitCommand("SOURCE", "BASE", "BATTLEFIELD:OTHER"));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        Assert.NotNull(Assert.Single(game.State.StackItems).TimingAlias); await game.Top();
        Assert.Equal(MatchPhases.Main, game.State.Phase);
        var schedule = Assert.Single(game.State.ScheduledUnitEvents!);
        Assert.Null(schedule.Receipt.FiredTick); Assert.Equal("OTHER", schedule.Context.TriggeringBattlefield!.ObjectId);
        await game.Act("P1", new EndTurnCommand());
        string[] mana = Enumerable.Range(3, 8).Select(i => "THEIR-RUNE" + i).ToArray();
        await game.Act("P2", new TapRuneCommand(mana[0], mana));
        for (var i = 0; i < 3; i++) await game.Act("P2", new RecycleRuneCommand("THEIR-RUNE" + i));
        await game.Act("P2", new PlayCardCommand("TAKE", "OGN·203/298", ["SOURCE"])); await game.Top();
        Assert.Equal("P2", game.State.CardObjects["SOURCE"].ControllerId);
        Assert.Contains("SOURCE", game.State.PlayerZones["P2"].Base);
        Assert.Equal("P1", Assert.Single(game.State.ScheduledUnitEvents!).ControllerId);
        await game.Act("P2", new EndTurnCommand()); await AdvanceToMain(game);
        Assert.Equal("P2", game.State.CardObjects["SOURCE"].ControllerId);
        Assert.Equal("P1", Assert.Single(game.State.StackItems).ControllerId);
        await Choose(game, "ENEMY"); await game.Top();
        Assert.Equal("OTHER", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task DecliningAtMainConsumesTheDelayWithoutMovingAnyUnit()
    {
        var game = new Driver(Position()); await Schedule(game); await AdvanceToMain(game); await Choose(game);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.ScheduledUnitEvents);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base); await game.Replay();
    }

    [Fact]
    public async Task TheIndependentMainMovementSkillCanBeCounteredAfterTheEnemyIsConfirmed()
    {
        var game = new Driver(Position()); await Schedule(game); await AdvanceToMain(game); await Choose(game, "ENEMY");
        var id = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new TapRuneCommand("P2-RUNE0", ["P2-RUNE0", "P2-RUNE1"]));
        await game.Act("P2", new RecycleRuneCommand("P2-RUNE2"));
        await game.Act("P2", new PlayCardCommand("COUNTER", "SFD·045/221", [id])); await game.Top();
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.ScheduledUnitEvents);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base); await game.Replay();
    }

    [Theory]
    [InlineData("erase")] [InlineData("duplicate")] [InlineData("tick")] [InlineData("controller")]
    public async Task RecoveryRejectsErasedDuplicatedOrForgedScheduledHistory(string change)
    {
        var game = new Driver(Position()); await Schedule(game);
        var schedule = Assert.Single(game.State.ScheduledUnitEvents!);
        var forged = game.State with { ScheduledUnitEvents = change switch {
            "erase" => null, "duplicate" => [schedule, schedule],
            "tick" => [schedule with { Receipt = schedule.Receipt with { CreatedTick = schedule.Receipt.CreatedTick + 1 } }],
            _ => [schedule with { ControllerId = "P2" }] } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Replay();
    }

    [Theory]
    [InlineData("erase")] [InlineData("battlefield")] [InlineData("tick")]
    public async Task RecoveryRequiresTheNewMainBirthAndTheUnchangedOriginalBattlefield(string change)
    {
        var game = new Driver(Position()); await Schedule(game); await AdvanceToMain(game);
        var item = Assert.Single(game.State.StackItems); var context = item.UnitEvent!;
        var forged = game.State with { StackItems = change == "erase" ? [] : [item with {
            UnitEvent = change == "battlefield" ? context with { TriggeringBattlefield = new("OTHER", 0) }
                : context with { DelayedReceipt = context.DelayedReceipt! with { FiredTick = context.CapturedTick } } }],
            PendingCardChoice = change == "erase" ? null : game.State.PendingCardChoice };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await Choose(game); await game.Replay();
    }

    [Fact]
    public async Task AStandaloneHoldCanCreateAndFireItsDelayInTheSameAcceptedCompletion()
    {
        var state = RadUnitEffectTestFixture.Place(Position(), "DRAWER", "P1", "BASE");
        var game = new Driver(state); await game.Act("P1", new PassPriorityCommand());
        Assert.Equal("SOURCE", Assert.Single(game.State.StackItems).SourceObjectId);
        await game.Top(); await AdvanceToMain(game);
        var receipt = Assert.Single(game.State.StackItems).UnitEvent!.DelayedReceipt!;
        Assert.Equal(receipt.CreatedTick, receipt.FiredTick);
        await Choose(game, "ENEMY"); await game.Top();
        Assert.Equal("BF", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task TheEarnedHoldStillCreatesTheOriginalBattlefieldDelayAfterTheSourceReturnsInResponse()
    {
        var game = new Driver(Position()); await game.Act("P1", new PassPriorityCommand());
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue
            .OrderBy(t => t.SourceObjectId == "SOURCE" ? 0 : 1).Select(t => t.TriggerId).ToArray()));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["SOURCE"])); await game.Top();
        Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Hand);
        await game.Top(); var schedule = Assert.Single(game.State.ScheduledUnitEvents!);
        var created = Assert.Single(game.Events(), e => e.Event.Kind == "DELAYED_UNIT_EVENT_CREATED");
        Assert.DoesNotContain("SOURCE", JsonSerializer.Serialize(created.Event.Payload));
        Assert.Equal(MatchStateHasher.HashValue(schedule), created.Event.Payload["scheduledUnitEventHash"]);
        await AdvanceToMain(game);
        var queued = Assert.Single(game.Events(), e => e.Event.Kind == "TRIGGER_QUEUED"
            && Equals(e.Event.Payload.GetValueOrDefault("triggeredByEventKind"), "MAIN_PHASE_BEGAN"));
        Assert.Equal("HIDDEN", queued.Event.Payload["sourceObjectId"]);
        await Choose(game, "ENEMY"); await game.Top();
        Assert.Equal("BF", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId); await game.Replay();
    }
}

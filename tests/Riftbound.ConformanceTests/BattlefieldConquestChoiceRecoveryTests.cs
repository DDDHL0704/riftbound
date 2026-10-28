using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BattlefieldConquestChoiceRecoveryTests
{
    [Fact]
    public async Task ShiranaReceiptRequiresTheActualAcceptedBoonPayment()
    {
        var initial = ThreeBattlefieldTimingProbe.Position("P1", "OGN·282/298", boon: true);
        var driver = await Conquer(initial);
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Contains("ALLY", choice.LegalObjectIds);
        var simulated = await new CoreRuleEngine().ResolveAsync(driver.State,
            new("unjournalled-boon", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]), default);
        Assert.True(simulated.Accepted, simulated.ErrorMessage);
        Assert.NotNull(Assert.Single(simulated.State.StackItems, i => i.BattlefieldConquestConsumeBoon is not null).TriggerCost?.ConsumedBoon);

        // The original journal still contains no accepted cost choice; restore
        // the unspent boon and forge the paid stack and all public views.
        var forged = simulated.State with { Tick = driver.State.Tick, CardObjects = driver.State.CardObjects };
        Assert.Contains(CardObjectTags.Boon, forged.CardObjects["ALLY"].Tags);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "BOON_CONSUMED");
        var frame = Frame(initial, driver, forged);
        Assert.DoesNotContain(frame.ValidationErrors, e => e.Contains("invalid trigger cost receipt", StringComparison.Ordinal));
        Assert.Contains(frame.ValidationErrors, e => e.Contains("boon cost", StringComparison.OrdinalIgnoreCase)
            && e.Contains("payment history", StringComparison.OrdinalIgnoreCase));
        Assert.Throws<MatchSessionException>(() => MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, forged,
            new CoreRuleEngine(), default, frame.Events);
        Assert.False(replay.IsMatch);
    }

    [Fact]
    public async Task MoonveilTargetRequiresTheActualAcceptedConfirmation()
    {
        var initial = ThreeBattlefieldTimingProbe.Position("P1", "SFD·221/221", equipment: true);
        var driver = await Conquer(initial);
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Contains("EQUIP", choice.LegalObjectIds);
        var simulated = await new CoreRuleEngine().ResolveAsync(driver.State,
            new("unjournalled-equipment", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["EQUIP"]), default);
        Assert.True(simulated.Accepted, simulated.ErrorMessage);
        var forged = simulated.State with { Tick = driver.State.Tick };
        var target = Assert.Single(forged.StackItems, i => i.BattlefieldConquestReadyEquipment is not null);
        Assert.Equal(new[] { "EQUIP" }, target.TargetObjectIds);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "TRIGGER_CONFIRMED"
            && Equals(e.Event.Payload.GetValueOrDefault("stackItemId"), target.StackItemId));
        var frame = Frame(initial, driver, forged);
        Assert.DoesNotContain(frame.ValidationErrors, e => e.Contains("invalid captured battlefield conquest choice stack", StringComparison.Ordinal));
        Assert.Contains(frame.ValidationErrors, e => e.Contains("equipment target", StringComparison.OrdinalIgnoreCase)
            && e.Contains("confirmation history", StringComparison.OrdinalIgnoreCase));
        Assert.Throws<MatchSessionException>(() => MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, forged,
            new CoreRuleEngine(), default, frame.Events);
        Assert.False(replay.IsMatch);
    }

    private static async Task<StandbyDefendObserverTests.Driver> Conquer(MatchState initial)
    {
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P1", new PassFocusCommand());
        await driver.Act("P2", new PassFocusCommand());
        return driver;
    }

    private static MatchRecoveryFrame Frame(MatchState initial, StandbyDefendObserverTests.Driver driver, MatchState state)
    {
        var sequence = driver.Commands().Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state);
        var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id,
            state.Tick, sequence, snapshots[id], state.Tick, sequence, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, driver.Commands(), driver.Events(),
            views, state, state.Tick, spectator);
        return new(state.RoomId, state.Tick, sequence, driver.Commands(), driver.Events(), views, errors, state, initial, spectator)
            { OpeningSeed = initial.Seed };
    }
}

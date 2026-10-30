using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BattlefieldConquestChoiceRecoveryTests
{
    [Theory]
    [InlineData("P1", "OGN·282/298", "ALLY")]
    [InlineData("P2", "OGN·282/298", "ALLY")]
    [InlineData("P1", "SFD·221/221", "EQUIP")]
    [InlineData("P2", "SFD·221/221", "EQUIP")]
    public async Task ConquestTypedPaddedCardChoiceSurvivesColdRecoveryAndReplay(string player, string card, string target)
    {
        var initial = ThreeBattlefieldTimingProbe.Position(player, card,
            boon: target == "ALLY", equipment: target == "EQUIP");
        var driver = await Conquer(initial, player);
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        var session = MatchSession.Restore(Frame(initial, driver, driver.State), new CoreRuleEngine(), driver);
        var command = new ChooseCardsCommand(" \t" + choice.ChoiceId + " ",
            " " + choice.ChoiceWindow + "\n", [" \t" + target + " \n"]);
        var result = await session.SubmitAsync(player, "padded-conquest-card-choice", command, null, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(driver.Commands().Last().RawCommand);
        Assert.NotNull(driver.Commands().Last().TypedCommand);
        Assert.Null(result.State.PendingCardChoice);
        var item = Assert.Single(result.State.StackItems);
        if (target == "ALLY")
        {
            Assert.Equal(target, item.TriggerCost!.ConsumedBoon!.ObjectId);
            Assert.DoesNotContain(CardObjectTags.Boon, result.State.CardObjects[target].Tags);
        }
        else
        {
            Assert.Equal(new[] { target }, item.TargetObjectIds);
            Assert.True(result.State.CardObjects[target].IsExhausted);
        }
        var frame = Frame(initial, driver, result.State);
        Assert.Empty(frame.ValidationErrors);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, options), options)!;
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), NoopMatchJournal.Instance);
        Assert.Equal(MatchStateHasher.HashValue(result.Snapshots[player]), MatchStateHasher.HashValue(restored.SnapshotFor(player)));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), result.State,
            new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Theory]
    [InlineData("OGN·282/298", "ALLY", "BOON_CONSUMED", "targetObjectId")]
    [InlineData("SFD·221/221", "EQUIP", "TRIGGER_CONFIRMED", "targetObjectIds")]
    public async Task ConquestChoiceRecoveryRejectsNoncanonicalEventTargets(string card, string target, string eventKind, string property)
    {
        var initial = ThreeBattlefieldTimingProbe.Position("P1", card,
            boon: target == "ALLY", equipment: target == "EQUIP");
        var driver = await Conquer(initial);
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        var result = await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        Assert.Single(result.Events, e => e.Kind == eventKind);
        var frame = Frame(initial, driver, driver.State);
        Assert.Empty(frame.ValidationErrors);
        var forged = frame with { Events = frame.Events.Select(e => e.Event.Kind == eventKind
            ? e with { Event = e.Event with { Payload = new Dictionary<string, object?>(e.Event.Payload) {
                [property] = property == "targetObjectIds" ? new[] { " " + target + " " } : " " + target + " " } } }
            : e).ToArray() };
        var errors = MatchRecoveryValidator.Validate(forged.RoomId, forged.LastEventSequence,
            forged.Commands, forged.Events, forged.PlayerViews, forged.AuthoritativeState, forged.CurrentTick, forged.SpectatorReplayFrame);
        Assert.Contains(errors, e => e.Contains(target == "ALLY" ? "boon cost" : "equipment target", StringComparison.Ordinal));
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var persisted = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(forged, options), options)!;
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance,
            new ConquestChoiceRecoveryStore(persisted));
        var error = await Assert.ThrowsAsync<MatchSessionException>(async () => await registry.GetOrCreateAsync(forged.RoomId, default));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains(target == "ALLY" ? "boon cost" : "equipment target", error.Message, StringComparison.Ordinal);
        Assert.Null(await registry.FindAsync(forged.RoomId, default));
    }

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

    private static async Task<StandbyDefendObserverTests.Driver> Conquer(MatchState initial, string player = "P1")
    {
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act(player, new PassFocusCommand());
        await driver.Act(ThreeBattlefieldTimingProbe.Other(player), new PassFocusCommand());
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

    private sealed class ConquestChoiceRecoveryStore(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken cancellationToken)
            => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }
}

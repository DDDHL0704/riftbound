using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class BattleDamageAssignmentLifecycleTests
{
    [Fact]
    public async Task VayneTypedPaddedPaymentSurvivesColdRecoveryAndReplay()
    {
        const string spend = "SPEND_MANA:1";
        var (initial, game, _) = await OpenVaynePaymentTimingBattle(mana: 1);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        var session = MatchSession.Restore(VayneRecoveryFrame(initial, game, game.State),
            new CoreRuleEngine(), game);
        var command = new PayCostCommand(" \t" + payment.PaymentId + " ",
            " " + payment.PaymentWindow + "\n", [" \t" + spend + " \n"]);
        var paid = await session.SubmitAsync("P1", "padded-vayne-payment", command, null, default);
        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(game.Commands().Last().RawCommand);
        Assert.NotNull(game.Commands().Last().TypedCommand);
        Assert.Equal(new[] { spend }, Assert.Single(paid.Events, e => e.Kind == "COST_PAID").Payload["paymentChoiceIds"]);
        Assert.Contains(AttackerObjectId, paid.State.PlayerZones["P1"].Battlefields);
        var frame = VayneRecoveryFrame(initial, game, paid.State);
        Assert.Empty(frame.ValidationErrors);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, options), options)!;
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), NoopMatchJournal.Instance);
        Assert.Equal(MatchStateHasher.HashValue(paid.Snapshots["P1"]), MatchStateHasher.HashValue(restored.SnapshotFor("P1")));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), paid.State,
            new CoreRuleEngine(), default, JournalRoundTrip(game.Events()));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Fact]
    public async Task VayneTypedPaddedPaymentAliasIsRejectedAfterColdRecoveryWithoutMutation()
    {
        var (initial, game, _) = await OpenVaynePaymentTimingBattle(mana: 1);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.DoesNotContain("PAY", payment.LegalPaymentChoiceIds);
        var session = MatchSession.Restore(VayneRecoveryFrame(initial, game, game.State),
            new CoreRuleEngine(), game);
        var before = MatchStateHasher.Hash(game.State);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(game.State));
        var command = new PayCostCommand(" \t" + payment.PaymentId + " ",
            " " + payment.PaymentWindow + "\n", [" \tPAY \n"]);

        var rejected = await session.SubmitAsync("P1", "padded-vayne-payment-alias", command, null, default);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, rejected.ErrorCode);
        Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(rejected.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(rejected.State)));
        var frame = VayneRecoveryFrame(initial, game, rejected.State);
        Assert.Empty(frame.ValidationErrors);
        MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), rejected.State,
            new CoreRuleEngine(), default, JournalRoundTrip(game.Events()));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Fact]
    public async Task VayneForgedPaidReceiptWithoutPaymentHistoryCannotRestore()
    {
        var (initial, game, _) = await OpenVaynePaymentTimingBattle();
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        var simulated = await new CoreRuleEngine().ResolveAsync(game.State,
            new("vayne-not-journalled-payment", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:1"]), default);
        Assert.True(simulated.Accepted, simulated.ErrorMessage);
        Assert.NotNull(Assert.Single(simulated.State.StackItems, s => s.SourceObjectId == AttackerObjectId).TriggerCost);

        // Keep the original journal and unspent resources; forge the paid continuation,
        // its tick, and all player/spectator views consistently.
        var forged = simulated.State with { Tick = game.State.Tick, RunePools = game.State.RunePools };
        Assert.Null(forged.PendingPayment);
        Assert.Equal(1, forged.RunePools["P1"].Mana);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("paymentId"), payment.PaymentId));
        var frame = VayneRecoveryFrame(initial, game, forged);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, forged,
            new CoreRuleEngine(), default, JournalRoundTrip(frame.Events));
        Assert.False(replay.IsMatch);
        Assert.DoesNotContain(frame.ValidationErrors, e => e.Contains("invalid trigger cost receipt", StringComparison.Ordinal));
        Assert.Contains(frame.ValidationErrors, e => e.Contains("payment", StringComparison.OrdinalIgnoreCase)
            && e.Contains("history", StringComparison.OrdinalIgnoreCase));
        Assert.Throws<MatchSessionException>(() => MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance));
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var persisted = JsonSerializer.Deserialize<MatchRecoveryFrame>(
            JsonSerializer.Serialize(frame with { ValidationErrors = [] }, options), options)!;
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance,
            new ConquestContinuationRecoveryStore(persisted));
        var error = await Assert.ThrowsAsync<MatchSessionException>(async () => await registry.GetOrCreateAsync(forged.RoomId, default));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("payment history", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await registry.FindAsync(forged.RoomId, default));
    }

    [Fact]
    public async Task VayneRecoveryBindsSourceGenerationToAcceptedConquestHistory()
    {
        var (baseline, original, _) = await OpenVaynePaymentTimingBattle();
        var initial = baseline with { CardObjects = new Dictionary<string, CardObjectState>(baseline.CardObjects) {
            [AttackerObjectId] = baseline.CardObjects[AttackerObjectId] with { ObjectGeneration = 3 } } };
        var game = new StandbyDefendObserverTests.Driver(initial);
        foreach (var command in original.Commands())
            await game.Act(command.PlayerId, GameCommandJsonMapper.Map(command.RawCommand!.Value));

        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        var item = Assert.Single(game.State.StackItems, s => s.StackItemId == payment.ResolvingStackItemId);
        var captured = Assert.IsType<UnitConquestReturnContext>(item.UnitConquestReturn);
        Assert.Equal(3, captured.Source.Generation);
        var birth = Assert.Single(game.Events(), e => e.Event.Kind == "TRIGGER_QUEUED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), AttackerObjectId));
        Assert.Equal(3L, birth.Event.Payload["sourceGeneration"]);

        var earlier = captured with { Source = captured.Source with { Generation = 0 } };
        var triggerId = $"UNIT-CONQUEST-RETURN:{earlier.ConquestTick}:{MatchStateHasher.HashValue(new {
            earlier.Source, earlier.ControllerId, earlier.BattlefieldObjectId, earlier.Occurrence })}";
        var stackId = "ordered-" + triggerId;
        var forged = game.State with {
            StackItems = game.State.StackItems.Select(s => s.StackItemId == item.StackItemId
                ? s with { StackItemId = stackId, UnitConquestReturn = earlier } : s).ToArray(),
            PendingPayment = payment with { PaymentId = "TRIGGER-COST:" + stackId, ResolvingStackItemId = stackId } };
        var frame = VayneRecoveryFrame(initial, game, forged);
        Assert.Contains(frame.ValidationErrors, e => e.Contains("not bound to accepted conquest history", StringComparison.Ordinal));
        Assert.DoesNotContain(frame.ValidationErrors, e => e.Contains("invalid captured unit conquest return stack", StringComparison.Ordinal));
        Assert.Throws<MatchSessionException>(() => MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance));

        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), forged,
            new CoreRuleEngine(), default, game.Events());
        Assert.False(replay.IsMatch);
        var changedBirth = game.Events().Select(e => e == birth ? e with { Event = e.Event with {
            Payload = new Dictionary<string, object?>(e.Event.Payload) {
                ["sourceGeneration"] = 0L, ["triggerId"] = triggerId } } } : e).ToArray();
        var changedReplay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), forged,
            new CoreRuleEngine(), default, changedBirth);
        Assert.False(changedReplay.IsMatch);
        Assert.Contains(changedReplay.Errors, e => e.Contains("payload hash", StringComparison.Ordinal));
    }

    [Fact]
    public async Task VaynePublicViewsRedactSourceAfterResponseReturnsIt()
    {
        var (initial, game, _) = await OpenVaynePaymentTimingBattle(responseCard: true);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:1"]));
        if (game.State.PriorityPlayerId == "P2") await game.Act("P2", new PassPriorityCommand());
        Assert.Equal("P1", game.State.PriorityPlayerId);
        await game.Act("P1", new PlayCardCommand("VAYNE-RESPONSE", "OGN·104/298", [AttackerObjectId]));
        await game.Top();

        var item = Assert.Single(game.State.StackItems, s => s.UnitConquestReturn is not null);
        var frame = VayneRecoveryFrame(initial, game, game.State);
        Assert.Empty(frame.ValidationErrors);
        var ownerView = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(Assert.Single(frame.PlayerViews["P1"].Snapshot.Stack));
        var opponentView = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(Assert.Single(frame.PlayerViews["P2"].Snapshot.Stack));
        var spectatorView = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(Assert.Single(frame.SpectatorReplayFrame!.SpectatorSnapshot.Stack));
        Assert.Equal(AttackerObjectId, ownerView["sourceObjectId"]);
        Assert.Equal("HIDDEN", opponentView["sourceObjectId"]);
        Assert.Equal("HIDDEN", spectatorView["sourceObjectId"]);
        Assert.Equal(item.EffectKind, opponentView["effectKind"]);
        Assert.DoesNotContain("unitConquestReturn", opponentView.Keys);

        var leaked = new Dictionary<string, object?>(opponentView) { ["unitConquestReturn"] = item.UnitConquestReturn };
        var views = new Dictionary<string, RecoveredPlayerView>(frame.PlayerViews) {
            ["P2"] = frame.PlayerViews["P2"] with { Snapshot = frame.PlayerViews["P2"].Snapshot with { Stack = [leaked] } } };
        var errors = MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events,
            views, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);
        Assert.Contains(errors, e => e.Contains("must not expose the private unit conquest return binding", StringComparison.Ordinal));
    }
}

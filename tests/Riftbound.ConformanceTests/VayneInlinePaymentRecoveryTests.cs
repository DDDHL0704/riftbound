using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class BattleDamageAssignmentLifecycleTests
{
    [Theory]
    [InlineData("PAY")]
    [InlineData("SPEND_MANA:1")]
    public async Task VayneTypedPaddedInlinePaymentSurvivesColdRecoveryAndReplay(string spend)
    {
        var (initial, game, _) = await OpenVaynePaymentTimingBattle(mana: 0, includeRune: true);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        var session = MatchSession.Restore(VayneRecoveryFrame(initial, game, game.State),
            new CoreRuleEngine(), game);
        var choices = new[] { "TAP_RUNE:VAYNE-RUNE", spend };
        var command = new PayCostCommand(" \t" + payment.PaymentId + " ",
            " " + payment.PaymentWindow + "\n", choices.Select(choice => " \t" + choice + " \n").ToArray());
        var paid = await session.SubmitAsync("P1", "padded-vayne-inline-payment", command, null, default);
        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(game.Commands().Last().RawCommand);
        Assert.NotNull(game.Commands().Last().TypedCommand);
        Assert.True(paid.State.CardObjects["VAYNE-RUNE"].IsExhausted);
        Assert.Equal(0, paid.State.RunePools["P1"].Mana);
        Assert.Equal(choices, Assert.Single(paid.Events, e => e.Kind == "COST_PAID").Payload["paymentChoiceIds"]);
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

    [Theory]
    [InlineData(0, "PAY")]
    [InlineData(0, "SPEND_MANA:1")]
    [InlineData(1, "SPEND_MANA:1")]
    public async Task VayneInlineRunePaymentSurvivesColdRecoveryAndReplay(int mana, string spend)
    {
        var (initial, game, _) = await OpenVaynePaymentTimingBattle(mana: mana, includeRune: true);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        var choices = new[] { "TAP_RUNE:VAYNE-RUNE", spend };
        var paid = await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, choices));
        Assert.True(game.State.CardObjects["VAYNE-RUNE"].IsExhausted);
        Assert.Equal(mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(choices, Assert.Single(paid.Events, e => e.Kind == "COST_PAID").Payload["paymentChoiceIds"]);
        Assert.DoesNotContain(paid.Events, e => e.Kind == "UNIT_RETURNED_TO_HAND");
        Assert.Contains(AttackerObjectId, game.State.PlayerZones["P1"].Battlefields);
        AssertVayneJsonColdFrame(initial, game);
        await game.Top();
        Assert.Contains(AttackerObjectId, game.State.PlayerZones["P1"].Hand);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), game.State,
            new CoreRuleEngine(), default, JournalRoundTrip(game.Events()));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Theory]
    [InlineData("playerId", "P2")]
    [InlineData("sourceObjectId", "OTHER-RUNE")]
    [InlineData("paymentId", "OTHER-PAYMENT")]
    [InlineData("paymentWindow", "OTHER-WINDOW")]
    [InlineData("abilityId", "OTHER-ABILITY")]
    [InlineData("mana", 2)]
    public async Task VayneInlinePaymentRecoveryRejectsUnboundRuneEvents(string property, object value)
    {
        var (initial, game, _) = await OpenVaynePaymentTimingBattle(mana: 0, includeRune: true);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow,
            ["TAP_RUNE:VAYNE-RUNE", "SPEND_MANA:1"]));
        var frame = VayneRecoveryFrame(initial, game, game.State);
        var forged = frame with { Events = frame.Events.Select(e => e.Event.Kind == "RUNE_TAPPED"
            ? e with { Event = e.Event with { Payload = new Dictionary<string, object?>(e.Event.Payload) {
                [property] = value } } } : e).ToArray() };
        AssertVaynePaymentHistoryRejected(forged);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VayneInlinePaymentRecoveryRejectsUnrecordedOrEarlierRuneAction(bool earlierTap)
    {
        var (initial, game, _) = await OpenVaynePaymentTimingBattle(mana: 1, includeRune: true);
        if (earlierTap) await game.Act("P1", new TapRuneCommand("VAYNE-RUNE"));
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:1"]));
        var frame = VayneRecoveryFrame(initial, game, game.State);
        var choices = new[] { "TAP_RUNE:VAYNE-RUNE", "SPEND_MANA:1" };
        var raw = JsonSerializer.SerializeToElement(new PayCostCommand(payment.PaymentId, payment.PaymentWindow, choices),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var last = frame.Commands.Last();
        var forged = frame with {
            Commands = frame.Commands.Select(c => c == last ? c with { RawCommand = raw, TypedCommand = null } : c).ToArray(),
            Events = frame.Events.Select(e => e.Event.Kind == "COST_PAID"
                ? e with { Event = e.Event with { Payload = new Dictionary<string, object?>(e.Event.Payload) {
                    ["paymentChoiceIds"] = choices } } } : e).ToArray() };
        AssertVaynePaymentHistoryRejected(forged);
    }

    [Fact]
    public async Task VayneInlinePaymentRecoveryRejectsNoncanonicalReceiptChoices()
    {
        var (initial, game, _) = await OpenVaynePaymentTimingBattle(mana: 0, includeRune: true);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        var choices = new[] { "TAP_RUNE:VAYNE-RUNE", "SPEND_MANA:1" };
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, choices));
        var frame = VayneRecoveryFrame(initial, game, game.State);
        var forged = frame with { Events = frame.Events.Select(e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("paymentId"), payment.PaymentId)
            ? e with { Event = e.Event with { Payload = new Dictionary<string, object?>(e.Event.Payload) {
                ["paymentChoiceIds"] = choices.Select(choice => " " + choice + " ").ToArray() } } } : e).ToArray() };
        AssertVaynePaymentHistoryRejected(forged);
    }

    private static void AssertVaynePaymentHistoryRejected(MatchRecoveryFrame frame)
    {
        var errors = MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events,
            frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);
        Assert.Contains(errors, e => e.Contains("not bound to accepted payment history", StringComparison.Ordinal));
        Assert.Throws<MatchSessionException>(() => MatchSession.Restore(frame with { ValidationErrors = errors },
            new CoreRuleEngine(), NoopMatchJournal.Instance));
    }
}

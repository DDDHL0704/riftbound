using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

// CN 164.2.a, 204.4.b.1, 430.2.a/430.3: resource acquisition belongs to the
// payment command and neither opens a response nor survives a rejected payment.
public sealed class PendingPaymentRuneTapTests
{
    private const string Rune = "INLINE-RUNE";
    private const string Tap = "TAP_RUNE:" + Rune;

    [Theory]
    [InlineData("ordinary")]
    [InlineData("mighty")]
    [InlineData("trigger-cost")]
    [InlineData("insight")]
    [InlineData("tempered")]
    public async Task TheSharedPaymentEntryAcquiresManaForEveryPaymentContinuation(string window)
    {
        var state = await Open(window);
        var pending = state.PendingPayment!;
        var before = MatchStateHasher.Hash(state);
        var command = new PayCostCommand(pending.PaymentId, pending.PaymentWindow, [Tap, Spend(window)]);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("inline-pay", pending.PlayerId, command.CmdType), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(before, MatchStateHasher.Hash(state));
        Assert.True(result.State.CardObjects[Rune].IsExhausted);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal(state.Tick + 1, result.State.Tick);
        Assert.Single(result.Events, e => e.Kind == "RUNE_TAPPED");
        var paid = Assert.Single(result.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(pending.PaymentId, paid.Payload["paymentId"]);
        Assert.Equal(new[] { Tap }, Assert.IsType<string[]>(paid.Payload["paymentResourceActions"]));
        Assert.Equal(new[] { Rune }, Assert.IsType<string[]>(paid.Payload["tappedRuneObjectIds"]));
        Assert.Equal(state.RunePools[pending.PlayerId].Mana + 1 - pending.ManaCost,
            result.State.RunePools[pending.PlayerId].Mana);
        if (window != "ordinary") Restore(result.State);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("exhausted")]
    [InlineData("opponent")]
    [InlineData("hidden")]
    [InlineData("missing")]
    [InlineData("bad-choice")]
    [InlineData("insufficient")]
    [InlineData("capacity")]
    [InlineData("stale")]
    [InlineData("decline")]
    public async Task ARejectedPaymentDoesNotExhaustTheRuneOrLeaveMana(string boundary)
    {
        var state = await Open("ordinary");
        if (boundary is "exhausted" or "opponent" or "hidden")
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [Rune] = state.CardObjects[Rune] with { IsExhausted = boundary == "exhausted",
                    ControllerId = boundary == "opponent" ? "P2" : "P1", IsFaceDown = boundary == "hidden" } } };
        if (boundary == "capacity") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(int.MaxValue, 0) } };
        if (boundary == "insufficient") state = state with { PendingPayment = state.PendingPayment! with {
            ManaCost = 2, LegalPaymentChoiceIds = ["SPEND_MANA:2"] } };
        var pending = state.PendingPayment!;
        string[] choices = boundary switch {
            "duplicate" => [Tap, Tap, "SPEND_MANA:1"],
            "missing" => ["TAP_RUNE:missing", "SPEND_MANA:1"],
            "bad-choice" => [Tap, "SPEND_MANA:999"],
            "insufficient" => [Tap, "SPEND_MANA:2"],
            "decline" => [Tap, "DECLINE"],
            _ => [Tap, "SPEND_MANA:1"] };
        var command = new PayCostCommand(boundary == "stale" ? "old-id" : pending.PaymentId, pending.PaymentWindow, choices);
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-pay", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(before, MatchStateHasher.Hash(state));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AReadyRuneCanBeTappedBeforeRecyclingItButCannotBeTappedAfterwards(bool recycleFirst)
    {
        var state = await Open("mighty");
        var p = state.PendingPayment!;
        var recycle = "RECYCLE_RUNE:" + Rune;
        var command = new PayCostCommand(p.PaymentId, p.PaymentWindow, recycleFirst
            ? [recycle, Tap, "SPEND_POWER:yellow:1"] : [Tap, recycle, "SPEND_POWER:yellow:1"]);
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("ordered-pay", "P1", command.CmdType), command, default);
        Assert.Equal(!recycleFirst, result.Accepted);
        if (recycleFirst) { Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State)); }
        else { Assert.Contains(Rune, result.State.PlayerZones["P1"].RuneDeck); Assert.Equal(1, result.State.RunePools["P1"].Mana); Restore(result.State); }
    }

    [Fact]
    public async Task ThePromptOffersTapResourcesAndTheRealSessionRecoversReplaysAndDeduplicatesThePayment()
    {
        var initial = OfficialInsightSourceTests.Diana() with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(10, 0), ["P2"] = RunePool.Empty } };
        var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act("P1", new MoveUnitCommand("UNIT", Destination: "BATTLEFIELD:HILL", SourceObjectIds: ["UNIT"]));
        var payment = game.State.PendingPayment!;
        var prompt = ResolutionResult.BuildPrompts(game.State)["P2"];
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.PayCost);
        var metadata = candidate.Metadata!;
        Assert.Contains(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["paymentResourceChoices"]), c => c.Id == "TAP_RUNE:R");
        var command = new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["TAP_RUNE:R", "PAY"]);
        var paid = await game.Act("P2", command);
        Assert.True(game.State.CardObjects["R"].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P2"].Mana);
        Assert.Null(game.State.PendingCardChoice);
        await RideTheWindCurrentMovementTests.Replay(initial, game);

        var sessionState = await Open("insight");
        payment = sessionState.PendingPayment!;
        var session = new MatchSession(sessionState, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var p = session.PromptFor("P2");
        var raw = JsonSerializer.SerializeToElement(new { cmdType = CommandTypes.PayCost,
            paymentId = payment.PaymentId, paymentWindow = payment.PaymentWindow, paymentChoiceIds = new[] { Tap, "PAY" },
            promptId = p.PromptId, snapshotTick = p.SnapshotTick });
        var withTap = new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [Tap, "PAY"]);
        var first = await session.SubmitAsync("P2", "same-payment", withTap, raw, default);
        Assert.True(first.Accepted, first.ErrorMessage);
        var repeat = await session.SubmitAsync("P2", "same-payment", withTap, raw, default);
        Assert.True(repeat.Accepted, repeat.ErrorMessage);
        Assert.Equal(MatchStateHasher.Hash(first.State), MatchStateHasher.Hash(repeat.State));
        Assert.Equal(MatchStateHasher.HashValue(first.Events), MatchStateHasher.HashValue(repeat.Events));
    }

    private static string Spend(string window) => window switch { "mighty" => "SPEND_POWER:yellow:1",
        "ordinary" => "SPEND_MANA:1", _ => "PAY" };

    private static MatchState AddRune(MatchState state, string player = "P1")
    {
        state = VoluntaryPaymentResourceChoicesTests.WithRune(state, RuneTrait.Yellow, Rune);
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [Rune] = state.CardObjects[Rune] with { IsExhausted = false, OwnerId = player, ControllerId = player } };
        var zones = new Dictionary<string, PlayerZones>(state.PlayerZones);
        if (player != "P1") {
            zones["P1"] = zones["P1"] with { Base = zones["P1"].Base.Where(id => id != Rune).ToArray() };
            zones[player] = zones[player] with { Base = zones[player].Base.Append(Rune).ToArray() };
        }
        return state with { CardObjects = cards, PlayerZones = zones,
            Seats = state.Seats.Keys.ToDictionary(id => id, id => id, StringComparer.Ordinal),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [Rune] = new(player, "BASE") } };
    }

    private static async Task<MatchState> Open(string window)
    {
        if (window == "ordinary") return AddRune(PlayCostPreviewTests.Position(RunePool.Empty, "OGN·064/298")) with {
            PendingPayment = new("inline-test", "TEST_PAYMENT", "P1", manaCost: 1, legalPaymentChoiceIds: ["SPEND_MANA:1"]) };
        if (window == "mighty") return (await TriggerPaymentTests.ResolveFioraPowerfulReadyTriggerAsync(
            AddRune(TriggerPaymentTests.BuildFioraPowerfulReadyState(yellowPower: 1)), new CoreRuleEngine())).State;
        if (window == "trigger-cost") return (await Act(AddRune(OfficialTriggerCostTests.Position("SFD·214/221")), "P1", new PassPriorityCommand())).State;
        if (window == "insight") {
            var state = AddRune(OfficialInsightSourceTests.Diana(), "P2");
            state = state with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(10, 0), ["P2"] = RunePool.Empty } };
            return (await Act(state, "P1", new MoveUnitCommand("UNIT", Destination: "BATTLEFIELD:HILL", SourceObjectIds: ["UNIT"]))).State;
        }
        var selected = await TemperedPlayTriggerAuditTests.Confirm(AddRune(TemperedPlayTriggerAuditTests.PositionFor(gear: "SFD·022/221")));
        // The referenced cost has no mana component; acquisition can be voluntary.
        selected = selected with { State = selected.State with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } } };
        return (await Top(selected.State)).State with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(0, 1), ["P2"] = RunePool.Empty } };
    }
}

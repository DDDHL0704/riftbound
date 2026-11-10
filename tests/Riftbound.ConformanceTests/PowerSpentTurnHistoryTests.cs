using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class PowerSpentTurnHistoryTests
{
    internal static IEnumerable<GameEvent> Receipts(Driver game)
        => game.Events().Select(row => row.Event).Where(item => item.Payload.ContainsKey(CoreRuleEngine.PowerPaymentReceiptKey));

    internal static IReadOnlyDictionary<string, object?> Debit(GameEvent item)
        => Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(item.Payload[CoreRuleEngine.PowerPaymentReceiptKey]);

    [Theory]
    [InlineData("SFD·143/221", false, 1)] [InlineData("SFD·143/221", true, 2)]
    [InlineData("SFD·143a/221", false, 1)] [InlineData("SFD·143a/221", true, 2)]
    public async Task RealPrintedAndOptionalFeesProduceOneAcceptedPowerReceipt(string no, bool haste, int paid)
    {
        var initial = RadGearTestPosition.Put(RadGearTestPosition.Create(), "SIVIR", no, "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SIVIR", no, [],
            OptionalCosts: haste ? [HasteOptionalCostNames.HasteReady] : []));
        Assert.Equal(paid, game.State.RunePools["P1"].PaidPowerThisTurn);
        Assert.Equal(0, game.State.RunePools["P2"].PaidPowerThisTurn);
        var receipt = Assert.Single(Receipts(game));
        Assert.Equal("COST_PAID", receipt.Kind);
        Assert.DoesNotContain(game.Events(), row => row.Event.Kind == CoreRuleEngine.PowerSpentEvent);
        var debit = Debit(receipt);
        Assert.Equal("P1", debit["playerId"]);
        Assert.Equal((long)paid, debit["amount"]);
        Assert.Equal((long)paid, debit["paidPowerTotal"]);
        Assert.Equal(game.State.TurnNumber, debit["turnNumber"]);
        await game.Replay();
    }

    [Fact]
    public async Task ARealStartPaymentSurvivesMainPoolClearingAndResetsOnlyAtTheNextTurn()
    {
        var initial = RadGearTestPosition.Put(OfficialHoldSequenceTests.State("SFD·214/221"),
            "HOST", "SFD·143/221", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PassPriorityCommand());
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(4, payment.PowerCost);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        Assert.Equal(4, game.State.RunePools["P1"].PaidPowerThisTurn);
        var receipt = Assert.Single(Receipts(game));
        var original = JsonSerializer.Serialize(receipt);
        var turn = game.State.TurnNumber;
        await game.Top();
        Assert.Equal(MatchPhases.Main, game.State.Phase);
        Assert.Equal(turn, game.State.TurnNumber);
        Assert.Equal(0, game.State.RunePools["P1"].TotalStoredPower);
        Assert.Equal(0, game.State.RunePools["P1"].TotalStoredMana);
        Assert.Equal(4, game.State.RunePools["P1"].PaidPowerThisTurn);
        Assert.Equal(6, ArmamentMightTests.Might(game.State));
        Assert.Contains(CardCombatKeywordNames.Roam, game.State.CardObjects["HOST"].Tags);
        Assert.Equal(original, JsonSerializer.Serialize(Assert.Single(Receipts(game))));
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(turn + 1, game.State.TurnNumber);
        Assert.All(game.State.RunePools.Values, pool => Assert.Equal(0, pool.PaidPowerThisTurn));
        Assert.Equal(4, ArmamentMightTests.Might(game.State));
        Assert.DoesNotContain(CardCombatKeywordNames.Roam, game.State.CardObjects["HOST"].Tags);
        Assert.Equal(original, JsonSerializer.Serialize(Assert.Single(Receipts(game))));
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(-1), InlineData(1)]
    public async Task RebuiltPublicViewsCannotAuthenticateAnAlteredPaidPowerCount(int change)
    {
        const string no = "SFD·143/221";
        var game = new Driver(RadGearTestPosition.Put(RadGearTestPosition.Create(), "SIVIR", no, "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("SIVIR", no, [], OptionalCosts: [HasteOptionalCostNames.HasteReady]));
        var pool = game.State.RunePools["P1"];
        var forged = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) {
            ["P1"] = pool with { PaidPowerThisTurn = pool.PaidPowerThisTurn + change } } };
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, forged), error =>
            error.Contains("power payment counter differs from original accepted debit history", StringComparison.Ordinal));
        await game.Replay();
    }

    [Theory]
    [InlineData("amount")] [InlineData("total")] [InlineData("player")] [InlineData("turn")]
    [InlineData("missing")] [InlineData("malformed")] [InlineData("event-kind")]
    public async Task PaymentReceiptFieldsMustBelongToTheirOriginalAcceptedCost(string field)
    {
        const string no = "SFD·143/221";
        var game = new Driver(RadGearTestPosition.Put(RadGearTestPosition.Create(), "SIVIR", no, "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("SIVIR", no, [], OptionalCosts: [HasteOptionalCostNames.HasteReady]));
        var original = Assert.Single(Receipts(game));
        var debit = new Dictionary<string, object?>(Debit(original));
        switch (field)
        {
            case "amount": debit["amount"] = 1; break;
            case "total": debit["paidPowerTotal"] = 1; break;
            case "player": debit["playerId"] = "P2"; break;
            case "turn": debit["turnNumber"] = game.State.TurnNumber - 1; break;
        }
        var payload = new Dictionary<string, object?>(original.Payload) { [CoreRuleEngine.PowerPaymentReceiptKey] = debit };
        if (field == "missing") payload.Remove(CoreRuleEngine.PowerPaymentReceiptKey);
        if (field == "malformed") payload[CoreRuleEngine.PowerPaymentReceiptKey] = "invalid";
        var changed = original with { Kind = field == "event-kind" ? "CARD_DRAWN" : original.Kind, Payload = payload };
        var events = game.Events().Select(row => ReferenceEquals(row.Event, original) ? row with { Event = changed } : row).ToArray();
        var state = game.State; var sequence = events[^1].Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player,
            state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), events, views,
            state, state.Tick, MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state));
        Assert.Contains(errors, error => error.Contains("power payment counter", StringComparison.Ordinal));
        Assert.Equal(2L, Debit(original)["amount"]);
        Assert.Equal("COST_PAID", original.Kind);
        await game.Replay();
    }

    [Fact]
    public async Task AnEmptyReplayInitialBalanceCannotClaimPastPowerPayments()
    {
        var initial = MatchReplayInitialStateBuilder.FromSeats("EMPTY-POWER-HISTORY",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var forged = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(0, 0, paidPowerThisTurn: 2) } };
        Assert.Equal(RunePool.Empty, forged.RunePools["P1"]);
        var rejected = new RecoveredCommand("P1", "no-payment", CommandTypes.EndTurn, null,
            0, 0, 0, 0, false, "No accepted payment") { ErrorCode = ErrorCodes.PhaseNotAllowed };
        var recovery = new MatchRecoveryFrame(initial.RoomId, 0, 0, [rejected], [],
            new Dictionary<string, RecoveredPlayerView>(), [], initial, forged) { OpeningSeed = initial.Seed };
        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(recovery, new CoreRuleEngine(), default);
        Assert.Contains("action-log replay initial state rune pool for P1 must be empty", errors);
    }
}

using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TriggerPaymentTests
{
    private const string GoldTrigger = "BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD";
    private const string TriggerPaymentWindow = "TRIGGER_PAYMENT";
    private const string PayOneMana = "SPEND_MANA:1";
    private const string Decline = "DECLINE";

    [Fact]
    public async Task BattlefieldConquerGoldOpensTriggerPaymentPrompt()
    {
        var result = await DeclareBattleAsync(BuildBattlefieldConquerGoldState());

        Assert.True(result.Accepted, result.ErrorMessage);
        var payment = AssertTriggerPaymentOpen(result);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        Assert.Equal(TriggerPaymentWindow, payment.PaymentWindow);
        Assert.Equal("P1", payment.PlayerId);
        Assert.Equal(1, payment.ManaCost);
        Assert.Contains(PayOneMana, payment.LegalPaymentChoiceIds);
        Assert.Contains(Decline, payment.LegalPaymentChoiceIds);
        Assert.Empty(GoldTokenIds(result.State));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BattlefieldConquerGoldTriggerPaymentAcceptedCreatesGoldAndClosesWindow()
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareBattleAsync(BuildBattlefieldConquerGoldState(), engine);
        var payment = opened.State.PendingPayment;
        Assert.NotNull(payment);

        var paid = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-trigger-payment-pay", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneMana]),
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        Assert.Equal(0, paid.State.RunePools["P1"].Mana);
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, GoldTrigger, StringComparison.Ordinal));
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
        var tokenEvent = Assert.Single(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, GoldTrigger, StringComparison.Ordinal));
        var tokenObjectId = Assert.IsType<string>(tokenEvent.Payload["tokenObjectId"]);
        Assert.Contains(tokenObjectId, paid.State.PlayerZones["P1"].Base);
        Assert.True(paid.State.CardObjects[tokenObjectId].IsExhausted);
        Assert.Contains("金币", paid.State.CardObjects[tokenObjectId].Tags);
    }

    [Fact]
    public async Task BattlefieldConquerGoldTriggerPaymentDeclineClosesWindowWithoutCostOrToken()
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareBattleAsync(BuildBattlefieldConquerGoldState(), engine);
        var payment = opened.State.PendingPayment;
        Assert.NotNull(payment);

        var declined = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-trigger-payment-decline", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [Decline]),
            CancellationToken.None);

        Assert.True(declined.Accepted, declined.ErrorMessage);
        Assert.Null(declined.State.PendingPayment);
        Assert.Equal(1, declined.State.RunePools["P1"].Mana);
        Assert.Empty(GoldTokenIds(declined.State));
        Assert.Contains(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_PAYMENT_DECLINED", StringComparison.Ordinal));
        Assert.Contains(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BattlefieldConquerGoldTriggerPaymentRejectsInvalidChoicesWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareBattleAsync(BuildBattlefieldConquerGoldState(), engine);
        var payment = opened.State.PendingPayment;
        Assert.NotNull(payment);

        var wrongPlayer = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-trigger-payment-wrong-player", "P2", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneMana]),
            CancellationToken.None);
        var unknownChoice = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-trigger-payment-unknown-choice", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:2"]),
            CancellationToken.None);
        var duplicateChoice = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-trigger-payment-duplicate-choice", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [Decline, Decline]),
            CancellationToken.None);
        var payAndDecline = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-trigger-payment-pay-and-decline", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneMana, Decline]),
            CancellationToken.None);
        var malformed = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-trigger-payment-malformed", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, null),
            CancellationToken.None);

        AssertRejectedNoMutation(wrongPlayer, opened.State, ErrorCodes.PhaseNotAllowed);
        AssertRejectedNoMutation(unknownChoice, opened.State, ErrorCodes.InvalidTarget);
        AssertRejectedNoMutation(duplicateChoice, opened.State, ErrorCodes.InvalidTarget);
        AssertRejectedNoMutation(payAndDecline, opened.State, ErrorCodes.InvalidTarget);
        AssertRejectedNoMutation(malformed, opened.State, ErrorCodes.InvalidPayload);
    }

    [Fact]
    public async Task BattlefieldConquerGoldTriggerPaymentRejectsInsufficientManaAndKeepsWindow()
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareBattleAsync(BuildBattlefieldConquerGoldState(mana: 0), engine);
        var payment = opened.State.PendingPayment;
        Assert.NotNull(payment);

        var insufficient = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-trigger-payment-insufficient", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneMana]),
            CancellationToken.None);

        AssertRejectedNoMutation(insufficient, opened.State, ErrorCodes.InsufficientCost);
        Assert.NotNull(insufficient.State.PendingPayment);
    }

    private static async Task<ResolutionResult> DeclareBattleAsync(
        MatchState state,
        CoreRuleEngine? engine = null)
    {
        return await (engine ?? new CoreRuleEngine()).ResolveAsync(
            state,
            new PlayerIntent("intent-trigger-payment-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-TREASURE-PILE",
                ["P1-BATTLEFIELD-GOLD-ATTACKER"],
                ["P2-BATTLEFIELD-GOLD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);
    }

    private static PendingPaymentState AssertTriggerPaymentOpen(ResolutionResult result)
    {
        var payment = result.State.PendingPayment;
        Assert.NotNull(payment);
        Assert.Equal(TriggerPaymentWindow, payment.PaymentWindow);
        var openedEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal));
        Assert.Equal(payment.PaymentId, openedEvent.Payload["paymentId"]);
        Assert.Equal(payment.PaymentWindow, openedEvent.Payload["paymentWindow"]);
        Assert.Equal(GoldTrigger, openedEvent.Payload["trigger"]);
        Assert.Equal([PayOneMana, Decline], Assert.IsType<string[]>(openedEvent.Payload["paymentChoices"]));

        var prompt = result.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        Assert.Equal(payment.PaymentId, Assert.IsType<string>(metadata["paymentId"]));
        Assert.Equal(TriggerPaymentWindow, Assert.IsType<string>(metadata["paymentWindow"]));
        var cost = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(metadata["cost"]);
        Assert.Equal(1, Assert.IsType<int>(cost["mana"]));
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["paymentChoices"]).ToArray();
        Assert.Contains(choices, choice => string.Equals(choice.Id, PayOneMana, StringComparison.Ordinal));
        Assert.Contains(choices, choice => string.Equals(choice.Id, Decline, StringComparison.Ordinal));
        return payment;
    }

    private static void AssertRejectedNoMutation(
        ResolutionResult result,
        MatchState original,
        string expectedErrorCode)
    {
        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Equal(original.Tick, result.State.Tick);
        Assert.Empty(result.Events);
        Assert.Equal(original.RunePools["P1"].Mana, result.State.RunePools["P1"].Mana);
        Assert.Equal(original.PendingPayment?.PaymentId, result.State.PendingPayment?.PaymentId);
        Assert.Equal(original.PendingPayment?.PaymentWindow, result.State.PendingPayment?.PaymentWindow);
        Assert.Equal(GoldTokenIds(original), GoldTokenIds(result.State));
    }

    private static IReadOnlyList<string> GoldTokenIds(MatchState state)
    {
        return state.PlayerZones["P1"].Base
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Tags.Contains("金币", StringComparer.Ordinal))
            .ToArray();
    }

    private static MatchState BuildBattlefieldConquerGoldState(int mana = 1)
    {
        return new MatchState(
            roomId: "trigger-payment-test",
            tick: 23,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "connection-1",
                ["P2"] = "connection-2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(mana, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-TREASURE-PILE", "P1-BATTLEFIELD-GOLD-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-GOLD-DEFENDER"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-TREASURE-PILE"] = new(
                    "P1-BATTLEFIELD-TREASURE-PILE",
                    cardNo: "SFD·220/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-GOLD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-GOLD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-GOLD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-GOLD-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }
}

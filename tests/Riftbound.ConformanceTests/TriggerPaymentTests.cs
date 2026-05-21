using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TriggerPaymentTests
{
    private const string GoldTrigger = "BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD";
    private const string PowerfulDrawTrigger = "BATTLEFIELD_CONQUERED_POWERFUL_PAY_1_DRAW";
    private const string VayneTrigger = "OGN_VAYNE_CONQUER_PAY_1_RECALL";
    private const string IcevaleTrigger = "ICEVALE_ARCHER_ATTACK_PAY_1_POWER_MINUS_1";
    private const string JaxTrigger = "JAX_WEAPON_ATTACH_PAY_1_DRAW_1";
    private const string FioraTrigger = "SFD_FIORA_POWERFUL_READY_PAY_YELLOW_READY";
    private const string TriggerPaymentWindow = "TRIGGER_PAYMENT";
    private const string PayOneMana = "SPEND_MANA:1";
    private const string PayOneYellowPower = "SPEND_POWER:yellow:1";
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
        var costEvent = Assert.Single(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(payment.PaymentId, costEvent.Payload["paymentId"]);
        Assert.Equal(TriggerPaymentWindow, costEvent.Payload["paymentWindow"]);
        Assert.Equal("P1", costEvent.Payload["playerId"]);
        Assert.Equal(1, costEvent.Payload["totalManaCost"]);
        Assert.Equal(0, costEvent.Payload["genericPower"]);
        Assert.Equal(0, costEvent.Payload["totalPowerCost"]);
        Assert.Equal(GoldTrigger, costEvent.Payload["reason"]);
        Assert.Equal([PayOneMana], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
        Assert.Equal([PayOneMana, Decline], Assert.IsType<string[]>(costEvent.Payload["legalPaymentChoiceIds"]));
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
    public async Task BattlefieldConquerGoldTriggerPaymentBlocksNextContestedBattlefieldUntilAccepted()
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareBattleAsync(BuildBattlefieldConquerGoldState(includeNextContest: true), engine);
        var payment = AssertTriggerPaymentOpen(opened);

        AssertNextContestedBattlefieldNotAdvanced(opened);

        var paid = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-trigger-payment-pay-advances-next-contest", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneMana]),
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        AssertNextContestedBattlefieldAdvancedAfterPaymentClosed(paid, declined: false);
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, GoldTrigger, StringComparison.Ordinal));
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
    public async Task BattlefieldConquerGoldTriggerPaymentDeclineAdvancesNextContestedBattlefield()
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareBattleAsync(BuildBattlefieldConquerGoldState(includeNextContest: true), engine);
        var payment = AssertTriggerPaymentOpen(opened);

        AssertNextContestedBattlefieldNotAdvanced(opened);

        var declined = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-trigger-payment-decline-advances-next-contest", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [Decline]),
            CancellationToken.None);

        Assert.True(declined.Accepted, declined.ErrorMessage);
        Assert.Null(declined.State.PendingPayment);
        Assert.Empty(GoldTokenIds(declined.State));
        AssertNextContestedBattlefieldAdvancedAfterPaymentClosed(declined, declined: true);
        Assert.Contains(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_PAYMENT_DECLINED", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal));
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

    [Theory]
    [InlineData("wrong-payment-id")]
    [InlineData("wrong-payment-window")]
    public async Task BattlefieldConquerGoldTriggerPaymentRejectsWrongPaymentIdentityWithoutMutation(string scenario)
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareBattleAsync(BuildBattlefieldConquerGoldState(includeNextContest: true), engine);
        var payment = AssertTriggerPaymentOpen(opened);
        var command = scenario switch
        {
            "wrong-payment-id" => new PayCostCommand(
                $"{payment.PaymentId}:stale",
                payment.PaymentWindow,
                [PayOneMana]),
            _ => new PayCostCommand(
                payment.PaymentId,
                "STALE_TRIGGER_PAYMENT",
                [PayOneMana])
        };

        AssertNextContestedBattlefieldNotAdvanced(opened);

        var rejected = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent($"intent-trigger-payment-{scenario}", "P1", CommandTypes.PayCost),
            command,
            CancellationToken.None);

        AssertRejectedNoMutation(rejected, opened.State, ErrorCodes.PhaseNotAllowed);
        AssertNoTriggerPaymentSideEffects(rejected);
        Assert.NotNull(rejected.State.PendingPayment);
        Assert.Equal(payment.PaymentId, rejected.State.PendingPayment.PaymentId);
        Assert.Equal(payment.PaymentWindow, rejected.State.PendingPayment.PaymentWindow);
        Assert.Equal([PayOneMana, Decline], rejected.State.PendingPayment.LegalPaymentChoiceIds);
        Assert.Empty(GoldTokenIds(rejected.State));
        AssertNextContestedBattlefieldNotAdvanced(rejected);
    }

    [Fact]
    public async Task BattlefieldConquerGoldTriggerPaymentRejectsPostPaymentReplayWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareBattleAsync(BuildBattlefieldConquerGoldState(includeNextContest: true), engine);
        var payment = AssertTriggerPaymentOpen(opened);
        var command = new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneMana]);

        var paid = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-trigger-payment-pay-before-replay", "P1", CommandTypes.PayCost),
            command,
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        Assert.Single(GoldTokenIds(paid.State));
        AssertNextContestedBattlefieldAdvancedAfterPaymentClosed(paid, declined: false);
        var postPaymentHash = MatchStateHasher.Hash(paid.State);

        var replay = await engine.ResolveAsync(
            paid.State,
            new PlayerIntent("intent-trigger-payment-pay-stale-replay", "P1", CommandTypes.PayCost),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Empty(replay.Events);
        AssertNoTriggerPaymentSideEffects(replay);
        Assert.Equal(postPaymentHash, MatchStateHasher.Hash(replay.State));
        Assert.Null(replay.State.PendingPayment);
        Assert.Equal(GoldTokenIds(paid.State), GoldTokenIds(replay.State));
        AssertNextContestedBattlefieldStillAdvancedOnceAfterReplay(paid, replay, declined: false);
    }

    [Fact]
    public async Task BattlefieldConquerGoldTriggerPaymentRejectsPostDeclineReplayWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareBattleAsync(BuildBattlefieldConquerGoldState(includeNextContest: true), engine);
        var payment = AssertTriggerPaymentOpen(opened);
        var command = new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [Decline]);

        var declined = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-trigger-payment-decline-before-replay", "P1", CommandTypes.PayCost),
            command,
            CancellationToken.None);

        Assert.True(declined.Accepted, declined.ErrorMessage);
        Assert.Null(declined.State.PendingPayment);
        Assert.Empty(GoldTokenIds(declined.State));
        AssertNextContestedBattlefieldAdvancedAfterPaymentClosed(declined, declined: true);
        var postDeclineHash = MatchStateHasher.Hash(declined.State);

        var replay = await engine.ResolveAsync(
            declined.State,
            new PlayerIntent("intent-trigger-payment-decline-stale-replay", "P1", CommandTypes.PayCost),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Empty(replay.Events);
        AssertNoTriggerPaymentSideEffects(replay);
        Assert.Equal(postDeclineHash, MatchStateHasher.Hash(replay.State));
        Assert.Null(replay.State.PendingPayment);
        Assert.Empty(GoldTokenIds(replay.State));
        AssertNextContestedBattlefieldStillAdvancedOnceAfterReplay(declined, replay, declined: true);
    }

    [Fact]
    public async Task BattlefieldConquerGoldTriggerPaymentRejectsInvalidChoiceWithoutAdvancingNextContestedBattlefield()
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareBattleAsync(BuildBattlefieldConquerGoldState(includeNextContest: true), engine);
        var payment = AssertTriggerPaymentOpen(opened);

        var rejected = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-trigger-payment-reject-keeps-next-contest-blocked", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:2"]),
            CancellationToken.None);

        AssertRejectedNoMutation(rejected, opened.State, ErrorCodes.InvalidTarget);
        AssertNextContestedBattlefieldNotAdvanced(rejected);
        Assert.NotNull(rejected.State.PendingPayment);
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

    [Fact]
    public async Task BattlefieldConquerSunkenTemplePowerfulOpensTriggerPaymentPrompt()
    {
        var result = await DeclareSunkenTempleBattleAsync(BuildBattlefieldConquerPowerfulDrawState());

        Assert.True(result.Accepted, result.ErrorMessage);
        var payment = AssertSunkenTemplePaymentOpen(result);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        Assert.Equal(TriggerPaymentWindow, payment.PaymentWindow);
        Assert.Equal("P1", payment.PlayerId);
        Assert.Equal(1, payment.ManaCost);
        Assert.Contains(PayOneMana, payment.LegalPaymentChoiceIds);
        Assert.Contains(Decline, payment.LegalPaymentChoiceIds);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-BATTLEFIELD-POWERFUL-DRAW-001", "P1-BATTLEFIELD-POWERFUL-DRAW-002"],
            result.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, PowerfulDrawTrigger, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BattlefieldConquerSunkenTemplePowerfulPaymentAcceptedDrawsAndClosesWindow()
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareSunkenTempleBattleAsync(BuildBattlefieldConquerPowerfulDrawState(), engine);
        var payment = opened.State.PendingPayment;
        Assert.NotNull(payment);

        var paid = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-sunken-temple-pay", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneMana]),
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        Assert.Equal(0, paid.State.RunePools["P1"].Mana);
        Assert.Equal(["P1-BATTLEFIELD-POWERFUL-DRAW-001"], paid.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-POWERFUL-DRAW-002"], paid.State.PlayerZones["P1"].MainDeck);
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, PowerfulDrawTrigger, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["powerfulObjectId"] as string, "P1-BATTLEFIELD-POWERFUL-ATTACKER", StringComparison.Ordinal));
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BattlefieldConquerSunkenTemplePowerfulPaymentDeclineClosesWithoutDraw()
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareSunkenTempleBattleAsync(BuildBattlefieldConquerPowerfulDrawState(), engine);
        var payment = opened.State.PendingPayment;
        Assert.NotNull(payment);

        var declined = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-sunken-temple-decline", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [Decline]),
            CancellationToken.None);

        Assert.True(declined.Accepted, declined.ErrorMessage);
        Assert.Null(declined.State.PendingPayment);
        Assert.Equal(1, declined.State.RunePools["P1"].Mana);
        Assert.Empty(declined.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-BATTLEFIELD-POWERFUL-DRAW-001", "P1-BATTLEFIELD-POWERFUL-DRAW-002"],
            declined.State.PlayerZones["P1"].MainDeck);
        Assert.Contains(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_PAYMENT_DECLINED", StringComparison.Ordinal));
        Assert.Contains(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, PowerfulDrawTrigger, StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BattlefieldConquerSunkenTemplePowerfulPaymentRejectsInvalidChoicesWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareSunkenTempleBattleAsync(BuildBattlefieldConquerPowerfulDrawState(), engine);
        var payment = opened.State.PendingPayment;
        Assert.NotNull(payment);

        var wrongPlayer = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-sunken-temple-wrong-player", "P2", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneMana]),
            CancellationToken.None);
        var stalePayment = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-sunken-temple-stale-payment", "P1", CommandTypes.PayCost),
            new PayCostCommand($"{payment.PaymentId}:stale", payment.PaymentWindow, [PayOneMana]),
            CancellationToken.None);
        var unknownChoice = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-sunken-temple-unknown-choice", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:2"]),
            CancellationToken.None);
        var duplicateChoice = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-sunken-temple-duplicate-choice", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [Decline, Decline]),
            CancellationToken.None);
        var payAndDecline = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-sunken-temple-pay-and-decline", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneMana, Decline]),
            CancellationToken.None);
        var malformed = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-sunken-temple-malformed", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, null),
            CancellationToken.None);

        AssertRejectedNoMutation(wrongPlayer, opened.State, ErrorCodes.PhaseNotAllowed);
        AssertRejectedNoMutation(stalePayment, opened.State, ErrorCodes.PhaseNotAllowed);
        AssertRejectedNoMutation(unknownChoice, opened.State, ErrorCodes.InvalidTarget);
        AssertRejectedNoMutation(duplicateChoice, opened.State, ErrorCodes.InvalidTarget);
        AssertRejectedNoMutation(payAndDecline, opened.State, ErrorCodes.InvalidTarget);
        AssertRejectedNoMutation(malformed, opened.State, ErrorCodes.InvalidPayload);
    }

    [Fact]
    public async Task BattlefieldConquerSunkenTemplePowerfulPaymentRejectsInsufficientManaAndKeepsWindow()
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareSunkenTempleBattleAsync(BuildBattlefieldConquerPowerfulDrawState(mana: 0), engine);
        var payment = opened.State.PendingPayment;
        Assert.NotNull(payment);

        var insufficient = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-sunken-temple-insufficient", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneMana]),
            CancellationToken.None);

        AssertRejectedNoMutation(insufficient, opened.State, ErrorCodes.InsufficientCost);
        Assert.NotNull(insufficient.State.PendingPayment);
        Assert.Empty(insufficient.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task BattlefieldConquerSunkenTempleDoesNotOpenPaymentForNonPowerfulConqueror()
    {
        var result = await DeclareSunkenTempleBattleAsync(BuildBattlefieldConquerPowerfulDrawState(power: 4));

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, PowerfulDrawTrigger, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, PowerfulDrawTrigger, StringComparison.Ordinal));
    }

    [Fact]
    public async Task OgnVayneConquerOpensTriggerPaymentPromptWithoutReturningBeforePay()
    {
        var result = await DeclareVayneBattleAsync(BuildOgnVayneConquerRecallState());

        Assert.True(result.Accepted, result.ErrorMessage);
        var payment = AssertVaynePaymentOpen(result);
        Assert.Equal(TriggerPaymentWindow, payment.PaymentWindow);
        Assert.Equal("P1", payment.PlayerId);
        Assert.Equal(1, payment.ManaCost);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        AssertVayneStillOnBattlefield(result.State);
        Assert.DoesNotContain("P1-BATTLEFIELD-VAYNE", result.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, IsVayneTriggerResolved);
        Assert.DoesNotContain(result.Events, IsVayneReturnedToHand);
    }

    [Fact]
    public async Task OgnVayneConquerPaymentAcceptedReturnsVayneAndClosesWindow()
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareVayneBattleAsync(BuildOgnVayneConquerRecallState(), engine);
        var payment = AssertVaynePaymentOpen(opened);

        var paid = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-vayne-pay", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneMana]),
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        Assert.Equal(0, paid.State.RunePools["P1"].Mana);
        Assert.Contains("P1-BATTLEFIELD-VAYNE", paid.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("P1-BATTLEFIELD-VAYNE", paid.State.PlayerZones["P1"].Battlefields);
        Assert.False(paid.State.CardObjects.ContainsKey("P1-BATTLEFIELD-VAYNE"));
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(paid.Events, IsVayneTriggerResolved);
        Assert.Contains(paid.Events, IsVayneReturnedToHand);
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OgnVayneConquerPaymentDeclineClosesWithoutReturning()
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareVayneBattleAsync(BuildOgnVayneConquerRecallState(), engine);
        var payment = AssertVaynePaymentOpen(opened);

        var declined = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-vayne-decline", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [Decline]),
            CancellationToken.None);

        Assert.True(declined.Accepted, declined.ErrorMessage);
        Assert.Null(declined.State.PendingPayment);
        Assert.Equal(1, declined.State.RunePools["P1"].Mana);
        AssertVayneStillOnBattlefield(declined.State);
        Assert.DoesNotContain("P1-BATTLEFIELD-VAYNE", declined.State.PlayerZones["P1"].Hand);
        Assert.Contains(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_PAYMENT_DECLINED", StringComparison.Ordinal));
        Assert.Contains(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, IsVayneTriggerResolved);
        Assert.DoesNotContain(declined.Events, IsVayneReturnedToHand);
    }

    [Fact]
    public async Task OgnVayneConquerPaymentRejectsInsufficientManaAndKeepsWindow()
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareVayneBattleAsync(BuildOgnVayneConquerRecallState(mana: 0), engine);
        var payment = AssertVaynePaymentOpen(opened);

        var insufficient = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-vayne-insufficient", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneMana]),
            CancellationToken.None);

        AssertRejectedNoMutation(insufficient, opened.State, ErrorCodes.InsufficientCost);
        Assert.NotNull(insufficient.State.PendingPayment);
        AssertVayneStillOnBattlefield(insufficient.State);
        Assert.DoesNotContain("P1-BATTLEFIELD-VAYNE", insufficient.State.PlayerZones["P1"].Hand);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task OgnVayneInvalidSourceDoesNotOpenPayment(
        bool faceDown,
        bool standby,
        bool opponentControlled)
    {
        var result = await DeclareVayneBattleAsync(
            BuildOgnVayneConquerRecallState(faceDown: faceDown, standby: standby, opponentControlled: opponentControlled));

        Assert.Null(result.State.PendingPayment);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, VayneTrigger, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, IsVayneTriggerResolved);
        Assert.DoesNotContain(result.Events, IsVayneReturnedToHand);
    }

    [Fact]
    public async Task IcevaleArcherAttackOpensTriggerPaymentPromptForSameBattlefieldTarget()
    {
        var result = await DeclareIcevaleBattleAsync(BuildIcevaleArcherAttackState());

        Assert.True(result.Accepted, result.ErrorMessage);
        var payment = AssertIcevalePaymentOpen(result);
        Assert.Equal(TriggerPaymentWindow, payment.PaymentWindow);
        Assert.Equal("P1", payment.PlayerId);
        Assert.Equal(1, payment.ManaCost);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        Assert.Equal(3, result.State.CardObjects["P2-BATTLEFIELD-ICEVALE-TARGET"].Power);
        Assert.Equal(0, result.State.CardObjects["P2-BATTLEFIELD-ICEVALE-TARGET"].UntilEndOfTurnPowerModifier);
        Assert.DoesNotContain(result.Events, IsIcevaleTriggerResolved);
        Assert.DoesNotContain(result.Events, IsIcevalePowerModified);
    }

    [Fact]
    public async Task IcevaleArcherAttackPaymentAcceptedAppliesTemporaryPowerMinusOne()
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareIcevaleBattleAsync(BuildIcevaleArcherAttackState(), engine);
        var payment = AssertIcevalePaymentOpen(opened);

        var paid = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-icevale-pay", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneMana]),
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        Assert.Equal(0, paid.State.RunePools["P1"].Mana);
        var target = paid.State.CardObjects["P2-BATTLEFIELD-ICEVALE-TARGET"];
        Assert.Equal(2, target.Power);
        Assert.Equal(-1, target.UntilEndOfTurnPowerModifier);
        var modifier = Assert.Single(target.UntilEndOfTurnPowerModifiers);
        Assert.Equal(-1, modifier.PowerDelta);
        Assert.Equal(-1, modifier.RequestedPowerDelta);
        Assert.Equal(0, modifier.MinimumPower);
        Assert.Equal(2, modifier.ResultingPower);
        Assert.Equal(3, modifier.BasePower);
        Assert.Equal(2, modifier.EffectivePower);
        Assert.Equal(1, modifier.AppliedOrder);
        Assert.Equal("P1-BATTLEFIELD-ICEVALE", modifier.SourceObjectId);
        Assert.Equal("UNL-065/219", modifier.SourceCardNo);
        Assert.Equal(IcevaleTrigger, modifier.EffectKind);
        Assert.Equal("CoreRuleEngine.ResolveIcevaleArcherAttackTriggerPayment", modifier.SourcePath);
        var powerEffect = Assert.Single(
            paid.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P2-BATTLEFIELD-ICEVALE-TARGET", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-ICEVALE", powerEffect.SourceObjectId);
        Assert.Equal("UNL-065/219", powerEffect.SourceCardNo);
        Assert.Equal(IcevaleTrigger, powerEffect.EffectKind);
        Assert.Equal("CoreRuleEngine.ResolveIcevaleArcherAttackTriggerPayment", powerEffect.SourcePath);
        Assert.Equal(-1, powerEffect.RequestedPowerDelta);
        Assert.Equal(-1, powerEffect.AppliedPowerDelta);
        Assert.Equal(0, powerEffect.MinimumPower);
        Assert.Equal(2, powerEffect.ResultingPower);
        Assert.Equal(1, powerEffect.AppliedOrder);
        var continuousEffects = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            paid.Snapshots["P1"].Timing["continuousEffects"]);
        var powerEffectView = Assert.Single(
            continuousEffects,
            effect => string.Equals(effect["layer"] as string, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                && string.Equals(effect["targetObjectId"] as string, "P2-BATTLEFIELD-ICEVALE-TARGET", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-ICEVALE", powerEffectView["sourceObjectId"]);
        Assert.Equal("UNL-065/219", powerEffectView["sourceCardNo"]);
        Assert.Equal(IcevaleTrigger, powerEffectView["effectKind"]);
        Assert.Equal("CoreRuleEngine.ResolveIcevaleArcherAttackTriggerPayment", powerEffectView["sourcePath"]);
        Assert.Equal(-1, Assert.IsType<int>(powerEffectView["requestedPowerDelta"]));
        Assert.Equal(-1, Assert.IsType<int>(powerEffectView["appliedPowerDelta"]));
        Assert.Equal(0, Assert.IsType<int>(powerEffectView["minimumPower"]));
        Assert.Equal(2, Assert.IsType<int>(powerEffectView["resultingPower"]));
        Assert.Equal(1, Assert.IsType<int>(powerEffectView["appliedOrder"]));
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(paid.Events, IsIcevaleTriggerResolved);
        Assert.Contains(paid.Events, IsIcevalePowerModified);
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task IcevaleArcherAttackPaymentDeclineClosesWithoutPowerChange()
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareIcevaleBattleAsync(BuildIcevaleArcherAttackState(), engine);
        var payment = AssertIcevalePaymentOpen(opened);

        var declined = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-icevale-decline", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [Decline]),
            CancellationToken.None);

        Assert.True(declined.Accepted, declined.ErrorMessage);
        Assert.Null(declined.State.PendingPayment);
        Assert.Equal(1, declined.State.RunePools["P1"].Mana);
        Assert.Equal(3, declined.State.CardObjects["P2-BATTLEFIELD-ICEVALE-TARGET"].Power);
        Assert.Equal(0, declined.State.CardObjects["P2-BATTLEFIELD-ICEVALE-TARGET"].UntilEndOfTurnPowerModifier);
        Assert.Contains(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_PAYMENT_DECLINED", StringComparison.Ordinal));
        Assert.Contains(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, IsIcevaleTriggerResolved);
        Assert.DoesNotContain(declined.Events, IsIcevalePowerModified);
    }

    [Theory]
    [InlineData("P2-BATTLEFIELD-ICEVALE-OFF-FIELD")]
    [InlineData("P2-BATTLEFIELD-ICEVALE-WRONG-BATTLEFIELD")]
    [InlineData("P2-BATTLEFIELD-ICEVALE-FACE-DOWN-TARGET")]
    public async Task IcevaleArcherAttackRejectsInvalidPreselectedTargetWithoutMutation(string targetObjectId)
    {
        var state = BuildIcevaleArcherAttackState(includeInvalidTargets: true);

        var result = await DeclareIcevaleBattleAsync(state, battlefieldTargetObjectId: targetObjectId);

        Assert.False(result.Accepted);
        Assert.True(
            string.Equals(result.ErrorCode, ErrorCodes.InvalidTarget, StringComparison.Ordinal)
            || string.Equals(result.ErrorCode, ErrorCodes.PhaseNotAllowed, StringComparison.Ordinal),
            result.ErrorCode);
        Assert.Equal(state.Tick, result.State.Tick);
        Assert.Empty(result.Events);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        Assert.Equal(3, result.State.CardObjects["P2-BATTLEFIELD-ICEVALE-TARGET"].Power);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, IcevaleTrigger, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task IcevaleArcherInvalidSourceDoesNotOpenPayment(
        bool faceDown,
        bool standby,
        bool opponentControlled)
    {
        var result = await DeclareIcevaleBattleAsync(
            BuildIcevaleArcherAttackState(faceDown: faceDown, standby: standby, opponentControlled: opponentControlled),
            battlefieldTargetObjectId: null);

        Assert.Null(result.State.PendingPayment);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, IcevaleTrigger, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, IsIcevaleTriggerResolved);
        Assert.DoesNotContain(result.Events, IsIcevalePowerModified);
    }

    [Theory]
    [InlineData("SFD·119/221")]
    [InlineData("SFD·119a/221")]
    public async Task JaxWeaponAttachOpensTriggerPaymentPrompt(string jaxCardNo)
    {
        var result = await AssembleJaxWeaponAsync(BuildJaxWeaponAttachState(jaxCardNo: jaxCardNo));

        Assert.True(result.Accepted, result.ErrorMessage);
        var payment = AssertJaxPaymentOpen(result);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        Assert.Equal(
            0,
            result.State.RunePools["P1"].PowerByTrait.TryGetValue(RuneTrait.Red, out var remainingRedPower)
                ? remainingRedPower
                : 0);
        Assert.Equal(2, result.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal("P1-JAX", result.State.CardObjects["P1-JAX-WEAPON"].AttachedToObjectId);
        Assert.Equal(TriggerPaymentWindow, payment.PaymentWindow);
        Assert.DoesNotContain(result.Events, IsJaxTriggerResolved);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task JaxWeaponAttachPaymentAcceptedDrawsOneAndClosesWindow()
    {
        var engine = new CoreRuleEngine();
        var opened = await AssembleJaxWeaponAsync(BuildJaxWeaponAttachState(), engine);
        var payment = AssertJaxPaymentOpen(opened);

        var paid = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-jax-pay", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneMana]),
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        Assert.Equal(0, paid.State.RunePools["P1"].Mana);
        Assert.Equal(["P1-JAX-DRAW-001"], paid.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-JAX-DRAW-002"], paid.State.PlayerZones["P1"].MainDeck);
        Assert.Equal("P1-JAX", paid.State.CardObjects["P1-JAX-WEAPON"].AttachedToObjectId);
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(paid.Events, IsJaxTriggerResolved);
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task JaxWeaponAttachPaymentDeclineClosesWithoutDraw()
    {
        var engine = new CoreRuleEngine();
        var opened = await AssembleJaxWeaponAsync(BuildJaxWeaponAttachState(), engine);
        var payment = AssertJaxPaymentOpen(opened);

        var declined = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-jax-decline", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [Decline]),
            CancellationToken.None);

        Assert.True(declined.Accepted, declined.ErrorMessage);
        Assert.Null(declined.State.PendingPayment);
        Assert.Equal(1, declined.State.RunePools["P1"].Mana);
        Assert.Empty(declined.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-JAX-DRAW-001", "P1-JAX-DRAW-002"], declined.State.PlayerZones["P1"].MainDeck);
        Assert.Equal("P1-JAX", declined.State.CardObjects["P1-JAX-WEAPON"].AttachedToObjectId);
        Assert.Contains(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_PAYMENT_DECLINED", StringComparison.Ordinal));
        Assert.Contains(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, IsJaxTriggerResolved);
        Assert.DoesNotContain(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true, "SFD·119/221")]
    [InlineData(false, "SFD·125/221")]
    public async Task JaxWeaponAttachNonJaxOrNonEquipmentDoesNotOpenPayment(
        bool nonEquipment,
        string jaxCardNo)
    {
        var result = await AssembleJaxWeaponAsync(
            BuildJaxWeaponAttachState(nonEquipment: nonEquipment, jaxCardNo: jaxCardNo));

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        Assert.Equal("P1-JAX", result.State.CardObjects["P1-JAX-WEAPON"].AttachedToObjectId);
        AssertNoJaxTriggerLeak(result);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task JaxWeaponAttachHiddenStandbyOrOpponentControlledDoesNotOpenPayment(
        bool faceDown,
        bool standby,
        bool opponentControlled)
    {
        var state = BuildJaxWeaponAttachState(faceDown: faceDown, standby: standby, opponentControlled: opponentControlled);
        var result = await AssembleJaxWeaponAsync(
            state);

        Assert.Null(result.State.PendingPayment);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-JAX-DRAW-001", "P1-JAX-DRAW-002"], result.State.PlayerZones["P1"].MainDeck);
        AssertNoJaxTriggerLeak(result);
        if (result.Accepted)
        {
            Assert.Equal("P1-JAX", result.State.CardObjects["P1-JAX-WEAPON"].AttachedToObjectId);
        }
        else
        {
            Assert.Equal(state.Tick, result.State.Tick);
            Assert.Empty(result.Events);
            Assert.Null(result.State.CardObjects["P1-JAX-WEAPON"].AttachedToObjectId);
            Assert.Equal(state.PlayerZones["P1"].Base, result.State.PlayerZones["P1"].Base);
        }
    }

    [Fact]
    public async Task JaxWeaponAttachInsufficientPaymentRejectsWithoutDraw()
    {
        var engine = new CoreRuleEngine();
        var opened = await AssembleJaxWeaponAsync(BuildJaxWeaponAttachState(mana: 0), engine);
        var payment = AssertJaxPaymentOpen(opened);

        var insufficient = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-jax-insufficient", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneMana]),
            CancellationToken.None);

        Assert.False(insufficient.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, insufficient.ErrorCode);
        Assert.Empty(insufficient.Events);
        Assert.NotNull(insufficient.State.PendingPayment);
        Assert.Empty(insufficient.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-JAX-DRAW-001", "P1-JAX-DRAW-002"], insufficient.State.PlayerZones["P1"].MainDeck);
        Assert.Equal("P1-JAX", insufficient.State.CardObjects["P1-JAX-WEAPON"].AttachedToObjectId);
    }

    [Theory]
    [InlineData("SFD·180/221")]
    [InlineData("SFD·180a/221")]
    public async Task SfdFioraBoonPowerTransitionOpensYellowTriggerPayment(string fioraCardNo)
    {
        var opened = await ResolveFioraPowerfulReadyTriggerAsync(
            BuildFioraPowerfulReadyState(yellowPower: 1, fioraCardNo: fioraCardNo));

        Assert.True(opened.Accepted, opened.ErrorMessage);
        var payment = AssertFioraPaymentOpen(opened, expectedResourceActionIds: []);
        Assert.Equal(0, payment.ManaCost);
        Assert.Equal(0, payment.PowerCost);
        Assert.Equal(1, payment.PowerCostByTrait[RuneTrait.Yellow]);
        Assert.Equal(5, opened.State.CardObjects["P1-FIORA-TARGET"].Power);
        Assert.True(opened.State.CardObjects["P1-FIORA-TARGET"].IsExhausted);
        Assert.Contains(CardObjectTags.Boon, opened.State.CardObjects["P1-FIORA-TARGET"].Tags);
        Assert.DoesNotContain(opened.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SfdFioraYellowPaymentReadiesPowerfulUnitAndClosesWindow()
    {
        var engine = new CoreRuleEngine();
        var opened = await ResolveFioraPowerfulReadyTriggerAsync(BuildFioraPowerfulReadyState(yellowPower: 1), engine);
        var payment = AssertFioraPaymentOpen(opened, expectedResourceActionIds: []);

        var paid = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-fiora-pay-yellow", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneYellowPower]),
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        Assert.False(paid.State.CardObjects["P1-FIORA-TARGET"].IsExhausted);
        Assert.Empty(paid.State.RunePools["P1"].PowerByTrait);
        var costEvent = Assert.Single(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(payment.PaymentId, costEvent.Payload["paymentId"]);
        Assert.Equal(TriggerPaymentWindow, costEvent.Payload["paymentWindow"]);
        Assert.Equal(FioraTrigger, costEvent.Payload["reason"]);
        Assert.Equal(0, Assert.IsType<int>(costEvent.Payload["totalManaCost"]));
        Assert.Equal(1, Assert.IsType<int>(costEvent.Payload["totalPowerCost"]));
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]);
        Assert.Equal(1, powerByTrait[RuneTrait.Yellow]);
        Assert.Equal([PayOneYellowPower], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
        Assert.Equal([PayOneYellowPower, Decline], Assert.IsType<string[]>(costEvent.Payload["legalPaymentChoiceIds"]));
        Assert.Contains(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, FioraTrigger, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-FIORA-TARGET", StringComparison.Ordinal));
        Assert.Contains(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, FioraTrigger, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-FIORA-TARGET", StringComparison.Ordinal));
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SfdFioraDeclineDoesNotPayRecycleOrReady()
    {
        var engine = new CoreRuleEngine();
        var opened = await ResolveFioraPowerfulReadyTriggerAsync(BuildFioraPowerfulReadyState(yellowPower: 1), engine);
        var payment = AssertFioraPaymentOpen(opened, expectedResourceActionIds: []);

        var declined = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-fiora-decline", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [Decline]),
            CancellationToken.None);

        Assert.True(declined.Accepted, declined.ErrorMessage);
        Assert.Null(declined.State.PendingPayment);
        Assert.True(declined.State.CardObjects["P1-FIORA-TARGET"].IsExhausted);
        Assert.Equal(1, declined.State.RunePools["P1"].PowerByTrait[RuneTrait.Yellow]);
        Assert.Contains(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_PAYMENT_DECLINED", StringComparison.Ordinal));
        Assert.Contains(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("wrong-payment-id")]
    [InlineData("wrong-payment-window")]
    public async Task SfdFioraYellowTriggerPaymentRejectsWrongPaymentIdentityWithoutMutation(string scenario)
    {
        var engine = new CoreRuleEngine();
        var opened = await ResolveFioraPowerfulReadyTriggerAsync(BuildFioraPowerfulReadyState(yellowPower: 1), engine);
        var payment = AssertFioraPaymentOpen(opened, expectedResourceActionIds: []);
        var command = scenario switch
        {
            "wrong-payment-id" => new PayCostCommand(
                $"{payment.PaymentId}:stale",
                payment.PaymentWindow,
                [PayOneYellowPower]),
            _ => new PayCostCommand(
                payment.PaymentId,
                "STALE_TRIGGER_PAYMENT",
                [PayOneYellowPower])
        };

        var rejected = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent($"intent-fiora-yellow-{scenario}", "P1", CommandTypes.PayCost),
            command,
            CancellationToken.None);

        AssertFioraRejectedNoMutation(rejected, opened.State, payment, expectedResourceActionIds: [], ErrorCodes.PhaseNotAllowed);
        AssertFioraPayCostPromptMatchesPendingPayment(rejected, expectedResourceActionIds: []);
    }

    [Fact]
    public async Task SfdFioraYellowPaymentRejectsPostPaymentReplayWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var opened = await ResolveFioraPowerfulReadyTriggerAsync(BuildFioraPowerfulReadyState(yellowPower: 1), engine);
        var payment = AssertFioraPaymentOpen(opened, expectedResourceActionIds: []);
        var command = new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneYellowPower]);

        var paid = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-fiora-yellow-pay-before-replay", "P1", CommandTypes.PayCost),
            command,
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        Assert.False(paid.State.CardObjects["P1-FIORA-TARGET"].IsExhausted);
        Assert.Empty(paid.State.RunePools["P1"].PowerByTrait);
        Assert.Equal(1, CountEvents(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)));
        Assert.Equal(1, CountEvents(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)));
        Assert.Equal(1, CountEvents(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal)));
        var postPaymentHash = MatchStateHasher.Hash(paid.State);

        var replay = await engine.ResolveAsync(
            paid.State,
            new PlayerIntent("intent-fiora-yellow-pay-stale-replay", "P1", CommandTypes.PayCost),
            command,
            CancellationToken.None);

        AssertFioraClosedReplayRejectedNoMutation(replay, paid.State, postPaymentHash);
        Assert.False(replay.State.CardObjects["P1-FIORA-TARGET"].IsExhausted);
        Assert.Empty(replay.State.RunePools["P1"].PowerByTrait);
    }

    [Fact]
    public async Task SfdFioraDeclineRejectsPostDeclineReplayWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var opened = await ResolveFioraPowerfulReadyTriggerAsync(BuildFioraPowerfulReadyState(yellowPower: 1), engine);
        var payment = AssertFioraPaymentOpen(opened, expectedResourceActionIds: []);
        var command = new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [Decline]);

        var declined = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-fiora-decline-before-replay", "P1", CommandTypes.PayCost),
            command,
            CancellationToken.None);

        Assert.True(declined.Accepted, declined.ErrorMessage);
        Assert.Null(declined.State.PendingPayment);
        Assert.True(declined.State.CardObjects["P1-FIORA-TARGET"].IsExhausted);
        Assert.Equal(1, declined.State.RunePools["P1"].PowerByTrait[RuneTrait.Yellow]);
        Assert.Equal(1, CountEvents(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_PAYMENT_DECLINED", StringComparison.Ordinal)));
        Assert.Equal(1, CountEvents(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal)));
        var postDeclineHash = MatchStateHasher.Hash(declined.State);

        var replay = await engine.ResolveAsync(
            declined.State,
            new PlayerIntent("intent-fiora-decline-stale-replay", "P1", CommandTypes.PayCost),
            command,
            CancellationToken.None);

        AssertFioraClosedReplayRejectedNoMutation(replay, declined.State, postDeclineHash);
        Assert.True(replay.State.CardObjects["P1-FIORA-TARGET"].IsExhausted);
        Assert.Equal(1, replay.State.RunePools["P1"].PowerByTrait[RuneTrait.Yellow]);
    }

    [Fact]
    public async Task SfdFioraPaymentCanRecycleYellowRuneResource()
    {
        var engine = new CoreRuleEngine();
        var opened = await ResolveFioraPowerfulReadyTriggerAsync(
            BuildFioraPowerfulReadyState(yellowPower: 0, includeYellowRune: true),
            engine);
        var paymentResourceAction = "RECYCLE_RUNE:P1-FIORA-YELLOW-RUNE";
        var payment = AssertFioraPaymentOpen(opened, expectedResourceActionIds: [paymentResourceAction]);

        var paid = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-fiora-pay-recycle-yellow", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [paymentResourceAction, PayOneYellowPower]),
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        Assert.False(paid.State.CardObjects["P1-FIORA-TARGET"].IsExhausted);
        Assert.DoesNotContain("P1-FIORA-YELLOW-RUNE", paid.State.PlayerZones["P1"].Base);
        Assert.Contains("P1-FIORA-YELLOW-RUNE", paid.State.PlayerZones["P1"].RuneDeck);
        Assert.Empty(paid.State.RunePools["P1"].PowerByTrait);
        var recycledEvent = Assert.Single(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        Assert.Equal(payment.PaymentId, recycledEvent.Payload["paymentId"]);
        Assert.Equal(TriggerPaymentWindow, recycledEvent.Payload["paymentWindow"]);
        Assert.Equal(RuneTrait.Yellow, recycledEvent.Payload["trait"]);
        var powerEvent = Assert.Single(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.Equal(payment.PaymentId, powerEvent.Payload["paymentId"]);
        var costEvent = Assert.Single(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal([paymentResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal(["P1-FIORA-YELLOW-RUNE"], Assert.IsType<string[]>(costEvent.Payload["recycledRuneObjectIds"]));
        Assert.Equal([paymentResourceAction, PayOneYellowPower], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
    }

    [Fact]
    public async Task SfdFioraTriggerPaymentPromptQuotesTypedYellowTemporaryPaymentResource()
    {
        var temporaryResource = FioraTemporaryPaymentResource("UNITY_SIGIL:TEMP-FIORA-PROMPT");
        var state = BuildFioraPowerfulReadyState(yellowPower: 0) with
        {
            TemporaryPaymentResources = [temporaryResource]
        };

        var opened = await ResolveFioraPowerfulReadyTriggerAsync(state);
        var payment = opened.State.PendingPayment;
        Assert.NotNull(payment);
        Assert.Equal(TriggerPaymentWindow, payment.PaymentWindow);
        Assert.Empty(payment.PaymentResourceActionIds);

        var prompt = opened.Prompts["P1"];
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var resourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["paymentResourceChoices"])
            .Select(choice => choice.Id)
            .ToArray();
        Assert.Equal([resourceAction], resourceChoices);
        var powerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            metadata["paymentResourcePowerByChoice"]);
        Assert.Equal(RuneTrait.Yellow, powerByChoice[resourceAction]["trait"]);
        Assert.Equal(0, powerByChoice[resourceAction]["power"]);
        var choicePowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(powerByChoice[resourceAction]["powerByTrait"]);
        Assert.Equal(1, choicePowerByTrait[RuneTrait.Yellow]);
        Assert.Equal(1, Assert.IsType<int>(metadata["availablePowerWithPaymentResources"]));
        var availablePowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            metadata["availablePowerByTraitWithPaymentResources"]);
        Assert.Equal(1, availablePowerByTrait[RuneTrait.Yellow]);
    }

    [Fact]
    public async Task SfdFioraTriggerPaymentPromptDoesNotQuoteWrongTraitTemporaryPaymentResource()
    {
        var temporaryResource = FioraTemporaryPaymentResource(
            "RAGE_SIGIL:TEMP-FIORA-WRONG-TRAIT",
            remainingPowerByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RuneTrait.Red] = 1
            });
        var state = BuildFioraPowerfulReadyState(yellowPower: 0) with
        {
            TemporaryPaymentResources = [temporaryResource]
        };

        var opened = await ResolveFioraPowerfulReadyTriggerAsync(state);
        var prompt = opened.Prompts["P1"];
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var resourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["paymentResourceChoices"])
            .Select(choice => choice.Id)
            .ToArray();
        Assert.DoesNotContain(resourceAction, resourceChoices);
        Assert.Empty(resourceChoices);
        Assert.Equal(0, Assert.IsType<int>(metadata["availablePowerWithPaymentResources"]));
        var availablePowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            metadata["availablePowerByTraitWithPaymentResources"]);
        Assert.Empty(availablePowerByTrait);
    }

    [Fact]
    public async Task SfdFioraTriggerPaymentPromptCountsMixedRecycleAndTemporaryResourcesOnce()
    {
        var temporaryResource = FioraTemporaryPaymentResource("UNITY_SIGIL:TEMP-FIORA-MIXED-PROMPT");
        var state = BuildFioraPowerfulReadyState(yellowPower: 0, includeYellowRune: true) with
        {
            TemporaryPaymentResources = [temporaryResource]
        };

        var opened = await ResolveFioraPowerfulReadyTriggerAsync(state);
        var prompt = opened.Prompts["P1"];
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var recycleAction = "RECYCLE_RUNE:P1-FIORA-YELLOW-RUNE";
        var temporaryAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var resourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["paymentResourceChoices"])
            .Select(choice => choice.Id)
            .ToArray();
        Assert.Equal([recycleAction, temporaryAction], resourceChoices);
        Assert.Equal(2, Assert.IsType<int>(metadata["availablePowerWithPaymentResources"]));
        var availablePowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            metadata["availablePowerByTraitWithPaymentResources"]);
        Assert.Equal(2, availablePowerByTrait[RuneTrait.Yellow]);
        var powerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            metadata["paymentResourcePowerByChoice"]);
        Assert.Equal(1, powerByChoice[recycleAction]["power"]);
        Assert.Equal(0, powerByChoice[temporaryAction]["power"]);
        var temporaryPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            powerByChoice[temporaryAction]["powerByTrait"]);
        Assert.Equal(1, temporaryPowerByTrait[RuneTrait.Yellow]);
    }

    [Fact]
    public async Task SfdFioraPaymentConsumesTypedYellowTemporaryPaymentResource()
    {
        var engine = new CoreRuleEngine();
        var temporaryResource = FioraTemporaryPaymentResource("UNITY_SIGIL:TEMP-FIORA-PAY");
        var opened = await ResolveFioraPowerfulReadyTriggerAsync(
            BuildFioraPowerfulReadyState(yellowPower: 0) with
            {
                TemporaryPaymentResources = [temporaryResource]
            },
            engine);
        var payment = opened.State.PendingPayment;
        Assert.NotNull(payment);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);

        var paid = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-fiora-pay-temporary-yellow", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [resourceAction, PayOneYellowPower]),
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        Assert.Empty(paid.State.TemporaryPaymentResources);
        Assert.False(paid.State.CardObjects["P1-FIORA-TARGET"].IsExhausted);
        Assert.Empty(paid.State.RunePools["P1"].PowerByTrait);
        var spentEvent = Assert.Single(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));
        var clearedEvent = Assert.Single(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_CLEARED", StringComparison.Ordinal));
        var costEvent = Assert.Single(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(payment.PaymentId, spentEvent.Payload["paymentId"]);
        Assert.Equal(payment.PaymentId, clearedEvent.Payload["paymentId"]);
        Assert.Equal(payment.PaymentId, costEvent.Payload["paymentId"]);
        Assert.Equal(TriggerPaymentWindow, spentEvent.Payload["paymentWindow"]);
        Assert.Equal(TriggerPaymentWindow, clearedEvent.Payload["paymentWindow"]);
        Assert.Equal(TriggerPaymentWindow, costEvent.Payload["paymentWindow"]);
        Assert.Equal(temporaryResource.ResourceId, spentEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal(0, spentEvent.Payload["consumedPower"]);
        var spentPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(spentEvent.Payload["consumedPowerByTrait"]);
        Assert.Equal(1, spentPowerByTrait[RuneTrait.Yellow]);
        Assert.Equal([resourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([resourceAction, PayOneYellowPower], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
        Assert.Equal([temporaryResource.ResourceId], Assert.IsType<string[]>(costEvent.Payload["temporaryPaymentResourceIds"]));
        Assert.Equal(0, costEvent.Payload["temporaryPaymentResourcePower"]);
        var costPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["temporaryPaymentResourcePowerByTrait"]);
        Assert.Equal(1, costPowerByTrait[RuneTrait.Yellow]);
        Assert.Contains(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-FIORA-TARGET", StringComparison.Ordinal));
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("wrong-owner")]
    [InlineData("zero")]
    [InlineData("wrong-kind")]
    [InlineData("wrong-trait")]
    [InlineData("duplicate")]
    [InlineData("invalid-id")]
    [InlineData("unnecessary")]
    [InlineData("insufficient")]
    [InlineData("stale-source")]
    [InlineData("stale-target")]
    public async Task SfdFioraPaymentRejectsInvalidTemporaryResourcesWithoutMutation(string caseName)
    {
        var engine = new CoreRuleEngine();
        var temporaryResource = caseName switch
        {
            "wrong-owner" => FioraTemporaryPaymentResource("UNITY_SIGIL:TEMP-FIORA-INVALID", ownerPlayerId: "P2"),
            "zero" => FioraTemporaryPaymentResource("UNITY_SIGIL:TEMP-FIORA-INVALID", remainingPowerByTrait: new Dictionary<string, int>(StringComparer.Ordinal)),
            "wrong-kind" => FioraTemporaryPaymentResource("UNITY_SIGIL:TEMP-FIORA-INVALID", allowedPaymentKinds: ["MANA_COST"]),
            "wrong-trait" or "insufficient" => FioraTemporaryPaymentResource(
                "UNITY_SIGIL:TEMP-FIORA-INVALID",
                remainingPowerByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 1
                }),
            _ => FioraTemporaryPaymentResource("UNITY_SIGIL:TEMP-FIORA-INVALID")
        };
        var opened = await ResolveFioraPowerfulReadyTriggerAsync(
            BuildFioraPowerfulReadyState(yellowPower: string.Equals(caseName, "unnecessary", StringComparison.Ordinal) ? 1 : 0) with
            {
                TemporaryPaymentResources = [temporaryResource]
            },
            engine);
        var payment = opened.State.PendingPayment;
        Assert.NotNull(payment);
        var resourceAction = string.Equals(caseName, "invalid-id", StringComparison.Ordinal)
            ? PaymentCostRules.TemporaryPaymentResourceActionId("UNITY_SIGIL:TEMP-FIORA-MISSING")
            : PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var paymentChoices = string.Equals(caseName, "duplicate", StringComparison.Ordinal)
            ? new[] { resourceAction, resourceAction, PayOneYellowPower }
            : [resourceAction, PayOneYellowPower];
        var paymentState = caseName switch
        {
            "stale-target" => opened.State with
            {
                CardObjects = new Dictionary<string, CardObjectState>(opened.State.CardObjects, StringComparer.Ordinal)
                {
                    ["P1-FIORA-TARGET"] = opened.State.CardObjects["P1-FIORA-TARGET"] with
                    {
                        Power = 4
                    }
                }
            },
            "stale-source" => opened.State with
            {
                PlayerZones = new Dictionary<string, PlayerZones>(opened.State.PlayerZones, StringComparer.Ordinal)
                {
                    ["P1"] = opened.State.PlayerZones["P1"] with
                    {
                        Base = opened.State.PlayerZones["P1"].Base
                            .Where(objectId => !string.Equals(objectId, "P1-SFD-FIORA", StringComparison.Ordinal))
                            .ToArray(),
                        Graveyard = opened.State.PlayerZones["P1"].Graveyard
                            .Concat(["P1-SFD-FIORA"])
                            .ToArray()
                    }
                }
            },
            _ => opened.State
        };
        var initialHash = MatchStateHasher.Hash(paymentState);

        var result = await engine.ResolveAsync(
            paymentState,
            new PlayerIntent($"intent-fiora-invalid-temporary-{caseName}", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, paymentChoices),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.True(
            string.Equals(result.ErrorCode, ErrorCodes.InvalidTarget, StringComparison.Ordinal)
            || string.Equals(result.ErrorCode, ErrorCodes.InsufficientCost, StringComparison.Ordinal),
            $"Unexpected error code {result.ErrorCode}");
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("duplicate-resource")]
    [InlineData("unnecessary-resource")]
    [InlineData("stale-target")]
    [InlineData("stale-source")]
    public async Task SfdFioraPaymentRejectsInvalidOrStaleChoicesWithoutMutation(string caseName)
    {
        var engine = new CoreRuleEngine();
        var opened = await ResolveFioraPowerfulReadyTriggerAsync(
            BuildFioraPowerfulReadyState(yellowPower: caseName == "unnecessary-resource" ? 1 : 0, includeYellowRune: true),
            engine);
        var paymentResourceAction = "RECYCLE_RUNE:P1-FIORA-YELLOW-RUNE";
        var payment = opened.State.PendingPayment;
        Assert.NotNull(payment);

        var paymentChoices = caseName switch
        {
            "duplicate-resource" => new[] { paymentResourceAction, paymentResourceAction, PayOneYellowPower },
            "unnecessary-resource" => new[] { paymentResourceAction, PayOneYellowPower },
            _ => new[] { paymentResourceAction, PayOneYellowPower }
        };
        var paymentState = caseName switch
        {
            "stale-target" => opened.State with
            {
                CardObjects = new Dictionary<string, CardObjectState>(opened.State.CardObjects, StringComparer.Ordinal)
                {
                    ["P1-FIORA-TARGET"] = opened.State.CardObjects["P1-FIORA-TARGET"] with
                    {
                        Power = 4
                    }
                }
            },
            "stale-source" => opened.State with
            {
                PlayerZones = new Dictionary<string, PlayerZones>(opened.State.PlayerZones, StringComparer.Ordinal)
                {
                    ["P1"] = opened.State.PlayerZones["P1"] with
                    {
                        Base = opened.State.PlayerZones["P1"].Base
                            .Where(objectId => !string.Equals(objectId, "P1-SFD-FIORA", StringComparison.Ordinal))
                            .ToArray(),
                        Graveyard = opened.State.PlayerZones["P1"].Graveyard
                            .Concat(["P1-SFD-FIORA"])
                            .ToArray()
                    }
                }
            },
            _ => opened.State
        };

        var result = await engine.ResolveAsync(
            paymentState,
            new PlayerIntent($"intent-fiora-invalid-{caseName}", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, paymentChoices),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.True(
            string.Equals(result.ErrorCode, ErrorCodes.InvalidTarget, StringComparison.Ordinal)
            || string.Equals(result.ErrorCode, ErrorCodes.InsufficientCost, StringComparison.Ordinal),
            $"Unexpected error code {result.ErrorCode}");
        Assert.Empty(result.Events);
        Assert.Equal(paymentState.Tick, result.State.Tick);
        Assert.Equal(paymentState.PendingPayment?.PaymentId, result.State.PendingPayment?.PaymentId);
        Assert.Equal(paymentState.PlayerZones["P1"].Base, result.State.PlayerZones["P1"].Base);
        Assert.Equal(paymentState.PlayerZones["P1"].RuneDeck, result.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(paymentState.RunePools["P1"], result.State.RunePools["P1"]);
        Assert.Equal(paymentState.CardObjects["P1-FIORA-TARGET"].IsExhausted, result.State.CardObjects["P1-FIORA-TARGET"].IsExhausted);
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

    private static async Task<ResolutionResult> DeclareSunkenTempleBattleAsync(
        MatchState state,
        CoreRuleEngine? engine = null)
    {
        return await (engine ?? new CoreRuleEngine()).ResolveAsync(
            state,
            new PlayerIntent("intent-trigger-payment-sunken-temple-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-SUNKEN-TEMPLE",
                ["P1-BATTLEFIELD-POWERFUL-ATTACKER"],
                ["P2-BATTLEFIELD-POWERFUL-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> DeclareVayneBattleAsync(
        MatchState state,
        CoreRuleEngine? engine = null)
    {
        return await (engine ?? new CoreRuleEngine()).ResolveAsync(
            state,
            new PlayerIntent("intent-vayne-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-VAYNE-FIELD",
                ["P1-BATTLEFIELD-VAYNE"],
                ["P2-BATTLEFIELD-VAYNE-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> DeclareIcevaleBattleAsync(
        MatchState state,
        CoreRuleEngine? engine = null,
        string? battlefieldTargetObjectId = "P2-BATTLEFIELD-ICEVALE-TARGET")
    {
        return await (engine ?? new CoreRuleEngine()).ResolveAsync(
            state,
            new PlayerIntent("intent-icevale-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-ICEVALE-FIELD",
                ["P1-BATTLEFIELD-ICEVALE"],
                ["P2-BATTLEFIELD-ICEVALE-DEFENDER"],
                ["COMBAT_ASSIGNMENT"],
                string.IsNullOrWhiteSpace(battlefieldTargetObjectId) ? [] : [battlefieldTargetObjectId]),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> AssembleJaxWeaponAsync(
        MatchState state,
        CoreRuleEngine? engine = null)
    {
        return await (engine ?? new CoreRuleEngine()).ResolveAsync(
            state,
            new PlayerIntent("intent-jax-assemble", "P1", CommandTypes.AssembleEquipment),
            new AssembleEquipmentCommand(
                "P1-JAX-WEAPON",
                "P1-JAX",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> ResolveFioraPowerfulReadyTriggerAsync(
        MatchState state,
        CoreRuleEngine? engine = null)
    {
        var ruleEngine = engine ?? new CoreRuleEngine();
        var played = await ruleEngine.ResolveAsync(
            state,
            new PlayerIntent("intent-fiora-play-mercy", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-FIORA-MERCY",
                "OGN·053/298",
                ["P1-FIORA-TARGET"]),
            CancellationToken.None);
        Assert.True(played.Accepted, played.ErrorMessage);

        var p1Pass = await ruleEngine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-fiora-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        var p2Pass = await ruleEngine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-fiora-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        return p2Pass;
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

    private static PendingPaymentState AssertSunkenTemplePaymentOpen(ResolutionResult result)
    {
        var payment = result.State.PendingPayment;
        Assert.NotNull(payment);
        Assert.Equal(TriggerPaymentWindow, payment.PaymentWindow);
        var openedEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, PowerfulDrawTrigger, StringComparison.Ordinal));
        Assert.Equal(payment.PaymentId, openedEvent.Payload["paymentId"]);
        Assert.Equal(payment.PaymentWindow, openedEvent.Payload["paymentWindow"]);
        Assert.Equal("P1-BATTLEFIELD-SUNKEN-TEMPLE", openedEvent.Payload["battlefieldId"]);
        Assert.Equal("P1-BATTLEFIELD-SUNKEN-TEMPLE", openedEvent.Payload["battlefieldObjectId"]);
        Assert.Equal("P1-BATTLEFIELD-POWERFUL-ATTACKER", openedEvent.Payload["sourceObjectId"]);
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

    private static PendingPaymentState AssertVaynePaymentOpen(ResolutionResult result)
    {
        var payment = result.State.PendingPayment;
        Assert.NotNull(payment);
        Assert.Equal(TriggerPaymentWindow, payment.PaymentWindow);
        var openedEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, VayneTrigger, StringComparison.Ordinal));
        Assert.Equal(payment.PaymentId, openedEvent.Payload["paymentId"]);
        Assert.Equal(payment.PaymentWindow, openedEvent.Payload["paymentWindow"]);
        Assert.Equal("P1-BATTLEFIELD-VAYNE-FIELD", openedEvent.Payload["battlefieldId"]);
        Assert.Equal("P1-BATTLEFIELD-VAYNE-FIELD", openedEvent.Payload["battlefieldObjectId"]);
        Assert.Equal("P1-BATTLEFIELD-VAYNE", openedEvent.Payload["sourceObjectId"]);
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

    private static PendingPaymentState AssertIcevalePaymentOpen(ResolutionResult result)
    {
        var payment = result.State.PendingPayment;
        Assert.NotNull(payment);
        Assert.Equal(TriggerPaymentWindow, payment.PaymentWindow);
        var openedEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, IcevaleTrigger, StringComparison.Ordinal));
        Assert.Equal(payment.PaymentId, openedEvent.Payload["paymentId"]);
        Assert.Equal(payment.PaymentWindow, openedEvent.Payload["paymentWindow"]);
        Assert.Equal("P1-BATTLEFIELD-ICEVALE-FIELD", openedEvent.Payload["battlefieldId"]);
        Assert.Equal("P1-BATTLEFIELD-ICEVALE", openedEvent.Payload["sourceObjectId"]);
        Assert.Equal("P2-BATTLEFIELD-ICEVALE-TARGET", openedEvent.Payload["targetObjectId"]);
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

    private static PendingPaymentState AssertJaxPaymentOpen(ResolutionResult result)
    {
        var payment = result.State.PendingPayment;
        Assert.NotNull(payment);
        Assert.Equal(TriggerPaymentWindow, payment.PaymentWindow);
        Assert.Equal("P1", payment.PlayerId);
        Assert.Equal(1, payment.ManaCost);
        Assert.Contains(PayOneMana, payment.LegalPaymentChoiceIds);
        Assert.Contains(Decline, payment.LegalPaymentChoiceIds);
        var openedEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, JaxTrigger, StringComparison.Ordinal));
        Assert.Equal(payment.PaymentId, openedEvent.Payload["paymentId"]);
        Assert.Equal(payment.PaymentWindow, openedEvent.Payload["paymentWindow"]);
        Assert.Equal("P1-JAX", openedEvent.Payload["sourceObjectId"]);
        Assert.Equal("P1-JAX-WEAPON", openedEvent.Payload["equipmentObjectId"]);
        Assert.Equal("SFD·022/221", openedEvent.Payload["equipmentCardNo"]);
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

    private static PendingPaymentState AssertFioraPaymentOpen(
        ResolutionResult result,
        IReadOnlyList<string> expectedResourceActionIds)
    {
        var payment = result.State.PendingPayment;
        Assert.NotNull(payment);
        Assert.Equal(TriggerPaymentWindow, payment.PaymentWindow);
        Assert.Equal("P1", payment.PlayerId);
        Assert.Equal(0, payment.ManaCost);
        Assert.Equal(0, payment.PowerCost);
        Assert.Equal(1, payment.PowerCostByTrait[RuneTrait.Yellow]);
        Assert.Contains(PayOneYellowPower, payment.LegalPaymentChoiceIds);
        Assert.Contains(Decline, payment.LegalPaymentChoiceIds);
        Assert.Equal(expectedResourceActionIds, payment.PaymentResourceActionIds);
        var openedEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, FioraTrigger, StringComparison.Ordinal));
        Assert.Equal(payment.PaymentId, openedEvent.Payload["paymentId"]);
        Assert.Equal(payment.PaymentWindow, openedEvent.Payload["paymentWindow"]);
        Assert.Equal("P1-SFD-FIORA", openedEvent.Payload["sourceObjectId"]);
        Assert.Equal("P1-FIORA-TARGET", openedEvent.Payload["targetObjectId"]);
        Assert.Equal(4, Assert.IsType<int>(openedEvent.Payload["previousPower"]));
        Assert.Equal(5, Assert.IsType<int>(openedEvent.Payload["resultingPower"]));
        Assert.Equal([PayOneYellowPower, Decline], Assert.IsType<string[]>(openedEvent.Payload["paymentChoices"]));
        Assert.Equal(expectedResourceActionIds, Assert.IsType<string[]>(openedEvent.Payload["paymentResourceActions"]));

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
        Assert.Equal(0, Assert.IsType<int>(cost["mana"]));
        var costPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(cost["powerByTrait"]);
        Assert.Equal(1, costPowerByTrait[RuneTrait.Yellow]);
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["paymentChoices"]).ToArray();
        Assert.Contains(choices, choice => string.Equals(choice.Id, PayOneYellowPower, StringComparison.Ordinal));
        Assert.Contains(choices, choice => string.Equals(choice.Id, Decline, StringComparison.Ordinal));
        var resourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["paymentResourceChoices"])
            .Select(choice => choice.Id)
            .ToArray();
        Assert.Equal(expectedResourceActionIds, resourceChoices);
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
        Assert.Equal(original.PlayerZones["P1"].Hand, result.State.PlayerZones["P1"].Hand);
        Assert.Equal(original.PlayerZones["P1"].MainDeck, result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(GoldTokenIds(original), GoldTokenIds(result.State));
    }

    private static void AssertNoTriggerPaymentSideEffects(ResolutionResult result)
    {
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_PAYMENT_DECLINED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
    }

    private static void AssertFioraRejectedNoMutation(
        ResolutionResult result,
        MatchState original,
        PendingPaymentState expectedPayment,
        IReadOnlyList<string> expectedResourceActionIds,
        string expectedErrorCode)
    {
        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(original), MatchStateHasher.Hash(result.State));
        AssertFioraPendingPaymentPreserved(result.State.PendingPayment, expectedPayment, expectedResourceActionIds);
        Assert.True(result.State.CardObjects["P1-FIORA-TARGET"].IsExhausted);
        Assert.Equal(1, result.State.RunePools["P1"].PowerByTrait[RuneTrait.Yellow]);
        AssertNoFioraTriggerPaymentSideEffects(result);
    }

    private static void AssertFioraClosedReplayRejectedNoMutation(
        ResolutionResult result,
        MatchState original,
        string expectedHash)
    {
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(expectedHash, MatchStateHasher.Hash(result.State));
        Assert.Equal(MatchStateHasher.Hash(original), MatchStateHasher.Hash(result.State));
        Assert.Null(result.State.PendingPayment);
        AssertNoPendingPayCostPrompt(result);
        AssertNoFioraTriggerPaymentSideEffects(result);
    }

    private static void AssertFioraPendingPaymentPreserved(
        PendingPaymentState? actual,
        PendingPaymentState expected,
        IReadOnlyList<string> expectedResourceActionIds)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.PaymentId, actual.PaymentId);
        Assert.Equal(expected.PaymentWindow, actual.PaymentWindow);
        Assert.Equal(expected.PlayerId, actual.PlayerId);
        Assert.Equal(expected.ManaCost, actual.ManaCost);
        Assert.Equal(expected.PowerCost, actual.PowerCost);
        Assert.Equal(expected.PowerCostByTrait, actual.PowerCostByTrait);
        Assert.Equal([PayOneYellowPower, Decline], actual.LegalPaymentChoiceIds);
        Assert.Equal(expectedResourceActionIds, actual.PaymentResourceActionIds);
    }

    private static void AssertFioraPayCostPromptMatchesPendingPayment(
        ResolutionResult result,
        IReadOnlyList<string> expectedResourceActionIds)
    {
        var payment = result.State.PendingPayment;
        Assert.NotNull(payment);
        var prompt = result.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        Assert.Equal(payment.PaymentId, Assert.IsType<string>(metadata["paymentId"]));
        Assert.Equal(payment.PaymentWindow, Assert.IsType<string>(metadata["paymentWindow"]));
        var cost = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(metadata["cost"]);
        Assert.Equal(payment.ManaCost, Assert.IsType<int>(cost["mana"]));
        var costPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(cost["powerByTrait"]);
        Assert.Equal(payment.PowerCostByTrait, costPowerByTrait);
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["paymentChoices"])
            .Select(choice => choice.Id)
            .ToArray();
        Assert.Equal([PayOneYellowPower, Decline], choices);
        var resourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["paymentResourceChoices"])
            .Select(choice => choice.Id)
            .ToArray();
        Assert.Equal(expectedResourceActionIds, resourceChoices);
    }

    private static void AssertNoPendingPayCostPrompt(ResolutionResult result)
    {
        Assert.Null(result.State.PendingPayment);
        var prompt = result.Prompts["P1"];
        Assert.NotEqual(PromptTypes.PayCost, prompt.View?.Type);
        Assert.DoesNotContain(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
    }

    private static void AssertNoFioraTriggerPaymentSideEffects(ResolutionResult result)
    {
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_PAYMENT_DECLINED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, FioraTrigger, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_CLEARED", StringComparison.Ordinal));
    }

    private static void AssertNextContestedBattlefieldNotAdvanced(ResolutionResult result)
    {
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "P1-BATTLEFIELD-NEXT-FIELD", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "P1-BATTLEFIELD-NEXT-FIELD", StringComparison.Ordinal));
        Assert.NotEqual(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.NotEqual(PromptTypes.SpellDuelFocus, result.Prompts["P1"].View?.Type);
    }

    private static void AssertNextContestedBattlefieldAdvancedAfterPaymentClosed(
        ResolutionResult result,
        bool declined)
    {
        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P1", result.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:P1-BATTLEFIELD-NEXT-FIELD", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, result.Prompts["P1"].View?.Type);
        Assert.Equal("P1-BATTLEFIELD-NEXT-FIELD", result.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.DoesNotContain(
            result.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, "P1-BATTLEFIELD-TREASURE-PILE", StringComparison.Ordinal));

        var paymentClosedIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["declined"], declined));
        var nextContestIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "P1-BATTLEFIELD-NEXT-FIELD", StringComparison.Ordinal));
        var nextSpellDuelIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "P1-BATTLEFIELD-NEXT-FIELD", StringComparison.Ordinal));

        Assert.True(paymentClosedIndex < nextContestIndex);
        Assert.True(nextContestIndex < nextSpellDuelIndex);
    }

    private static void AssertNextContestedBattlefieldStillAdvancedOnceAfterReplay(
        ResolutionResult closed,
        ResolutionResult replay,
        bool declined)
    {
        Assert.Equal(TimingStates.SpellDuelOpen, replay.State.TimingState);
        Assert.Equal(closed.State.PendingTaskQueue.Phase, replay.State.PendingTaskQueue.Phase);
        Assert.Equal(closed.State.PendingTaskQueue.ActiveTaskId, replay.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(closed.State.PendingTaskQueue.IsBlocking, replay.State.PendingTaskQueue.IsBlocking);
        Assert.Equal(
            closed.State.PendingTaskQueue.Tasks.Select(task => task.TaskId).ToArray(),
            replay.State.PendingTaskQueue.Tasks.Select(task => task.TaskId).ToArray());
        Assert.Equal(closed.Prompts["P1"].View?.Type, replay.Prompts["P1"].View?.Type);
        Assert.Equal(PromptTypes.SpellDuelFocus, replay.Prompts["P1"].View?.Type);
        Assert.Equal("P1-BATTLEFIELD-NEXT-FIELD", replay.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.Equal(
            1,
            CountEvents(closed.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal)
                && Equals(gameEvent.Payload["declined"], declined)));
        Assert.Empty(replay.Events);
    }

    private static int CountEvents(
        IReadOnlyList<GameEvent> events,
        Func<GameEvent, bool> predicate)
    {
        return events.Count(predicate);
    }

    private static int EventIndex(
        IReadOnlyList<GameEvent> events,
        Func<GameEvent, bool> predicate)
    {
        for (var index = 0; index < events.Count; index++)
        {
            if (predicate(events[index]))
            {
                return index;
            }
        }

        Assert.Fail("Expected event was not emitted.");
        return -1;
    }

    private static void AssertVayneStillOnBattlefield(MatchState state)
    {
        Assert.Contains("P1-BATTLEFIELD-VAYNE", state.PlayerZones["P1"].Battlefields);
        Assert.True(state.CardObjects.ContainsKey("P1-BATTLEFIELD-VAYNE"));
    }

    private static bool IsVayneTriggerResolved(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, VayneTrigger, StringComparison.Ordinal);
    }

    private static bool IsVayneReturnedToHand(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "UNIT_RETURNED_TO_HAND", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, VayneTrigger, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-VAYNE", StringComparison.Ordinal);
    }

    private static bool IsIcevaleTriggerResolved(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, IcevaleTrigger, StringComparison.Ordinal);
    }

    private static bool IsIcevalePowerModified(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, IcevaleTrigger, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-ICEVALE-TARGET", StringComparison.Ordinal);
    }

    private static bool IsJaxTriggerResolved(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, JaxTrigger, StringComparison.Ordinal);
    }

    private static void AssertNoJaxTriggerLeak(ResolutionResult result)
    {
        Assert.DoesNotContain(result.Events, gameEvent =>
            gameEvent.Payload.TryGetValue("trigger", out var trigger)
            && string.Equals(trigger as string, JaxTrigger, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            gameEvent.Payload.TryGetValue("reason", out var reason)
            && string.Equals(reason as string, JaxTrigger, StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> GoldTokenIds(MatchState state)
    {
        return state.PlayerZones["P1"].Base
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Tags.Contains("金币", StringComparer.Ordinal))
            .ToArray();
    }

    private static MatchState BuildFioraPowerfulReadyState(
        int yellowPower = 0,
        bool includeYellowRune = false,
        string fioraCardNo = "SFD·180/221")
    {
        var p1Base = includeYellowRune
            ? new[] { "P1-SFD-FIORA", "P1-FIORA-TARGET", "P1-FIORA-YELLOW-RUNE" }
            : ["P1-SFD-FIORA", "P1-FIORA-TARGET"];
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-FIORA-MERCY"] = new(
                "P1-FIORA-MERCY",
                cardNo: "OGN·053/298",
                manaCost: 3,
                tags: [CardObjectTags.SpellCard],
                ownerId: "P1",
                controllerId: "P1"),
            ["P1-SFD-FIORA"] = new(
                "P1-SFD-FIORA",
                cardNo: fioraCardNo,
                power: 3,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P1",
                controllerId: "P1"),
            ["P1-FIORA-TARGET"] = new(
                "P1-FIORA-TARGET",
                cardNo: "SFD·125/221",
                power: 4,
                isExhausted: true,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P1",
                controllerId: "P1"),
            ["P2-FIORA-DUMMY"] = new(
                "P2-FIORA-DUMMY",
                cardNo: "SFD·125/221",
                power: 2,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2")
        };
        if (includeYellowRune)
        {
            cardObjects["P1-FIORA-YELLOW-RUNE"] = new(
                "P1-FIORA-YELLOW-RUNE",
                cardNo: "RUNES·YELLOW",
                tags: [CardObjectTags.RuneCard, "COLOR:yellow"],
                ownerId: "P1",
                controllerId: "P1");
        }

        return new MatchState(
            roomId: "trigger-payment-fiora-test",
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
                ["P1"] = new(3, 0, yellowPower > 0
                    ? new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Yellow] = yellowPower
                    }
                    : null),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-FIORA-MERCY"],
                    Base = p1Base
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-FIORA-DUMMY"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: cardObjects);
    }

    private static TemporaryPaymentResourceState FioraTemporaryPaymentResource(
        string resourceId,
        string ownerPlayerId = "P1",
        IReadOnlyDictionary<string, int>? remainingPowerByTrait = null,
        IReadOnlyList<string>? allowedPaymentKinds = null)
    {
        var typedPower = PaymentCostRules.NormalizePowerCostByTrait(
            remainingPowerByTrait ?? new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RuneTrait.Yellow] = 1
            });
        return new TemporaryPaymentResourceState(
            resourceId,
            ownerPlayerId,
            "P1-UNITY-SIGIL",
            P4ActivatedAbilityCatalog.UnitySigilResourceAbilityId,
            "ACTIVATE_ABILITY",
            generatedPower: 0,
            remainingPower: 0,
            allowedPaymentKinds: allowedPaymentKinds ?? [PaymentCostRules.RuneCostPaymentKind],
            createdTick: 1,
            generatedPowerByTrait: typedPower,
            remainingPowerByTrait: typedPower);
    }

    private static MatchState BuildBattlefieldConquerGoldState(
        int mana = 1,
        bool includeNextContest = false)
    {
        string[] p1Battlefields = includeNextContest
            ? [
                "P1-BATTLEFIELD-TREASURE-PILE",
                "P1-BATTLEFIELD-GOLD-ATTACKER",
                "P1-BATTLEFIELD-NEXT-FIELD",
                "P1-BATTLEFIELD-NEXT-UNIT"
            ]
            : ["P1-BATTLEFIELD-TREASURE-PILE", "P1-BATTLEFIELD-GOLD-ATTACKER"];
        string[] p2Battlefields = includeNextContest
            ? ["P2-BATTLEFIELD-GOLD-DEFENDER", "P2-BATTLEFIELD-NEXT-UNIT"]
            : ["P2-BATTLEFIELD-GOLD-DEFENDER"];
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
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
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["P1-BATTLEFIELD-TREASURE-PILE"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-TREASURE-PILE"),
            ["P1-BATTLEFIELD-GOLD-ATTACKER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-TREASURE-PILE"),
            ["P2-BATTLEFIELD-GOLD-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-TREASURE-PILE")
        };
        string[] untilEndOfTurnEffects = includeNextContest
            ? [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-TREASURE-PILE")]
            : [];
        if (includeNextContest)
        {
            cardObjects["P1-BATTLEFIELD-NEXT-FIELD"] = new(
                "P1-BATTLEFIELD-NEXT-FIELD",
                cardNo: "OGN·275/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1");
            cardObjects["P1-BATTLEFIELD-NEXT-UNIT"] = new(
                "P1-BATTLEFIELD-NEXT-UNIT",
                cardNo: "SFD·125/221",
                power: 2,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P1",
                controllerId: "P1");
            cardObjects["P2-BATTLEFIELD-NEXT-UNIT"] = new(
                "P2-BATTLEFIELD-NEXT-UNIT",
                cardNo: "SFD·125/221",
                power: 2,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2");
            objectLocations["P1-BATTLEFIELD-NEXT-FIELD"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-NEXT-FIELD");
            objectLocations["P1-BATTLEFIELD-NEXT-UNIT"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-NEXT-FIELD");
            objectLocations["P2-BATTLEFIELD-NEXT-UNIT"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-NEXT-FIELD");
        }

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
                    Battlefields = p1Battlefields
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = p2Battlefields
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: cardObjects,
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            untilEndOfTurnEffects: includeNextContest ? untilEndOfTurnEffects : null,
            objectLocations: includeNextContest ? objectLocations : null);
    }

    private static MatchState BuildOgnVayneConquerRecallState(
        int mana = 1,
        bool faceDown = false,
        bool standby = false,
        bool opponentControlled = false)
    {
        var vayneTags = standby
            ? new[] { CardObjectTags.UnitCard, CardObjectTags.Standby, "强攻3" }
            : [CardObjectTags.UnitCard, "强攻3"];
        return new MatchState(
            roomId: "trigger-payment-vayne-test",
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
                    Battlefields = ["P1-BATTLEFIELD-VAYNE-FIELD", "P1-BATTLEFIELD-VAYNE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-VAYNE-DEFENDER"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-VAYNE-FIELD"] = new(
                    "P1-BATTLEFIELD-VAYNE-FIELD",
                    cardNo: "SFD·013/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-VAYNE"] = new(
                    "P1-BATTLEFIELD-VAYNE",
                    isFaceDown: faceDown,
                    cardNo: "OGN·035/298",
                    power: 2,
                    tags: vayneTags,
                    ownerId: "P1",
                    controllerId: opponentControlled ? "P2" : "P1"),
                ["P2-BATTLEFIELD-VAYNE-DEFENDER"] = new(
                    "P2-BATTLEFIELD-VAYNE-DEFENDER",
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

    private static MatchState BuildIcevaleArcherAttackState(
        int mana = 1,
        bool faceDown = false,
        bool standby = false,
        bool opponentControlled = false,
        bool includeInvalidTargets = false)
    {
        var icevaleTags = standby
            ? new[] { CardObjectTags.UnitCard, CardObjectTags.Standby }
            : [CardObjectTags.UnitCard];
        var p1Battlefields = new List<string>
        {
            "P1-BATTLEFIELD-ICEVALE-FIELD",
            "P1-BATTLEFIELD-ICEVALE"
        };
        var p1Base = new List<string>();
        var p2Battlefields = new List<string>
        {
            "P2-BATTLEFIELD-ICEVALE-DEFENDER",
            "P2-BATTLEFIELD-ICEVALE-TARGET"
        };
        var p2Base = new List<string>();
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-BATTLEFIELD-ICEVALE-FIELD"] = new(
                "P1-BATTLEFIELD-ICEVALE-FIELD",
                cardNo: "SFD·013/221",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1"),
            ["P1-BATTLEFIELD-ICEVALE"] = new(
                "P1-BATTLEFIELD-ICEVALE",
                isFaceDown: faceDown,
                cardNo: "UNL-065/219",
                power: 2,
                tags: icevaleTags,
                ownerId: "P1",
                controllerId: opponentControlled ? "P2" : "P1"),
            ["P2-BATTLEFIELD-ICEVALE-DEFENDER"] = new(
                "P2-BATTLEFIELD-ICEVALE-DEFENDER",
                cardNo: "SFD·125/221",
                power: 1,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2"),
            ["P2-BATTLEFIELD-ICEVALE-TARGET"] = new(
                "P2-BATTLEFIELD-ICEVALE-TARGET",
                cardNo: "SFD·125/221",
                power: 3,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2")
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["P1-BATTLEFIELD-ICEVALE-FIELD"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-ICEVALE-FIELD"),
            ["P1-BATTLEFIELD-ICEVALE"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-ICEVALE-FIELD"),
            ["P2-BATTLEFIELD-ICEVALE-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-ICEVALE-FIELD"),
            ["P2-BATTLEFIELD-ICEVALE-TARGET"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-ICEVALE-FIELD")
        };
        if (includeInvalidTargets)
        {
            p2Base.Add("P2-BATTLEFIELD-ICEVALE-OFF-FIELD");
            p2Battlefields.AddRange(
            [
                "P2-BATTLEFIELD-ICEVALE-WRONG-BATTLEFIELD",
                "P2-BATTLEFIELD-ICEVALE-FACE-DOWN-TARGET"
            ]);
            cardObjects["P2-BATTLEFIELD-ICEVALE-OFF-FIELD"] = new(
                "P2-BATTLEFIELD-ICEVALE-OFF-FIELD",
                cardNo: "SFD·125/221",
                power: 3,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2");
            cardObjects["P2-BATTLEFIELD-ICEVALE-WRONG-BATTLEFIELD"] = new(
                "P2-BATTLEFIELD-ICEVALE-WRONG-BATTLEFIELD",
                cardNo: "SFD·125/221",
                power: 3,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2");
            cardObjects["P2-BATTLEFIELD-ICEVALE-FACE-DOWN-TARGET"] = new(
                "P2-BATTLEFIELD-ICEVALE-FACE-DOWN-TARGET",
                isFaceDown: true,
                cardNo: "SFD·125/221",
                power: 3,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2");
            objectLocations["P2-BATTLEFIELD-ICEVALE-OFF-FIELD"] = new("P2", "BASE");
            objectLocations["P2-BATTLEFIELD-ICEVALE-WRONG-BATTLEFIELD"] = new("P2", "BATTLEFIELD", "P2-OTHER-BATTLEFIELD");
            objectLocations["P2-BATTLEFIELD-ICEVALE-FACE-DOWN-TARGET"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-ICEVALE-FIELD");
        }

        return new MatchState(
            roomId: "trigger-payment-icevale-test",
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
                    Battlefields = p1Battlefields,
                    Base = p1Base
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = p2Battlefields,
                    Base = p2Base
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: cardObjects,
            objectLocations: objectLocations,
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            }) with
            {
                UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-ICEVALE-FIELD")]
            };
    }

    private static MatchState BuildJaxWeaponAttachState(
        int mana = 1,
        string jaxCardNo = "SFD·119/221",
        bool faceDown = false,
        bool standby = false,
        bool opponentControlled = false,
        bool nonEquipment = false)
    {
        var jaxTags = standby
            ? new[] { CardObjectTags.UnitCard, CardObjectTags.Standby, "百炼" }
            : [CardObjectTags.UnitCard, "百炼"];
        var weaponTags = nonEquipment
            ? new[] { CardObjectTags.EquipmentCard }
            : [CardObjectTags.EquipmentCard, "武装"];
        return new MatchState(
            roomId: "trigger-payment-jax-attach-test",
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
                ["P1"] = new(
                    mana,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-JAX-DRAW-001", "P1-JAX-DRAW-002"],
                    Base = ["P1-JAX-WEAPON", "P1-JAX"]
                },
                ["P2"] = PlayerZones.Empty
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-JAX-WEAPON"] = new(
                    "P1-JAX-WEAPON",
                    cardNo: "SFD·022/221",
                    tags: weaponTags,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-JAX"] = new(
                    "P1-JAX",
                    isFaceDown: faceDown,
                    cardNo: jaxCardNo,
                    power: 3,
                    tags: jaxTags,
                    ownerId: "P1",
                    controllerId: opponentControlled ? "P2" : "P1"),
                ["P1-JAX-DRAW-001"] = new(
                    "P1-JAX-DRAW-001",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-JAX-DRAW-002"] = new(
                    "P1-JAX-DRAW-002",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildBattlefieldConquerPowerfulDrawState(
        int mana = 1,
        int power = 5)
    {
        return new MatchState(
            roomId: "trigger-payment-sunken-temple-test",
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
                    MainDeck = ["P1-BATTLEFIELD-POWERFUL-DRAW-001", "P1-BATTLEFIELD-POWERFUL-DRAW-002"],
                    Battlefields = ["P1-BATTLEFIELD-SUNKEN-TEMPLE", "P1-BATTLEFIELD-POWERFUL-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-POWERFUL-DEFENDER"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SUNKEN-TEMPLE"] = new(
                    "P1-BATTLEFIELD-SUNKEN-TEMPLE",
                    cardNo: "SFD·218/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-POWERFUL-ATTACKER"] = new(
                    "P1-BATTLEFIELD-POWERFUL-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: power,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-POWERFUL-DEFENDER"] = new(
                    "P2-BATTLEFIELD-POWERFUL-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-POWERFUL-DRAW-001"] = new(
                    "P1-BATTLEFIELD-POWERFUL-DRAW-001",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-POWERFUL-DRAW-002"] = new(
                    "P1-BATTLEFIELD-POWERFUL-DRAW-002",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }
}

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
        Assert.Equal(2, paid.State.CardObjects["P2-BATTLEFIELD-ICEVALE-TARGET"].Power);
        Assert.Equal(-1, paid.State.CardObjects["P2-BATTLEFIELD-ICEVALE-TARGET"].UntilEndOfTurnPowerModifier);
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

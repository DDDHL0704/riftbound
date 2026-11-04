using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private const string PaidRecallKind = "PAID_RECALL";
    private static readonly IReadOnlyDictionary<string, int> PaidRecallPower = new Dictionary<string, int> { [RuneTrait.Red] = 1 };

    private static bool CurrentPaidRecallTarget(MatchState state, ActivatedPlayGrant grant)
        => grant.Target is { ObjectId.Length: > 0, Generation: >= 0 } binding
            && state.CardObjects.TryGetValue(binding.ObjectId, out var target) && target.ObjectGeneration == binding.Generation
            && IsObjectOnField(state.PlayerZones, binding.ObjectId);

    private static StackResolutionResult ResolveActivatedPaidRecall(MatchState state, StackItemState item)
    {
        var id = item.TargetObjectIds.Single();
        var legal = item.BorrowedExhaust is { } borrowed
            ? IsBorrowedExhaustTarget(state, item.ControllerId, id,
                BorrowedExhaustRules.Spec(borrowed.Donor.CardNo, borrowed.AbilityIndex)!, item.SourceObjectId)
            : P4ActivatedAbilityCatalog.TryGetByAbilityId(item.SimpleActivation!.AbilityId, out var ability)
                && IsSimpleAbilityTarget(state, item.ControllerId, item.SourceObjectId, id, ability);
        if (!StackObjectRules.ReferencesCurrentObject(state, item, id) || !legal) return NoopStackResolutionResult(state);
        var grant = new ActivatedPlayGrant(state.TurnNumber, item.ControllerId, item.CardNo!, item.StackItemId,
            item.BorrowedExhaust, item.SimpleActivation) { Kind = PaidRecallKind,
                Target = new(id, state.CardObjects[id].ObjectGeneration) };
        if (!ValidActivatedPlayGrant(state, grant)) throw new InvalidOperationException("Invalid paid recall grant.");
        return NoopStackResolutionResult(state) with {
            UntilEndOfTurnEffects = AddUntilEndOfTurnEffect(state.UntilEndOfTurnEffects, ActivatedPlayGrantMarker(grant)),
            Events = [new("PAID_RECALL_GRANTED", "本回合目标下次被摧毁时，可支付红色符能改为休眠召回",
                new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["targetObjectId"] = id,
                    ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator) })] };
    }

    private static void AddPaidRecallOptions(MatchState state, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, Dictionary<string, RunePool> pools, string target, string player,
        List<DestructionOption> options, ref bool optionalAvailable)
    {
        foreach (var marker in state.UntilEndOfTurnEffects)
        {
            if (ReadActivatedPlayGrant(marker) is not { Kind: PaidRecallKind, Target: { } binding } grant || binding.ObjectId != target
                || binding.Generation != cards[target].ObjectGeneration
                || RuleChoices.Value!.ConsumedNextUnitPlayGrants.Contains(grant.StackItemId)) continue;
            var payer = grant.ControllerId;
            if (payer == player) optionalAvailable = true;
            var payments = new List<string>();
            if (PaymentCostRules.CanPayPowerCost(pools.GetValueOrDefault(payer, RunePool.Empty), 0, PaidRecallPower)) payments.Add("");
            payments.AddRange(PaidRecallRunes(zones, cards, payer).Select(id => "RUNE:" + id));
            // The affected player orders replacements; the skill controller
            // independently authorizes payment and any resource skills.
            if (payer != player && payments.Count == 0
                && ReplacementResourceActions(ReplacementResourceContext(state, zones, cards, pools, payer), payer).Count > 0)
                payments.Add("");
            var source = grant.Borrowed?.Borrower.Object.ObjectId ?? grant.Printed!.Source.ObjectId;
            var identity = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(grant.StackItemId)));
            foreach (var payment in payments)
                options.Add(new(new($"PAID_RECALL:{target}:{identity}" + (payment.Length == 0 ? "" : ":" + payment),
                    payment.Length == 0 ? "支付 1 点红色符能，改为休眠召回（保留伤害）"
                        : $"回收红色符文 {payment[5..]} 支付，改为休眠召回（保留伤害）", [target]),
                    target, source, PaidRecallKind, "PAID_RECALL:" + grant.StackItemId, grant.CardNo, Optional: true,
                    Payment: payment, PaidRecall: grant));
        }
    }

    private static IEnumerable<string> PaidRecallRunes(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, string player)
        => zones[player].Base.Where(id => cards.TryGetValue(id, out var card) && !card.IsFaceDown
            && card.Tags.Contains(CardObjectTags.RuneCard) && SourceObjectControlledByPlayerOrLegacyOwned(card, player)
            && TryGetRuneTrait(card, out var trait) && trait == RuneTrait.Red).Order(StringComparer.Ordinal);

    private static bool ConfirmPaidRecallPayment(MatchState state, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, Dictionary<string, RunePool> pools, string affectedPlayer,
        DestructionOption option, List<GameEvent> events)
    {
        var payer = option.PaidRecall!.ControllerId;
        if (payer == affectedPlayer) return true;
        while (true)
        {
            var context = ReplacementResourceContext(state, zones, cards, pools, payer);
            var resources = ReplacementResourceActions(context, payer);
            var choices = resources.Select(r => r.Choice).ToList();
            if (option.Payment.StartsWith("RUNE:", StringComparison.Ordinal)
                ? PaidRecallRunes(zones, cards, payer).Contains(option.Payment[5..])
                : PaymentCostRules.CanPayPowerCost(pools.GetValueOrDefault(payer, RunePool.Empty), 0, PaidRecallPower))
                choices.Add(new("PAY", "支付 1 点红色符能，改为休眠召回", [option.Target]));
            choices.Add(new("DECLINE", "不支付召回费用"));
            var selected = RuleChoices.Value!.Choose(payer, "是否支付来路不明的武器授予的召回费用？", choices, powerCost: 1);
            if (selected == "DECLINE") return false;
            if (selected == "PAY") return true;
            ApplyReplacementResourceAction(context, zones, cards, pools, payer, resources.Single(r => r.Choice.Id == selected), events);
        }
    }

    private static void PayActivatedRecall(MatchState state, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, Dictionary<string, RunePool> pools,
        DestructionOption option, List<GameEvent> events)
    {
        var grant = option.PaidRecall!;
        if (option.Payment.StartsWith("RUNE:", StringComparison.Ordinal))
            ApplyRuleChoiceRecycleRuneResource(ReplacementResourceContext(state, zones, cards, pools, grant.ControllerId),
                zones, cards, pools, grant.ControllerId, option.Payment[5..], events, RuleChoiceWindow);
        pools[grant.ControllerId] = PaymentCostRules.PayPowerCost(pools.GetValueOrDefault(grant.ControllerId, RunePool.Empty),
            0, PaidRecallPower, PowerPaymentPurpose.Other);
        RuleChoices.Value!.ConsumedNextUnitPlayGrants.Add(grant.StackItemId);
        events.Add(new("COST_PAID", "支付来路不明的武器召回费用", new Dictionary<string, object?> {
            ["playerId"] = grant.ControllerId, ["mana"] = 0, ["power"] = 1, ["powerByTrait"] = PaidRecallPower,
            ["reason"] = PaidRecallKind, [ActivatedPlayGrantConsumedKey] = new[] { grant.StackItemId } }));
    }
}

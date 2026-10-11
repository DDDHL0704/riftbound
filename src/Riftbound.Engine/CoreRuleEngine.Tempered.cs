using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record TemperedGrant(ObjectBinding Source, string CardNo);
public sealed record ReferencedEquipmentCosts(ObjectBinding? DestroyedUnit = null, IReadOnlyList<ObjectBinding>? RecycledCards = null);
public sealed record TemperedContext(string CardNo, ObjectBinding Unit, string? TokenFactoryCardNo = null,
    TemperedGrant? Grant = null, ObjectBinding? Equipment = null, ReferencedEquipmentCosts? Costs = null,
    bool UnableToPay = false);

public sealed partial class CoreRuleEngine
{
    internal const string TemperedEffect = "PLAYED_UNIT_WEAPONMASTER";
    internal const string TemperedConfirmationWindow = "TEMPERED_CONFIRMATION";
    internal const string TemperedCostWindow = "TEMPERED_EQUIPMENT_COST";
    internal const string TemperedPaymentWindow = "TEMPERED_EQUIPMENT_PAYMENT";
    private const string TemperedObservationKey = "temperedObservation";
    private sealed record TemperedObservation(string Controller, TemperedContext Context);

    private static bool HasPrintedTempered(string? cardNo)
        => cardNo is not null && CardBehaviorRegistry.TryGetByCardNo(cardNo, out var behavior)
            && behavior.PlaysSourceToBaseAsUnit
            && behavior.SourceUnitTags.Split('|').Contains(CardEquipmentKeywordNames.Tempered, StringComparer.Ordinal);

    private static bool ValidTemperedDefinition(TemperedContext context)
    {
        if (context.TokenFactoryCardNo is null)
            return context.Grant is null && HasPrintedTempered(context.CardNo);
        return context.Grant is { } grant && grant.Source.Generation >= 0 && !string.IsNullOrWhiteSpace(grant.Source.ObjectId)
            && LegendActionAbilityCatalog.IsSourceCardNoForAbility(LegendActionAbilityCatalog.AzirLegendAbilityId, grant.CardNo)
            && P6TokenFactoryCatalog.TryGetByCardNo(context.TokenFactoryCardNo, out var factory)
            && factory.CreateObject("definition", "P1", "P1") is { } token && token.Tags.Contains(CardObjectTags.SandSoldier)
            && context.CardNo == (token.CardNo ?? string.Empty);
    }

    internal static bool ValidTempered(MatchState state, TemperedContext? context, string effect, string player, string sourceId,
        string? cardNo = null)
    {
        if (context is null) return effect != TemperedEffect;
        bool ValidBinding(ObjectBinding binding) => !string.IsNullOrWhiteSpace(binding.ObjectId) && binding.Generation >= 0
            && (!state.CardObjects.TryGetValue(binding.ObjectId, out var card) || binding.Generation <= card.ObjectGeneration);
        return effect == TemperedEffect && state.Seats.ContainsKey(player) && context.Unit.ObjectId == sourceId
            && ValidTemperedDefinition(context) && ValidBinding(context.Unit)
            && (cardNo is null || context.CardNo == cardNo)
            && (context.Equipment is null || context.Equipment.ObjectId != sourceId && ValidBinding(context.Equipment))
            && (context.Grant is null || ValidBinding(context.Grant.Source)
                && context.Grant.Source.ObjectId != sourceId
                && (!state.CardObjects.TryGetValue(context.Grant.Source.ObjectId, out var grantSource)
                    || grantSource.ObjectGeneration != context.Grant.Source.Generation || grantSource.CardNo == context.Grant.CardNo))
            && (!state.CardObjects.TryGetValue(sourceId, out var source) || source.ObjectGeneration != context.Unit.Generation
                || (source.CardNo ?? string.Empty) == context.CardNo);
    }

    internal static bool ValidTemperedStack(MatchState state, StackItemState item, bool resolving = false)
    {
        if (!ValidTempered(state, item.Tempered, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo)) return false;
        if (item.Tempered is not { } context) return true;
        if (item.SourceConfirmed || item.EffectRepeatCount != 1 || item.OptionalCosts.Count != 0
            || context.UnableToPay && !resolving) return false;
        if (context.Equipment is not { } gear)
            return context.Costs is null && !context.UnableToPay && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null;
        if (!item.TargetObjectIds.SequenceEqual([gear.ObjectId]) || item.TargetGenerations is not { Count: 1 }
            || item.TargetGenerations.GetValueOrDefault(gear.ObjectId, -1) != gear.Generation) return false;
        if (context.Costs is not { } costs) return true;
        if (!state.CardObjects.TryGetValue(gear.ObjectId, out var card)
            || !AssembleEquipmentProfileCatalog.TryGet(card.CardNo ?? string.Empty, out var profile)) return false;
        if (costs.DestroyedUnit is null && costs.RecycledCards is null) return false;
        bool Binding(ObjectBinding binding) => binding.Generation >= 0 && !string.IsNullOrWhiteSpace(binding.ObjectId)
            && state.CardObjects.TryGetValue(binding.ObjectId, out var known) && binding.Generation <= known.ObjectGeneration;
        return (costs.DestroyedUnit is null || profile.RequiresDestroyFriendlyUnitCost && Binding(costs.DestroyedUnit))
            && (costs.RecycledCards is null || costs.RecycledCards.Count == profile.RequiredGraveyardRecycleCardCount
                && costs.RecycledCards.Count > 0 && costs.RecycledCards.All(Binding)
                && costs.RecycledCards.Select(b => b.ObjectId).Distinct(StringComparer.Ordinal).Count() == costs.RecycledCards.Count);
    }

    private static GameEvent CaptureTempered(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState unit)
    {
        if (unit.IsFaceDown || unit.Tags.Contains(CardObjectTags.Standby)
            || !unit.Tags.Contains(CardEquipmentKeywordNames.Tempered)
            || FindFieldObjectLocation(zones, unit.ObjectId) is not { } field) return ev;
        var controller = string.IsNullOrWhiteSpace(unit.ControllerId) ? field.PlayerId : unit.ControllerId;
        TemperedGrant? grant = null; string? factoryNo = null;
        if (!HasPrintedTempered(unit.CardNo))
        {
            if (!unit.Tags.Contains(CardObjectTags.SandSoldier) || !zones.TryGetValue(controller, out var playerZones)) return ev;
            var origin = playerZones.LegendZone.Select(id => cards.GetValueOrDefault(id)).FirstOrDefault(c => c is not null
                && !c.IsFaceDown && SourceObjectControlledByPlayerOrLegacyOwned(c, controller)
                && LegendActionAbilityCatalog.IsSourceCardNoForAbility(LegendActionAbilityCatalog.AzirLegendAbilityId, c.CardNo));
            if (origin is null) return ev;
            factoryNo = unit.TokenFactoryCardNo;
            grant = new(new(origin.ObjectId, origin.ObjectGeneration), origin.CardNo!);
        }
        var context = new TemperedContext(unit.CardNo ?? string.Empty, new(unit.ObjectId, unit.ObjectGeneration), factoryNo, grant);
        if (!ValidTemperedDefinition(context)) return ev;
        return ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
            [TemperedObservationKey] = new TemperedObservation(controller, context) } };
    }

    private static ResolutionResult QueueTempered(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted || !result.Events.Any(e => e.Payload.ContainsKey(TemperedObservationKey))) return result;
        var events = new List<GameEvent>(); var queue = result.State.TriggerQueue.ToList();
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i)))
        {
            if (ev.Payload.GetValueOrDefault(TemperedObservationKey) is not TemperedObservation observed) { events.Add(ev); continue; }
            // The captured private generation bindings never enter public events.
            events.Add(ev with { Payload = ev.Payload.Where(p => p.Key != TemperedObservationKey)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal) });
            if (result.State.Status != MatchStatuses.InProgress) continue;
            var context = observed.Context;
            if (before.CardObjects.TryGetValue(context.Unit.ObjectId, out var old) && !IsObjectOnField(before.PlayerZones, old.ObjectId))
                context = context with { Unit = context.Unit with { Generation = Math.Max(context.Unit.Generation, checked(old.ObjectGeneration + 1)) } };
            var trigger = new TriggerQueueItemState($"tempered-{result.State.Tick}-{index}-{context.Unit.ObjectId}",
                observed.Controller, context.Unit.ObjectId, TemperedEffect, ev.Kind) { SourceCardNo = context.CardNo, Tempered = context };
            queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
        }
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static string[] TemperedTargets(MatchState state, StackItemState item)
        => state.CardObjects.Values.Where(c => !c.IsFaceDown && c.Tags.Contains(CardObjectTags.EquipmentCard)
            && c.Tags.Contains(CardEquipmentKeywordNames.Weapon) && IsObjectOnField(state.PlayerZones, c.ObjectId)
            && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == item.ControllerId)
            .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();

    private static ResolutionResult PrepareTempered(ResolutionResult result, StackItemState item)
    {
        var legal = TemperedTargets(result.State, item);
        if (legal.Length == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var choice = new PendingCardChoiceState("TEMPERED:" + item.StackItemId, TemperedConfirmationWindow, item.ControllerId,
            0, 1, legal, [item.SourceObjectId], "百炼：选择一件你控制的武装；不选则放弃。确认后双方可响应，随后按调整后的装配费用执行。",
            item.SourceObjectId, TemperedEffect) { ResolvingStackItemId = item.StackItemId };
        var state = result.State with { PendingCardChoice = choice, ActivePlayerId = item.ControllerId, PriorityPlayerId = null };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    internal static bool ValidTemperedChoice(MatchState state, PendingCardChoiceState choice)
        => state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId) is { Tempered: { Equipment: null } } item
            && ValidTemperedStack(state, item)
            && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null
            && choice.ChoiceWindow == TemperedConfirmationWindow && choice.ChoiceId == "TEMPERED:" + item.StackItemId
            && choice.PlayerId == item.ControllerId && choice.SourceObjectId == item.SourceObjectId && choice.EffectKind == TemperedEffect
            && choice.RequiredCount == 0 && choice.MaxCount == 1 && choice.ContextObjectIds.SequenceEqual([item.SourceObjectId])
            && choice.LegalObjectIds.SequenceEqual(TemperedTargets(state, item));

    private static ResolutionResult ResolveTemperedChoice(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        if (!ValidTemperedChoice(state, choice)) return RejectWithCorePrompts(state, "百炼确认状态不再合法。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == choice.ResolvingStackItemId);
        var next = state with { Tick = state.Tick + 1, PendingCardChoice = null };
        if (selected.Count == 0) return DiscardUnconfirmedTrigger(new(true, null, next, [], ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next)), item);
        var gear = state.CardObjects[selected[0]];
        item = item with { Tempered = item.Tempered! with { Equipment = new(gear.ObjectId, gear.ObjectGeneration) },
            TargetObjectIds = [gear.ObjectId], TargetGenerations = new Dictionary<string, long> { [gear.ObjectId] = gear.ObjectGeneration } };
        next = RestoreAfterTriggerConfirmation(next with { StackItems = next.StackItems.Select(i => i.StackItemId == item.StackItemId ? item : i).ToArray() }, item);
        return new(true, null, next, [new("TRIGGER_CONFIRMED", "已确认百炼武装，等待双方响应", new Dictionary<string, object?> {
            ["sourceObjectId"] = item.SourceObjectId, ["targetObjectIds"] = item.TargetObjectIds.ToArray(), ["effectKind"] = item.EffectKind })],
            ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static bool TemperedGear(MatchState state, StackItemState item, out CardObjectState gear,
        out AssembleEquipmentProfile profile)
    {
        gear = null!; profile = null!;
        return ValidTemperedStack(state, item, resolving: true) && item.Tempered!.Equipment is { } binding
            && state.CardObjects.TryGetValue(binding.ObjectId, out gear!) && gear.ObjectGeneration == binding.Generation
            && !gear.IsFaceDown && gear.Tags.Contains(CardObjectTags.EquipmentCard) && gear.Tags.Contains(CardEquipmentKeywordNames.Weapon)
            && IsObjectOnField(state.PlayerZones, gear.ObjectId)
            && EffectiveFieldControllerId(state.PlayerZones, gear.ObjectId, gear) == item.ControllerId
            && AssembleEquipmentProfileCatalog.TryGet(gear.CardNo ?? string.Empty, out profile!);
    }

    private static string[] TemperedCostObjects(MatchState state, StackItemState item, AssembleEquipmentProfile profile, bool destruction)
        => destruction ? state.CardObjects.Keys.Where(id => CanDestroyFriendlyUnitAsAdditionalCost(state, item.ControllerId, id)
                && !state.CardObjects[id].IsFaceDown).Order(StringComparer.Ordinal).ToArray()
            : state.PlayerZones[item.ControllerId].Graveyard.Where(id => CanRecycleGraveyardCardAsAdditionalCost(state, item.ControllerId, id))
                .Order(StringComparer.Ordinal).ToArray();

    private static bool NextTemperedCost(StackItemState item, AssembleEquipmentProfile profile, out bool destruction, out int count)
    {
        destruction = profile.RequiresDestroyFriendlyUnitCost && item.Tempered!.Costs?.DestroyedUnit is null;
        count = destruction ? 1 : item.Tempered!.Costs?.RecycledCards is null ? profile.RequiredGraveyardRecycleCardCount : 0;
        return count > 0;
    }

    private static PendingCardChoiceState TemperedCostChoice(MatchState state, StackItemState item,
        AssembleEquipmentProfile profile, bool destruction, int count)
        => new("TEMPERED-COST:" + item.StackItemId + (destruction ? ":DESTROY" : ":RECYCLE"), TemperedCostWindow,
            item.ControllerId, count, count, TemperedCostObjects(state, item, profile, destruction), [item.SourceObjectId],
            destruction ? "百炼结算：选择摧毁一名友方单位以支付所引用的装配费用。" : $"百炼结算：选择回收 {count} 张废牌以支付所引用的装配费用。",
            item.SourceObjectId, TemperedEffect) { ResolvingStackItemId = item.StackItemId };

    internal static bool ValidTemperedCostChoice(MatchState state, PendingCardChoiceState choice)
    {
        if (state.StackItems.LastOrDefault() is not { } item || item.StackItemId != choice.ResolvingStackItemId
            || !TemperedGear(state, item, out _, out var profile) || !NextTemperedCost(item, profile, out var destroy, out var count)) return false;
        var expected = TemperedCostChoice(state, item, profile, destroy, count);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow
            && choice.PlayerId == expected.PlayerId && choice.SourceObjectId == expected.SourceObjectId && choice.EffectKind == expected.EffectKind
            && choice.RequiredCount == count && choice.MaxCount == count && choice.ContextObjectIds.SequenceEqual([item.SourceObjectId])
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds) && choice.LegalObjectIds.Count >= count;
    }

    private static PendingPaymentState TemperedPayment(StackItemState item, EquipmentCostAmounts amounts)
        => new("TEMPERED-PAY:" + item.StackItemId, TemperedPaymentWindow, item.ControllerId, amounts.Mana,
            amounts.GenericPower, amounts.PowerByTrait, ["PAY", "UNABLE_TO_PAY"],
            "百炼结算：支付所引用的装配费用；可以先使用符文或反应资源技能。仅在资源不足时可选择无法支付，武装将留在原处。")
            { ResolvingStackItemId = item.StackItemId, ExperienceCost = amounts.Experience };

    private static EquipmentCostAmounts TemperedAmounts(MatchState state, StackItemState item, AssembleEquipmentProfile profile)
        => EquipmentCosts(state, profile, state.CardObjects[item.SourceObjectId], genericPowerReduction: 1);

    internal static bool ValidTemperedPayment(MatchState state, PendingPaymentState payment)
    {
        if (state.StackItems.LastOrDefault() is not { } item || item.StackItemId != payment.ResolvingStackItemId
            || !TemperedGear(state, item, out _, out var profile) || NextTemperedCost(item, profile, out _, out _)
            || !state.CardObjects.ContainsKey(item.SourceObjectId)) return false;
        var expected = TemperedPayment(item, TemperedAmounts(state, item, profile));
        return payment.PaymentId == expected.PaymentId && payment.PaymentWindow == expected.PaymentWindow
            && payment.PlayerId == expected.PlayerId && payment.ManaCost == expected.ManaCost && payment.PowerCost == expected.PowerCost
            && payment.ExperienceCost == expected.ExperienceCost && payment.PowerCostByTrait.Count == expected.PowerCostByTrait.Count
            && payment.PowerCostByTrait.All(p => expected.PowerCostByTrait.GetValueOrDefault(p.Key, -1) == p.Value)
            && payment.LegalPaymentChoiceIds.SequenceEqual(expected.LegalPaymentChoiceIds) && payment.PaymentResourceActionIds.Count == 0;
    }

    private static ResolutionResult ResolveTemperedCostChoice(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        if (!ValidTemperedCostChoice(state, choice)) return RejectWithCorePrompts(state, "百炼装配费用选择已失效。", ErrorCodes.InvalidTarget);
        var item = state.StackItems[^1];
        TemperedGear(state, item, out _, out var profile); NextTemperedCost(item, profile, out var destroy, out _);
        var bindings = selected.Select(id => new ObjectBinding(id, state.CardObjects[id].ObjectGeneration)).ToArray();
        var costs = item.Tempered!.Costs ?? new();
        costs = destroy ? costs with { DestroyedUnit = bindings.Single() } : costs with { RecycledCards = bindings };
        var next = state with { PendingCardChoice = null, StackItems = state.StackItems.Take(state.StackItems.Count - 1)
            .Append(item with { Tempered = item.Tempered with { Costs = costs } }).ToArray() };
        return ResolvePassPriority(next, new("tempered-cost-continuation", item.ControllerId, CommandTypes.PassPriority), forceResolve: true);
    }

    private static ResolutionResult ResolveTemperedPayment(MatchState state, PendingPaymentState payment,
        IReadOnlyList<string> choices, int rawCount)
    {
        if (!ValidTemperedPayment(state, payment) || choices.Count != 1 || rawCount != 1 || choices[0] is not ("PAY" or "UNABLE_TO_PAY"))
            return RejectWithCorePrompts(state, "百炼费用支付状态不再合法。", ErrorCodes.InvalidTarget);
        var item = state.StackItems[^1]; TemperedGear(state, item, out _, out var profile);
        var plan = EquipmentPaymentPlan(TemperedAmounts(state, item, profile), profile, payment.PaymentId,
            TemperedPaymentWindow, item.ControllerId, item.SourceObjectId, [], []);
        var canPay = PaymentCostRules.AuthorizePayment(plan, state.RunePools.GetValueOrDefault(item.ControllerId, RunePool.Empty),
            state.PlayerExperience.GetValueOrDefault(item.ControllerId)).Accepted;
        if (choices[0] == "PAY" && !canPay) return RejectWithCorePrompts(state, "资源不足，可以先使用符文或反应资源技能。", ErrorCodes.InsufficientCost);
        if (choices[0] == "UNABLE_TO_PAY" && canPay) return RejectWithCorePrompts(state, "当前资源足以支付已确认技能的费用。", ErrorCodes.InvalidTarget);
        var next = state with { PendingPayment = null, StackItems = state.StackItems.Take(state.StackItems.Count - 1)
            .Append(item with { Tempered = item.Tempered! with { UnableToPay = !canPay } }).ToArray() };
        var resumed = ResolvePassPriority(next, new("tempered-payment-continuation", item.ControllerId, CommandTypes.PassPriority), forceResolve: true);
        return resumed with { Events = new[] { BuildPaymentWindowClosedEvent(payment, item.ControllerId, declined: !canPay) }.Concat(resumed.Events).ToArray() };
    }

    private static StackResolutionResult ResolveTempered(MatchState state, StackItemState item)
    {
        if (!TemperedGear(state, item, out var gear, out var profile) || item.Tempered!.UnableToPay
            || !state.CardObjects.ContainsKey(item.SourceObjectId)) return NoopStackResolutionResult(state);
        if (NextTemperedCost(item, profile, out var destroy, out var count))
        {
            var choice = TemperedCostChoice(state, item, profile, destroy, count);
            return choice.LegalObjectIds.Count < count ? NoopStackResolutionResult(state)
                : NoopStackResolutionResult(state) with { PendingCardChoice = choice, StackItems = state.StackItems };
        }
        var costs = item.Tempered.Costs;
        bool Continuous(ObjectBinding binding) => state.CardObjects.TryGetValue(binding.ObjectId, out var c) && c.ObjectGeneration == binding.Generation;
        if (costs?.DestroyedUnit is { } destroyed && (!Continuous(destroyed) || !CanDestroyFriendlyUnitAsAdditionalCost(state, item.ControllerId, destroyed.ObjectId))
            || costs?.RecycledCards is { } recycled && recycled.Any(b => !Continuous(b) || !CanRecycleGraveyardCardAsAdditionalCost(state, item.ControllerId, b.ObjectId)))
            return NoopStackResolutionResult(state);
        var amounts = TemperedAmounts(state, item, profile);
        var payment = TemperedPayment(item, amounts);
        var plan = EquipmentPaymentPlan(amounts, profile, payment.PaymentId, TemperedPaymentWindow,
            item.ControllerId, item.SourceObjectId, [], []);
        var commit = PaymentCostRules.TryCommitPayment(plan, state.RunePools, state.PlayerExperience);
        if (!commit.Accepted) return NoopStackResolutionResult(state) with { PendingPayment = payment,
            StackItems = state.StackItems, Events = [new("PAYMENT_WINDOW_OPENED", "等待支付百炼引用的装配费用",
                new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                    ["paymentId"] = payment.PaymentId, ["paymentWindow"] = TemperedPaymentWindow,
                    ["manaCost"] = amounts.Mana, ["powerCost"] = amounts.GenericPower,
                    ["powerCostByTrait"] = amounts.PowerByTrait, ["experienceCost"] = amounts.Experience })] };
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var events = new List<GameEvent> { new("COST_PAID", "支付百炼引用的装配费用", PaymentCostRules.BuildCostPaidPayload(plan,
            commit.RunePools, commit.PlayerExperience, new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId,
                ["equipmentObjectId"] = gear.ObjectId, ["effectKind"] = TemperedEffect, ["targetPower"] = ResolveCurrentFieldUnitPower(state, cards[item.SourceObjectId]) })) };
        var paid = state with { RunePools = commit.RunePools, PlayerExperience = commit.PlayerExperience };
        var result = NoopStackResolutionResult(paid);
        if (costs?.DestroyedUnit is { } costUnit)
        {
            var removal = ResolveFieldDestructions(paid, zones, cards, item, new HashSet<string>(),
                state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(StringComparer.Ordinal), commit.RunePools,
                objectLocations: state.ObjectLocations, explicitDestroyObjectIds: new HashSet<string> { costUnit.ObjectId }, explicitDestroyReason: "ADDITIONAL_COST");
            events.AddRange(removal.Events);
            result = result with { RunePools = removal.RunePools, DestroyedUnitOwnerIds = removal.DestroyedUnitOwnerIds, TriggerQueue = removal.TriggerQueue };
        }
        if (costs?.RecycledCards is { } costCards)
        {
            var recycledCost = RecycleGraveyardCards(paid, zones, item.ControllerId, item.SourceObjectId, costCards.Select(b => b.ObjectId).ToArray());
            events.AddRange(recycledCost.Events); result = result with { RngCursor = recycledCost.RngCursor };
        }
        IReadOnlyDictionary<string, ObjectLocationState> locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        if (cards.TryGetValue(item.SourceObjectId, out var host) && host.ObjectGeneration == item.Tempered.Unit.Generation
            && IsFaceUpNonStandbyUnit(host) && IsObjectOnField(zones, host.ObjectId))
            TryAttachEquipment(zones, cards, locations, item.ControllerId, item.SourceObjectId, gear.ObjectId,
                host.ObjectId, TemperedEffect, events, out locations, new Dictionary<string, object?> { ["unitCardNo"] = item.CardNo });
        return result with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, Events = events };
    }
}

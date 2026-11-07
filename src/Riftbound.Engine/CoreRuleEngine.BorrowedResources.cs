using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static bool BorrowedResourceGainSupported(MatchState state, string player,
        BorrowedExhaustSpec spec, CardObjectState source)
    {
        var pool = state.RunePools.GetValueOrDefault(player, RunePool.Empty);
        return BorrowedResourceGainSupported(pool, EffectiveBorrowedResourceSpec(state, player, source, spec), source)
            || spec.GeneratedMana == 0 && spec.GeneratedTraits.Count == 0 && spec.GeneratedPower > 0
                && BorrowedDestructionCostChoices(state, player, spec).Any(cost =>
                    CanStartDestructionResourceGain(state, player, source.ObjectId,
                        cost[DestroyFriendlyPermanentAdditionalCostPrefix.Length..], spec.GeneratedPower, BorrowedExhaustRules.Effect));
    }

    internal static bool TryBorrowedConversionMaximum(MatchState state, string player, CardObjectState source,
        BorrowedExhaustSpec spec, out int maximum)
        => ResourceQuantityRules.TryConversionMaximum(state.RunePools.GetValueOrDefault(player, RunePool.Empty),
            spec.Conversion == "MANA_TO_POWER", ManaPurposeForActivatedAbility(state, source.ObjectId),
            BorrowedPowerPurpose(source), out maximum, state);

    private static BorrowedExhaustSpec EffectiveBorrowedResourceSpec(MatchState state, string player,
        CardObjectState source, BorrowedExhaustSpec spec)
        => !spec.EffectRequiresEncouraged || EncourageActive(state, player, source.ObjectId) ? spec
            : spec with { GeneratedMana = 0, EmpoweredMana = null, GeneratedPower = 0,
                GeneratedTraits = new Dictionary<string, int>() };

    internal static bool BorrowedResourceGainSupported(RunePool pool, BorrowedExhaustSpec spec, CardObjectState source)
    {
        var mana = source.IsEmpowered ? spec.EmpoweredMana ?? spec.GeneratedMana : spec.GeneratedMana;
        return pool.HasSupportedAmounts
            && (long)pool.TotalStoredMana - spec.Mana + mana <= int.MaxValue
            && (long)pool.TotalStoredPower - spec.Power - spec.TypedPower.Values.Sum()
                + spec.GeneratedPower + spec.GeneratedTraits.Values.Sum() <= int.MaxValue;
    }

    private static ResolutionResult ResolveBorrowedResource(MatchState state, PlayerIntent intent,
        BorrowedExhaustAbility ability, PaymentCostRules.PaymentPlan plan, PaymentCostRules.PaymentCommitResult payment,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, ObjectLocationState> locations, List<GameEvent> events, IReadOnlyList<string> extraCosts,
        CardObjectState activationSource, IReadOnlyList<TriggerQueueItemState> destructionTriggers,
        IReadOnlyList<string> destroyedOwners, bool borrowerDestroyedAsCost)
    {
        var source = activationSource;
        var spec = EffectiveBorrowedResourceSpec(state, intent.PlayerId, source, ability.Spec);
        var pool = payment.RunePools.GetValueOrDefault(intent.PlayerId, RunePool.Empty);
        var mana = source.IsEmpowered ? spec.EmpoweredMana ?? spec.GeneratedMana : spec.GeneratedMana;
        var power = spec.GeneratedPower;
        var converted = 0;
        if (spec.Conversion.Length > 0)
        {
            var context = state with { CardObjects = cards, PlayerZones = zones, RunePools = payment.RunePools };
            if (!TryBorrowedConversionMaximum(context, intent.PlayerId, source, spec, out var maximum))
                return RejectWithCorePrompts(state, "资源数量超出支持范围。", ErrorCodes.UnsupportedCardBehavior);
            var choice = extraCosts.SingleOrDefault(id => id.StartsWith(BorrowedExhaustRules.ConversionChoicePrefix, StringComparison.Ordinal)) ?? "";
            if (choice == BorrowedExhaustRules.ConversionChoicePrefix + ResourceQuantityRules.ChooseAmountSuffix)
            {
                if ((long)maximum + 1 <= ResourceQuantityRules.DirectChoiceLimit)
                    return RejectWithCorePrompts(state, "该数量范围应直接选择精确数字。", ErrorCodes.InvalidTarget);
                converted = ChooseBoundedQuantity(context, intent.PlayerId, source.ObjectId, ability.Id,
                    "RESOURCE_CONVERSION", 0, maximum, ResourceConversionAmountKind, "选择资源转换数量；确认前不横置或支付资源。");
            }
            else if (!choice.StartsWith(BorrowedExhaustRules.ConversionChoicePrefix, StringComparison.Ordinal)
                || !int.TryParse(choice[BorrowedExhaustRules.ConversionChoicePrefix.Length..],
                    System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out converted)
                || converted > maximum)
                return RejectWithCorePrompts(state, "请选择可支付且不会溢出的资源转换数量。", ErrorCodes.InvalidTarget);
            if (spec.Conversion == "MANA_TO_POWER")
            {
                pool = PaymentCostRules.SpendMana(pool, converted, ManaPurposeForActivatedAbility(context, source.ObjectId), context);
                power = converted;
            }
            else
            {
                var conversionPlan = new PaymentCostRules.PaymentPlan(plan.PaymentId + ":CONVERSION", "RESOURCE_CONVERSION", intent.PlayerId,
                    genericPowerCost: converted, totalPowerCost: converted, sourceObjectId: source.ObjectId,
                    abilityId: ability.Id, powerPurpose: BorrowedPowerPurpose(source));
                var paid = PaymentCostRules.TryCommitPayment(conversionPlan, payment.RunePools, state: context);
                if (!paid.Accepted) return RejectWithCorePrompts(state, "符能不足。", ErrorCodes.InsufficientCost);
                pool = paid.RunePools[intent.PlayerId];
                mana = converted;
            }
        }
        if (!pool.HasSupportedAmounts || (long)pool.TotalStoredMana + mana > int.MaxValue
            || (long)pool.TotalStoredPower + power + spec.GeneratedTraits.Values.Sum() > int.MaxValue)
            return RejectWithCorePrompts(state, "资源数量超出支持范围，未获取资源或支付费用。", ErrorCodes.UnsupportedCardBehavior);
        var traits = pool.PowerByTrait.ToDictionary();
        foreach (var (color, amount) in spec.GeneratedTraits) traits[color] = traits.GetValueOrDefault(color) + amount;
        var pools = payment.RunePools.ToDictionary();
        pools[intent.PlayerId] = pool with {
            Mana = pool.Mana + (spec.ResourceRestriction == "" ? mana : 0),
            SpellOnlyMana = pool.SpellOnlyMana + (spec.ResourceRestriction == "SPELL" ? mana : 0),
            DuelOnlyMana = pool.DuelOnlyMana + (spec.ResourceRestriction == "DUEL" ? mana : 0),
            UnitPlayOrUnitAbilityOnlyMana = pool.UnitPlayOrUnitAbilityOnlyMana + (spec.ResourceRestriction == "UNIT" ? mana : 0),
            Power = pool.Power + (spec.ResourceRestriction == "" ? power : 0),
            SpellOnlyPower = pool.SpellOnlyPower + (spec.ResourceRestriction == "SPELL" ? power : 0),
            EquipmentOnlyPower = pool.EquipmentOnlyPower + (spec.ResourceRestriction == "EQUIPMENT" ? power : 0),
            UnitOrEquipmentPlayOnlyPower = pool.UnitOrEquipmentPlayOnlyPower + (spec.ResourceRestriction == "PERMANENT_PLAY" ? power : 0),
            PowerByTrait = traits };
        if (cards.ContainsKey(source.ObjectId) && !spec.DestroysSourceAsCost && !borrowerDestroyedAsCost)
            cards[source.ObjectId] = ExhaustForInstruction(zones, cards, source);
        var payload = new Dictionary<string, object?> {
            ["playerId"] = intent.PlayerId, ["controllerId"] = intent.PlayerId,
            ["sourceObjectId"] = source.ObjectId, ["targetObjectId"] = source.ObjectId,
            ["cardNo"] = source.CardNo, ["abilityId"] = ability.Id,
            ["generatedMana"] = mana, ["generatedPower"] = power,
            ["conversionAmount"] = converted, ["conversionKind"] = spec.Conversion,
            ["generatedPowerByTrait"] = spec.GeneratedTraits, ["resourceRestriction"] = spec.ResourceRestriction,
            ["resourceSkill"] = true, ["reactionSpeed"] = spec.Reaction, ["swiftSpeed"] = spec.Swift,
            ["resourceLifecycle"] = "rune-pool",
            ["stackPolicy"] = "no-ordinary-stack-item", ["timingContext"] = state.TimingState,
            ["wasExhausted"] = false, ["isExhausted"] = true };
        events.Add(new("ABILITY_ACTIVATED", "激活获得的资源技能", payload));
        events.Add(new(source.Tags.Contains(CardObjectTags.EquipmentCard) ? "EQUIPMENT_EXHAUSTED" : "UNIT_EXHAUSTED",
            "横置来源以支付资源技能费用", payload));
        events.Add(new("COST_PAID", "支付获得的资源技能费用",
            PaymentCostRules.BuildCostPaidPayload(plan, payment.RunePools, payment.PlayerExperience, payload)));
        events.Add(new("RESOURCE_SKILL_RESOLVED", "获得的资源技能即时结算", payload));
        if (mana > 0) events.Add(new("MANA_GAINED", "获得法力并保留用途限制", payload));
        if (power + spec.GeneratedTraits.Values.Sum() > 0)
            events.Add(new("POWER_GAINED", "获得符能并保留用途限制", payload));
        var next = state with { Tick = state.Tick + 1, CardObjects = cards, PlayerZones = zones,
            ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = pools,
            PlayerExperience = payment.PlayerExperience,
            TriggerQueue = state.TriggerQueue.Concat(destructionTriggers).ToArray(),
            DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(state.DestroyedUnitOwnerIdsThisTurn, destroyedOwners) };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }
}

using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
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
        Dictionary<string, ObjectLocationState> locations, List<GameEvent> events)
    {
        var source = cards[plan.SourceObjectId!];
        var spec = ability.Spec;
        var pool = payment.RunePools.GetValueOrDefault(intent.PlayerId, RunePool.Empty);
        var mana = source.IsEmpowered ? spec.EmpoweredMana ?? spec.GeneratedMana : spec.GeneratedMana;
        if (!pool.HasSupportedAmounts || (long)pool.TotalStoredMana + mana > int.MaxValue
            || (long)pool.TotalStoredPower + spec.GeneratedPower + spec.GeneratedTraits.Values.Sum() > int.MaxValue)
            return RejectWithCorePrompts(state, "资源数量超出支持范围，未获取资源或支付费用。", ErrorCodes.UnsupportedCardBehavior);
        var traits = pool.PowerByTrait.ToDictionary();
        foreach (var (color, amount) in spec.GeneratedTraits) traits[color] = traits.GetValueOrDefault(color) + amount;
        var pools = payment.RunePools.ToDictionary();
        pools[intent.PlayerId] = pool with {
            Mana = pool.Mana + (spec.ResourceRestriction == "" ? mana : 0),
            SpellOnlyMana = pool.SpellOnlyMana + (spec.ResourceRestriction == "SPELL" ? mana : 0),
            DuelOnlyMana = pool.DuelOnlyMana + (spec.ResourceRestriction == "DUEL" ? mana : 0),
            UnitPlayOrUnitAbilityOnlyMana = pool.UnitPlayOrUnitAbilityOnlyMana + (spec.ResourceRestriction == "UNIT" ? mana : 0),
            Power = pool.Power + (spec.ResourceRestriction == "" ? spec.GeneratedPower : 0),
            SpellOnlyPower = pool.SpellOnlyPower + (spec.ResourceRestriction == "SPELL" ? spec.GeneratedPower : 0),
            EquipmentOnlyPower = pool.EquipmentOnlyPower + (spec.ResourceRestriction == "EQUIPMENT" ? spec.GeneratedPower : 0),
            PowerByTrait = traits };
        cards[source.ObjectId] = ExhaustForInstruction(zones, cards, source);
        var payload = new Dictionary<string, object?> {
            ["playerId"] = intent.PlayerId, ["controllerId"] = intent.PlayerId,
            ["sourceObjectId"] = source.ObjectId, ["targetObjectId"] = source.ObjectId,
            ["cardNo"] = source.CardNo, ["abilityId"] = ability.Id,
            ["generatedMana"] = mana, ["generatedPower"] = spec.GeneratedPower,
            ["generatedPowerByTrait"] = spec.GeneratedTraits, ["resourceRestriction"] = spec.ResourceRestriction,
            ["resourceSkill"] = true, ["reactionSpeed"] = true, ["resourceLifecycle"] = "rune-pool",
            ["stackPolicy"] = "no-ordinary-stack-item", ["timingContext"] = state.TimingState,
            ["wasExhausted"] = false, ["isExhausted"] = true };
        events.Add(new("ABILITY_ACTIVATED", "激活获得的资源技能", payload));
        events.Add(new(source.Tags.Contains(CardObjectTags.EquipmentCard) ? "EQUIPMENT_EXHAUSTED" : "UNIT_EXHAUSTED",
            "横置来源以支付资源技能费用", payload));
        events.Add(new("COST_PAID", "支付获得的资源技能费用",
            PaymentCostRules.BuildCostPaidPayload(plan, payment.RunePools, payment.PlayerExperience, payload)));
        events.Add(new("RESOURCE_SKILL_RESOLVED", "获得的资源技能即时结算", payload));
        if (mana > 0) events.Add(new("MANA_GAINED", "获得法力并保留用途限制", payload));
        if (spec.GeneratedPower + spec.GeneratedTraits.Values.Sum() > 0)
            events.Add(new("POWER_GAINED", "获得符能并保留用途限制", payload));
        var next = state with { Tick = state.Tick + 1, CardObjects = cards, PlayerZones = zones,
            ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = pools,
            PlayerExperience = payment.PlayerExperience };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }
}

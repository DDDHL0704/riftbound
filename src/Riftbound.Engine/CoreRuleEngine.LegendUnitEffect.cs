using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LegendUnitEffectContext(string AbilityId, long SourceGeneration, int SpellshieldTaxPower);

public sealed partial class CoreRuleEngine
{
    internal static bool ValidLegendEquipmentUnit(MatchState state, CardObjectState card)
    {
        var transform = card.LegendEquipmentUnit;
        if (transform is null) return true;
        var originalTags = transform.OriginalTags ?? [];
        return transform.SourceLegend is { Generation: >= 0, ObjectId.Length: > 0 }
            && transform.OriginalPower >= 0
            && transform.CapturedTick >= 0 && transform.CapturedTick <= state.Tick
            && originalTags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
            && !originalTags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && card.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
            && card.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && card.Power == card.ManaCost;
    }

    internal static bool IsLegendUnitEffect(string effect)
        => effect is LegendAbilityEffectKinds.UnitPowerModifier or LegendAbilityEffectKinds.ReadyUnit
            or LegendAbilityEffectKinds.EquipmentAsUnit;

    internal static bool IsLegendEquipmentAsUnitEffect(string effect)
        => effect == LegendAbilityEffectKinds.EquipmentAsUnit;

    internal static bool IsLegendUnitEffectTarget(MatchState state, string player, string id, bool battlefieldOnly)
        => IsFieldObject(state.PlayerZones, id) && state.CardObjects.TryGetValue(id, out var card)
            && IsFaceUpNonStandbyUnit(card) && !string.IsNullOrWhiteSpace(card.CardNo)
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, player, id)
            && (!battlefieldOnly || state.ObjectLocations.TryGetValue(id, out var location) && location.Zone == MoveUnitBattlefieldZone);

    internal static bool IsLegendEquipmentUnitTarget(MatchState state, string player, string id)
        => IsLegendEquipmentReadyTarget(state, player, id)
            && state.CardObjects.TryGetValue(id, out var card)
            && CardControlRules.IsControlledByPlayerOrLegacyOwned(card, player);

    internal static bool ValidLegendUnitEffect(MatchState state, StackItemState item)
    {
        if (item.LegendUnitEffect is not { } context) return !IsLegendUnitEffect(item.EffectKind);
        if (!IsLegendUnitEffect(item.EffectKind) || !TryGetLegendAbility(context.AbilityId, out var ability)
            || ability.EffectKind != item.EffectKind
            || item.EffectKind != LegendAbilityEffectKinds.EquipmentAsUnit
                && (!ability.RemovesSourceEmpowerAsCost || !ability.RequiresAnyUnitTarget)
            || item.EffectKind == LegendAbilityEffectKinds.EquipmentAsUnit
                && (!ability.RequiresAnyEquipmentTarget || ability.RemovesSourceEmpowerAsCost)
            || !LegendActionAbilityCatalog.IsSourceCardNoForAbility(context.AbilityId, item.CardNo)
            || context.SourceGeneration < 0 || context.SpellshieldTaxPower < 0 || !state.Seats.ContainsKey(item.ControllerId)
            || string.IsNullOrWhiteSpace(item.SourceObjectId) || item.SourceConfirmed || item.EffectRepeatCount != 1
            || item.DamageAmount != 0
            || (item.EffectKind == LegendAbilityEffectKinds.EquipmentAsUnit
                ? !item.OptionalCosts.SequenceEqual(new[] { "SPEND_MANA:1" }, StringComparer.Ordinal)
                : item.OptionalCosts.Count != 0)
            || item.Destination.Length != 0
            || item.TargetObjectIds is not { Count: 1 } || item.TargetObjectIds[0] == item.SourceObjectId
            || item.TargetGenerations is not { Count: 1 } bindings
            || !bindings.TryGetValue(item.TargetObjectIds[0], out var generation) || generation < 0
            || state.CardObjects.TryGetValue(item.TargetObjectIds[0], out var target) && generation > target.ObjectGeneration
            || state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
                && (context.SourceGeneration > source.ObjectGeneration
                    || context.SourceGeneration == source.ObjectGeneration && source.CardNo != item.CardNo)) return false;
        return item.TimingContext == TimingStates.NeutralOpen;
    }

    private static ResolutionResult QueueLegendUnitEffect(MatchState state, string player, string sourceId,
        string cardNo, LegendAbilityDefinition ability, IReadOnlyList<string> targets, IReadOnlyList<string> optionalCosts,
        int spellshieldTax, List<GameEvent> events)
    {
        var item = new StackItemState($"STACK-{state.Tick + 1}-{sourceId}-LEGEND-UNIT", player, sourceId,
            ability.EffectKind, cardNo, targets, optionalCosts: optionalCosts,
            timingContext: StackTimingContextForNewStackItem(state)) {
            LegendUnitEffect = new(ability.AbilityId, state.CardObjects[sourceId].ObjectGeneration, spellshieldTax),
            TargetGenerations = targets.ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration, StringComparer.Ordinal)
        };
        var next = state with { Tick = state.Tick + 1, ActivePlayerId = player, TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = player, PassedPriorityPlayerIds = [], StackItems = [..state.StackItems, item] };
        events.Add(new("STACK_ITEM_ADDED", $"{ability.DisplayName}加入结算链", new Dictionary<string, object?> {
            ["stackItemId"] = item.StackItemId, ["controllerId"] = player, ["sourceObjectId"] = sourceId,
            ["cardNo"] = cardNo, ["effectKind"] = ability.EffectKind, ["abilityId"] = ability.AbilityId,
            ["targetObjectIds"] = targets.ToArray() }));
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveLegendUnitEffect(MatchState state, StackItemState item)
    {
        if (!ValidLegendUnitEffect(state, item)) throw new InvalidOperationException("Invalid captured legend unit effect.");
        var context = item.LegendUnitEffect!;
        TryGetLegendAbility(context.AbilityId, out var ability);
        var events = new List<GameEvent> { new("LEGEND_ABILITY_RESOLVED", "传奇单位技能结算", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
            ["cardNo"] = item.CardNo, ["abilityId"] = context.AbilityId, ["stackItemId"] = item.StackItemId }) };
        var id = item.TargetObjectIds[0];
        if (!(ability.EffectKind == LegendAbilityEffectKinds.EquipmentAsUnit
                ? IsLegendEquipmentUnitTarget(state, item.ControllerId, id)
                : IsLegendUnitEffectTarget(state, item.ControllerId, id, ability.RequiresBattlefieldTarget))
            || !state.CardObjects.TryGetValue(id, out var target) || target.ObjectGeneration != item.TargetGenerations![id])
        {
            events.Add(new("ABILITY_NO_EFFECT", "目标不再合法，已支付的费用不退回", new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["abilityId"] = context.AbilityId,
                ["stackItemId"] = item.StackItemId, ["reason"] = "TARGET_NO_LONGER_LEGAL" }));
            return NoopStackResolutionResult(state) with { Events = events };
        }
        var cards = state.CardObjects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (ability.EffectKind == LegendAbilityEffectKinds.EquipmentAsUnit)
        {
            var previous = cards.Values.FirstOrDefault(card => card.LegendEquipmentUnit is { } transform
                && transform.SourceLegend.ObjectId == item.SourceObjectId
                && transform.SourceLegend.Generation == context.SourceGeneration);
            if (previous is not null && previous.ObjectId != id)
            {
                cards[previous.ObjectId] = previous with {
                    Power = previous.LegendEquipmentUnit!.OriginalPower,
                    Tags = previous.LegendEquipmentUnit.OriginalTags,
                    LegendEquipmentUnit = null };
                events.Add(new("LEGEND_EQUIPMENT_UNIT_ENDED", "发条魔灵选择了另一件装备，旧装备恢复为装备", new Dictionary<string, object?> {
                    ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                    ["targetObjectId"] = previous.ObjectId, ["abilityId"] = context.AbilityId }));
            }

            var current = cards[id];
            if (current.LegendEquipmentUnit is { } existing
                && existing.SourceLegend.ObjectId == item.SourceObjectId
                && existing.SourceLegend.Generation == context.SourceGeneration)
            {
                current = current with { Power = existing.OriginalPower, Tags = existing.OriginalTags,
                    LegendEquipmentUnit = null };
            }
            cards[id] = current with {
                Power = current.ManaCost,
                Tags = current.Tags.Append(CardObjectTags.UnitCard).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                LegendEquipmentUnit = new(new(item.SourceObjectId, context.SourceGeneration), current.Power, current.Tags, state.Tick + 1) };
            events.Add(new("LEGEND_EQUIPMENT_UNIT_STARTED", "装备变为单位装备", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                ["targetObjectId"] = id, ["power"] = current.ManaCost, ["manaCost"] = current.ManaCost,
                ["abilityId"] = context.AbilityId }));
        }
        else if (ability.EffectKind == LegendAbilityEffectKinds.UnitPowerModifier)
        {
            var behavior = new CardBehaviorDefinition(item.CardNo, ability.DisplayName, 0, item.EffectKind, 0, 1,
                TargetScope: CardTargetScopes.AnyUnit, PowerModifierAmount: ability.PowerModifierAmount);
            cards[id] = ApplyPowerModifier(state, target, behavior, item, id, ability.PowerModifierAmount, out var modified);
            events.Add(modified);
        }
        else if (!AttachedRuleTextRules.Instances(state, target)
            .Any(instance => CardStaticAbilitySpecRules.CardCannotBecomeActive(instance.CardNo)))
        {
            AddReadiedObservation(state.PlayerZones, cards, target, events);
            cards[id] = target with { IsExhausted = false };
            events.Add(CaptureSelfReady(new("UNIT_READIED", "单位变为活跃状态", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id,
                ["abilityId"] = context.AbilityId, ["wasExhausted"] = target.IsExhausted, ["isExhausted"] = false }), target, item.ControllerId, state.PlayerZones, cards));
        }
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}

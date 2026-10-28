using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LegendEquipmentReadyContext(string AbilityId, long SourceGeneration,
    bool SourceWasEmpowered, int SpellshieldTaxPower,
    IReadOnlyDictionary<string, string>? TargetTokenFactories = null);

public sealed partial class CoreRuleEngine
{
    internal static bool IsLegendEquipmentReadyEffect(string effect)
        => effect == LegendAbilityEffectKinds.ReadyEquipment;

    internal static bool IsLegendEquipmentReadyTarget(MatchState state, string player, string id)
        => IsFieldObject(state.PlayerZones, id) && state.CardObjects.TryGetValue(id, out var card)
            && !card.IsFaceDown && !string.IsNullOrWhiteSpace(card.CardNo)
            && card.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, player, id);

    internal static bool ValidLegendEquipmentReady(MatchState state, StackItemState item)
    {
        if (item.LegendEquipmentReady is not { } context) return !IsLegendEquipmentReadyEffect(item.EffectKind);
        if (!IsLegendEquipmentReadyEffect(item.EffectKind) || !TryGetLegendAbility(context.AbilityId, out var ability)
            || ability.EffectKind != item.EffectKind || !ability.RequiresAnyEquipmentTarget
            || ability.RequiresSourceEmpowered && !context.SourceWasEmpowered
            || !LegendActionAbilityCatalog.IsSourceCardNoForAbility(context.AbilityId, item.CardNo)
            || context.SourceGeneration < 0 || context.SpellshieldTaxPower < 0 || !state.Seats.ContainsKey(item.ControllerId)
            || string.IsNullOrWhiteSpace(item.SourceObjectId) || item.SourceConfirmed || item.EffectRepeatCount != 1
            || item.DamageAmount != 0 || !item.OptionalCosts.SequenceEqual(new[] { "SPEND_MANA:1" }, StringComparer.Ordinal)
            || item.Destination.Length != 0 || item.TargetObjectIds.Count != ability.RequiredTargetCount
            || item.TargetObjectIds.Distinct(StringComparer.Ordinal).Count() != item.TargetObjectIds.Count
            || item.TargetObjectIds.Contains(item.SourceObjectId, StringComparer.Ordinal)
            || item.TargetGenerations is not { } bindings || bindings.Count != item.TargetObjectIds.Count
            || item.TargetObjectIds.Any(id => !bindings.TryGetValue(id, out var generation) || generation < 0
                || state.CardObjects.TryGetValue(id, out var target) && generation > target.ObjectGeneration)
            || context.TargetTokenFactories is { } factories && factories.Any(pair => !item.TargetObjectIds.Contains(pair.Key, StringComparer.Ordinal)
                || !P6TokenFactoryCatalog.IsTokenFactory(pair.Value)
                || state.CardObjects.TryGetValue(pair.Key, out var token)
                    && (token.TokenFactoryCardNo ?? token.CardNo) != pair.Value)
            || item.TargetObjectIds.Any(id => state.CardObjects.TryGetValue(id, out var token)
                && token.ObjectGeneration == bindings[id] && TokenObjectRules.IsToken(token)
                && (context.TargetTokenFactories is null || !context.TargetTokenFactories.TryGetValue(id, out var factory)
                    || factory != (token.TokenFactoryCardNo ?? token.CardNo)))
            || item.TargetObjectIds.Any(id => !state.CardObjects.ContainsKey(id)
                && (context.TargetTokenFactories is null || !context.TargetTokenFactories.ContainsKey(id)))
            || state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
                && (context.SourceGeneration > source.ObjectGeneration
                    || context.SourceGeneration == source.ObjectGeneration && source.CardNo != item.CardNo)) return false;
        return item.TimingContext == TimingStates.NeutralOpen;
    }

    private static ResolutionResult QueueLegendEquipmentReady(MatchState state, string player, string sourceId,
        string cardNo, LegendAbilityDefinition ability, IReadOnlyList<string> targets, IReadOnlyList<string> optionalCosts,
        int spellshieldTax, List<GameEvent> events)
    {
        var source = state.CardObjects[sourceId];
        var tokenFactories = targets.Where(id => TokenObjectRules.IsToken(state.CardObjects[id]))
            .ToDictionary(id => id, id => state.CardObjects[id].TokenFactoryCardNo ?? state.CardObjects[id].CardNo!, StringComparer.Ordinal);
        var item = new StackItemState($"STACK-{state.Tick + 1}-{sourceId}-LEGEND-EQUIPMENT", player, sourceId,
            ability.EffectKind, cardNo, targets, optionalCosts: optionalCosts, timingContext: StackTimingContextForNewStackItem(state)) {
            LegendEquipmentReady = new(ability.AbilityId, source.ObjectGeneration, source.IsEmpowered, spellshieldTax,
                tokenFactories.Count == 0 ? null : tokenFactories),
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

    private static StackResolutionResult ResolveLegendEquipmentReady(MatchState state, StackItemState item)
    {
        if (!ValidLegendEquipmentReady(state, item)) throw new InvalidOperationException("Invalid captured legend equipment effect.");
        var context = item.LegendEquipmentReady!;
        var cards = state.CardObjects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var events = new List<GameEvent> { new("LEGEND_ABILITY_RESOLVED", "传奇装备技能结算", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
            ["cardNo"] = item.CardNo, ["abilityId"] = context.AbilityId, ["stackItemId"] = item.StackItemId }) };
        foreach (var id in item.TargetObjectIds)
        {
            if (!IsLegendEquipmentReadyTarget(state, item.ControllerId, id)
                || !cards.TryGetValue(id, out var equipment) || equipment.ObjectGeneration != item.TargetGenerations![id]) continue;
            if (AttachedRuleTextRules.Instances(state, equipment)
                .Any(instance => CardStaticAbilitySpecRules.CardCannotBecomeActive(instance.CardNo))) continue;
            cards[id] = equipment with { IsExhausted = false };
            events.Add(new("EQUIPMENT_READIED", "装备变为活跃状态", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id,
                ["abilityId"] = context.AbilityId, ["wasExhausted"] = equipment.IsExhausted, ["isExhausted"] = false }));
        }
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}

using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // This is a choice made while resolving the instruction, not a target or
    // an activation cost. Destruction replacements must finish before the reward.
    private static StackResolutionResult ResolveOptionalEquipmentDestructionMight(
        MatchState state, StackItemState item, int amount)
    {
        var legal = FriendlyDestructionChoices(state, item.ControllerId, requiredTag: CardObjectTags.EquipmentCard);
        if (legal.Length == 0) return NoopStackResolutionResult(state);
        var frame = RuleChoices.Value ?? throw new InvalidOperationException("Equipment destruction requires a rule choice frame.");
        var options = new[] { new RuleChoiceOption("SKIP", "不摧毁装备") }.Concat(legal.Select(card =>
            new RuleChoiceOption("DESTROY:" + card.ObjectId, "摧毁装备（" + card.ObjectId + "）", [card.ObjectId]))).ToArray();
        var selected = frame.Choose(item.ControllerId, "你可以摧毁一件友方装备，使来源单位本回合获得战力加成。",
            options, kind: "DESTRUCTION_SELECTION", preview: state);
        if (selected == "SKIP") return NoopStackResolutionResult(state);
        var destroyed = selected["DESTROY:".Length..];
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var removal = ResolveFieldDestructions(state, zones, cards, item, new HashSet<string>(),
            state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(), state.RunePools, objectLocations: state.ObjectLocations,
            explicitDestroyObjectIds: new HashSet<string> { destroyed }, explicitDestroyReason: "SKILL_EFFECT");
        var events = removal.Events.ToList();
        if (removal.DestroyedObjectIds.Contains(destroyed)
            && cards.TryGetValue(item.SourceObjectId, out var source)
            && source.ObjectGeneration == item.UnitEvent!.SourceGeneration && IsObjectOnField(zones, source.ObjectId))
        {
            cards[source.ObjectId] = ApplyDirectUntilEndPowerModifier(source, source.ObjectId, item.SourceObjectId,
                item.CardNo, item.EffectKind, "DESTROY_FRIENDLY_EQUIPMENT", amount, source.Power + amount);
            events.Add(new("UNIT_POWER_CHANGED", "摧毁友方装备后，来源单位本回合获得战力", new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = source.ObjectId, ["powerDelta"] = amount }));
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), RunePools = removal.RunePools,
            TriggerQueue = removal.TriggerQueue, DestroyedUnitOwnerIds = removal.DestroyedUnitOwnerIds, Events = events };
    }
}

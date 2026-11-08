using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static CardObjectState[] AttachedArmamentChoices(MatchState state, string host)
        => state.CardObjects.Values.Where(gear => gear.AttachedToObjectId == host && !gear.IsFaceDown
                && gear.Tags.Contains(CardObjectTags.EquipmentCard) && IsArmamentKeywordProvider(gear)
                && IsObjectOnField(state.PlayerZones, gear.ObjectId)
                && IsSameFieldLocation(state.PlayerZones, state.ObjectLocations, gear.ObjectId, host))
            .OrderBy(gear => gear.ObjectId, StringComparer.Ordinal).ToArray();

    internal static bool IsArmamentTargetConditionAllowed(MatchState state, CardBehaviorDefinition behavior,
        string target, int index)
        => !behavior.FirstTargetRequiresArmament || index != 0 || AttachedArmamentChoices(state, target).Length > 0;

    // CN321 defers cleanup until the whole chain item is finished. CN436.3:
    // choosing which attached weapon to detach is not an additional target.
    // The ordinary rule-choice checkpoint repeats the uncommitted damage and
    // instruction together; retry/recovery cannot commit either one twice.
    private static void DetachArmamentFromFirstTarget(MatchState state, StackItemState item,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards, List<GameEvent> events)
    {
        var host = item.TargetObjectIds[0];
        if (string.IsNullOrWhiteSpace(host) || !StackObjectRules.ReferencesCurrentObject(state, item, host)
            || !cards.TryGetValue(host, out var unit) || !IsFaceUpNonStandbyUnit(unit)
            || !IsObjectOnField(zones, host)) return;
        var current = state with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
        var choices = AttachedArmamentChoices(current, host);
        if (choices.Length == 0) return;
        var selected = choices.Length == 1 ? choices[0].ObjectId
            : RuleChoices.Value!.Choose(item.ControllerId, "选择卸除这名单位的一件武装。",
                choices.Select(gear => new RuleChoiceOption(gear.ObjectId, DeckChoiceLabel(current, gear.ObjectId),
                    [gear.ObjectId])).ToArray(), kind: "DETACH_ARMAMENT", preview: current);
        var gear = cards[selected];
        using var instruction = new MightInstructionScope(current, zones, cards, events);
        cards[selected] = gear with { AttachedToObjectId = null };
        events.Add(new("EQUIPMENT_DETACHED", "卸除所选单位的一件武装", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
            ["unitObjectId"] = host, ["equipmentObjectId"] = selected,
            ["controllerId"] = gear.ControllerId, ["ownerId"] = gear.OwnerId,
            ["previousAttachedToObjectId"] = host, ["reason"] = item.EffectKind }));
    }
}

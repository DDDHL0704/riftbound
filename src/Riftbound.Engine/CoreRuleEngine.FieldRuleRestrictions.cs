using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // Current public text, location and control determine the restriction.
    // Moving or losing a source takes effect during the next instruction.
    private static bool HasEnemyBattlefieldRestriction(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, string player, string kind)
    {
        foreach (var id in zones.Values.SelectMany(zone => zone.Battlefields).Distinct(StringComparer.Ordinal)) {
            if (!cards.TryGetValue(id, out var source) || source.IsFaceDown
                || FindFieldObjectLocation(zones, id) is not { Zone: "BATTLEFIELD" }) continue;
            var controller = EffectiveFieldControllerId(zones, id, source);
            if (string.IsNullOrWhiteSpace(controller) || controller == player) continue;
            if (AttachedRuleTextRules.Instances(zones, cards, source).Any(instance =>
                CardStaticAbilitySpecRules.TryGetStaticAbility(instance.CardNo, spec => spec.Kind == kind, out _))) return true;
        }
        return false;
    }

    private static bool UnitPlayPermittedByFieldRules(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, string player, string destination)
        => destination == "BASE" || !HasEnemyBattlefieldRestriction(zones, cards, player,
            StaticAbilityKinds.BattlefieldOpponentUnitsBaseOnly);

    private static GameEvent PreventedUnitPlay(string player, string source, string destination)
        => new("UNIT_PLAY_PREVENTED", "场上效果阻止了单位的打出。", new Dictionary<string, object?> {
            ["playerId"] = player, ["sourceObjectId"] = source, ["destination"] = destination });

    // CN 054 / 055 / 369.3 / 416: this gate belongs to the ready action in a
    // resolving spell or ability. Active entry and the wake task use their own
    // existing operations and do not call this predicate.
    private static bool ReadyByEffectPermittedByFieldRules(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState target)
    {
        if (target.IsFaceDown || !IsObjectOnField(zones, target.ObjectId)
            || !target.Tags.Contains(CardObjectTags.UnitCard) && !target.Tags.Contains(CardObjectTags.EquipmentCard)) return true;
        var controller = EffectiveFieldControllerId(zones, target.ObjectId, target);
        return string.IsNullOrWhiteSpace(controller) || !HasEnemyBattlefieldRestriction(zones, cards, controller,
            StaticAbilityKinds.BattlefieldEnemyUnitEquipmentCannotReadyByEffect);
    }
}

using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static readonly Lazy<IReadOnlySet<string>> EnemyEffectMovementProtectedArmaments = new(() =>
        OfficialArmamentCatalog.All.Where(definition => definition.EffectText.Trim().TrimEnd('。', '.')
                == "我无法被敌方法术和技能移动")
            .Select(definition => definition.CardNo).ToHashSet(StringComparer.Ordinal));

    // A movement prohibition constrains this instruction at its execution.
    // It does not grant target protection or block the controller's own moves.
    private static bool CanMoveUnitByEffect(MatchState state, string effectController, string movingPlayer, string id)
    {
        if (!state.CardObjects.TryGetValue(id, out var unit) || !MovementRestrictionRules.CanMove(unit, movingPlayer)) return false;
        if (EffectiveFieldControllerId(state.PlayerZones, id, unit) == effectController) return true;
        return !state.CardObjects.Values.Any(gear => gear.AttachedToObjectId == id && !gear.IsFaceDown
            && gear.Tags.Contains(CardObjectTags.EquipmentCard) && IsObjectOnField(state.PlayerZones, gear.ObjectId)
            && IsObjectOnField(state.PlayerZones, id) && !unit.IsFaceDown
            && EnemyEffectMovementProtectedArmaments.Value.Contains(AttachedRuleTextRules.Face(gear) ?? "")
            && IsSameFieldLocation(state.PlayerZones, state.ObjectLocations, gear.ObjectId, id));
    }
}

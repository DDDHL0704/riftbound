using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record SelectedBattlefieldEffectSpec(int EnemyPowerModifier, bool MoveOptionalEnemy);

public sealed partial class CoreRuleEngine
{
    internal static bool HasFriendlyUnitAtSelectedBattlefield(MatchState state, string playerId, string battlefieldId)
        => BattlefieldLocalRules.Battlefield(state, battlefieldId) is not null
            && state.CardObjects.Keys.Any(id => IsPlayerControlledFieldUnitObject(state, playerId, id)
                && BattlefieldLocalRules.AtUnit(state, id)?.ObjectId == battlefieldId);

    internal static bool IsSelectedBattlefieldTargetRelationAllowed(MatchState state,
        CardBehaviorDefinition behavior, IReadOnlyList<string> targets, int index)
        => behavior.SelectedBattlefieldEffect is null || index == 0
            || index == 1 && targets.Count > 1 && behavior.SelectedBattlefieldEffect.MoveOptionalEnemy
                && IsUnitMovementDestination(state, targets[1], "BATTLEFIELD:" + targets[0]);

    private static void ResolveSelectedBattlefieldEffect(MatchState state,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, ObjectLocationState> locations, CardBehaviorDefinition behavior,
        StackItemState item, List<GameEvent> events)
    {
        if (behavior.SelectedBattlefieldEffect is not { } spec || item.TargetObjectIds.Count == 0
            || string.IsNullOrWhiteSpace(item.TargetObjectIds[0])) return;
        var field = item.TargetObjectIds[0];
        var current = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations };
        if (!HasFriendlyUnitAtSelectedBattlefield(current, item.ControllerId, field)) return;

        for (var repeat = 0; repeat < item.EffectRepeatCount; repeat++)
        {
            if (spec.MoveOptionalEnemy && item.TargetObjectIds.Count > 1
                && !string.IsNullOrWhiteSpace(item.TargetObjectIds[1]))
                MoveFieldUnitsToChosenLocation(current, zones, cards, locations, item,
                    [item.TargetObjectIds[1]], "BATTLEFIELD:" + field, events);

            // CN 355.5.a: the chosen battlefield/unit are targets; the local group
            // is collected after movement and neither pays Ward nor gets retargeted.
            var affected = cards.Keys.Where(id => IsEnemyBattlefieldUnitObject(current, item.ControllerId, id)
                && BattlefieldLocalRules.AtUnit(current, id)?.ObjectId == field)
                .Order(StringComparer.Ordinal).ToArray();
            foreach (var id in affected)
            {
                cards[id] = ApplyPowerModifier(cards[id], behavior, item, id, spec.EnemyPowerModifier, out var powerEvent);
                events.Add(powerEvent);
            }
        }
    }
}

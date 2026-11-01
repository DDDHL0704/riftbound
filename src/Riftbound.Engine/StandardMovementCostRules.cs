namespace Riftbound.Engine;

internal static class StandardMovementCostRules
{
    internal readonly record struct MovementCost(int Mana, int Power,
        IReadOnlyDictionary<string, int>? PowerCostByTrait = null)
    {
        public bool IsZero => Mana == 0 && Power == 0 && (PowerCostByTrait is null || PowerCostByTrait.Values.Sum() == 0);
    }

    public static MovementCost ForMove(MatchState state, string playerId, string? battlefieldId, int unitCount)
    {
        if (battlefieldId is null || !state.BattlefieldStates.TryGetValue(battlefieldId, out var battlefield))
            return default;

        var mana = 0;
        var genericPower = 0;
        var gravityFields = state.ObjectLocations.Count(entry =>
                entry.Value.Zone == "BATTLEFIELD"
                && entry.Value.BattlefieldObjectId == battlefieldId
                && state.CardObjects.TryGetValue(entry.Key, out var source)
                && source.CardNo == "RAD·062/167"
                && !source.IsFaceDown
                && source.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
                && state.PlayerZones.Values.Any(zones => zones.Battlefields.Contains(entry.Key, StringComparer.Ordinal))
                && CoreRuleEngine.EffectiveFieldControllerId(state.PlayerZones, entry.Key, source) != playerId);
        if (gravityFields > 0)
        {
            // RAD·062 imposes {{1}} plus {{A}}.  The latter is one generic
            // power, rather than an orange typed-power requirement.
            mana = gravityFields;
            genericPower = gravityFields;
        }

        var power = genericPower + PowerPerExtraUnit(state, playerId, battlefieldId) * Math.Max(0, unitCount - 1);
        var typed = new Dictionary<string, int>(StringComparer.Ordinal);
        return new(mana, power, typed);
    }

    // CN 204.4 / UNL-163/219: independent text instances impose independent
    // costs. Copied weapon text belongs to the weapon's controller (CN 718.5.f).
    public static int PowerPerExtraUnit(MatchState state, string playerId, string? battlefieldId)
    {
        if (battlefieldId is null || !state.BattlefieldStates.TryGetValue(battlefieldId, out var battlefield)) return 0;
        var count = 0;
        foreach (var id in battlefield.OccupantObjectIds)
        {
            if (!state.CardObjects.TryGetValue(id, out var unit) || !FieldObjectTypeRules.IsVisibleUnit(unit)) continue;
            foreach (var text in AttachedRuleTextRules.Instances(state, unit))
            {
                if (!CardBehaviorRegistry.TryGetByCardNo(text.CardNo, out var behavior)
                    || behavior.EffectKind != "MAGESEEKER_PATROL_MOVE_TAX_STATIC") continue;
                var source = unit;
                if (text.Origin is { } origin && state.CardObjects.TryGetValue(origin.Equipment.ObjectId, out var gear)
                    && AttachedRuleTextRules.CopiesPrintedText(gear.CardNo))
                {
                    if (gear.ObjectGeneration != origin.Equipment.Generation || !AttachedRuleTextRules.Bound(state, gear)
                        || state.ObjectLocations.GetValueOrDefault(gear.ObjectId) is not { Zone: "BATTLEFIELD" } position
                        || position.BattlefieldObjectId != battlefieldId) continue;
                    source = gear;
                }
                if (!source.IsFaceDown && !CardControlRules.IsControlledByPlayerOrLegacyOwned(source, playerId)) count++;
            }
        }
        return count;
    }

    public static bool TryPowerCost(MatchState state, string playerId, string? battlefieldId, int unitCount, out int cost)
    {
        var movement = ForMove(state, playerId, battlefieldId, unitCount);
        var amount = (long)movement.Power + (movement.PowerCostByTrait?.Values.Sum() ?? 0);
        cost = amount <= int.MaxValue ? (int)amount : 0;
        return amount <= int.MaxValue;
    }
}

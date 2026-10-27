namespace Riftbound.Engine;

internal static class StandardMovementCostRules
{
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
        var amount = (long)PowerPerExtraUnit(state, playerId, battlefieldId) * Math.Max(0, unitCount - 1);
        cost = amount <= int.MaxValue ? (int)amount : 0;
        return amount <= int.MaxValue;
    }
}

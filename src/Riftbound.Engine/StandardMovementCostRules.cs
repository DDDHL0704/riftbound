namespace Riftbound.Engine;

internal static class StandardMovementCostRules
{
    // CN 204.4: independent text instances impose independent fees on a move.
    // Copied weapon text belongs to the weapon's controller (CN 718.5.f).
    private static IEnumerable<MovementImposedCostSpec> Costs(MatchState state, string playerId, string? battlefieldId)
    {
        if (battlefieldId is null || !state.BattlefieldStates.ContainsKey(battlefieldId)) yield break;
        // Battlefield occupants list units. Passive fees can also belong to
        // non-unit equipment, so enumerate every public field permanent.
        foreach (var id in state.PlayerZones.Values.SelectMany(z => z.Battlefields).Distinct(StringComparer.Ordinal))
        {
            if (state.ObjectLocations.GetValueOrDefault(id) is not { Zone: "BATTLEFIELD" } location
                || location.BattlefieldObjectId != battlefieldId) continue;
            if (!state.CardObjects.TryGetValue(id, out var unit) || unit.IsFaceDown
                || !(FieldObjectTypeRules.IsVisibleUnit(unit) || unit.Tags.Contains(CardObjectTags.EquipmentCard))) continue;
            foreach (var text in AttachedRuleTextRules.Instances(state, unit))
            {
                if (!CardBehaviorRegistry.TryGetByCardNo(text.CardNo, out var behavior)
                    || behavior.MovementImposedCost is not { } cost) continue;
                var source = unit;
                if (text.Origin is { } origin && state.CardObjects.TryGetValue(origin.Equipment.ObjectId, out var gear)
                    && AttachedRuleTextRules.CopiesPrintedText(gear.CardNo))
                {
                    if (gear.ObjectGeneration != origin.Equipment.Generation || !AttachedRuleTextRules.Bound(state, gear)) continue;
                    source = gear;
                }
                if (!source.IsFaceDown && state.ObjectLocations.GetValueOrDefault(source.ObjectId) is { Zone: "BATTLEFIELD" } position
                    && position.BattlefieldObjectId == battlefieldId
                    && !CardControlRules.IsControlledByPlayerOrLegacyOwned(source, playerId)) yield return cost;
            }
        }
    }

    public static int PowerPerExtraUnit(MatchState state, string playerId, string? battlefieldId)
        => (int)Math.Min(int.MaxValue, Costs(state, playerId, battlefieldId).Sum(c => (long)Math.Max(0, c.PowerPerExtraUnit)));
    public static int ManaPerMove(MatchState state, string playerId, string? battlefieldId)
        => (int)Math.Min(int.MaxValue, Costs(state, playerId, battlefieldId).Sum(c => (long)Math.Max(0, c.ManaPerMove)));
    public static int PowerPerMove(MatchState state, string playerId, string? battlefieldId)
        => (int)Math.Min(int.MaxValue, Costs(state, playerId, battlefieldId).Sum(c => (long)Math.Max(0, c.PowerPerMove)));

    public static bool TryCost(MatchState state, string playerId, string? battlefieldId, int unitCount, out int mana, out int power)
    {
        long totalMana = 0, totalPower = 0;
        if (unitCount > 0)
            foreach (var cost in Costs(state, playerId, battlefieldId))
            {
                totalMana += Math.Max(0, cost.ManaPerMove);
                totalPower += Math.Max(0, cost.PowerPerMove) + (long)Math.Max(0, cost.PowerPerExtraUnit) * (unitCount - 1);
                if (totalMana > int.MaxValue || totalPower > int.MaxValue) { mana = power = 0; return false; }
            }
        mana = (int)totalMana; power = (int)totalPower; return true;
    }
}

using System.Globalization;

namespace Riftbound.Engine;

// Numeric status values live on the same field generation and expire with all
// other until-turn effects. A new grant adds to the unspent prevention budget.
internal static class TemporaryUnitEffectRules
{
    internal sealed record PreventionBudget(string Id, int Remaining);
    internal const string EnemyTargetProtection = "ENEMY_SPELL_SKILL_TARGET_PROTECTION_THIS_TURN";
    internal const string BaseMightPrefix = "BASE_MIGHT_THIS_TURN:";
    internal const string DamagePreventionPrefix = "DAMAGE_PREVENTION_THIS_TURN:";

    internal static PreventionBudget[] PreventionBudgets(CardObjectState card)
        => card.UntilEndOfTurnEffects.Where(e => e.StartsWith(DamagePreventionPrefix, StringComparison.Ordinal))
            .Select(e => (Effect: e, Split: e.LastIndexOf(':')))
            .Where(e => e.Split > DamagePreventionPrefix.Length
                && int.TryParse(e.Effect.AsSpan(e.Split + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0)
            .Select(e => new PreventionBudget(e.Effect[..e.Split], int.Parse(e.Effect.AsSpan(e.Split + 1), CultureInfo.InvariantCulture))).ToArray();

    internal static int RemainingPrevention(CardObjectState card) => PreventionBudgets(card).Sum(b => b.Remaining);

    internal static CardObjectState GrantPrevention(CardObjectState card, long tick, int instruction, int amount)
        => SetNumber(card, $"{DamagePreventionPrefix}{tick}:{instruction}:", amount);

    internal static CardObjectState SpendPrevention(CardObjectState card, PreventionBudget budget, int amount)
        => SetNumber(card, budget.Id + ":", budget.Remaining - amount);

    internal static int? Number(CardObjectState card, string prefix)
        => card.UntilEndOfTurnEffects.Where(e => e.StartsWith(prefix, StringComparison.Ordinal))
            .Select(e => int.TryParse(e.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? (int?)n : null)
            .FirstOrDefault();

    internal static CardObjectState SetNumber(CardObjectState card, string prefix, int value, bool retainZero = false)
        => card with { UntilEndOfTurnEffects = card.UntilEndOfTurnEffects.Where(e => !e.StartsWith(prefix, StringComparison.Ordinal))
            .Concat(value > 0 || retainZero ? [prefix + value.ToString(CultureInfo.InvariantCulture)] : [])
            .Order(StringComparer.Ordinal).ToArray() };

    internal static int BaseMightDelta(CardObjectState card)
    {
        if (Number(card, BaseMightPrefix) is not { } replacement) return 0;
        var printed = PrintedCardFactory.TryCreate(card.ObjectId, card.OwnerId ?? "", card.CardNo, out var face) ? face.Power
            : card.CardNo is { } no && P6TokenFactoryCatalog.TryGetByCardNo(no, out var token) ? token.DefaultPower
            : card.Power - card.UntilEndOfTurnPowerModifier - (card.Tags.Contains("增益") ? 1 : 0);
        return replacement - printed;
    }

    internal static bool ThreatAllowsTarget(MatchState state, string player, string target, CardBehaviorDefinition behavior)
    {
        if (behavior.TargetRequiresEnemyTraitThreat is not { Length: > 0 } trait) return true;
        if (!state.CardObjects.TryGetValue(target, out var unit) || !FieldObjectTypeRules.IsVisibleUnit(unit)
            || unit.ControllerId != player) return false;
        bool EnemyTrait(CardObjectState c) => c.ControllerId != player && c.Tags.Contains("COLOR:" + trait);
        var battle = state.BattleState;
        if (battle.IsActive && state.ObjectLocations.TryGetValue(target, out var location)
            && location.Zone == "BATTLEFIELD" && location.BattlefieldObjectId == battle.BattlefieldObjectId)
        {
            var enemies = battle.AttackerObjectIds.Contains(target) ? battle.DefenderObjectIds
                : battle.DefenderObjectIds.Contains(target) ? battle.AttackerObjectIds : [];
            if (enemies.Any(id => state.CardObjects.TryGetValue(id, out var enemy) && FieldObjectTypeRules.IsVisibleUnit(enemy)
                && EnemyTrait(enemy))) return true;
        }
        return state.StackItems.Any(item => !StackObjectRules.IsAbility(item) && item.ControllerId != player
            && state.CardObjects.TryGetValue(item.SourceObjectId, out var spell) && spell.Tags.Contains("COLOR:" + trait)
            && (item.TargetObjectIds.Contains(target) && item.TargetGenerations?.GetValueOrDefault(target, -1) == unit.ObjectGeneration
                || item.RepeatExecutions?.Any(execution => execution.TargetObjectIds.Contains(target)
                    && execution.TargetGenerations.GetValueOrDefault(target, -1) == unit.ObjectGeneration) == true));
    }
}

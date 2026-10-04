namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal sealed record PlayManaCost(
        int Total, int CardReduction, int OptionalReduction, int EchoReduction, int EquipmentReduction,
        int UnitReduction, int NextSpellReduction, int BattlefieldSpellReduction, int Increase, int Spellshield,
        IReadOnlyList<string> SpellshieldTargets);

    // CN 356: component reductions apply to that component, then total reductions apply
    // to base + additional costs + increases. A floor belongs only to its own reducer.
    // Apply higher floors first for the lowest legal payment, with deterministic ties.
    internal static PlayManaCost CalculatePlayManaCost(
        MatchState state, string playerId, CardBehaviorDefinition behavior,
        int additionalMana = 0, int optionalReduction = 0,
        IReadOnlyList<string>? optionalCosts = null, IReadOnlyList<string>? targets = null)
    {
        var echoReduction = ResolveBattlefieldEchoCostReductionMana(state, playerId, behavior, optionalCosts ?? [], additionalMana);
        var increase = ResolveBattlefieldHeldUnitCostIncreaseMana(state, playerId, behavior);
        var spellshield = ResolveSpellshieldTargetTaxMana(state, playerId, behavior, targets ?? [], out var shieldTargets);
        var total = Math.Max(0, behavior.ManaCost) + Math.Max(0, additionalMana - echoReduction) + increase + spellshield;
        var reductions = new List<(string Kind, int Amount, int Floor)>();
        if (behavior.PlaysSourceToBaseAsUnit)
            reductions.AddRange(StaticUnitCostReductionSourceBehaviors(state, playerId)
                .Where(source => StaticUnitCostReductionAppliesToBehavior(source, behavior))
                .Select(source => ("unit", source.StaticUnitCostReductionMana, source.StaticUnitCostReductionMinimumManaCost)));
        if (IsSpellPlayBehavior(behavior))
        {
            reductions.AddRange(StaticSpellCostReductionSourceBehaviors(state, playerId)
                .Select(source => ("spell", source.StaticSpellCostReductionMana, source.StaticSpellCostReductionMinimumManaCost)));
            reductions.Add(("next", SourceNextSpellCostReductionEffects(state, playerId).Sum(effect => effect.Mana), 0));
        }
        reductions.Add(("card", ResolveCostReductionMana(state, playerId, behavior), 0));
        reductions.Add(("optional", optionalReduction, 0));
        reductions.Add(("equipment", ResolveBattlefieldEquipmentCostReductionMana(state, playerId, behavior), 0));
        var applied = new Dictionary<string, int>();
        foreach (var reduction in reductions.OrderByDescending(item => item.Floor))
        {
            var amount = Math.Min(Math.Max(0, reduction.Amount), Math.Max(0, total - Math.Max(0, reduction.Floor)));
            total -= amount;
            applied[reduction.Kind] = applied.GetValueOrDefault(reduction.Kind) + amount;
        }
        return new(total, applied.GetValueOrDefault("card"), applied.GetValueOrDefault("optional"), echoReduction,
            applied.GetValueOrDefault("equipment"), applied.GetValueOrDefault("unit"), applied.GetValueOrDefault("next"),
            applied.GetValueOrDefault("spell"), increase, spellshield, shieldTargets);
    }

    // A prompt's lower bound may assume affordable optional reductions. The quote uses
    // the actual selections and targets instead, through the same cost calculation.
    internal static PlayManaCost MinimumPlayManaCost(
        MatchState state, string playerId, CardBehaviorDefinition behavior, string? sourceObjectId, int additionalMana = 0)
    {
        var optionalReduction = 0;
        if (behavior.OptionalExperienceCost > 0 && state.PlayerExperience.GetValueOrDefault(playerId) >= behavior.OptionalExperienceCost)
            optionalReduction += behavior.ManaReductionIfExperiencePaid;
        if (behavior.ManaReductionIfDiscardHandCardOptionalCost > 0 && sourceObjectId is not null
            && state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Hand.Any(id => CanDiscardHandCardAsOptionalCost(state, playerId, sourceObjectId, id)))
            optionalReduction += behavior.ManaReductionIfDiscardHandCardOptionalCost;
        return CalculatePlayManaCost(state, playerId, behavior, additionalMana, optionalReduction);
    }
}

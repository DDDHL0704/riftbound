namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal sealed record PlayManaCost(
        int Total, int CardReduction, int OptionalReduction, int EchoReduction, int EquipmentReduction,
        int UnitReduction, int NextSpellReduction, int BattlefieldSpellReduction, int Increase,
        int EmpoweredSpellReduction = 0, int OpponentSpellIncrease = 0);

    // CN 356: component reductions apply to that component, then total reductions apply
    // to base + additional costs + increases. A floor belongs only to its own reducer.
    // Apply higher floors first for the lowest legal payment, with deterministic ties.
    internal static PlayManaCost CalculatePlayManaCost(
        MatchState state, string playerId, CardBehaviorDefinition behavior,
        int additionalMana = 0, int optionalReduction = 0,
        IReadOnlyList<string>? optionalCosts = null, IReadOnlyList<string>? targets = null,
        string? sourceObjectId = null, int? componentEchoReduction = null)
    {
        if (EffectPlayIgnoresAllCosts(state, playerId)) return new(0, 0, 0, 0, 0, 0, 0, 0, 0);
        var echoReduction = componentEchoReduction
            ?? ResolveBattlefieldEchoCostReductionMana(state, playerId, behavior, optionalCosts ?? []);
        var increase = ResolveBattlefieldHeldUnitCostIncreaseMana(state, playerId, behavior);
        var spellIncrease = OpponentSpellCostIncreases(state, playerId, behavior).Mana;
        var effect = state.PendingEffectPlay is { } pending && pending.PlayerId == playerId ? pending : null;
        var total = (effect?.IgnoreBaseMana == true || behavior.AppliedEquipmentPlayGrant is not null || behavior.StandbyPlayFrom is not null ? 0 : Math.Max(0, behavior.AppliedFlowCost?.Mana ?? behavior.ManaCost))
            + Math.Max(0, additionalMana - (componentEchoReduction.HasValue ? 0 : echoReduction)) + increase + spellIncrease;
        var reductions = new List<(string Kind, int Amount, int Floor)> { ("card", effect?.ManaReduction ?? 0, 0) };
        if (behavior.PlaysSourceToBaseAsUnit)
            reductions.AddRange(StaticUnitCostReductionSourceBehaviors(state, playerId)
                .Where(source => StaticUnitCostReductionAppliesToBehavior(source, behavior))
                .Select(source => ("unit", source.StaticUnitCostReductionMana, source.StaticUnitCostReductionMinimumManaCost)));
        if (IsSpellPlayBehavior(behavior))
        {
            reductions.AddRange(StaticSpellCostReductionSourceBehaviors(state, playerId)
                .Select(source => ("spell", source.StaticSpellCostReductionMana, source.StaticSpellCostReductionMinimumManaCost)));
            reductions.AddRange(EmpoweredSpellCostReducers(state, playerId, behavior)
                .Select(source => ("empoweredSpell", source.Mana, source.MinimumMana)));
            reductions.AddRange(FlowSpellCostReducers(state, playerId, behavior)
                .Select(source => ("spell", source.StaticFlowSpellCostReductionMana, source.StaticFlowSpellCostReductionMinimumManaCost)));
        }
        reductions.Add(("next", SourceNextSpellCostReductionEffects(state, playerId)
            .Where(effect => effect.AppliesToAllCards || IsSpellPlayBehavior(behavior)).Sum(effect => effect.Mana), 0));
        reductions.Add(("card", ResolveCostReductionMana(state, playerId, behavior, sourceObjectId), 0));
        if (!string.IsNullOrEmpty(behavior.TargetTraitsManaReductionTags)
            && (targets ?? []).Any(id => state.CardObjects.TryGetValue(id, out var card)
                && behavior.TargetTraitsManaReductionTags.Split('|').Any(tag => card.Tags.Contains(tag))))
            reductions.Add(("card", behavior.TargetTraitsManaReduction, 0));
        reductions.Add(("optional", optionalReduction, 0));
        if (behavior.MayReturnFriendlyEquipmentAdditionalCost && (optionalCosts ?? []).Any(c =>
                TryParseReturnFriendlyEquipmentAdditionalCost(c, out var id) && CanReturnFriendlyEquipmentAsAdditionalCost(state, playerId, id)))
            reductions.Add(("optional", behavior.ManaReductionIfEquipmentReturned, 0));
        reductions.Add(("optional", SelectedSacrificeCostReduction(state, playerId, behavior, optionalCosts ?? []).Mana, 0));
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
            applied.GetValueOrDefault("spell"), increase, applied.GetValueOrDefault("empoweredSpell"), spellIncrease);
    }

    // A prompt's lower bound may assume affordable optional reductions. The quote uses
    // the actual selections and targets instead, through the same cost calculation.
    internal static PlayManaCost MinimumPlayManaCost(
        MatchState state, string playerId, CardBehaviorDefinition behavior, string? sourceObjectId, int additionalMana = 0)
    {
        if (behavior.AppliedEquipmentPlayGrant is null && TurnEquipmentPlayRules.Available(state, playerId, behavior, sourceObjectId).FirstOrDefault() is { } grant)
            return MinimumPlayManaCost(state, playerId, behavior with { AppliedEquipmentPlayGrant = grant.Id }, sourceObjectId, additionalMana);
        if (behavior.MaySacrificeUnitForPrintedCostReduction)
            return OptionalSacrificePlayCosts(state, playerId, behavior, sourceObjectId, additionalMana)
                .MinBy(cost => cost.Mana.Total)!.Mana;
        var optionalReduction = behavior.MayReturnFriendlyEquipmentAdditionalCost
            && state.CardObjects.Keys.Any(id => CanReturnFriendlyEquipmentAsAdditionalCost(state, playerId, id))
                ? behavior.ManaReductionIfEquipmentReturned : 0;
        if (behavior.OptionalExperienceCost > 0 && state.PlayerExperience.GetValueOrDefault(playerId) >= behavior.OptionalExperienceCost)
            optionalReduction += behavior.ManaReductionIfExperiencePaid;
        if (behavior.ManaReductionIfDiscardHandCardOptionalCost > 0 && sourceObjectId is not null
            && state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Hand.Any(id => CanDiscardHandCardAsOptionalCost(state, playerId, sourceObjectId, id)))
            optionalReduction += behavior.ManaReductionIfDiscardHandCardOptionalCost;
        if (behavior.TargetTraitsManaReduction > 0 && state.PlayerZones.TryGetValue(playerId, out var own)
            && own.Graveyard.Any(id => state.CardObjects.TryGetValue(id, out var card) && card.Tags.Contains(CardObjectTags.UnitCard)
                && IsTargetManaCostAllowed(state, playerId, id, behavior)
                && behavior.TargetTraitsManaReductionTags.Split('|').Any(tag => card.Tags.Contains(tag))))
            optionalReduction += behavior.TargetTraitsManaReduction;
        var cost = CalculatePlayManaCost(state, playerId, behavior, additionalMana, optionalReduction,
            sourceObjectId: sourceObjectId);
        var reduction = Math.Min(cost.Total, AvailableOptionalPlayManaReduction(state, playerId, behavior));
        return cost with { Total = cost.Total - reduction, OptionalReduction = cost.OptionalReduction + reduction };
    }
}

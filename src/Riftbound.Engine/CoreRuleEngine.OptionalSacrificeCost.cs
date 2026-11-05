namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal sealed record OptionalSacrificePlayCost(IReadOnlyList<string> Choices, PlayManaCost Mana,
        int GenericPower, IReadOnlyDictionary<string, int> TypedPower, ManaPaymentPurpose ManaPurpose)
    {
        internal bool CanPay(RunePool pool, MatchState? state = null) => PaymentCostRules.AvailableMana(pool,
            ManaPurpose, state: state) >= Mana.Total
            && PaymentCostRules.CanPayPowerCost(pool, GenericPower, TypedPower);
    }

    // CN 206 and its Atakhan example: read the current printed/copied face,
    // not combat might, historical payment or a mutable materialized mana field.
    private static bool TrySacrificeUnitCost(MatchState state, string player, string id, out int mana, out int power)
    {
        mana = power = 0;
        if (!IsPlayerControlledFieldUnitObject(state, player, id)
            || !state.ObjectLocations.TryGetValue(id, out var at) || at.PlayerId != player
            || at.Zone is not ("BASE" or "BATTLEFIELD")
            || !state.CardObjects.TryGetValue(id, out var card) || card.ControllerId != player) return false;
        if (PrintedCardFactory.TryCreate(id, player, card.CardNo, out var printed)) mana = printed.ManaCost;
        else if (!P6TokenFactoryCatalog.IsTokenFactory(card.CardNo)) return false;
        power = PrintedPowerCostRules.ForCard(card.CardNo ?? "").Amount;
        return true;
    }

    private static IReadOnlyList<string> OptionalSacrificeCandidates(MatchState state, string player)
        => state.PlayerZones.TryGetValue(player, out var zones)
            ? zones.Base.Concat(zones.Battlefields).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
                .Where(id => TrySacrificeUnitCost(state, player, id, out _, out _))
                .Select(id => DestroyFriendlyUnitAdditionalCostPrefix + id).ToArray() : [];

    private static (int Mana, int Power) SelectedSacrificeCostReduction(MatchState state, string player,
        CardBehaviorDefinition behavior, IReadOnlyList<string> costs)
    {
        if (!behavior.MaySacrificeUnitForPrintedCostReduction) return (0, 0);
        var selected = costs.Where(cost => cost.StartsWith(DestroyFriendlyUnitAdditionalCostPrefix, StringComparison.Ordinal)).ToArray();
        if (behavior.FixedYellowPowerReductionPerSacrificedUnit > 0)
            return selected.Length > 0 && selected.Distinct(StringComparer.Ordinal).Count() == selected.Length
                && selected.All(cost => TryParseDestroyFriendlyUnitAdditionalCost(cost, out var unit)
                    && TrySacrificeUnitCost(state, player, unit, out _, out _))
                ? (0, checked(selected.Length * behavior.FixedYellowPowerReductionPerSacrificedUnit)) : (0, 0);
        if (selected.Length != 1 || !TryParseDestroyFriendlyUnitAdditionalCost(selected[0], out var id)
            || !TrySacrificeUnitCost(state, player, id, out var mana, out var power)) return (0, 0);
        // A fixed reduction changes only mana; it does not inherit either
        // printed cost of the sacrificed unit (CN 356.4).
        return behavior.FixedUnitSacrificeManaReduction is { } fixedMana
            ? (Math.Max(0, fixedMana), 0) : (mana, power);
    }

    // Atakhan reduces yellow power in the total cost, including yellow Haste.
    // CN 356.4.f/f.1: a reduction to zero still pays the selected extra cost.
    private static IReadOnlyDictionary<string, int> ApplySacrificePowerReduction(
        IReadOnlyDictionary<string, int> typed, int reduction)
        => reduction <= 0 ? typed : typed.ToDictionary(entry => entry.Key,
            entry => entry.Key == RuneTrait.Yellow ? Math.Max(0, entry.Value - reduction) : entry.Value,
            StringComparer.Ordinal).Where(entry => entry.Value > 0)
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

    private static IReadOnlyList<IReadOnlyList<string>> OptionalSacrificeSelections(
        MatchState state, string player, CardBehaviorDefinition behavior)
    {
        var selections = new List<IReadOnlyList<string>> { Array.Empty<string>() };
        if (!behavior.MaySacrificeUnitForPrintedCostReduction) return selections;
        var candidates = OptionalSacrificeCandidates(state, player);
        // A fixed per-unit reduction is monotone. The full set gives the price
        // lower bound without enumerating subsets; real commands quote and pay
        // their actual selection, including selections beyond the zero floor.
        if (behavior.FixedYellowPowerReductionPerSacrificedUnit > 0)
        {
            if (candidates.Count > 0) selections.Add(candidates);
        }
        else foreach (var choice in candidates) selections.Add(new[] { choice });
        return selections;
    }

    internal static IReadOnlyList<OptionalSacrificePlayCost> OptionalSacrificePlayCosts(
        MatchState state, string player, CardBehaviorDefinition behavior, string? source,
        int additionalMana = 0, int additionalPower = 0, string additionalTrait = "")
    {
        var pool = state.RunePools.GetValueOrDefault(player) ?? RunePool.Empty;
        var choices = OptionalSacrificeSelections(state, player, behavior);
        var extraTyped = string.IsNullOrWhiteSpace(additionalTrait) ? new Dictionary<string, int>()
            : new Dictionary<string, int> { [additionalTrait] = additionalPower };
        return choices.Select(costs => {
            var reduction = SelectedSacrificeCostReduction(state, player, behavior, costs);
            var mana = CalculatePlayManaCost(state, player, behavior, additionalMana,
                optionalCosts: costs, sourceObjectId: source);
            TrySelectPlayPowerCost(state, player, behavior, null, pool,
                SourceReplayPowerCost(state, player) + (additionalTrait.Length == 0 ? additionalPower : 0), extraTyped,
                out var generic, out var typed);
            return new OptionalSacrificePlayCost(costs, mana, generic, ApplySacrificePowerReduction(typed, reduction.Power), ManaPurposeForPlay(behavior));
        }).ToArray();
    }
}

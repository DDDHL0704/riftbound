namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal sealed record OptionalSacrificePlayCost(string? Choice, PlayManaCost Mana,
        int GenericPower, IReadOnlyDictionary<string, int> TypedPower, bool SpellPlay)
    {
        internal bool CanPay(RunePool pool) => PaymentCostRules.AvailableMana(pool,
            SpellPlay ? ManaPaymentPurpose.PlaySpell : ManaPaymentPurpose.Other) >= Mana.Total
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
        return selected.Length == 1 && TryParseDestroyFriendlyUnitAdditionalCost(selected[0], out var id)
            && TrySacrificeUnitCost(state, player, id, out var mana, out var power) ? (mana, power) : (0, 0);
    }

    // Atakhan reduces yellow power in the total cost, including yellow Haste.
    // CN 356.4.f/f.1: a reduction to zero still pays the selected extra cost.
    private static IReadOnlyDictionary<string, int> ApplySacrificePowerReduction(
        IReadOnlyDictionary<string, int> typed, int reduction)
        => reduction <= 0 ? typed : typed.ToDictionary(entry => entry.Key,
            entry => entry.Key == RuneTrait.Yellow ? Math.Max(0, entry.Value - reduction) : entry.Value,
            StringComparer.Ordinal).Where(entry => entry.Value > 0)
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

    internal static IReadOnlyList<OptionalSacrificePlayCost> OptionalSacrificePlayCosts(
        MatchState state, string player, CardBehaviorDefinition behavior, string? source,
        int additionalMana = 0, int additionalPower = 0, string additionalTrait = "")
    {
        var pool = state.RunePools.GetValueOrDefault(player) ?? RunePool.Empty;
        var choices = new string?[] { null }.Concat(behavior.MaySacrificeUnitForPrintedCostReduction
            ? OptionalSacrificeCandidates(state, player) : []);
        var extraTyped = string.IsNullOrWhiteSpace(additionalTrait) ? new Dictionary<string, int>()
            : new Dictionary<string, int> { [additionalTrait] = additionalPower };
        return choices.Select(choice => {
            IReadOnlyList<string> costs = choice is null ? [] : [choice];
            var reduction = SelectedSacrificeCostReduction(state, player, behavior, costs);
            var mana = CalculatePlayManaCost(state, player, behavior, additionalMana,
                optionalCosts: costs, sourceObjectId: source);
            TrySelectPlayPowerCost(state, player, behavior, null, pool,
                SourceReplayPowerCost(state, player) + (additionalTrait.Length == 0 ? additionalPower : 0), extraTyped,
                out var generic, out var typed);
            return new OptionalSacrificePlayCost(choice, mana, generic, ApplySacrificePowerReduction(typed, reduction.Power), IsSpellPlayBehavior(behavior));
        }).ToArray();
    }
}

using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private const string RenektonPaymentPrefix = "LEGEND_RESOURCE:";
    internal sealed record RenektonPaymentChoice(
        string Id, string Label, string SourceObjectId, string PowerSpend, RunePool RemainingPool,
        IReadOnlyDictionary<string, int> RequiredRunePower, IReadOnlyDictionary<string, int> RemainingRunePower,
        int RemainingRuneCapacity);

    internal static IReadOnlyDictionary<string, int> RenektonRecyclablePower(MatchState state, string player)
        => RecycleRunePaymentResourceActionIds(state, player)
            .Select(action => TryParseRecycleRunePaymentOptionalCost(action, out var id)
                && state.CardObjects.TryGetValue(id, out var rune) && TryGetRuneTrait(rune, out var trait)
                    ? trait : string.Empty)
            .Where(trait => !string.IsNullOrWhiteSpace(trait))
            .GroupBy(trait => trait, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count() * BasicRuneRecyclePowerGain, StringComparer.Ordinal);

    // Enumerate actual wallet allocations, so the same power cannot pay both
    // the legend and the card. Other restricted power is not a legend cost.
    internal static IReadOnlyList<RenektonPaymentChoice> RenektonPlayPaymentChoices(
        MatchState state, string player, RunePool pool, IReadOnlyDictionary<string, int>? possibleRunes = null)
    {
        if (!state.PlayerZones.TryGetValue(player, out var zones)
            || !pool.HasSupportedAmounts || !CanGainSpellOnlyMana(pool, 2)
            || !TryGetLegendAbility(RenektonLegendAbilityId, out var ability)
            || !IsLegendAbilityTimingAllowed(state, player, ability)) return [];

        var candidates = possibleRunes ?? new Dictionary<string, int>(StringComparer.Ordinal);
        if (candidates.Any(pair => pair.Value < 0)) return [];
        var capacity = int.MaxValue - pool.TotalStoredPower;
        var keys = new[] { "A", "blue", "green", "orange", "purple", "red", "yellow" };
        var result = new List<RenektonPaymentChoice>();
        foreach (var sourceId in zones.LegendZone.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(sourceId, out var source) || source.ObjectId != sourceId
                || source.IsFaceDown || source.IsExhausted || !SourceObjectControlledByPlayerOrLegacyOwned(source, player)
                || !LegendAbilitySourceHasAbility(state, player, source.CardNo, ability)) continue;
            for (var first = 0; first < keys.Length; first++)
            for (var second = first; second < keys.Length; second++)
            {
                var amounts = new Dictionary<string, int>(StringComparer.Ordinal) { [keys[first]] = 1 };
                amounts[keys[second]] = amounts.GetValueOrDefault(keys[second]) + 1;
                var required = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var (key, amount) in amounts)
                {
                    var present = key == "A" ? pool.Power : pool.PowerByTrait.GetValueOrDefault(key);
                    var missing = Math.Max(0, amount - present);
                    if (missing > 0) required[key] = missing;
                }
                if (required.GetValueOrDefault("A") > 0
                    || required.Any(pair => pair.Value > candidates.GetValueOrDefault(pair.Key))
                    || required.Values.Sum() > capacity) continue;
                var augmentedPower = pool.PowerByTrait.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                foreach (var (trait, amount) in required)
                    augmentedPower[trait] = augmentedPower.GetValueOrDefault(trait) + amount;
                var augmented = pool with { PowerByTrait = augmentedPower };
                var choice = PlayPowerSpendSelectionRules.ChoicePrefix
                    + string.Join(",", amounts.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + ":" + pair.Value));
                if (!PlayPowerSpendSelectionRules.TryExtract([choice], out _, out var spend) || spend is null) continue;
                var remaining = PaymentCostRules.PayPowerCost(augmented, spend.Wildcard, spend.ByTrait, PowerPaymentPurpose.Other);
                var remainingRunes = candidates.ToDictionary(pair => pair.Key,
                    pair => pair.Value - required.GetValueOrDefault(pair.Key), StringComparer.Ordinal);
                var description = string.Join("、", choice[PlayPowerSpendSelectionRules.ChoicePrefix.Length..].Split(',')
                    .Select(part => part.Split(':')).Select(pair => pair[1] + " 点" + (pair[0] == "A" ? "任意" : RuneTraitLabel(pair[0])) + "符能"));
                result.Add(new($"{RenektonPaymentPrefix}{sourceId}:{RenektonLegendAbilityId}:{choice}",
                    $"支付{description}并横置传奇：获得 2 点单位专用法力", sourceId, choice, remaining,
                    required, remainingRunes, capacity - required.Values.Sum()));
            }
        }
        return result;
    }

    private static bool TryExtractRenektonPlayPayment(
        IReadOnlyList<string> costs, out IReadOnlyList<string> remaining, out string? choice)
    {
        var actions = costs.Where(cost => cost.StartsWith(RenektonPaymentPrefix, StringComparison.Ordinal)).ToArray();
        remaining = costs.Where(cost => !cost.StartsWith(RenektonPaymentPrefix, StringComparison.Ordinal)).ToArray();
        choice = actions.Length == 1 ? actions[0] : null;
        return actions.Length <= 1;
    }

    private static bool TryResolveRenektonPlayPayment(MatchState state, string player, string choice,
        out ResolutionResult result)
    {
        result = default!;
        var pool = state.RunePools.GetValueOrDefault(player, RunePool.Empty);
        var selected = RenektonPlayPaymentChoices(state, player, pool)
            .SingleOrDefault(candidate => candidate.Id == choice);
        if (selected is null) return false;
        // The resource ability is resolved in the card's payment window, not
        // as a second player turn or a separately committed command.
        var context = state with { PendingPayment = new("play-card-resource", "PLAY_CARD", player) };
        result = ResolveLegendAct(context, new("play-card-resource", player, CommandTypes.LegendAct),
            new LegendActCommand(selected.SourceObjectId, RenektonLegendAbilityId, [], [selected.PowerSpend]));
        return result.Accepted;
    }
}

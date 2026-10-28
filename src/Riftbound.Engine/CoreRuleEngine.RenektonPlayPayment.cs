using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private const string RenektonPaymentPrefix = "LEGEND_RESOURCE:";
    internal sealed record RenektonPaymentChoice(
        string Id, string Label, string SourceObjectId, string PowerSpend, RunePool RemainingPool);

    // Enumerate actual wallet allocations, so the same power cannot pay both
    // the legend and the card. Other restricted power is not a legend cost.
    internal static IReadOnlyList<RenektonPaymentChoice> RenektonPlayPaymentChoices(
        MatchState state, string player, RunePool pool)
    {
        if (!state.PlayerZones.TryGetValue(player, out var zones)
            || !pool.HasSupportedAmounts || !CanGainSpellOnlyMana(pool, 2)
            || !TryGetLegendAbility(RenektonLegendAbilityId, out var ability)
            || !IsLegendAbilityTimingAllowed(state, player, ability)) return [];

        var result = new List<RenektonPaymentChoice>();
        foreach (var sourceId in zones.LegendZone.Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(sourceId, out var source) || source.ObjectId != sourceId
                || source.IsFaceDown || source.IsExhausted || !SourceObjectControlledByPlayerOrLegacyOwned(source, player)
                || !LegendAbilitySourceHasAbility(state, player, source.CardNo, ability)) continue;
            foreach (var choice in RenektonPowerSpendChoices(pool))
            {
                if (!PlayPowerSpendSelectionRules.TryExtract([choice], out _, out var spend) || spend is null) continue;
                var remaining = PaymentCostRules.PayPowerCost(pool, spend.Wildcard, spend.ByTrait, PowerPaymentPurpose.Other);
                var description = string.Join("、", choice[PlayPowerSpendSelectionRules.ChoicePrefix.Length..].Split(',')
                    .Select(part => part.Split(':')).Select(pair => pair[1] + " 点" + (pair[0] == "A" ? "任意" : RuneTraitLabel(pair[0])) + "符能"));
                result.Add(new($"{RenektonPaymentPrefix}{sourceId}:{RenektonLegendAbilityId}:{choice}",
                    $"支付{description}并横置传奇：获得 2 点单位专用法力", sourceId, choice, remaining));
            }
        }
        return result;
    }

    private static bool TryExtractRenektonPlayPayment(
        IReadOnlyList<string> costs, out IReadOnlyList<string> remaining, out string? choice)
    {
        var actions = costs.Where(cost => cost.StartsWith(RenektonPaymentPrefix, StringComparison.Ordinal)).ToArray();
        remaining = costs.Where(cost => !cost.StartsWith(RenektonPaymentPrefix, StringComparison.Ordinal)).ToArray();
        choice = actions.SingleOrDefault();
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

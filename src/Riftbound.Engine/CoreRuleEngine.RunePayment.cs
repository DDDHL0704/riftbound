using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool TryExtractTapRunePaymentResourceActions(
        MatchState state,
        string playerId,
        IReadOnlyList<string> normalizedOptionalCosts,
        out IReadOnlyList<string> behaviorOptionalCosts,
        out IReadOnlyList<string> paymentResourceActions,
        out IReadOnlyList<string> tappedRuneObjectIds)
    {
        var behaviorCosts = new List<string>();
        var paymentActions = new List<string>();
        var runeObjectIds = new List<string>();
        var seenRuneObjectIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var optionalCost in normalizedOptionalCosts)
        {
            if (TryParseTapRunePaymentOptionalCost(optionalCost, out var runeObjectId))
            {
                if (!seenRuneObjectIds.Add(runeObjectId)
                    || !CanTapRuneForPayment(state, playerId, runeObjectId))
                {
                    behaviorOptionalCosts = [];
                    paymentResourceActions = [];
                    tappedRuneObjectIds = [];
                    return false;
                }

                paymentActions.Add(optionalCost);
                runeObjectIds.Add(runeObjectId);
                continue;
            }

            if (optionalCost.StartsWith(TapRunePaymentOptionalCostPrefix, StringComparison.Ordinal))
            {
                behaviorOptionalCosts = [];
                paymentResourceActions = [];
                tappedRuneObjectIds = [];
                return false;
            }

            behaviorCosts.Add(optionalCost);
        }

        var currentPool = state.RunePools.GetValueOrDefault(playerId, RunePool.Empty);
        if (!currentPool.HasSupportedAmounts
            || runeObjectIds.Count > int.MaxValue - currentPool.TotalStoredMana)
        {
            behaviorOptionalCosts = [];
            paymentResourceActions = [];
            tappedRuneObjectIds = [];
            return false;
        }

        behaviorOptionalCosts = behaviorCosts;
        paymentResourceActions = paymentActions;
        tappedRuneObjectIds = runeObjectIds;
        return true;
    }

    private static string ParseTapRunePaymentObjectId(string actionId)
    {
        return TryParseTapRunePaymentOptionalCost(actionId, out var objectId) ? objectId : string.Empty;
    }

    private static bool TryParseTapRunePaymentOptionalCost(
        string optionalCost,
        out string runeObjectId)
    {
        runeObjectId = string.Empty;
        if (string.IsNullOrWhiteSpace(optionalCost)
            || !optionalCost.StartsWith(TapRunePaymentOptionalCostPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        runeObjectId = optionalCost[TapRunePaymentOptionalCostPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(runeObjectId);
    }

    private static bool IsTapRunePaymentResourceActionId(string choiceId)
    {
        return choiceId.StartsWith(TapRunePaymentOptionalCostPrefix, StringComparison.Ordinal);
    }

    private static bool CanTapRuneForPayment(
        MatchState state,
        string playerId,
        string runeObjectId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Base.Contains(runeObjectId, StringComparer.Ordinal)
            && state.CardObjects.TryGetValue(runeObjectId, out var runeState)
            && runeState.Tags.Contains(CardObjectTags.RuneCard, StringComparer.Ordinal)
            && SourceObjectControlledByPlayerOrLegacyOwned(runeState, playerId)
            && !runeState.IsFaceDown
            && !runeState.IsExhausted
            && !string.IsNullOrWhiteSpace(runeState.CardNo);
    }

    internal static IReadOnlyList<string> TapRunePaymentResourceActionIds(MatchState state, string playerId)
    {
        var pool = state.RunePools.GetValueOrDefault(playerId, RunePool.Empty);
        if (!pool.HasSupportedAmounts || pool.TotalStoredMana >= int.MaxValue
            || !state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        // Every individually legal rune remains a choice; selected combinations are capacity-checked.
        return zones.Base
            .Where(id => CanTapRuneForPayment(state, playerId, id))
            .Select(id => TapRunePaymentOptionalCostPrefix + id)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static RunePool ApplyTapRunePaymentToPool(
        RunePool currentPool,
        IReadOnlyList<string> tappedRuneObjectIds)
    {
        var mana = checked(currentPool.Mana + tappedRuneObjectIds.Count);
        return currentPool with { Mana = mana };
    }

    private static Dictionary<string, RunePool> ApplyTapRunePaymentResourceActions(
        IReadOnlyDictionary<string, RunePool> currentRunePools,
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        IReadOnlyList<string> tappedRuneObjectIds,
        List<GameEvent> events,
        string paymentWindow,
        string? paymentId)
    {
        var runePools = currentRunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        if (tappedRuneObjectIds.Count == 0)
        {
            return runePools;
        }

        var currentPool = runePools.GetValueOrDefault(playerId, RunePool.Empty);
        foreach (var runeObjectId in tappedRuneObjectIds)
        {
            if (!playerZones.TryGetValue(playerId, out var zones)
                || !zones.Base.Contains(runeObjectId, StringComparer.Ordinal)
                || !cardObjects.TryGetValue(runeObjectId, out var runeState)
                || !runeState.Tags.Contains(CardObjectTags.RuneCard, StringComparer.Ordinal)
                || !SourceObjectControlledByPlayerOrLegacyOwned(runeState, playerId)
                || runeState.IsFaceDown
                || runeState.IsExhausted
                || string.IsNullOrWhiteSpace(runeState.CardNo))
            {
                throw new InvalidOperationException("Validated rune payment source is unavailable.");
            }

            cardObjects[runeObjectId] = runeState with { IsExhausted = true };
            currentPool = currentPool with { Mana = checked(currentPool.Mana + BasicRuneTapManaGain) };
            runePools[playerId] = currentPool;
            events.Add(new GameEvent(
                "RUNE_TAPPED",
                $"{playerId} 横置符文获得 1 点法力",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = runeObjectId,
                    ["cardNo"] = runeState.CardNo,
                    ["abilityId"] = BasicRuneTapAbilityId,
                    ["mana"] = BasicRuneTapManaGain,
                    ["manaAfter"] = currentPool.Mana,
                    ["paymentWindow"] = paymentWindow,
                    ["paymentId"] = paymentId
                }));
            events.Add(new GameEvent(
                "MANA_GAINED",
                $"{playerId} 通过支付资源动作获得 1 点法力",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = runeObjectId,
                    ["abilityId"] = BasicRuneTapAbilityId,
                    ["mana"] = BasicRuneTapManaGain,
                    ["manaAfter"] = currentPool.Mana,
                    ["paymentWindow"] = paymentWindow,
                    ["paymentId"] = paymentId
                }));
        }

        return runePools;
    }

    private static bool TryPreviewOrderedPaymentResources(MatchState state, string player,
        RunePool current, IReadOnlyList<string> actions, out RunePool remaining)
    {
        remaining = current;
        var tapped = new HashSet<string>(StringComparer.Ordinal);
        var recycled = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in actions)
        {
            if (TryParseTapRunePaymentOptionalCost(action, out var tapRune))
            {
                if (!remaining.HasSupportedAmounts || remaining.TotalStoredMana == int.MaxValue
                    || recycled.Contains(tapRune) || !tapped.Add(tapRune)
                    || !CanTapRuneForPayment(state, player, tapRune)) return false;
                remaining = ApplyTapRunePaymentToPool(remaining, [tapRune]);
                if (!remaining.HasSupportedAmounts) return false;
            }
            else if (TryParseRecycleRunePaymentOptionalCost(action, out var rune))
            {
                if (!remaining.HasSupportedAmounts || remaining.TotalStoredPower == int.MaxValue
                    || !recycled.Add(rune) || !CanRecycleRuneForPayment(state, player, rune)) return false;
                remaining = ApplyRecycleRunePaymentToPool(remaining, state.CardObjects, [rune]);
                if (!remaining.HasSupportedAmounts) return false;
            }
            else if (action.StartsWith(RenektonPaymentPrefix, StringComparison.Ordinal))
            {
                var pools = state.RunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
                pools[player] = remaining;
                if (!TryResolveRenektonPlayPayment(state with { RunePools = pools }, player, action, out var legend))
                    return false;
                remaining = legend.State.RunePools[player];
            }
        }
        return true;
    }

    private static bool TryApplyOrderedPaymentResources(MatchState state, string player,
        IReadOnlyList<string> actions, Dictionary<string, PlayerZones> zones,
        ref Dictionary<string, CardObjectState> cards, Dictionary<string, ObjectLocationState> locations,
        ref Dictionary<string, RunePool> pools, List<GameEvent> events, string window, string paymentId)
    {
        foreach (var action in actions)
        {
            if (TryParseTapRunePaymentOptionalCost(action, out var tapRune))
            {
                var pool = pools.GetValueOrDefault(player, RunePool.Empty);
                if (!pool.HasSupportedAmounts || pool.TotalStoredMana == int.MaxValue
                    || !CanTapRuneForPayment(state with { PlayerZones = zones, CardObjects = cards }, player, tapRune)) return false;
                pools = ApplyTapRunePaymentResourceActions(pools, zones, cards, player,
                    [tapRune], events, window, paymentId);
            }
            else if (TryParseRecycleRunePaymentOptionalCost(action, out var rune))
            {
                var pool = pools.GetValueOrDefault(player, RunePool.Empty);
                if (!pool.HasSupportedAmounts || pool.TotalStoredPower == int.MaxValue
                    || !CanRecycleRuneForPayment(state with { PlayerZones = zones, CardObjects = cards }, player, rune)) return false;
                pools = ApplyRecycleRunePaymentResourceActions(pools, zones, cards, locations, player,
                    [rune], events, window, paymentId);
            }
            else if (action.StartsWith(RenektonPaymentPrefix, StringComparison.Ordinal))
            {
                var context = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, RunePools = pools };
                if (!TryResolveRenektonPlayPayment(context, player, action, out var legend)) return false;
                pools = legend.State.RunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
                cards = legend.State.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
                events.AddRange(legend.Events);
            }
            else if (TryParseLuxSpellOnlyResourceActionId(action, out var luxSource))
            {
                var pool = pools.GetValueOrDefault(player, RunePool.Empty);
                if (!CanGainSpellOnlyMana(pool, P4ActivatedAbilityCatalog.LuxGeneratedMana)) return false;
                pools = ApplyLuxSpellOnlyResourceActions(pools, cards, player, [luxSource], events, paymentId);
            }
        }
        return true;
    }

}

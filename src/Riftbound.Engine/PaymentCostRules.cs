namespace Riftbound.Engine;

public static class PaymentCostRules
{
    public static string BuildPaymentId(
        long tick,
        string paymentWindow,
        string playerId,
        string? sourceObjectId = null,
        string? abilityId = null,
        string? reason = null)
    {
        var discriminator = FirstNonEmpty(sourceObjectId, abilityId, reason, "COST");
        return $"{NormalizePaymentToken(paymentWindow)}:{tick}:{NormalizePaymentToken(playerId)}:{NormalizePaymentToken(discriminator)}";
    }

    public static Dictionary<string, object?> BuildCostPaidPayload(
        string paymentId,
        string paymentWindow,
        string playerId,
        IReadOnlyDictionary<string, RunePool> runePoolsAfterPayment,
        IReadOnlyDictionary<string, int>? playerExperienceAfterPayment,
        IReadOnlyDictionary<string, object?> payload)
    {
        var result = new Dictionary<string, object?>(payload, StringComparer.Ordinal);
        result.TryAdd("paymentId", paymentId);
        result.TryAdd("paymentWindow", paymentWindow);
        result.TryAdd("playerId", playerId);

        var remainingPool = runePoolsAfterPayment.TryGetValue(playerId, out var runePool)
            ? runePool
            : RunePool.Empty;
        result.TryAdd("remainingMana", remainingPool.Mana);
        result.TryAdd("remainingPower", remainingPool.Power);
        result.TryAdd("remainingPowerByTrait", remainingPool.PowerByTrait);

        if (playerExperienceAfterPayment is not null)
        {
            result.TryAdd(
                "remainingExperience",
                playerExperienceAfterPayment.TryGetValue(playerId, out var experience)
                    ? experience
                    : 0);
        }

        return result;
    }

    public static Dictionary<string, RunePool> PayRuneCosts(
        MatchState state,
        string playerId,
        int manaCost,
        int powerCost,
        IReadOnlyDictionary<string, int>? powerCostByTrait = null)
    {
        return PayRuneCosts(
            state.RunePools,
            playerId,
            manaCost,
            powerCost,
            powerCostByTrait);
    }

    public static Dictionary<string, RunePool> PayRuneCosts(
        IReadOnlyDictionary<string, RunePool> currentRunePools,
        string playerId,
        int manaCost,
        int powerCost,
        IReadOnlyDictionary<string, int>? powerCostByTrait = null)
    {
        var runePools = currentRunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var currentPool = runePools.TryGetValue(playerId, out var runePool) ? runePool : RunePool.Empty;
        var (remainingAnyPower, remainingPowerByTrait) = PayPowerCost(
            currentPool,
            powerCost,
            powerCostByTrait ?? new Dictionary<string, int>(StringComparer.Ordinal));
        runePools[playerId] = new RunePool(
            currentPool.Mana - manaCost,
            remainingAnyPower,
            remainingPowerByTrait);

        return runePools;
    }

    public static bool CanPayRuneCosts(
        RunePool pool,
        int manaCost,
        int anyPowerCost,
        IReadOnlyDictionary<string, int>? powerCostByTrait = null)
    {
        return manaCost >= 0
            && anyPowerCost >= 0
            && pool.Mana >= manaCost
            && CanPayPowerCost(
                pool,
                anyPowerCost,
                powerCostByTrait ?? new Dictionary<string, int>(StringComparer.Ordinal));
    }

    public static bool CanPayPowerCost(
        RunePool pool,
        int anyPowerCost,
        IReadOnlyDictionary<string, int> powerCostByTrait)
    {
        if (anyPowerCost < 0)
        {
            return false;
        }

        var remainingPowerByTrait = pool.PowerByTrait.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        foreach (var cost in NormalizePowerCostByTrait(powerCostByTrait))
        {
            if (!remainingPowerByTrait.TryGetValue(cost.Key, out var available)
                || available < cost.Value)
            {
                return false;
            }

            remainingPowerByTrait[cost.Key] = available - cost.Value;
        }

        return pool.Power + remainingPowerByTrait.Values.Sum() >= anyPowerCost;
    }

    public static (int AnyPower, IReadOnlyDictionary<string, int> PowerByTrait) PayPowerCost(
        RunePool pool,
        int anyPowerCost,
        IReadOnlyDictionary<string, int> powerCostByTrait)
    {
        var remainingAnyPower = pool.Power;
        var remainingPowerByTrait = pool.PowerByTrait.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        foreach (var cost in NormalizePowerCostByTrait(powerCostByTrait))
        {
            remainingPowerByTrait[cost.Key] -= cost.Value;
        }

        var remainingAnyCost = anyPowerCost;
        var paidFromAny = Math.Min(remainingAnyPower, remainingAnyCost);
        remainingAnyPower -= paidFromAny;
        remainingAnyCost -= paidFromAny;

        foreach (var trait in remainingPowerByTrait.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray())
        {
            if (remainingAnyCost <= 0)
            {
                break;
            }

            var paidFromTrait = Math.Min(remainingPowerByTrait[trait], remainingAnyCost);
            remainingPowerByTrait[trait] -= paidFromTrait;
            remainingAnyCost -= paidFromTrait;
        }

        return (
            remainingAnyPower,
            remainingPowerByTrait
                .Where(entry => entry.Value > 0)
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal));
    }

    public static IReadOnlyDictionary<string, int> NormalizePowerCostByTrait(
        IReadOnlyDictionary<string, int> powerCostByTrait)
    {
        return powerCostByTrait
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && entry.Value > 0)
            .GroupBy(entry => RuneTrait.Normalize(entry.Key), StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(entry => Math.Max(0, entry.Value)),
                StringComparer.Ordinal);
    }

    public static IReadOnlyDictionary<string, int> PayExperienceCosts(
        MatchState state,
        string playerId,
        int experienceCost)
    {
        if (experienceCost <= 0)
        {
            return state.PlayerExperience;
        }

        var playerExperience = state.Seats.Keys.ToDictionary(
            seatPlayerId => seatPlayerId,
            seatPlayerId => state.PlayerExperience.TryGetValue(seatPlayerId, out var experience) ? experience : 0,
            StringComparer.Ordinal);
        playerExperience[playerId] = Math.Max(
            0,
            playerExperience.TryGetValue(playerId, out var currentExperience)
                ? currentExperience - experienceCost
                : 0);
        return playerExperience;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "COST";
    }

    private static string NormalizePaymentToken(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "UNKNOWN"
            : value.Trim().Replace(' ', '_');
    }
}

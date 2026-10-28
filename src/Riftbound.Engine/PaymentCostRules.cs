namespace Riftbound.Engine;

public enum ManaPaymentPurpose { Other, PlaySpell }
// Equipment includes equipment abilities; the play-only purposes distinguish
// a resource that can be spent to play a unit or equipment, but not an ability.
public enum PowerPaymentPurpose { Other, PlaySpell, Equipment, PlayUnit, PlayEquipment }

public static class PaymentCostRules
{
    public const string RuneCostPaymentKind = "RUNE_COST";

    public sealed record PaymentPlan
    {
        public PaymentPlan(
            string paymentId,
            string paymentWindow,
            string playerId,
            int baseManaCost = 0,
            int totalManaCost = 0,
            int genericPowerCost = 0,
            int totalPowerCost = 0,
            IReadOnlyDictionary<string, int>? powerCostByTrait = null,
            int experienceCost = 0,
            IReadOnlyList<string>? optionalCostIds = null,
            IReadOnlyList<string>? extraCostIds = null,
            IReadOnlyList<string>? paymentResourceActionIds = null,
            IReadOnlyList<string>? legalPaymentChoiceIds = null,
            string? reason = null,
            string? sourceObjectId = null,
            string? abilityId = null,
            IReadOnlyDictionary<string, object?>? auditMetadata = null,
            ManaPaymentPurpose manaPurpose = ManaPaymentPurpose.Other,
            PowerPaymentPurpose? powerPurpose = null,
            IReadOnlyDictionary<string, int>? powerSourceSelection = null)
        {
            PaymentId = NormalizeOptionalText(paymentId) ?? "UNKNOWN";
            PaymentWindow = NormalizeOptionalText(paymentWindow) ?? "UNKNOWN";
            PlayerId = NormalizeOptionalText(playerId) ?? string.Empty;
            BaseManaCost = Math.Max(0, baseManaCost);
            TotalManaCost = Math.Max(0, totalManaCost);
            GenericPowerCost = Math.Max(0, genericPowerCost);
            PowerCostByTrait = NormalizePowerCostByTrait(
                powerCostByTrait ?? new Dictionary<string, int>(StringComparer.Ordinal));
            TotalPowerCost = Math.Max(
                Math.Max(0, totalPowerCost),
                GenericPowerCost + PowerCostByTrait.Values.Sum());
            ExperienceCost = Math.Max(0, experienceCost);
            OptionalCostIds = NormalizeTextList(optionalCostIds);
            ExtraCostIds = NormalizeTextList(extraCostIds);
            PaymentResourceActionIds = NormalizeTextList(paymentResourceActionIds);
            LegalPaymentChoiceIds = NormalizeTextList(legalPaymentChoiceIds);
            Reason = NormalizeOptionalText(reason);
            SourceObjectId = NormalizeOptionalText(sourceObjectId);
            AbilityId = NormalizeOptionalText(abilityId);
            ManaPurpose = manaPurpose;
            PowerPurpose = powerPurpose ?? (manaPurpose == ManaPaymentPurpose.PlaySpell
                ? PowerPaymentPurpose.PlaySpell : PowerPaymentPurpose.Other);
            PowerSourceSelection = powerSourceSelection?.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
            AuditMetadata = (auditMetadata ?? new Dictionary<string, object?>(StringComparer.Ordinal))
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
                .ToDictionary(entry => entry.Key.Trim(), entry => entry.Value, StringComparer.Ordinal);
        }

        public string PaymentId { get; }

        public string PaymentWindow { get; }

        public string PlayerId { get; }

        public int BaseManaCost { get; }

        public int TotalManaCost { get; }

        public int GenericPowerCost { get; }

        public int TotalPowerCost { get; }

        public IReadOnlyDictionary<string, int> PowerCostByTrait { get; }

        public int ExperienceCost { get; }

        public IReadOnlyList<string> OptionalCostIds { get; }

        public IReadOnlyList<string> ExtraCostIds { get; }

        public IReadOnlyList<string> PaymentResourceActionIds { get; }

        public IReadOnlyList<string> LegalPaymentChoiceIds { get; }

        public string? Reason { get; }

        public string? SourceObjectId { get; }

        public string? AbilityId { get; }

        public ManaPaymentPurpose ManaPurpose { get; }

        public PowerPaymentPurpose PowerPurpose { get; }

        public IReadOnlyDictionary<string, int>? PowerSourceSelection { get; }

        public IReadOnlyDictionary<string, object?> AuditMetadata { get; }
    }

    public sealed record PaymentAuthorizationResult(
        bool Accepted,
        string? ErrorCode = null,
        string? Reason = null);

    public sealed record PaymentCommitResult(
        bool Accepted,
        string? ErrorCode,
        string? ErrorMessage,
        IReadOnlyDictionary<string, RunePool> RunePools,
        IReadOnlyDictionary<string, int> PlayerExperience);

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

    public static PaymentAuthorizationResult AuthorizePayment(
        PaymentPlan plan,
        RunePool currentPool,
        int currentExperience = 0,
        MatchState? state = null)
    {
        if (string.IsNullOrWhiteSpace(plan.PlayerId))
        {
            return new(false, "INVALID_PAYMENT_PLAN", "Payment plan requires a player id.");
        }

        if (!currentPool.HasSupportedAmounts
            || plan.ManaPurpose is not (ManaPaymentPurpose.Other or ManaPaymentPurpose.PlaySpell)
            || !SupportedPowerPurpose(plan.PowerPurpose))
            return new(false, "INVALID_PAYMENT_PLAN", "Unsupported resource amount or mana payment purpose.");

        if (AvailableMana(currentPool, plan.ManaPurpose, state) < plan.TotalManaCost
            || !CanPayPowerCost(currentPool, plan.GenericPowerCost, plan.PowerCostByTrait, plan.PowerPurpose))
        {
            return new(false, "INSUFFICIENT_COST", "Not enough rune resources to pay the payment plan.");
        }

        if (plan.PowerSourceSelection is { } selected
            && (!CanSpendPowerSources(currentPool, plan.PowerPurpose, selected, plan.GenericPowerCost)
                || plan.PowerCostByTrait.Any(cost => cost.Value > currentPool.PowerByTrait.GetValueOrDefault(cost.Key))))
            return new(false, "INVALID_PAYMENT_PLAN", "The selected wildcard power sources cannot pay this plan.");

        if (currentExperience < plan.ExperienceCost)
        {
            return new(false, "INSUFFICIENT_COST", "Not enough experience to pay the payment plan.");
        }

        return new(true);
    }

    public static PaymentCommitResult TryCommitPayment(
        PaymentPlan plan,
        IReadOnlyDictionary<string, RunePool> currentRunePools,
        IReadOnlyDictionary<string, int>? currentPlayerExperience = null,
        MatchState? state = null)
    {
        var runePools = currentRunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var playerExperience = (currentPlayerExperience ?? new Dictionary<string, int>(StringComparer.Ordinal))
            .ToDictionary(entry => entry.Key, entry => Math.Max(0, entry.Value), StringComparer.Ordinal);
        var currentPool = runePools.TryGetValue(plan.PlayerId, out var runePool) ? runePool : RunePool.Empty;
        var currentExperience = playerExperience.TryGetValue(plan.PlayerId, out var experience) ? experience : 0;
        var authorization = AuthorizePayment(plan, currentPool, currentExperience, state);
        if (!authorization.Accepted)
        {
            return new(
                false,
                authorization.ErrorCode,
                authorization.Reason,
                runePools,
                playerExperience);
        }

        var paidPower = PayPowerCost(
            currentPool,
            plan.GenericPowerCost,
            plan.PowerCostByTrait,
            plan.PowerPurpose,
            plan.PowerSourceSelection);
        var paidMana = SpendMana(currentPool, plan.TotalManaCost, plan.ManaPurpose, state);
        runePools[plan.PlayerId] = paidPower with {
            Mana = paidMana.Mana, SpellOnlyMana = paidMana.SpellOnlyMana, DuelOnlyMana = paidMana.DuelOnlyMana };

        if (plan.ExperienceCost > 0)
        {
            playerExperience[plan.PlayerId] = Math.Max(0, currentExperience - plan.ExperienceCost);
        }

        return new(true, null, null, runePools, playerExperience);
    }

    public static Dictionary<string, object?> BuildCostPaidPayload(
        PaymentPlan plan,
        IReadOnlyDictionary<string, RunePool> runePoolsAfterPayment,
        IReadOnlyDictionary<string, int>? playerExperienceAfterPayment,
        IReadOnlyDictionary<string, object?> payload)
    {
        var result = BuildCostPaidPayload(
            plan.PaymentId,
            plan.PaymentWindow,
            plan.PlayerId,
            runePoolsAfterPayment,
            playerExperienceAfterPayment,
            payload);
        result.TryAdd("baseManaCost", plan.BaseManaCost);
        result.TryAdd("totalManaCost", plan.TotalManaCost);
        result.TryAdd("genericPower", plan.GenericPowerCost);
        result.TryAdd("totalPowerCost", plan.TotalPowerCost);
        result.TryAdd("powerByTrait", plan.PowerCostByTrait);
        result.TryAdd("experienceCost", plan.ExperienceCost);
        if (plan.ManaPurpose == ManaPaymentPurpose.PlaySpell)
            result.TryAdd("manaPurpose", "PLAY_SPELL");
        if (plan.PowerPurpose != PowerPaymentPurpose.Other)
            result.TryAdd("powerPurpose", plan.PowerPurpose switch {
                PowerPaymentPurpose.PlaySpell => "PLAY_SPELL", PowerPaymentPurpose.PlayUnit => "PLAY_UNIT", _ => "EQUIPMENT" });
        result.TryAdd("optionalCosts", plan.OptionalCostIds.ToArray());
        result.TryAdd("extraCosts", plan.ExtraCostIds.ToArray());
        result.TryAdd("paymentResourceActions", plan.PaymentResourceActionIds.ToArray());
        result.TryAdd("legalPaymentChoiceIds", plan.LegalPaymentChoiceIds.ToArray());
        if (!string.IsNullOrWhiteSpace(plan.Reason))
        {
            result.TryAdd("reason", plan.Reason);
        }

        if (!string.IsNullOrWhiteSpace(plan.SourceObjectId))
        {
            result.TryAdd("sourceObjectId", plan.SourceObjectId);
        }

        if (!string.IsNullOrWhiteSpace(plan.AbilityId))
        {
            result.TryAdd("abilityId", plan.AbilityId);
        }

        foreach (var entry in plan.AuditMetadata)
        {
            result.TryAdd(entry.Key, entry.Value);
        }

        return result;
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
        if (remainingPool.SpellOnlyMana > 0) result.TryAdd("remainingSpellOnlyMana", remainingPool.SpellOnlyMana);
        if (remainingPool.DuelOnlyMana > 0) result.TryAdd("remainingDuelOnlyMana", remainingPool.DuelOnlyMana);
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
            powerCostByTrait, state);
    }

    public static Dictionary<string, RunePool> PayRuneCosts(
        IReadOnlyDictionary<string, RunePool> currentRunePools,
        string playerId,
        int manaCost,
        int powerCost,
        IReadOnlyDictionary<string, int>? powerCostByTrait = null,
        MatchState? state = null)
    {
        var runePools = currentRunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var currentPool = runePools.TryGetValue(playerId, out var runePool) ? runePool : RunePool.Empty;
        var (remainingAnyPower, remainingPowerByTrait) = PayPowerCost(
            currentPool,
            powerCost,
            powerCostByTrait ?? new Dictionary<string, int>(StringComparer.Ordinal));
        runePools[playerId] = SpendMana(currentPool, manaCost, state: state) with {
            Power = remainingAnyPower, PowerByTrait = remainingPowerByTrait };

        return runePools;
    }

    public static bool CanPayRuneCosts(
        RunePool pool,
        int manaCost,
        int anyPowerCost,
        IReadOnlyDictionary<string, int>? powerCostByTrait = null,
        MatchState? state = null)
    {
        return pool.HasSupportedAmounts && manaCost >= 0
            && anyPowerCost >= 0
            && AvailableMana(pool, ManaPaymentPurpose.Other, state) >= manaCost
            && CanPayPowerCost(
                pool,
                anyPowerCost,
                powerCostByTrait ?? new Dictionary<string, int>(StringComparer.Ordinal));
    }

    public static bool IsSpellDuel(MatchState? state)
        // A closed chain can retain NEUTRAL_CLOSED while its captured stack
        // context still belongs to a formal duel. Use the existing derived window.
        => state?.TurnWindow.IsSpellDuel == true;

    public static int AvailableMana(RunePool pool, ManaPaymentPurpose purpose, MatchState? state = null)
        => !pool.HasSupportedAmounts || purpose is not (ManaPaymentPurpose.Other or ManaPaymentPurpose.PlaySpell) ? -1 : pool.Mana
            + (purpose == ManaPaymentPurpose.PlaySpell ? pool.SpellOnlyMana : 0)
            + (IsSpellDuel(state) ? pool.DuelOnlyMana : 0);

    // The current authoritative window enables this bucket; a caller without
    // that context cannot spend it. Spend temporary-window mana before pools
    // which remain usable after the duel. Never relabel the remaining balance.
    public static RunePool SpendMana(RunePool pool, int amount,
        ManaPaymentPurpose purpose = ManaPaymentPurpose.Other, MatchState? state = null)
    {
        if (amount < 0 || AvailableMana(pool, purpose, state) < amount)
            throw new InvalidOperationException("Not enough eligible mana for this payment.");
        var duel = IsSpellDuel(state) ? Math.Min(pool.DuelOnlyMana, amount) : 0;
        var spell = purpose == ManaPaymentPurpose.PlaySpell ? Math.Min(pool.SpellOnlyMana, amount - duel) : 0;
        return pool with { Mana = pool.Mana - (amount - duel - spell),
            SpellOnlyMana = pool.SpellOnlyMana - spell, DuelOnlyMana = pool.DuelOnlyMana - duel };
    }

    public static bool CanPayPowerCost(
        RunePool pool,
        int anyPowerCost,
        IReadOnlyDictionary<string, int> powerCostByTrait,
        PowerPaymentPurpose purpose = PowerPaymentPurpose.Other)
    {
        if (anyPowerCost < 0 || !pool.HasSupportedAmounts || !SupportedPowerPurpose(purpose))
        {
            return false;
        }

        return PowerDeficit(PowerPoolForPurpose(pool, purpose), anyPowerCost, powerCostByTrait) == 0;
    }

    public static int AvailablePower(RunePool pool, PowerPaymentPurpose purpose)
        => !pool.HasSupportedAmounts || !SupportedPowerPurpose(purpose) ? -1
            : pool.TotalPower + RestrictedPowerAvailable(pool, purpose);

    private static bool SupportedPowerPurpose(PowerPaymentPurpose purpose)
        => purpose is PowerPaymentPurpose.Other or PowerPaymentPurpose.PlaySpell or PowerPaymentPurpose.Equipment
            or PowerPaymentPurpose.PlayUnit or PowerPaymentPurpose.PlayEquipment;

    private static int RestrictedPowerAvailable(RunePool pool, PowerPaymentPurpose purpose)
        => purpose switch {
            PowerPaymentPurpose.PlaySpell => pool.SpellOnlyPower,
            PowerPaymentPurpose.Equipment => pool.EquipmentOnlyPower,
            PowerPaymentPurpose.PlayUnit => pool.UnitOrEquipmentPlayOnlyPower,
            PowerPaymentPurpose.PlayEquipment => pool.EquipmentOnlyPower + pool.UnitOrEquipmentPlayOnlyPower,
            _ => 0 };

    public static RunePool PowerPoolForPurpose(RunePool pool, PowerPaymentPurpose purpose)
        => !pool.HasSupportedAmounts || !SupportedPowerPurpose(purpose) ? pool : pool with {
            Power = pool.Power + RestrictedPowerAvailable(pool, purpose),
            SpellOnlyPower = purpose == PowerPaymentPurpose.PlaySpell ? 0 : pool.SpellOnlyPower,
            EquipmentOnlyPower = purpose is PowerPaymentPurpose.Equipment or PowerPaymentPurpose.PlayEquipment ? 0 : pool.EquipmentOnlyPower,
            UnitOrEquipmentPlayOnlyPower = purpose is PowerPaymentPurpose.PlayUnit or PowerPaymentPurpose.PlayEquipment ? 0 : pool.UnitOrEquipmentPlayOnlyPower };

    public static IReadOnlyDictionary<string, int> AvailablePowerSources(RunePool pool, PowerPaymentPurpose purpose)
    {
        var sources = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!pool.HasSupportedAmounts || !SupportedPowerPurpose(purpose)) return sources;
        if (pool.Power > 0) sources["UNRESTRICTED"] = pool.Power;
        if (purpose == PowerPaymentPurpose.PlaySpell && pool.SpellOnlyPower > 0) sources["SPELL_ONLY"] = pool.SpellOnlyPower;
        if (purpose is PowerPaymentPurpose.Equipment or PowerPaymentPurpose.PlayEquipment && pool.EquipmentOnlyPower > 0)
            sources["EQUIPMENT_ONLY"] = pool.EquipmentOnlyPower;
        if (purpose is PowerPaymentPurpose.PlayUnit or PowerPaymentPurpose.PlayEquipment && pool.UnitOrEquipmentPlayOnlyPower > 0)
            sources["UNIT_OR_EQUIPMENT_PLAY_ONLY"] = pool.UnitOrEquipmentPlayOnlyPower;
        return sources;
    }

    public static bool CanSpendPowerSources(RunePool pool, PowerPaymentPurpose purpose,
        IReadOnlyDictionary<string, int> selection, int amount)
    {
        if (amount <= 0 || !pool.HasSupportedAmounts || !SupportedPowerPurpose(purpose) || selection.Count == 0) return false;
        var available = AvailablePowerSources(pool, purpose);
        long total = 0;
        foreach (var (source, count) in selection)
        {
            if (count <= 0 || !available.TryGetValue(source, out var capacity) || count > capacity) return false;
            total += count;
        }
        return total == amount;
    }

    public static RunePool PayPowerCost(RunePool pool, int anyPowerCost,
        IReadOnlyDictionary<string, int> powerCostByTrait, PowerPaymentPurpose purpose,
        IReadOnlyDictionary<string, int>? powerSourceSelection = null)
    {
        var view = PowerPoolForPurpose(pool, purpose);
        var paid = PayPowerCost(view, anyPowerCost, powerCostByTrait);
        var spent = view.Power - paid.AnyPower;
        if (powerSourceSelection is { } selected)
        {
            if (!CanSpendPowerSources(pool, purpose, selected, spent))
                throw new InvalidOperationException("Invalid selected wildcard power sources.");
            return pool with { Power = pool.Power - selected.GetValueOrDefault("UNRESTRICTED"),
                PowerByTrait = paid.PowerByTrait,
                SpellOnlyPower = pool.SpellOnlyPower - selected.GetValueOrDefault("SPELL_ONLY"),
                EquipmentOnlyPower = pool.EquipmentOnlyPower - selected.GetValueOrDefault("EQUIPMENT_ONLY"),
                UnitOrEquipmentPlayOnlyPower = pool.UnitOrEquipmentPlayOnlyPower - selected.GetValueOrDefault("UNIT_OR_EQUIPMENT_PLAY_ONLY") };
        }
        var spellSpent = purpose == PowerPaymentPurpose.PlaySpell ? Math.Min(pool.SpellOnlyPower, spent) : 0;
        var equipmentSpent = purpose is PowerPaymentPurpose.Equipment or PowerPaymentPurpose.PlayEquipment
            ? Math.Min(pool.EquipmentOnlyPower, spent - spellSpent) : 0;
        var permanentSpent = purpose is PowerPaymentPurpose.PlayUnit or PowerPaymentPurpose.PlayEquipment
            ? Math.Min(pool.UnitOrEquipmentPlayOnlyPower, spent - spellSpent - equipmentSpent) : 0;
        var restrictedSpent = spellSpent + equipmentSpent + permanentSpent;
        return pool with {
            Power = pool.Power - (spent - restrictedSpent), PowerByTrait = paid.PowerByTrait,
            SpellOnlyPower = pool.SpellOnlyPower - spellSpent,
            EquipmentOnlyPower = pool.EquipmentOnlyPower - equipmentSpent,
            UnitOrEquipmentPlayOnlyPower = pool.UnitOrEquipmentPlayOnlyPower - permanentSpent };
    }

    public static int PowerDeficit(RunePool pool, int anyPowerCost, IReadOnlyDictionary<string, int> powerCostByTrait)
    {
        var costs = NormalizePowerCostByTrait(powerCostByTrait);
        var coloredDeficit = costs.Sum(cost => Math.Max(0, cost.Value - pool.PowerByTrait.GetValueOrDefault(cost.Key)));
        // Rainbow [A] may pay any trait; unrelated colored power cannot.
        return Math.Max(Math.Max(0, coloredDeficit - pool.Power),
            Math.Max(0, Math.Max(0, anyPowerCost) + costs.Values.Sum() - pool.TotalPower));
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
            var available = remainingPowerByTrait.GetValueOrDefault(cost.Key);
            var coloredPayment = Math.Min(available, cost.Value);
            remainingPowerByTrait[cost.Key] = available - coloredPayment;
            remainingAnyPower -= cost.Value - coloredPayment;
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

    private static IReadOnlyList<string> NormalizeTextList(IReadOnlyList<string>? values)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

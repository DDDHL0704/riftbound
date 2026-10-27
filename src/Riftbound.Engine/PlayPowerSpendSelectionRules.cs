using System.Globalization;

namespace Riftbound.Engine;

// CN 135.2.e.5.a/b: the required traits and the wallet resources used to pay
// them are separate. This selector never changes a fee or acquires resources.
internal static class PlayPowerSpendSelectionRules
{
    internal const string ChoicePrefix = "POWER_SPEND:";
    private static readonly string[] Traits = ["blue", "green", "orange", "purple", "red", "yellow"];
    internal sealed record Selection(int Wildcard, IReadOnlyDictionary<string, int> ByTrait)
    {
        internal long Total => (long)Wildcard + ByTrait.Values.Sum(value => (long)value);
    }

    internal static IReadOnlyDictionary<string, object?> Metadata { get; } = new Dictionary<string, object?> {
        ["choicePrefix"] = ChoicePrefix, ["wildcardKey"] = "A", ["typedKeys"] = Traits.ToArray(),
        ["componentFormat"] = "<key>:<positiveInt32>", ["separator"] = ",",
        ["maximumComponents"] = 7, ["maximumTotal"] = int.MaxValue, ["ordinalKeys"] = true,
        ["positivePowerCostOnly"] = true, ["usePreviewAvailableWallet"] = true, ["exactTotal"] = true };

    internal static bool TryExtract(IReadOnlyList<string> costs, out IReadOnlyList<string> remaining, out Selection? selection)
    {
        selection = null;
        var choices = costs.Where(IsSelection).ToArray();
        remaining = costs.Where(cost => !IsSelection(cost)).ToArray();
        if (choices.Length == 0) return true;
        if (choices.Length != 1 || choices[0] != choices[0].Trim() || choices[0].Length > ChoicePrefix.Length + 7 * 18) return false;
        var parts = choices[0][ChoicePrefix.Length..].Split(',');
        if (parts.Length is < 1 or > 7) return false;
        var wildcard = 0;
        var typed = new Dictionary<string, int>(StringComparer.Ordinal);
        string? previous = null;
        long total = 0;
        foreach (var part in parts)
        {
            var pair = part.Split(':');
            if (pair.Length != 2 || (pair[0] != "A" && !Traits.Contains(pair[0], StringComparer.Ordinal))
                || previous is not null && string.CompareOrdinal(previous, pair[0]) >= 0
                || pair[1].Length == 0 || pair[1][0] == '0' || pair[1].Any(c => c is < '0' or > '9')
                || !int.TryParse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
                return false;
            total += amount;
            if (total > int.MaxValue) return false;
            if (pair[0] == "A") wildcard = amount;
            else typed.Add(pair[0], amount);
            previous = pair[0];
        }
        selection = new(wildcard, typed);
        return true;

        static bool IsSelection(string cost)
            => !string.IsNullOrWhiteSpace(cost) && cost.Trim().StartsWith(ChoicePrefix, StringComparison.Ordinal);
    }

    internal static bool CanPay(Selection selection, RunePool available, int generic, IReadOnlyDictionary<string, int> typed)
    {
        if (!available.HasSupportedAmounts || generic < 0 || selection.Total <= 0
            || selection.Wildcard > available.Power || typed.Values.Any(value => value < 0)
            || typed.Keys.Any(trait => !Traits.Contains(trait, StringComparer.Ordinal))
            || selection.ByTrait.Any(pair => pair.Value > available.PowerByTrait.GetValueOrDefault(pair.Key))) return false;
        var required = (long)generic + typed.Values.Sum(value => (long)value);
        // Wildcards must cover every missing required trait. All remaining
        // selected resources can then pay the generic part, by the exact total.
        return required <= int.MaxValue && selection.Total == required
            && typed.Sum(pair => Math.Max(0L, (long)pair.Value - selection.ByTrait.GetValueOrDefault(pair.Key))) <= selection.Wildcard;
    }

    internal static PaymentCostRules.PaymentPlan DebitPlan(PaymentCostRules.PaymentPlan fee, Selection selection)
        => new(fee.PaymentId, fee.PaymentWindow, fee.PlayerId,
            baseManaCost: fee.BaseManaCost, totalManaCost: fee.TotalManaCost,
            genericPowerCost: selection.Wildcard, totalPowerCost: fee.TotalPowerCost, powerCostByTrait: selection.ByTrait,
            experienceCost: fee.ExperienceCost, optionalCostIds: fee.OptionalCostIds, extraCostIds: fee.ExtraCostIds,
            paymentResourceActionIds: fee.PaymentResourceActionIds, legalPaymentChoiceIds: fee.LegalPaymentChoiceIds,
            reason: fee.Reason, sourceObjectId: fee.SourceObjectId, abilityId: fee.AbilityId,
            auditMetadata: fee.AuditMetadata, manaPurpose: fee.ManaPurpose, powerPurpose: fee.PowerPurpose);
}

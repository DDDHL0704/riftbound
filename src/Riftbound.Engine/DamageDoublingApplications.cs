using System.Globalization;

namespace Riftbound.Engine;

// One flag is the first application; later ordinals retain independent
// applications through the existing normalized temporary-effect set.
internal static class DamageDoublingApplications
{
    internal const string EffectId = "DAMAGE_RECEIVED_DOUBLED_THIS_TURN";
    private const string Prefix = EffectId + ":";

    private static bool IsApplication(string? effect)
        => effect == EffectId || effect?.StartsWith(Prefix, StringComparison.Ordinal) == true;

    internal static int Count(CardObjectState card) => card.UntilEndOfTurnEffects.Count(IsApplication);

    internal static string NextEffectId(CardObjectState card, string definition)
    {
        if (definition != EffectId) return definition;
        var count = Count(card);
        if (count == int.MaxValue) throw new DamageNumericLimitException("伤害替换次数");
        return count == 0 ? EffectId : Prefix + (count + 1).ToString(CultureInfo.InvariantCulture);
    }

    internal static int LethalBudget(CardObjectState card, int requiredDamage)
    {
        for (var remaining = Count(card); remaining > 0 && requiredDamage > 1; remaining--)
            requiredDamage = requiredDamage / 2 + requiredDamage % 2;
        return requiredDamage;
    }

    internal static int Apply(CardObjectState card, int damage)
    {
        if (damage == 0) return 0;
        for (var remaining = Count(card); remaining > 0; remaining--)
        {
            if (damage > int.MaxValue / 2) throw new DamageNumericLimitException("伤害翻倍结果");
            damage *= 2;
        }
        return damage;
    }

    internal static int AddMarkedDamage(int marked, int damage)
    {
        if (damage > 0 && marked > int.MaxValue - damage) throw new DamageNumericLimitException("累计伤害标记");
        return marked + damage;
    }

    internal static bool IsValid(CardObjectState card)
    {
        var entries = card.UntilEndOfTurnEffects.Where(IsApplication).ToArray();
        if (entries.Length == 0) return true;
        if (entries.Count(e => e == EffectId) != 1
            || !entries.SequenceEqual(entries.Order(StringComparer.Ordinal))) return false;
        var ordinals = new List<int>();
        foreach (var entry in entries.Where(e => e != EffectId))
        {
            var suffix = entry[Prefix.Length..];
            if (!int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal)
                || ordinal < 2 || suffix != ordinal.ToString(CultureInfo.InvariantCulture)) return false;
            ordinals.Add(ordinal);
        }
        return ordinals.Order().SequenceEqual(Enumerable.Range(2, ordinals.Count));
    }
}

internal sealed class DamageNumericLimitException(string operation) : Exception(operation);

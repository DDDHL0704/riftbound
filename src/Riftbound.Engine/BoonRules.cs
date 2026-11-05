using Riftbound.CardCatalog;

namespace Riftbound.Engine;

internal static class BoonRules
{
    private static readonly Lazy<HashSet<string>> UnlimitedBoonFaces = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards
            .Where(card => card.CardEffect.Contains("我可以拥有不限数量的增益", StringComparison.Ordinal))
            .Select(card => card.CardNo).ToHashSet(StringComparer.Ordinal));

    // Tag-only fixtures describe a single counter. All gameplay mutations write
    // the count and retain the tag only while at least one counter remains.
    internal static int Count(CardObjectState unit)
        => Math.Max(unit.BoonCount, unit.Tags.Contains(CardObjectTags.Boon, StringComparer.Ordinal) ? 1 : 0);

    internal static bool CanGrant(CardObjectState unit, IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards)
        => Count(unit) == 0 || AttachedRuleTextRules.Instances(zones, cards, unit)
            .Any(instance => UnlimitedBoonFaces.Value.Contains(instance.CardNo));

    internal static bool TryGrant(CardObjectState unit, IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, out CardObjectState result)
    {
        result = unit;
        // CN 427.1.b.1-2 / 702.3 / 703: one counter unless permitted otherwise;
        // every counter contributes its own +1 Might.
        if (!CanGrant(unit, zones, cards)) return false;
        result = unit with { BoonCount = checked(Count(unit) + 1), Power = unit.Power + 1,
            Tags = unit.Tags.Append(CardObjectTags.Boon).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
        return true;
    }

    internal static CardObjectState Consume(CardObjectState unit)
    {
        // CN 702.2.b removes a single counter, not every counter on the unit.
        var count = Count(unit);
        if (count == 0) return unit;
        return unit with { BoonCount = count - 1, Power = unit.Power - 1,
            Tags = count == 1 ? unit.Tags.Where(tag => tag != CardObjectTags.Boon).ToArray() : unit.Tags };
    }
}

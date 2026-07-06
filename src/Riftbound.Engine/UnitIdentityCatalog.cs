namespace Riftbound.Engine;

public static class UnitIdentityCatalog
{
    public const string TeemoUnitIdentityId = "UNIT_IDENTITY_TEEMO";

    private static readonly IReadOnlyDictionary<string, string> DisplayNameByIdentityId =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [TeemoUnitIdentityId] = "提莫"
        };

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> SourceCardNosByIdentityId =
        new(BuildSourceCardNosByIdentityId, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<string> SourceCardNosForIdentity(string? identityId)
    {
        return !string.IsNullOrWhiteSpace(identityId)
            && SourceCardNosByIdentityId.Value.TryGetValue(identityId.Trim(), out var sourceCardNos)
                ? sourceCardNos
                : [];
    }

    public static bool IsSourceCardNoForIdentity(
        string? identityId,
        string? cardNo)
    {
        return !string.IsNullOrWhiteSpace(cardNo)
            && SourceCardNosForIdentity(identityId).Contains(cardNo.Trim(), StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildSourceCardNosByIdentityId()
    {
        return DisplayNameByIdentityId.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)CardBehaviorRegistry.GetAll()
                .Where(definition => definition.PlaysSourceToBaseAsUnit
                    && string.Equals(definition.DisplayName, pair.Value, StringComparison.Ordinal))
                .Select(definition => definition.CardNo)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(cardNo => cardNo, StringComparer.Ordinal)
                .ToArray(),
            StringComparer.Ordinal);
    }
}

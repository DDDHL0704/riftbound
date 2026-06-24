using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class StaticAuraSpecRules
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<StaticAuraSpec>>> StaticAurasByCardNo =
        new(BuildStaticAuraMap, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<StaticAuraSpec> GetStaticAuras(string? cardNo)
    {
        if (string.IsNullOrWhiteSpace(cardNo))
        {
            return [];
        }

        return StaticAurasByCardNo.Value.TryGetValue(cardNo.Trim(), out var auras)
            ? auras
            : [];
    }

    public static bool TryGetFriendlyEquipmentPowerAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.FriendlyFieldEquipmentCountToSourceUnitPower,
            out aura);
    }

    public static bool TryGetBattlefieldAllUnitsPowerAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.BattlefieldAllUnitsPowerPlusOne,
            out aura);
    }

    public static bool TryGetSameBattlefieldOtherFriendlyUnitsPowerAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.SameBattlefieldOtherFriendlyUnitsPowerPlusOne,
            out aura);
    }

    public static bool TryGetSameBattlefieldOtherFriendlyFilteredUnitsPowerAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.SameBattlefieldOtherFriendlyFilteredUnitsPower,
            out aura);
    }

    public static bool TryGetOtherFriendlyUnitsPowerAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.OtherFriendlyUnitsPower,
            out aura);
    }

    public static bool TryGetFriendlyFilteredUnitsPowerAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.FriendlyFilteredUnitsPower,
            out aura);
    }

    public static bool TargetMatchesFilter(StaticAuraSpec aura, CardObjectState target)
    {
        if (string.IsNullOrWhiteSpace(aura.TargetFilter))
        {
            return false;
        }

        if (string.Equals(aura.TargetFilter, StaticAuraTargetFilters.UnitToken, StringComparison.Ordinal))
        {
            return IsUnitTokenCardNo(target.CardNo);
        }

        if (aura.TargetFilter.StartsWith(StaticAuraTargetFilters.TagPrefix, StringComparison.Ordinal))
        {
            var requiredTag = aura.TargetFilter[StaticAuraTargetFilters.TagPrefix.Length..];
            return !string.IsNullOrWhiteSpace(requiredTag)
                && target.Tags.Contains(requiredTag, StringComparer.Ordinal);
        }

        return false;
    }

    private static bool TryGetAura(string? cardNo, string kind, out StaticAuraSpec aura)
    {
        aura = default!;
        var match = GetStaticAuras(cardNo)
            .FirstOrDefault(candidate => string.Equals(candidate.Kind, kind, StringComparison.Ordinal));
        if (match is null)
        {
            return false;
        }

        aura = match;
        return true;
    }

    private static bool IsUnitTokenCardNo(string? cardNo)
    {
        return !string.IsNullOrWhiteSpace(cardNo)
            && P6TokenFactoryCatalog.TryGetByCardNo(cardNo, out var definition)
            && string.Equals(definition.CategoryName, "指示物单位", StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<StaticAuraSpec>> BuildStaticAuraMap()
    {
        var catalog = OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult();
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var playCardBehaviors = CardBehaviorRegistry.GetAll()
            .Select(behavior => new ImplementedCardBehavior(
                behavior.CardNo,
                behavior.EffectKind,
                behavior.DisplayName))
            .ToArray();
        var implementedBehaviors = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(
            catalog.Cards,
            playCardBehaviors);

        return BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, implementedBehaviors)
            .Where(spec => spec.StaticAuras.Count > 0)
            .ToDictionary(
                spec => spec.CardNo,
                spec => (IReadOnlyList<StaticAuraSpec>)spec.StaticAuras,
                StringComparer.Ordinal);
    }
}

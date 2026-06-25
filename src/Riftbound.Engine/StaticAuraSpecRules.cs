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

    public static IReadOnlyList<StaticAuraSpec> GetStaticAuras(string? cardNo, string kind)
    {
        return GetStaticAuras(cardNo)
            .Where(aura => string.Equals(aura.Kind, kind, StringComparison.Ordinal))
            .ToArray();
    }

    public static bool TryGetFriendlyEquipmentPowerAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.FriendlyFieldEquipmentCountToSourceUnitPower,
            out aura);
    }

    public static bool TryGetSourceObjectFilteredPowerAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.SourceObjectFilteredPower,
            out aura);
    }

    public static bool TryGetBattlefieldAllUnitsPowerAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.BattlefieldAllUnitsPowerPlusOne,
            out aura);
    }

    public static bool TryGetBattlefieldFilteredUnitsPowerAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.BattlefieldFilteredUnitsPower,
            out aura);
    }

    public static bool TryGetBattlefieldAllUnitsKeywordAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.BattlefieldAllUnitsKeyword,
            out aura);
    }

    public static bool TryGetBattlefieldFilteredUnitsKeywordAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.BattlefieldFilteredUnitsKeyword,
            out aura);
    }

    public static bool TryGetBattlefieldIsolatedDefenderKeywordModifierAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.BattlefieldIsolatedDefenderKeywordModifier,
            out aura);
    }

    public static bool TryGetSameBattlefieldOtherFriendlyUnitsPowerAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.SameBattlefieldOtherFriendlyUnitsPowerPlusOne,
            out aura);
    }

    public static bool TryGetSameBattlefieldOtherFriendlyUnitsKeywordAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.SameBattlefieldOtherFriendlyUnitsKeyword,
            out aura);
    }

    public static bool TryGetSameBattlefieldOtherFriendlyFilteredUnitsPowerAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.SameBattlefieldOtherFriendlyFilteredUnitsPower,
            out aura);
    }

    public static bool TryGetSameBattlefieldFriendlyFilteredUnitCountToSourcePowerAura(
        string? cardNo,
        out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.SameBattlefieldFriendlyFilteredUnitCountToSourcePower,
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

    public static bool TryGetFriendlyFilteredUnitsKeywordAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.FriendlyFilteredUnitsKeyword,
            out aura);
    }

    public static bool TryGetSourceAttackingWithAnotherUnitPowerAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.SourceAttackingWithAnotherUnitPower,
            out aura);
    }

    public static bool TryGetSourceLoneBattlePowerAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.SourceLoneBattlePower,
            out aura);
    }

    public static bool TryGetSourceAttackingReadyEnemyUnitPowerAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.SourceAttackingReadyEnemyUnitPower,
            out aura);
    }

    public static bool TargetMatchesFilter(StaticAuraSpec aura, CardObjectState target)
    {
        if (string.IsNullOrWhiteSpace(aura.TargetFilter))
        {
            return false;
        }

        if (aura.TargetFilter.StartsWith(StaticAuraTargetFilters.AnyPrefix, StringComparison.Ordinal))
        {
            return aura.TargetFilter[StaticAuraTargetFilters.AnyPrefix.Length..]
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(filter => TargetMatchesFilter(filter, target));
        }

        return TargetMatchesFilter(aura.TargetFilter, target);
    }

    private static bool TargetMatchesFilter(string targetFilter, CardObjectState target)
    {
        if (string.Equals(targetFilter, StaticAuraTargetFilters.UnitToken, StringComparison.Ordinal))
        {
            return P6TokenFactoryCatalog.IsUnitTokenFactory(target.CardNo);
        }

        if (targetFilter.StartsWith(StaticAuraTargetFilters.TagPrefix, StringComparison.Ordinal))
        {
            var requiredTag = targetFilter[StaticAuraTargetFilters.TagPrefix.Length..];
            return !string.IsNullOrWhiteSpace(requiredTag)
                && target.Tags.Contains(requiredTag, StringComparer.Ordinal);
        }

        if (targetFilter.StartsWith(StaticAuraTargetFilters.CardNamePrefix, StringComparison.Ordinal))
        {
            var requiredCardName = targetFilter[StaticAuraTargetFilters.CardNamePrefix.Length..];
            return !string.IsNullOrWhiteSpace(requiredCardName)
                && !string.IsNullOrWhiteSpace(target.CardNo)
                && CardBehaviorRegistry.TryGetByCardNo(target.CardNo, out var behavior)
                && string.Equals(behavior.DisplayName, requiredCardName, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool TryGetAura(string? cardNo, string kind, out StaticAuraSpec aura)
    {
        aura = default!;
        var match = GetStaticAuras(cardNo, kind).FirstOrDefault();
        if (match is null)
        {
            return false;
        }

        aura = match;
        return true;
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

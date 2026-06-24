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

    public static bool TryGetOtherFriendlyUnitsPowerAura(string? cardNo, out StaticAuraSpec aura)
    {
        return TryGetAura(
            cardNo,
            StaticAuraKinds.OtherFriendlyUnitsPower,
            out aura);
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

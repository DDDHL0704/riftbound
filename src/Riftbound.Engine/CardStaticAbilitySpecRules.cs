using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class CardStaticAbilitySpecRules
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<StaticAbilitySpec>>> StaticAbilitiesByCardNo =
        new(BuildStaticAbilityMap, LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool CardCannotBecomeActive(string? cardNo)
    {
        return TryGetUnitCannotBecomeActiveAbility(cardNo, out _);
    }

    public static bool TryGetUnitCannotBecomeActiveAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.UnitCannotBecomeActive,
            out ability);
    }

    public static bool TryGetUnitPowerfulSelfKeywordsAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
                cardNo,
                StaticAbilityKinds.UnitPowerfulSelfKeywords,
                out ability)
            && ability.RequiredPowerThreshold is > 0
            && ability.GrantedKeywords is { Count: > 0 };
    }

    public static bool TryGetSourceUnitEnterReadyAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.SourceUnitEnterReady,
            out ability);
    }

    public static bool TryGetOtherFriendlyUnitsEnterReadyAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.OtherFriendlyUnitsEnterReady,
            out ability);
    }

    public static bool TryGetFriendlyUnitsEnterReadyAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.FriendlyUnitsEnterReady,
            out ability);
    }

    public static bool TryGetFriendlyFilteredUnitsEnterReadyAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.FriendlyFilteredUnitsEnterReady,
            out ability)
            && !string.IsNullOrWhiteSpace(ability.TargetFilter);
    }

    public static bool TryGetStaticAbility(string? cardNo, string kind, out StaticAbilitySpec ability)
    {
        ability = default!;
        if (string.IsNullOrWhiteSpace(cardNo))
        {
            return false;
        }

        if (!StaticAbilitiesByCardNo.Value.TryGetValue(cardNo.Trim(), out var abilities))
        {
            return false;
        }

        var match = abilities.FirstOrDefault(candidate => string.Equals(candidate.Kind, kind, StringComparison.Ordinal));
        if (match is null)
        {
            return false;
        }

        ability = match;
        return true;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<StaticAbilitySpec>> BuildStaticAbilityMap()
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
            .Where(spec => spec.StaticAbilities.Count > 0)
            .ToDictionary(
                spec => spec.CardNo,
                spec => (IReadOnlyList<StaticAbilitySpec>)spec.StaticAbilities,
                StringComparer.Ordinal);
    }
}

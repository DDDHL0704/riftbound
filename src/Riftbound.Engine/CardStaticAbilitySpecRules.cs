using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class CardStaticAbilitySpecRules
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<StaticAbilitySpec>>> StaticAbilitiesByCardNo =
        new(BuildStaticAbilityMap, LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool CardCannotBecomeActive(string? cardNo)
    {
        return TryGetStaticAbility(cardNo, IsUnitCannotBecomeActiveAbility, out _);
    }

    public static bool IsUnitCannotBecomeActiveAbility(StaticAbilitySpec ability)
    {
        return string.Equals(ability.Kind, StaticAbilityKinds.UnitCannotBecomeActive, StringComparison.Ordinal);
    }

    public static bool IsUnitPowerfulSelfKeywordsAbility(StaticAbilitySpec ability)
    {
        return string.Equals(ability.Kind, StaticAbilityKinds.UnitPowerfulSelfKeywords, StringComparison.Ordinal)
            && ability.RequiredPowerThreshold is > 0
            && ability.GrantedKeywords is { Count: > 0 };
    }

    public static bool IsSourceUnitEnterReadyAbility(StaticAbilitySpec ability)
    {
        return string.Equals(ability.Kind, StaticAbilityKinds.SourceUnitEnterReady, StringComparison.Ordinal);
    }

    public static bool IsOtherFriendlyUnitsEnterReadyAbility(StaticAbilitySpec ability)
    {
        return string.Equals(ability.Kind, StaticAbilityKinds.OtherFriendlyUnitsEnterReady, StringComparison.Ordinal);
    }

    public static bool IsFriendlyUnitsEnterReadyAbility(StaticAbilitySpec ability)
    {
        return string.Equals(ability.Kind, StaticAbilityKinds.FriendlyUnitsEnterReady, StringComparison.Ordinal);
    }

    public static bool IsFriendlyFilteredUnitsEnterReadyAbility(StaticAbilitySpec ability)
    {
        return string.Equals(ability.Kind, StaticAbilityKinds.FriendlyFilteredUnitsEnterReady, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(ability.TargetFilter);
    }

    public static bool IsSameBattlefieldEphemeralTurnStartSuppressionAbility(StaticAbilitySpec ability)
    {
        return string.Equals(
                ability.Kind,
                StaticAbilityKinds.SameBattlefieldEphemeralTurnStartSuppression,
                StringComparison.Ordinal)
            && string.Equals(
                ability.TargetFilter,
                StaticAuraTargetFilters.TagPrefix + CardObjectTags.Ephemeral,
                StringComparison.Ordinal);
    }

    public static bool IsSourceUnitEnemySpellSkillTargetProtectionAbility(StaticAbilitySpec ability)
    {
        return string.Equals(
            ability.Kind,
            StaticAbilityKinds.SourceUnitEnemySpellSkillTargetProtection,
            StringComparison.Ordinal);
    }

    public static bool TryGetStaticAbility(
        string? cardNo,
        Func<StaticAbilitySpec, bool> predicate,
        out StaticAbilitySpec ability)
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

        var match = abilities.FirstOrDefault(predicate);
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

using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class BattlefieldStaticAbilitySpecRules
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<StaticAbilitySpec>>> StaticAbilitiesByCardNo =
        new(BuildStaticAbilityMap, LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool TryGetBattlefieldPreventMoveToBaseAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.BattlefieldPreventMoveToBase,
            out ability);
    }

    public static bool TryGetBattlefieldPreventUnitPlayAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.BattlefieldPreventUnitPlay,
            out ability);
    }

    public static bool TryGetBattlefieldEchoCostReductionAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.BattlefieldEchoCostReduction,
            out ability);
    }

    public static bool TryGetBattlefieldEquipmentCostReductionAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.BattlefieldEquipmentCostReduction,
            out ability);
    }

    public static bool TryGetBattlefieldGrantUnitExperienceAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.BattlefieldGrantUnitExperienceAbility,
            out ability);
    }

    private static bool TryGetStaticAbility(string? cardNo, string kind, out StaticAbilitySpec ability)
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

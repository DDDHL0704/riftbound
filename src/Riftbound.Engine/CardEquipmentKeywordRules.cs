using System.Diagnostics.CodeAnalysis;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static class CardEquipmentKeywordNames
{
    public const string Assemble = "装配";
    public const string Agile = "灵便";
    public const string Tempered = "百炼";
    public const string Weapon = "武装";
}

public static class EquipmentKeywordProfileStatuses
{
    public const string ImplementedRepresentative = "implemented-representative";
    public const string RecognizedDeferred = "recognized-deferred";
    public const string NotApplicable = "not-applicable";
}

public static class EquipmentAttachmentProfileStatuses
{
    public const string ImplementedRepresentative = "implemented-representative";
    public const string NotApplicable = "not-applicable";
}

public static class EquipmentRepresentativeBoundaryKinds
{
    public const string AgileDirectPlayAttach = "AGILE_DIRECT_PLAY_ATTACH";
    public const string TemperedOptionalAttach = "TEMPERED_OPTIONAL_ATTACH";
    public const string TemperedOptionalAttachEquipment = "TEMPERED_OPTIONAL_ATTACH_EQUIPMENT";
    public const string FriendlyEquipmentStaticPower = "FRIENDLY_EQUIPMENT_STATIC_POWER";
    public const string EquipmentState = "EQUIPMENT_STATE";
}

public sealed record CardEquipmentKeywordProfile(
    bool HasAssemble,
    bool HasAgile,
    bool HasTempered,
    bool HasWeapon,
    bool HasImplementedRepresentativeAssembleBoundary,
    bool HasImplementedRepresentativeAgileDirectPlayAttachBoundary,
    bool HasImplementedRepresentativeTemperedOptionalAttachBoundary,
    bool HasImplementedRepresentativeFriendlyEquipmentStaticPowerBoundary,
    bool HasImplementedRepresentativeEquipmentStateBoundary,
    IReadOnlyList<string> EquipmentStateRepresentativeVerifierTests,
    string Status,
    string Reason);

public sealed record CardEquipmentStateRepresentative(
    string CardNo,
    string CardName,
    IReadOnlyList<string> CoveredBoundaries,
    IReadOnlyList<string> VerifierTestNames);

public sealed record CardEquipmentRepresentativeBoundary(
    string CardNo,
    string Kind);

public sealed record CardEquipmentAttachmentProfile(
    bool CanAttachOrDetachWeapon,
    int DrawCount,
    string Status,
    string Reason);

public static class CardEquipmentKeywordRules
{
    public static readonly IReadOnlyList<CardEquipmentRepresentativeBoundary> EquipmentRepresentativeBoundaries =
    [
        new("SFD·022/221", EquipmentRepresentativeBoundaryKinds.AgileDirectPlayAttach),
        new("SFD·056/221", EquipmentRepresentativeBoundaryKinds.AgileDirectPlayAttach),
        new("SFD·064/221", EquipmentRepresentativeBoundaryKinds.AgileDirectPlayAttach),
        new("SFD·186/221", EquipmentRepresentativeBoundaryKinds.AgileDirectPlayAttach),
        new("SFD·002/221", EquipmentRepresentativeBoundaryKinds.TemperedOptionalAttach),
        new("SFD·008/221", EquipmentRepresentativeBoundaryKinds.TemperedOptionalAttach),
        new("SFD·119/221", EquipmentRepresentativeBoundaryKinds.TemperedOptionalAttach),
        new("SFD·119a/221", EquipmentRepresentativeBoundaryKinds.TemperedOptionalAttach),
        new("SFD·186/221", EquipmentRepresentativeBoundaryKinds.TemperedOptionalAttachEquipment),
        new("SFD·085/221", EquipmentRepresentativeBoundaryKinds.FriendlyEquipmentStaticPower),
        new("SFD·085a/221", EquipmentRepresentativeBoundaryKinds.FriendlyEquipmentStaticPower),
        new("SFD·022/221", EquipmentRepresentativeBoundaryKinds.EquipmentState)
    ];

    public static readonly IReadOnlyList<CardEquipmentStateRepresentative> EquipmentStateRepresentatives =
    [
        new(
            "SFD·022/221",
            "Long Sword",
            [
                "Long Sword owner/controller/attachment invariant",
                "controller mismatch no-mutation rejection",
                "controlled opponent-owned target attach",
                "attached equipment follows host base-to-battlefield movement",
                "attached equipment follows host battlefield-to-base movement",
                "host destroyed detach/recall to owner base"
            ],
            [
                "P5EquipmentStateAssembleLongSwordPreservesOwnerControllerAndAttachment",
                "P5EquipmentStateAssembleLongSwordRejectsControllerMismatchWithoutSideEffects",
                "P5EquipmentStateAssembleLongSwordAllowsControlledOpponentOwnedTarget",
                "P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBattlefield",
                "P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBase",
                "CoreRuleEngineDetachesEquipmentWhenHostUnitIsDestroyed",
                "P5EquipmentStateAssembleLongSwordOwnerControllerFixture",
                "P5MoveUnitCommandAttachedEquipmentFollowsHostFixture"
            ])
    ];

    public static bool HasRepresentativeBoundary(
        string? cardNo,
        string boundaryKind)
    {
        return !string.IsNullOrWhiteSpace(cardNo)
            && !string.IsNullOrWhiteSpace(boundaryKind)
            && EquipmentRepresentativeBoundaries.Any(boundary =>
                string.Equals(boundary.CardNo, cardNo, StringComparison.Ordinal)
                && string.Equals(boundary.Kind, boundaryKind, StringComparison.Ordinal));
    }

    public static bool HasAgileDirectPlayAttachRepresentativeBoundary(string? cardNo)
    {
        return HasRepresentativeBoundary(
            cardNo,
            EquipmentRepresentativeBoundaryKinds.AgileDirectPlayAttach);
    }

    public static bool HasTemperedOptionalAttachRepresentativeBoundary(string? cardNo)
    {
        return HasRepresentativeBoundary(
            cardNo,
            EquipmentRepresentativeBoundaryKinds.TemperedOptionalAttach);
    }

    public static bool CanBeTemperedOptionalAttachEquipment(string? cardNo)
    {
        return HasRepresentativeBoundary(
            cardNo,
            EquipmentRepresentativeBoundaryKinds.TemperedOptionalAttachEquipment);
    }

    public static bool HasFriendlyEquipmentStaticPowerRepresentativeBoundary(string? cardNo)
    {
        return HasRepresentativeBoundary(
            cardNo,
            EquipmentRepresentativeBoundaryKinds.FriendlyEquipmentStaticPower);
    }

    public static bool TryGetEquipmentStateRepresentative(
        string? cardNo,
        [NotNullWhen(true)] out CardEquipmentStateRepresentative? representative)
    {
        representative = null;
        if (!HasRepresentativeBoundary(cardNo, EquipmentRepresentativeBoundaryKinds.EquipmentState))
        {
            return false;
        }

        representative = EquipmentStateRepresentatives.FirstOrDefault(candidate =>
            string.Equals(candidate.CardNo, cardNo, StringComparison.Ordinal));
        return representative is not null;
    }

    public static CardEquipmentKeywordProfile BuildProfile(
        BehaviorSpec spec,
        CardBehaviorDefinition? behavior)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var sourceUnitTags = behavior is null ? [] : ParseDelimitedValues(behavior.SourceUnitTags);
        var sourceEquipmentTags = behavior is null ? [] : ParseDelimitedValues(behavior.SourceEquipmentTags);
        var isEquipmentCard = string.Equals(spec.CardCategoryName, "装备", StringComparison.Ordinal);
        var hasAssemble = isEquipmentCard
            && (HasOwnKeywordLine(spec, CardEquipmentKeywordNames.Assemble)
                || HasKeyword(spec, CardEquipmentKeywordNames.Assemble));
        var hasAgile = HasExactKeyword(sourceEquipmentTags, CardEquipmentKeywordNames.Agile)
            || (isEquipmentCard && HasOwnKeywordLine(spec, CardEquipmentKeywordNames.Agile));
        var hasTempered = HasExactKeyword(sourceUnitTags, CardEquipmentKeywordNames.Tempered)
            || HasOwnKeywordLine(spec, CardEquipmentKeywordNames.Tempered);
        var hasWeapon = HasExactKeyword(sourceEquipmentTags, CardEquipmentKeywordNames.Weapon);
        var hasAnyEquipmentKeyword = hasAssemble
            || hasAgile
            || hasTempered
            || hasWeapon;
        var hasImplementedRepresentativeAssembleBoundary = hasAssemble
            && AssembleEquipmentProfileCatalog.HasImplementedRepresentative(spec.CardNo);
        var hasImplementedRepresentativeAgileDirectPlayAttachBoundary = hasAgile
            && HasAgileDirectPlayAttachRepresentativeBoundary(spec.CardNo);
        var hasImplementedRepresentativeTemperedOptionalAttachBoundary = hasTempered
            && HasTemperedOptionalAttachRepresentativeBoundary(spec.CardNo);
        var hasImplementedRepresentativeFriendlyEquipmentStaticPowerBoundary =
            HasFriendlyEquipmentStaticPowerRepresentativeBoundary(spec.CardNo)
            && spec.StaticAuras.Any(aura => string.Equals(
                aura.Kind,
                StaticAuraKinds.FriendlyFieldEquipmentCountToSourceUnitPower,
                StringComparison.Ordinal));
        TryGetEquipmentStateRepresentative(spec.CardNo, out var equipmentStateRepresentative);
        var hasImplementedRepresentativeEquipmentStateBoundary = equipmentStateRepresentative is not null;
        var hasDeferredOfficialBreadth = hasAgile
            || hasTempered
            || hasWeapon
            || (hasAssemble && !hasImplementedRepresentativeAssembleBoundary);

        return new CardEquipmentKeywordProfile(
            hasAssemble,
            hasAgile,
            hasTempered,
            hasWeapon,
            hasImplementedRepresentativeAssembleBoundary,
            hasImplementedRepresentativeAgileDirectPlayAttachBoundary,
            hasImplementedRepresentativeTemperedOptionalAttachBoundary,
            hasImplementedRepresentativeFriendlyEquipmentStaticPowerBoundary,
            hasImplementedRepresentativeEquipmentStateBoundary,
            equipmentStateRepresentative?.VerifierTestNames ?? [],
            hasAnyEquipmentKeyword
                ? hasDeferredOfficialBreadth
                    ? EquipmentKeywordProfileStatuses.RecognizedDeferred
                    : EquipmentKeywordProfileStatuses.ImplementedRepresentative
                : EquipmentKeywordProfileStatuses.NotApplicable,
            hasAnyEquipmentKeyword
                ? EquipmentKeywordReason(
                    hasImplementedRepresentativeAssembleBoundary,
                    hasImplementedRepresentativeAgileDirectPlayAttachBoundary,
                    hasImplementedRepresentativeTemperedOptionalAttachBoundary,
                    hasImplementedRepresentativeFriendlyEquipmentStaticPowerBoundary,
                    hasImplementedRepresentativeEquipmentStateBoundary,
                    hasDeferredOfficialBreadth)
                : "Card does not expose equipment keyword surfaces through P3 BehaviorSpec or the P2 source-object tag path.");
    }

    public static CardEquipmentAttachmentProfile BuildAttachmentProfile(CardBehaviorDefinition behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);

        if (!behavior.AttachesOrDetachesSecondTargetEquipmentToFirstTarget)
        {
            return new CardEquipmentAttachmentProfile(
                false,
                0,
                EquipmentAttachmentProfileStatuses.NotApplicable,
                "Card does not expose the P4.58 attach/detach representative route.");
        }

        return new CardEquipmentAttachmentProfile(
            true,
            behavior.DrawCount,
            EquipmentAttachmentProfileStatuses.ImplementedRepresentative,
            "P4.58 verifies the existing Take Up attach/detach representative through P2 fixtures; assemble costs, Agile auto-attach, Tempered optional attachment, other static equipment modifiers, full owner/controller breadth, and full attach lifecycle breadth remain deferred.");
    }

    private static bool HasKeyword(
        BehaviorSpec spec,
        string keyword)
    {
        return spec.Keywords.Any(candidate => string.Equals(candidate.Keyword, keyword, StringComparison.Ordinal));
    }

    private static bool HasOwnKeywordLine(
        BehaviorSpec spec,
        string keyword)
    {
        var marker = $"{{{{{keyword}";
        return spec.OfficialText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(line => line.StartsWith(marker, StringComparison.Ordinal));
    }

    private static bool HasExactKeyword(
        IReadOnlyList<string> tags,
        string keyword)
    {
        return tags.Any(tag => string.Equals(tag, keyword, StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> ParseDelimitedValues(string value)
    {
        return value
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
    }

    private static string EquipmentKeywordReason(
        bool hasImplementedRepresentativeAssembleBoundary,
        bool hasImplementedRepresentativeAgileDirectPlayAttachBoundary,
        bool hasImplementedRepresentativeTemperedOptionalAttachBoundary,
        bool hasImplementedRepresentativeFriendlyEquipmentStaticPowerBoundary,
        bool hasImplementedRepresentativeEquipmentStateBoundary,
        bool hasDeferredOfficialBreadth)
    {
        var implementedBoundaries = new List<string>();
        if (hasImplementedRepresentativeAssembleBoundary)
        {
            implementedBoundaries.Add("P4 ASSEMBLE_EQUIPMENT");
        }

        if (hasImplementedRepresentativeAgileDirectPlayAttachBoundary)
        {
            implementedBoundaries.Add("Agile direct-play attach");
        }

        if (hasImplementedRepresentativeTemperedOptionalAttachBoundary)
        {
            implementedBoundaries.Add("Tempered optional attach");
        }

        if (hasImplementedRepresentativeFriendlyEquipmentStaticPowerBoundary)
        {
            implementedBoundaries.Add("Ornn friendly-equipment static power");
        }

        if (hasImplementedRepresentativeEquipmentStateBoundary)
        {
            implementedBoundaries.Add("P5 equipment state representatives");
        }

        if (implementedBoundaries.Count > 0 && !hasDeferredOfficialBreadth)
        {
            return $"{string.Join(" and ", implementedBoundaries)} are covered by existing server-authoritative representative boundaries; Agile reaction timing, Jax-granted Agile, remaining ephemeral/static equipment breadth, full Tempered official breadth, copy-text effects, full owner/controller breadth, full attach lifecycle breadth, LayerEngine, and full equipment official coverage remain deferred.";
        }

        if (implementedBoundaries.Count > 0)
        {
            return $"{string.Join(" and ", implementedBoundaries)} are covered by existing server-authoritative representative boundaries, but this card still exposes deferred equipment breadth such as Agile reaction timing, Jax-granted Agile, remaining ephemeral/static equipment breadth, full Tempered official breadth, other weapon/static modifiers, copy-text effects, full owner/controller breadth, full attach lifecycle breadth, LayerEngine, and full equipment official coverage.";
        }

        return "P4.8 recognizes equipment keyword surfaces from P3 BehaviorSpec and P2 source-object tags; assemble costs without a registered representative profile, Agile reaction attachment, Jax-granted Agile, full Tempered official breadth, static equipment modifiers outside registered representatives, copy-text effects, full owner/controller breadth, full attach lifecycle breadth, LayerEngine, and full equipment official coverage remain deferred.";
    }
}

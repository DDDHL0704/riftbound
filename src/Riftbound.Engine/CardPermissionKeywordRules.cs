namespace Riftbound.Engine;

public static class CardPermissionKeywordNames
{
    public const string None = "NONE";
    public const string OrdinaryTurn = "ORDINARY_TURN";
    public const string Swift = "迅捷";
    public const string Reaction = "反应";
    public const string Haste = "急速";
}

public static class HasteOptionalReadyBranchStatuses
{
    public const string NotApplicable = "not-applicable";
    public const string RecognizedDeferred = "recognized-deferred";
    public const string ImplementedRepresentative = "implemented-representative";
}

public static class HasteOptionalCostNames
{
    public const string HasteReady = "HASTE_READY";
}

public sealed record CardPermissionKeywordProfile(
    bool HasSwift,
    bool HasReaction,
    bool HasHaste,
    string HasteOptionalReadyBranchStatus,
    string HasteOptionalReadyBranchReason,
    int HasteReadyManaCost,
    int HasteReadyPowerCost);

public sealed record CardPlayTimingDecision(
    bool IsAllowed,
    string PermissionKeyword,
    string Reason);

public static class CardPermissionKeywordRules
{
    public static CardPermissionKeywordProfile BuildProfile(CardBehaviorDefinition behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);

        var hasHaste = HasSourceKeyword(behavior, CardPermissionKeywordNames.Haste);
        var hasImplementedHasteReadyBranch = hasHaste
            && (behavior.HasteReadyManaCost > 0 || behavior.HasteReadyPowerCost > 0);
        return new CardPermissionKeywordProfile(
            behavior.CanPlayDuringSpellDuel,
            behavior.CanPlayDuringPriority || HasSourceKeyword(behavior, CardPermissionKeywordNames.Reaction),
            hasHaste,
            hasHaste
                ? hasImplementedHasteReadyBranch
                    ? HasteOptionalReadyBranchStatuses.ImplementedRepresentative
                    : HasteOptionalReadyBranchStatuses.RecognizedDeferred
                : HasteOptionalReadyBranchStatuses.NotApplicable,
            hasHaste
                ? hasImplementedHasteReadyBranch
                    ? "P4.13/P4.18/P4.20/P4.25/P4.26/P4.27/P4.30/P4.31/P4.32/P4.33/P4.34/P4.35/P4.36/P4.37/P4.38/P4.39/P4.40/P4.41/P4.42/P4.43/P4.44/P4.45/P4.46/P4.47/P4.48/P4.49/P4.50/P4.51/P4.52/P4.53/P4.54/P4.55/P4.56/P4.57 implement representative HASTE_READY optional costs through the current mana + power resource model and keep remaining Haste cards deferred."
                    : "P4.2 recognizes Haste in source unit tags and keeps the verified no-optional entry path; the extra-pay ready-entry branch is deferred until colored resource and ready-entry cost modeling."
                : "Card does not expose the Haste keyword through the P2 source unit tag path.",
            hasImplementedHasteReadyBranch ? behavior.HasteReadyManaCost : 0,
            hasImplementedHasteReadyBranch ? behavior.HasteReadyPowerCost : 0);
    }

    public static CardPlayTimingDecision EvaluatePlayTiming(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(behavior);

        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal))
        {
            return Rejected("PLAY_CARD is not allowed outside MAIN phase.");
        }

        if (IsTurnPlayerOrdinaryOpenMainPhase(state, playerId))
        {
            return new CardPlayTimingDecision(
                true,
                CardPermissionKeywordNames.OrdinaryTurn,
                "Turn player may play cards in the ordinary open main phase.");
        }

        if (CanPlayReactionInPriorityWindow(state, playerId, behavior))
        {
            return new CardPlayTimingDecision(
                true,
                CardPermissionKeywordNames.Reaction,
                "Reaction card may be played by the priority player while a stack item is pending.");
        }

        if (CanPlaySwiftInSpellDuelFocusWindow(state, playerId, behavior))
        {
            return new CardPlayTimingDecision(
                true,
                CardPermissionKeywordNames.Swift,
                "Swift card may be played by the focus player during an open spell duel.");
        }

        return Rejected("PLAY_CARD is not allowed in the current timing window.");
    }

    public static bool HasSourceKeyword(CardBehaviorDefinition behavior, string keyword)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        return SourceTags(behavior)
            .Any(tag => string.Equals(tag, keyword.Trim(), StringComparison.Ordinal));
    }

    public static bool TryBuildHasteReadyOptionalCost(
        IReadOnlyList<string> normalizedOptionalCosts,
        CardBehaviorDefinition behavior,
        out int extraManaCost,
        out int extraPowerCost)
    {
        ArgumentNullException.ThrowIfNull(normalizedOptionalCosts);
        ArgumentNullException.ThrowIfNull(behavior);

        extraManaCost = 0;
        extraPowerCost = 0;
        if (normalizedOptionalCosts.Count == 1
            && string.Equals(normalizedOptionalCosts[0], HasteOptionalCostNames.HasteReady, StringComparison.Ordinal)
            && HasSourceKeyword(behavior, CardPermissionKeywordNames.Haste)
            && (behavior.HasteReadyManaCost > 0 || behavior.HasteReadyPowerCost > 0))
        {
            extraManaCost = behavior.HasteReadyManaCost;
            extraPowerCost = behavior.HasteReadyPowerCost;
            return true;
        }

        return false;
    }

    public static bool IsHasteReadyOptionalCostPaid(
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> optionalCosts)
    {
        return TryBuildHasteReadyOptionalCost(
            optionalCosts,
            behavior,
            out _,
            out _);
    }

    private static bool IsTurnPlayerOrdinaryOpenMainPhase(MatchState state, string playerId)
    {
        return string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            && string.Equals(state.TurnPlayerId, playerId, StringComparison.Ordinal);
    }

    private static bool CanPlayReactionInPriorityWindow(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        return behavior.CanPlayDuringPriority
            && state.StackItems.Count > 0
            && !string.IsNullOrWhiteSpace(state.PriorityPlayerId)
            && string.Equals(state.PriorityPlayerId, playerId, StringComparison.Ordinal);
    }

    private static bool CanPlaySwiftInSpellDuelFocusWindow(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        return behavior.CanPlayDuringSpellDuel
            && state.StackItems.Count == 0
            && string.Equals(state.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(state.FocusPlayerId)
            && string.Equals(state.FocusPlayerId, playerId, StringComparison.Ordinal);
    }

    private static CardPlayTimingDecision Rejected(string reason)
    {
        return new CardPlayTimingDecision(false, CardPermissionKeywordNames.None, reason);
    }

    private static IReadOnlyList<string> SourceTags(CardBehaviorDefinition behavior)
    {
        return ParseDelimitedValues(behavior.SourceUnitTags)
            .Concat(ParseDelimitedValues(behavior.SourceEquipmentTags))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ParseDelimitedValues(string value)
    {
        return value
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
    }
}

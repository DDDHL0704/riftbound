using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // Ambush changes the legal location and timing of the ordinary play process.
    // It does not create a separate payment or permanent-confirmation process.
    private static bool AmbushLocationAllowed(MatchState state, string player,
        CardBehaviorDefinition behavior, string destination, IReadOnlyList<string>? destroyedCosts = null)
    {
        if (!behavior.PlaysSourceToBaseAsUnit || !AmbushInteractionSpecRules.HasAmbush(behavior.CardNo)
            || !destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)) return false;
        var field = destination["BATTLEFIELD:".Length..];
        if (!state.CardObjects.TryGetValue(field, out var battlefield) || !IsBattlefieldCardObject(battlefield)
            || battlefield.IsFaceDown || !IsObjectOnField(state.PlayerZones, field)) return false;
        // The currently implemented destruction replacements recall to Base or
        // otherwise remove the chosen unit from the battlefield. Its departure
        // is part of paying the cost, before the final timing check (358.4).
        return state.CardObjects.Values.Any(unit => !(destroyedCosts?.Contains(unit.ObjectId) ?? false)
            && IsVisibleFieldUnitObject(state.CardObjects, unit.ObjectId)
            && IsObjectOnField(state.PlayerZones, unit.ObjectId)
            && state.ObjectLocations.TryGetValue(unit.ObjectId, out var location)
            && location.Zone == "BATTLEFIELD" && location.BattlefieldObjectId == field
            && (EffectiveFieldControllerId(state.PlayerZones, unit.ObjectId, unit) == player
                || behavior.AllowsAmbushToEnemyOccupiedBattlefield));
    }

    private static CardBehaviorDefinition AmbushTimingBehavior(MatchState state, string player,
        CardBehaviorDefinition behavior, string destination, IReadOnlyList<string>? destroyedCosts = null)
        => AmbushLocationAllowed(state, player, behavior, destination, destroyedCosts)
            ? behavior with { CanPlayDuringPriority = true, CanPlayDuringSpellDuel = true } : behavior;

    internal static CardBehaviorDefinition PromptAmbushTimingBehavior(MatchState state, string player,
        CardBehaviorDefinition behavior)
        => behavior.PlaysSourceToBaseAsUnit && state.CardObjects.Values.Any(field =>
            IsBattlefieldCardObject(field) && AmbushLocationAllowed(state, player, behavior, "BATTLEFIELD:" + field.ObjectId))
            ? behavior with { CanPlayDuringPriority = true, CanPlayDuringSpellDuel = true } : behavior;

    private static bool AdditionalCostPlayLocationAllowed(MatchState state, CardBehaviorDefinition behavior,
        string destination, IReadOnlyList<string> destroyedCosts)
        => behavior.AllowsPlayAtDestroyedAdditionalCostLocation && destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)
            && destroyedCosts.Any(id => state.ObjectLocations.TryGetValue(id, out var location)
                && location.Zone == "BATTLEFIELD" && "BATTLEFIELD:" + location.BattlefieldObjectId == destination);

    internal static IReadOnlyList<ActionPromptChoiceDto> UnitPlayDestinationChoices(MatchState state, string player,
        CardBehaviorDefinition behavior)
    {
        // Recover the underlying card mode before testing each destination;
        // the aggregate prompt's reaction permission cannot permit a Base play.
        if (CardBehaviorRegistry.TryGetByCardNoAndMode(behavior.CardNo, behavior.Mode, out var printed))
            behavior = EffectPlayBehavior(state, player, printed);
        var choices = new List<ActionPromptChoiceDto>();
        if (CardPermissionKeywordRules.EvaluatePlayTiming(state, player, behavior).IsAllowed)
            choices.Add(new("BASE", "基地"));
        foreach (var field in state.CardObjects.Values.Where(c => IsBattlefieldCardObject(c) && IsObjectOnField(state.PlayerZones, c.ObjectId))) {
            var destination = "BATTLEFIELD:" + field.ObjectId;
            if ((IsPlayCardUnitBattlefieldDestinationAllowed(state, player, destination)
                    || AmbushLocationAllowed(state, player, behavior, destination)
                    || RengarAttackingBattlefieldPlayAllowed(state, player, behavior, destination)
                    || behavior.AllowsPlayAtDestroyedAdditionalCostLocation && state.CardObjects.Values.Any(unit =>
                        CanDestroyFriendlyTraitUnitAsAdditionalCost(state, player, unit.ObjectId)
                        && state.ObjectLocations.TryGetValue(unit.ObjectId, out var at) && at.Zone == "BATTLEFIELD"
                        && at.BattlefieldObjectId == field.ObjectId))
                && CardPermissionKeywordRules.EvaluatePlayTiming(state, player,
                    AmbushTimingBehavior(state, player, behavior, destination)).IsAllowed
                && !HasBattlefieldStaticPreventUnitPlayToBattlefield(state, player, destination))
                choices.Add(new(destination, field.ObjectId));
        }
        // Retain ordinary Base-only development fixtures' existing destination;
        // Ambush always requires a real occupied battlefield.
        if (state.BattlefieldStates.Count == 0 && CardPermissionKeywordRules.EvaluatePlayTiming(state, player, behavior).IsAllowed) {
            var destination = "BATTLEFIELD:" + player + "-MAIN";
            if (!HasBattlefieldStaticPreventUnitPlayToBattlefield(state, player, destination)) choices.Add(new(destination, "己方主战场"));
        }
        return choices;
    }
}

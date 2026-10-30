namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // A battlefield-only printed ability contributes neither targets nor damage
    // on a base play. The independent legend ability can still use that item.
    private static CardBehaviorDefinition PermanentPlayDefinition(CardBehaviorDefinition behavior, bool battlefield)
        => OfficialPrintedTimingRules.HasPlayTrigger(behavior.CardNo)
            && !OfficialPrintedTimingRules.HasPlayTrigger(behavior.CardNo, battlefield)
                ? behavior with { DamageAmount = 0, RequiredTargetCount = 0, MinTargetCount = 0 }
                : behavior;

    private static bool PermanentPlayHereTargetAllowed(MatchState state, StackItemState item,
        CardBehaviorDefinition behavior, string target)
    {
        if (!item.SourceConfirmed || (behavior.TargetScope != CardTargetScopes.EnemyUnitAtSourceLocation && !behavior.PlayTargetsAtSourceLocation)) return true;
        // CN 124 and 359.3.f.2: movement within the field preserves this source;
        // leaving and re-entering creates another object, not a new "here".
        if ((behavior.PlayTargetsAtSourceLocation ? PlayInstructionSource(state, item) : item.PermanentPlaySource) is not { } binding
            || !state.CardObjects.TryGetValue(binding.ObjectId, out var source)
            || source.ObjectGeneration != binding.Generation || !IsFaceUpNonStandbyUnit(source)
            || !state.ObjectLocations.TryGetValue(binding.ObjectId, out var location)
            || !state.ObjectLocations.TryGetValue(target, out var targetLocation)) return false;
        if (location.Zone == "BATTLEFIELD")
            return BattlefieldLocalRules.AtUnit(state, binding.ObjectId) is { } here
                && BattlefieldLocalRules.AtUnit(state, target)?.ObjectId == here.ObjectId;
        return location.Zone == "BASE" && targetLocation.Zone == "BASE"
            && location.PlayerId == targetLocation.PlayerId
            && state.PlayerZones.TryGetValue(location.PlayerId, out var zones)
            && zones.Base.Contains(binding.ObjectId) && zones.Base.Contains(target);
    }
}

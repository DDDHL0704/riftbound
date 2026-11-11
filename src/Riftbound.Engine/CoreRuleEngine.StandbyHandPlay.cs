namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string StandbyHandPlayHereEffect = "PLAY_HAND_STANDBY_HERE";

    internal static bool IsStandbyHandPlayHere(StackItemState item)
        => item.EffectKind == UnitEventEffect && item.UnitEvent is { } context
            && SelectedUnitEventSpec(context)?.Effect == StandbyHandPlayHereEffect;

    // CN359.3.f.2: read the source's current location when the instruction is
    // executed. A departed generation cannot supply a new location.
    private static string? StandbyHandPlayHere(MatchState state, StackItemState parent)
        => CombatMightSourceLocation(state, parent.SourceObjectId, parent.UnitEvent!.SourceGeneration) is { } here
            ? here.Zone == "BASE" ? here.PlayerId == parent.ControllerId ? "BASE" : null
                : here.Zone == "BATTLEFIELD" ? "BATTLEFIELD:" + here.BattlefieldObjectId : null
            : null;

    internal static bool ValidStandbyHandPlayHere(MatchState state, PendingEffectPlayState pending)
    {
        var parent = pending.Parent;
        if (!IsStandbyHandPlayHere(parent) || !ValidUnitEvent(state, parent.UnitEvent, parent.EffectKind,
                parent.SourceObjectId, parent.CardNo, parent.ControllerId)
            || parent.TriggerCost is null || !ValidTriggerCostReceipt(state, parent)
            || parent.EffectPlayCompleted || parent.TargetObjectIds.Count != 0
            || pending.PlayerId != parent.ControllerId || !state.PlayerZones.TryGetValue(pending.PlayerId, out var zones)
            || pending.SourceZone != "HAND" || pending.IgnoreAllCosts || !pending.IgnoreBaseMana || !pending.IgnoreBasePower
            || pending.ManaReduction != 0 || pending.Optional || pending.DestinationPolicy != "SOURCE_HERE_IF_UNIT"
            || pending.SourcePlayerId is not null || pending.SourceReplay is not null || pending.RevealedHand is not null
            || pending.OriginalLocation is not null || pending.OriginalBattlefield is not null
            || pending.MaximumUnitPower is not null || pending.ViewedCardIds is not null
            || !TryGetEffectPlayDefinition(parent, out var definition)) return false;
        var expected = zones.Hand.Where(id => state.CardObjects.TryGetValue(id, out var card)
            && EffectPlaySourceMatches(card, definition)).ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration);
        return expected.Count > 0 && expected.Count == pending.Sources.Count
            && expected.All(p => pending.Sources.GetValueOrDefault(p.Key, -1) == p.Value);
    }
}

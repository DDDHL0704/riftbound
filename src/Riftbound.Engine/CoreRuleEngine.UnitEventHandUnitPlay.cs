namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string UnitEventLookUnitEffect = "LOOK_TOP_REVEAL_DRAW_UNIT_RECYCLE_REST";
    internal const string UnitEventHandUnitPlayEffect = "PLAY_HAND_UNIT_IGNORE_BASE_MANA";

    internal static bool IsUnitEventHandUnitPlay(StackItemState parent)
        => parent.EffectKind == UnitEventEffect && parent.UnitEvent is { } context
            && parent.CardNo == context.CardNo && SelectedUnitEventSpec(context)?.Effect == UnitEventHandUnitPlayEffect;

    private static bool TryGetUnitEventHandUnitPlayDefinition(StackItemState parent, out CardBehaviorDefinition definition)
    {
        definition = null!;
        if (!IsUnitEventHandUnitPlay(parent)) return false;
        // CN356.1.b.2-3: waive base mana only. Printed power, increases and
        // additional costs still go through the ordinary play/payment flow.
        definition = new(parent.CardNo, "从手牌打出单位并无视基础法力费用", 0, parent.EffectKind, 0, 0,
            EffectPlaySourceZone: "HAND", EffectPlayIgnoreBaseMana: true,
            EffectPlayIgnoreBasePower: false, EffectPlayIgnoreAllCosts: false,
            EffectPlayDestination: "BASE", EffectPlayOptional: false);
        return true;
    }

    internal static bool ValidUnitEventHandUnitPlay(MatchState state, PendingEffectPlayState pending)
    {
        var parent = pending.Parent;
        if (!IsUnitEventHandUnitPlay(parent) || !ValidUnitEvent(state, parent.UnitEvent, parent.EffectKind,
                parent.SourceObjectId, parent.CardNo, parent.ControllerId)
            || parent.TriggerCost is not null || parent.EffectPlayCompleted || parent.TargetObjectIds.Count != 0
            || pending.PlayerId != parent.ControllerId || !state.PlayerZones.TryGetValue(pending.PlayerId, out var zones)
            || pending.SourceZone != "HAND" || !pending.IgnoreBaseMana || pending.IgnoreBasePower || pending.IgnoreAllCosts
            || pending.ManaReduction != 0 || pending.Optional || pending.DestinationPolicy != "BASE"
            || pending.SourcePlayerId is not null || pending.SourceOwners is not null
            || pending.SourceReplay is not null || pending.RevealedHand is not null
            || pending.OriginalLocation is not null || pending.OriginalBattlefield is not null
            || pending.MaximumUnitPower is not null || pending.ViewedCardIds is not null
            || !TryGetEffectPlayDefinition(parent, out var definition)) return false;
        var expected = zones.Hand.Where(id => state.CardObjects.TryGetValue(id, out var card)
            && EffectPlaySourceMatches(card, definition)).ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration);
        return expected.Count > 0 && expected.Count == pending.Sources.Count
            && expected.All(p => pending.Sources.GetValueOrDefault(p.Key, -1) == p.Value);
    }
}

using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record UnitEventAbilitySpec
{
    public bool CreatesUnitTokenHere { get; init; }
    public bool ObservesMovementByAnyPlayer { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string UnitEventGraveyardPlayEffect = "PLAY_GRAVEYARD_UNIT";
    internal const string UnitEventPlayPermissionCaptured = "UNIT_EVENT_PLAY_PERMISSION_CAPTURED";

    internal static bool IsUnitEventGraveyardPlay(StackItemState parent)
        => parent.UnitEvent is { } context && parent.EffectKind == UnitEventEffect
            && parent.CardNo == context.CardNo && SelectedUnitEventSpec(context)?.Effect == UnitEventGraveyardPlayEffect;

    private static bool TryGetUnitEventGraveyardPlayDefinition(StackItemState parent, out CardBehaviorDefinition definition)
    {
        definition = null!;
        if (!IsUnitEventGraveyardPlay(parent)) return false;
        definition = new(parent.CardNo, "从废牌堆中打出单位", 0, parent.EffectKind, 0, 0,
            EffectPlaySourceZone: "GRAVEYARD", EffectPlayOptional: true);
        return true;
    }

    private static bool IsUnitEventGraveyardPlaySource(CardObjectState card)
        => !card.IsFaceDown && !IsTokenObject(card) && card.Tags.Contains(CardObjectTags.UnitCard)
            && CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var unit) && unit.PlaysSourceToBaseAsUnit;

    private static StackResolutionResult ResolveUnitEventGraveyardPlay(MatchState state, StackItemState item)
    {
        if (item.EffectPlayCompleted) return NoopStackResolutionResult(state);
        var sources = state.PlayerZones[item.ControllerId].Graveyard.Where(id => state.CardObjects.TryGetValue(id, out var card)
            && IsUnitEventGraveyardPlaySource(card)).ToArray();
        TryGetUnitEventGraveyardPlayDefinition(item, out var definition);
        var result = BeginEffectPlay(state, item, definition, sources);
        if (result.PendingEffectPlay is not { } pending) return result;
        return result with { Events = result.Events.Append(new GameEvent(UnitEventPlayPermissionCaptured,
            "可选择从废牌堆中打出一名单位，仍需支付费用", new Dictionary<string, object?> {
                ["choiceId"] = pending.ChoiceId, ["parentStackItemId"] = item.StackItemId,
                ["permissionHash"] = UnitEventPlayPermissionHash(pending) })).ToArray() };
    }

    internal static string UnitEventPlayPermissionHash(PendingEffectPlayState pending)
        => MatchStateHasher.HashValue(new { pending.ChoiceId, pending.PlayerId, pending.Parent.StackItemId,
            pending.Parent.CardNo, pending.SourceZone, pending.Sources, pending.IgnoreBaseMana,
            pending.IgnoreBasePower, pending.IgnoreAllCosts, pending.DestinationPolicy, pending.Optional });

    internal static bool ValidUnitEventGraveyardPlay(MatchState state, PendingEffectPlayState pending)
        => IsUnitEventGraveyardPlay(pending.Parent)
            && ValidUnitEvent(state, pending.Parent.UnitEvent, pending.Parent.EffectKind,
                pending.Parent.SourceObjectId, pending.Parent.CardNo, pending.Parent.ControllerId)
            && ValidUnitEventTargets(pending.Parent) && !pending.Parent.EffectPlayCompleted
            && pending.PlayerId == pending.Parent.ControllerId && state.PlayerZones.TryGetValue(pending.PlayerId, out var zones)
            && pending.SourceZone == "GRAVEYARD" && pending.DestinationPolicy == "ANY" && pending.Optional
            && !pending.IgnoreBaseMana && !pending.IgnoreBasePower && !pending.IgnoreAllCosts && pending.ManaReduction == 0
            && pending.SourcePlayerId is null && pending.SourceOwners is null && pending.SourceReplay is null
            && pending.OriginalLocation is null && pending.OriginalBattlefield is null && pending.ViewedCardIds is null
            && pending.RevealedHand is null && pending.MaximumUnitPower is null && pending.Sources.Count > 0
            && pending.Sources.All(source => zones.Graveyard.Contains(source.Key)
                && state.CardObjects.TryGetValue(source.Key, out var card) && card.ObjectGeneration == source.Value
                && IsUnitEventGraveyardPlaySource(card));
}

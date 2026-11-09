using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record UnitEventAbilitySpec
{
    public bool CreatesUnitTokenHere { get; init; }
    public bool ObservesMovementByAnyPlayer { get; init; }
    public bool RequiresMoveToBattlefield { get; init; }
    public int GraveyardPlayMaximumMana { get; init; }
    public int GraveyardPlayMaximumPower { get; init; }
    public bool GraveyardPlayIgnoresBaseCosts { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string UnitEventGraveyardPlayEffect = "PLAY_GRAVEYARD_UNIT";

    internal static bool IsUnitEventGraveyardPlay(StackItemState parent)
        => parent.UnitEvent is { } context && parent.EffectKind == UnitEventEffect
            && parent.CardNo == context.CardNo && SelectedUnitEventSpec(context)?.Effect == UnitEventGraveyardPlayEffect;

    internal static string UnitEventPlayTargetsHash(StackItemState item)
        => MatchStateHasher.HashValue(new { item.StackItemId, item.ControllerId,
            Targets = item.TargetObjectIds.Select(id => new ObjectBinding(id,
                item.TargetGenerations?.GetValueOrDefault(id, -1) ?? -1)).ToArray() });

    private static bool TryGetUnitEventGraveyardPlayDefinition(StackItemState parent, out CardBehaviorDefinition definition)
    {
        definition = null!;
        if (!IsUnitEventGraveyardPlay(parent)) return false;
        var spec = SelectedUnitEventSpec(parent.UnitEvent!)!;
        definition = new(parent.CardNo, "从废牌堆中打出单位", 0, parent.EffectKind, 0, 0,
            EffectPlaySourceZone: "GRAVEYARD",
            EffectPlayMaximumManaCost: spec.GraveyardPlayMaximumMana,
            EffectPlayMaximumPowerCost: spec.GraveyardPlayMaximumPower,
            EffectPlayIgnoreBaseMana: spec.GraveyardPlayIgnoresBaseCosts,
            EffectPlayIgnoreBasePower: spec.GraveyardPlayIgnoresBaseCosts);
        return true;
    }

    private static bool IsUnitEventGraveyardPlaySource(CardObjectState card)
        => !card.IsFaceDown && !IsTokenObject(card) && card.Tags.Contains(CardObjectTags.UnitCard)
            && CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var unit) && unit.PlaysSourceToBaseAsUnit;

    private static bool UnitEventGraveyardPlaySourceAllowed(StackItemState parent, CardObjectState card)
        => IsUnitEventGraveyardPlaySource(card)
            && TryGetUnitEventGraveyardPlayDefinition(parent, out var definition)
            && EffectPlayCandidateCostAllowed(card, definition);

    private static StackResolutionResult ResolveUnitEventGraveyardPlay(MatchState state, StackItemState item)
    {
        if (item.EffectPlayCompleted) return NoopStackResolutionResult(state);
        if (item.TargetObjectIds.SingleOrDefault() is not { } target
            || !StackObjectRules.ReferencesCurrentObject(state, item, target)
            || !UnitEventTargets(state, item).Contains(target)) return NoopStackResolutionResult(state);
        TryGetUnitEventGraveyardPlayDefinition(item, out var definition);
        return BeginEffectPlay(state, item, definition);
    }

    internal static bool ValidUnitEventGraveyardPlay(MatchState state, PendingEffectPlayState pending)
        => IsUnitEventGraveyardPlay(pending.Parent)
            && ValidUnitEvent(state, pending.Parent.UnitEvent, pending.Parent.EffectKind,
                pending.Parent.SourceObjectId, pending.Parent.CardNo, pending.Parent.ControllerId)
            && ValidUnitEventTargets(pending.Parent) && !pending.Parent.EffectPlayCompleted
            && pending.PlayerId == pending.Parent.ControllerId && state.PlayerZones.TryGetValue(pending.PlayerId, out var zones)
            && pending.SourceZone == "GRAVEYARD" && pending.DestinationPolicy == "ANY" && !pending.Optional
            && pending.IgnoreBaseMana == SelectedUnitEventSpec(pending.Parent.UnitEvent!)!.GraveyardPlayIgnoresBaseCosts
            && pending.IgnoreBasePower == pending.IgnoreBaseMana && !pending.IgnoreAllCosts && pending.ManaReduction == 0
            && pending.SourcePlayerId is null && pending.SourceOwners is null && pending.SourceReplay is null
            && pending.OriginalLocation is null && pending.OriginalBattlefield is null && pending.ViewedCardIds is null
            && pending.RevealedHand is null && pending.MaximumUnitPower is null && pending.Sources.Count == 1
            && pending.Sources.All(source => zones.Graveyard.Contains(source.Key)
                && pending.Parent.TargetObjectIds.Contains(source.Key)
                && pending.Parent.TargetGenerations?.GetValueOrDefault(source.Key, -1) == source.Value
                && state.CardObjects.TryGetValue(source.Key, out var card) && card.ObjectGeneration == source.Value
                && UnitEventGraveyardPlaySourceAllowed(pending.Parent, card));
}

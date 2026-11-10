using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record SpellInstructionReflexiveOrigin
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnitEventOtherObject? PlayedUnit { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string PlayedUnitStatusReflexiveEffect = "STUN_INSTRUCTION_PLAYED_UNIT";

    private static string PlayedUnitInstructionOccurrence(string parent, int execution, UnitEventOtherObject unit)
        => $"played-unit:{parent}:{execution}:{MatchStateHasher.HashValue(unit)}";

    private static bool ValidPlayedUnitStatusReflexive(MatchState state, UnitEventContext context,
        UnitEventAbilitySpec spec)
        => spec.Timing == SpellInstructionReflexiveTiming && spec.Effect == PlayedUnitStatusReflexiveEffect
            && context.SpellInstruction is { ReferencedMight: null, PlayedUnit: {
                Binding: { ObjectId.Length: > 0, Generation: >= 0 }, CardNo.Length: > 0, FaceRevision: >= 0 } unit } origin
            && CardBehaviorRegistry.TryGetByCardNo(context.CardNo, out var behavior)
            && behavior.EffectPlayedUnitStatus == "STUNNED"
            && origin.Occurrence == PlayedUnitInstructionOccurrence(origin.ParentStackHash, origin.ExecutionIndex, unit)
            && TryPredictEnteredFace(unit.CardNo, out var entered) && entered.Tags.Contains(CardObjectTags.UnitCard)
            && PredictBindingMatches(state, unit.Binding, unit.CardNo, unit.FaceRevision,
                context.CapturedTick, unit.TokenFactoryCardNo);

    private static bool ValidPlayedUnitStatusReflexiveStack(StackItemState item)
        => item.UnitEvent?.SpellInstruction?.PlayedUnit is not null
            && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null && item.OptionalCosts.Count == 0
            && item.TriggerCost is null && item.EffectRepeatCount == 1 && item.RepeatExecutions is null
            && item.DamageAmount == 0 && item.Destination.Length == 0 && !item.SourceConfirmed;

    private static GameEvent CaptureInstructionPlayedUnitReflexive(GameEvent ev, CardObjectState entered)
    {
        if (ev.Kind is not ("UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD")
            || RuleChoices.Value?.Origin.PendingEffectPlay is not { RevealedHand: { Choosing: false } } pending
            || !pending.Sources.TryGetValue(entered.ObjectId, out var handGeneration)
            || UnitEventSpec(pending.Parent.CardNo)?.Effect != PlayedUnitStatusReflexiveEffect
            || CaptureSpellInstructionSource(pending.Parent) is not { } source) return ev;
        // Capture at the actual entry instruction. A later entry replacement,
        // death or return does not undo having executed the required play.
        // The outer continuity finalizer still owns the HAND -> field birth.
        var unit = new UnitEventOtherObject(new(entered.ObjectId, Math.Max(entered.ObjectGeneration,
            checked(handGeneration + 1))), entered.CardNo!, entered.CopyFaceTransitions?.Count ?? 0, entered.TokenFactoryCardNo);
        var parent = MatchStateHasher.HashValue(source.StackItemId);
        var context = SpellInstructionContext(source, PlayedUnitInstructionOccurrence(parent, source.ExecutionIndex, unit));
        context = context with { SpellInstruction = context.SpellInstruction! with { PlayedUnit = unit } };
        return WithUnitEvents(ev, [new(source.Source.ObjectId, source.Controller, context)]);
    }

    private static StackResolutionResult ResolvePlayedUnitStatusReflexive(MatchState state, StackItemState item)
    {
        if (item.UnitEvent?.SpellInstruction?.PlayedUnit is not { } unit
            || !state.CardObjects.TryGetValue(unit.Binding.ObjectId, out var card)
            || card.ObjectGeneration != unit.Binding.Generation || !IsFaceUpNonStandbyUnit(card)
            || !IsObjectOnField(state.PlayerZones, card.ObjectId)) return NoopStackResolutionResult(state);
        // The already selected hidden-hand object is a reference, not a new
        // target choice. A real return/replay cannot replace its captured birth.
        var cards = state.CardObjects.ToDictionary(); var events = new List<GameEvent>();
        ApplyUnitEventStun(state, item, NormalizeZonesForSeats(state), cards, events, card.ObjectId);
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}

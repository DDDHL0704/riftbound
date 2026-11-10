using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record SpellInstructionReflexiveOrigin(string ParentStackHash, int ExecutionIndex, string Occurrence,
    string OwnerId, int? ReferencedMight = null);

public sealed partial record UnitEventContext
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SpellInstructionReflexiveOrigin? SpellInstruction { get; init; }
}

public sealed partial record UnitEventAbilitySpec
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ReflexiveTargetMaximumMight { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string SpellInstructionReflexiveTiming = "SPELL_INSTRUCTION_REFLEXIVE";
    internal const string UnitEventSelfGraveSpellPlayEffect = "PLAY_THIS_SPELL_FROM_GRAVEYARD";
    private sealed record CapturedSpellInstructionSource(string CardNo, string Controller, ObjectBinding Source,
        long FaceRevision, string Owner, string StackItemId, int ExecutionIndex);

    private static CapturedSpellInstructionSource? CaptureSpellInstructionSource(StackItemState? item)
    {
        if (item is null || !PrintedSpellSources.Value.Contains(item.CardNo)
            || !CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior)
            || behavior.CardNo != item.CardNo || !IsSpellPlayBehavior(behavior)
            || behavior.UnitEventAbility?.Timing != SpellInstructionReflexiveTiming
            || AttributionSource(item.SourceObjectId) is not { } source || source.CardNo != item.CardNo) return null;
        return new(item.CardNo, item.ControllerId, new(source.ObjectId, source.ObjectGeneration),
            source.CopyFaceTransitions?.Count ?? 0, source.OwnerId ?? item.ControllerId,
            item.StackItemId, item.CompletedRepeatExecutions);
    }

    private static UnitEventContext SpellInstructionContext(CapturedSpellInstructionSource source,
        string occurrence, int? might = null)
        => new(source.CardNo, source.Source.Generation, source.FaceRevision, 0, SpellInstructionReflexiveTiming) {
            SpellInstruction = new(MatchStateHasher.HashValue(source.StackItemId), source.ExecutionIndex, occurrence, source.Owner, might) };

    private static bool ValidSpellInstructionReflexive(MatchState state, UnitEventContext context, UnitEventAbilitySpec spec,
        string source)
    {
        if (spec.Timing != SpellInstructionReflexiveTiming) return context.SpellInstruction is null;
        if (context.SpellInstruction is not { ExecutionIndex: >= 0 } origin
            || string.IsNullOrWhiteSpace(origin.ParentStackHash) || string.IsNullOrWhiteSpace(origin.Occurrence)
            || !PrintedSpellSources.Value.Contains(context.CardNo) || !state.Seats.ContainsKey(origin.OwnerId)
            || !state.CardObjects.TryGetValue(source, out var physical) || physical.OwnerId != origin.OwnerId) return false;
        return spec.Effect == "GAIN_EXPERIENCE"
            ? origin.ReferencedMight is null && origin.Occurrence.StartsWith("death-", StringComparison.Ordinal)
            : spec.Effect == UnitEventSelfGraveSpellPlayEffect && spec.ReflexiveTargetMaximumMight is { } maximum
                && origin.ReferencedMight is >= 0 && origin.ReferencedMight <= maximum
                && origin.Occurrence == $"instruction:{origin.ParentStackHash}:{origin.ExecutionIndex}";
    }

    private static void CaptureTargetInstructionReflexive(MatchState before, StackItemState item,
        CardBehaviorDefinition behavior, string target, long generation, int? lastKnownMight,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        List<GameEvent> events)
    {
        if (lastKnownMight is null || behavior.UnitEventAbility is not {
                Timing: SpellInstructionReflexiveTiming, Effect: UnitEventSelfGraveSpellPlayEffect,
                ReflexiveTargetMaximumMight: { } maximum }
            || CaptureSpellInstructionSource(item) is not { } source) return;
        // CN359.3.e.13-14: query a surviving replacement at its current location;
        // an object moved off the field by this instruction supplies last-known might.
        var might = cards.TryGetValue(target, out var current) && current.ObjectGeneration == generation
            && IsObjectOnField(zones, target) ? ResolveCurrentFieldUnitPower(before with {
                PlayerZones = zones, CardObjects = cards, ObjectLocations = ReconcileObjectLocations(before.ObjectLocations, zones) }, current)
            : lastKnownMight.Value;
        if (might > maximum) return;
        var context = SpellInstructionContext(source, $"instruction:{MatchStateHasher.HashValue(source.StackItemId)}:{source.ExecutionIndex}", Math.Max(0, might));
        events.Add(WithUnitEvents(new(SpellInstructionReflexiveTiming, "关联指示满足条件，内嵌技能等待确认",
            new Dictionary<string, object?> { ["playerId"] = source.Controller, ["sourceObjectId"] = source.Source.ObjectId }),
            [new(source.Source.ObjectId, source.Controller, context)]));
    }

    private static void QueueDamageInstructionReflexives(MatchState state, CapturedUnitDeath death,
        List<TriggerQueueItemState> queue, List<GameEvent> events)
    {
        var damage = RuleChoices.Value!.AppliedDamages.LastOrDefault(d => d.Target.ObjectId == death.Unit.ObjectId
            && d.Target.Generation == death.Unit.Generation);
        if (damage?.ReflexiveSource is not { } source
            || UnitEventSpec(source.CardNo) is not { Timing: SpellInstructionReflexiveTiming, Effect: "GAIN_EXPERIENCE" }) return;
        var context = SpellInstructionContext(source, death.Id);
        var trigger = UnitEventTrigger(new(source.Source.ObjectId, source.Controller, context), state.Tick,
            RuleChoices.Value.UnitEventOrdinal++, "UNIT_DESTROYED");
        queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
    }

    internal static bool IsUnitEventSelfGraveSpellPlay(StackItemState parent)
        => parent.EffectKind == UnitEventEffect && parent.UnitEvent is { } context && parent.CardNo == context.CardNo
            && SelectedUnitEventSpec(context)?.Effect == UnitEventSelfGraveSpellPlayEffect;

    private static bool TryGetUnitEventSelfGraveSpellDefinition(StackItemState parent, out CardBehaviorDefinition definition)
    {
        definition = null!;
        if (!IsUnitEventSelfGraveSpellPlay(parent)) return false;
        // The printed leading payment has already been paid to confirm this
        // reflexive skill. The specified replay cost replaces base card costs;
        // additional costs and increases remain part of the actual card play.
        definition = new(parent.CardNo, "按已支付费用再次打出此法术", 0, parent.EffectKind, 0, 0,
            EffectPlaySourceZone: "GRAVEYARD", EffectPlayIgnoreBaseMana: true, EffectPlayIgnoreBasePower: true,
            EffectPlayIgnoreAllCosts: false, EffectPlayAllowsAnyCard: true, EffectPlayDestination: "STACK");
        return true;
    }

    private static StackResolutionResult ResolveSelfGraveSpellPlay(MatchState state, StackItemState item)
    {
        if (item.EffectPlayCompleted || item.UnitEvent?.SpellInstruction is not { } origin
            || !state.CardObjects.TryGetValue(item.SourceObjectId, out var card)
            || card.ObjectGeneration != item.UnitEvent.SourceGeneration + 1 || card.OwnerId != origin.OwnerId
            || !state.PlayerZones[origin.OwnerId].Graveyard.Contains(card.ObjectId)) return NoopStackResolutionResult(state);
        var pending = new PendingEffectPlayState($"EFFECT-PLAY:{state.Tick + 1}:{item.StackItemId}", item.ControllerId, item,
            "GRAVEYARD", new Dictionary<string, long> { [card.ObjectId] = card.ObjectGeneration },
            true, true, 0, "STACK", false) { SourcePlayerId = origin.OwnerId == item.ControllerId ? null : origin.OwnerId };
        return NoopStackResolutionResult(state) with { PendingEffectPlay = pending };
    }

    internal static bool ValidSelfGraveSpellPlay(MatchState state, PendingEffectPlayState pending)
    {
        var parent = pending.Parent;
        return IsUnitEventSelfGraveSpellPlay(parent) && parent.UnitEvent!.SpellInstruction is { } origin
            && ValidUnitEvent(state, parent.UnitEvent, parent.EffectKind, parent.SourceObjectId, parent.CardNo, parent.ControllerId)
            && parent.TriggerCost is not null && ValidTriggerCostReceipt(state, parent) && !parent.EffectPlayCompleted
            && parent.TargetObjectIds.Count == 0 && pending.PlayerId == parent.ControllerId
            && pending.SourcePlayerId == (origin.OwnerId == parent.ControllerId ? null : origin.OwnerId)
            && pending.SourceZone == "GRAVEYARD" && pending.IgnoreBaseMana && pending.IgnoreBasePower
            && !pending.IgnoreAllCosts && pending.ManaReduction == 0 && !pending.Optional && pending.DestinationPolicy == "STACK"
            && pending.SourceOwners is null && pending.SourceReplay is null && pending.RevealedHand is null
            && pending.ViewedCardIds is null && pending.MaximumUnitPower is null
            && pending.OriginalLocation is null && pending.OriginalBattlefield is null
            && pending.Sources.Count == 1 && pending.Sources.TryGetValue(parent.SourceObjectId, out var generation)
            && generation == parent.UnitEvent.SourceGeneration + 1
            && state.CardObjects.TryGetValue(parent.SourceObjectId, out var physical) && physical.ObjectGeneration == generation
            && physical.CardNo == parent.CardNo && physical.OwnerId == origin.OwnerId
            && state.PlayerZones[origin.OwnerId].Graveyard.Contains(physical.ObjectId);
    }
}

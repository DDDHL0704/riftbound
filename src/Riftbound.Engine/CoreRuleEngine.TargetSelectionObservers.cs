using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TargetSelectionOrigin(string Actor, string CauseId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Spell = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool EquipmentUnit = false);

public sealed partial record UnitEventAbilitySpec
{
    public bool TargetSelectionRequiresSpell { get; init; }
    public bool TargetSelectionRequiresSource { get; init; }
}

public sealed partial class CoreRuleEngine
{
    private const string FriendlyUnitTargetedTiming = UnitEventTimings.FriendlyUnitTargeted;
    private const string ReadySelectedEventUnit = "READY_SELECTED_EVENT_UNIT";
    private const string TargetSelectionCaptureKey = "internalTargetSelectionObservers";

    private static readonly Lazy<IReadOnlyDictionary<string, UnitEventAbilitySpec>> TargetSelectionLegendEvents = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards
            .Where(card => card.CardCategory == "legendary")
            .Select(card => (card.CardNo, Spec: SourceUnitPlayedTriggerSpecRules.TriggersForCard(card.CardNo)
                .FirstOrDefault(IsFriendlyUnitTargetedReadyTrigger)))
            .Where(entry => entry.Spec is not null)
            .ToDictionary(entry => entry.CardNo, entry => new UnitEventAbilitySpec(FriendlyUnitTargetedTiming,
                ReadySelectedEventUnit, LeadingSourceExhaust: true, LeadingPowerCost: entry.Spec!.PowerCost!.Value,
                Optional: true, SourceInLegendZone: true), StringComparer.Ordinal));

    private static bool IsFriendlyUnitTargetedReadyTrigger(TriggerSpec spec)
        => spec.Kind == TriggerKinds.ControllerTargetedFriendlyUnitPayExhaustReady
            && spec.Timing == TriggerTimings.ControllerTargetedFriendlyUnit
            && spec.TargetScope == TriggerTargetScopes.SelectedFriendlyEventUnit
            && spec.ExhaustsSource == true && spec.Optional == true && spec.PowerCost is > 0
            && spec.UnitReadyCount == 1 && spec.ManaCost.GetValueOrDefault() == 0;

    private static UnitEventObservation[] CaptureFriendlyUnitTargetedObservers(
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        CardObjectState unit, string actor, string cause, bool spell = false)
    {
        if (!IsFaceUpNonStandbyUnit(unit) || !IsObjectOnField(zones, unit.ObjectId)
            || EffectiveFieldControllerId(zones, unit.ObjectId, unit) != actor) return [];
        var sources = PublicStaticAuraSourceObjectIds(zones).Where(id => cards.TryGetValue(id, out var card)
            && EffectiveFieldControllerId(zones, id, card) == actor);
        return CaptureUnitEvents(zones, cards, sources, FriendlyUnitTargetedTiming)
            .Where(observation => UnitEventSpec(observation.Context.CardNo) is { } spec
                && (!spec.TargetSelectionRequiresSpell || spell)
                && (!spec.TargetSelectionRequiresSource || observation.Source == unit.ObjectId))
            .Select(observation => observation with { Context = observation.Context with {
                Other = new(new(unit.ObjectId, unit.ObjectGeneration), unit.CardNo!,
                    unit.CopyFaceTransitions?.Count ?? 0, unit.TokenFactoryCardNo),
                TargetSelection = new(actor, cause, spell, unit.Tags.Contains(CardObjectTags.EquipmentCard)) } }).ToArray();
    }

    private static void QueueTargetSelectionObservers(MatchState state, IEnumerable<UnitEventObservation> observations,
        List<TriggerQueueItemState> queue, List<GameEvent> events, ref int ordinal)
    {
        if (state.Status != MatchStatuses.InProgress) return;
        var tick = RuleChoices.Value?.CompletionTick ?? state.Tick;
        foreach (var observation in observations)
        {
            TriggerQueueItemState trigger;
            do { trigger = UnitEventTrigger(observation, tick, ordinal++, "TARGET_CONFIRMED"); }
            while (queue.Any(item => item.TriggerId == trigger.TriggerId)
                || state.StackItems.Any(item => item.StackItemId == "ordered-" + trigger.TriggerId));
            queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
        }
    }

    private static bool ValidTargetSelectionContext(MatchState state, UnitEventContext context,
        UnitEventAbilitySpec spec, string source, string? controller)
        => spec.Timing != FriendlyUnitTargetedTiming ? context.TargetSelection is null
            : (spec.Effect == ReadySelectedEventUnit && spec.SourceInLegendZone
                || spec.Effect == "DRAW" && spec.Amount > 0)
                && context.TargetSelection is { } selection && !string.IsNullOrWhiteSpace(selection.Actor)
                && state.Seats.ContainsKey(selection.Actor)
                && (controller is null || controller == selection.Actor) && !string.IsNullOrWhiteSpace(selection.CauseId)
                && (!spec.TargetSelectionRequiresSpell || selection.Spell)
                && context.Other is { Binding.Generation: >= 0, FaceRevision: >= 0 } other
                && (!spec.TargetSelectionRequiresSource
                    || other.Binding.ObjectId == source && other.Binding.Generation == context.SourceGeneration)
                && !string.IsNullOrWhiteSpace(other.CardNo) && TryPredictEnteredFace(other.CardNo, out var face)
                && (face.Tags.Contains(CardObjectTags.UnitCard)
                    || selection.EquipmentUnit && face.Tags.Contains(CardObjectTags.EquipmentCard))
                && PredictBindingMatches(state, other.Binding, other.CardNo, other.FaceRevision,
                    context.CapturedTick, other.TokenFactoryCardNo);

    private static StackResolutionResult ResolveSelectedEventUnitReady(MatchState state, StackItemState item)
    {
        if (!ValidUnitEvent(state, item.UnitEvent, item.EffectKind, item.SourceObjectId, item.CardNo, item.ControllerId)
            || !ValidUnitEventTargets(item)) throw new InvalidOperationException("Invalid captured target-selection trigger.");
        if (item.TriggerCost is null) return NoopStackResolutionResult(state);
        var context = item.UnitEvent!; var binding = context.Other!.Binding;
        var cards = state.CardObjects.ToDictionary(); var events = new List<GameEvent>();
        // "That unit" refers to the original object. Friendship was a trigger
        // condition, and this instruction does not choose or tax it again.
        if (cards.TryGetValue(binding.ObjectId, out var unit) && unit.ObjectGeneration == binding.Generation
            && IsFaceUpNonStandbyUnit(unit) && IsObjectOnField(state.PlayerZones, unit.ObjectId))
        {
            var behavior = new CardBehaviorDefinition(context.CardNo, "活跃触发时被选中的单位", 0,
                item.EffectKind, 0, 0, ReadiesTarget: true);
            cards[unit.ObjectId] = ApplyReadyState(unit, behavior, item, unit.ObjectId, out var ready, state.PlayerZones, cards);
            if (ready is not null) events.Add(ready);
        }
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}

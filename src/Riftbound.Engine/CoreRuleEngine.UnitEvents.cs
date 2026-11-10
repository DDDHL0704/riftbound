using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record UnitEventContext(string CardNo, long SourceGeneration, long FaceRevision, long CapturedTick,
    string Timing, string? TokenFactoryCardNo = null, RuleTextInstanceBinding? Origin = null,
    ObjectLocationState? Here = null, int? LastKnownPower = null, UnitEventOtherObject? Other = null,
    BurnedUnitFact? BurnedUnit = null, long? ReflexiveTick = null, string? Mode = null, IReadOnlyList<UnitEventOtherObject>? Attackers = null, MightReductionFact? MightReduction = null, ObjectBinding? DeploymentBattlefield = null, IReadOnlyList<UnitEventOtherObject>? ExhaustedUnits = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PlayedUnitRuneObservation? PlayedUnitRunes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RevealedRuneFact? RevealedRune = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TargetSelectionOrigin? TargetSelection = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FriendlyUnitActionOrigin? FriendlyUnitAction { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ObjectBinding? BattleTieBattlefield { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourcePlayDestinationZone { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool SourceOptionalExperiencePaid { get; init; }
}
public sealed record UnitEventOtherObject(ObjectBinding Binding, string CardNo, long FaceRevision, string? TokenFactoryCardNo = null);
public sealed record BurnedUnitFact(ObjectBinding Binding, string CardNo, long FaceRevision, int Power);

public sealed partial class CoreRuleEngine
{
    internal const string UnitEventEffect = "CAPTURED_UNIT_EVENT_ABILITY";
    internal const string TookDamageThisTurn = "TOOK_DAMAGE_THIS_TURN";
    private const string UnitEventCaptureKey = "unitEventObservations";
    private sealed record UnitEventObservation(string Source, string Controller, UnitEventContext Context,
        string InstructionEventKind = "UNIT_EXHAUSTED", string? PlayedCardPlayId = null);
    internal static UnitEventAbilitySpec? UnitEventSpec(string? no)
        => no is null ? null
            : (CardBehaviorRegistry.TryGetByCardNo(no, out var card) ? card.UnitEventAbility : null)
                ?? TargetSelectionLegendEvents.Value.GetValueOrDefault(no)
                ?? MainPhaseLegendEventSpec(no)
                ?? DeploymentPlayLegends.Value.GetValueOrDefault(no)
                ?? PlayedUnitRuneSpecs.Value.GetValueOrDefault(no)
                ?? ArmamentUnitEventRules.Spec(no)
                ?? ConquestInstructionSpec(no);

    // Independent printed skills may listen to different events. The captured
    // declared timing identifies the registered skill, including copied text.
    internal static IEnumerable<UnitEventAbilitySpec> UnitEventSpecs(string? no)
    {
        if (UnitEventSpec(no) is { } primary) yield return primary;
        if (no is not null && CardBehaviorRegistry.TryGetByCardNo(no, out var card))
            foreach (var additional in card.AdditionalUnitEventAbilities) yield return additional;
    }

    internal static UnitEventAbilitySpec? UnitEventSpecForContext(UnitEventContext context)
        => context.Timing == AgilePlayTiming ? AgilePlaySpec
            : UnitEventSpecs(context.CardNo).SingleOrDefault(spec => spec.Timing == context.Timing);

    private static bool UnitEventTimingMatches(string? declared, string timing)
        => declared?.Split("_OR_", StringSplitOptions.None).Contains(timing, StringComparer.Ordinal) == true;

    private static UnitEventObservation[] CaptureUnitEvents(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, IEnumerable<string> sources, string timing)
        => sources.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Where(id => cards.TryGetValue(id, out var card)
                && !card.IsFaceDown && ((IsFaceUpNonStandbyUnit(card) || card.Tags.Contains(CardObjectTags.EquipmentCard))
                    && IsObjectOnField(zones, id)
                    || UnitEventSpec(card.CardNo) is { SourceInLegendZone: true }
                        && zones.Values.Any(z => z.LegendZone.Contains(id))
                    || timing == "FRIENDLY_UNIT_PLAYED" && PlayedUnitRuneSpecs.Value.ContainsKey(card.CardNo ?? "")
                        && zones.Values.Any(z => z.LegendZone.Contains(id))
                    || timing == "MAIN" && MainPhaseLegendEventSpec(card.CardNo) is not null
                        && card.Tags.Contains("CARD_TYPE:LEGEND") && zones.Values.Any(z => z.LegendZone.Contains(id))))
            .Select(id => cards[id]).SelectMany(card => AttachedRuleTextRules.Instances(zones, cards, card)
                .Concat(ArmamentUnitEventRules.Instances(zones, cards, card))
                .SelectMany(i => UnitEventSpecs(i.CardNo).Where(spec => spec.GrantedByArmament == (i.Origin?.ArmamentGranted == true)
                    && (UnitEventTimingMatches(spec.Timing, timing)
                        || spec.Timing == "PLAY_OR_EXHAUST" && timing == "BECAME_EXHAUSTED")
                    && (!spec.RequiresEmpowered || card.IsEmpowered)
                    // CN383.2.a.1: an immediately following condition is checked
                    // at the triggering event; later responses do not recheck it.
                    && (spec.MinimumControlledEquipment == 0 || CountControlledPublicFieldEquipmentObjects(zones, cards,
                        EffectiveFieldControllerId(zones, card.ObjectId, card)) >= spec.MinimumControlledEquipment)
                    && (!spec.RequiresNoDamageThisTurn || !card.UntilEndOfTurnEffects.Contains(TookDamageThisTurn)))
                .Select(spec => new UnitEventObservation(card.ObjectId, EffectiveFieldControllerId(zones, card.ObjectId, card),
                    new(i.CardNo, card.ObjectGeneration, card.CopyFaceTransitions?.Count ?? 0, 0, spec.Timing,
                        card.TokenFactoryCardNo, i.Origin))))).ToArray();

    private static GameEvent WithUnitEvents(GameEvent ev, IReadOnlyList<UnitEventObservation> observations)
        => observations.Count == 0 ? ev : ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
            [UnitEventCaptureKey] = ((ev.Payload.GetValueOrDefault(UnitEventCaptureKey) as UnitEventObservation[]) ?? [])
                .Concat(observations).ToArray() } };

    private static GameEvent CapturePhaseCardEvents(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, string player, string timing)
        => WithUnitEvents(ev, CaptureUnitEvents(zones, cards, cards.Values.Where(c =>
            EffectiveFieldControllerId(zones, c.ObjectId, c) == player).Select(c => c.ObjectId), timing));

    private static MatchState QueueBeginningUnitEventAbilities(MatchState state, List<GameEvent> events)
    {
        var observations = CaptureUnitEvents(state.PlayerZones, state.CardObjects, state.CardObjects.Values
            .Where(c => EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == state.TurnPlayerId)
            .Select(c => c.ObjectId), "START").Where(o => BeginningUnitEventConditionSatisfied(state, o)).ToArray();
        var queue = state.TriggerQueue.ToList();
        foreach (var (observation, ordinal) in observations.Select((o, i) => (o, i)))
        {
            var trigger = UnitEventTrigger(observation, state.Tick, ordinal, "TURN_START_BEGAN");
            queue.Add(trigger);
            events.Add(RedactQueuedUnitEventSource(state, trigger, BuildTriggerQueuedEvent(trigger)));
        }
        return state with { TriggerQueue = queue };
    }

    private static GameEvent CaptureOtherUnitPlayed(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState entered, string player,
        IReadOnlyList<string> turnEffects, StackItemState? playedItem = null)
    {
        if (ev.Kind is not ("UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD" or "UNIT_TOKEN_CREATED")) return ev;
        var destinationZone = ev.Payload.GetValueOrDefault("destinationZone") as string ?? "BASE";
        // Some token instructions append the batch to its zone after emitting
        // each entry event. Include the entering object in this comparison.
        if (!IsObjectOnField(zones, entered.ObjectId))
        {
            var own = zones[player];
            zones = new Dictionary<string, PlayerZones>(zones) { [player] = destinationZone == "BATTLEFIELD"
                ? own with { Battlefields = [..own.Battlefields, entered.ObjectId] }
                : own with { Base = [..own.Base, entered.ObjectId] } };
        }
        var friendly = cards.Values.Where(c => EffectiveFieldControllerId(zones, c.ObjectId, c) == player)
            .Select(c => c.ObjectId).ToArray();
        // Destination conditions belong to the play event, before any response
        // can move the source to a different location (CN 383.2.c).
        var observations = CaptureUnitEvents(zones, cards, friendly.Where(id => id != entered.ObjectId), "ANOTHER_UNIT_PLAYED")
            .Concat(CaptureUnitEvents(zones, cards, friendly, "FRIENDLY_UNIT_PLAYED"))
            .Concat(CaptureUnitEvents(zones, cards, [entered.ObjectId], "PLAY")
                .Where(o => !OfficialPrintedTimingRules.HasPlayTrigger(o.Context.CardNo)
                    || OfficialPrintedTimingRules.HasPlayTrigger(o.Context.CardNo, destinationZone == "BATTLEFIELD"))).ToArray();
        observations = CaptureFriendlyPlayConditions(observations);
        if (observations.Any(o => UnitEventSpec(o.Context.CardNo)!.RequiresPlayedUnitStrongest))
        {
            // Compare at the entry instruction, including current auras. Later
            // responses cannot remove or create the earned trigger (CN383.2).
            var origin = RuleChoices.Value!.ResolutionState ?? RuleChoices.Value.Origin;
            var locations = ReconcileObjectLocations(origin.ObjectLocations, zones);
            var field = ev.Payload.GetValueOrDefault("battlefieldObjectId") as string
                ?? ev.Payload.GetValueOrDefault("battlefieldId") as string;
            if (string.IsNullOrEmpty(field) && ev.Payload.GetValueOrDefault("destination") is string destination)
                field = PreciseBattlefieldLocationObjectId(NormalizeMoveUnitLocation(destination));
            if (string.IsNullOrEmpty(field) && ev.Payload.GetValueOrDefault("sourceObjectId") is string source
                && origin.BattlefieldStates.ContainsKey(source)) field = source;
            locations[entered.ObjectId] = new(player, destinationZone, destinationZone == "BATTLEFIELD" ? field : null);
            var current = origin with { PlayerZones = zones, CardObjects = cards,
                ObjectLocations = locations, UntilEndOfTurnEffects = turnEffects };
            var power = ResolveCurrentFieldUnitPower(current, entered);
            var strongest = cards.Values.Where(c => c.ObjectId != entered.ObjectId && IsFaceUpNonStandbyUnit(c)
                    && IsObjectOnField(zones, c.ObjectId)).All(c => ResolveCurrentFieldUnitPower(current, c) < power);
            observations = observations.Where(o => !UnitEventSpec(o.Context.CardNo)!.RequiresPlayedUnitStrongest || strongest).ToArray();
        }
        if (observations.Any(o => UnitEventSpec(o.Context.CardNo)!.RequiredOpponentVictoryDistance > 0))
        {
            var origin = RuleChoices.Value!.ResolutionState ?? RuleChoices.Value.Origin;
            var current = origin with { PlayerZones = zones, CardObjects = cards };
            observations = observations.Where(o => UnitEventSpec(o.Context.CardNo)!.RequiredOpponentVictoryDistance <= 0
                || OpponentWithinWinningScoreDistance(current, o.Controller,
                    UnitEventSpec(o.Context.CardNo)!.RequiredOpponentVictoryDistance)).ToArray();
        }
        // CN185.1 / 420.4: a copied Image remains a token. Mutable face tags
        // cannot establish or remove that intrinsic token origin.
        observations = observations.Where(o => !UnitEventSpec(o.Context.CardNo)!.RequiresPlayedUnitToken
            || TokenObjectRules.IsToken(entered)).ToArray();
        observations = CapturePlayedUnitRunes(ev, zones, cards, entered, player, turnEffects, observations);
        observations = CaptureConditionalSourcePlayDestination(entered.ObjectId, destinationZone, observations);
        observations = CaptureSourcePlayConditions(entered, player, observations);
        observations = observations.Select(observation => observation.Source == entered.ObjectId
            && UnitEventSpec(observation.Context.CardNo)?.ExemptsControllerIfExperiencePaid == true
            && playedItem is not null
            && CardBehaviorRegistry.TryGetByCardNo(observation.Context.CardNo, out var paidBehavior)
                ? observation with { Context = observation.Context with {
                    SourceOptionalExperiencePaid = OptionalExperiencePaymentSelected(paidBehavior, playedItem.OptionalCosts) } }
                : observation).ToArray();
        return WithUnitEvents(ev, observations);
    }

    private static GameEvent CaptureEquipmentPlayed(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState entered, string player)
        => WithUnitEvents(ev, CaptureSourcePlayConditions(entered, player,
            CaptureUnitEvents(zones, cards, cards.Values.Where(c =>
            EffectiveFieldControllerId(zones, c.ObjectId, c) != player).Select(c => c.ObjectId), "OPPONENT_EQUIPMENT_PLAYED")
            .Select(o => o with { Context = o.Context with { Other = new(new(entered.ObjectId, entered.ObjectGeneration), entered.CardNo!,
                entered.CopyFaceTransitions?.Count ?? 0, entered.TokenFactoryCardNo) } }).Concat(
                    CaptureUnitEvents(zones, cards, [entered.ObjectId], "PLAY").Select(o =>
                        o.Context.Timing == "PLAY_OR_MOVE" ? o with { Context = o.Context with {
                            Here = new(player, (string)ev.Payload["destinationZone"]!, ev.Payload.GetValueOrDefault("battlefieldObjectId") as string) } } : o))
                .Concat(CaptureDeploymentPlayedLegends(ev, zones, cards, entered, player)).ToArray())
            .Concat(CaptureGrantedAgilePlay(zones, cards, entered, player)).ToArray());

    private static void CaptureSelfDiscardEvents(IReadOnlyDictionary<string, CardObjectState> cards, string player,
        string eventKind, IReadOnlyList<string> discarded, List<GameEvent> events)
    {
        var observations = discarded.Distinct(StringComparer.Ordinal).Where(id => cards.TryGetValue(id, out var card)
            && UnitEventTimingMatches(UnitEventSpec(card.CardNo)?.Timing, "SELF_DISCARDED")).Select(id => cards[id])
            .Select(card => new UnitEventObservation(card.ObjectId, player, new(card.CardNo!, card.ObjectGeneration,
                card.CopyFaceTransitions?.Count ?? 0, 0, UnitEventSpec(card.CardNo)!.Timing, card.TokenFactoryCardNo))).ToArray();
        var index = events.FindLastIndex(e => e.Kind == eventKind);
        if (observations.Length > 0 && index >= 0) events[index] = WithUnitEvents(events[index], observations);
    }

    private static UnitEventObservation[] CaptureFirstCardObservers(IReadOnlyDictionary<string, PlayerZones>? zones,
        IReadOnlyDictionary<string, CardObjectState>? cards, string player)
        => zones is null || cards is null ? [] : CaptureUnitEvents(zones, cards,
            zones.GetValueOrDefault(player)?.Battlefields ?? [], "FIRST_CARD_PLAYED")
                .Where(o => o.Controller == player).ToArray();

    // Record at the instruction, so ready-then-exhaust and departure later in
    // the same command cannot erase or invent an earned trigger (CN383.2.c).
    private static GameEvent CaptureUnitReadied(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, string unit, bool wasExhausted)
    {
        if (!wasExhausted) return ev;
        if (cards.TryGetValue(unit, out var readied) && ev.Payload.GetValueOrDefault("playerId") is string actor)
            return CaptureSelfReady(ev, readied with { IsExhausted = true }, actor, zones, cards);
        return WithUnitEvents(ev, CaptureUnitEvents(zones, cards, [unit], "BECAME_READY"));
    }

    // Capture the transition at the instruction, before later ready/departure
    // instructions can erase it. Entry initialization and legality previews do
    // not call this operation. The command frame is rebuilt on choice replay.
    private static CardObjectState ExhaustForInstruction(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState source)
    {
        if (!source.IsExhausted && RuleChoices.Value is { } frame && IsObjectOnField(zones, source.ObjectId))
        {
            IReadOnlyDictionary<string, CardObjectState> current = cards.TryGetValue(source.ObjectId, out var old) && ReferenceEquals(old, source)
                ? cards : new Dictionary<string, CardObjectState>(cards) { [source.ObjectId] = source };
            frame.InstructionUnitEvents.AddRange(CaptureUnitEvents(zones, current, [source.ObjectId], "BECAME_EXHAUSTED"));
            if (IsFaceUpNonStandbyUnit(source))
            {
                var controller = EffectiveFieldControllerId(zones, source.ObjectId, source);
                frame.InstructionUnitEvents.AddRange(CaptureUnitEvents(zones, current,
                    current.Values.Where(card => card.ObjectId != source.ObjectId
                        && EffectiveFieldControllerId(zones, card.ObjectId, card) == controller).Select(card => card.ObjectId),
                    "ANOTHER_FRIENDLY_UNIT_EXHAUSTED"));
            }
        }
        return source with { IsExhausted = true };
    }

    // CN383.2.c.3: capture after replacement selection but before the object
    // changes zones. Command before/after comparisons lose leave-and-reenter.
    private static void CaptureFieldDeparture(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, string id)
    {
        if (RuleChoices.Value is { } frame)
            frame.InstructionUnitEvents.AddRange(CaptureUnitEvents(zones, cards, [id], "LEAVE")
                .Select(o => o with { InstructionEventKind = "OBJECT_LEFT_FIELD" }));
    }

    private static TriggerQueueItemState[] CaptureAttackUnitEvents(MatchState state, IEnumerable<string> attackers,
        string timing, string eventKind, long? capturedTick = null)
        => CaptureUnitEvents(state.PlayerZones, state.CardObjects, attackers, "ATTACK")
            .Where(o => UnitEventCaptureConditionSatisfied(state, o))
            .Concat(CaptureFriendlyAttackHere(state, attackers))
            .Concat(CaptureSoloRoleObservers(state, attackers, "ATTACK")).Select(o => CaptureUnitEventFacts(state, o))
            .Select((o, i) => UnitEventTrigger(o, capturedTick ?? state.Tick + 1, i, eventKind, timing)).ToArray();

    private static TriggerQueueItemState[] CaptureRoleUnitEvents(MatchState state, IEnumerable<string> units,
        string role, string timing, string eventKind, long? capturedTick = null)
        => CaptureUnitEvents(state.PlayerZones, state.CardObjects, units, role)
            .Where(o => UnitEventCaptureConditionSatisfied(state, o))
            .Concat(CaptureSoloRoleObservers(state, units, role)).Select(o => CaptureUnitEventFacts(state, o)).Select((o, i) =>
            UnitEventTrigger(o, capturedTick ?? state.Tick + 1, i, eventKind, timing)).ToArray();

    private static UnitEventObservation CaptureUnitEventFacts(MatchState state, UnitEventObservation o)
    {
        o = CaptureRequiredUnitDestruction(state, o);
        return UnitEventSpecForContext(o.Context)?.Effect is "MATCH_POWER_THEN_BUFF" or "DESTROY_CHEAPER_EQUIPMENT" or "MOVE_ENEMIES_TO_BASE"
            ? o with { Context = o.Context with {
                LastKnownPower = UnitEventSpecForContext(o.Context)!.Effect == "MOVE_ENEMIES_TO_BASE" ? null
                    : ResolveCurrentFieldUnitPower(state, state.CardObjects[o.Source]),
                Here = UnitEventSpecForContext(o.Context)!.Effect is "MATCH_POWER_THEN_BUFF" or "MOVE_ENEMIES_TO_BASE" ? state.ObjectLocations[o.Source] : null } } : o;
    }

    private static TriggerQueueItemState[] CaptureConquestUnitEvents(MatchState state, IEnumerable<string> units, long tick,
        string player, string battlefield)
    {
        var conquerors = units.ToHashSet(StringComparer.Ordinal);
        var observers = state.CardObjects.Values.Where(c => !conquerors.Contains(c.ObjectId)
            && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == player
            && state.ObjectLocations.TryGetValue(c.ObjectId, out var at) && at.Zone == "BATTLEFIELD"
            && at.BattlefieldObjectId == battlefield).Select(c => c.ObjectId);
        var observations = CaptureConquestEventObservations(state, conquerors)
            .Concat(CaptureUnitEvents(state.PlayerZones, state.CardObjects, observers, "CONQUEST")
                .Where(o => o.Context.Timing == "PLAY_OR_CONQUEST"))
            .Select(o => CaptureUnitEventFacts(state, o)).ToArray();
        var repeats = ResolveUnitConquestEffectRepeatCount(state.PlayerZones, state.CardObjects, state.ObjectLocations,
            player, battlefield, "BATTLEFIELD_CONQUERED");
        return observations.SelectMany(o => Enumerable.Range(0, repeats).Select(_ => o))
            .Select((o, i) => UnitEventTrigger(o, tick, i, "BATTLEFIELD_CONQUERED")).ToArray();
    }

    private static ResolutionResult RecordUnitEventSourceFacts(MatchState before, ResolutionResult result)
    {
        UnitEventContext? Update(string id, UnitEventContext? c) {
            // Current-here token instructions read the source directly when
            // resolving. Their original movement capture remains immutable.
            if (c is null || SelectedUnitEventSpec(c) is { CreatesUnitTokenHere: true } or { RequiresMoveToBattlefield: true } or { PreservesMovementCapture: true }
                || c.LastKnownPower is null && c.Here is null) return c;
            var current = CombatMightSourceLocation(result.State, id, c.SourceGeneration) is not null ? result.State
                : CombatMightSourceLocation(before, id, c.SourceGeneration) is not null ? before : null;
            return current is null ? c : c with { LastKnownPower = c.LastKnownPower is null ? null : ResolveCurrentFieldUnitPower(current, current.CardObjects[id]),
                Here = c.Here is null ? null : current.ObjectLocations[id] };
        }
        var next = result.State with {
            StackItems = result.State.StackItems.Select(i => i with { UnitEvent = Update(i.SourceObjectId, i.UnitEvent) }).ToArray(),
            TriggerQueue = result.State.TriggerQueue.Select(t => t with { UnitEvent = Update(t.SourceObjectId, t.UnitEvent) }).ToArray() };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static string UnitEventSourceIdentity(string source, string timing)
        => MatchStateHasher.HashValue(new { source, timing });

    private static TriggerQueueItemState UnitEventTrigger(UnitEventObservation observation, long tick, int ordinal,
        string eventKind, string? timing = null)
    {
        // A synchronous cost choice replays the original command. Its earned
        // skills are born in the final accepted completion, after resource children.
        tick = Math.Max(tick, RuleChoices.Value?.CompletionTick ?? tick);
        var captured = observation.Context;
        if (captured.Origin is { ArmamentGranted: true } armament && captured.DelayedReceipt is null)
            captured = captured with { Origin = armament with { CapturedTick = tick } };
        // The public skill may outlive its source's stay in a public zone.
        // Its stack and choice ids cannot contain the source's transport id.
        var id = $"unit-event-{tick}-{ordinal}-{UnitEventSourceIdentity(observation.Source, captured.Timing)}";
        if (captured.Origin is { ArmamentGranted: true })
            id = "armament-unit-event-" + MatchStateHasher.HashValue(new {
                tick, ordinal, observation.Source, captured.CardNo, captured.Origin });
        if (captured.Timing == EquipmentSkillCompletedTiming)
            captured = captured with { EquipmentSkillTriggerHash = MatchStateHasher.HashValue(id) };
        if (UnitEventSpecForContext(captured) is { Effect: "INSIGHT" } insight)
            return new(id, observation.Controller, observation.Source, InsightTriggerEffect, eventKind, timing) {
                SourceCardNo = captured.CardNo,
                InsightContext = new(captured.CardNo, observation.Controller, insight.Amount, captured.SourceGeneration, "EXHAUSTED",
                    Origin: captured.Origin, SourceTokenFactoryCardNo: captured.TokenFactoryCardNo,
                    SourceFaceRevision: captured.FaceRevision, SourceCapturedTick: tick) };
        return new(id, observation.Controller, observation.Source, UnitEventEffect, eventKind, timing) {
            UnitEvent = captured with { CapturedTick = captured.CapturedTick > 0 ? captured.CapturedTick : tick },
            SourceCardNo = captured.CardNo };
    }

    private static GameEvent RedactQueuedUnitEventSource(MatchState state, TriggerQueueItemState trigger, GameEvent queued)
    {
        if (trigger.UnitEvent is null) return queued;
        var source = ResolutionResult.VisibleObjectIdForViewer(state, trigger.SourceObjectId, ProjectionViewers.Spectator);
        if (source == trigger.SourceObjectId) return queued;
        // Keep the original source authoritative without exposing its current
        // private identity in a newly born public queue event.
        return queued with { Payload = new Dictionary<string, object?>(queued.Payload) {
            ["sourceObjectId"] = source,
            ["timingSourceHash"] = MatchStateHasher.HashValue(trigger.SourceObjectId) } };
    }

    private static ResolutionResult QueueUnitEvents(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var queue = result.State.TriggerQueue.ToList(); var events = result.Events.ToList();
        var ordinal = RuleChoices.Value?.UnitEventOrdinal ?? 0;
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i))) {
            if (ev.Payload.GetValueOrDefault(UnitEventCaptureKey) is not UnitEventObservation[] observations) continue;
            events[index] = ev with { Payload = ev.Payload.Where(p => p.Key != UnitEventCaptureKey).ToDictionary(p => p.Key, p => p.Value) };
            foreach (var observation in observations) {
                var timing = ev.Kind == "BATTLE_CLOSED" ? null : before.StackItems.LastOrDefault()?.TimingContext;
                if (timing is not (TimingStates.SpellDuelOpen or TimingStates.NeutralClosed))
                    timing = before.SpellDuelState.IsActive ? TimingStates.SpellDuelOpen : null;
                var captured = observation;
                var enteredId = ev.Payload.GetValueOrDefault("unitObjectId") as string ?? ev.Payload.GetValueOrDefault("tokenObjectId") as string;
                if (((UnitEventTimingMatches(captured.Context.Timing, "PLAY") || captured.Context.Timing == AgilePlayTiming)
                        && (ev.Kind is "EQUIPMENT_PLAYED_TO_BASE" or "EQUIPMENT_PLAYED_TO_BATTLEFIELD" || captured.Source == enteredId)
                        || captured.Context.Timing == "FRIENDLY_UNIT_PLAYED" && captured.Source == enteredId)
                    && before.CardObjects.TryGetValue(captured.Source, out var entering) && !IsObjectOnField(before.PlayerZones, entering.ObjectId))
                    captured = captured with { Context = captured.Context with {
                        SourceGeneration = Math.Max(captured.Context.SourceGeneration, entering.ObjectGeneration + 1) } };
                if (captured.Context.Other is { } other && before.CardObjects.TryGetValue(other.Binding.ObjectId, out var prior)
                    && !IsObjectOnField(before.PlayerZones, prior.ObjectId))
                    captured = captured with { Context = captured.Context with { Other = other with {
                        Binding = other.Binding with { Generation = Math.Max(other.Binding.Generation, prior.ObjectGeneration + 1) } } } };
                captured = captured with { Context = NormalizePlayedUnitRuneFrame(before, captured.Context) };
                var eventKind = captured.Context.Timing == EnemyMightReducedHere ? "STATUS_EFFECT_APPLIED"
                    : ev.Kind == CapturedMovementEvent
                        ? captured.Context.Here?.Zone == "BASE" ? "UNIT_MOVED_TO_BASE" : "UNIT_MOVED_TO_BATTLEFIELD"
                        : ev.Kind;
                var trigger = UnitEventTrigger(captured, result.State.Tick, ordinal++, eventKind, timing);
                queue.Add(trigger);
                events.Add(RedactQueuedUnitEventSource(result.State, trigger, BuildTriggerQueuedEvent(trigger)));
            }
        }
        if (RuleChoices.Value is { } frame) frame.UnitEventOrdinal = ordinal;
        if (events.Count == result.Events.Count) return RecordFirstConquestUses(QueueInstructionUnitEvents(before, result));
        var next = result.State with { TriggerQueue = queue };
        return RecordFirstConquestUses(QueueInstructionUnitEvents(before, result with { State = next, Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) }));
    }

    private static ResolutionResult QueueInstructionUnitEvents(MatchState before, ResolutionResult result, int start = 0)
    {
        if (!result.Accepted || RuleChoices.Value is not { } frame || frame.InstructionUnitEvents.Count <= start) return result;
        var queue = result.State.TriggerQueue.ToList(); var events = result.Events.ToList();
        foreach (var observation in frame.InstructionUnitEvents.Skip(start)) {
            var trigger = UnitEventTrigger(observation, result.State.Tick, frame.UnitEventOrdinal++, observation.InstructionEventKind,
                before.SpellDuelState.IsActive ? TimingStates.SpellDuelOpen : before.StackItems.LastOrDefault()?.TimingContext);
            queue.Add(trigger);
            events.Add(RedactQueuedUnitEventSource(result.State, trigger, BuildTriggerQueuedEvent(trigger)));
        }
        frame.InstructionUnitEvents.RemoveRange(start, frame.InstructionUnitEvents.Count - start);
        var next = result.State with { TriggerQueue = queue };
        return result with { State = next, Events = events, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static bool ValidUnitEvent(MatchState state, UnitEventContext? c, string effect, string source, string? no = null, string? controller = null)
        => c is null ? effect != UnitEventEffect : effect == UnitEventEffect && (no is null || no == c.CardNo)
            && UnitEventSpecForContext(c) is { } spec && c.SourceGeneration >= 0 && c.FaceRevision >= 0
            && spec.GrantedByArmament == (c.Origin?.ArmamentGranted == true)
            && (!spec.GrantedByArmament || c.Origin!.CapturedTick == c.CapturedTick)
            && (spec.FirstConquestOfTurn ? c.FirstConquestTurn == state.TurnNumber : c.FirstConquestTurn is null)
            && (c.Mode is null || spec.Modes?.Any(m => m.Id == c.Mode) == true)
            && (c.Mode is null || !spec.DistinctModesPerTurn || state.RuleTextModeUses is { } uses
                && uses.TurnNumber == state.TurnNumber && uses.Uses.Contains(new RuleTextModeUse(new(source, c.SourceGeneration), c.CardNo, c.Mode, c.Origin, c.TokenFactoryCardNo)))
            && c.CapturedTick > 0 && c.CapturedTick <= state.Tick
            && ValidArmamentDeath(state, c, source, controller)
            && EquipmentSkillObservationShape(c)
            && ValidAgilePlayGrant(state, c, source, controller)
            && (spec.Timing == EnemyMightReducedHere ? ValidMightReduction(state, c, source) && (controller is null || c.MightReduction!.Actor == controller) : c.MightReduction is null)
            && (spec.Effect == "BURN_MIGHT_FRIENDLY"
                ? c.BurnedUnit is { } burned
                    ? c.ReflexiveTick is { } reflexive && reflexive >= c.CapturedTick && reflexive <= state.Tick && burned.Power >= 0
                        && CardBehaviorRegistry.TryGetByCardNo(burned.CardNo, out var burnedBehavior)
                        && burnedBehavior.PlaysSourceToBaseAsUnit && burnedBehavior.SourceUnitPower == burned.Power
                        && PredictBindingMatches(state, burned.Binding, burned.CardNo, burned.FaceRevision, reflexive, null)
                    : c.ReflexiveTick is null
                : c.BurnedUnit is null && (spec.Effect == RuneDomainRevealEffect || HasArmamentPlayAttachment(c) || c.ReflexiveTick is null))
            && ValidArmamentPlayAttachmentContext(state, c, controller)
            && ValidTargetSelectionContext(state, c, spec, source, controller)
            && ValidScoringActionOrigin(state, c, spec, controller)
            && ValidUnitEventOpponent(state, c, spec, controller)
            && ValidFriendlyUnitActionContext(state, c, spec, controller)
            && ValidBattleDrawContext(state, c, spec, controller)
            && ValidPlayerDefenseContext(state, c, spec)
            && ValidConditionalSourcePlayDestination(c, spec)
            && ValidRequiredUnitDestruction(state, c, spec, controller)
            && ValidSpellInstructionReflexive(state, c, spec, source)
            && ValidStartPhaseDeathUnitEvent(state, c, spec, controller)
            && (!c.SourceOptionalExperiencePaid || spec.Timing == "PLAY" && spec.ExemptsControllerIfExperiencePaid)
            && ValidFriendlyPlayCondition(c, spec)
            && ValidSourcePlayCondition(state, c, spec, source)
            && ValidEndConquestCondition(c, spec)
            && ValidTriggeringBattlefield(c, spec)
            && ValidScheduledUnitEventReceipt(state, c, source, controller)
            && ValidRevealedRuneContext(state, c, controller)
            && ValidExhaustionBatchContext(state, c, spec)
            && ValidPlayedUnitRuneObservation(state, c, source, controller)
            && (spec.Effect == DeploymentPlayMoveEffect ? ValidDeploymentPlayBattlefield(state, c) : c.DeploymentBattlefield is null)
            && (spec.Effect is "BANISH_ENTERED_EQUIPMENT" or DeploymentPlayMoveEffect ? c.Other is { } other
                && PredictBindingMatches(state, other.Binding, other.CardNo, other.FaceRevision, c.CapturedTick, other.TokenFactoryCardNo)
                && (CardBehaviorRegistry.TryGetByCardNo(other.CardNo, out var gear) && gear.PlaysSourceToBaseAsEquipment
                    || P6TokenFactoryCatalog.TryGetByCardNo(other.CardNo, out var token) && token.Tags.Contains(CardObjectTags.EquipmentCard))
                : spec.Effect == ReadySelectedEventUnit || IsFriendlyObservedUnitSpec(spec)
                    || spec.Timing == FriendlyUnitTargetedTiming || HasArmamentPlayAttachment(c) && c.ReflexiveTick is not null
                    ? c.Other is not null : c.Other is null)
            && (spec.Effect is "MATCH_POWER_THEN_BUFF" or "DESTROY_CHEAPER_EQUIPMENT" ? c.LastKnownPower is >= 0 : c.LastKnownPower is null)
            && (spec.Effect == "MIGHT_ATTACKERS" ? c.Attackers is { Count: > 0 }
                && c.Attackers.All(a => a is not null && a.Binding is { Generation: >= 0 }
                    && !string.IsNullOrWhiteSpace(a.Binding.ObjectId)
                    && PredictBindingMatches(state, a.Binding, a.CardNo, a.FaceRevision, c.CapturedTick, a.TokenFactoryCardNo))
                && c.Attackers.Select(a => a.Binding.ObjectId).Distinct(StringComparer.Ordinal).Count() == c.Attackers.Count
                : c.Attackers is null)
            && (spec.Effect is "MATCH_POWER_THEN_BUFF" or "MOVE_ENEMIES_TO_BASE" || UnitEventTimingMatches(spec.Timing, "MOVE") ? c.Here is { } here && state.Seats.ContainsKey(here.PlayerId)
                && (here.Zone == "BASE" && here.BattlefieldObjectId is null
                    || here.Zone == "BATTLEFIELD" && BattlefieldLocalRules.Battlefield(state, here.BattlefieldObjectId) is not null)
                : c.Here is null)
            && (CombatMightSourceLocation(state, source, c.SourceGeneration) is not { } current
                || (c.LastKnownPower is null || c.LastKnownPower == ResolveCurrentFieldUnitPower(state, state.CardObjects[source]))
                    && (c.Here is null || spec.CreatesUnitTokenHere || spec.RequiresMoveToBattlefield || spec.PreservesMovementCapture || c.Here == current))
            && (!spec.RequiresMoveToBattlefield || UnitEventTimingMatches(spec.Timing, "MOVE") && c.Here?.Zone == "BATTLEFIELD")
            && (c.Origin is { } origin ? AttachedRuleTextRules.ValidCaptured(state, origin, source, c.SourceGeneration, c.CardNo)
                : PredictBindingMatches(state, new(source, c.SourceGeneration), c.CardNo, c.FaceRevision, c.CapturedTick, c.TokenFactoryCardNo));

    private static bool NeedsUnitEventTarget(StackItemState item) => item.UnitEvent is { } c
        && (SelectedUnitEventSpec(c)?.Effect is AgilePlayAttachEffect or UnitEventGraveyardPlayEffect or UnitEventGraveyardReturnEffect or UnitEventEnemyDamageEffect or ArmamentUnitEventRules.FixedDamageEffect or RuneDomainDamageEffect or PlayedUnitReadyRunes or "BANISH_LINKED_UNIT_HERE" or DeploymentPlayMoveEffect or MoveFriendlyToArrivalBattlefieldEffect or MoveEnemyToCapturedBattlefieldEffect or MoveEnemyToArrivalBattlefieldEffect or MoveWeakerEnemyHereToAnotherBattlefieldEffect or MoveFriendlyGroupToOpenBattlefieldEffect or "STUN_ENEMY" or "BOON_FRIENDLY" or "READY_HERE" or FriendlyUnitTokensMoveHere or "MOVE_ENEMY_HERE" or "MOVE_ENEMY" or "READY_ANOTHER_FRIENDLY" or "MATCH_POWER_THEN_BUFF" or "DESTROY_CHEAPER_EQUIPMENT" or ConquestEquipmentBoon or "MOVE_ENEMIES_TO_BASE" or "MIGHT_FRIENDLY" or "MIGHT_ATTACKERS" or EquipmentUnitTransformationEffect
            || UnitEventSpecForContext(c)?.Effect == "BURN_MIGHT_FRIENDLY" && c.BurnedUnit is not null);
    internal static bool ValidUnitEventTargets(StackItemState item)
        => item.UnitEvent is { } grant && SelectedUnitEventSpec(grant)?.Effect == UnitEventTurnPlayGrantEffect
            ? ValidUnitEventTurnPlayGrantStack(item)
            : item.UnitEvent is { } traitScore && SelectedUnitEventSpec(traitScore)?.Effect == FriendlyUnitTraitScoreEffect
            ? ValidFriendlyUnitTraitScoreStack(item)
            : item.UnitEvent?.Timing == FriendlyUnitTargetedTiming
            ? item.TargetObjectIds.Count == 0 && item.TargetGenerations is null && !item.SourceConfirmed
                && item.EffectRepeatCount == 1 && item.DamageAmount == 0 && item.OptionalCosts.Count == 0 && item.Destination.Length == 0
            : item.TargetGenerations is null ? item.TargetObjectIds.Count == 0 : NeedsUnitEventTarget(item)
            && (item.TargetObjectIds.Count > 0 || UnitEventSpecForContext(item.UnitEvent!)!.Effect == PlayedUnitReadyRunes)
            && (UnitEventSpecForContext(item.UnitEvent!)!.AnyNumberOfTargets || item.TargetObjectIds.Count == 1)
            && (UnitEventSpecForContext(item.UnitEvent!)!.Effect != PlayedUnitReadyRunes || item.TargetObjectIds.Count <= UnitEventSpecForContext(item.UnitEvent!)!.Amount)
            && item.TargetObjectIds.Distinct(StringComparer.Ordinal).Count() == item.TargetObjectIds.Count
            && item.TargetGenerations.Count == item.TargetObjectIds.Count
            && item.TargetObjectIds.All(id => item.TargetGenerations.TryGetValue(id, out var generation) && generation >= 0);
    private static string[] UnitEventTargets(MatchState state, StackItemState item)
        => SelectedUnitEventSpec(item.UnitEvent!)?.Effect == UnitEventGraveyardReturnEffect
            ? state.PlayerZones[item.ControllerId].Graveyard.Where(id => IsUnitEventGraveyardReturnTarget(state, item, id))
                .Order(StringComparer.Ordinal).ToArray()
            : IsUnitEventGraveyardPlay(item) ? state.PlayerZones[item.ControllerId].Graveyard
            .Where(id => state.CardObjects.TryGetValue(id, out var card) && UnitEventGraveyardPlaySourceAllowed(item, card))
            .Order(StringComparer.Ordinal).ToArray()
            : state.CardObjects.Values.Where(c => !c.IsFaceDown && IsObjectOnField(state.PlayerZones, c.ObjectId)
            && (SelectedUnitEventSpec(item.UnitEvent!)!.Effect switch {
                PlayedUnitReadyRunes => c.Tags.Contains(CardObjectTags.RuneCard),
                ArmamentUnitEventRules.FixedDamageEffect or RuneDomainDamageEffect => IsFaceUpNonStandbyUnit(c)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) != item.ControllerId
                    && UnitEventTargetIsHere(state, item, c.ObjectId),
                UnitEventEnemyDamageEffect or "STUN_ENEMY" => IsFaceUpNonStandbyUnit(c)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) != item.ControllerId,
                EquipmentUnitTransformationEffect => c.Tags.Contains(CardObjectTags.EquipmentCard)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == item.ControllerId,
                DeploymentPlayMoveEffect => IsFaceUpNonStandbyUnit(c)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == item.ControllerId
                    && DeploymentPlayDestination(state, item.UnitEvent!) is { } playedDestination
                    && IsUnitMovementDestination(state, c.ObjectId, playedDestination)
                    && MovementRestrictionRules.CanMove(c, item.ControllerId),
                AgilePlayAttachEffect => IsFaceUpNonStandbyUnit(c)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == item.ControllerId,
                MoveFriendlyGroupToOpenBattlefieldEffect or "BOON_FRIENDLY" => IsFaceUpNonStandbyUnit(c)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == item.ControllerId,
                "BANISH_LINKED_UNIT_HERE" or "READY_HERE" => IsFaceUpNonStandbyUnit(c) && UnitEventTargetIsHere(state, item, c.ObjectId),
                MoveFriendlyToArrivalBattlefieldEffect => c.ObjectId != item.SourceObjectId && IsFaceUpNonStandbyUnit(c)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == item.ControllerId
                    && UnitEventArrivalBattlefieldDestination(state, item) is { } arrivalDestination
                    && IsUnitMovementDestination(state, c.ObjectId, arrivalDestination)
                    && MovementRestrictionRules.CanMove(c, item.ControllerId),
                FriendlyUnitTokensMoveHere => IsFaceUpNonStandbyUnit(c) && IsTokenObject(c)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == item.ControllerId,
                MoveWeakerEnemyHereToAnotherBattlefieldEffect => IsWeakerEnemyHereMovementTarget(state, item, c),
                // CN355.9 / 358.3.a: movement feasibility is checked by the executor,
                // after a legal enemy was confirmed and both players could respond.
                MoveEnemyToArrivalBattlefieldEffect => IsFaceUpNonStandbyUnit(c)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) != item.ControllerId,
                MoveEnemyToCapturedBattlefieldEffect => IsFaceUpNonStandbyUnit(c)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) != item.ControllerId,
                "MOVE_ENEMY_HERE" => IsFaceUpNonStandbyUnit(c)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) != item.ControllerId
                    && UnitEventHereDestination(state, item) is { } destination
                    && IsUnitMovementDestination(state, c.ObjectId, destination)
                    && MovementRestrictionRules.CanMove(c, item.ControllerId),
                "MOVE_ENEMY" => IsFaceUpNonStandbyUnit(c)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) != item.ControllerId
                    && HasChosenMovementDestination(state, item.ControllerId, c.ObjectId)
                    && MovementRestrictionRules.CanMove(c, item.ControllerId),
                "READY_ANOTHER_FRIENDLY" => c.ObjectId != item.SourceObjectId && IsFaceUpNonStandbyUnit(c)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == item.ControllerId,
                "MIGHT_FRIENDLY" or "BURN_MIGHT_FRIENDLY" => IsFaceUpNonStandbyUnit(c)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == item.ControllerId,
                "MIGHT_ATTACKERS" => IsFaceUpNonStandbyUnit(c)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == item.ControllerId
                    && item.UnitEvent!.Attackers!.Any(a => a.Binding.ObjectId == c.ObjectId && a.Binding.Generation == c.ObjectGeneration),
                "MATCH_POWER_THEN_BUFF" => IsFaceUpNonStandbyUnit(c)
                    && state.ObjectLocations.TryGetValue(c.ObjectId, out var loc) && loc.Zone == item.UnitEvent!.Here!.Zone
                    && (loc.Zone == "BASE" ? loc.PlayerId == item.UnitEvent.Here.PlayerId : loc.BattlefieldObjectId == item.UnitEvent.Here.BattlefieldObjectId),
                "DESTROY_CHEAPER_EQUIPMENT" => c.Tags.Contains(CardObjectTags.EquipmentCard) && c.ManaCost <= UnitEventSourcePower(state, item),
                "MOVE_ENEMIES_TO_BASE" => IsFaceUpNonStandbyUnit(c)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) != item.ControllerId
                    && state.ObjectLocations.TryGetValue(c.ObjectId, out var loc) && loc.Zone == item.UnitEvent!.Here!.Zone
                    && (loc.Zone == "BASE" ? loc.PlayerId == item.UnitEvent.Here.PlayerId : loc.BattlefieldObjectId == item.UnitEvent.Here.BattlefieldObjectId)
                    && ResolveCurrentFieldUnitPower(state, c) <= UnitEventSpecForContext(item.UnitEvent)!.Amount,
                _ => false
            })
            && (!SelectedUnitEventSpec(item.UnitEvent!)!.TargetsAnotherFriendlyHere
                || c.ObjectId != item.SourceObjectId
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == item.ControllerId
                    && UnitEventTargetIsHere(state, item, c.ObjectId))
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, c.ObjectId))
            .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();
    private static PendingCardChoiceState UnitEventChoice(MatchState state, StackItemState item)
    {
        var legal = SelectedUnitEventSpec(item.UnitEvent!)?.Effect == MoveFriendlyGroupToOpenBattlefieldEffect
            && OpenBattlefieldDestinations(state, item.ControllerId).Count == 0 ? [] : UnitEventInstructionTargets(state, item);
        return new("UNIT-EVENT:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId,
            item.UnitEvent!.DelayedReceipt is not null || UnitEventSpecForContext(item.UnitEvent)!.Optional ? 0 : 1,
            UnitEventSpecForContext(item.UnitEvent)!.Effect == PlayedUnitReadyRunes ? Math.Min(legal.Length, UnitEventSpecForContext(item.UnitEvent)!.Amount)
                : UnitEventSpecForContext(item.UnitEvent)!.AnyNumberOfTargets ? legal.Length : 1, legal, legal,
            "选择单位技能的合法目标；确认后双方可以响应。", item.SourceObjectId, item.EffectKind)
            { ResolvingStackItemId = item.StackItemId };
    }
    private static ResolutionResult PrepareUnitEventTarget(ResolutionResult result, StackItemState item)
    {
        var choice = UnitEventChoice(result.State, item);
        if (choice.LegalObjectIds.Count == 0)
        {
            if (UnitEventSpecForContext(item.UnitEvent!)!.Effect == PlayedUnitReadyRunes)
            {
                var confirmed = ResolveTriggerTargetConfirmation(result.State, choice, [], advanceTick: false);
                return confirmed with { Events = result.Events.Concat(confirmed.Events).ToArray() };
            }
            return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        }
        var next = result.State with { PendingCardChoice = choice, ActivePlayerId = item.ControllerId, PriorityPlayerId = null };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }
    internal static bool ValidUnitEventChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item is null || !NeedsUnitEventTarget(item) || item.TargetGenerations is not null
            || !ValidUnitEvent(state, item.UnitEvent, item.EffectKind, item.SourceObjectId, item.CardNo, item.ControllerId)) return false;
        var expected = UnitEventChoice(state, item);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow && choice.PlayerId == item.ControllerId
            && choice.SourceObjectId == item.SourceObjectId && choice.EffectKind == item.EffectKind && choice.RequiredCount == expected.RequiredCount && choice.MaxCount == expected.MaxCount
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds) && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    private static int UnitEventSourcePower(MatchState state, StackItemState item)
        => CombatMightSourceLocation(state, item.SourceObjectId, item.UnitEvent!.SourceGeneration) is not null
            ? ResolveCurrentFieldUnitPower(state, state.CardObjects[item.SourceObjectId]) : item.UnitEvent.LastKnownPower!.Value;

    private static StackResolutionResult ResolveUnitEvent(MatchState state, StackItemState item)
    {
        var result = ResolveConquestUnitEventInstruction(state, item) ?? ResolveUnitEventInstruction(state, item);
        result = AppendIndependentUnitEventExperience(state, item, result);
        var source = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator);
        return source == item.SourceObjectId ? result : result with { Events = result.Events.Select(e =>
            Equals(e.Payload.GetValueOrDefault("sourceObjectId"), item.SourceObjectId)
                ? e with { Payload = new Dictionary<string, object?>(e.Payload) { ["sourceObjectId"] = source } } : e).ToArray() };
    }

    private static StackResolutionResult ResolveUnitEventInstruction(MatchState state, StackItemState item)
    {
        var context = item.UnitEvent!;
        if (!ValidUnitEvent(state, context, item.EffectKind, item.SourceObjectId, item.CardNo, item.ControllerId))
            throw new InvalidOperationException("Invalid captured unit event.");
        var spec = SelectedUnitEventSpec(context)!;
        if (spec.Effect == UnitEventSelfGraveSpellPlayEffect) return ResolveSelfGraveSpellPlay(state, item);
        if (spec.Effect == UnitEventLookUnitEffect)
        {
            if (item.DeckChoiceCompleted) return NoopStackResolutionResult(state);
            if (!TryGetDeckChoiceBehavior(state, item, out var deck)) throw new InvalidOperationException("Missing unit deck choice.");
            return BeginDeckChoice(state, item, deck);
        }
        if (spec.Effect == UnitEventHandUnitPlayEffect)
        {
            if (item.EffectPlayCompleted) return NoopStackResolutionResult(state);
            if (!TryGetEffectPlayDefinition(item, out var hand)) throw new InvalidOperationException("Missing hand unit play.");
            return BeginEffectPlay(state, item, hand);
        }
        if (spec.Effect == EachPlayerDestroyUnitEffect) return ResolveEachPlayerDestroyUnit(state, item, spec);
        if (spec.Effect == UnitEventGraveyardReturnEffect) return ResolveUnitEventGraveyardReturn(state, item);
        if (spec.Effect == AgilePlayAttachEffect) return ResolveAgilePlayAttachment(state, item);
        if (spec.Effect == ReturnLowMightUnitsEffect && spec.ReturnAllUnitsMaximumMight is { } maximum)
            return ResolveLowMightGroupReturn(state, item, maximum);
        if (spec.Effect == MoveFriendlyGroupToOpenBattlefieldEffect) return ResolveFriendlyGroupToOpenBattlefield(state, item);
        if (spec.Effect == ScheduleNextMainUnitEventEffect) return ScheduleNextMainUnitEvent(state, item);
        if (spec.Effect == "GAIN_EXPERIENCE") return ResolveUnitEventExperience(state, item, spec);
        if (spec.Effect == DetachOriginDamageSourceEffect) return ResolveDetachOriginDamageSource(state, item, spec);
        if (spec.Effect == RuneDomainRevealEffect && context.RevealedRune is null) return ResolveRuneDomainReveal(state, item);
        if (spec.Modes is not null) throw new InvalidOperationException("Unconfirmed unit event mode.");
        if (spec.Effect == UnitEventTurnPlayGrantEffect) return ResolveUnitEventTurnPlayGrant(state, item);
        if (IsFriendlyObservedUnitSpec(spec)) return ResolveObservedFriendlyUnit(state, item, spec);
        if (spec.Effect == ReadySelectedEventUnit) return ResolveSelectedEventUnitReady(state, item);
        if (spec.Effect == BattleDrawRecallEffect) return ResolveBattleDrawRecall(state, item);
        if (spec.Effect == PlayerDefenseMoveEffect) return ResolvePlayerDefenseMovement(state, item);
        if (spec.Effect is UnitEventEnemyDamageEffect or RuneDomainDamageEffect or ArmamentUnitEventRules.FixedDamageEffect)
            return ResolveRuneDomainDamage(state, item, spec);
        if (spec.Effect is StandbyHandPlayHereEffect or PlayHandArmamentThenAttachEffect)
        {
            if (item.EffectPlayCompleted) return NoopStackResolutionResult(state);
            TryGetEffectPlayDefinition(item, out var definition);
            return BeginEffectPlay(state, item, definition);
        }
        if (spec.Effect == UnitEventGraveyardPlayEffect) return ResolveUnitEventGraveyardPlay(state, item);
        if (spec.Effect == AttachPlayedArmamentEffect) return ResolvePlayedArmamentAttachment(state, item);
        if (IsPlayedUnitRuneSpec(spec)) return ResolvePlayedUnitRunes(state, item, spec);
        if (spec.Effect == "CALL_RUNES_PER_CONSUMED_BOON") return ResolvePaidBoonRuneInstruction(state, item, spec);
        if (spec.Effect == "BANISH_LINKED_UNIT_HERE") return ResolveLinkedUnitExile(state, item);
        if (spec.Effect == ArmamentLastBreathRules.BanishEffect) return ResolveArmamentDeathBanish(state, item);
        if (spec.Effect == "BANISH_TOP_TIMED_PLAY") return ResolveTimedBanishPlay(state, item,
            [new("ABILITY_RESOLVED", "触发技能结算", new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId })]);
        if (spec.Effect is "BOON_SELF" or "BOON_SELF_AND_READY" or "READY_SELF" or "MIGHT_SELF_AND_READY" or "STUN_ENEMY")
            return ResolveUnitEventBoonInstruction(state, item, spec);
        if (spec.Effect == EquipmentUnitTransformationEffect) return ResolveEquipmentUnitTransformation(state, item);
        if (spec.Effect == "DESTROY_FRIENDLY_EQUIPMENT_MIGHT_SELF")
            return ResolvePaidEquipmentDestructionMight(state, item, spec.Amount);
        if (spec.Effect == "DISCARD_DRAW") return ResolvePrivateDiscardDraw(state, item, spec.DiscardCount, spec.Amount);
        if (spec.Effect == "MIGHT_OTHER_FRIENDLIES") return ResolveOtherFriendlyUnitMight(state, item, spec);
        if (spec.Effect is DeploymentPlayMoveEffect or MoveFriendlyToArrivalBattlefieldEffect or MoveEnemyToCapturedBattlefieldEffect or MoveEnemyToArrivalBattlefieldEffect or MoveWeakerEnemyHereToAnotherBattlefieldEffect or "BOON_FRIENDLY" or "READY_HERE" or FriendlyUnitTokensMoveHere or "MOVE_ENEMY_HERE" or "MOVE_ENEMY" or "CREATE_UNIT_TOKEN")
            return ResolvePermanentUnitEventInstruction(state, item, spec);
        if (spec.Effect == "DAMAGE_ENEMIES_HERE") return ResolveUnitEventAreaDamage(state, item, spec.Amount);
        if (spec.Effect == "DESTROY_DAMAGED_ENEMIES_HERE") return ResolveUnitEventDamagedEnemyDestruction(state, item);
        if (spec.Effect == "BANISH_HAND_GRAVEYARD_BURN") return ResolveBanishHandGraveyardBurn(state, item);
        if (spec.Effect == "BURN_MIGHT_FRIENDLY") return ResolveBurnMightAbility(state, item);
        if (spec.Effect == BeginningWinEffect) return ResolveUnitEventWin(state, item);
        if (spec.Effect == FriendlyUnitTraitScoreEffect) return ResolveFriendlyUnitTraitScore(state, item, spec);
        if (spec.Effect == "SCORE" && (LeadingTriggerCost(item) is null || item.TriggerCost is not null))
            return ResolveUnitEventPointGain(state, item, spec.Amount);
        if (spec.Effect == "NEXT_CARD_COST_REDUCTION") {
            CardBehaviorRegistry.TryGetByCardNo(context.CardNo, out var reducer);
            var marker = BuildSourceNextSpellCostReductionEffectId(reducer, item.ControllerId, item.SourceObjectId);
            return NoopStackResolutionResult(state) with {
                UntilEndOfTurnEffects = AddUntilEndOfTurnEffect(state.UntilEndOfTurnEffects, marker),
                Events = [new("NEXT_CARD_COST_REDUCTION_GRANTED", "本回合下一张卡牌减费", new Dictionary<string, object?> {
                    ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                    ["manaReduction"] = reducer.SourceNextSpellCostReductionMana,
                    ["powerReduction"] = reducer.SourceNextSpellCostReductionGenericPower })] };
        }
        if (spec.Effect == "EMPOWER_SELF") return ApplyEmpowerAction(state, new(item.SourceObjectId, context.SourceGeneration), item.ControllerId);
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var events = new List<GameEvent> { new("TRIGGER_RESOLVED", "触发技能结算", new Dictionary<string, object?> {
            ["sourceObjectId"] = item.SourceObjectId, ["sourceCardNo"] = context.CardNo, ["effectKind"] = item.EffectKind }) };
        if (spec.Effect == "DESTROY_SELF") {
            if (!cards.TryGetValue(item.SourceObjectId, out var self) || self.ObjectGeneration != context.SourceGeneration
                || !IsObjectOnField(zones, self.ObjectId)) return NoopStackResolutionResult(state) with { Events = events };
            var removal = ResolveFieldDestructions(state, zones, cards, item, new HashSet<string>(),
                state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(), state.RunePools, objectLocations: state.ObjectLocations,
                explicitDestroyObjectIds: new HashSet<string> { self.ObjectId }, explicitDestroyReason: "SKILL_EFFECT");
            return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, RunePools = removal.RunePools,
                TriggerQueue = removal.TriggerQueue, DestroyedUnitOwnerIds = removal.DestroyedUnitOwnerIds,
                Events = events.Concat(removal.Events).ToArray(), ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
        }
        if (spec.Effect == EachPlayerEquipmentTokenEffect) return ResolveEachPlayerEquipmentTokens(state, item, spec);
        if (spec.Effect == "CREATE_EQUIPMENT_TOKEN") {
            if (!P6TokenFactoryCatalog.TryGetByCardNo(spec.CreatedTokenFactoryCardNo!, out var factory)
                || !factory.Tags.Contains(CardObjectTags.EquipmentCard))
                throw new InvalidOperationException("Invalid unit event equipment token factory.");
            for (var i = 0; i < spec.Amount; i++)
                CreateLegendEquipmentToken(zones, cards, item.ControllerId, item.SourceObjectId, item.EffectKind,
                    factory.TokenFamilyName, factory.Tags, true, events, context.CardNo,
                    tokenIdentitySeed: "equipment-token-" + MatchStateHasher.HashValue(item.StackItemId));
            var publicSource = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator);
            for (var i = 0; i < events.Count; i++)
                if (Equals(events[i].Payload.GetValueOrDefault("sourceObjectId"), item.SourceObjectId))
                    events[i] = events[i] with { Payload = new Dictionary<string, object?>(events[i].Payload) {
                        ["sourceObjectId"] = publicSource } };
            return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
        }
        if ((spec.Effect == "MIGHT_FRIENDLY" && (LeadingTriggerCost(item) is null || item.TriggerCost is not null) || spec.Effect == "MIGHT_ATTACKERS") && item.TargetObjectIds.SingleOrDefault() is { } buffed
            && StackObjectRules.ReferencesCurrentObject(state, item, buffed) && UnitEventTargets(state, item).Contains(buffed)) {
            var behavior = new CardBehaviorDefinition(context.CardNo, "触发技能给予战力加成", 0, item.EffectKind, 0, 1, PowerModifierAmount: spec.Amount);
            cards[buffed] = ApplyPowerModifier(state, cards[buffed], behavior, item, buffed, spec.Amount, out var power);
            events.Add(power);
            if (spec.GrantedCombatKeyword is { } keyword)
                GrantUnitEventCombatKeyword(state, item, cards, events, buffed, keyword);
        }
        if (spec.Effect == "BANISH_ENTERED_EQUIPMENT" && item.TriggerCost?.SourceWasBanished == true
            && context.Other is { } entered && cards.TryGetValue(entered.Binding.ObjectId, out var equipment)
            && equipment.ObjectGeneration == entered.Binding.Generation && equipment.Tags.Contains(CardObjectTags.EquipmentCard)
            && TryRemoveFieldTargetCore(zones, cards, equipment.ObjectId, true, out var banished))
            return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
                Events = events.Concat(BuildFieldRemovalEvents("拉文布鲁姆级长", item, equipment.ObjectId, banished)).ToArray() };
        if (spec.Effect == "MOVE_ENEMIES_TO_BASE") {
            var legal = UnitEventTargets(state, item).ToHashSet(StringComparer.Ordinal);
            var selected = item.TargetObjectIds.Where(id => legal.Contains(id) && StackObjectRules.ReferencesCurrentObject(state, item, id)).ToArray();
            var locations = state.ObjectLocations.ToDictionary(); var pools = state.RunePools.ToDictionary();
            foreach (var group in selected.GroupBy(id => FindFieldObjectLocation(zones, id)!.Value.PlayerId))
                MoveFieldUnitsToChosenLocation(state, zones, cards, locations, pools, item, group.ToArray(), "BASE:" + group.Key, events);
            return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, RunePools = pools,
                ObjectLocations = ReconcileObjectLocations(locations, zones), Events = events };
        }
        if (spec.Effect == "DESTROY_CHEAPER_EQUIPMENT" && item.TargetObjectIds.SingleOrDefault() is { } gear
            && StackObjectRules.ReferencesCurrentObject(state, item, gear) && UnitEventTargets(state, item).Contains(gear)) {
            var removal = ResolveFieldDestructions(state, zones, cards, item, new HashSet<string>(), state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(),
                state.RunePools, objectLocations: state.ObjectLocations, explicitDestroyObjectIds: new HashSet<string> { gear });
            return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, RunePools = removal.RunePools,
                TriggerQueue = removal.TriggerQueue, Events = events.Concat(removal.Events).ToArray(), DestroyedUnitOwnerIds = removal.DestroyedUnitOwnerIds,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
        }
        if (spec.Effect == "ASSAULT_SELF" && item.TriggerCost is not null && cards.TryGetValue(item.SourceObjectId, out var assaulter)
            && assaulter.ObjectGeneration == context.SourceGeneration && IsObjectOnField(zones, assaulter.ObjectId)) {
            var effect = TemporaryCombatKeywordRules.AppliedEffectId(assaulter, $"{TemporaryCombatKeywordRules.Prefix}{CardCombatKeywordNames.Assault}{spec.Amount}");
            cards[assaulter.ObjectId] = assaulter with { UntilEndOfTurnEffects = [..assaulter.UntilEndOfTurnEffects, effect] };
            events.Add(new("UNIT_STATUS_APPLIED", "单位在本回合内获得强攻", new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = assaulter.ObjectId, ["statusEffectId"] = effect }));
        }
        if (spec.Effect == "MATCH_POWER_THEN_BUFF" && item.TargetObjectIds.SingleOrDefault() is { } matchedTarget
            && StackObjectRules.ReferencesCurrentObject(state, item, matchedTarget) && UnitEventTargets(state, item).Contains(matchedTarget)
            && cards.TryGetValue(item.SourceObjectId, out var matching) && matching.ObjectGeneration == context.SourceGeneration
            && IsObjectOnField(zones, matching.ObjectId)) {
            // Raising to a lower or equal value does nothing; the subsequent +1 still applies.
            var delta = Math.Max(0, ResolveCurrentFieldUnitPower(state, cards[matchedTarget]) - UnitEventSourcePower(state, item)) + 1;
            var behavior = new CardBehaviorDefinition(context.CardNo, "追平战力后获得+1", 0, item.EffectKind, 0, 1, PowerModifierAmount: delta);
            cards[matching.ObjectId] = ApplyPowerModifier(state, matching, behavior, item, matching.ObjectId, delta, out var power);
            events.Add(power);
        }
        if (spec.Effect == "DRAW" && (LeadingTriggerCost(item) is null || item.TriggerCost is not null)) {
            // CN383.2.a.1: the undamaged clause is checked when the battle ends.
            var draw = ApplyDrawToPlayer(state, zones, cards, state.PlayerScores, item.ControllerId, spec.Amount, state.RngCursor, events, deferNormalVictory: true);
            if (spec.RuneCallCount > 0 && draw.WinnerPlayerId is null)
            {
                var called = CallRunes(zones, cards, item.ControllerId, spec.RuneCallCount);
                events.Add(new("RUNES_CALLED", "触发技能召出休眠符文", new Dictionary<string, object?> {
                    ["playerId"] = item.ControllerId,
                    ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator),
                    ["count"] = called.CalledRuneObjectIds.Count,
                    ["runeObjectIds"] = called.CalledRuneObjectIds.ToArray(), ["reason"] = item.EffectKind }));
            }
            return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, PlayerScores = draw.PlayerScores,
                RngCursor = draw.RngCursor, WinnerPlayerId = draw.WinnerPlayerId, Events = events,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
        }
        if (spec.Effect == "MIGHT_SELF" && cards.TryGetValue(item.SourceObjectId, out var source)
            && source.ObjectGeneration == context.SourceGeneration && IsObjectOnField(zones, source.ObjectId)) {
            cards[source.ObjectId] = ApplyDirectUntilEndPowerModifier(source, source.ObjectId, item.SourceObjectId, context.CardNo,
                item.EffectKind, "UNIT_EVENT", spec.Amount, source.Power + spec.Amount);
            events.Add(new("UNIT_POWER_CHANGED", "单位在本回合内获得战力", new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = source.ObjectId, ["powerDelta"] = spec.Amount }));
        }
        if (spec.Effect == "READY_ANOTHER_FRIENDLY" && item.TargetObjectIds.SingleOrDefault() is { } target
            && StackObjectRules.ReferencesCurrentObject(state, item, target) && UnitEventTargets(state, item).Contains(target)) {
            var card = cards[target]; var behavior = new CardBehaviorDefinition(context.CardNo, "进攻触发技能", 0, item.EffectKind, 0, 1, ReadiesTarget: true);
            cards[target] = ApplyReadyState(card, behavior, item, target, out var ready, state.PlayerZones, cards);
            if (ready is not null) events.Add(ready);
        }
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}

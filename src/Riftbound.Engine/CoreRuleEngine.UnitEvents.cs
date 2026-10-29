using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UnitEventContext(string CardNo, long SourceGeneration, long FaceRevision, long CapturedTick,
    string Timing, string? TokenFactoryCardNo = null, RuleTextInstanceBinding? Origin = null,
    ObjectLocationState? Here = null, int? LastKnownPower = null, UnitEventOtherObject? Other = null,
    BurnedUnitFact? BurnedUnit = null, long? ReflexiveTick = null);
public sealed record UnitEventOtherObject(ObjectBinding Binding, string CardNo, long FaceRevision, string? TokenFactoryCardNo = null);
public sealed record BurnedUnitFact(ObjectBinding Binding, string CardNo, long FaceRevision, int Power);

public sealed partial class CoreRuleEngine
{
    internal const string UnitEventEffect = "CAPTURED_UNIT_EVENT_ABILITY";
    internal const string TookDamageThisTurn = "TOOK_DAMAGE_THIS_TURN";
    private const string UnitEventCaptureKey = "unitEventObservations";
    private sealed record UnitEventObservation(string Source, string Controller, UnitEventContext Context);
    private static UnitEventAbilitySpec? UnitEventSpec(string? no)
        => no is not null && CardBehaviorRegistry.TryGetByCardNo(no, out var card) ? card.UnitEventAbility : null;

    private static UnitEventObservation[] CaptureUnitEvents(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, IEnumerable<string> sources, string timing)
        => sources.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Where(id => cards.TryGetValue(id, out var card)
                && !card.IsFaceDown && (IsFaceUpNonStandbyUnit(card) || card.Tags.Contains(CardObjectTags.EquipmentCard))
                && IsObjectOnField(zones, id))
            .Select(id => cards[id]).SelectMany(card => AttachedRuleTextRules.Instances(zones, cards, card)
                .Where(i => UnitEventSpec(i.CardNo) is { } spec
                    && (spec.Timing == timing || spec.Timing == "ATTACK_OR_DEFEND" && timing is "ATTACK" or "DEFEND"
                        || spec.Timing == "PLAY_OR_START" && timing is "PLAY" or "START")
                    && (!spec.RequiresEmpowered || card.IsEmpowered)
                    && (!spec.RequiresNoDamageThisTurn || !card.UntilEndOfTurnEffects.Contains(TookDamageThisTurn)))
                .Select(i => new UnitEventObservation(card.ObjectId, EffectiveFieldControllerId(zones, card.ObjectId, card),
                    new(i.CardNo, card.ObjectGeneration, card.CopyFaceTransitions?.Count ?? 0, 0, UnitEventSpec(i.CardNo)!.Timing,
                        card.TokenFactoryCardNo, i.Origin)))).ToArray();

    private static GameEvent WithUnitEvents(GameEvent ev, IReadOnlyList<UnitEventObservation> observations)
        => observations.Count == 0 ? ev : ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
            [UnitEventCaptureKey] = ((ev.Payload.GetValueOrDefault(UnitEventCaptureKey) as UnitEventObservation[]) ?? [])
                .Concat(observations).ToArray() } };

    private static GameEvent CapturePhaseCardEvents(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, string player, string timing)
        => WithUnitEvents(ev, CaptureUnitEvents(zones, cards, cards.Values.Where(c =>
            EffectiveFieldControllerId(zones, c.ObjectId, c) == player).Select(c => c.ObjectId), timing));

    private static GameEvent CaptureOtherUnitPlayed(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState entered, string player)
        => ev.Kind is not ("UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD" or "UNIT_TOKEN_CREATED") ? ev
            : WithUnitEvents(ev, CaptureUnitEvents(zones, cards, cards.Values.Where(c => c.ObjectId != entered.ObjectId
                && EffectiveFieldControllerId(zones, c.ObjectId, c) == player).Select(c => c.ObjectId), "ANOTHER_UNIT_PLAYED"));

    private static GameEvent CaptureEquipmentPlayed(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState entered, string player)
        => WithUnitEvents(ev, CaptureUnitEvents(zones, cards, cards.Values.Where(c =>
            EffectiveFieldControllerId(zones, c.ObjectId, c) != player).Select(c => c.ObjectId), "OPPONENT_EQUIPMENT_PLAYED")
            .Select(o => o with { Context = o.Context with { Other = new(new(entered.ObjectId, entered.ObjectGeneration), entered.CardNo!,
                entered.CopyFaceTransitions?.Count ?? 0, entered.TokenFactoryCardNo) } }).Concat(
                    CaptureUnitEvents(zones, cards, [entered.ObjectId], "PLAY")).ToArray());

    private static void CaptureSelfDiscardEvents(IReadOnlyDictionary<string, CardObjectState> cards, string player,
        string eventKind, IReadOnlyList<string> discarded, List<GameEvent> events)
    {
        var observations = discarded.Distinct(StringComparer.Ordinal).Where(id => cards.TryGetValue(id, out var card)
            && UnitEventSpec(card.CardNo)?.Timing == "SELF_DISCARDED").Select(id => cards[id])
            .Select(card => new UnitEventObservation(card.ObjectId, player, new(card.CardNo!, card.ObjectGeneration,
                card.CopyFaceTransitions?.Count ?? 0, 0, "SELF_DISCARDED", card.TokenFactoryCardNo))).ToArray();
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
        => !wasExhausted ? ev : WithUnitEvents(ev, CaptureUnitEvents(zones, cards, [unit], "BECAME_READY"));

    private static TriggerQueueItemState[] CaptureAttackUnitEvents(MatchState state, IEnumerable<string> attackers,
        string timing, string eventKind)
        => CaptureRoleUnitEvents(state, attackers, "ATTACK", timing, eventKind);

    private static TriggerQueueItemState[] CaptureRoleUnitEvents(MatchState state, IEnumerable<string> units,
        string role, string timing, string eventKind)
        => CaptureUnitEvents(state.PlayerZones, state.CardObjects, units, role).Select(o => CaptureUnitEventFacts(state, o)).Select((o, i) =>
            UnitEventTrigger(o, state.Tick + 1, i, eventKind, timing)).ToArray();

    private static UnitEventObservation CaptureUnitEventFacts(MatchState state, UnitEventObservation o)
        => UnitEventSpec(o.Context.CardNo)?.Effect is "MATCH_GREATER_POWER" or "DESTROY_CHEAPER_EQUIPMENT" or "MOVE_ENEMIES_TO_BASE"
            ? o with { Context = o.Context with {
                LastKnownPower = UnitEventSpec(o.Context.CardNo)!.Effect == "MOVE_ENEMIES_TO_BASE" ? null
                    : ResolveCurrentFieldUnitPower(state, state.CardObjects[o.Source]),
                Here = UnitEventSpec(o.Context.CardNo)!.Effect is "MATCH_GREATER_POWER" or "MOVE_ENEMIES_TO_BASE" ? state.ObjectLocations[o.Source] : null } } : o;

    private static TriggerQueueItemState[] CaptureConquestUnitEvents(MatchState state, IEnumerable<string> units, long tick)
        => CaptureUnitEvents(state.PlayerZones, state.CardObjects, units, "CONQUEST")
            .Select(o => CaptureUnitEventFacts(state, o)).Select((o, i) => UnitEventTrigger(o, tick, i, "BATTLEFIELD_CONQUERED")).ToArray();

    private static ResolutionResult RecordUnitEventSourceFacts(MatchState before, ResolutionResult result)
    {
        UnitEventContext? Update(string id, UnitEventContext? c) {
            if (c is null || c.LastKnownPower is null && c.Here is null) return c;
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

    private static TriggerQueueItemState UnitEventTrigger(UnitEventObservation observation, long tick, int ordinal,
        string eventKind, string? timing = null)
        => new($"unit-event-{tick}-{ordinal}-{observation.Source}-{observation.Context.Timing}", observation.Controller,
            observation.Source, UnitEventEffect, eventKind, timing) {
            UnitEvent = observation.Context with { CapturedTick = observation.Context.CapturedTick > 0 ? observation.Context.CapturedTick : tick },
            SourceCardNo = observation.Context.CardNo };

    private static ResolutionResult QueueUnitEvents(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var queue = result.State.TriggerQueue.ToList(); var events = result.Events.ToList(); var ordinal = 0;
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i))) {
            if (ev.Payload.GetValueOrDefault(UnitEventCaptureKey) is not UnitEventObservation[] observations) continue;
            events[index] = ev with { Payload = ev.Payload.Where(p => p.Key != UnitEventCaptureKey).ToDictionary(p => p.Key, p => p.Value) };
            foreach (var observation in observations) {
                var timing = ev.Kind == "BATTLE_CLOSED" ? null : before.StackItems.LastOrDefault()?.TimingContext;
                if (timing is not (TimingStates.SpellDuelOpen or TimingStates.NeutralClosed))
                    timing = before.SpellDuelState.IsActive ? TimingStates.SpellDuelOpen : null;
                var captured = observation;
                if (captured.Context.Timing is "PLAY" or "PLAY_OR_START" && ev.Kind is "EQUIPMENT_PLAYED_TO_BASE" or "EQUIPMENT_PLAYED_TO_BATTLEFIELD"
                    && before.CardObjects.TryGetValue(captured.Source, out var entering) && !IsObjectOnField(before.PlayerZones, entering.ObjectId))
                    captured = captured with { Context = captured.Context with {
                        SourceGeneration = Math.Max(captured.Context.SourceGeneration, entering.ObjectGeneration + 1) } };
                if (captured.Context.Other is { } other && before.CardObjects.TryGetValue(other.Binding.ObjectId, out var prior)
                    && !IsObjectOnField(before.PlayerZones, prior.ObjectId))
                    captured = captured with { Context = captured.Context with { Other = other with {
                        Binding = other.Binding with { Generation = Math.Max(other.Binding.Generation, prior.ObjectGeneration + 1) } } } };
                var trigger = UnitEventTrigger(captured, result.State.Tick, ordinal++, ev.Kind, timing);
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        if (events.Count == result.Events.Count) return result;
        var next = result.State with { TriggerQueue = queue };
        return result with { State = next, Events = events, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static bool ValidUnitEvent(MatchState state, UnitEventContext? c, string effect, string source, string? no = null)
        => c is null ? effect != UnitEventEffect : effect == UnitEventEffect && (no is null || no == c.CardNo)
            && UnitEventSpec(c.CardNo) is { } spec && spec.Timing == c.Timing && c.SourceGeneration >= 0 && c.FaceRevision >= 0
            && c.CapturedTick > 0 && c.CapturedTick <= state.Tick
            && (spec.Effect == "BURN_MIGHT_FRIENDLY"
                ? c.BurnedUnit is { } burned
                    ? c.ReflexiveTick is { } reflexive && reflexive >= c.CapturedTick && reflexive <= state.Tick && burned.Power >= 0
                        && CardBehaviorRegistry.TryGetByCardNo(burned.CardNo, out var burnedBehavior)
                        && burnedBehavior.PlaysSourceToBaseAsUnit && burnedBehavior.SourceUnitPower == burned.Power
                        && PredictBindingMatches(state, burned.Binding, burned.CardNo, burned.FaceRevision, reflexive, null)
                    : c.ReflexiveTick is null
                : c.BurnedUnit is null && c.ReflexiveTick is null)
            && (spec.Effect == "BANISH_ENTERED_EQUIPMENT" ? c.Other is { } other
                && PredictBindingMatches(state, other.Binding, other.CardNo, other.FaceRevision, c.CapturedTick, other.TokenFactoryCardNo)
                && (CardBehaviorRegistry.TryGetByCardNo(other.CardNo, out var gear) && gear.PlaysSourceToBaseAsEquipment
                    || P6TokenFactoryCatalog.TryGetByCardNo(other.CardNo, out var token) && token.Tags.Contains(CardObjectTags.EquipmentCard))
                : c.Other is null)
            && (spec.Effect is "MATCH_GREATER_POWER" or "DESTROY_CHEAPER_EQUIPMENT" ? c.LastKnownPower is >= 0 : c.LastKnownPower is null)
            && (spec.Effect is "MATCH_GREATER_POWER" or "MOVE_ENEMIES_TO_BASE" ? c.Here is { } here && state.Seats.ContainsKey(here.PlayerId)
                && (here.Zone == "BASE" && here.BattlefieldObjectId is null
                    || here.Zone == "BATTLEFIELD" && BattlefieldLocalRules.Battlefield(state, here.BattlefieldObjectId) is not null)
                : c.Here is null)
            && (CombatMightSourceLocation(state, source, c.SourceGeneration) is not { } current
                || (c.LastKnownPower is null || c.LastKnownPower == ResolveCurrentFieldUnitPower(state, state.CardObjects[source]))
                    && (c.Here is null || c.Here == current))
            && (c.Origin is { } origin ? AttachedRuleTextRules.ValidCaptured(state, origin, source, c.SourceGeneration, c.CardNo)
                : PredictBindingMatches(state, new(source, c.SourceGeneration), c.CardNo, c.FaceRevision, c.CapturedTick, c.TokenFactoryCardNo));

    private static bool NeedsUnitEventTarget(StackItemState item) => item.UnitEvent is { } c
        && (UnitEventSpec(c.CardNo)?.Effect is "READY_ANOTHER_FRIENDLY" or "MATCH_GREATER_POWER" or "DESTROY_CHEAPER_EQUIPMENT" or "MOVE_ENEMIES_TO_BASE" or "MIGHT_FRIENDLY"
            || UnitEventSpec(c.CardNo)?.Effect == "BURN_MIGHT_FRIENDLY" && c.BurnedUnit is not null);
    internal static bool ValidUnitEventTargets(StackItemState item)
        => item.TargetGenerations is null || NeedsUnitEventTarget(item) && item.TargetObjectIds.Count > 0
            && (UnitEventSpec(item.UnitEvent!.CardNo)!.AnyNumberOfTargets || item.TargetObjectIds.Count == 1)
            && item.TargetObjectIds.Distinct(StringComparer.Ordinal).Count() == item.TargetObjectIds.Count
            && item.TargetGenerations.Count == item.TargetObjectIds.Count
            && item.TargetObjectIds.All(id => item.TargetGenerations.TryGetValue(id, out var generation) && generation >= 0);
    private static string[] UnitEventTargets(MatchState state, StackItemState item)
        => state.CardObjects.Values.Where(c => !c.IsFaceDown && IsObjectOnField(state.PlayerZones, c.ObjectId)
            && (UnitEventSpec(item.UnitEvent!.CardNo)!.Effect switch {
                "READY_ANOTHER_FRIENDLY" => c.ObjectId != item.SourceObjectId && IsFaceUpNonStandbyUnit(c)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == item.ControllerId,
                "MIGHT_FRIENDLY" or "BURN_MIGHT_FRIENDLY" => IsFaceUpNonStandbyUnit(c)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == item.ControllerId,
                "MATCH_GREATER_POWER" => IsFaceUpNonStandbyUnit(c) && c.ObjectId != item.SourceObjectId
                    && state.ObjectLocations.TryGetValue(c.ObjectId, out var loc) && loc.Zone == item.UnitEvent.Here!.Zone
                    && (loc.Zone == "BASE" ? loc.PlayerId == item.UnitEvent.Here.PlayerId : loc.BattlefieldObjectId == item.UnitEvent.Here.BattlefieldObjectId)
                    && ResolveCurrentFieldUnitPower(state, c) > UnitEventSourcePower(state, item),
                "DESTROY_CHEAPER_EQUIPMENT" => c.Tags.Contains(CardObjectTags.EquipmentCard) && c.ManaCost <= UnitEventSourcePower(state, item),
                "MOVE_ENEMIES_TO_BASE" => IsFaceUpNonStandbyUnit(c)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) != item.ControllerId
                    && state.ObjectLocations.TryGetValue(c.ObjectId, out var loc) && loc.Zone == item.UnitEvent.Here!.Zone
                    && (loc.Zone == "BASE" ? loc.PlayerId == item.UnitEvent.Here.PlayerId : loc.BattlefieldObjectId == item.UnitEvent.Here.BattlefieldObjectId)
                    && ResolveCurrentFieldUnitPower(state, c) <= UnitEventSpec(item.UnitEvent.CardNo)!.Amount,
                _ => false
            })
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, c.ObjectId))
            .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();
    private static PendingCardChoiceState UnitEventChoice(MatchState state, StackItemState item)
    {
        var legal = UnitEventTargets(state, item);
        return new("UNIT-EVENT:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId,
            UnitEventSpec(item.UnitEvent!.CardNo)!.Optional ? 0 : 1,
            UnitEventSpec(item.UnitEvent.CardNo)!.AnyNumberOfTargets ? legal.Length : 1, legal, legal,
            "选择单位技能的合法目标；确认后双方可以响应。", item.SourceObjectId, item.EffectKind)
            { ResolvingStackItemId = item.StackItemId };
    }
    private static ResolutionResult PrepareUnitEventTarget(ResolutionResult result, StackItemState item)
    {
        var choice = UnitEventChoice(result.State, item);
        if (choice.LegalObjectIds.Count == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var next = result.State with { PendingCardChoice = choice, ActivePlayerId = item.ControllerId, PriorityPlayerId = null };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }
    internal static bool ValidUnitEventChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item is null || !NeedsUnitEventTarget(item) || item.TargetGenerations is not null
            || !ValidUnitEvent(state, item.UnitEvent, item.EffectKind, item.SourceObjectId, item.CardNo)) return false;
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
        var context = item.UnitEvent!;
        if (!ValidUnitEvent(state, context, item.EffectKind, item.SourceObjectId, item.CardNo))
            throw new InvalidOperationException("Invalid captured unit event.");
        var spec = UnitEventSpec(context.CardNo)!;
        if (spec.Effect == "BANISH_HAND_GRAVEYARD_BURN") return ResolveBanishHandGraveyardBurn(state, item);
        if (spec.Effect == "BURN_MIGHT_FRIENDLY") return ResolveBurnMightAbility(state, item);

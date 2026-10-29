using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmpowerInteractionContext(string CardNo, ObjectBinding Source, long CapturedTick, long FaceRevision,
    ObjectLocationState? Here = null, RuleTextInstanceBinding? Origin = null, string? TokenFactoryCardNo = null,
    ObjectBinding? RemovedEmpower = null, string? Opponent = null, bool OpponentConfirmed = false,
    long? ReflexiveTick = null, string? RemovedEmpowerTokenFactory = null);

public sealed partial class CoreRuleEngine
{
    internal const string EmpowerInteractionEffect = "CAPTURED_EMPOWER_INTERACTION";
    internal const string EmpowerRemovalCostWindow = "EMPOWER_REMOVAL_COST";
    internal const string EmpowerOpponentWindow = "EMPOWER_OPPONENT";
    private const string EmpowerInteractionCaptureKey = "empowerInteractionObservations";
    private sealed record EmpowerInteractionObservation(string Controller, EmpowerInteractionContext Context);
    private static EmpowerInteractionSpec? EmpowerInteractionDefinition(string? no)
        => no is not null && CardBehaviorRegistry.TryGetByCardNo(no, out var behavior) ? behavior.EmpowerInteraction : null;

    private static EmpowerInteractionObservation[] CaptureEmpowerInteractions(MatchState state,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState source, string timing)
        => source.IsFaceDown || !IsObjectOnField(state.PlayerZones, source.ObjectId) ? []
            : AttachedRuleTextRules.Instances(state.PlayerZones, cards, source)
                .Where(i => EmpowerInteractionDefinition(i.CardNo) is { } spec && spec.Timing == timing
                    && EmpowerRules.ControlledRunes(state, source.ControllerId!) >= spec.MinimumRunes)
                .Select(i => new EmpowerInteractionObservation(EffectiveFieldControllerId(state.PlayerZones, source.ObjectId, source),
                    new(i.CardNo, new(source.ObjectId, source.ObjectGeneration), 0, source.CopyFaceTransitions?.Count ?? 0,
                        timing == "STANDBY_PLAY" ? state.ObjectLocations.GetValueOrDefault(source.ObjectId)
                            ?? new(source.ControllerId!, "BATTLEFIELD", source.LastStandbyPlay!.Battlefield.ObjectId) : null,
                        i.Origin, source.TokenFactoryCardNo))).ToArray();

    private static GameEvent CaptureEmpowerInteractionPlay(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, CardObjectState entered)
    {
        if (ev.Kind is not ("UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD" or "UNIT_TOKEN_CREATED")) return ev;
        var state = MatchState.Create("capture") with { PlayerZones = zones, CardObjects = cards };
        var observations = CaptureEmpowerInteractions(state, cards, entered, "PLAY")
            .Concat(entered.LastStandbyPlay is null ? [] : CaptureEmpowerInteractions(state, cards, entered, "STANDBY_PLAY")).ToArray();
        return observations.Length == 0 ? ev : ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
            [EmpowerInteractionCaptureKey] = observations } };
    }

    private static ResolutionResult QueueEmpowerInteractions(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        result = RecordEmpowerSourceLocations(before, result);
        var events = result.Events.ToList(); var queue = result.State.TriggerQueue.ToList(); var found = false;
        foreach (var (ev, index) in result.Events.Select((ev, index) => (ev, index))) {
            if (ev.Payload.GetValueOrDefault(EmpowerInteractionCaptureKey) is not EmpowerInteractionObservation[] observations) continue;
            found = true;
            events[index] = ev with { Payload = ev.Payload.Where(p => p.Key != EmpowerInteractionCaptureKey).ToDictionary(p => p.Key, p => p.Value) };
            foreach (var (o, ordinal) in observations.Select((o, ordinal) => (o, ordinal))) {
                var source = o.Context.Source;
                if (o.Context.ReflexiveTick is null && ev.Kind != "OBJECT_EMPOWERED" && before.CardObjects.TryGetValue(source.ObjectId, out var old)
                    && !IsObjectOnField(before.PlayerZones, source.ObjectId)) source = source with { Generation = Math.Max(source.Generation, old.ObjectGeneration + 1) };
                var c = o.Context with { Source = source, CapturedTick = o.Context.CapturedTick == 0
                    ? RuleChoices.Value?.CompletionTick ?? result.State.Tick : o.Context.CapturedTick };
                var trigger = new TriggerQueueItemState($"empower-interaction-{c.CapturedTick}-{index}-{ordinal}", o.Controller,
                    source.ObjectId, EmpowerInteractionEffect, ev.Kind) { EmpowerInteraction = c, SourceCardNo = c.CardNo };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        if (!found) return result;
        var next = result.State with { TriggerQueue = queue };
        return result with { State = next, Events = events, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    private static ResolutionResult RecordEmpowerSourceLocations(MatchState before, ResolutionResult result)
    {
        EmpowerInteractionContext? Update(EmpowerInteractionContext? c)
        {
            if (c?.Here is null) return c;
            var here = CombatMightSourceLocation(result.State, c.Source.ObjectId, c.Source.Generation)
                ?? CombatMightSourceLocation(before, c.Source.ObjectId, c.Source.Generation);
            return here is null || here == c.Here ? c : c with { Here = here };
        }
        var next = result.State with {
            StackItems = result.State.StackItems.Select(i => i with { EmpowerInteraction = Update(i.EmpowerInteraction) }).ToArray(),
            TriggerQueue = result.State.TriggerQueue.Select(t => t with { EmpowerInteraction = Update(t.EmpowerInteraction) }).ToArray() };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static bool ValidEmpowerInteraction(MatchState state, EmpowerInteractionContext? c, string effect,
        string source, string controller, string? no = null)
    {
        if (c is null) return effect != EmpowerInteractionEffect;
        if (effect != EmpowerInteractionEffect || c.Source is not { Generation: >= 0 } || c.Source.ObjectId != source
            || c.CapturedTick <= 0 || c.CapturedTick > state.Tick || c.FaceRevision < 0 || no is not null && no != c.CardNo
            || !state.Seats.ContainsKey(controller) || EmpowerInteractionDefinition(c.CardNo) is not { } spec
            || (spec.Effect == "TEMPORARY_EMPOWER_HERE" ? c.Here is not { } here || !state.Seats.ContainsKey(here.PlayerId)
                || !(here.Zone == "BASE" && here.BattlefieldObjectId is null
                    || here.Zone == "BATTLEFIELD" && BattlefieldLocalRules.Battlefield(state, here.BattlefieldObjectId) is not null)
                || CombatMightSourceLocation(state, source, c.Source.Generation) is { } current && current != here : c.Here is not null)
            || spec.Effect != "TRANSFER_EMPOWER" && c.RemovedEmpower is not null
            || c.RemovedEmpower is null && c.RemovedEmpowerTokenFactory is not null
            || c.RemovedEmpower is { } paid && (paid.Generation < 0 || string.IsNullOrWhiteSpace(paid.ObjectId)
                || (state.CardObjects.TryGetValue(paid.ObjectId, out var removed) ? removed.ObjectGeneration < paid.Generation
                    : c.RemovedEmpowerTokenFactory is null || !P6TokenFactoryCatalog.TryGetByCardNo(c.RemovedEmpowerTokenFactory, out _)))
            || spec.Effect != "MILL_THEN_PLAY_OPPONENT_UNIT" && (c.Opponent is not null || c.OpponentConfirmed || c.ReflexiveTick is not null)
            || c.ReflexiveTick is { } reflexive && (!c.OpponentConfirmed || reflexive < c.CapturedTick || reflexive > state.Tick)
            || c.OpponentConfirmed != (c.Opponent is not null) || c.Opponent is not null && (c.Opponent == controller || !state.Seats.ContainsKey(c.Opponent))) return false;
        return c.Origin is { } origin ? AttachedRuleTextRules.ValidCaptured(state, origin, source, c.Source.Generation, c.CardNo)
            : PredictBindingMatches(state, c.Source, c.CardNo, c.FaceRevision, c.CapturedTick, c.TokenFactoryCardNo);
    }

    private static string[] EmpowerInteractionTargets(MatchState state, StackItemState item)
    {
        var c = item.EmpowerInteraction!; var spec = EmpowerInteractionDefinition(c.CardNo)!;
        var here = CombatMightSourceLocation(state, item.SourceObjectId, c.Source.Generation) ?? c.Here;
        return state.CardObjects.Values.Where(card => !card.IsFaceDown && (spec.Effect switch {
            "DISENCHANT_OR_DESTROY_EQUIPMENT" => card.Tags.Contains(CardObjectTags.EquipmentCard)
                && IsObjectOnField(state.PlayerZones, card.ObjectId) && card.ControllerId != item.ControllerId,
            "TRANSFER_EMPOWER" => (card.Tags.Contains(CardObjectTags.UnitCard) || card.Tags.Contains(CardObjectTags.EquipmentCard)
                    || state.PlayerZones.Values.Any(z => z.LegendZone.Contains(card.ObjectId)))
                && (IsObjectOnField(state.PlayerZones, card.ObjectId) || state.PlayerZones.Values.Any(z => z.LegendZone.Contains(card.ObjectId))),
            "TEMPORARY_EMPOWER_HERE" => IsObjectOnField(state.PlayerZones, card.ObjectId)
                && state.ObjectLocations.TryGetValue(card.ObjectId, out var loc) && loc.Zone == here?.Zone
                && (loc.Zone == "BASE" ? loc.PlayerId == here.PlayerId : loc.BattlefieldObjectId == here.BattlefieldObjectId),
            "PLAY_GRAVEYARD_UNIT" => state.PlayerZones[item.ControllerId].Graveyard.Contains(card.ObjectId)
                && IsEmpowerInteractionPlayableUnit(card, spec),
            "MILL_THEN_PLAY_OPPONENT_UNIT" when c.ReflexiveTick is not null => state.PlayerZones[c.Opponent!].Graveyard.Contains(card.ObjectId)
                && IsEmpowerInteractionPlayableUnit(card, spec),
            _ => false
        }) && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, card.ObjectId))
            .Select(card => card.ObjectId).Order(StringComparer.Ordinal).ToArray();
    }

    private static bool IsEmpowerInteractionPlayableUnit(CardObjectState card, EmpowerInteractionSpec spec)
        => card.Tags.Contains(CardObjectTags.UnitCard) && CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var behavior)
            && behavior.PlaysSourceToBaseAsUnit && (spec.MaximumMana is null || card.ManaCost <= spec.MaximumMana)
            && (spec.MaximumPower is null || PrintedPowerCostRules.ForCard(card.CardNo!).Amount <= spec.MaximumPower);

    private static string[] EmpowerRemovalChoices(MatchState state, StackItemState item)
        => state.CardObjects.Values.Where(card => card.IsEmpowered && !card.IsFaceDown && card.ControllerId == item.ControllerId
            && (IsObjectOnField(state.PlayerZones, card.ObjectId) || state.PlayerZones[item.ControllerId].LegendZone.Contains(card.ObjectId)))
            .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();

    private static PendingCardChoiceState EmpowerInteractionChoice(MatchState state, StackItemState item)
    {
        var c = item.EmpowerInteraction!; var effect = EmpowerInteractionDefinition(c.CardNo)!.Effect;
        var window = effect == "TRANSFER_EMPOWER" && c.RemovedEmpower is null ? EmpowerRemovalCostWindow
            : effect == "MILL_THEN_PLAY_OPPONENT_UNIT" && c.ReflexiveTick is null ? EmpowerOpponentWindow : "TRIGGER_CONFIRMATION";
        var legal = window == EmpowerRemovalCostWindow ? EmpowerRemovalChoices(state, item)
            : window == EmpowerOpponentWindow ? state.Seats.Keys.Where(p => p != item.ControllerId).Order(StringComparer.Ordinal).ToArray()
            : EmpowerInteractionTargets(state, item);
        return new("EMPOWER-INTERACTION:" + item.StackItemId, window, item.ControllerId,
            effect is "DISENCHANT_OR_DESTROY_EQUIPMENT" or "TRANSFER_EMPOWER" && window != EmpowerRemovalCostWindow
                || window == EmpowerOpponentWindow ? 1 : 0,
            1, legal, window == EmpowerOpponentWindow ? [item.SourceObjectId] : legal, window == EmpowerRemovalCostWindow ? "可解除一个受你控制的物体的强化以确认技能，或放弃。"
                : window == EmpowerOpponentWindow ? "选择一名对手；确认后双方可以响应，结算时该玩家燃烧，再选择是否打出其废牌堆中的单位。"
                : "选择强化技能的目标，或按提示放弃；确认目标及费用后双方可以响应。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };
    }

    private static bool NeedsEmpowerInteractionChoice(StackItemState item)
        => item.EmpowerInteraction is { } c && (!c.OpponentConfirmed || c.ReflexiveTick is not null) && item.TargetGenerations is null;

    private static ResolutionResult PrepareEmpowerInteraction(ResolutionResult result, StackItemState item)
    {
        var choice = EmpowerInteractionChoice(result.State, item);
        if (choice.LegalObjectIds.Count == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var next = result.State with { PendingCardChoice = choice, ActivePlayerId = item.ControllerId, PriorityPlayerId = null };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static bool ValidEmpowerInteractionChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item is null || !NeedsEmpowerInteractionChoice(item)
            || !ValidEmpowerInteraction(state, item.EmpowerInteraction, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo)) return false;
        var expected = EmpowerInteractionChoice(state, item);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow && choice.PlayerId == expected.PlayerId
            && choice.SourceObjectId == expected.SourceObjectId && choice.EffectKind == expected.EffectKind
            && choice.RequiredCount == expected.RequiredCount && choice.MaxCount == expected.MaxCount
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds) && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    private static ResolutionResult ResolveEmpowerInteractionChoice(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        if (!ValidEmpowerInteractionChoice(state, choice)) return RejectWithCorePrompts(state, "强化技能的选择上下文已失效。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == choice.ResolvingStackItemId);
        var next = state with { Tick = state.Tick + 1, PendingCardChoice = null };
        var events = new List<GameEvent>();
        if (selected.Count == 0) return DiscardUnconfirmedTrigger(new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next)), item);
        var c = item.EmpowerInteraction!;
        if (choice.ChoiceWindow == EmpowerOpponentWindow) {
            item = item with { EmpowerInteraction = c with { Opponent = selected.Single(), OpponentConfirmed = true } };
            next = RestoreAfterTriggerConfirmation(next with { StackItems = next.StackItems.Select(i => i.StackItemId == item.StackItemId ? item : i).ToArray() }, item);
        } else {
            var card = state.CardObjects[selected.Single()];
            var changed = ApplyEmpowerStatusAction(next, new(card.ObjectId, card.ObjectGeneration), false, item.ControllerId);
            next = next with { CardObjects = changed.CardObjects }; events.AddRange(changed.Events);
            item = item with { EmpowerInteraction = c with { RemovedEmpower = new(card.ObjectId, card.ObjectGeneration),
                RemovedEmpowerTokenFactory = IsTokenObject(card) ? card.TokenFactoryCardNo ?? card.CardNo : null } };
            next = next with { StackItems = next.StackItems.Select(i => i.StackItemId == item.StackItemId ? item : i).ToArray() };
        }
        events.Add(new("TRIGGER_COST_OR_OPPONENT_CONFIRMED", "确认强化技能的费用或对手", new Dictionary<string, object?> {
            ["sourceObjectId"] = item.SourceObjectId, ["selectedIds"] = selected.ToArray() }));
        return PrepareTriggerConfirmation(new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next)));
    }

    private static CardBehaviorDefinition EmpowerInteractionPlayBehavior(EmpowerInteractionContext c)
        => new(c.CardNo, "强化触发：从废牌堆打出单位", 0, EmpowerInteractionEffect, 0, 0,
            EffectPlaySourceZone: "GRAVEYARD", EffectPlayIgnoreBaseMana: true, EffectPlayIgnoreBasePower: true,
            EffectPlayDestination: c.Opponent is null ? "BASE" : "ANY", EffectPlayOptional: true,
            MaxTargetManaCost: EmpowerInteractionDefinition(c.CardNo)!.MaximumMana ?? 0,
            MaxTargetPowerCost: EmpowerInteractionDefinition(c.CardNo)!.MaximumPower);

    private static StackResolutionResult ResolveEmpowerInteraction(MatchState state, StackItemState item)
    {
        if (!ValidEmpowerInteraction(state, item.EmpowerInteraction, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo))
            throw new InvalidOperationException("Invalid captured empower interaction.");
        if (item.EffectPlayCompleted) return NoopStackResolutionResult(state);
        var c = item.EmpowerInteraction!; var spec = EmpowerInteractionDefinition(c.CardNo)!;
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var events = new List<GameEvent>();
        if (spec.Effect == "MILL_THEN_PLAY_OPPONENT_UNIT" && c.ReflexiveTick is null) {
            var owner = c.Opponent!;
            ApplyMillToPlayer(zones, cards, owner, spec.MillCount, item.SourceObjectId, events);
            // CN 387: the embedded trigger is confirmed after its parent finishes,
            // with its own targets and response window, even if no cards burned.
            events.Add(new("EMPOWER_MILL_COMPLETED", "燃烧指示完成，内嵌打出技能等待确认", new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId,
                [EmpowerInteractionCaptureKey] = new[] { new EmpowerInteractionObservation(item.ControllerId,
                    c with { ReflexiveTick = RuleChoices.Value?.CompletionTick ?? state.Tick + 1 }) } }));
            return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
        }
        var id = item.TargetObjectIds.SingleOrDefault();
        if (id is null || !StackObjectRules.ReferencesCurrentObject(state, item, id) || !EmpowerInteractionTargets(state, item).Contains(id))
            return NoopStackResolutionResult(state);
        if (spec.Effect is "PLAY_GRAVEYARD_UNIT" or "MILL_THEN_PLAY_OPPONENT_UNIT")
            return BeginEffectPlay(state, item, EmpowerInteractionPlayBehavior(c));
        if (spec.Effect == "DISENCHANT_OR_DESTROY_EQUIPMENT" && !cards[id].IsEmpowered) {
            var removal = ResolveFieldDestructions(state, zones, cards, item, new HashSet<string>(), state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(),
                state.RunePools, objectLocations: state.ObjectLocations, explicitDestroyObjectIds: new HashSet<string> { id });
            return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, RunePools = removal.RunePools,
                TriggerQueue = removal.TriggerQueue, Events = removal.Events, DestroyedUnitOwnerIds = removal.DestroyedUnitOwnerIds,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
        }
        var empowered = spec.Effect != "DISENCHANT_OR_DESTROY_EQUIPMENT";
        var result = ApplyEmpowerStatusAction(state, new(id, cards[id].ObjectGeneration), empowered, item.ControllerId);
        if (spec.Effect != "TEMPORARY_EMPOWER_HERE") return result;
        var effects = state.UntilEndOfTurnEffects.ToList();
        ScheduleDelayedEmpower(state, item, id, false, effects, events);
        return result with { UntilEndOfTurnEffects = effects, Events = result.Events.Concat(events).ToArray() };
    }

    internal static bool ValidEmpowerInteractionPlay(MatchState state, PendingEffectPlayState p)
        => p.Parent.EmpowerInteraction is { } c && ValidEmpowerInteraction(state, c, p.Parent.EffectKind,
                p.Parent.SourceObjectId, p.Parent.ControllerId, p.Parent.CardNo)
            && EmpowerInteractionDefinition(c.CardNo) is { Effect: "PLAY_GRAVEYARD_UNIT" or "MILL_THEN_PLAY_OPPONENT_UNIT" } spec
            && p.PlayerId == p.Parent.ControllerId && p.SourcePlayerId == c.Opponent
            && (c.Opponent is null || c.ReflexiveTick is not null)
            && p.SourceZone == "GRAVEYARD" && !p.IgnoreAllCosts && p.IgnoreBaseMana && p.IgnoreBasePower
            && p.ManaReduction == 0 && p.DestinationPolicy == (c.Opponent is null ? "BASE" : "ANY") && p.Optional
            && p.ViewedCardIds is null && p.RevealedHand is null && p.SourceReplay is null && !p.Parent.EffectPlayCompleted
            && p.Sources.Count > 0 && p.Sources.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(p.Parent.TargetObjectIds)
            && state.PlayerZones.TryGetValue(c.Opponent ?? p.PlayerId, out var zones)
            && p.Sources.All(s => zones.Graveyard.Contains(s.Key) && state.CardObjects.TryGetValue(s.Key, out var card)
                && card.ObjectGeneration == s.Value && IsEmpowerInteractionPlayableUnit(card, spec));

    internal static bool IsForeignGraveyardEffectPlay(MatchState state, string player, string source)
        => state.PendingEffectPlay is { SourcePlayerId: not null } p && p.PlayerId == player
            && p.Sources.ContainsKey(source) && ValidEmpowerInteractionPlay(state, p);
}

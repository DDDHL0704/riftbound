using Riftbound.Contracts;
using System.Text.Json.Serialization;

namespace Riftbound.Engine;

public sealed record TurnDeathLedger(int TurnNumber, IReadOnlyDictionary<string,int> Counts)
{
    public IReadOnlyList<string> PermanentControllers { get; init; } = [];
}
public sealed record DestroyedUnitFact(string ObjectId, string CardNo, string OwnerId, string ControllerId, long Generation, bool WasMinion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool WasStunned = false);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeathObserverContext(string CardNo, string Kind, long SourceGeneration, DestroyedUnitFact Destroyed)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RuleTextInstanceId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuleTextInstanceBinding? Origin { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ObjectLocationState? SourceLocation { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ObjectLocationState? DestroyedLocation { get; init; }
}
internal sealed record CapturedDeathObserver(string ObjectId, string ControllerId, DeathObserverContext Context);
internal sealed record CapturedUnitDeath(string Id, int Batch, int TurnNumber, DestroyedUnitFact Unit, IReadOnlyList<CapturedDeathObserver> Observers)
{
    public IReadOnlyList<LastBreathUnitCreationContext> UnitCreations { get; init; } = [];
    public IReadOnlyList<LastBreathRuneCallContext> RuneCalls { get; init; } = [];
    public IReadOnlyList<LastBreathDrawContext> Draws { get; init; } = [];
    public IReadOnlyList<LastBreathBattlefieldDamageContext> BattlefieldDamages { get; init; } = [];
    public IReadOnlyList<LastBreathMightContext> LastBreathMights { get; init; } = [];
    public DravenBattleDeathScoreContext? DravenBattleDeathScore { get; init; }
    public DestructionAttribution? Attribution { get; init; }
    public IReadOnlyList<CapturedGraveyardPlay> GraveyardPlays { get; init; } = [];
}

public sealed partial class CoreRuleEngine
{
    internal const string DeathObserverEffect = "FRIENDLY_UNIT_DEATH_OBSERVER";
    internal static bool IsDeathObserverKind(string kind) => kind is TriggerKinds.UnitFriendlyDestroyedPowerUntilEndOfTurn
        or TriggerKinds.UnitFriendlyDestroyedGainExperience or TriggerKinds.UnitFirstFriendlyDestroyedDrawOne
        or TriggerKinds.UnitDestroyedNonMinionCreateMinion
        or TriggerKinds.UnitEnemyDestroyedHereOncePerTurnCallRune or TriggerKinds.UnitStunnedEnemyDestroyedHereDraw
        or TriggerKinds.UnitFriendlyBatchDestroyedBoon;

    private static bool MatchingLocalDeath(DeathObserverContext c, string controller)
        => !string.IsNullOrWhiteSpace(c.Destroyed.ControllerId) && c.Destroyed.ControllerId != controller
            && c.SourceLocation is { } source && c.DestroyedLocation is { } target
            && !string.IsNullOrWhiteSpace(source.PlayerId) && !string.IsNullOrWhiteSpace(target.PlayerId)
            && source.Zone == target.Zone && (source.Zone == "BASE" ? source.PlayerId == target.PlayerId
                : source.Zone == "BATTLEFIELD" && !string.IsNullOrWhiteSpace(source.BattlefieldObjectId)
                    && source.BattlefieldObjectId == target.BattlefieldObjectId);

    private static string OncePerTurnDeathObserverMarker(CapturedDeathObserver observer)
        => $"DEATH_OBSERVER_ONCE:{observer.ObjectId}:{observer.Context.SourceGeneration}:{observer.Context.Kind}:{observer.Context.RuleTextInstanceId ?? "PRINTED"}";

    internal static bool ValidDeathObserver(DeathObserverContext c, string effect, string controller, string source, string? cardNo=null)
        => effect == DeathObserverEffect && c.SourceGeneration >= 0 && (cardNo is null || cardNo == c.CardNo)
            && c.Destroyed is { Generation: >= 0 } d && !string.IsNullOrWhiteSpace(d.ObjectId) && d.ObjectId != source
            && !string.IsNullOrWhiteSpace(d.CardNo) && !string.IsNullOrWhiteSpace(d.OwnerId)
            && (c.Kind is TriggerKinds.UnitEnemyDestroyedHereOncePerTurnCallRune or TriggerKinds.UnitStunnedEnemyDestroyedHereDraw
                ? MatchingLocalDeath(c, controller) && (c.Kind != TriggerKinds.UnitStunnedEnemyDestroyedHereDraw || d.WasStunned)
                : d.ControllerId == controller && c.SourceLocation is null && c.DestroyedLocation is null)
            && IsDeathObserverKind(c.Kind) && UnitDestroyedTriggerSpecRules.TryGetTrigger(c.CardNo,t=>t.Kind==c.Kind,out _)
            && (c.Kind != TriggerKinds.UnitDestroyedNonMinionCreateMinion || !d.WasMinion)
            && (c.Origin is null ? c.RuleTextInstanceId is null
                : c.Origin.Unit is { } unit && unit.ObjectId == source && unit.Generation == c.SourceGeneration
                    && c.Origin.CardNo == c.CardNo && c.Origin.Equipment is { Generation: >= 0 } equipment
                    && c.Origin.Revision > 0 && c.RuleTextInstanceId == $"ATTACHED:{equipment.ObjectId}:{equipment.Generation}:{c.Origin.Revision}");

    internal static bool ValidDeathObserver(MatchState state, DeathObserverContext c, string effect,
        string controller, string source, string? cardNo = null)
        => ValidDeathObserver(c, effect, controller, source, cardNo)
            && (c.Kind is not (TriggerKinds.UnitEnemyDestroyedHereOncePerTurnCallRune or TriggerKinds.UnitStunnedEnemyDestroyedHereDraw)
                || state.Seats.ContainsKey(controller) && state.Seats.ContainsKey(c.Destroyed.ControllerId))
            && AttachedRuleTextRules.ValidCaptured(state, c.Origin, source, c.SourceGeneration, c.CardNo);

    // The public death fact is unchanged. Grant bindings belong to authority;
    // the equipment may since have moved into a private zone.
    internal static DeathObserverContext? PublicDeathObserver(DeathObserverContext? c)
        => c is null || c.Origin is null ? c : c with { RuleTextInstanceId = null, Origin = null };

    internal static string DeathObserverLabel(DeathObserverContext c)
    {
        var source=CardBehaviorRegistry.TryGetByCardNo(c.CardNo,out var card) ? card.DisplayName : c.CardNo;
        var victim=CardBehaviorRegistry.TryGetByCardNo(c.Destroyed.CardNo,out var unit) ? unit.DisplayName : P6TokenFactoryCatalog.TryGetByCardNo(c.Destroyed.CardNo,out var token) ? token.CardName : c.Destroyed.CardNo;
        var effect=c.Kind switch { TriggerKinds.UnitDestroyedNonMinionCreateMinion=>"打出随从",TriggerKinds.UnitFriendlyDestroyedGainExperience=>"获得经验",TriggerKinds.UnitFirstFriendlyDestroyedDrawOne=>"本回合首次死亡：抽牌",TriggerKinds.UnitEnemyDestroyedHereOncePerTurnCallRune=>"召出休眠符文", TriggerKinds.UnitStunnedEnemyDestroyedHereDraw=>"眩晕敌方单位被摧毁：抽牌", TriggerKinds.UnitFriendlyBatchDestroyedBoon=>"友方单位被摧毁：给予增益", _=>"本回合战力 +2" };
        return $"{source}：{effect}（{victim}被摧毁）";
    }

    private static bool NeedsDeathObserverTarget(StackItemState item)
        => item.DeathObserver?.Kind == TriggerKinds.UnitFriendlyBatchDestroyedBoon;

    private static string[] DeathObserverTargets(MatchState state, StackItemState item)
        => state.CardObjects.Values.Where(c => IsFaceUpNonStandbyUnit(c) && IsObjectOnField(state.PlayerZones, c.ObjectId)
            && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == item.ControllerId
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, c.ObjectId))
            .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();

    private static PendingCardChoiceState DeathObserverChoice(MatchState state, StackItemState item)
    {
        var legal = DeathObserverTargets(state, item);
        return new("DEATH-OBSERVER:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId, 1, 1,
            legal, legal, "选择一名友方单位获得增益；确认后双方可以响应。", item.SourceObjectId, item.EffectKind)
            { ResolvingStackItemId = item.StackItemId };
    }

    private static ResolutionResult PrepareDeathObserverTarget(ResolutionResult result, StackItemState item)
    {
        var choice = DeathObserverChoice(result.State, item);
        if (choice.LegalObjectIds.Count == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var state = result.State with { PendingCardChoice = choice, ActivePlayerId = item.ControllerId, PriorityPlayerId = null };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    internal static bool ValidDeathObserverTargets(StackItemState item)
        => item.TargetGenerations is null ? item.TargetObjectIds.Count == 0
            : NeedsDeathObserverTarget(item) && item.TargetObjectIds.Count == 1 && item.TargetGenerations.Count == 1
                && item.TargetGenerations.TryGetValue(item.TargetObjectIds[0], out var generation) && generation >= 0;

    internal static bool ValidDeathObserverChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item?.DeathObserver is not { } c || !NeedsDeathObserverTarget(item) || item.TargetGenerations is not null
            || !ValidDeathObserver(state, c, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo)) return false;
        var expected = DeathObserverChoice(state, item);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow
            && choice.PlayerId == expected.PlayerId && choice.SourceObjectId == expected.SourceObjectId && choice.EffectKind == expected.EffectKind
            && choice.RequiredCount == 1 && choice.MaxCount == 1 && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds)
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds);
    }

    private static CapturedUnitDeath? CaptureUnitDeath(MatchState state, IReadOnlyDictionary<string,PlayerZones> zones,
        IReadOnlyDictionary<string,CardObjectState> cards, CardObjectState unit)
    {
        if (!unit.Tags.Contains(CardObjectTags.UnitCard) || unit.IsFaceDown) return null;
        var frame=RuleChoices.Value!;
        var controller=EffectiveFieldControllerId(zones,unit.ObjectId,unit);
        var fact=new DestroyedUnitFact(unit.ObjectId,unit.CardNo ?? "",unit.OwnerId ?? controller,controller,unit.ObjectGeneration,unit.Tags.Contains(CardObjectTags.MinionTokenFamily), IsStunnedForBattle(unit));
        var observers=new List<CapturedDeathObserver>();
        var locations = frame.CurrentDestructionLocations ?? state.ObjectLocations;
        // CN 383.2.c.2: an observer leaving in the same action does not trigger.
        // A nested resource cost has its own destruction batch, before the outer death.
        foreach(var source in cards.Values.OrderBy(c=>c.ObjectId,StringComparer.Ordinal))
            if (source.ObjectId != unit.ObjectId && !frame.CurrentDestructions.Contains(source.ObjectId)
                && source.Tags.Contains(CardObjectTags.UnitCard) && !source.IsFaceDown
                && IsObjectOnField(zones,source.ObjectId))
                foreach (var instance in AttachedRuleTextRules.Instances(zones, cards, source))
                    if (!(instance.Origin is { } origin && frame.CurrentDestructions.Contains(origin.Equipment.ObjectId))
                        && UnitDestroyedTriggerSpecRules.TryGetTrigger(instance.CardNo, t => IsDeathObserverKind(t.Kind), out var spec)
                        && (spec.Kind != TriggerKinds.UnitDestroyedNonMinionCreateMinion || !fact.WasMinion))
                    {
                        var observerController = EffectiveFieldControllerId(zones, source.ObjectId, source);
                        var localEnemy = spec.Kind is TriggerKinds.UnitEnemyDestroyedHereOncePerTurnCallRune or TriggerKinds.UnitStunnedEnemyDestroyedHereDraw;
                        if (spec.Kind == TriggerKinds.UnitStunnedEnemyDestroyedHereDraw && !fact.WasStunned) continue;
                        if (localEnemy ? observerController == controller || !IsSameFieldLocation(zones, locations, unit.ObjectId, source.ObjectId)
                            : observerController != controller) continue;
                        ObjectLocationState? observerLocation = null, destroyedLocation = null;
                        if (localEnemy)
                        {
                            if (!TryGetPreciseFieldLocation(zones, locations, source.ObjectId, out var capturedSource)
                                || !TryGetPreciseFieldLocation(zones, locations, unit.ObjectId, out var capturedDestroyed)) continue;
                            observerLocation = capturedSource;
                            destroyedLocation = capturedDestroyed;
                        }
                        observers.Add(new(source.ObjectId, observerController,
                            new(instance.CardNo, spec.Kind, source.ObjectGeneration, fact) {
                                RuleTextInstanceId = instance.Origin is null ? null : instance.Id, Origin = instance.Origin,
                                SourceLocation = observerLocation, DestroyedLocation = destroyedLocation }));
                    }
        var deathId = $"death-{frame.Origin.Tick}-{frame.DeathSequence++}";
        return new(deathId,frame.CurrentDestructionBatch ?? frame.DestructionBatchSequence++,state.TurnNumber,fact,observers) {
            UnitCreations = CaptureLastBreathUnitCreations(state, zones, cards, unit, deathId),
            RuneCalls = CaptureLastBreathRuneCalls(state, zones, cards, unit, deathId),
            Draws = CaptureLastBreathDraws(state, zones, cards, unit, deathId),
            BattlefieldDamages = CaptureLastBreathBattlefieldDamage(state, zones, cards, unit, deathId),
            DravenBattleDeathScore = CaptureDravenBattleDeathScore(state, zones, cards,
                frame.CurrentDestructionLocations ?? state.ObjectLocations, unit, deathId, state.Tick + 1),
            LastBreathMights = CaptureLastBreathMight(state, zones, cards, unit), GraveyardPlays = CaptureGraveyardPlays(zones, cards, unit) };
    }

    private static ResolutionResult RecordDeathObservers(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        result = QueueBombDestructionObservers(result);
        result = QueueEquipmentDestructionObservers(result);
        var state=result.State;
        var ledger = RuleChoices.Value?.ResourceDeathLedger ?? before.DeathLedger;
        var counts=(ledger.TurnNumber==state.TurnNumber ? ledger.Counts : new Dictionary<string,int>())
            .ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
        var events=new List<GameEvent>(); var queue=state.TriggerQueue.ToList(); var seen=new HashSet<string>();
        var effects = state.UntilEndOfTurnEffects;
        var permanentControllers = (ledger.TurnNumber == state.TurnNumber ? ledger.PermanentControllers : [])
            .Concat(RuleChoices.Value?.DestroyedPermanentControllers.Where(entry => entry.TurnNumber == state.TurnNumber)
                .Select(entry => entry.ControllerId) ?? [])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var deaths = result.Events.Select(e => e.Payload.GetValueOrDefault("capturedUnitDeath"))
            .OfType<CapturedUnitDeath>().DistinctBy(d => d.Id).Where(d => d.TurnNumber == state.TurnNumber).ToArray();
        var firstChoices = new Dictionary<(int Batch, string Source, string? Instance), string>();
        var visitedBatches = new HashSet<int>();
        var observedBatches = new HashSet<(int Batch, string Source, string? Instance)>();
        foreach(var ev in result.Events)
        {
            events.Add(ev.Payload.ContainsKey("capturedUnitDeath") ? ev with { Payload=ev.Payload.Where(p=>p.Key!="capturedUnitDeath").ToDictionary(p=>p.Key,p=>p.Value) } : ev);
            if (!ev.Payload.TryGetValue("capturedUnitDeath",out var raw) || raw is not CapturedUnitDeath death || !seen.Add(death.Id)) continue;
            if (death.TurnNumber != state.TurnNumber) continue;
            if (death.DravenBattleDeathScore is not null)
                events[^1] = events[^1] with { Payload = new Dictionary<string, object?>(events[^1].Payload)
                    { ["destroyedUnitFact"] = death.Unit, ["deathId"] = death.Id, ["deathBatch"] = death.Batch } };
            if (visitedBatches.Add(death.Batch) && state.Status == MatchStatuses.InProgress)
            {
                // CN 383.1.b: choose the occurrence when the first event happens simultaneously.
                var candidates = deaths.Where(d => d.Batch == death.Batch)
                    .SelectMany(d => d.Observers.Where(o => o.Context.Kind == TriggerKinds.UnitFirstFriendlyDestroyedDrawOne)
                        .Select(o => (Death: d, Observer: o)))
                    .GroupBy(x => (x.Observer.ObjectId, x.Observer.Context.RuleTextInstanceId));
                foreach (var group in candidates)
                {
                    var observer = group.First().Observer;
                    if (counts.GetValueOrDefault(observer.ControllerId) > 0) continue;
                    var options = group.Select(x => new RuleChoiceOption(x.Death.Id,
                        DeathObserverLabel(x.Observer.Context), [x.Death.Unit.ObjectId, observer.ObjectId])).ToArray();
                    firstChoices[(death.Batch, group.Key.ObjectId, group.Key.RuleTextInstanceId)] = options.Length == 1 ? options[0].Id
                        : RuleChoices.Value!.Choose(observer.ControllerId, "选择本次同时死亡中的一次，触发每回合首次死亡技能。", options, kind: "FIRST_DEATH_OCCURRENCE");
                }
            }
            var prior=counts.GetValueOrDefault(death.Unit.ControllerId);
            counts[death.Unit.ControllerId]=prior+1;
            if (state.Status!=MatchStatuses.InProgress) continue;
            foreach (var captured in death.UnitCreations)
            {
                var context = captured with { CreatedAtTick = RuleChoices.Value?.CompletionTick ?? state.Tick };
                var trigger = LastBreathUnitCreationTrigger(context);
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
            foreach (var captured in death.RuneCalls)
            {
                var context = captured with { CreatedAtTick = RuleChoices.Value?.CompletionTick ?? state.Tick };
                var trigger = LastBreathRuneCallTrigger(context);
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
            foreach (var captured in death.Draws)
            {
                var context = captured with { CreatedAtTick = RuleChoices.Value?.CompletionTick ?? state.Tick };
                var trigger = LastBreathDrawTrigger(context);
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
            foreach (var captured in death.BattlefieldDamages)
            {
                var context = captured with { CreatedAtTick = RuleChoices.Value?.CompletionTick ?? state.Tick };
                var trigger = LastBreathBattlefieldDamageTrigger(context);
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
            QueueGraveyardPlays(state, death, deaths, queue, events);
            if (death.DravenBattleDeathScore is { } dravenDeath)
            {
                // An optional replacement may suspend the parent instruction; its
                // committed death belongs to this accepted completion tick.
                dravenDeath = dravenDeath with { DeathTick = RuleChoices.Value?.CompletionTick ?? state.Tick };
                var trigger = BuildDravenBattleDeathScoreTrigger(dravenDeath);
                queue.Add(trigger);
                events.Add(BuildTriggerQueuedEvent(trigger) with { Payload = new Dictionary<string, object?>(BuildTriggerQueuedEvent(trigger).Payload)
                    { ["dravenBattleDeathScore"] = dravenDeath, ["deathId"] = death.Id, ["deathBatch"] = death.Batch,
                        ["enqueuedAtTick"] = dravenDeath.DeathTick } });
            }
            foreach (var captured in death.LastBreathMights.Where(c => UnitDestroyedTriggerSpecRules.TryGetTrigger(c.CardNo,
                UnitDestroyedTriggerSpecRules.IsLastBreathPowerfulDrawTrigger, out var spec)
                && c.Might >= spec.RequiredPowerThreshold.GetValueOrDefault(PowerfulUnitPowerThreshold)))
            {
                var trigger = new TriggerQueueItemState(
                    $"TRIGGER-{death.Id}{(captured.Origin is null ? "" : ":" + captured.InstanceId)}-{death.Unit.ObjectId}-{TriggerKinds.UnitLastBreathPowerfulDraw}",
                    captured.ControllerId, death.Unit.ObjectId, TriggerKinds.UnitLastBreathPowerfulDraw, "UNIT_DESTROYED") {
                    SourceCardNo = captured.CardNo, LastBreathMight = captured };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
            foreach(var observer in death.Observers)
            {
                if (observer.Context.Kind == TriggerKinds.UnitFriendlyBatchDestroyedBoon
                    && !observedBatches.Add((death.Batch, observer.ObjectId, observer.Context.RuleTextInstanceId))) continue;
                if (observer.Context.Kind==TriggerKinds.UnitFirstFriendlyDestroyedDrawOne
                    && firstChoices.GetValueOrDefault((death.Batch, observer.ObjectId, observer.Context.RuleTextInstanceId)) != death.Id) continue;
                if (observer.Context.Kind == TriggerKinds.UnitEnemyDestroyedHereOncePerTurnCallRune)
                {
                    var marker = OncePerTurnDeathObserverMarker(observer);
                    if (effects.Contains(marker, StringComparer.Ordinal)) continue;
                    effects = AddUntilEndOfTurnEffect(effects, marker);
                }
                var instanceSuffix = observer.Context.RuleTextInstanceId is { } instance ? ":" + instance : "";
                var trigger=new TriggerQueueItemState($"{(instanceSuffix.Length == 0 ? "" : "copied-")}{death.Id}-{observer.ObjectId}{instanceSuffix}",observer.ControllerId,observer.ObjectId,
                    DeathObserverEffect,TriggerTimings.UnitDestroyed) { DeathObserver=observer.Context,SourceCardNo=observer.Context.CardNo };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        state=state with { DeathLedger=new(state.TurnNumber,counts) { PermanentControllers = permanentControllers },TriggerQueue=queue,UntilEndOfTurnEffects=effects };
        return result with { State=state,Events=events,Snapshots=ResolutionResult.BuildSnapshots(state),Prompts=BuildCorePrompts(state) };
    }

    private static StackResolutionResult ResolveDeathObserver(MatchState state, StackItemState item)
    {
        var c=item.DeathObserver!;
        if (!ValidDeathObserver(state,c,item.EffectKind,item.ControllerId,item.SourceObjectId,item.CardNo)
            || !UnitDestroyedTriggerSpecRules.TryGetTrigger(c.CardNo,t=>t.Kind==c.Kind,out var spec))
            throw new InvalidOperationException("Invalid captured death observer.");
        var resolved=BuildTriggerResolvedEvent(new TriggerQueueItemState(item.StackItemId,item.ControllerId,item.SourceObjectId,item.EffectKind,TriggerTimings.UnitDestroyed) { DeathObserver=c });
        if (c.Kind == TriggerKinds.UnitEnemyDestroyedHereOncePerTurnCallRune)
        {
            var zones = NormalizeZonesForSeats(state);
            var callCards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var called = CallRunes(zones, callCards, item.ControllerId, spec.RuneCallCount.GetValueOrDefault(1));
            var calledEvent = new GameEvent("RUNES_CALLED", "敌方单位被摧毁后召出休眠符文", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                ["count"] = called.CalledRuneObjectIds.Count, ["runeObjectIds"] = called.CalledRuneObjectIds,
                ["reason"] = c.Kind });
            return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = callCards, Events = [resolved, calledEvent] };
        }
        if (c.Kind==TriggerKinds.UnitDestroyedNonMinionCreateMinion) {
            var batch=CreateUnitTokenBatch(state,item,P6TokenFactoryCatalog.ZaunMinionTokenCardNo,1+AdditionalUnitTokens(item),false,abilityId:c.Kind);
            return batch with { Events=new[] { resolved }.Concat(batch.Events).ToArray() };
        }
        if (c.Kind is TriggerKinds.UnitFirstFriendlyDestroyedDrawOne or TriggerKinds.UnitStunnedEnemyDestroyedHereDraw) {
            var draw=ResolveLastBreathDrawStackItem(state,item,spec.DrawCount.GetValueOrDefault(1));
            return draw with { Events=draw.Events.Select(e=>e.Kind=="TRIGGER_RESOLVED" ? resolved : e).ToArray() };
        }
        var events=new List<GameEvent> { resolved };
        if (c.Kind == TriggerKinds.UnitFriendlyBatchDestroyedBoon)
        {
            var boonCards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            if (item.TargetObjectIds.SingleOrDefault() is { } target && DeathObserverTargets(state, item).Contains(target)
                && item.TargetGenerations?.GetValueOrDefault(target, -1) == boonCards[target].ObjectGeneration)
            {
                using var instruction = new MightInstructionScope(state, state.PlayerZones, boonCards, events);
                GrantLegendBoon(boonCards, target, item.ControllerId, item.SourceObjectId, item.EffectKind, events);
            }
            return NoopStackResolutionResult(state) with { CardObjects = boonCards, Events = events };
        }
        if (c.Kind==TriggerKinds.UnitFriendlyDestroyedGainExperience)
            return NoopStackResolutionResult(state) with { PlayerExperience=GainExperience(NormalizeExperienceForSeats(state),item.ControllerId,
                spec.ExperienceCount.GetValueOrDefault(1),item,events,item.SourceObjectId,c.CardNo),Events=events };
        var cards=state.CardObjects.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
        // The earned skill survives a control change, but cannot buff a new incarnation.
        if (cards.TryGetValue(item.SourceObjectId,out var source) && source.ObjectGeneration==c.SourceGeneration
            && IsObjectOnField(state.PlayerZones,source.ObjectId) && source.Tags.Contains(CardObjectTags.UnitCard))
        {
            var behavior=new CardBehaviorDefinition(c.CardNo,"幽魂半人马",0,c.Kind,0,0,PowerModifierAmount:spec.PowerDelta.GetValueOrDefault());
            cards[source.ObjectId]=ApplyPowerModifier(state, source,behavior,item,source.ObjectId,behavior.PowerModifierAmount,out var powerEvent);
            events.Add(powerEvent);
        }
        return NoopStackResolutionResult(state) with { CardObjects=cards,Events=events };
    }
}

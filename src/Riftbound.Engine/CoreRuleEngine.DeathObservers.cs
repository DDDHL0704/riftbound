using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record TurnDeathLedger(int TurnNumber, IReadOnlyDictionary<string,int> Counts);
public sealed record DestroyedUnitFact(string ObjectId, string CardNo, string OwnerId, string ControllerId, long Generation, bool WasMinion);
public sealed record DeathObserverContext(string CardNo, string Kind, long SourceGeneration, DestroyedUnitFact Destroyed);
internal sealed record CapturedDeathObserver(string ObjectId, string ControllerId, DeathObserverContext Context);
internal sealed record CapturedUnitDeath(string Id, int Batch, int TurnNumber, DestroyedUnitFact Unit, IReadOnlyList<CapturedDeathObserver> Observers);

public sealed partial class CoreRuleEngine
{
    internal const string DeathObserverEffect = "FRIENDLY_UNIT_DEATH_OBSERVER";
    internal static bool IsDeathObserverKind(string kind) => kind is TriggerKinds.UnitFriendlyDestroyedPowerUntilEndOfTurn
        or TriggerKinds.UnitFriendlyDestroyedGainExperience or TriggerKinds.UnitFirstFriendlyDestroyedDrawOne
        or TriggerKinds.UnitDestroyedNonMinionCreateMinion;

    internal static bool ValidDeathObserver(DeathObserverContext c, string effect, string controller, string source, string? cardNo=null)
        => effect == DeathObserverEffect && c.SourceGeneration >= 0 && (cardNo is null || cardNo == c.CardNo)
            && c.Destroyed is { Generation: >= 0 } d && !string.IsNullOrWhiteSpace(d.ObjectId) && d.ObjectId != source
            && !string.IsNullOrWhiteSpace(d.CardNo) && !string.IsNullOrWhiteSpace(d.OwnerId) && d.ControllerId == controller
            && IsDeathObserverKind(c.Kind) && UnitDestroyedTriggerSpecRules.TryGetTrigger(c.CardNo,t=>t.Kind==c.Kind,out _)
            && (c.Kind != TriggerKinds.UnitDestroyedNonMinionCreateMinion || !d.WasMinion);

    internal static string DeathObserverLabel(DeathObserverContext c)
    {
        var source=CardBehaviorRegistry.TryGetByCardNo(c.CardNo,out var card) ? card.DisplayName : c.CardNo;
        var victim=CardBehaviorRegistry.TryGetByCardNo(c.Destroyed.CardNo,out var unit) ? unit.DisplayName : P6TokenFactoryCatalog.TryGetByCardNo(c.Destroyed.CardNo,out var token) ? token.CardName : c.Destroyed.CardNo;
        var effect=c.Kind switch { TriggerKinds.UnitDestroyedNonMinionCreateMinion=>"打出随从",TriggerKinds.UnitFriendlyDestroyedGainExperience=>"获得经验",TriggerKinds.UnitFirstFriendlyDestroyedDrawOne=>"本回合首次死亡：抽牌",_=>"本回合战力 +2" };
        return $"{source}：{effect}（{victim}被摧毁）";
    }

    private static CapturedUnitDeath? CaptureUnitDeath(MatchState state, IReadOnlyDictionary<string,PlayerZones> zones,
        IReadOnlyDictionary<string,CardObjectState> cards, CardObjectState unit)
    {
        if (!unit.Tags.Contains(CardObjectTags.UnitCard) || unit.IsFaceDown || unit.Tags.Contains(CardObjectTags.Standby)) return null;
        var frame=RuleChoices.Value!;
        var controller=EffectiveFieldControllerId(zones,unit.ObjectId,unit);
        var fact=new DestroyedUnitFact(unit.ObjectId,unit.CardNo ?? "",unit.OwnerId ?? controller,controller,unit.ObjectGeneration,unit.Tags.Contains(CardObjectTags.MinionTokenFamily));
        var observers=new List<CapturedDeathObserver>();
        // CN 383.2.c.2: an observer leaving in the same action does not trigger.
        // A nested resource cost has its own destruction batch, before the outer death.
        foreach(var source in cards.Values.OrderBy(c=>c.ObjectId,StringComparer.Ordinal))
            if (source.ObjectId != unit.ObjectId && !frame.CurrentDestructions.Contains(source.ObjectId)
                && source.Tags.Contains(CardObjectTags.UnitCard) && !source.IsFaceDown && !source.Tags.Contains(CardObjectTags.Standby)
                && IsObjectOnField(zones,source.ObjectId) && EffectiveFieldControllerId(zones,source.ObjectId,source)==controller
                && UnitDestroyedTriggerSpecRules.TryGetTrigger(source.CardNo,t=>IsDeathObserverKind(t.Kind),out var spec)
                && (spec.Kind != TriggerKinds.UnitDestroyedNonMinionCreateMinion || !fact.WasMinion))
                observers.Add(new(source.ObjectId,controller,new(source.CardNo!,spec.Kind,source.ObjectGeneration,fact)));
        return new($"death-{frame.Origin.Tick}-{frame.DeathSequence++}",frame.CurrentDestructionBatch ?? frame.DestructionBatchSequence++,state.TurnNumber,fact,observers);
    }

    private static ResolutionResult RecordDeathObservers(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var state=result.State;
        var counts=(before.DeathLedger.TurnNumber==state.TurnNumber ? before.DeathLedger.Counts : new Dictionary<string,int>())
            .ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
        var events=new List<GameEvent>(); var queue=state.TriggerQueue.ToList(); var seen=new HashSet<string>();
        var deaths = result.Events.Select(e => e.Payload.GetValueOrDefault("capturedUnitDeath"))
            .OfType<CapturedUnitDeath>().DistinctBy(d => d.Id).Where(d => d.TurnNumber == state.TurnNumber).ToArray();
        var firstChoices = new Dictionary<(int Batch, string Source), string>();
        var visitedBatches = new HashSet<int>();
        foreach(var ev in result.Events)
        {
            events.Add(ev.Payload.ContainsKey("capturedUnitDeath") ? ev with { Payload=ev.Payload.Where(p=>p.Key!="capturedUnitDeath").ToDictionary(p=>p.Key,p=>p.Value) } : ev);
            if (!ev.Payload.TryGetValue("capturedUnitDeath",out var raw) || raw is not CapturedUnitDeath death || !seen.Add(death.Id)) continue;
            if (death.TurnNumber != state.TurnNumber) continue;
            if (visitedBatches.Add(death.Batch) && state.Status == MatchStatuses.InProgress)
            {
                // CN 383.1.b: choose the occurrence when the first event happens simultaneously.
                var candidates = deaths.Where(d => d.Batch == death.Batch)
                    .SelectMany(d => d.Observers.Where(o => o.Context.Kind == TriggerKinds.UnitFirstFriendlyDestroyedDrawOne)
                        .Select(o => (Death: d, Observer: o)))
                    .GroupBy(x => x.Observer.ObjectId);
                foreach (var group in candidates)
                {
                    var observer = group.First().Observer;
                    if (counts.GetValueOrDefault(observer.ControllerId) > 0) continue;
                    var options = group.Select(x => new RuleChoiceOption(x.Death.Id,
                        DeathObserverLabel(x.Observer.Context), [x.Death.Unit.ObjectId, observer.ObjectId])).ToArray();
                    firstChoices[(death.Batch, group.Key)] = options.Length == 1 ? options[0].Id
                        : RuleChoices.Value!.Choose(observer.ControllerId, "选择本次同时死亡中的一次，触发每回合首次死亡技能。", options, kind: "FIRST_DEATH_OCCURRENCE");
                }
            }
            var prior=counts.GetValueOrDefault(death.Unit.ControllerId);
            counts[death.Unit.ControllerId]=prior+1;
            if (state.Status!=MatchStatuses.InProgress) continue;
            foreach(var observer in death.Observers)
            {
                if (observer.Context.Kind==TriggerKinds.UnitFirstFriendlyDestroyedDrawOne
                    && firstChoices.GetValueOrDefault((death.Batch, observer.ObjectId)) != death.Id) continue;
                var trigger=new TriggerQueueItemState($"{death.Id}-{observer.ObjectId}",observer.ControllerId,observer.ObjectId,
                    DeathObserverEffect,TriggerTimings.UnitDestroyed) { DeathObserver=observer.Context,SourceCardNo=observer.Context.CardNo };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        state=state with { DeathLedger=new(state.TurnNumber,counts),TriggerQueue=queue };
        return result with { State=state,Events=events,Snapshots=ResolutionResult.BuildSnapshots(state),Prompts=BuildCorePrompts(state) };
    }

    private static StackResolutionResult ResolveDeathObserver(MatchState state, StackItemState item)
    {
        var c=item.DeathObserver!;
        if (!ValidDeathObserver(c,item.EffectKind,item.ControllerId,item.SourceObjectId,item.CardNo)
            || !UnitDestroyedTriggerSpecRules.TryGetTrigger(c.CardNo,t=>t.Kind==c.Kind,out var spec))
            throw new InvalidOperationException("Invalid captured death observer.");
        var resolved=BuildTriggerResolvedEvent(new TriggerQueueItemState(item.StackItemId,item.ControllerId,item.SourceObjectId,item.EffectKind,TriggerTimings.UnitDestroyed) { DeathObserver=c });
        if (c.Kind==TriggerKinds.UnitDestroyedNonMinionCreateMinion) {
            var batch=CreateUnitTokenBatch(state,item,P6TokenFactoryCatalog.ZaunMinionTokenCardNo,1+AdditionalUnitTokens(item),false,abilityId:c.Kind);
            return batch with { Events=new[] { resolved }.Concat(batch.Events).ToArray() };
        }
        if (c.Kind==TriggerKinds.UnitFirstFriendlyDestroyedDrawOne) {
            var draw=ResolveLastBreathDrawStackItem(state,item,spec.DrawCount.GetValueOrDefault(1));
            return draw with { Events=draw.Events.Select(e=>e.Kind=="TRIGGER_RESOLVED" ? resolved : e).ToArray() };
        }
        var events=new List<GameEvent> { resolved };
        if (c.Kind==TriggerKinds.UnitFriendlyDestroyedGainExperience)
            return NoopStackResolutionResult(state) with { PlayerExperience=GainExperience(NormalizeExperienceForSeats(state),item.ControllerId,
                spec.ExperienceCount.GetValueOrDefault(1),item,events,item.SourceObjectId,c.CardNo),Events=events };
        var cards=state.CardObjects.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
        // The earned skill survives a control change, but cannot buff a new incarnation.
        if (cards.TryGetValue(item.SourceObjectId,out var source) && source.ObjectGeneration==c.SourceGeneration
            && IsObjectOnField(state.PlayerZones,source.ObjectId) && source.Tags.Contains(CardObjectTags.UnitCard))
        {
            var behavior=new CardBehaviorDefinition(c.CardNo,"幽魂半人马",0,c.Kind,0,0,PowerModifierAmount:spec.PowerDelta.GetValueOrDefault());
            cards[source.ObjectId]=ApplyPowerModifier(source,behavior,item,source.ObjectId,behavior.PowerModifierAmount,out var powerEvent);
            events.Add(powerEvent);
        }
        return NoopStackResolutionResult(state) with { CardObjects=cards,Events=events };
    }
}

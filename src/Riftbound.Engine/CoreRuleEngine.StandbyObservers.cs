using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record StandbyPlayedObserverContext(string CardNo,ObjectBinding Source,
    RuleTextInstanceBinding? Origin=null);

public sealed partial class CoreRuleEngine
{
    internal const string StandbyPlayedEffect="STANDBY_PLAYED_SELF_MIGHT";
    private sealed record CapturedStandbyObserver(string Controller,string InstanceId,StandbyPlayedObserverContext Context);

    private static GameEvent CaptureStandbyPlayed(GameEvent ev,IReadOnlyDictionary<string,PlayerZones> zones,
        IReadOnlyDictionary<string,CardObjectState> cards,string player)
    {
        var captured=new List<CapturedStandbyObserver>();
        foreach(var source in cards.Values.Where(c=>c.ControllerId==player && FieldObjectTypeRules.IsVisibleUnit(c)
            && IsObjectOnField(zones,c.ObjectId)).OrderBy(c=>c.ObjectId,StringComparer.Ordinal))
        foreach(var instance in AttachedRuleTextRules.Instances(zones,cards,source))
            if(StandbyRules.PlayedSelfMight(instance.CardNo)>0)
                captured.Add(new(player,instance.Id,new(instance.CardNo,new(source.ObjectId,source.ObjectGeneration),instance.Origin)));
        return captured.Count==0 ? ev : ev with {Payload=new Dictionary<string,object?>(ev.Payload) {["standbyPlayObservers"]=captured.ToArray()}};
    }

    private static ResolutionResult QueueStandbyPlayedObservers(ResolutionResult result)
    {
        if(!result.Accepted)return result;
        var events=result.Events.ToList();var queue=result.State.TriggerQueue.ToList();
        for(var i=0;i<events.Count;i++) {
            var ev=events[i];
            if(ev.Payload.GetValueOrDefault("standbyPlayObservers") is not CapturedStandbyObserver[] captured)continue;
            events[i]=ev with {Payload=ev.Payload.Where(p=>p.Key!="standbyPlayObservers").ToDictionary(p=>p.Key,p=>p.Value)};
            if(result.State.Status!=MatchStatuses.InProgress)continue;
            foreach(var source in captured) {
                var trigger=new TriggerQueueItemState($"standby-play-{result.State.Tick}-{i}-{source.Context.Source.ObjectId}-{source.InstanceId}",
                    source.Controller,source.Context.Source.ObjectId,StandbyPlayedEffect,"CARD_PLAYED_FROM_FACE_DOWN") {
                    SourceCardNo=source.Context.CardNo,StandbyObserver=source.Context };
                queue.Add(trigger);events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        return result with {State=result.State with {TriggerQueue=queue},Events=events};
    }

    internal static bool ValidStandbyObserver(MatchState state,StandbyPlayedObserverContext c,string effect,string source,string? cardNo=null)
        => effect==StandbyPlayedEffect && c is not null && c.Source is not null && c.Source.Generation>=0
            && c.Source.ObjectId==source && !string.IsNullOrWhiteSpace(c.CardNo) && (cardNo is null || cardNo==c.CardNo)
            && StandbyRules.PlayedSelfMight(c.CardNo)>0
            && AttachedRuleTextRules.ValidCaptured(state,c.Origin,source,c.Source.Generation,c.CardNo);

    private static StackResolutionResult ResolveStandbyObserver(MatchState state,StackItemState item)
    {
        var c=item.StandbyObserver!;
        if(!state.CardObjects.TryGetValue(c.Source.ObjectId,out var source) || source.ObjectGeneration!=c.Source.Generation
            || !IsObjectOnField(state.PlayerZones,source.ObjectId) || !FieldObjectTypeRules.IsVisibleUnit(source))return NoopStackResolutionResult(state);
        var effect=StandbyRules.PlayedSelfEffect(c.CardNo)!;
        var amount=effect.Might;
        var behavior=new CardBehaviorDefinition(c.CardNo,"从待命打出的触发",0,StandbyPlayedEffect,0,0,PowerModifierAmount:amount);
        var cards=state.CardObjects.ToDictionary(p=>p.Key,p=>p.Value);
        var events=new List<GameEvent>();
        if(effect.ReadiesSelf && source.IsExhausted && !AttachedRuleTextRules.Instances(state,source)
            .Any(instance=>CardStaticAbilitySpecRules.CardCannotBecomeActive(instance.CardNo)))
        {
            AddReadiedObservation(state.PlayerZones,cards,source,events);
            var beforeReady=source;
            source=source with {IsExhausted=false};
            cards[source.ObjectId]=source;
            events.Add(CaptureSelfReady(new("UNIT_READIED","从正面朝下打出卡牌的技能使来源活跃",new Dictionary<string,object?> {
                ["playerId"]=item.ControllerId,["sourceObjectId"]=source.ObjectId,["targetObjectId"]=source.ObjectId,
                ["reason"]=item.EffectKind,["wasExhausted"]=true,["isExhausted"]=false }),beforeReady,item.ControllerId,state.PlayerZones,cards));
        }
        cards[source.ObjectId]=ApplyPowerModifier(state,source,behavior,item,source.ObjectId,amount,out var changed);
        events.Add(changed);
        return NoopStackResolutionResult(state) with {CardObjects=cards,Events=events};
    }
}

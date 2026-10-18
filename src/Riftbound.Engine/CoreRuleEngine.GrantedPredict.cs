using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record PredictGrantOrigin(string CardNo, ObjectBinding Source, long SourceFaceRevision,
    long CapturedTick, long EnteredFaceRevision,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? SourceTokenFactoryCardNo = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? EnteredTokenFactoryCardNo = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] RuleTextInstanceBinding? Origin = null);

public sealed partial class CoreRuleEngine
{
    private sealed record GrantedPredictObservation(string ObjectId, string CardNo, long Generation, string Controller,
        IReadOnlyList<PredictGrantOrigin> Grants);

    private static bool IsPredictAura(StaticAuraSpec aura) => StaticAuraSpecRules.IsPublicFieldFriendlyKeywordStaticAura(aura)
        && aura.GrantedKeyword == CardLifecycleKeywordNames.Predict;

    internal static bool ValidPredictGrant(PredictGrantOrigin? origin, string enteredCardNo) => origin is not null && origin.Source is not null
        && origin.Source.Generation >= 0 && !string.IsNullOrWhiteSpace(origin.Source.ObjectId)
        && origin.SourceFaceRevision >= 0 && origin.CapturedTick >= 0 && origin.EnteredFaceRevision >= 0
        && (origin.Origin is not { } binding || binding.CardNo == origin.CardNo && binding.Unit == origin.Source
            && binding.FaceRevision == origin.SourceFaceRevision && binding.CapturedTick is >= 0 && binding.CapturedTick <= origin.CapturedTick)
        && (origin.SourceTokenFactoryCardNo is null || P6TokenFactoryCatalog.IsTokenFactory(origin.SourceTokenFactoryCardNo))
        && (origin.EnteredTokenFactoryCardNo is null || P6TokenFactoryCatalog.IsTokenFactory(origin.EnteredTokenFactoryCardNo))
        && TryPredictEnteredFace(enteredCardNo, out var entered) && entered.Tags.Contains(CardObjectTags.UnitCard)
        && StaticAuraSpecRules.GetStaticAuras(origin.CardNo).Any(a => IsPredictAura(a)
            && (a.TargetScope == StaticAuraTargetScopes.OtherFriendlyUnits || StaticAuraSpecRules.TargetMatchesFilter(a, entered)));

    private static bool TryPredictEnteredFace(string cardNo, out CardObjectState card)
    {
        if (P6TokenFactoryCatalog.TryGetByCardNo(cardNo, out var token)) {
            card = token.CreateObject("", "", ""); return true;
        }
        return PrintedCardFactory.TryCreate("", "", cardNo, out card);
    }

    internal static bool ValidGrantedPredictBindings(MatchState state, InsightTriggerContext context, string enteredId)
    {
        if (context.Kind != "GRANTED_PREDICT") return true;
        if (!ValidPredictGrant(context.PredictGrant, context.CardNo) || context.PredictGrant is not { } grant
            || grant.CapturedTick > state.Tick
            || !PredictBindingMatches(state, grant.Source, grant.CardNo, grant.SourceFaceRevision,
                grant.Origin?.CapturedTick ?? grant.CapturedTick, grant.SourceTokenFactoryCardNo)
            || !AttachedRuleTextRules.ValidCaptured(state, grant.Origin, grant.Source.ObjectId, grant.Source.Generation, grant.CardNo)
            || !PredictBindingMatches(state, new(enteredId, context.SourceGeneration), context.CardNo, grant.EnteredFaceRevision, grant.CapturedTick, grant.EnteredTokenFactoryCardNo)) return false;
        if (grant.Source.ObjectId == enteredId && (grant.Source.Generation != context.SourceGeneration || grant.CardNo != context.CardNo)) return false;
        return TryPredictEnteredFace(context.CardNo, out var entered)
            && StaticAuraSpecRules.GetStaticAuras(grant.CardNo).Any(a => IsPredictAura(a)
                && PublicFieldFriendlyKeywordStaticAuraAppliesToParticipant(a, grant.Source.ObjectId, enteredId, entered));
    }

    private static bool PredictBindingMatches(MatchState state, ObjectBinding binding, string face, long revision, long tick, string? tokenFactory)
        => !string.IsNullOrWhiteSpace(binding.ObjectId) && binding.Generation >= 0
            && (state.CardObjects.TryGetValue(binding.ObjectId, out var current)
                ? current.ObjectGeneration >= binding.Generation && current.TokenFactoryCardNo == tokenFactory
                    && (current.ObjectGeneration > binding.Generation || CopyFaceTimeline.FaceAt(current, revision, tick) == face)
                : tokenFactory is not null && P6TokenFactoryCatalog.IsTokenFactory(tokenFactory));

    private static GameEvent CaptureGrantedPredict(GameEvent ev, IReadOnlyDictionary<string,PlayerZones> zones,
        Dictionary<string,CardObjectState> cards, CardObjectState entered, string player)
    {
        // CN 817.1.c/817.2: capture each grant when the permanent enters, including
        // its own filtered aura. The trigger survives later changes to the aura.
        player = EffectiveFieldControllerId(zones, entered.ObjectId, entered);
        if (string.IsNullOrWhiteSpace(player)) return ev;
        var grants = PublicStaticAuraSourceObjectIds(zones).Where(cards.ContainsKey).Select(id=>cards[id])
            .Where(c=>!c.IsFaceDown && EffectivePublicStaticAuraSourceControllerId(zones,c.ObjectId,c)==player)
            .SelectMany(c=>AttachedRuleTextRules.Instances(zones,cards,c).SelectMany(instance=>
                StaticAuraSpecRules.GetStaticAuras(instance.CardNo).Where(IsPredictAura)
                    .Where(a=>PublicFieldFriendlyKeywordStaticAuraAppliesToParticipant(a,c.ObjectId,entered.ObjectId,entered))
                    .Select(_=>new PredictGrantOrigin(instance.CardNo,new(c.ObjectId,c.ObjectGeneration),
                        instance.Origin?.FaceRevision ?? c.CopyFaceTransitions?.Count ?? 0,
                        0, entered.CopyFaceTransitions?.Count ?? 0, c.TokenFactoryCardNo, entered.TokenFactoryCardNo,instance.Origin)))).ToArray();
        return grants.Length==0 ? ev : ev with {Payload=new Dictionary<string,object?>(ev.Payload) {
            ["grantedPredictObservation"]=new GrantedPredictObservation(entered.ObjectId,entered.CardNo!,entered.ObjectGeneration,player,grants)}};
    }

    private static ResolutionResult QueueGrantedPredict(MatchState before, ResolutionResult result)
    {
        if(!result.Accepted)return result;
        var queue=result.State.TriggerQueue.ToList();var events=result.Events.ToList();
        foreach(var (ev,index) in result.Events.Select((e,i)=>(e,i))) {
            if(ev.Payload.GetValueOrDefault("grantedPredictObservation") is not GrantedPredictObservation observed)continue;
            events[index]=ev with {Payload=ev.Payload.Where(p=>p.Key!="grantedPredictObservation").ToDictionary(p=>p.Key,p=>p.Value)};
            var generation=observed.Generation;
            if(before.CardObjects.TryGetValue(observed.ObjectId,out var old) && !IsObjectOnField(before.PlayerZones,observed.ObjectId))
                generation=Math.Max(generation,old.ObjectGeneration+1);
            foreach(var (origin,ordinal) in observed.Grants.Select((g,i)=>(g,i))) {
                var grant = origin with { CapturedTick = result.State.Tick,
                    Source = origin.Source.ObjectId == observed.ObjectId ? origin.Source with { Generation = generation } : origin.Source };
                var trigger=new TriggerQueueItemState($"predict-entry-{result.State.Tick}-{index}-{ordinal}",observed.Controller,
                    observed.ObjectId,InsightTriggerEffect,ev.Kind) {
                    InsightContext=new(observed.CardNo,observed.Controller,1,generation,"GRANTED_PREDICT",PredictGrant:grant)};
                queue.Add(trigger);events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        if(events.Count==result.Events.Count)return result;
        var state=result.State with {TriggerQueue=queue};
        return result with {State=state,Events=events,Snapshots=ResolutionResult.BuildSnapshots(state),Prompts=BuildCorePrompts(state)};
    }
}

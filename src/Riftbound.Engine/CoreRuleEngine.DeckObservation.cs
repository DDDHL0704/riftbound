using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeckObservationInstruction(StackItemState Parent, string PlayerId, string Kind, int ExecutionIndex,
    IReadOnlyList<ObjectBinding> Seen)
{
    public bool SelectionReveal {get;init;}
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ObservedDeckPlayContext(string Id, DeckObservationInstruction Instruction, ObjectBinding Card,
    string CardNo, int PowerCost, long CapturedTick);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ObservedDeckPlayPermission(ObservedDeckPlayContext Context,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PendingEffectPlayState? Suspended = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeckObservationView(string PlayerId, bool Public, IReadOnlyDictionary<string,long> Cards);

public sealed partial class CoreRuleEngine
{
    internal const string ObservedDeckPlayEffect="OBSERVED_DECK_UNIT_PLAY_PERMISSION";
    private static readonly Lazy<IReadOnlyDictionary<string,int>> ObservationPlayCosts=new(()=>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards.Select(c=>new {
            c.CardNo,Spec=StaticAbilityParser.Parse(c.CardEffect??"",[]).FirstOrDefault(s=>s.Kind==StaticAbilityKinds.DeckObservationBanishPlay) })
            .Where(c=>c.Spec is not null).ToDictionary(c=>c.CardNo,c=>c.Spec!.Amount));

    private static string[] ObserveDeckCards(MatchState state, Dictionary<string,PlayerZones> zones,
        Dictionary<string,CardObjectState> cards, StackItemState parent, string player, IReadOnlyList<string> seen,
        string kind, int execution, List<GameEvent> events, bool selectionReveal=false)
    {
        if(RuleChoices.Value is not { } frame)return seen.ToArray();
        var instruction=new DeckObservationInstruction(parent,player,kind,execution,
            seen.Select(id=>new ObjectBinding(id,cards.TryGetValue(id,out var known)?known.ObjectGeneration:0)).ToArray()) {SelectionReveal=selectionReveal};
        foreach(var id in seen)
        {
            if(!cards.TryGetValue(id,out var card))continue;
            if(!ObservationPlayCosts.Value.TryGetValue(card.CardNo??"",out var cost)
                || card.OwnerId!=player || card.ControllerId!=player || !zones[player].MainDeck.Contains(id))continue;
            var preview=state with {PlayerZones=zones.ToDictionary(),CardObjects=cards.ToDictionary(),
                ObjectLocations=ReconcileObjectLocations(state.ObjectLocations,zones),
                DeckObservationView=new(player,kind=="REVEAL",seen.Where(x=>zones[player].MainDeck.Contains(x))
                    .Where(cards.ContainsKey).ToDictionary(x=>x,x=>cards[x].ObjectGeneration))};
            var decision=frame.Choose(player,"所查看的卡牌提供可选放逐出牌权限。",[new("KEEP","保留给原效果处理"),new("BANISH","放逐，原效果完成后可支付符能打出")],
                kind:"DECK_OBSERVATION_BANISH",preview:preview,privateReason:$"所查看的《{DeckCardNames.Value.GetValueOrDefault(card.CardNo!,card.CardNo!)}》可选择放逐，原效果完成后可支付一个任意符能打出。");
            if(decision!="BANISH")continue;
            var restored=PrintedCardFactory.TryRestoreOutsidePlay(card,player,out var printed)?printed:card;
            cards[id]=restored with {ControllerId=player,ObjectGeneration=checked(card.ObjectGeneration+1)};
            zones[player]=zones[player] with {MainDeck=RemoveFromZone(zones[player].MainDeck,id),Banished=zones[player].Banished.Append(id).ToArray()};
            frame.ObservedPlays.Add(new($"OBSERVED:{state.Tick}:{parent.StackItemId}:{execution}:{id}",instruction,
                new(id,cards[id].ObjectGeneration),card.CardNo!,cost,state.Tick));
            events.Add(new("CARD_BANISHED","所查看的卡牌以静态能力放逐，开始待确认出牌",new Dictionary<string,object?> {
                ["playerId"]=player,["sourceObjectId"]=id,["targetObjectId"]=id,["cardNo"]=card.CardNo,["destinationZone"]="BANISHED"}));
        }
        return seen.Where(id=>zones[player].MainDeck.Contains(id)).ToArray();
    }

    private static ResolutionResult CollectObservedDeckPlays(ResolutionResult result)
    {
        if(!result.Accepted || RuleChoices.Value is not {ObservedPlays.Count:>0} frame)return result;
        var zones=NormalizeZonesForSeats(result.State);var cards=result.State.CardObjects.ToDictionary();
        var stack=result.State.StackItems.ToList();var contexts=(result.State.ObservedDeckPlays??[]).ToList();
        var originalIds=frame.Origin.StackItems.Select(s=>s.StackItemId).ToHashSet(StringComparer.Ordinal);
        var detachedInsertIndex=stack.FindLastIndex(s=>originalIds.Contains(s.StackItemId))+1;
        foreach(var observed in frame.ObservedPlays)
        {
            var id=observed.Card.ObjectId;var card=cards[id];
            card=card with {ObjectGeneration=checked(card.ObjectGeneration+1)};cards[id]=card;
            zones[observed.Instruction.PlayerId]=zones[observed.Instruction.PlayerId] with {Banished=RemoveFromZone(zones[observed.Instruction.PlayerId].Banished,id)};
            var c=observed with {Card=new(id,card.ObjectGeneration)};contexts.Add(c);
            CardBehaviorRegistry.TryGetByCardNo(c.CardNo,out var definition);
            var slot=new StackItemState(c.Id,c.Instruction.PlayerId,id,definition.EffectKind,c.CardNo) {ObservedDeckPlay=c};
            var parentIndex=stack.FindIndex(s=>s.StackItemId==observed.Instruction.Parent.StackItemId);
            stack.Insert(parentIndex<0?detachedInsertIndex++:parentIndex,slot);
        }
        var next=result.State with {PlayerZones=zones,CardObjects=cards,StackItems=stack,ObservedDeckPlays=contexts,
            ObjectLocations=ReconcileObjectLocations(result.State.ObjectLocations,zones)};
        foreach(var c in contexts)if(next.StackItems.Any(s=>s.ObservedDeckPlay?.Id==c.Id))
            next=next with {ObjectLocations=new Dictionary<string,ObjectLocationState>(next.ObjectLocations){[c.Card.ObjectId]=new(c.Instruction.PlayerId,"STACK")}};
        return result with {State=next,Snapshots=ResolutionResult.BuildSnapshots(next),Prompts=BuildCorePrompts(next)};
    }

    private static ResolutionResult AdvanceObservedDeckPlays(ResolutionResult result)
    {
        var state=result.State;
        if(!result.Accepted || state.Status!=MatchStatuses.InProgress || state.PendingRuleChoice is not null || state.PendingCardChoice is not null
            || state.PendingHandChoice is not null || state.PendingPayment is not null
            || state.PendingEffectPlay?.Parent.ObservedPlayPermission is not null)return result;
        var context=NextObservedDeckPlay(state,state.PendingEffectPlay);
        if(context is null)return result;
        var permission=new ObservedDeckPlayPermission(context,state.PendingEffectPlay);
        var parent=new StackItemState("OBSERVED-PERMISSION:"+context.Id,context.Instruction.PlayerId,context.Card.ObjectId,ObservedDeckPlayEffect,context.CardNo)
            {ObservedPlayPermission=permission};
        var pending=new PendingEffectPlayState("OBSERVED-PLAY:"+context.Id,context.Instruction.PlayerId,parent,"STACK",
            new Dictionary<string,long>{{context.Card.ObjectId,context.Card.Generation}},true,true,0,"",true);
        var next=state with {PendingEffectPlay=pending,PriorityPlayerId=null,PassedPriorityPlayerIds=[],ActivePlayerId=pending.PlayerId};
        return result with {State=next,Snapshots=ResolutionResult.BuildSnapshots(next),Prompts=BuildCorePrompts(next)};
    }

    private static ObservedDeckPlayContext? NextObservedDeckPlay(MatchState state,PendingEffectPlayState? suspended)
        => (state.ObservedDeckPlays??[]).FirstOrDefault(c=>CompleteObservationShape(c) && !state.StackItems.Any(s=>s.StackItemId==c.Instruction.Parent.StackItemId)
            && (suspended is null || suspended.Parent.StackItemId==c.Instruction.Parent.StackItemId));

    private static bool CompleteObservationShape(ObservedDeckPlayContext? c)
        => c?.Card is not null && c.Instruction?.Parent is not null && c.Instruction.Seen is not null
            && c.Instruction.Seen.All(b=>b is not null);

    private static ResolutionResult FinishObservedDeckPlay(MatchState state, PendingEffectPlayState pending, IReadOnlyList<GameEvent> events)
    {
        var p=pending.Parent.ObservedPlayPermission!;var c=p.Context;var id=c.Card.ObjectId;
        if(state.StackItems.Any(s=>s.ObservedDeckPlay?.Id==c.Id))
        {
            var zones=NormalizeZonesForSeats(state);zones[c.Instruction.PlayerId]=zones[c.Instruction.PlayerId] with {Banished=zones[c.Instruction.PlayerId].Banished.Append(id).ToArray()};
            state=state with {PlayerZones=zones,StackItems=state.StackItems.Where(s=>s.ObservedDeckPlay?.Id!=c.Id).ToArray(),
                ObjectLocations=ReconcileObjectLocations(state.ObjectLocations,zones)};
            events=events.Append(new GameEvent("EFFECT_PLAY_DECLINED","静态许可未完成出牌，卡牌留在放逐区",new Dictionary<string,object?> {
                ["playerId"]=c.Instruction.PlayerId,["sourceObjectId"]=id,["destinationZone"]="BANISHED"})).ToArray();
        }
        var remaining=(state.ObservedDeckPlays??[]).Where(x=>x.Id!=c.Id).ToArray();
        var next=state with {PendingEffectPlay=p.Suspended,ObservedDeckPlays=remaining.Length==0?null:remaining,
            PriorityPlayerId=p.Suspended is null?state.StackItems.LastOrDefault()?.ControllerId:null,ActivePlayerId=p.Suspended?.PlayerId??state.TurnPlayerId};
        return new(true,null,next,events,ResolutionResult.BuildSnapshots(next),BuildCorePrompts(next));
    }

    internal static string[]? ObservedRemainingDeckCards(MatchState state, StackItemState parent, string player,int execution)
    {
        var observed=(state.ObservedDeckPlays??[]).Where(c=>CompleteObservationShape(c) && c.Instruction.Parent.StackItemId==parent.StackItemId
            && c.Instruction.PlayerId==player && c.Instruction.ExecutionIndex==execution).ToArray();
        if(observed.Length==0)return null;
        var removed=observed.Select(c=>c.Card.ObjectId).ToHashSet(StringComparer.Ordinal);
        return observed[0].Instruction.Seen.Where(b=>!removed.Contains(b.ObjectId)).Select(b=>b.ObjectId).ToArray();
    }

    internal static bool CanViewObservedDeckCard(MatchState state,string id,string viewer)
        => state.DeckObservationView is { } v && (v.Public || v.PlayerId==viewer)
            && v.Cards.TryGetValue(id,out var generation) && state.CardObjects.TryGetValue(id,out var card)
            && card.ObjectGeneration==generation && state.PlayerZones[v.PlayerId].MainDeck.Contains(id);

    internal static bool ValidObservedDeckPlay(MatchState state, ObservedDeckPlayContext c)
    {
        try
        {
            var i=c.Instruction;var parent=i.Parent;
            var count=TryGetEffectPlayDefinition(parent,out var play)&&play.DeckPlayKind.Length>0?DeckPlayLookCount(play)
                : TryGetDeckChoiceBehavior(state,parent,out var choice)?choice.MainDeckLookCount
                : parent.InsightContext is { } insight?insight.Count
                : CardBehaviorRegistry.TryGetByCardNo(parent.CardNo??"",out var printed)
                    ? printed.PerformsInsight?1:Math.Max(printed.DefendTriggerMainDeckLookCount,printed.StandbyReactionMainDeckLookCount)
                    : BattlefieldTriggerSpecRules.TryGetTrigger(parent.CardNo,
                        s=>s.Kind==parent.EffectKind && s.Kind is TriggerKinds.BattlefieldConquerRevealRecycle or TriggerKinds.BattlefieldDefendRevealTopDrawSpellOrRecycle,
                        out var battlefield)?battlefield.RevealCount??0:0;
            var revealUntilUnit=parent.EndAbility?.Kind==TriggerKinds.TurnEndRevealUntilUnit;
            if(c.Card is null || i is null || parent is null || parent.ObservedDeckPlay is not null || parent.ObservedPlayPermission is not null
                || c.CapturedTick<0 || c.CapturedTick>state.Tick || !state.Seats.ContainsKey(i.PlayerId)
                || i.Kind is not ("LOOK" or "REVEAL") || i.ExecutionIndex<0 || i.ExecutionIndex>=parent.EffectRepeatCount
                || (i.SelectionReveal ? i.Kind!="REVEAL" || i.Seen.Count!=1
                    || !TryGetDeckChoiceBehavior(state,parent,out var selectedBehavior) || selectedBehavior.MainDeckTargetRequiredTag!=CardObjectTags.UnitCard
                    : i.Kind!=ExpectedObservationKind(parent))
                || !ObservationPlayCosts.Value.TryGetValue(c.CardNo,out var cost) || cost!=c.PowerCost || cost!=1
                || i.Seen is not {Count:>0} || i.Seen.Any(b=>b is null || b.Generation<0)
                || i.Seen.Select(b=>b.ObjectId).Distinct().Count()!=i.Seen.Count
                || !i.Seen.Any(b=>b.ObjectId==c.Card.ObjectId && c.Card.Generation==b.Generation+2)
                || !ValidObservationParent(state,parent) || !revealUntilUnit && (count<=0 || i.Seen.Count>count)
                || !state.CardObjects.TryGetValue(c.Card.ObjectId,out var card) || card.CardNo!=c.CardNo
                || card.ObjectGeneration!=c.Card.Generation || card.OwnerId!=i.PlayerId || card.ControllerId!=i.PlayerId
                || !card.Tags.Contains(CardObjectTags.UnitCard)
                || c.Id!=$"OBSERVED:{c.CapturedTick}:{parent.StackItemId}:{i.ExecutionIndex}:{c.Card.ObjectId}")return false;
            return i.Seen.All(b=>state.CardObjects.TryGetValue(b.ObjectId,out var seen)&&seen.ObjectGeneration>=b.Generation)
                && (!revealUntilUnit || i.Kind=="REVEAL" && i.Seen.Last().ObjectId==c.Card.ObjectId
                    && i.Seen.Take(i.Seen.Count-1).All(b=>!state.CardObjects[b.ObjectId].Tags.Contains(CardObjectTags.UnitCard)));
        }
        catch(Exception e) when(e is NullReferenceException or ArgumentException or InvalidOperationException or KeyNotFoundException or OverflowException){return false;}
    }

    private static bool ValidObservationParent(MatchState state,StackItemState parent)
        => parent.EndAbility is not null ? ValidEndAbility(state,parent)
            : parent.DestroyDeckPlay is not null ? ValidDestroyDeckPlayContext(state,parent)
            : parent.FieldContext is { } field ? ValidFieldContextState(state,field,parent.SourceObjectId,parent.ControllerId,parent.EffectKind,parent.CardNo)
            : parent.InsightContext is { } insight ? ValidInsightTrigger(insight,parent.EffectKind,parent.ControllerId,parent.CardNo)
                && ValidConquestLookSource(state,insight,parent.SourceObjectId)
                && ValidPredictEntryBindings(state,insight,parent.SourceObjectId)
            : parent.HeldContext is { } held ? HeldDefinition(held.CardNo) is {Kind:"LOOK_EQUIPMENT"} heldDefinition
                && heldDefinition.Amount==held.Amount && parent.CardNo==held.CardNo && parent.EffectKind=="HOLD_"+held.Kind
            : parent.LegendConquest is { } conquest ? ValidLegendConquest(conquest,parent.EffectKind,parent.CardNo)
                && parent.TriggerCost is not null && ValidTriggerCostReceipt(state,parent)
            : BattlefieldTriggerSpecRules.TryGetTrigger(parent.CardNo,
                s=>s.Kind==parent.EffectKind && s.Kind is TriggerKinds.BattlefieldConquerRevealRecycle or TriggerKinds.BattlefieldDefendRevealTopDrawSpellOrRecycle,out _)
                ? state.CardObjects.TryGetValue(parent.SourceObjectId,out var battlefield)&&battlefield.CardNo==parent.CardNo
            : CardBehaviorRegistry.TryGetByCardNo(parent.CardNo??"",out var definition)&&definition.EffectKind==parent.EffectKind
                && state.CardObjects.TryGetValue(parent.SourceObjectId,out var source)&&source.CardNo==parent.CardNo;

    private static string ExpectedObservationKind(StackItemState parent)
        => parent.InsightContext is { } insight ? insight.Kind=="DUEL" && parent.InsightCompleted?"REVEAL":"LOOK"
            : TryGetEffectPlayDefinition(parent,out var play)&&play.DeckPlayKind.Length>0
                ? DeckPlaySequenceReveals(parent)?"REVEAL":"LOOK"
            : parent.FieldContext is {Kind:"STANDBY_DEFEND"}?"REVEAL"
            : parent.EffectKind is TriggerKinds.BattlefieldConquerRevealRecycle or TriggerKinds.BattlefieldDefendRevealTopDrawSpellOrRecycle?"REVEAL"
            : CardBehaviorRegistry.TryGetByCardNo(parent.CardNo??"",out var definition)
                && (definition.DefendTriggerMainDeckLookCount>0 || definition.StandbyReactionMainDeckLookCount>0)?"REVEAL":"LOOK";

    internal static bool HasObservedConfirmationBeforeTriggers(MatchState state)
        => (state.ObservedDeckPlays??[]).Any(c=>CompleteObservationShape(c) && !state.StackItems.Any(s=>s.StackItemId==c.Instruction.Parent.StackItemId));

    internal static bool ValidObservedDeckSlot(MatchState state, StackItemState item)
        => item.ObservedPlayPermission is null && (item.ObservedDeckPlay is not { } c || ValidObservedDeckPlay(state,c)
            && (state.ObservedDeckPlays??[]).Any(x=>MatchStateHasher.HashValue(x)==MatchStateHasher.HashValue(c))
            && item.StackItemId==c.Id && item.SourceObjectId==c.Card.ObjectId && item.CardNo==c.CardNo
            && item.ControllerId==c.Instruction.PlayerId && !item.SourceConfirmed && item.TargetObjectIds.Count==0
            && item.DamageAmount==0 && item.OptionalCosts.Count==0 && item.EffectRepeatCount==1
            && CardBehaviorRegistry.TryGetByCardNo(c.CardNo,out var definition)&&item.EffectKind==definition.EffectKind
            && state.ObjectLocations.GetValueOrDefault(c.Card.ObjectId)?.Zone=="STACK");

    internal static bool ValidObservedDeckOrder(MatchState state)
    {
        var captured=state.ObservedDeckPlays??[];
        if(captured.Any(c=>!CompleteObservationShape(c)))return false;
        if(!state.StackItems.Where(s=>s.ObservedDeckPlay is not null).Select(s=>s.ObservedDeckPlay!.Id)
            .SequenceEqual(captured.Select(c=>c.Id)))return false;
        if(!captured.Select(c=>c.CapturedTick).SequenceEqual(captured.Select(c=>c.CapturedTick).Order()))return false;
        foreach(var group in captured.GroupBy(c=>(c.CapturedTick,c.Instruction.Parent.StackItemId,c.Instruction.PlayerId,
            c.Instruction.Kind,c.Instruction.ExecutionIndex,c.Instruction.SelectionReveal)))
        {
            var seen=group.First().Instruction.Seen;
            if(group.Any(c=>!seen.SequenceEqual(c.Instruction.Seen)))return false;
            var positions=group.Select(c=>seen.Select(b=>b.ObjectId).ToList().IndexOf(c.Card.ObjectId)).ToArray();
            if(!positions.SequenceEqual(positions.Order()))return false;
        }
        return true;
    }

    internal static bool ValidObservedPlayPermission(MatchState state, PendingEffectPlayState pending)
    {
        if(pending.Parent.ObservedPlayPermission is not { } p)return pending.Parent.EffectKind!=ObservedDeckPlayEffect;
        var c=p.Context;
        if(c is null || !ValidObservedDeckPlay(state,c) || !(state.ObservedDeckPlays??[]).Any(x=>MatchStateHasher.HashValue(x)==MatchStateHasher.HashValue(c))
            || NextObservedDeckPlay(state,p.Suspended)?.Id!=c.Id
            || pending.PlayerId!=c.Instruction.PlayerId || pending.ChoiceId!="OBSERVED-PLAY:"+c.Id
            || pending.Parent.StackItemId!="OBSERVED-PERMISSION:"+c.Id || pending.Parent.SourceObjectId!=c.Card.ObjectId
            || pending.Parent.ControllerId!=pending.PlayerId || pending.Parent.CardNo!=c.CardNo || pending.Parent.EffectKind!=ObservedDeckPlayEffect
            || pending.SourceZone!="STACK" || pending.Sources.Count!=1 || pending.Sources.GetValueOrDefault(c.Card.ObjectId,-1)!=c.Card.Generation
            || !pending.IgnoreBaseMana || !pending.IgnoreBasePower || pending.IgnoreAllCosts || pending.ManaReduction!=0
            || !pending.Optional || pending.DestinationPolicy!="" || pending.RevealedHand is not null || pending.ViewedCardIds is not null
            || !state.StackItems.Any(s=>s.ObservedDeckPlay?.Id==c.Id && ValidObservedDeckSlot(state,s)))return false;
        return p.Suspended is null || p.Suspended.Parent.StackItemId==c.Instruction.Parent.StackItemId
            && ValidDeckPlayPending(state with {PendingEffectPlay=p.Suspended},p.Suspended);
    }
}

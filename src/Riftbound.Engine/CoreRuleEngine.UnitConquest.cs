using Riftbound.Contracts;
namespace Riftbound.Engine;

// Damage uses source Might on execution; conquest eligibility uses the event's pre-control state.
public sealed record ConquestDamageSpec(bool RequiresUncontrolledBattlefield);
public sealed record UnitConquestContext(string CardNo, long SourceGeneration, string BattlefieldId, string? PreviousControllerId);

public sealed partial class CoreRuleEngine
{
    internal const string ConquestDamageEffect="UNIT_CONQUEST_SOURCE_POWER_DAMAGE";
    private static ConquestDamageSpec? ConquestDamageDefinition(string no)
        => CardBehaviorRegistry.TryGetByCardNo(no,out var behavior) ? behavior.ConquestDamage : null;
    internal static bool ValidUnitConquest(UnitConquestContext c,string effect,string? no=null)
        => effect==ConquestDamageEffect && c.SourceGeneration>=0 && !string.IsNullOrWhiteSpace(c.BattlefieldId)
            && (no is null || no==c.CardNo) && ConquestDamageDefinition(c.CardNo) is { } spec
            && (!spec.RequiresUncontrolledBattlefield || c.PreviousControllerId is null);

    private static IEnumerable<TriggerQueueItemState> CaptureConquestDamage(MatchState before,MatchState after,string player,
        string field,IReadOnlyList<string> units,long tick)
    {
        if (!before.BattlefieldStates.TryGetValue(field,out var battlefield)) yield break;
        foreach(var id in units.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!after.CardObjects.TryGetValue(id,out var source) || source.CardNo is null || source.ControllerId!=player
                || source.IsFaceDown || !source.Tags.Contains(CardObjectTags.UnitCard) || source.Tags.Contains(CardObjectTags.Standby)
                || BattlefieldLocalRules.AtUnit(after,id)?.ObjectId!=field || ConquestDamageDefinition(source.CardNo) is not { } spec
                || spec.RequiresUncontrolledBattlefield && battlefield.ControllerId is not null) continue;
            var repeats=ResolveUnitConquestEffectRepeatCount(after.PlayerZones,after.CardObjects,after.ObjectLocations,player,field,"BATTLEFIELD_CONQUERED");
            for(var i=0;i<repeats;i++) yield return new TriggerQueueItemState($"conquest-damage-{tick}-{field}-{id}-{i}",player,id,ConquestDamageEffect,"BATTLEFIELD_CONQUERED") {
                UnitConquest=new(source.CardNo,source.ObjectGeneration,field,battlefield.ControllerId) };
        }
    }
    private static string[] ConquestDamageTargets(MatchState state,StackItemState item)
        => state.PlayerZones.Where(p=>p.Key!=item.ControllerId).SelectMany(p=>p.Value.Base)
            .Where(id=>state.CardObjects.TryGetValue(id,out var c) && c.ControllerId!=item.ControllerId
                && !c.IsFaceDown && c.Tags.Contains(CardObjectTags.UnitCard) && !c.Tags.Contains(CardObjectTags.Standby)
                && TargetProtectionRules.IsLegalActivatedSkillTarget(state,item.ControllerId,id)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    private static PendingCardChoiceState ConquestTargetChoice(MatchState state,StackItemState item)
        => new("UNIT-CONQUEST-TARGET:"+item.StackItemId,"TRIGGER_CONFIRMATION",item.ControllerId,1,1,
            ConquestDamageTargets(state,item),[item.UnitConquest!.BattlefieldId],
            "选择基地中的一名敌方单位。确认目标和法盾费用后双方可响应；结算时造成等同于来源当前战力的伤害。",
            item.SourceObjectId,item.EffectKind) { ResolvingStackItemId=item.StackItemId };
    private static ResolutionResult PrepareConquestTarget(ResolutionResult result,StackItemState item)
    {
        var choice=ConquestTargetChoice(result.State,item);
        if(choice.LegalObjectIds.Count==0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result,item));
        var state=result.State with {PendingCardChoice=choice,PriorityPlayerId=null,ActivePlayerId=item.ControllerId};
        return result with {State=state,Snapshots=ResolutionResult.BuildSnapshots(state),Prompts=BuildCorePrompts(state)};
    }
    private static bool ValidConquestTargetChoice(MatchState state,PendingCardChoiceState choice)
    {
        var item=state.StackItems.FirstOrDefault(i=>i.StackItemId==choice.ResolvingStackItemId);
        if(item?.UnitConquest is null || item.TargetGenerations is not null || !ValidUnitConquest(item.UnitConquest,item.EffectKind,item.CardNo)) return false;
        var expected=ConquestTargetChoice(state,item);
        return choice.ChoiceId==expected.ChoiceId && choice.ChoiceWindow==expected.ChoiceWindow && choice.PlayerId==expected.PlayerId
            && choice.SourceObjectId==expected.SourceObjectId && choice.EffectKind==expected.EffectKind && choice.RequiredCount==1 && choice.MaxCount==1
            && choice.LegalObjectIds.Count>0 && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds) && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }
    internal static bool ValidConquestDamageStack(MatchState state,StackItemState item)
    {
        if(item.UnitConquest is not { } context) return item.EffectKind!=ConquestDamageEffect;
        if(!ValidUnitConquest(context,item.EffectKind,item.CardNo)) return false;
        return item.TargetGenerations is { } generations
            ? item.TargetObjectIds.Count==1 && generations.Count==1 && generations.TryGetValue(item.TargetObjectIds[0],out var g) && g>=0
            : item.TargetObjectIds.Count==0 && state.PriorityPlayerId is null
                && (state.PendingCardChoice is not null || state.TriggerQueue.Count>0);
    }
    private static StackResolutionResult ResolveConquestDamage(MatchState state,StackItemState item)
    {
        var context=item.UnitConquest!;
        var result=NoopStackResolutionResult(state);
        if(item.TargetObjectIds.SingleOrDefault() is not { } target || !ConquestDamageTargets(state,item).Contains(target)
            || item.TargetGenerations?.GetValueOrDefault(target,-1)!=state.CardObjects.GetValueOrDefault(target)?.ObjectGeneration
            || !state.CardObjects.TryGetValue(item.SourceObjectId,out var source) || source.ObjectGeneration!=context.SourceGeneration
            || !IsObjectOnField(state.PlayerZones,source.ObjectId)) return result;
        var amount=ResolveCurrentFieldUnitPower(state,source);if(amount<=0) return result;
        var zones=NormalizeZonesForSeats(state);var cards=state.CardObjects.ToDictionary(e=>e.Key,e=>e.Value);
        var destroyed=new HashSet<string>(StringComparer.Ordinal);
        var prevention=state.UntilEndOfTurnEffects.Contains(PreventSpellAndSkillDamageThisTurnEffectId);
        var damage=ApplyDamageToCardObject(cards,item.ControllerId,target,amount,destroyed,prevention,PreventSpellAndSkillDamageThisTurnEffectId);
        var events=new List<GameEvent> {new("DAMAGE_APPLIED","征服技能造成战力伤害",BuildDamagePayload(item.SourceObjectId,target,damage))};
        var locations=state.ObjectLocations.ToDictionary(e=>e.Key,e=>e.Value);
        var cleanup=RunStateBasedCleanupLoop(state,zones,cards,item,state.RunePools,damageTriggeredDestroyTargetObjectIds:destroyed,
            objectLocations:locations,destroyedUnitOwnerIdsAlreadyThisTurn:state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        return result with {PlayerZones=zones,CardObjects=cards,Events=events,RunePools=cleanup.RunePools,
            TriggerQueue=cleanup.TriggerQueue,DestroyedUnitOwnerIds=cleanup.DestroyedUnitOwnerIds,ObjectLocations=ReconcileObjectLocations(locations,zones)};
    }
}

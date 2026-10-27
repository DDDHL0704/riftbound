using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static IReadOnlyList<string> StandbyPlaySources(MatchState state,string player)
        => state.PlayerZones.TryGetValue(player,out var zones) ? zones.Battlefields.Where(id=>
            state.CardObjects.TryGetValue(id,out var card) && card.ControllerId==player && StandbyRules.ValidPlacement(state,card)
            && state.TurnNumber>card.StandbyPlacement!.TurnNumber
            && !StandbyRules.EnemyPreventsPlayHere(state,player,card.StandbyPlacement.Battlefield.ObjectId)).ToArray() : [];

    internal static CardBehaviorDefinition StandbyPlayBehavior(MatchState state,string player,string source,CardBehaviorDefinition behavior)
        => StandbyPlaySources(state,player).Contains(source) ? StandbyDefinition(behavior with {
            CanPlayDuringPriority=true, CanPlayDuringSpellDuel=true, IgnorePrintedPowerCost=true,
            StandbyPlayFrom=state.CardObjects[source].StandbyPlacement }, state.CardObjects[source].StandbyPlacement) : behavior;

    private static bool HasStandbyCountedPlayAbility(CardBehaviorDefinition behavior)
        => behavior.StandbyPlayFrom is not null && behavior.StandbyReactionMainDeckLookCount > 0;

    private static bool HasStandbyAttachmentPlayAbility(CardBehaviorDefinition behavior)
        => behavior.StandbyPlayFrom is not null && behavior.PlaysSourceToBaseAsEquipment && StandbyRules.AttachesSelfOnPlay(behavior.CardNo);

    private static CardBehaviorDefinition StandbyDefinition(CardBehaviorDefinition behavior, StandbyPlacement? origin)
        => origin is not null && behavior.StandbyReactionMainDeckLookCount > 0
            ? behavior with { StandbyPlayFrom = origin, RequiredTargetCount = behavior.StandbyReactionMaxTargetCount,
                MinTargetCount = behavior.StandbyReactionMaxTargetCount, TargetScope = behavior.StandbyReactionTargetScope }
            : origin is not null && behavior.PlaysSourceToBaseAsEquipment && StandbyRules.AttachesSelfOnPlay(behavior.CardNo)
                ? behavior with { StandbyPlayFrom = origin, RequiredTargetCount = 1, MinTargetCount = 1,
                    TargetScope = CardTargetScopes.FriendlyBattlefieldUnit, TargetRequiredTag = CardObjectTags.UnitCard }
                : behavior with { StandbyPlayFrom = origin };

    internal static string StandbyPlayDestination(CardBehaviorDefinition behavior,string destination)
        => behavior.StandbyPlayFrom is { } origin && (behavior.PlaysSourceToBaseAsUnit || behavior.PlaysSourceToBaseAsEquipment)
            && string.IsNullOrEmpty(destination) ? "BATTLEFIELD:"+origin.Battlefield.ObjectId : destination;

    internal static bool StandbyTargetAllowed(MatchState state,CardBehaviorDefinition behavior,string target,int index)
    {
        if(behavior.StandbyPlayFrom is not { } origin)return true;
        var scope=PlayCardTargetScopeForBehavior(behavior);
        // Only explicit nonlocal instructions override the original battlefield.
        if(scope is CardTargetScopes.BaseUnit or CardTargetScopes.FriendlyBaseUnit or CardTargetScopes.Legend
            or CardTargetScopes.FriendlyHandCard or CardTargetScopes.AnyHandCard or CardTargetScopes.OpponentHandCard
            or CardTargetScopes.FriendlyGraveyardCard or CardTargetScopes.OpponentGraveyardCard or CardTargetScopes.AnyGraveyardCard
            or CardTargetScopes.OpponentMainDeckTopCard or CardTargetScopes.AnyMainDeckTopFiveCard or CardTargetScopes.StackSpell
            || scope==CardTargetScopes.FriendlyMainDeckCard && index>=behavior.MainDeckLookTargetStartIndex
            || scope==CardTargetScopes.FriendlyHandCardThenBattlefieldUnit && index==0
            || (scope is CardTargetScopes.FriendlyUnitThenStackSpell or CardTargetScopes.FriendlyBattlefieldUnitThenStackSpell) && index>0
            || behavior.SwapsSourceWithFirstTargetLocation || behavior.SwapsTargetLocations && index>0)return true;
        return state.CardObjects.TryGetValue(origin.Battlefield.ObjectId,out var field) && field.ObjectGeneration==origin.Battlefield.Generation
            && state.ObjectLocations.TryGetValue(target,out var location) && location.Zone=="BATTLEFIELD"
            && (target==origin.Battlefield.ObjectId || location.BattlefieldObjectId==origin.Battlefield.ObjectId);
    }

    internal static IReadOnlyList<string> StandbyHideSources(MatchState state,string player)
        => state.PlayerZones.TryGetValue(player,out var zones) ? zones.Hand.Concat(zones.ChampionZone).Where(id=>
            state.CardObjects.TryGetValue(id,out var card) && !card.IsFaceDown && SourceObjectControlledByPlayerOrLegacyOwned(card,player)
            && CardBehaviorRegistry.TryGetByCardNo(card.CardNo??"",out _)
            && StandbyRules.HasPrintedKeyword(card.CardNo))
            .Distinct(StringComparer.Ordinal).ToArray() : [];

    internal static IReadOnlyList<string> StandbyHideBattlefields(MatchState state,string player)
        => BattlefieldLocalRules.ControlledBy(state,player).Where(field=>
        {
            var capacity=TryGetBattlefieldExtraStandbyObject(state.PlayerZones,state.CardObjects,player,field.ObjectId,out _) ? 2 : 1;
            return state.CardObjects.Values.Count(card=>card.IsFaceDown && state.ObjectLocations.TryGetValue(card.ObjectId,out var at)
                && at.Zone=="BATTLEFIELD" && at.BattlefieldObjectId==field.ObjectId) < capacity;
        }).Select(field=>field.ObjectId).Order(StringComparer.Ordinal).ToArray();

    private static ResolutionResult ResolveStandbyHide(MatchState state,PlayerIntent intent,HideCardCommand command)
    {
        if (state.Phase!=MatchPhases.Main || state.TimingState!=TimingStates.NeutralOpen
            || state.TurnPlayerId!=intent.PlayerId || state.StackItems.Count>0)
            return RejectWithCorePrompts(state,"待命埋伏只能在当前玩家的开放主阶段提交。",ErrorCodes.PhaseNotAllowed);
        if (!CardBehaviorRegistry.TryGetByCardNo(command.CardNo, out var behavior))
            return RejectWithCorePrompts(state, $"暂不支持该牌的待命埋伏行为：{command.CardNo}", ErrorCodes.UnsupportedCardBehavior);
        if (!state.PlayerZones.TryGetValue(intent.PlayerId, out var ownZones)
            || !ownZones.Hand.Concat(ownZones.ChampionZone).Contains(command.SourceObjectId))
            return RejectWithCorePrompts(state, "待命埋伏只能选择自己手牌中的牌。", ErrorCodes.CardNotInHand);
        var costs = NormalizeOptionalCosts(command.OptionalCosts);
        var teemoPermission = HasTeemoStandbyHidePermission(state, intent.PlayerId);
        var freePermission = HasFreeStandbyHidePermission(state, intent.PlayerId);
        if (costs.Count != 1 || costs[0] is not (StandbyHideOptionalCost or StandbyHideTeemoOptionalCost or StandbyHideFreeOptionalCost)
            || costs[0] == StandbyHideTeemoOptionalCost && !teemoPermission
            || costs[0] == StandbyHideFreeOptionalCost && !freePermission)
            return RejectWithCorePrompts(state, $"{behavior.DisplayName} 不能以该费用进行待命埋伏。", ErrorCodes.UnsupportedCardBehavior);
        if (!TryParseBattlefieldDestination(command.Destination, out var fieldId))
            return RejectWithCorePrompts(state, $"{behavior.DisplayName} 不能以该去向进行待命埋伏。", ErrorCodes.UnsupportedCardBehavior);
        if (!state.CardObjects.TryGetValue(command.SourceObjectId, out var source) || string.IsNullOrWhiteSpace(source.CardNo))
            return RejectWithCorePrompts(state, "待命埋伏需要服务端已确认的手牌信息。", ErrorCodes.InvalidTarget);
        if (source.CardNo != command.CardNo)
            return RejectWithCorePrompts(state, "待命埋伏的手牌信息与提交的牌不匹配。", ErrorCodes.InvalidTarget);
        if (!SourceObjectControlledByPlayerOrLegacyOwned(source, intent.PlayerId))
            return RejectWithCorePrompts(state, "待命埋伏只能选择自己控制的手牌。", ErrorCodes.InvalidTarget);
        if (source.IsFaceDown)
            return RejectWithCorePrompts(state, "待命布置来源必须正面朝上。", ErrorCodes.InvalidTarget);
        var permission = costs[0] switch {
            StandbyHideOptionalCost when StandbyRules.HasPrintedKeyword(source.CardNo) => "PRINTED",
            StandbyHideTeemoOptionalCost when teemoPermission && StandbyRules.HasPrintedKeyword(source.CardNo) => "TEEMO",
            StandbyHideFreeOptionalCost when freePermission && StandbyRules.HasPrintedKeyword(source.CardNo) => "FREE",
            _ => "" };
        if (permission.Length == 0)
            return RejectWithCorePrompts(state, $"{behavior.DisplayName} 没有待命关键词，不能进行待命埋伏。", ErrorCodes.UnsupportedCardBehavior);
        if (!StandbyHideBattlefields(state, intent.PlayerId).Contains(fieldId))
            return RejectWithCorePrompts(state, "请选择你控制且仍有待命空位的战场。", ErrorCodes.InvalidTarget);
        var mana=permission=="TEEMO" ? 1 : 0;var power=permission=="PRINTED" ? 1 : 0;
        var payment=new PaymentCostRules.PaymentPlan(PaymentCostRules.BuildPaymentId(state.Tick+1,"HIDE_CARD",intent.PlayerId,command.SourceObjectId),
            "HIDE_CARD",intent.PlayerId,baseManaCost:mana,totalManaCost:mana,genericPowerCost:power,totalPowerCost:power,
            optionalCostIds:costs,reason:"STANDBY_HIDE",sourceObjectId:command.SourceObjectId);
        var committed=PaymentCostRules.TryCommitPayment(payment,state.RunePools,state.PlayerExperience, state: state);
        if(!committed.Accepted)return RejectWithCorePrompts(state,"待命所需资源不足。",ErrorCodes.InsufficientCost);
        var zones=NormalizeZonesForSeats(state);var own=zones[intent.PlayerId];
        zones[intent.PlayerId]=own with {Hand=RemoveFromZone(own.Hand,source.ObjectId),ChampionZone=RemoveFromZone(own.ChampionZone,source.ObjectId),
            Battlefields=own.Battlefields.Append(source.ObjectId).ToArray()};
        var placement=new StandbyPlacement(source.ObjectId,source.ObjectGeneration+1,source.CardNo!,intent.PlayerId,
            new(fieldId,state.CardObjects[fieldId].ObjectGeneration),state.TurnNumber,state.Tick+1,permission,StandbyRules.TextHash(source.CardNo!));
        var cards=state.CardObjects.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
        cards[source.ObjectId]=source with {IsFaceDown=true,IsAttacking=false,IsDefending=false,IsExhausted=false,
            ControllerId=intent.PlayerId,StandbyPlacement=placement,Tags=source.Tags.Append(CardObjectTags.Standby).Distinct(StringComparer.Ordinal).OrderBy(tag=>tag,StringComparer.Ordinal).ToArray()};
        var locations=ReconcileObjectLocations(state.ObjectLocations,zones);locations[source.ObjectId]=new(intent.PlayerId,"BATTLEFIELD",fieldId);
        var events=new List<GameEvent> {
            new("COST_PAID","支付待命布置费用",PaymentCostRules.BuildCostPaidPayload(payment,committed.RunePools,committed.PlayerExperience,
                new Dictionary<string,object?> { ["mana"]=mana,["power"]=power,
                    ["standbyHideCostWaived"]=permission=="FREE", ["teemoStandbyHideReplacement"]=permission=="TEEMO" })),
            new("CARD_HIDDEN","在战场的待命区布置卡牌",new Dictionary<string,object?> {
                ["playerId"]=intent.PlayerId,["sourceObjectId"]=source.ObjectId,["destination"]="BATTLEFIELD:"+fieldId,
                ["destinationZone"]="BATTLEFIELD",["battlefieldObjectId"]=fieldId,["isFaceDown"]=true }) };
        var next=state with {Tick=state.Tick+1,PlayerZones=zones,CardObjects=cards,ObjectLocations=locations,
            RunePools=committed.RunePools,PlayerExperience=committed.PlayerExperience};
        return new(true,null,next,events,ResolutionResult.BuildSnapshots(next),BuildCorePrompts(next));
    }
}

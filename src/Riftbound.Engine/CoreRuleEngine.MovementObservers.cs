using Riftbound.Contracts;
using System.Text.Json.Serialization;

namespace Riftbound.Engine;

public sealed record MovedUnitFact(string ObjectId, long Generation, string ControllerId, string MovingPlayerId,
    ObjectLocationState Origin, ObjectLocationState Destination);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MovementObserverContext(string CardNo, string Kind, long SourceGeneration, MovedUnitFact Unit,
    IReadOnlyList<MovedUnitFact>? MovementBatch = null, ObjectLocationState? SourceLocation = null, int? MovementOrdinal = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public MovementDiscardInlineContext? DiscardInline { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RuleTextInstanceId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuleTextInstanceBinding? Origin { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? SourceWasEmpowered { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetTokenFactoryCardNo { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ControlledRunesAtTrigger { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mode { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SelectedPlayerId { get; init; }
}
public sealed record MovementDiscardInlineContext(ObjectBinding DiscardedCard, string CardNo, int Types,
    int BranchType, long CreatedAtTick);
internal sealed record CapturedMovementObserver(string SourceId, string ControllerId, MovementObserverContext Context);

public sealed partial class CoreRuleEngine
{
    internal const string MovementObserverEffect = "CAPTURED_UNIT_MOVEMENT_OBSERVER";
    private const string CapturedMovementEvent = "INTERNAL_MOVEMENT_OBSERVERS";
    private const string MovementResourceKind = "MOVEMENT_RESOURCE";

    private static bool ValidMovedUnitFact(MovedUnitFact? unit)
        => unit is { Generation: >= 0 } && !string.IsNullOrWhiteSpace(unit.ObjectId)
            && !string.IsNullOrWhiteSpace(unit.ControllerId) && !string.IsNullOrWhiteSpace(unit.MovingPlayerId)
            && unit.Origin is not null && unit.Destination is not null && unit.Origin != unit.Destination
            && unit.Origin.PlayerId == unit.ControllerId && unit.Destination.PlayerId == unit.ControllerId
            && unit.Origin.Zone is "BASE" or "BATTLEFIELD" && unit.Destination.Zone is "BASE" or "BATTLEFIELD";

    internal static bool ValidMovementObserver(MovementObserverContext c, string effect, string controller, string source, string? cardNo = null)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(controller)
            || (effect == MovementObserverEffect ? c.DiscardInline is not null
                : effect != MovementDiscardInlineEffect || c.DiscardInline is null)
            || c.DiscardInline is not null && (c.Kind != TriggerKinds.UnitMovedDrawDiscardTypes || !ValidMovementDiscardInline(c.DiscardInline))
            || c.SourceGeneration < 0 || c.SourceWasEmpowered == false || cardNo is not null && cardNo != c.CardNo
            || c.TargetTokenFactoryCardNo is not null && (c.Kind is not (TriggerKinds.UnitMovedDamageAtOriginOrDestination or TriggerKinds.UnitMovedSetOtherUnitBaseMight or TriggerKinds.UnitMovedSourceKeywordDamage)
                || !P6TokenFactoryCatalog.IsUnitTokenFactory(c.TargetTokenFactoryCardNo))
            || !ValidMovedUnitFact(c.Unit)
            || (c.Origin is null ? c.RuleTextInstanceId is not null
                : c.Origin.Unit is not { } bound || bound.ObjectId != source || bound.Generation != c.SourceGeneration
                    || c.Origin.CardNo != c.CardNo || c.Origin.Equipment is not { Generation: >= 0 } equipment
                    || c.Origin.Revision <= 0 || c.RuleTextInstanceId != $"ATTACHED:{equipment.ObjectId}:{equipment.Generation}:{c.Origin.Revision}")) return false;
        var unit = c.Unit;
        if (!ValidMovementInstructionShape(c)) return false;
        if (c.Kind == MovementScoreKind) return source == unit.ObjectId && controller == unit.ControllerId && c.SourceGeneration == unit.Generation
            && c.SourceWasEmpowered is null && c.MovementBatch is null && c.SourceLocation is null && MovementScoreDefinition(c.CardNo) is { } score && c.MovementOrdinal == score.Ordinal;
        var ordinalSpec = UnitMovedTriggerSpecRules.MovementTriggers(c.CardNo).FirstOrDefault(t => t.Kind == c.Kind);
        if (c.Kind != TriggerKinds.UnitFirstMovementMillPlayer && c.MovementOrdinal != ordinalSpec?.MovementOrdinal) return false;
        if (c.Kind == TriggerKinds.OpponentMovedToOtherBattlefieldDraw)
        {
            if (c.SourceWasEmpowered is not null || c.MovementBatch is not { Count: > 0 } batch || c.SourceLocation is not { } location
                || location.PlayerId != controller || location.Zone is not ("BASE" or "BATTLEFIELD")
                || location.Zone == "BATTLEFIELD" && string.IsNullOrWhiteSpace(location.BattlefieldObjectId)
                || unit.Destination.Zone != "BATTLEFIELD" || string.IsNullOrWhiteSpace(unit.Destination.BattlefieldObjectId)
                || unit.MovingPlayerId == controller || location.Zone == "BATTLEFIELD" && location.BattlefieldObjectId == unit.Destination.BattlefieldObjectId
                || !batch.Contains(unit) || batch.Any(f => !ValidMovedUnitFact(f))
                || batch.Select(f => f.ObjectId).Distinct(StringComparer.Ordinal).Count() != batch.Count
                || !UnitMovedTriggerSpecRules.MovementTriggers(c.CardNo).Any(UnitMovedTriggerSpecRules.IsOpponentMovementDrawTrigger)) return false;
            var sourceMove = batch.FirstOrDefault(f => f.ObjectId == source);
            return sourceMove is null || sourceMove.Generation == c.SourceGeneration && sourceMove.Destination == location;
        }
        if (c.MovementBatch is not null || c.SourceLocation is not null) return false;
        return c.Kind != TriggerKinds.BattlefieldUnitMovedAwayPowerModifier
            ? source == unit.ObjectId && controller == unit.ControllerId && c.SourceGeneration == unit.Generation
                && UnitMovedTriggerSpecRules.MovementTriggers(c.CardNo).Any(t => t.Kind == c.Kind
                    && UnitMovedTriggerSpecRules.MatchesMovement(t, unit) && MovementEmpowermentMatches(t, c))
            : c.SourceWasEmpowered is null && unit.Origin.Zone == "BATTLEFIELD" && source == unit.Origin.BattlefieldObjectId
                && BattlefieldTriggerSpecRules.TryGetTrigger(c.CardNo, BattlefieldTriggerSpecRules.IsBattlefieldMovedUnitPowerModifierTrigger, out _);
    }

    private static bool MovementEmpowermentMatches(TriggerSpec spec, MovementObserverContext captured)
        => spec.RequiresSourceEmpowered == true ? captured.SourceWasEmpowered == true : captured.SourceWasEmpowered is null;

    internal static bool ValidMovementObserver(MatchState state, MovementObserverContext c, string effect,
        string controller, string source, string? cardNo = null)
        => ValidMovementObserver(c, effect, controller, source, cardNo)
            && (c.SelectedPlayerId is null || state.Seats.ContainsKey(c.SelectedPlayerId))
            && AttachedRuleTextRules.ValidCaptured(state, c.Origin, source, c.SourceGeneration, c.CardNo);

    internal static bool ValidMovementInstanceId(MovementObserverContext c, string? id, bool stack = false)
        => id is not null && id.StartsWith(stack ? "ordered-copied-movement-" : "copied-movement-", StringComparison.Ordinal) == (c.Origin is not null)
            && (c.RuleTextInstanceId is null || id.EndsWith(":" + c.RuleTextInstanceId, StringComparison.Ordinal));

    internal static MovementObserverContext? PublicMovementObserver(MovementObserverContext? c)
        => c is null || c.Origin is null ? c : c with { RuleTextInstanceId = null, Origin = null };

    internal static bool ValidMovementDiscardChoice(MatchState state)
    {
        var p = state.PendingHandChoice;
        if (p is null) return true;
        if (p.MovementObserver is null) return p.EffectKind is not (TriggerKinds.UnitMovedDiscardDraw or TriggerKinds.UnitMovedDrawDiscardTypes)
            && p.ChoiceWindow is not (TriggerKinds.UnitMovedDiscardDraw or TriggerKinds.UnitMovedDrawDiscardTypes)
            && p.Reason is not (TriggerKinds.UnitMovedDiscardDraw or TriggerKinds.UnitMovedDrawDiscardTypes);
        var c = p.MovementObserver;
        var spec = UnitMovedTriggerSpecRules.MovementTriggers(c.CardNo).FirstOrDefault(t => t.Kind == c.Kind);
        var drawnFirst = c.Kind == TriggerKinds.UnitMovedDrawDiscardTypes;
        return (drawnFirst || c.Kind == TriggerKinds.UnitMovedDiscardDraw) && c.DiscardInline is null
            && p.EffectKind == c.Kind && p.ChoiceWindow == c.Kind && p.Reason == c.Kind
            && ValidMovementObserver(state, c, MovementObserverEffect, p.PlayerId, p.SourceObjectId)
            && p.ChoiceId is { } choiceId && ValidMovementInstanceId(c, choiceId.StartsWith("CHOICE-", StringComparison.Ordinal)
                ? choiceId["CHOICE-".Length..] : choiceId, stack: true)
            && state.Seats.ContainsKey(c.Unit.MovingPlayerId) && state.PlayerZones.TryGetValue(p.PlayerId, out var zones)
            && spec is not null && p.DrawCount == (drawnFirst ? 0 : spec.DrawCount)
            && p.RequiredCount == Math.Min(spec.DiscardCount!.Value, zones.Hand.Count)
            && p.MaxCount == p.RequiredCount && p.RequiredCount > 0
            && p.LegalObjectIds.Order(StringComparer.Ordinal).SequenceEqual(zones.Hand.Order(StringComparer.Ordinal));
    }

    internal static string MovementObserverLabel(MovementObserverContext c)
        => OfficialCardSourceIdentityGroups.DisplayName(c.CardNo)
            + (c.DiscardInline is { } inline ? inline.BranchType switch {
                1 => "：弃置单位后，本回合战力 +3",
                2 => "：弃置装备后，让最多两枚符文变为活跃状态",
                _ => "：弃置法术后，抽一张牌" }
            : c.Kind switch {
                MovementScoreKind => "：第三次移动后获得 1 分",
                TriggerKinds.UnitMovedSetOtherUnitBaseMight => "：移动后可设置另一名单位本回合的基础战力",
                TriggerKinds.UnitMovedDamageAtOriginOrDestination => "：移动后可对起点或终点战场的一名单位造成伤害",
                TriggerKinds.UnitMovedSourceKeywordDamage => "：移动后可对战场上的一名单位造成等同于坚守数值的伤害",
                TriggerKinds.UnitMovedCreateDormantGold => "：移动后打出休眠金币",
                TriggerKinds.UnitMovedCreateUnitToken => "：移动后打出单位指示物",
                TriggerKinds.UnitMovedDiscardDraw => "：移动后弃牌再抽牌",
                TriggerKinds.UnitMovedDrawDiscardTypes => "：移动后先抽牌，再弃置手牌",
                TriggerKinds.OpponentMovedToOtherBattlefieldDraw => "：对手移动到其他战场后抽牌",
                TriggerKinds.UnitMovedDraw => "：移动后抽牌",
                TriggerKinds.UnitFirstMovementMillPlayer => "：本回合首次移动，所选玩家燃烧",
                TriggerKinds.UnitOrdinalMovementGainResources => "：本回合首次移动，立即获得资源",
                TriggerKinds.UnitOrdinalMovementBanishTopPlay => "：本回合第二次移动，放逐顶牌并获得临时打出权限",
                TriggerKinds.UnitMovedAllPlayersDrawOrDiscard => c.Mode switch {
                    "DRAW" => "：移动后每名玩家各抽一张牌",
                    "DISCARD" => "：移动后每名玩家各弃置一张手牌",
                    _ => "：移动后选择每名玩家各抽牌或弃牌" },
                TriggerKinds.UnitMovedRevealTopDrawUnitOrTrashPower => "：移动后展示顶牌，抽取单位或放入废牌堆并增加战力",
                TriggerKinds.UnitMovedRevealTopDrawPowerfulUnitOrRecycle => "：移动后展示顶牌，抽取强力单位或回收",
                TriggerKinds.UnitMovedGainExperience => "：移动后获得经验",
                TriggerKinds.UnitMovedPowerModifier => "：离开战场后本回合增加战力",
                _ => "：离开此处的单位本回合战力 +1" });

    private static MovedUnitFact? CaptureMovedUnitFact(MatchState before, string unitId, string movingPlayer,
        string destinationZone, string? destinationField = null)
    {
        if (!before.CardObjects.TryGetValue(unitId, out var unit) || !unit.Tags.Contains(CardObjectTags.UnitCard)
            || unit.IsFaceDown
            || FindFieldObjectLocation(before.PlayerZones, unitId) is not { } field) return null;
        var origin = before.ObjectLocations.GetValueOrDefault(unitId) ?? new(field.PlayerId, field.Zone);
        var destination = new ObjectLocationState(field.PlayerId, destinationZone, destinationField);
        return origin == destination ? null : new(unitId, unit.ObjectGeneration, field.PlayerId, movingPlayer, origin, destination);
    }

    private static GameEvent CaptureMovementObservers(MatchState before, IDictionary<string,CardObjectState> cards, string unitId, string movingPlayer,
        string destinationZone, string? destinationField = null)
        => CaptureMovementBatchObservers(before, cards, CaptureMovedUnitFact(before, unitId, movingPlayer, destinationZone, destinationField) is { } unit ? [unit] : []);

    // A movement instruction is captured once. Self triggers remain per unit; actor
    // triggers use the source's location after every simultaneous relocation (383.2.c).
    private static GameEvent CaptureMovementBatchObservers(MatchState before, IDictionary<string,CardObjectState> cards, IReadOnlyList<MovedUnitFact> units)
    {
        var observers = new List<CapturedMovementObserver>();
        void Add(string source, string controller, RuleTextInstance instance, MovementObserverContext context)
            => observers.Add(new(source, controller, context with {
                RuleTextInstanceId = instance.Origin is null ? null : instance.Id, Origin = instance.Origin }));
        foreach (var fact in units)
        {
            var unit = before.CardObjects[fact.ObjectId];
            var ordinal = checked(cards[fact.ObjectId].MovementCountThisTurn + 1);
            foreach (var instance in AttachedRuleTextRules.Instances(before, unit))
            {
                if (MovementScoreDefinition(instance.CardNo) is { } score && ordinal == score.Ordinal)
                    Add(unit.ObjectId, fact.ControllerId, instance, new(instance.CardNo, MovementScoreKind, unit.ObjectGeneration, fact, MovementOrdinal: ordinal));
                foreach (var spec in UnitMovedTriggerSpecRules.MovementTriggers(instance.CardNo)
                    .Where(t => (t.TargetScope == TriggerTargetScopes.SourceUnit || UnitMovedTriggerSpecRules.IsTargetedMovementTrigger(t))
                        && UnitMovedTriggerSpecRules.MatchesMovement(t, fact)
                        && (t.RequiresSourceEmpowered != true || unit.IsEmpowered)
                        && (t.OncePerTurn != true || ordinal == 1)
                        && (t.MovementOrdinal is null || t.MovementOrdinal == ordinal)
                        && (t.MaximumControlledRunes is not { } max || EmpowerRules.ControlledRunes(before, fact.ControllerId) <= max)))
                    Add(unit.ObjectId, fact.ControllerId, instance, new(instance.CardNo, spec.Kind, unit.ObjectGeneration, fact,
                        MovementOrdinal: spec.Kind == TriggerKinds.UnitFirstMovementMillPlayer || spec.MovementOrdinal is not null ? ordinal : null) {
                        SourceWasEmpowered = spec.RequiresSourceEmpowered == true ? true : null,
                        ControlledRunesAtTrigger = spec.MaximumControlledRunes is not null ? EmpowerRules.ControlledRunes(before, fact.ControllerId) : null });
                if (P4ActivatedAbilityCatalog.IsSourceCardNoForAbilityId(P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId, instance.CardNo))
                    Add(unit.ObjectId, fact.ControllerId, instance, new(instance.CardNo, MovementResourceKind, unit.ObjectGeneration, fact));
            }
            if (BattlefieldLocalRules.AtUnit(before, unit.ObjectId) is { } battlefield
                && BattlefieldTriggerSpecRules.TryGetTrigger(battlefield.CardNo, BattlefieldTriggerSpecRules.IsBattlefieldMovedUnitPowerModifierTrigger, out var bar))
            {
                var controller = before.BattlefieldStates.GetValueOrDefault(battlefield.ObjectId)?.ControllerId ?? before.TurnPlayerId;
                observers.Add(new(battlefield.ObjectId, controller, new(battlefield.CardNo!, bar.Kind, battlefield.ObjectGeneration, fact)));
            }
        }
        foreach (var source in before.CardObjects.Values)
        {
            if (source.IsFaceDown || !source.Tags.Contains(CardObjectTags.UnitCard) || source.IsFaceDown
                || FindFieldObjectLocation(before.PlayerZones, source.ObjectId) is not { } field) continue;
            var specs = AttachedRuleTextRules.Instances(before, source).SelectMany(instance =>
                UnitMovedTriggerSpecRules.MovementTriggers(instance.CardNo).Where(UnitMovedTriggerSpecRules.IsOpponentMovementDrawTrigger)
                    .Select(spec => (Instance: instance, Spec: spec))).ToArray();
            if (specs.Length == 0) continue;
            var after = units.FirstOrDefault(f => f.ObjectId == source.ObjectId)?.Destination
                ?? before.ObjectLocations.GetValueOrDefault(source.ObjectId) ?? new(field.PlayerId, field.Zone);
            if (after.Zone == "BATTLEFIELD" && string.IsNullOrWhiteSpace(after.BattlefieldObjectId)) continue;
            foreach (var group in units.Where(f => f.MovingPlayerId != field.PlayerId
                && f.Destination.Zone == "BATTLEFIELD" && !string.IsNullOrWhiteSpace(f.Destination.BattlefieldObjectId)
                && (after.Zone != "BATTLEFIELD" || after.BattlefieldObjectId != f.Destination.BattlefieldObjectId))
                .GroupBy(f => (f.MovingPlayerId, f.Destination.BattlefieldObjectId)))
                foreach (var (instance, spec) in specs)
                    Add(source.ObjectId, field.PlayerId, instance, new(instance.CardNo, spec.Kind, source.ObjectGeneration, group.First(), units.ToArray(), after));
        }
        foreach(var fact in units) cards[fact.ObjectId] = cards[fact.ObjectId] with {MovementCountThisTurn=checked(cards[fact.ObjectId].MovementCountThisTurn+1)};
        return new(CapturedMovementEvent, "捕获移动时的技能来源", new Dictionary<string, object?> { ["observers"] = observers.ToArray() });
    }

    private static ResolutionResult RecordMovementObservers(ResolutionResult result)
    {
        if (!result.Accepted || !result.Events.Any(e => e.Kind == CapturedMovementEvent)) return result;
        var events = new List<GameEvent>(); var queue = result.State.TriggerQueue.ToList();
        for (var index = 0; index < result.Events.Count; index++)
        {
            var ev = result.Events[index];
            if (ev.Kind != CapturedMovementEvent) { events.Add(ev); continue; }
            if (result.State.Status != MatchStatuses.InProgress) continue;
            foreach (var observer in (CapturedMovementObserver[])ev.Payload["observers"]!)
            {
                var resource = observer.Context.Kind == MovementResourceKind;
                var suffix = observer.Context.RuleTextInstanceId is { } instance ? ":" + instance : "";
                var trigger = new TriggerQueueItemState($"{(suffix.Length == 0 ? "" : "copied-")}movement-{result.State.Tick}-{index}-{observer.SourceId}-{observer.Context.Kind}-{observer.Context.Unit.ObjectId}{suffix}",
                    observer.ControllerId, observer.SourceId,
                    resource ? P4ActivatedAbilityCatalog.JhinMoveResourceAbilityEffectKind : MovementObserverEffect,
                    observer.Context.Unit.Destination.Zone == "BASE" ? "UNIT_MOVED_TO_BASE" : "UNIT_MOVED_TO_BATTLEFIELD") { MovementObserver = resource ? null : observer.Context, SourceCardNo = observer.Context.CardNo };
                queue.Add(trigger);
                var queued = BuildTriggerQueuedEvent(trigger);
                if (observer.Context.Kind is TriggerKinds.UnitMovedDamageAtOriginOrDestination or TriggerKinds.UnitMovedSetOtherUnitBaseMight or TriggerKinds.UnitMovedSourceKeywordDamage)
                    queued = queued with { Payload = new Dictionary<string, object?>(queued.Payload) {
                        ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(result.State, trigger.SourceObjectId, "__spectator__") } };
                events.Add(queued);
            }
        }
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static StackResolutionResult ResolveMovementObserver(MatchState state, StackItemState item)
    {
        var context = item.MovementObserver!;
        if (!ValidMovementObserver(state, context, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo))
            throw new InvalidOperationException("Invalid captured movement observer.");
        if (context.DiscardInline is not null) return ResolveMovementDiscardInline(state, item);
        if (context.Kind == MovementScoreKind) return ResolveMovementScore(state,item);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var events = new List<GameEvent> { BuildTriggerResolvedEvent(new(item.StackItemId, item.ControllerId,
            item.SourceObjectId, item.EffectKind, TriggerTimings.UnitMoved) { MovementObserver = context }) };
        var unitSpec = UnitMovedTriggerSpecRules.MovementTriggers(context.CardNo).FirstOrDefault(t => t.Kind == context.Kind
            && UnitMovedTriggerSpecRules.MatchesMovement(t, context.Unit) && MovementEmpowermentMatches(t, context));
        if (context.Kind == TriggerKinds.UnitOrdinalMovementBanishTopPlay)
            return ResolveMovementTimedBanishPlay(state, item, events);
        if (HasTargetedMovementSkill(item) || HasMovementDamage(item))
        {
            events[0] = events[0] with { Payload = new Dictionary<string, object?>(events[0].Payload) {
                ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__") } };
            return context.Kind == TriggerKinds.UnitMovedSetOtherUnitBaseMight
                ? ResolveMovementBaseMight(state, item, events) : ResolveMovementDamage(state, item, events);
        }
        if (context.Kind is TriggerKinds.UnitFirstMovementMillPlayer or TriggerKinds.UnitMovedAllPlayersDrawOrDiscard
            || unitSpec is { MillCount: > 0, Optional: true, PowerDelta: > 0 })
            return ResolveMovementInstruction(state, item, unitSpec!, events);
        if (unitSpec?.MaximumControlledRunes is { } maximum && EmpowerRules.ControlledRunes(state, item.ControllerId) > maximum)
            return NoopStackResolutionResult(state) with { Events = events };
        if (context.Kind == TriggerKinds.UnitMovedDrawDiscardTypes)
            return ResolveMovementDrawDiscardTypes(state, item, events[0]);
        if (unitSpec is not null && UnitMovedTriggerSpecRules.IsMovementRevealTrigger(unitSpec))
            return ResolveMovementReveal(state, item, unitSpec!, events);
        if (context.Kind == TriggerKinds.UnitMovedCreateUnitToken)
        {
            if (!TryMovementTokenInstruction(state, item, out var instruction))
                return NoopStackResolutionResult(state) with { Events = events };
            var created = CreateUnitTokenBatch(state, item, instruction.FactoryCardNo,
                instruction.Count + AdditionalUnitTokens(item), false, instruction.Location.BattlefieldObjectId, context.Kind);
            return created with { Events = events.Concat(created.Events).ToArray() };
        }
        if (context.Kind == TriggerKinds.UnitMovedDiscardDraw)
            return ResolveDiscardDrawStackItem(state, item, unitSpec!.DiscardCount!.Value, unitSpec.DrawCount!.Value,
                context.Kind, context.Kind, TriggerTimings.UnitMoved);
        if (context.Kind is TriggerKinds.UnitMovedDraw or TriggerKinds.OpponentMovedToOtherBattlefieldDraw)
        {
            var draw = ResolveLastBreathDrawStackItem(state, item, unitSpec!.DrawCount!.Value);
            return draw with { Events = draw.Events.Select(e => e.Kind == "TRIGGER_RESOLVED" ? events[0] : e).ToArray() };
        }
        if (context.Kind == TriggerKinds.UnitMovedGainExperience)
            return NoopStackResolutionResult(state) with { PlayerExperience = GainExperience(NormalizeExperienceForSeats(state),
                item.ControllerId, unitSpec!.ExperienceCount!.Value, item, events, item.SourceObjectId, context.CardNo), Events = events };
        if (context.Kind == TriggerKinds.UnitMovedCreateDormantGold)
        {
            UnitMovedTriggerSpecRules.TryGetTrigger(context.CardNo, UnitMovedTriggerSpecRules.IsUnitMovedCreateDormantGoldTrigger, out var spec);
            for (var i = 0; i < spec.CreatedTokenCount.GetValueOrDefault(); i++)
                CreateLegendEquipmentToken(zones, cards, item.ControllerId, item.SourceObjectId, spec.Kind,
                    spec.CreatedTokenName!, new[] { CardObjectTags.EquipmentCard, spec.CreatedTokenName! }
                        .Concat(spec.CreatedTokenKeywords ?? []).Distinct(StringComparer.Ordinal).ToArray(),
                    spec.CreatedTokenExhausted.GetValueOrDefault(true), events, capturedSourceCardNo: context.CardNo);
        }
        else if (cards.TryGetValue(context.Unit.ObjectId, out var unit) && unit.ObjectGeneration == context.Unit.Generation
            && IsObjectOnField(zones, unit.ObjectId) && unit.Tags.Contains(CardObjectTags.UnitCard))
        {
            var spec = unitSpec;
            if (spec is null) BattlefieldTriggerSpecRules.TryGetTrigger(context.CardNo, BattlefieldTriggerSpecRules.IsBattlefieldMovedUnitPowerModifierTrigger, out spec);
            var delta = spec!.PowerDelta.GetValueOrDefault();
            var behavior = new CardBehaviorDefinition(context.CardNo, OfficialCardSourceIdentityGroups.DisplayName(context.CardNo), 0, spec.Kind, 0, 0, PowerModifierAmount: delta);
            cards[unit.ObjectId] = ApplyPowerModifier(state, unit, behavior, item, unit.ObjectId, delta, out var powerEvent);
            events.Add(powerEvent);
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
    }
}

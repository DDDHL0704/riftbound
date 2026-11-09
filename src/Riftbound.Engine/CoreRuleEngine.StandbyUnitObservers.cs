using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StandbyUnitObservation(string ControllerId, string RuleTextInstanceId,
    long SourceFaceRevision, long CapturedTick, string ActionEventKind, string ActionHash,
    string? SourceTokenFactoryCardNo = null, CeasedEmpoweredSource? CeasedSource = null);

public sealed partial class CoreRuleEngine
{
    internal const string StandbyHiddenReadyEffect = "CONTROLLER_HIDDEN_SOURCE_READY";
    internal const string StandbyPlayedEnemyDamageEffect = "CONTROLLER_FACE_DOWN_PLAY_ENEMY_DAMAGE";
    internal const string StandbyPlayedEquipmentTokenEffect = "CONTROLLER_FACE_DOWN_PLAY_EQUIPMENT_TOKENS";

    internal static bool IsStandbyUnitObserverEffect(string effect)
        => effect is StandbyHiddenReadyEffect or StandbyPlayedEnemyDamageEffect or StandbyPlayedEquipmentTokenEffect;

    private static bool ValidStandbyEquipmentTokenReward(EquipmentTokenRewardSpec? reward)
        => reward is { Count: > 0 } && P6TokenFactoryCatalog.TryGetByCardNo(reward.FactoryCardNo, out var factory)
            && factory.Tags.Contains(CardObjectTags.EquipmentCard) && !factory.RequiresCopySource;

    internal static string StandbyObserverEventKind(string effect)
        => effect is StandbyHiddenTokenEffect or StandbyHiddenReadyEffect ? "CARD_HIDDEN" : "CARD_PLAYED_FROM_FACE_DOWN";

    // Do not retain the private identity of a hidden card in a projected skill.
    // The original action and the exact source/text capture can still be bound
    // to the original accepted journal when recovering the independent skill.
    internal static string StandbyUnitActionHash(GameEvent action)
    {
        var identity = new Dictionary<string, object?> { ["kind"] = action.Kind };
        foreach (var key in new[] { "playerId", "playedByPlayerId", "sourceObjectId", "unitObjectId",
            "targetObjectId", "cardNo", "destination", "battlefieldObjectId", "historyPlayId", "completedPlayHash", "playedFromStandby" })
            if (action.Payload.TryGetValue(key, out var value)) identity[key] = JsonSerializer.SerializeToElement(value);
        return MatchStateHasher.HashValue(identity);
    }

    internal static string StandbyUnitCaptureHash(StandbyPlayedObserverContext context)
        => MatchStateHasher.HashValue(context with { Unit = context.Unit! with { CeasedSource = null } });

    private static StandbyPlayedObserverContext? MarkCeasedStandbyUnit(MatchState before, MatchState after,
        IReadOnlySet<string> departed, StandbyPlayedObserverContext? context)
    {
        if (context?.Unit is not { CeasedSource: null } birth || !departed.Contains(context.Source.ObjectId)
            || before.CardObjects[context.Source.ObjectId] is not { } source
            || source.ObjectGeneration != context.Source.Generation
            || birth.SourceTokenFactoryCardNo != (source.TokenFactoryCardNo ?? source.CardNo)) return context;
        return context with { Unit = birth with { CeasedSource = new(after.Tick, context.Origin?.CardNo
            ?? CopyFaceTimeline.FaceAt(source, birth.SourceFaceRevision, birth.CapturedTick) ?? string.Empty) } };
    }

    private static IEnumerable<CapturedStandbyObserver> CaptureStandbyUnitObservers(GameEvent action,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        string controller, string effect)
    {
        foreach (var id in GetControlledFieldUnitObjectIds(zones, cards, controller).Order(StringComparer.Ordinal))
        {
            if (!cards.TryGetValue(id, out var source) || !IsFaceUpNonStandbyUnit(source)) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(zones, cards, source))
            {
                if (!CardBehaviorRegistry.TryGetByCardNo(instance.CardNo, out var behavior)
                    || (effect == StandbyHiddenReadyEffect ? !behavior.SourceReadiesWhenControllerHidesCard
                        : effect == StandbyPlayedEquipmentTokenEffect ? !ValidStandbyEquipmentTokenReward(behavior.FaceDownPlayEquipmentTokens)
                        : effect != StandbyPlayedEnemyDamageEffect || behavior.ControllerFaceDownPlayEnemyDamage <= 0)) continue;
                yield return new(controller, instance.Id,
                    new(instance.CardNo, new(source.ObjectId, source.ObjectGeneration), instance.Origin) {
                        Unit = new(controller, instance.Id,
                            instance.Origin?.FaceRevision ?? source.CopyFaceTransitions?.Count ?? 0, 0,
                            action.Kind, StandbyUnitActionHash(action),
                            IsTokenObject(source) ? source.TokenFactoryCardNo ?? source.CardNo : null) }, effect);
            }
        }
    }

    private static ResolutionResult QueueStandbyHiddenUnitObservers(ResolutionResult result, string controller)
    {
        if (!result.Accepted || result.State.Status != MatchStatuses.InProgress) return result;
        var events = result.Events.Select(action => {
            if (action.Kind != "CARD_HIDDEN") return action;
            var captures = CaptureStandbyUnitObservers(action, result.State.PlayerZones, result.State.CardObjects,
                controller, StandbyHiddenReadyEffect).ToArray();
            return captures.Length == 0 ? action : action with { Payload = new Dictionary<string, object?>(action.Payload) {
                ["standbyPlayObservers"] = captures } };
        }).ToArray();
        return QueueStandbyPlayedObservers(result with { Events = events });
    }

    private static bool ValidStandbyUnitObservation(MatchState state, StandbyPlayedObserverContext context,
        string effect, string sourceId, string? controller)
    {
        if (context.Draw is not null || context.BattlefieldId is not null
            || context.Unit is not { SourceFaceRevision: >= 0, CapturedTick: > 0,
                ControllerId.Length: > 0, RuleTextInstanceId.Length: > 0, ActionHash.Length: 64 } birth
            || !state.Seats.ContainsKey(birth.ControllerId) || controller is not null && controller != birth.ControllerId
            || birth.CapturedTick > state.Tick
            || (effect == StandbyHiddenReadyEffect ? birth.ActionEventKind != "CARD_HIDDEN"
                : effect is not (StandbyPlayedEnemyDamageEffect or StandbyPlayedEquipmentTokenEffect)
                    || birth.ActionEventKind is not ("CARD_PLAYED" or "UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD"
                        or "EQUIPMENT_PLAYED_TO_BASE" or "EQUIPMENT_PLAYED_TO_BATTLEFIELD" or FaceDownSpellCompletedEvent))
            || !CardBehaviorRegistry.TryGetByCardNo(context.CardNo, out var behavior)
            || (effect == StandbyHiddenReadyEffect ? !behavior.SourceReadiesWhenControllerHidesCard
                : effect == StandbyPlayedEquipmentTokenEffect ? !ValidStandbyEquipmentTokenReward(behavior.FaceDownPlayEquipmentTokens)
                : behavior.ControllerFaceDownPlayEnemyDamage <= 0)
            || !AttachedRuleTextRules.ValidCaptured(state, context.Origin, sourceId, context.Source.Generation, context.CardNo))
            return false;
        if (birth.CeasedSource is { } ceased)
        {
            if (birth.SourceTokenFactoryCardNo is not { } factory || state.CardObjects.ContainsKey(sourceId)
                || !P6TokenFactoryCatalog.TryGetByCardNo(factory, out var token) || !token.Tags.Contains(CardObjectTags.UnitCard)
                || ceased.CardNo != context.CardNo || ceased.Tick < birth.CapturedTick || ceased.Tick > state.Tick) return false;
        }
        else if (!state.CardObjects.TryGetValue(sourceId, out var current)
            || current.ObjectGeneration < context.Source.Generation
            || (IsTokenObject(current) ? current.TokenFactoryCardNo ?? current.CardNo : null) != birth.SourceTokenFactoryCardNo)
            return false;
        return context.Origin is null
            ? birth.RuleTextInstanceId == $"PRINTED:{sourceId}:{context.Source.Generation}"
                && (birth.CeasedSource is not null || PredictBindingMatches(state, context.Source, context.CardNo,
                    birth.SourceFaceRevision, birth.CapturedTick, birth.SourceTokenFactoryCardNo))
            : birth.RuleTextInstanceId == $"ATTACHED:{context.Origin.Equipment.ObjectId}:{context.Origin.Equipment.Generation}:{context.Origin.Revision}";
    }

    internal static bool ValidStandbyUnitObserverStack(MatchState state, StackItemState item)
    {
        if (!IsStandbyUnitObserverEffect(item.EffectKind)) return item.StandbyObserver?.Unit is null;
        if (item.StandbyObserver is not { Unit: not null } context
            || !ValidStandbyObserver(state, context, item.EffectKind, item.SourceObjectId, item.CardNo, item.ControllerId)
            || item.SourceConfirmed || item.DamageAmount != 0 || item.OptionalCosts.Count != 0 || item.EffectRepeatCount != 1
            || item.RepeatExecutions is not null || item.Destination.Length != 0 || item.TriggerCost is not null
            || item.FieldContext is not null || item.UnitEvent is not null || item.DamageDistribution is not null) return false;
        return item.EffectKind != StandbyPlayedEnemyDamageEffect || item.TargetGenerations is null
            ? item.TargetObjectIds.Count == 0 && item.TargetGenerations is null
            : item.TargetObjectIds.Count == 1 && item.TargetGenerations.Count == 1
                && item.TargetGenerations.GetValueOrDefault(item.TargetObjectIds[0], -1) >= 0;
    }

    private static string[] StandbyUnitDamageTargets(MatchState state, StackItemState item)
        => state.CardObjects.Values.Where(card => IsFaceUpNonStandbyUnit(card)
            && IsObjectOnField(state.PlayerZones, card.ObjectId)
            && EffectiveFieldControllerId(state.PlayerZones, card.ObjectId, card) != item.ControllerId
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, card.ObjectId))
            .Select(card => card.ObjectId).Order(StringComparer.Ordinal).ToArray();

    private static PendingCardChoiceState StandbyUnitDamageChoice(MatchState state, StackItemState item)
        => new("STANDBY-UNIT-TARGET:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId, 1, 1,
            StandbyUnitDamageTargets(state, item), StandbyUnitDamageTargets(state, item),
            "选择一名敌方单位；支付适用的法盾费用后，双方可以响应伤害技能。", item.SourceObjectId, item.EffectKind) {
                ResolvingStackItemId = item.StackItemId };

    private static ResolutionResult PrepareStandbyUnitDamage(ResolutionResult result, StackItemState item)
    {
        var choice = StandbyUnitDamageChoice(result.State, item);
        if (choice.LegalObjectIds.Count == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var state = result.State with { PendingCardChoice = choice, PriorityPlayerId = null, ActivePlayerId = item.ControllerId };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static bool ValidStandbyUnitDamageChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(item => item.StackItemId == choice.ResolvingStackItemId);
        if (item is null || item.EffectKind != StandbyPlayedEnemyDamageEffect || item.TargetGenerations is not null
            || !ValidStandbyUnitObserverStack(state, item)) return false;
        var expected = StandbyUnitDamageChoice(state, item);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow
            && choice.PlayerId == expected.PlayerId && choice.SourceObjectId == expected.SourceObjectId
            && choice.EffectKind == expected.EffectKind && choice.RequiredCount == 1 && choice.MaxCount == 1
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds)
            && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    private static StackResolutionResult ResolveStandbyUnitObserver(MatchState state, StackItemState item)
    {
        if (!ValidStandbyUnitObserverStack(state, item)) throw new InvalidOperationException("Invalid standby unit observer.");
        var context = item.StandbyObserver!;
        var visibleSource = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator);
        var events = new List<GameEvent> { new("TRIGGER_RESOLVED", "待命行动监听技能结算", new Dictionary<string, object?> {
            ["sourceObjectId"] = visibleSource, ["controllerId"] = item.ControllerId, ["effectKind"] = item.EffectKind }) };
        var result = NoopStackResolutionResult(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        if (item.EffectKind == StandbyPlayedEquipmentTokenEffect)
        {
            var reward = CardBehaviorRegistry.TryGetByCardNo(context.CardNo, out var behavior)
                ? behavior.FaceDownPlayEquipmentTokens : null;
            if (reward is null || !ValidStandbyEquipmentTokenReward(reward)) throw new InvalidOperationException("Invalid standby equipment token reward.");
            P6TokenFactoryCatalog.TryGetByCardNo(reward.FactoryCardNo, out var factory);
            var tokenZones = NormalizeZonesForSeats(state);
            for (var i = 0; i < reward.Count; i++)
                CreateLegendEquipmentToken(tokenZones, cards, item.ControllerId, item.SourceObjectId, item.EffectKind,
                    factory.TokenFamilyName, factory.Tags, true, events, context.CardNo,
                    publicSourceObjectId: visibleSource,
                    tokenIdentitySeed: "equipment-token-" + MatchStateHasher.HashValue(item.StackItemId));
            for (var i = 0; i < events.Count; i++)
                if (Equals(events[i].Payload.GetValueOrDefault("sourceObjectId"), item.SourceObjectId))
                    events[i] = events[i] with { Payload = new Dictionary<string, object?>(events[i].Payload) {
                        ["sourceObjectId"] = visibleSource } };
            return result with { PlayerZones = tokenZones, CardObjects = cards, Events = events,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, tokenZones) };
        }
        if (item.EffectKind == StandbyHiddenReadyEffect)
        {
            if (cards.TryGetValue(item.SourceObjectId, out var source) && source.ObjectGeneration == context.Source.Generation
                && source.IsExhausted && IsFaceUpNonStandbyUnit(source) && IsObjectOnField(state.PlayerZones, source.ObjectId)
                && !AttachedRuleTextRules.Instances(state, source)
                    .Any(instance => CardStaticAbilitySpecRules.CardCannotBecomeActive(instance.CardNo)))
            {
                var behavior = new CardBehaviorDefinition(context.CardNo, "藏匿后活跃来源", 0, item.EffectKind, 0, 0,
                    ReadiesTarget: true);
                cards[source.ObjectId] = ApplyReadyState(source, behavior, item, source.ObjectId, out var readied, state.PlayerZones, cards);
                if (readied is not null) events.Add(readied);
            }
            return result with { CardObjects = cards, Events = events };
        }
        var target = item.TargetObjectIds.SingleOrDefault();
        if (target is null || item.TargetGenerations?.GetValueOrDefault(target, -1) != cards.GetValueOrDefault(target)?.ObjectGeneration
            || !StandbyUnitDamageTargets(state, item).Contains(target, StringComparer.Ordinal)) return result with { Events = events };
        var zones = NormalizeZonesForSeats(state);
        var locations = state.ObjectLocations.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var destroyed = new HashSet<string>(StringComparer.Ordinal);
        var amount = ApplySpellSkillDamageBonuses(state, item.ControllerId, target,
            CardBehaviorRegistry.TryGetByCardNo(context.CardNo, out var printed) ? printed.ControllerFaceDownPlayEnemyDamage : 0, item);
        var damage = ApplyDamageToCardObject(state, cards, item.ControllerId, target, amount, destroyed,
            state.UntilEndOfTurnEffects.Contains(PreventSpellAndSkillDamageThisTurnEffectId), PreventSpellAndSkillDamageThisTurnEffectId);
        events.Add(new("DAMAGE_APPLIED", "从背面打出卡牌的技能对敌方单位造成伤害", BuildDamagePayload(visibleSource, target, damage)));
        var cleanup = RunStateBasedCleanupLoop(state, zones, cards, item, state.RunePools,
            damageTriggeredDestroyTargetObjectIds: destroyed, objectLocations: locations,
            destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        return result with { PlayerZones = zones, CardObjects = cards, ObjectLocations = ReconcileObjectLocations(locations, zones),
            RunePools = cleanup.RunePools, TriggerQueue = cleanup.TriggerQueue, DestroyedUnitOwnerIds = cleanup.DestroyedUnitOwnerIds,
            Events = events };
    }
}

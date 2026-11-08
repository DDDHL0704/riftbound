using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string MovedEnemyStunKind = "MOVED_ENEMY_OPTIONAL_EXHAUST_STUN";

    private static bool MovedEnemyStunDefinition(string no)
        => CardBehaviorRegistry.TryGetByCardNo(no, out var behavior) && behavior.MayExhaustToStunMovedEnemy;

    private static bool ValidMovedEnemyStunShape(MovementObserverContext context, string controller)
        => MovedEnemyStunDefinition(context.CardNo) && context.Unit.MovingPlayerId == controller
            && context.Unit.ControllerId != controller && context.SourceFaceRevision is >= 0 && context.CapturedTick is > 0
            && context.SourceWasEmpowered is null && context.TargetTokenFactoryCardNo is null
            && context.MovementBatch is null && context.SourceLocation is null && context.MovementOrdinal is null
            && context.ControlledRunesAtTrigger is null && context.Mode is null && context.SelectedPlayerId is null;

    private static bool ValidMovedEnemyStunSource(MatchState state, MovementObserverContext context, string source)
        => context.CapturedTick <= state.Tick && (context.Origin is not null
            || PredictBindingMatches(state, new(source, context.SourceGeneration), context.CardNo,
                context.SourceFaceRevision!.Value, context.CapturedTick!.Value, null));

    private static IEnumerable<CapturedMovementObserver> CaptureMovedEnemyStunObservers(MatchState state,
        IReadOnlyList<MovedUnitFact> units)
    {
        foreach (var source in state.CardObjects.Values.Where(source => !source.IsFaceDown
            && (source.Tags.Contains(CardObjectTags.UnitCard) || source.Tags.Contains(CardObjectTags.EquipmentCard))
            && IsObjectOnField(state.PlayerZones, source.ObjectId)))
        {
            var controller = EffectiveFieldControllerId(state.PlayerZones, source.ObjectId, source);
            foreach (var instance in AttachedRuleTextRules.Instances(state, source).Where(i => MovedEnemyStunDefinition(i.CardNo)))
            foreach (var unit in units.Where(unit => unit.MovingPlayerId == controller && unit.ControllerId != controller))
                yield return new(source.ObjectId, controller, new(instance.CardNo, MovedEnemyStunKind,
                    source.ObjectGeneration, unit) {
                    RuleTextInstanceId = instance.Origin is null ? null : instance.Id, Origin = instance.Origin,
                    SourceFaceRevision = instance.Origin?.FaceRevision ?? source.CopyFaceTransitions?.Count ?? 0,
                    CapturedTick = RuleChoices.Value?.CompletionTick ?? state.Tick + 1 });
        }
    }

    private static bool HasMovedEnemyStunCost(StackItemState item)
        => item.MovementObserver?.Kind == MovedEnemyStunKind;

    private static bool ValidMovedEnemyStunStack(StackItemState item)
        => item.MovementObserver is { Kind: MovedEnemyStunKind } context
            && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null && item.CardNo == context.CardNo
            && !item.SourceConfirmed && !item.EffectPlayCompleted && item.EffectRepeatCount == 1
            && item.DamageAmount == 0 && item.OptionalCosts.Count == 0 && item.Destination.Length == 0
            && item.UnitEvent is null
            && ValidMovementObserver(context, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo);

    private static StackResolutionResult ResolveMovedEnemyStun(MatchState state, StackItemState item)
    {
        if (!ValidMovedEnemyStunStack(item) || item.TriggerCost is null || !ValidTriggerCostReceipt(state, item))
            throw new InvalidOperationException("Invalid captured movement stun cost.");
        var fact = item.MovementObserver!.Unit;
        var cards = state.CardObjects.ToDictionary();
        var visibleSource = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator);
        var events = new List<GameEvent>();
        // The moved object is an effect reference, never a target (VEN FAQ,
        // 423.1.a.1). A new incarnation cannot inherit the reference.
        if (cards.TryGetValue(fact.ObjectId, out var unit) && unit.ObjectGeneration == fact.Generation
            && FieldObjectTypeRules.IsVisibleUnit(unit) && IsObjectOnField(state.PlayerZones, fact.ObjectId)
            && CanExecuteStun(unit))
        {
            cards[unit.ObjectId] = unit with { UntilEndOfTurnEffects = unit.UntilEndOfTurnEffects
                .Append("STUNNED").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
            var applied = new GameEvent("STATUS_EFFECT_APPLIED", "眩晕移动的单位", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = visibleSource, ["targetObjectId"] = unit.ObjectId,
                ["effectId"] = "STUNNED", ["statusEffectId"] = "STUNNED", ["duration"] = "UNTIL_END_OF_TURN" });
            events.Add(CaptureStunObservers(applied, state.PlayerZones, cards, item, unit.ObjectId, state.ObjectLocations));
        }
        events.Add(new("TRIGGER_RESOLVED", "移动后眩晕技能结算完成", new Dictionary<string, object?> {
            ["sourceObjectId"] = visibleSource }));
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}

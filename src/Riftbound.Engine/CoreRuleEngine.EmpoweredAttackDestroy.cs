using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string EmpoweredAttackDestroyKind = "EMPOWERED_ATTACK_DESTROY_WEAKER_HERE";

    private static TriggerSpec? EmpoweredAttackDestroySpec(string? cardNo)
        => SourceUnitPlayedTriggerSpecRules.TriggersForCard(cardNo).FirstOrDefault(spec =>
            spec.Kind == TriggerKinds.UnitEmpoweredAttackDestroyWeakerEnemyHere
            && spec.Timing == TriggerTimings.UnitAttack && spec.TargetScope == TriggerTargetScopes.EnemyUnitHere
            && spec.RequiresSourceEmpowered == true);

    private static TriggerQueueItemState[] CaptureEmpoweredAttackDestroyTriggers(MatchState state,
        IEnumerable<string> attackers, string battlefield, string timing = TimingStates.NeutralClosed,
        string? returnFocusPlayer = null)
    {
        var result = new List<TriggerQueueItemState>();
        LastKnownMightFrame? frame = null;
        foreach (var id in attackers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var source) || !IsFaceUpNonStandbyUnit(source)
                || !source.IsEmpowered || source.ControllerId is not { Length: > 0 } controller) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
            {
                if (EmpoweredAttackDestroySpec(instance.CardNo) is null) continue;
                var context = new FieldTriggerContext(instance.CardNo, EmpoweredAttackDestroyKind,
                    source.ObjectGeneration, battlefield, returnFocusPlayer) {
                    SourceLocation = new(controller, "BATTLEFIELD", battlefield), Origin = instance.Origin,
                    SourceTokenFactoryCardNo = IsTokenObject(source) ? source.TokenFactoryCardNo ?? source.CardNo : null,
                    AttackConditionFrame = frame ??= CapturePublicMightFrame(state, state.PlayerZones, state.CardObjects) };
                result.Add(new TriggerQueueItemState($"empowered-attack-destroy-{state.Tick + 1}-{result.Count}",
                    controller, id, FieldTriggerEffect, "BATTLE_DECLARED", timing) {
                    FieldContext = context, SourceCardNo = instance.CardNo });
            }
        }
        return result.ToArray();
    }

    private static bool ValidEmpoweredAttackDestroyShape(FieldTriggerContext context, string effect, string? cardNo)
        => effect == FieldTriggerEffect && context.Kind == EmpoweredAttackDestroyKind && context.SourceGeneration >= 0
            && (cardNo is null || cardNo == context.CardNo) && EmpoweredAttackDestroySpec(context.CardNo) is not null
            && !string.IsNullOrWhiteSpace(context.BattlefieldId) && context.SourceLocation is not null
            && context.AttackConditionFrame is not null
            && context.RecycledBoon is null && context.EmpoweredTransition is null && context.AffectedUnit is null
            && !context.AffectedUnitCeasedToExist
            && (context.SourceTokenFactoryCardNo is null ? !context.SourceCeasedToExist
                : P6TokenFactoryCatalog.TryGetByCardNo(context.SourceTokenFactoryCardNo, out var token)
                    && token.Tags.Contains(CardObjectTags.UnitCard));

    private static bool ValidEmpoweredAttackCondition(MatchState state, FieldTriggerContext context,
        string sourceId, string controller)
    {
        try
        {
            if (context.AttackConditionFrame is not { } frame || !ValidMightFrameExperience(frame)
                || frame.CardObjects is null || frame.ObjectLocations is null || frame.BattleDeclarations is null
                || !frame.PlayerZones.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(state.Seats.Keys)
                || frame.PlayerZones.Values.Any(z => z is null || z.Hand.Count != 0 || z.MainDeck.Count != 0 || z.RuneDeck.Count != 0)
                || frame.BattleDeclarations.Any(e => e is null || !e.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal))
                || !frame.CardObjects.TryGetValue(sourceId, out var captured) || captured.ObjectGeneration != context.SourceGeneration
                || captured.ControllerId != controller || !captured.IsEmpowered || !IsFaceUpNonStandbyUnit(captured)
                || !frame.ObjectLocations.TryGetValue(sourceId, out var location) || location.Zone != "BATTLEFIELD"
                || location.BattlefieldObjectId != context.BattlefieldId
                || !frame.PlayerZones.TryGetValue(location.PlayerId, out var sourceZones)
                || !sourceZones.Battlefields.Contains(sourceId, StringComparer.Ordinal)
                || !AttachedRuleTextRules.Instances(frame.PlayerZones, frame.CardObjects, captured)
                    .Any(instance => instance.CardNo == context.CardNo && instance.Origin == context.Origin)) return false;
            var publicIds = frame.PlayerZones.Values.SelectMany(z => z.Base.Concat(z.Battlefields).Concat(z.Graveyard)
                .Concat(z.Banished).Concat(z.LegendZone).Concat(z.ChampionZone)).ToHashSet(StringComparer.Ordinal);
            return !frame.CardObjects.Any(p => p.Value is null || p.Key != p.Value.ObjectId || p.Value.IsFaceDown
                    || !publicIds.Contains(p.Key) || (state.CardObjects.TryGetValue(p.Key, out var current)
                        ? current.ObjectGeneration < p.Value.ObjectGeneration : !HasKnownHistoricalTokenFactory(p.Value)))
                && !frame.ObjectLocations.Keys.Any(id => !frame.CardObjects.ContainsKey(id));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException
            or NullReferenceException or OverflowException) { return false; }
    }

    private static bool IsEmpoweredAttackDestroyTarget(MatchState state, StackItemState item, CardObjectState target)
        => state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            && source.ObjectGeneration == item.FieldContext!.SourceGeneration && IsFaceUpNonStandbyUnit(source)
            && IsObjectOnField(state.PlayerZones, source.ObjectId) && IsCombatMightTargetHere(state, item, target.ObjectId)
            && EffectiveFieldControllerId(state.PlayerZones, target.ObjectId, target) != item.ControllerId
            && ResolveCurrentFieldUnitPower(state, target) < ResolveCurrentFieldUnitPower(state, source);

    private static StackResolutionResult ResolveEmpoweredAttackDestroy(MatchState state, StackItemState item)
    {
        if (item.FieldContext is not { Kind: EmpoweredAttackDestroyKind } context
            || !ValidFieldContextState(state, context, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo))
            throw new InvalidOperationException("Invalid captured empowered attack skill.");
        var result = NoopStackResolutionResult(state);
        var events = new List<GameEvent> { new("TRIGGER_RESOLVED", "进攻技能结算", new Dictionary<string, object?> {
            ["controllerId"] = item.ControllerId, ["effectKind"] = item.EffectKind,
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__") }) };
        if (item.TargetObjectIds.SingleOrDefault() is not { } target
            || item.TargetGenerations?.GetValueOrDefault(target, -1) != state.CardObjects.GetValueOrDefault(target)?.ObjectGeneration
            || !FieldTriggerTargets(state, item).Contains(target)) return result with { Events = events };
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var removal = ResolveFieldDestructions(state, zones, cards, item, new HashSet<string>(),
            state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(), state.RunePools, objectLocations: state.ObjectLocations,
            explicitDestroyObjectIds: new HashSet<string> { target }, explicitDestroyReason: "SKILL_EFFECT");
        return result with { PlayerZones = zones, CardObjects = cards, RunePools = removal.RunePools,
            Events = events.Concat(removal.Events).ToArray(), TriggerQueue = removal.TriggerQueue,
            DestroyedUnitOwnerIds = removal.DestroyedUnitOwnerIds,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }
}

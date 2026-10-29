using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AttackMightDamageOrigin(string ControllerId, long CapturedTick, long FaceRevision,
    string RuleTextInstanceId, bool CostPaid = false, CeasedEmpoweredSource? CeasedSource = null);

public sealed partial class CoreRuleEngine
{
    internal const string AttackMightDamageKind = "ATTACK_PAID_SOURCE_MIGHT_DAMAGE";

    private static TriggerSpec? AttackMightDamageSpec(string? cardNo)
        => SourceUnitPlayedTriggerSpecRules.TriggersForCard(cardNo).FirstOrDefault(t =>
            t.Kind == TriggerKinds.UnitAttackPayExperienceSourceMightDamage && t.Timing == TriggerTimings.UnitAttack
            && t.TargetScope == TriggerTargetScopes.EnemyUnitHere && t.Optional == true && t.ExperienceCost is > 0);

    private static TriggerQueueItemState[] CaptureAttackMightDamage(MatchState state, IEnumerable<string> attackers,
        string battlefield, string timing = TimingStates.NeutralClosed, string? returnFocusPlayer = null)
    {
        var queue = new List<TriggerQueueItemState>();
        foreach (var id in attackers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var source) || !IsFaceUpNonStandbyUnit(source)) continue;
            var controller = EffectiveFieldControllerId(state.PlayerZones, id, source);
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
            {
                if (AttackMightDamageSpec(instance.CardNo) is null) continue;
                var context = new FieldTriggerContext(instance.CardNo, AttackMightDamageKind, source.ObjectGeneration,
                    battlefield, returnFocusPlayer) {
                    Origin = instance.Origin, SourceTokenFactoryCardNo = source.TokenFactoryCardNo,
                    AttackMightDamage = new(controller, RuleChoices.Value?.CompletionTick ?? state.Tick,
                        instance.Origin?.FaceRevision ?? source.CopyFaceTransitions?.Count ?? 0, instance.Id)
                };
                queue.Add(new($"attack-might-{state.Tick + 1}-{MatchStateHasher.HashValue(new { Source = id, Instance = instance.Id, Battlefield = battlefield })}", controller, id,
                    FieldTriggerEffect, "BATTLE_DECLARED", timing) { FieldContext = context, SourceCardNo = instance.CardNo });
            }
        }
        return queue.ToArray();
    }

    private static bool ValidAttackMightDamageShape(FieldTriggerContext c, string effect, string? cardNo)
        => c.Kind == AttackMightDamageKind && effect == FieldTriggerEffect && (cardNo is null || cardNo == c.CardNo)
            && AttackMightDamageSpec(c.CardNo) is not null && c.SourceGeneration >= 0
            && c.AttackMightDamage is { CapturedTick: >= 0, FaceRevision: >= 0 } birth
            && !string.IsNullOrWhiteSpace(birth.ControllerId) && !string.IsNullOrWhiteSpace(birth.RuleTextInstanceId)
            && !string.IsNullOrWhiteSpace(c.BattlefieldId) && c.SourceLocation is null
            && c.RecycledBoon is null && c.EmpoweredTransition is null && c.AffectedUnit is null
            && !c.AffectedUnitCeasedToExist && c.AttackConditionFrame is null
            && (c.SourceTokenFactoryCardNo is null ? !c.SourceCeasedToExist && birth.CeasedSource is null
                : P6TokenFactoryCatalog.TryGetByCardNo(c.SourceTokenFactoryCardNo, out var factory)
                    && factory.Tags.Contains(CardObjectTags.UnitCard) && c.SourceCeasedToExist == (birth.CeasedSource is not null));

    private static bool ValidAttackMightDamageSource(MatchState state, FieldTriggerContext c, string sourceId, string controller)
    {
        var birth = c.AttackMightDamage!;
        if (birth.ControllerId != controller || !state.Seats.ContainsKey(controller) || birth.CapturedTick > state.Tick
            || BattlefieldLocalRules.Battlefield(state, c.BattlefieldId) is null
            || c.ReturnFocusPlayerId is not null && !state.Seats.ContainsKey(c.ReturnFocusPlayerId)) return false;
        if (c.SourceCeasedToExist && (state.CardObjects.ContainsKey(sourceId) || birth.CeasedSource is not { } ceased
            || ceased.Tick < birth.CapturedTick || ceased.Tick > state.Tick || ceased.CardNo != c.CardNo)) return false;
        if (!c.SourceCeasedToExist && !state.CardObjects.ContainsKey(sourceId)) return false;
        return c.Origin is { } origin
            ? origin.Equipment is { } equipment
                && birth.RuleTextInstanceId == $"ATTACHED:{equipment.ObjectId}:{equipment.Generation}:{origin.Revision}"
                && AttachedRuleTextRules.ValidCaptured(state, origin, sourceId, c.SourceGeneration, c.CardNo)
            : birth.RuleTextInstanceId == $"PRINTED:{sourceId}:{c.SourceGeneration}"
                && PredictBindingMatches(state, new(sourceId, c.SourceGeneration), c.CardNo,
                    birth.FaceRevision, birth.CapturedTick, c.SourceTokenFactoryCardNo);
    }

    internal static bool ValidAttackMightDamageTrigger(TriggerQueueItemState trigger)
        => trigger.FieldContext is not { Kind: AttackMightDamageKind } c || c.AttackMightDamage is { CostPaid: false }
            && trigger.SourceCardNo == c.CardNo
            && trigger.TriggeredByEventKind is "BATTLE_DECLARED" or "BATTLE_ROLES_ESTABLISHED" or "BATTLE_ROLE_ACQUIRED";

    internal static bool ValidAttackMightDamageStack(MatchState state, StackItemState item)
    {
        if (item.FieldContext is not { Kind: AttackMightDamageKind } c) return true;
        if (item.OptionalCosts.Count != 0 || item.DamageAmount != 0 || item.EffectRepeatCount != 1 || item.Destination.Length != 0
            || item.SourceConfirmed || item.TargetedPowerAbility is not null || item.DamageDistribution is not null) return false;
        return c.AttackMightDamage?.CostPaid == true
            ? item.TargetGenerations is { Count: 1 } && item.TargetObjectIds.Count == 1
            : item.TargetGenerations is null
                ? item.TargetObjectIds.Count == 0 && state.PriorityPlayerId is null
                : state.PendingPayment is { } payment && payment.ResolvingStackItemId == item.StackItemId
                    && payment.ExperienceCost == AttackMightDamageSpec(c.CardNo)!.ExperienceCost;
    }

    private static StackResolutionResult ResolveAttackMightDamage(MatchState state, StackItemState item)
    {
        var context = item.FieldContext!;
        if (context.AttackMightDamage is not { CostPaid: true }
            || !ValidFieldContextState(state, context, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo))
            throw new InvalidOperationException("Invalid paid attack damage context.");
        var visibleSource = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__");
        var result = NoopStackResolutionResult(state) with { Events = [new("TRIGGER_RESOLVED", "进攻战力伤害技能结算",
            new Dictionary<string, object?> { ["sourceObjectId"] = visibleSource, ["effectKind"] = AttackMightDamageKind })] };
        // CN 359.3.e.12: a departed source has no field Might or location.
        if (item.TargetObjectIds.SingleOrDefault() is not { } target
            || item.TargetGenerations?.GetValueOrDefault(target, -1) != state.CardObjects.GetValueOrDefault(target)?.ObjectGeneration
            || !FieldTriggerTargets(state, item).Contains(target, StringComparer.Ordinal)
            || !state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            || source.ObjectGeneration != context.SourceGeneration || !IsObjectOnField(state.PlayerZones, source.ObjectId)) return result;
        var amount = ApplySpellSkillDamageBonuses(state, item.ControllerId, target, ResolveCurrentFieldUnitPower(state, source));
        if (amount <= 0) return result;
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var locations = state.ObjectLocations.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var destroyed = new HashSet<string>(StringComparer.Ordinal);
        var damage = ApplyDamageToCardObject(cards, item.ControllerId, target, amount, destroyed,
            state.UntilEndOfTurnEffects.Contains(PreventSpellAndSkillDamageThisTurnEffectId), PreventSpellAndSkillDamageThisTurnEffectId);
        var events = result.Events.ToList();
        events.Add(new("DAMAGE_APPLIED", "进攻技能造成等同于来源战力的伤害", BuildDamagePayload(visibleSource, target, damage)));
        var cleanup = RunStateBasedCleanupLoop(state, zones, cards, item, state.RunePools,
            damageTriggeredDestroyTargetObjectIds: destroyed, objectLocations: locations,
            destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        return result with { PlayerZones = zones, CardObjects = cards, Events = events, RunePools = cleanup.RunePools,
            TriggerQueue = cleanup.TriggerQueue, DestroyedUnitOwnerIds = cleanup.DestroyedUnitOwnerIds,
            ObjectLocations = ReconcileObjectLocations(locations, zones) };
    }
}

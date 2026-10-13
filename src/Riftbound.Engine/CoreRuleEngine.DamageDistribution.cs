using Riftbound.Contracts;
using System.Text.Json.Serialization;

namespace Riftbound.Engine;

public sealed record DistributedDamageBonus(string CardNo, string Kind, int Amount, ObjectBinding Source,
    RuleTextInstanceBinding? Origin = null);
public sealed record DamageDistributionContext(string CardNo, ObjectBinding Source, ObjectLocationState SourceLocation,
    int InitialDamage, IReadOnlyList<DistributedDamageBonus> InitialBonuses,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RuleTextInstanceBinding? Origin = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ResolutionDamage = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<CombatDamageAssignmentDto>? Assignments = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ReturnFocusPlayerId = null);

public sealed partial class CoreRuleEngine
{
    internal const string DistributedDamageEffect = "CAPTURED_DISTRIBUTED_DAMAGE";
    internal const string EffectDamageWindow = "EFFECT_DAMAGE_DISTRIBUTION";
    private static TriggerSpec? AttackDamageSpec(string? cardNo) => SourceUnitPlayedTriggerSpecRules.TriggersForCard(cardNo)
        .FirstOrDefault(t => t.Kind == TriggerKinds.UnitAttackDistributedDamage && t.Timing == TriggerTimings.UnitAttack
            && t.TargetScope == TriggerTargetScopes.EnemyUnitHere && t.DamageAmount > 0);

    private static IReadOnlyList<DistributedDamageBonus> DistributionBonuses(MatchState state, string controller,
        ObjectLocationState? location)
    {
        var bonuses = new List<DistributedDamageBonus>();
        foreach (var source in state.CardObjects.Values.OrderBy(c => c.ObjectId, StringComparer.Ordinal)
            .Where(c => IsFaceUpNonStandbyUnit(c) && c.ControllerId == controller && IsObjectOnField(state.PlayerZones, c.ObjectId)))
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
                if (CardStaticAbilitySpecRules.TryGetStaticAbility(instance.CardNo,
                    a => a.Kind == StaticAbilityKinds.ControllerSpellSkillDamageBonus && a.Amount > 0, out var bonus))
                    bonuses.Add(new(instance.CardNo, bonus.Kind, bonus.Amount, new(source.ObjectId, source.ObjectGeneration), instance.Origin));
        if (location?.Zone == "BATTLEFIELD" && BattlefieldLocalRules.Battlefield(state, location.BattlefieldObjectId) is { } field
            && BattlefieldStaticAbilitySpecRules.TryGetAbility(field.CardNo,
                BattlefieldStaticAbilitySpecRules.IsBattlefieldTargetSpellSkillDamageBonusAbility, out var fieldBonus))
            bonuses.Add(new(field.CardNo!, fieldBonus.Kind, fieldBonus.Amount, new(field.ObjectId, field.ObjectGeneration)));
        return bonuses;
    }

    private static TriggerQueueItemState[] CaptureAttackDamageTriggers(MatchState state, IReadOnlyList<string> attackers, string field,
        string timing = TimingStates.NeutralClosed, string? returnFocusPlayer = null)
    {
        var queue = new List<TriggerQueueItemState>();
        foreach (var id in attackers)
        {
            var source = state.CardObjects[id];
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
                if (AttackDamageSpec(instance.CardNo) is { } spec)
                {
                    var location = new ObjectLocationState(source.ControllerId!, "BATTLEFIELD", field);
                    var bonuses = DistributionBonuses(state, source.ControllerId!, location);
                    queue.Add(new TriggerQueueItemState($"attack-damage-{state.Tick + 1}-{id}-{instance.Id}",
                        source.ControllerId!, id, DistributedDamageEffect, "BATTLE_DECLARED", timing) {
                        DamageDistribution = new(instance.CardNo, new(id, source.ObjectGeneration), location,
                            checked(spec.DamageAmount!.Value + bonuses.Sum(b => b.Amount)), bonuses, instance.Origin,
                            ReturnFocusPlayerId: returnFocusPlayer) });
                }
        }
        return queue.ToArray();
    }

    // "Here" follows the source while it remains the same object on the field.
    // Retain its last field location only as recovery provenance. Execution-time
    // "here" needs a current location and cannot use this after departure.
    private static ResolutionResult RecordDistributionSourceLocations(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted || !result.State.StackItems.Any(i => i.DamageDistribution is not null)
            && !result.State.TriggerQueue.Any(i => i.DamageDistribution is not null)) return result;
        DamageDistributionContext? Update(DamageDistributionContext? context)
        {
            if (context is null) return null;
            ObjectLocationState? FieldLocation(MatchState s) => s.CardObjects.TryGetValue(context.Source.ObjectId, out var source)
                && source.ObjectGeneration == context.Source.Generation && s.ObjectLocations.TryGetValue(source.ObjectId, out var location)
                && location.Zone is "BASE" or "BATTLEFIELD" ? location : null;
            var location = FieldLocation(result.State) ?? FieldLocation(before);
            return location is null ? context : context with { SourceLocation = location };
        }
        var state = result.State with {
            StackItems = result.State.StackItems.Select(i => i.DamageDistribution is null ? i : i with { DamageDistribution = Update(i.DamageDistribution) }).ToArray(),
            TriggerQueue = result.State.TriggerQueue.Select(i => i.DamageDistribution is null ? i : i with { DamageDistribution = Update(i.DamageDistribution) }).ToArray() };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static ObjectLocationState? DistributionHere(MatchState state, DamageDistributionContext context)
        => state.CardObjects.TryGetValue(context.Source.ObjectId, out var source) && source.ObjectGeneration == context.Source.Generation
            && state.ObjectLocations.TryGetValue(source.ObjectId, out var location) && location.Zone is "BASE" or "BATTLEFIELD"
            ? location : null;

    private static string[] DistributionTargets(MatchState state, StackItemState item)
    {
        var here = DistributionHere(state, item.DamageDistribution!);
        if (here is null) return [];
        return state.CardObjects.Values.Where(c => IsFaceUpNonStandbyUnit(c) && c.ControllerId != item.ControllerId
            && IsObjectOnField(state.PlayerZones, c.ObjectId) && state.ObjectLocations.TryGetValue(c.ObjectId, out var location)
            && location.Zone == here.Zone && (here.Zone == "BASE" ? location.PlayerId == here.PlayerId : location.BattlefieldObjectId == here.BattlefieldObjectId)
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, c.ObjectId))
            .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();
    }
    private static string[] RemainingDistributionTargets(MatchState state, StackItemState item)
    {
        var legal = DistributionTargets(state, item).ToHashSet(StringComparer.Ordinal);
        return item.TargetObjectIds.Where(id => legal.Contains(id)
            && item.TargetGenerations?.GetValueOrDefault(id, -1) == state.CardObjects[id].ObjectGeneration).ToArray();
    }
    private static int DistributionDamage(MatchState state, StackItemState item) => checked(AttackDamageSpec(item.CardNo)!.DamageAmount!.Value
        + DistributionBonuses(state, item.ControllerId, DistributionHere(state, item.DamageDistribution!)).Sum(b => b.Amount));

    private static PendingCardChoiceState DistributionTargetChoice(MatchState state, StackItemState item)
    {
        var legal = DistributionTargets(state, item);
        return new("DISTRIBUTED-TARGETS:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId, 1,
            Math.Min(legal.Length, item.DamageDistribution!.InitialDamage), legal, legal,
            "选择此处的敌方单位；确认后双方可以响应，结算时再分配伤害。", item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };
    }
    private static ResolutionResult PrepareDistributionTargets(ResolutionResult result, StackItemState item)
    {
        if (DistributionTargets(result.State, item).Length == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var state = result.State with { PendingCardChoice = DistributionTargetChoice(result.State, item), PriorityPlayerId = null,
            ActivePlayerId = item.ControllerId };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }
    private static bool SameDistributionChoice(PendingCardChoiceState choice, PendingCardChoiceState expected)
        => choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow
            && choice.PlayerId == expected.PlayerId && choice.SourceObjectId == expected.SourceObjectId && choice.EffectKind == expected.EffectKind
            && choice.ResolvingStackItemId == expected.ResolvingStackItemId && choice.RequiredCount == expected.RequiredCount && choice.MaxCount == expected.MaxCount
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds) && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    private static bool ValidDistributionTargetChoice(MatchState state, PendingCardChoiceState choice)
        => state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId) is { DamageDistribution: not null, TargetGenerations: null } item
            && ValidDistributionStack(state, item) && DistributionTargets(state, item).Length > 0
            && SameDistributionChoice(choice, DistributionTargetChoice(state, item));

    internal static bool ValidDistributionContext(MatchState state, DamageDistributionContext context, string sourceId, string controller, string effect, string? cardNo = null)
    {
        bool Binding(ObjectBinding? b) => b is { Generation: >= 0 } && !string.IsNullOrWhiteSpace(b.ObjectId)
            && state.CardObjects.TryGetValue(b.ObjectId, out var card) && b.Generation <= card.ObjectGeneration;
        bool Location(ObjectLocationState? l) => l is not null && state.Seats.ContainsKey(l.PlayerId)
            && (l.Zone == "BASE" && l.BattlefieldObjectId is null || l.Zone == "BATTLEFIELD"
                && BattlefieldLocalRules.Battlefield(state, l.BattlefieldObjectId) is not null);
        var spec = AttackDamageSpec(context.CardNo);
        if (effect != DistributedDamageEffect || cardNo is not null && cardNo != context.CardNo || spec is null
            || !state.Seats.ContainsKey(controller) || !Binding(context.Source) || context.Source.ObjectId != sourceId || !Location(context.SourceLocation)
            || context.InitialBonuses is null || !AttachedRuleTextRules.ValidCaptured(state, context.Origin, sourceId, context.Source.Generation, context.CardNo)
            || context.Assignments is not null || context.ResolutionDamage is <= 0) return false;
        if (context.ReturnFocusPlayerId is not null && !state.Seats.ContainsKey(context.ReturnFocusPlayerId)) return false;
        foreach (var bonus in context.InitialBonuses)
        {
            if (bonus is null || !Binding(bonus.Source) || bonus.Amount <= 0
                || state.CardObjects[bonus.Source.ObjectId] is { } bonusSource && bonusSource.ObjectGeneration == bonus.Source.Generation
                    && bonusSource.CardNo != bonus.CardNo
                || !AttachedRuleTextRules.ValidCaptured(state, bonus.Origin, bonus.Source.ObjectId, bonus.Source.Generation, bonus.CardNo)) return false;
            var valid = bonus.Kind == StaticAbilityKinds.ControllerSpellSkillDamageBonus
                ? CardStaticAbilitySpecRules.TryGetStaticAbility(bonus.CardNo, a => a.Kind == bonus.Kind && a.Amount == bonus.Amount, out _)
                : bonus.Kind == StaticAbilityKinds.BattlefieldTargetSpellSkillDamageBonus && bonus.Origin is null
                    && BattlefieldStaticAbilitySpecRules.TryGetAbility(bonus.CardNo, a => a.Kind == bonus.Kind && a.Amount == bonus.Amount, out _);
            if (!valid) return false;
        }
        var keys = context.InitialBonuses.Select(b => b.Source.ObjectId + ":" + b.Source.Generation + ":" + b.Kind + ":" + (b.Origin?.Equipment.ObjectId ?? "PRINTED")).ToArray();
        if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length || (long)spec.DamageAmount!.Value + context.InitialBonuses.Sum(b => (long)b.Amount) != context.InitialDamage) return false;
        var source = state.CardObjects[sourceId];
        return source.ObjectGeneration != context.Source.Generation
            || (source.CardNo == context.CardNo && (!IsObjectOnField(state.PlayerZones, sourceId)
                || state.ObjectLocations[sourceId] == context.SourceLocation));
    }
    internal static bool ValidDistributionStack(MatchState state, StackItemState item)
    {
        if (item.DamageDistribution is not { } context) return item.EffectKind != DistributedDamageEffect;
        if (!ValidDistributionContext(state, context, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo)
            || !item.SourceConfirmed || item.EffectRepeatCount != 1 || item.DamageAmount != context.InitialDamage) return false;
        if (item.TargetGenerations is null) return item.TargetObjectIds.Count == 0 && context.ResolutionDamage is null;
        return item.TargetObjectIds.Count > 0 && item.TargetObjectIds.Count <= context.InitialDamage
            && item.TargetObjectIds.Distinct(StringComparer.Ordinal).Count() == item.TargetObjectIds.Count
            && item.TargetGenerations.Count == item.TargetObjectIds.Count
            && item.TargetObjectIds.All(id => item.TargetGenerations.TryGetValue(id, out var generation) && generation >= 0
                && state.CardObjects.TryGetValue(id, out var card) && generation <= card.ObjectGeneration
                && (generation != card.ObjectGeneration || card.Tags.Contains(CardObjectTags.UnitCard)));
    }

    private static PendingCardChoiceState DistributionAllocationChoice(MatchState state, StackItemState item)
    {
        var legal = RemainingDistributionTargets(state, item); var count = Math.Min(legal.Length, item.DamageDistribution!.ResolutionDamage!.Value);
        return new("DISTRIBUTED-AMOUNTS:" + item.StackItemId, EffectDamageWindow, item.ControllerId, count, count, legal,
            item.TargetObjectIds, "只给仍有效的已确认目标分配正整数伤害；不能新增目标。", item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };
    }
    internal static bool ValidDistributionAllocation(MatchState state, PendingCardChoiceState choice)
        => state.StackItems.LastOrDefault() is { DamageDistribution: { ResolutionDamage: not null } } item
            && ValidDistributionStack(state, item) && state.PriorityPlayerId is null
            && item.DamageDistribution.ResolutionDamage == DistributionDamage(state, item) && RemainingDistributionTargets(state, item).Length > 0
            && SameDistributionChoice(choice, DistributionAllocationChoice(state, item));

    private static ResolutionResult ResolveEffectDamageAssignment(MatchState state, PlayerIntent intent, GameCommand command)
    {
        if (command is not AssignEffectDamageCommand assignment || state.PendingCardChoice is not { ChoiceWindow: EffectDamageWindow } choice
            || !ValidDistributionAllocation(state, choice) || intent.PlayerId != choice.PlayerId || assignment.StackItemId != choice.ResolvingStackItemId
            || assignment.Assignments is not { Count: > 0 } values)
            return RejectWithCorePrompts(state, "当前没有匹配的技能伤害分配窗口。", ErrorCodes.InvalidTarget);
        var item = state.StackItems[^1]; var context = item.DamageDistribution!;
        if (values.Count != choice.RequiredCount || values.Select(v => v.TargetObjectId).Distinct(StringComparer.Ordinal).Count() != values.Count
            || values.Any(v => v.SourceObjectId != item.SourceObjectId || v.Damage <= 0 || !choice.LegalObjectIds.Contains(v.TargetObjectId))
            || values.Sum(v => (long)v.Damage) != context.ResolutionDamage)
            return RejectWithCorePrompts(state, "每个留存目标至少分配 1 点，且总量必须等于技能伤害；只能使用已确认目标。", ErrorCodes.InvalidTarget);
        var next = state with { PendingCardChoice = null, StackItems = state.StackItems.Take(state.StackItems.Count - 1)
            .Append(item with { DamageDistribution = context with { Assignments = values.ToArray() } }).ToArray() };
        return ResolvePassPriority(next, new("distributed-damage-continuation", item.ControllerId, CommandTypes.PassPriority), forceResolve: true);
    }

    private static StackResolutionResult ResolveDistributedDamage(MatchState state, StackItemState item)
    {
        var result = NoopStackResolutionResult(state); var context = item.DamageDistribution!;
        var legal = RemainingDistributionTargets(state, item);
        if (legal.Length == 0) return result;
        if (context.Assignments is null)
        {
            var suspended = item with { DamageDistribution = context with { ResolutionDamage = DistributionDamage(state, item) } };
            return result with { PendingCardChoice = DistributionAllocationChoice(state, suspended),
                StackItems = state.StackItems.Take(state.StackItems.Count - 1).Append(suspended).ToArray() };
        }
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value); var zones = NormalizeZonesForSeats(state);
        var events = new List<GameEvent>(); var destroyed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assignment in context.Assignments)
        {
            if (!legal.Contains(assignment.TargetObjectId)) continue;
            var damage = ApplyDamageToCardObject(cards, item.ControllerId, assignment.TargetObjectId, assignment.Damage, destroyed,
                state.UntilEndOfTurnEffects.Contains(PreventSpellAndSkillDamageThisTurnEffectId), PreventSpellAndSkillDamageThisTurnEffectId);
            events.Add(new("DAMAGE_APPLIED", "分摊进攻技能伤害", BuildDamagePayload(item.SourceObjectId, assignment.TargetObjectId, damage)));
        }
        var locations = state.ObjectLocations.ToDictionary(e => e.Key, e => e.Value);
        var cleanup = RunStateBasedCleanupLoop(state, zones, cards, item, state.RunePools, damageTriggeredDestroyTargetObjectIds: destroyed,
            objectLocations: locations, destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        return result with { PlayerZones = zones, CardObjects = cards, Events = events, RunePools = cleanup.RunePools,
            TriggerQueue = cleanup.TriggerQueue, DestroyedUnitOwnerIds = cleanup.DestroyedUnitOwnerIds, ObjectLocations = ReconcileObjectLocations(locations, zones) };
    }

    internal static IReadOnlyDictionary<string, object?> EffectDamageMetadata(MatchState state, string player)
    {
        if (state.PendingCardChoice is not { ChoiceWindow: EffectDamageWindow } choice || choice.PlayerId != player
            || state.StackItems.LastOrDefault() is not { DamageDistribution: { ResolutionDamage: not null } } item)
            return new Dictionary<string, object?> { ["assignmentState"] = "WAIT" };
        var amount = item.DamageDistribution.ResolutionDamage!.Value;
        return new Dictionary<string, object?> {
            ["stackItemId"] = item.StackItemId, ["assigningPlayerId"] = player,
            ["assignableDamagePool"] = new Dictionary<string, int> { [item.SourceObjectId] = amount },
            ["legalTargets"] = new Dictionary<string, IReadOnlyList<string>> { [item.SourceObjectId] = choice.LegalObjectIds },
            ["minimumAssignedTargets"] = choice.RequiredCount,
            ["requiredAssignments"] = new[] { new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["damage"] = amount, ["legalTargetObjectIds"] = choice.LegalObjectIds } },
            ["assignmentChoices"] = choice.LegalObjectIds.Select(id => new ActionPromptChoiceDto(
                item.SourceObjectId + "->" + id, DeckChoiceLabel(state, id), "分配技能伤害")).ToArray(),
            ["lethalDamageThreshold"] = choice.LegalObjectIds.ToDictionary(id => id,
                id => Math.Max(0, ResolutionResult.BattleEffectivePowerFor(state, id) - state.CardObjects[id].Damage)),
            ["battleParticipants"] = choice.LegalObjectIds.Append(item.SourceObjectId).Distinct().Select(id => new Dictionary<string, object?> {
                ["objectId"] = id, ["displayName"] = DeckChoiceLabel(state, id),
                ["role"] = id == item.SourceObjectId ? "EFFECT_SOURCE" : "EFFECT_TARGET",
                ["power"] = Math.Max(0, ResolutionResult.BattleEffectivePowerFor(state, id)), ["damage"] = state.CardObjects[id].Damage }).ToArray(),
            ["assignmentRule"] = "EFFECT_POSITIVE_INTEGER", ["assignmentGuidance"] = choice.Reason };
    }
}

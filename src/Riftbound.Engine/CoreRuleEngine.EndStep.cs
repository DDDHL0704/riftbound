using System.Text.Json.Serialization;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EndStepContinuation(string PlayerId, int TurnNumber);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EndAbilityContext(string CardNo, string Kind, CardObjectState Source, ObjectLocationState Location,
    int TurnNumber, int RuneCount = 0, RuleTextInstanceBinding? Origin = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? CreationTick = null);

public sealed partial class CoreRuleEngine
{
    internal const string EndAbilityEffect = "CAPTURED_END_STEP_ABILITY";
    internal const string EndRuneChoiceWindow = "END_STEP_RUNE_TARGETS";
    internal const string CreateEndAbilityEffect = "CREATE_DELAYED_END_ABILITY";
    internal const string DelayedEndReadyPrefix = "DELAYED_END_READY:";
    private static TriggerSpec? EndSpec(string? no) => SourceUnitPlayedTriggerSpecRules.TriggersForCard(no)
        .FirstOrDefault(t => t.Timing == TriggerTimings.TurnEnd && t.Kind is TriggerKinds.TurnEndRevealUntilUnit or TriggerKinds.TurnEndReadyRunes or TriggerKinds.TurnEndRecycleRunesCall)
        ?? (CardBehaviorRegistry.TryGetByCardNo(no ?? "", out var behavior) && behavior.EndStepSelfDestructionDamage > 0
            ? new("DESTROY_SOURCE_DAMAGE_CONTROLLER_UNITS", TriggerTimings.TurnEnd, "摧毁来源并对其控制者的所有单位造成伤害。", DamageAmount: behavior.EndStepSelfDestructionDamage) : null);

    private static TriggerQueueItemState[] CaptureEndAbilities(MatchState state)
    {
        var result = new List<TriggerQueueItemState>();
        var locations = ReconcileObjectLocations(state.ObjectLocations, state.PlayerZones);
        foreach (var source in state.CardObjects.Values.OrderBy(c => c.ObjectId, StringComparer.Ordinal))
        {
            if (source.IsFaceDown || source.ControllerId != state.TurnPlayerId
                || !locations.TryGetValue(source.ObjectId, out var location)
                || location.Zone is not ("BASE" or "BATTLEFIELD" or "LEGEND")) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
            {
                var spec = EndSpec(instance.CardNo);
                if (spec is null || spec.RequiresSourceBattlefieldAtTrigger == true && location.Zone != "BATTLEFIELD") continue;
                result.Add(new($"end-{state.Tick + 1}-{source.ObjectId}-{instance.Id}", state.TurnPlayerId, source.ObjectId,
                    EndAbilityEffect, "TURN_END_STEP_STARTED", TimingStates.NeutralClosed) {
                    EndAbility = new(instance.CardNo, spec.Kind, source, location, state.TurnNumber,
                        spec.RuneReadyCount ?? spec.RuneCallCount ?? 0, instance.Origin) });
            }
        }
        foreach (var marker in state.UntilEndOfTurnEffects.Where(s => s.StartsWith(DelayedEndReadyPrefix, StringComparison.Ordinal)))
        {
            var context = ReadDelayedEndReady(marker);
            if (context is null || context.TurnNumber != state.TurnNumber) continue;
            result.Add(new("delayed-end-" + state.Tick + "-" + result.Count, context.Source.ControllerId!, context.Source.ObjectId,
                EndAbilityEffect, "TURN_END_STEP_STARTED", TimingStates.NeutralClosed) { EndAbility = context });
        }
        result.AddRange(CaptureDelayedEmpowerEndAbilities(state));
        result.AddRange(CaptureBombRebuildEndAbilities(state));
        return result.ToArray();
    }

    private static EndAbilityContext? ReadDelayedEndReady(string marker)
    {
        try { return JsonSerializer.Deserialize<EndAbilityContext>(Convert.FromBase64String(marker[DelayedEndReadyPrefix.Length..])); }
        catch (Exception e) when (e is JsonException or FormatException or ArgumentException) { return null; }
    }

    private static ResolutionResult BeginEndStep(MatchState state, PlayerIntent intent, TriggerQueueItemState[] triggers)
    {
        var next = state with { Tick = state.Tick + 1, Phase = MatchPhases.TurnEnd,
            EndStep = new(intent.PlayerId, state.TurnNumber), TimingState = TimingStates.NeutralClosed,
            TriggerQueue = state.TriggerQueue.Concat(triggers).ToArray(), PriorityPlayerId = null, PassedPriorityPlayerIds = [],
            UntilEndOfTurnEffects = state.UntilEndOfTurnEffects.Where(s => !s.StartsWith(DelayedEndReadyPrefix, StringComparison.Ordinal) && !s.StartsWith(DelayedEmpowerPrefix, StringComparison.Ordinal) && !s.StartsWith(BombRebuildPrefix, StringComparison.Ordinal)).ToArray() };
        return new(true, null, next, [new("TURN_END_STEP_STARTED", "开始结束步骤，先处理触发和待确认出牌", new Dictionary<string,object?> {
            ["playerId"] = intent.PlayerId, ["turnNumber"] = state.TurnNumber })], ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static ResolutionResult AdvanceEndStep(ResolutionResult result)
    {
        if (!result.Accepted || result.State.EndStep is not { } end || result.State.Status != MatchStatuses.InProgress
            || result.State.PendingRuleChoice is not null || result.State.BattleState.IsActive
            || TurnSequenceBlocked(result.State)) return result;
        var finished = CompleteTurnEnd(result.State with { EndStep = null }, new("end-step-complete", end.PlayerId, CommandTypes.EndTurn), false);
        return finished with { Events = result.Events.Concat(finished.Events).ToArray() };
    }

    private static string[] EndRuneTargets(MatchState state, string player)
        => state.PlayerZones[player].Base.Where(id => state.CardObjects.TryGetValue(id, out var card)
            && !card.IsFaceDown && IsRuneObject(id, card) && SourceObjectControlledByPlayerOrLegacyOwned(card, player)).ToArray();

    private static ResolutionResult PrepareEndRuneTargets(ResolutionResult result, StackItemState item)
        => PrepareRuneTargets(result, item, item.EndAbility!.RuneCount, EndRuneChoiceWindow, "END-RUNES:" + item.StackItemId);

    private static ResolutionResult PrepareRuneTargets(ResolutionResult result, StackItemState item, int count,
        string window, string choiceId)
    {
        var state = result.State; var ids = EndRuneTargets(state, item.ControllerId);
        var choice = new PendingCardChoiceState(choiceId, window, item.ControllerId,
            0, Math.Min(ids.Length, count), ids, ids, "选择至多指定数量的符文变为活跃状态", item.SourceObjectId, item.EffectKind) {
            ResolvingStackItemId = item.StackItemId };
        if (ids.Length == 0)
        {
            var confirmed = item with { TargetGenerations = new Dictionary<string,long>() };
            var next = state with { StackItems = state.StackItems.Select(s => s.StackItemId == item.StackItemId ? confirmed : s).ToArray() };
            return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
        }
        var waiting = state with { PendingCardChoice = choice, PriorityPlayerId = null, PassedPriorityPlayerIds = [], ActivePlayerId = item.ControllerId };
        return result with { State = waiting, Snapshots = ResolutionResult.BuildSnapshots(waiting), Prompts = BuildCorePrompts(waiting) };
    }

    private static ResolutionResult ResolveEndRuneChoice(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        var item = state.StackItems.LastOrDefault(s => s.StackItemId == choice.ResolvingStackItemId);
        if (item?.EndAbility is not { Kind: TriggerKinds.TurnEndReadyRunes or "DELAYED_READY_RUNES" } context || item.TargetGenerations is not null
            || selected.Count > context.RuneCount || selected.Any(id => !EndRuneTargets(state, item.ControllerId).Contains(id)))
            return RejectWithCorePrompts(state, "结束步骤符文选择已失效。", ErrorCodes.InvalidTarget);
        return ConfirmRuneTargets(state, item, selected, "END_RUNE_TARGETS_CONFIRMED", "结束步骤符文目标已确认，双方可以响应");
    }

    private static ResolutionResult ConfirmRuneTargets(MatchState state, StackItemState item, IReadOnlyList<string> selected,
        string eventKind, string message)
    {
        item = item with { TargetObjectIds = selected, TargetGenerations = selected.ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration) };
        var next = state with { Tick = state.Tick + 1, PendingCardChoice = null, ActivePlayerId = item.ControllerId,
            PriorityPlayerId = item.ControllerId, PassedPriorityPlayerIds = [], StackItems = state.StackItems.Select(s => s.StackItemId == item.StackItemId ? item : s).ToArray() };
        return new(true, null, next, [new(eventKind, message, new Dictionary<string,object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId, ["objectIds"] = selected.ToArray() })],
            ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveEndAbility(MatchState state, StackItemState item)
    {
        if (item.EndAbility!.Kind == TriggerKinds.TurnEndRecycleRunesCall) return ResolveEndRuneRecycleCall(state, item);
        if (item.EndAbility!.Kind == "DESTROY_SOURCE_DAMAGE_CONTROLLER_UNITS") return ResolveEndSelfDestructionDamage(state, item);
        if (item.EndAbility!.Kind == TriggerKinds.TurnEndRevealUntilUnit)
        {
            if (item.EffectPlayCompleted) return NoopStackResolutionResult(state);
            TryGetEffectPlayDefinition(item, out var definition); return BeginDeckPlaySequence(state, item, definition);
        }
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value); var events = new List<GameEvent>();
        foreach (var id in item.TargetObjectIds)
            if (EndRuneTargets(state, item.ControllerId).Contains(id) && cards[id].ObjectGeneration == item.TargetGenerations!.GetValueOrDefault(id, -1))
            {
                cards[id] = cards[id] with { IsExhausted = false };
                events.Add(new("RUNE_READIED", "结束步骤让选择的符文变为活跃状态", new Dictionary<string,object?> {
                    ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId, ["objectIds"] = new[]{id}, ["count"] = 1 }));
            }
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }

    private static StackResolutionResult ResolveEndSelfDestructionDamage(MatchState state, StackItemState item)
    {
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var destroy = cards.TryGetValue(item.SourceObjectId, out var source)
            && source.ObjectGeneration == item.EndAbility!.Source.ObjectGeneration && IsObjectOnField(zones, source.ObjectId)
                ? new HashSet<string> { source.ObjectId } : [];
        var removal = ResolveFieldDestructions(state, zones, cards, item, new HashSet<string>(), state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(),
            state.RunePools, objectLocations: locations, explicitDestroyObjectIds: destroy);
        var events = removal.Events.ToList(); var lethal = new HashSet<string>();
        var damageState = state with { PlayerZones = zones, CardObjects = cards, RunePools = removal.RunePools };
        foreach (var id in zones[item.ControllerId].Base.Concat(zones[item.ControllerId].Battlefields)
            .Where(id => cards.TryGetValue(id, out var card) && IsFaceUpNonStandbyUnit(card)).ToArray()) {
            var amount = ApplySpellSkillDamageBonuses(damageState, item.ControllerId, id, EndSpec(item.CardNo)!.DamageAmount!.Value);
            var damage = ApplyDamageToCardObject(state, cards, item.ControllerId, id, amount, lethal,
                state.UntilEndOfTurnEffects.Contains(PreventSpellAndSkillDamageThisTurnEffectId), PreventSpellAndSkillDamageThisTurnEffectId);
            events.Add(new("DAMAGE_APPLIED", "结束技能对控制者的单位造成伤害", BuildDamagePayload(item.SourceObjectId, id, damage)));
        }
        var cleanup = RunStateBasedCleanupLoop(damageState, zones, cards, item, removal.RunePools,
            damageTriggeredDestroyTargetObjectIds: lethal, objectLocations: locations,
            destroyedUnitOwnerIdsAlreadyThisTurn: removal.DestroyedUnitOwnerIds);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = cleanup.RunePools,
            TriggerQueue = removal.TriggerQueue.Concat(cleanup.TriggerQueue).DistinctBy(t => t.TriggerId).ToArray(),
            DestroyedUnitOwnerIds = cleanup.DestroyedUnitOwnerIds, Events = events.Concat(cleanup.Events).ToArray() };
    }

    internal static bool ValidEndAbility(MatchState state, StackItemState item)
    {
        if (item.EndAbility is not { } c) return item.EffectKind is not (EndAbilityEffect or CreateEndAbilityEffect);
        var spec = c.Kind == "DELAYED_READY_RUNES" && BattlefieldTriggerSpecRules.TryGetTrigger(c.CardNo,
            BattlefieldTriggerSpecRules.IsBattlefieldConquerReadyRunesAtEndTrigger, out var delayed) ? delayed : EndSpec(c.CardNo);
        var creation = item.EffectKind == CreateEndAbilityEffect;
        return (creation ? c.Kind == "DELAYED_READY_RUNES"
            : state.EndStep is { } step && state.Phase == MatchPhases.TurnEnd && step.PlayerId == state.TurnPlayerId
                && step.TurnNumber == state.TurnNumber && (c.Kind == "DELAYED_READY_RUNES" || item.ControllerId == step.PlayerId))
            && spec is not null
            && (c.Kind == spec.Kind || c.Kind == "DELAYED_READY_RUNES" && spec.Kind == TriggerKinds.BattlefieldConquerReadyRunesAtEnd)
            && c.CardNo == item.CardNo && item.EffectKind is EndAbilityEffect or CreateEndAbilityEffect
            && c.Source.ObjectId == item.SourceObjectId && c.Source.ObjectGeneration >= 0 && !c.Source.IsFaceDown
            && c.TurnNumber == state.TurnNumber && state.Seats.ContainsKey(item.ControllerId)
            && c.Source.ControllerId == item.ControllerId && c.RuneCount == (spec.RuneReadyCount ?? spec.RuneCallCount ?? 0)
            && c.Source.OwnerId is { } owner && state.Seats.ContainsKey(owner)
            && (c.Origin is not null || c.Source.CardNo == c.CardNo)
            && (c.Kind != "DELAYED_READY_RUNES" ? c.CreationTick is null
                : c.CreationTick is >= 0 && c.CreationTick <= state.Tick && c.Origin is null
                    && c.Location.Zone == "BATTLEFIELD" && c.Location.BattlefieldObjectId == c.Source.ObjectId
                    && c.Source.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag))
            && c.Location.PlayerId == item.ControllerId && c.Location.Zone is "BASE" or "BATTLEFIELD" or "LEGEND"
            && (spec.RequiresSourceBattlefieldAtTrigger != true || c.Location.Zone == "BATTLEFIELD")
            && (state.CardObjects.TryGetValue(item.SourceObjectId, out var current) ? current.ObjectGeneration >= c.Source.ObjectGeneration
                : IsTokenObject(c.Source) && P6TokenFactoryCatalog.TryGetByCardNo(c.Source.TokenFactoryCardNo ?? c.Source.CardNo ?? "", out _))
            && AttachedRuleTextRules.ValidCaptured(state, c.Origin, item.SourceObjectId, c.Source.ObjectGeneration, c.CardNo)
            && item.EffectRepeatCount == 1 && item.OptionalCosts.Count == 0 && item.DamageAmount == 0
            && (creation || c.Kind is TriggerKinds.TurnEndRevealUntilUnit or TriggerKinds.TurnEndRecycleRunesCall ? item.TargetObjectIds.Count == 0 && item.TargetGenerations is null
                : item.TargetObjectIds.Count <= c.RuneCount && (item.TargetGenerations is null ? item.TargetObjectIds.Count == 0
                    : item.TargetGenerations.Keys.ToHashSet().SetEquals(item.TargetObjectIds)
                        && item.TargetGenerations.All(p => p.Value >= 0 && state.CardObjects.TryGetValue(p.Key,out var rune)
                            && IsRuneObject(p.Key,rune) && rune.ObjectGeneration >= p.Value)));
    }

    internal static bool ValidEndTrigger(MatchState state, TriggerQueueItemState trigger)
        => trigger.EndAbility is null ? trigger.EffectKind is not (EndAbilityEffect or CreateEndAbilityEffect)
            : trigger.TriggeredByEventKind == (trigger.EffectKind == CreateEndAbilityEffect ? "BATTLEFIELD_CONQUERED" : "TURN_END_STEP_STARTED")
                && ValidEndAbility(state, BuildStackItemForOrderedTrigger(state, trigger));

    internal static bool ValidEndRuneChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.LastOrDefault(s => s.StackItemId == choice.ResolvingStackItemId);
        if (item?.EndAbility is not { Kind: TriggerKinds.TurnEndReadyRunes or "DELAYED_READY_RUNES" } c
            || !ValidEndAbility(state, item) || item.TargetGenerations is not null) return false;
        var ids = EndRuneTargets(state, item.ControllerId);
        return choice.ChoiceId == "END-RUNES:" + item.StackItemId && choice.PlayerId == item.ControllerId
            && choice.SourceObjectId == item.SourceObjectId && choice.EffectKind == item.EffectKind
            && choice.RequiredCount == 0 && choice.MaxCount == Math.Min(ids.Length, c.RuneCount)
            && ids.SequenceEqual(choice.LegalObjectIds) && ids.SequenceEqual(choice.ContextObjectIds);
    }

    internal static bool ValidDelayedEndReady(MatchState state, string marker)
    {
        var c = ReadDelayedEndReady(marker);
        if (c is null || state.EndStep is not null || c.Kind != "DELAYED_READY_RUNES") return false;
        var ending = state with { Phase = MatchPhases.TurnEnd, EndStep = new(state.TurnPlayerId, state.TurnNumber) };
        return ValidEndAbility(ending, new("delayed-validation", c.Source.ControllerId!, c.Source.ObjectId,
            EndAbilityEffect, c.CardNo) { EndAbility = c });
    }

    private static IEnumerable<TriggerQueueItemState> CaptureDelayedEndCreation(MatchState state, string controller, string battlefield, long tick)
    {
        if (!TryGetBattlefieldCardObject(state.PlayerZones, state.CardObjects, battlefield, out var id, out var card)
            || !BattlefieldTriggerSpecRules.TryGetTrigger(card.CardNo, BattlefieldTriggerSpecRules.IsBattlefieldConquerReadyRunesAtEndTrigger, out var spec)) yield break;
        var context = new EndAbilityContext(card.CardNo!, "DELAYED_READY_RUNES", card with {ControllerId=controller},
            new(controller,"BATTLEFIELD",id),state.TurnNumber,spec.RuneReadyCount ?? 0,CreationTick:tick);
        yield return new("conquest-end-"+tick+"-"+id,controller,id,CreateEndAbilityEffect,"BATTLEFIELD_CONQUERED") {EndAbility=context};
    }

    private static StackResolutionResult ResolveDelayedEndCreation(MatchState state, StackItemState item)
    {
        if (state.EndStep is not null || state.Phase == MatchPhases.TurnEnd) return NoopStackResolutionResult(state);
        var marker=DelayedEndReadyPrefix+Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(item.EndAbility));
        return NoopStackResolutionResult(state) with {UntilEndOfTurnEffects=state.UntilEndOfTurnEffects.Append(marker).ToArray(),
            Events=[new("RUNE_READY_SCHEDULED","生成本回合结束时选择符文的延迟技能",new Dictionary<string,object?> {
                ["playerId"]=item.ControllerId,["sourceObjectId"]=item.SourceObjectId,["cardNo"]=item.CardNo,
                ["count"]=item.EndAbility!.RuneCount,["effectIds"]=new[]{marker},["abilityId"]=TriggerKinds.BattlefieldConquerReadyRunesAtEnd})]};
    }
}

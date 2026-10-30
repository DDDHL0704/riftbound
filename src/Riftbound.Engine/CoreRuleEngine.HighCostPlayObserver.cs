using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HighCostSkillWitness(ObjectBinding Source, string CardNo, string AbilityId,
    string EffectKind, int BaseMana, long Tick, long ActivationTick, long FaceRevision, string? TokenFactoryCardNo,
    EmpowerActivationContext? Empower = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HighCostPlayObserverContext(ObjectBinding Source, string CardNo, string ControllerId,
    long CapturedTick, long FaceRevision, string RuleTextInstanceId, CardPlayHistoryEntry? CardPlay,
    HighCostSkillWitness? SkillPlay, string? SourceTokenFactoryCardNo = null,
    RuleTextInstanceBinding? Origin = null, bool Paid = false);

public sealed partial class CoreRuleEngine
{
    internal const string HighCostPlayEffect = "CONTROLLER_HIGH_COST_PLAY_READY_RUNES";
    internal const string HighCostReadySourceEffect = "CONTROLLER_HIGH_COST_PLAY_READY_SOURCE";
    internal const string CardPlayBasePowerEffect = "CONTROLLER_CARD_PLAY_BASE_POWER";
    internal const string HighCostGraveyardPlayEffect = "CONTROLLER_HIGH_COST_GRAVEYARD_PLAY";
    private sealed record HighCostPlayObserverCandidate(ObjectBinding Source, string CardNo, string ControllerId,
        long FaceRevision, string RuleTextInstanceId, string? SourceTokenFactoryCardNo, RuleTextInstanceBinding? Origin)
    {
        internal HighCostPlayObserverContext ToContext(long tick, CardPlayHistoryEntry? card, HighCostSkillWitness? skill)
            => new(Source, CardNo, ControllerId, tick, FaceRevision, RuleTextInstanceId, card, skill,
                SourceTokenFactoryCardNo, Origin);
    }
    private static readonly Lazy<IReadOnlyDictionary<string, OfficialCard>> HighCostCards = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal));
    private static int? PrintedManaCost(string cardNo) => HighCostCards.Value.GetValueOrDefault(cardNo)?.Energy;
    private static TriggerSpec? HighCostPlaySpec(string cardNo) => SourceUnitPlayedTriggerSpecRules.TriggersForCard(cardNo)
        .FirstOrDefault(t => t.Kind is TriggerKinds.ControllerHighCostUnitEquipmentOrAbilityReadyRunes
            or TriggerKinds.ControllerHighCostCardReadySelf or TriggerKinds.ControllerCardPlayBasePowerSource
            or TriggerKinds.ControllerHighCostGraveyardPlaySource);
    internal static bool HighCostReadiesSource(string? cardNo) => cardNo is not null
        && HighCostPlaySpec(cardNo)?.Kind == TriggerKinds.ControllerHighCostCardReadySelf;
    internal static bool HighCostSetsSourceBasePower(string? cardNo) => cardNo is not null
        && HighCostPlaySpec(cardNo)?.Kind == TriggerKinds.ControllerCardPlayBasePowerSource;
    internal static bool HighCostPlaysSourceFromGraveyard(string? cardNo) => cardNo is not null
        && HighCostPlaySpec(cardNo)?.Kind == TriggerKinds.ControllerHighCostGraveyardPlaySource;
    private static string HighCostEffectFor(string cardNo) => HighCostReadiesSource(cardNo) ? HighCostReadySourceEffect
        : HighCostSetsSourceBasePower(cardNo) ? CardPlayBasePowerEffect
        : HighCostPlaysSourceFromGraveyard(cardNo) ? HighCostGraveyardPlayEffect : HighCostPlayEffect;
    private static bool HighCostObservesCard(string cardNo, int types) => HighCostReadiesSource(cardNo)
        || HighCostSetsSourceBasePower(cardNo) || HighCostPlaysSourceFromGraveyard(cardNo) || (types & 3) != 0;
    private static HighCostPlayObserverCandidate[] CaptureHighCostPlayObservers(
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards, string player)
    {
        if (!zones.TryGetValue(player, out var own)) return [];
        var field = own.Base.Concat(own.Battlefields).Concat(own.LegendZone).Distinct(StringComparer.Ordinal)
            .Where(id => cards.TryGetValue(id, out var source) && !source.IsFaceDown
                && SourceObjectControlledByPlayerOrLegacyOwned(source, player))
            .Order(StringComparer.Ordinal)
            .SelectMany(id => AttachedRuleTextRules.Instances(zones, cards, cards[id])
                .Where(instance => HighCostPlaySpec(instance.CardNo) is not null)
                .Select(instance => new HighCostPlayObserverCandidate(new(id, cards[id].ObjectGeneration),
                    instance.CardNo, player, instance.Origin?.FaceRevision ?? cards[id].CopyFaceTransitions?.Count ?? 0,
                    instance.Id, cards[id].TokenFactoryCardNo, instance.Origin)))
            .ToArray();
        var graveyard = own.Graveyard.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Where(id => cards.TryGetValue(id, out var source) && !source.IsFaceDown
                && HighCostPlaysSourceFromGraveyard(source.CardNo) && source.OwnerId == player)
            .Select(id => new HighCostPlayObserverCandidate(new(id, cards[id].ObjectGeneration), cards[id].CardNo!, player,
                cards[id].CopyFaceTransitions?.Count ?? 0, $"PRINTED:{id}:{cards[id].ObjectGeneration}", cards[id].TokenFactoryCardNo, null))
            .ToArray();
        return field.Concat(graveyard).ToArray();
    }

    private const string HighCostSkillCaptureKey = "internalHighCostSkillObservers";
    private sealed record CapturedHighCostSkill(HighCostPlayObserverContext Context);

    // CN 377.2.a: observers of "playing" an activated skill trigger when
    // that skill resolves. Capture before post-resolution state cleanup, so
    // observers are determined at the event rather than from a later state.
    private static CapturedHighCostSkill[] CaptureHighCostSkillResolution(MatchState before, StackItemState item,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards, long tick)
    {
        if (!item.StackItemId.StartsWith("STACK-", StringComparison.Ordinal) || !StackObjectRules.IsAbility(item)
            || item.SourceConfirmed || string.IsNullOrWhiteSpace(item.CardNo)) return [];
        var observers = CaptureHighCostPlayObservers(zones, cards, item.ControllerId)
            .Where(observer => !HighCostReadiesSource(observer.CardNo)).ToArray();
        if (observers.Length == 0) return [];
        var split = item.StackItemId.IndexOf('-', 6);
        if (split < 0 || !long.TryParse(item.StackItemId.AsSpan(6, split - 6), out var activationTick)) return [];
        before.CardObjects.TryGetValue(item.SourceObjectId, out var source);
        var empower = item.EmpowerActivation;
        var generation = empower?.Source.Generation ?? item.Equip?.SourceGeneration
            ?? item.TargetedPowerAbility?.SourceGeneration ?? item.LegendAttachment?.SourceGeneration
            ?? item.LegendDuelMovement?.SourceGeneration ?? item.LegendUnitKeyword?.SourceGeneration
            ?? item.LegendUnitEffect?.SourceGeneration ?? item.LegendEquipmentReady?.SourceGeneration
            ?? item.LegendDiscardDraw?.Source.Generation ?? source?.ObjectGeneration;
        if (generation is null) return [];
        var faceRevision = empower?.FaceRevision ?? (source?.CopyFaceTransitions?.Count(t => t.Tick <= activationTick) ?? 0);
        string? abilityId = null; int? baseMana = null;
        if (empower is not null)
        {
            var specs = EmpowerRules.Specs(empower.CardNo);
            if (empower.Keyword < 0 || empower.Keyword >= specs.Count || empower.Alternative < 0
                || empower.Alternative >= specs[empower.Keyword].Alternatives.Count) return [];
            abilityId = $"EMPOWER:{empower.Keyword}:{empower.Alternative}";
            baseMana = specs[empower.Keyword].Alternatives[empower.Alternative].Mana;
        }
        else if (item.Equip is not null && AssembleEquipmentProfileCatalog.TryGet(item.CardNo, out var equip))
        { abilityId = "ASSEMBLE"; baseMana = equip.ManaCost; }
        else if (P4ActivatedAbilityCatalog.TryGetByEffectKind(item.EffectKind, out var ability)
            && P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, item.CardNo))
        { abilityId = ability.AbilityId; baseMana = ability.ManaCost; }
        else
        {
            abilityId = item.LegendAttachment?.AbilityId ?? item.LegendDuelMovement?.AbilityId
                ?? item.LegendUnitKeyword?.AbilityId ?? item.LegendUnitEffect?.AbilityId
                ?? item.LegendEquipmentReady?.AbilityId ?? item.LegendDiscardDraw?.AbilityId;
            if (abilityId is null && TryGetLegendUnitToken(item.EffectKind, out var token)) abilityId = token.AbilityId;
            if (abilityId is not null && TryGetLegendAbility(abilityId, out var legend)) baseMana = legend.ManaCost;
        }
        if (abilityId is null || baseMana is not int mana
            || !observers.Any(observer => mana >= HighCostPlaySpec(observer.CardNo)!.MinimumCardMana)) return [];
        var witness = new HighCostSkillWitness(new(item.SourceObjectId, generation.Value), item.CardNo, abilityId,
            item.EffectKind, mana, tick, activationTick, faceRevision, source?.TokenFactoryCardNo, empower);
        if (!ValidHighCostSkillWitness(before with { Tick = tick }, item.ControllerId, witness)) return [];
        return observers.Where(observer => mana >= HighCostPlaySpec(observer.CardNo)!.MinimumCardMana)
            .Select(observer => new CapturedHighCostSkill(observer.ToContext(tick, null, witness))).ToArray();
    }

    private static ResolutionResult QueueHighCostSkillObservers(MatchState before, ResolutionResult result, int eventOffset)
    {
        if (!result.Accepted) return result;
        var queue = result.State.TriggerQueue.ToList(); var events = new List<GameEvent>(); var found = false;
        foreach (var (ev, index) in result.Events.Select((value, index) => (value, index)))
        {
            var captured = ev.Payload.GetValueOrDefault(HighCostSkillCaptureKey) as CapturedHighCostSkill[];
            // A suspended instruction can finish through a later continuation
            // rather than the normal pass-priority path.
            if (captured is null && index >= eventOffset && ev.Kind == "STACK_ITEM_RESOLVED"
                && ev.Payload.GetValueOrDefault("stackItemId") is string id
                && before.StackItems.FirstOrDefault(item => item.StackItemId == id) is { } finished
                && result.State.StackItems.All(item => item.StackItemId != id))
                captured = CaptureHighCostSkillResolution(before, finished, result.State.PlayerZones,
                    result.State.CardObjects, result.State.Tick);
            // Resource skills resolve immediately on confirmation (CN 401.2).
            if (captured is null && index >= eventOffset && ev.Kind is "ABILITY_ACTIVATED" or "LEGEND_ABILITY_ACTIVATED")
                captured = CaptureHighCostResourceSkill(before, result.State, ev);
            if (captured is { Length: 0 }) captured = null;
            if (captured is not null) found = true;
            events.Add(!ev.Payload.ContainsKey(HighCostSkillCaptureKey) ? ev : ev with { Payload = ev.Payload
                .Where(pair => pair.Key != HighCostSkillCaptureKey).ToDictionary(pair => pair.Key, pair => pair.Value) });
            if (result.State.Status != MatchStatuses.InProgress || captured is null) continue;
            foreach (var (observation, ordinal) in captured.Select((value, ordinal) => (value, ordinal)))
            {
                var context = observation.Context;
                var trigger = new TriggerQueueItemState($"high-cost-play-{context.CapturedTick}-{index}-{ordinal}",
                    context.ControllerId, context.Source.ObjectId, HighCostEffectFor(context.CardNo), "ABILITY_RESOLVED")
                    { HighCostPlayObserver = context, SourceCardNo = context.CardNo };
                queue.Add(trigger);
                var queued = BuildTriggerQueuedEvent(trigger);
                events.Add(queued with { Payload = new Dictionary<string, object?>(queued.Payload) {
                    ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(result.State, context.Source.ObjectId, "__spectator__") } });
            }
        }
        if (!found) return result;
        var next = result.State with { TriggerQueue = queue };
        return result with { State = next, Events = events, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    private static CapturedHighCostSkill[] CaptureHighCostResourceSkill(MatchState before, MatchState after, GameEvent ev)
    {
        if (ev.Payload.GetValueOrDefault("playerId") is not string player
            || ev.Payload.GetValueOrDefault("sourceObjectId") is not string sourceId
            || ev.Payload.GetValueOrDefault("abilityId") is not string abilityId
            || !before.CardObjects.TryGetValue(sourceId, out var source) || source.CardNo is not { } cardNo) return [];
        int mana; string effect;
        if (P4ActivatedAbilityCatalog.TryGetByAbilityId(abilityId, out var ability) && ability.IsResourceSkill
            && P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, cardNo))
        { mana = ability.ManaCost; effect = ability.EffectKind; }
        else if (TryGetLegendAbility(abilityId, out var legend) && legend.TimingKind == LegendAbilityTimingKinds.ResourceAction
            && legend.SourceCardNos.Contains(cardNo))
        { mana = legend.ManaCost; effect = legend.EffectKind; }
        else return [];
        var observers = CaptureHighCostPlayObservers(after.PlayerZones, after.CardObjects, player)
            .Where(observer => !HighCostReadiesSource(observer.CardNo) && mana >= HighCostPlaySpec(observer.CardNo)!.MinimumCardMana).ToArray();
        if (observers.Length == 0) return [];
        var witness = new HighCostSkillWitness(new(sourceId, source.ObjectGeneration), cardNo, abilityId, effect,
            mana, after.Tick, after.Tick, source.CopyFaceTransitions?.Count ?? 0, source.TokenFactoryCardNo);
        if (!ValidHighCostSkillWitness(after, player, witness)) return [];
        return observers.Select(observer => new CapturedHighCostSkill(observer.ToContext(after.Tick, null, witness))).ToArray();
    }

    private static bool ValidHighCostSkillWitness(MatchState state, string controller, HighCostSkillWitness skill)
    {
        if (skill.Source is not { Generation: >= 0 } || string.IsNullOrWhiteSpace(skill.Source.ObjectId)
            || string.IsNullOrWhiteSpace(skill.CardNo) || string.IsNullOrWhiteSpace(skill.AbilityId)
            || skill.Tick <= 0 || skill.Tick > state.Tick || skill.ActivationTick <= 0 || skill.ActivationTick > skill.Tick
            || skill.FaceRevision < 0 || skill.BaseMana < 0) return false;
        if (skill.Empower is { } empower)
        {
            var specs = EmpowerRules.Specs(empower.CardNo);
            if (empower.Keyword < 0 || empower.Keyword >= specs.Count || empower.Alternative < 0
                || empower.Alternative >= specs[empower.Keyword].Alternatives.Count
                || skill.CardNo != empower.CardNo || skill.Source != empower.Source || skill.ActivationTick != empower.AcquiredTick
                || skill.AbilityId != $"EMPOWER:{empower.Keyword}:{empower.Alternative}" || skill.EffectKind != EmpowerRules.Effect
                || skill.BaseMana != specs[empower.Keyword].Alternatives[empower.Alternative].Mana) return false;
            var item = new StackItemState($"STACK-{empower.AcquiredTick}-{skill.Source.ObjectId}-EMPOWER", controller,
                skill.Source.ObjectId, EmpowerRules.Effect, skill.CardNo, [], 0, 0, []) { EmpowerActivation = empower };
            return EmpowerRules.Valid(state, item);
        }
        if (!PredictBindingMatches(state, skill.Source, skill.CardNo, skill.FaceRevision, skill.ActivationTick, skill.TokenFactoryCardNo)) return false;
        if (skill.AbilityId == "ASSEMBLE") return skill.EffectKind == EquipEffect
            && AssembleEquipmentProfileCatalog.TryGet(skill.CardNo, out var equip) && skill.BaseMana == equip.ManaCost;
        if (P4ActivatedAbilityCatalog.TryGetByAbilityId(skill.AbilityId, out var ability))
            return P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, skill.CardNo)
                && ability.EffectKind == skill.EffectKind && ability.ManaCost == skill.BaseMana;
        return TryGetLegendAbility(skill.AbilityId, out var legend) && legend.SourceCardNos.Contains(skill.CardNo)
            && legend.ManaCost == skill.BaseMana;
    }

    internal static bool ValidHighCostObserver(MatchState state, HighCostPlayObserverContext? context,
        string effect, string sourceId, string controller, string? cardNo)
    {
        if (context is null) return effect is not (HighCostPlayEffect or HighCostReadySourceEffect or CardPlayBasePowerEffect or HighCostGraveyardPlayEffect);
        if (string.IsNullOrWhiteSpace(context.CardNo)) return false;
        var spec = HighCostPlaySpec(context.CardNo);
        if (spec?.MinimumCardMana is not { } minimum || effect != HighCostEffectFor(context.CardNo) || context.CardNo != cardNo
            || context.Source is not { Generation: >= 0 } || context.Source.ObjectId != sourceId
            || context.ControllerId != controller || !state.Seats.ContainsKey(controller)
            || context.CapturedTick <= 0 || context.CapturedTick > state.Tick || context.FaceRevision < 0
            || (context.CardPlay is null) == (context.SkillPlay is null)) return false;
        if (context.CardPlay is { } card && (!HighCostObservesCard(context.CardNo, card.Types) || card.PlayerId != controller
            || card.Tick != context.CapturedTick || PrintedManaCost(card.CardNo) is not int printed || printed < minimum
            || !state.CardPlays.Resolved.Contains(card))) return false;
        if (context.SkillPlay is { } skill && (HighCostReadiesSource(context.CardNo) || skill.Tick != context.CapturedTick || skill.BaseMana < minimum
            || skill.Empower is not null && (skill.Tick <= skill.ActivationTick
                || state.StackItems.Any(item => item.EmpowerActivation == skill.Empower))
            || !ValidHighCostSkillWitness(state, controller, skill))) return false;
        return context.Origin is null
            ? context.RuleTextInstanceId == $"PRINTED:{context.Source.ObjectId}:{context.Source.Generation}"
                && PredictBindingMatches(state, context.Source, context.CardNo, context.FaceRevision,
                    context.CapturedTick, context.SourceTokenFactoryCardNo)
            : context.Origin.Equipment is { } equipment
                && context.RuleTextInstanceId == $"ATTACHED:{equipment.ObjectId}:{equipment.Generation}:{context.Origin.Revision}"
                && AttachedRuleTextRules.ValidCaptured(state, context.Origin, sourceId, context.Source.Generation, context.CardNo);
    }

    internal static bool ValidHighCostTrigger(MatchState state, TriggerQueueItemState item)
        => ValidHighCostObserver(state, item.HighCostPlayObserver, item.EffectKind, item.SourceObjectId, item.ControllerId, item.SourceCardNo)
            && (item.HighCostPlayObserver is null || !item.HighCostPlayObserver.Paid
                && item.TriggeredByEventKind == (item.HighCostPlayObserver.CardPlay is null ? "ABILITY_RESOLVED" : "CARD_PLAY_COMPLETED")
                && item.TriggerId.StartsWith($"high-cost-play-{item.HighCostPlayObserver.CapturedTick}-", StringComparison.Ordinal));

    internal static bool ValidHighCostStack(MatchState state, StackItemState item)
    {
        var context = item.HighCostPlayObserver;
        if (!ValidHighCostObserver(state, context, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo)) return false;
        if (context is null) return true;
        if (HighCostReadiesSource(context.CardNo))
            return item.StackItemId.StartsWith($"ordered-high-cost-play-{context.CapturedTick}-", StringComparison.Ordinal)
                && !context.Paid && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null
                && item.OptionalCosts.Count == 0 && item.DamageAmount == 0 && item.EffectRepeatCount == 1 && !item.SourceConfirmed;
        if (HighCostSetsSourceBasePower(context.CardNo) || HighCostPlaysSourceFromGraveyard(context.CardNo))
            return item.StackItemId.StartsWith($"ordered-high-cost-play-{context.CapturedTick}-", StringComparison.Ordinal)
                && (!context.Paid && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null
                    || context.Paid && item.TargetObjectIds.Count <= 1
                        && (item.TargetGenerations is null || item.TargetGenerations.Count == item.TargetObjectIds.Count))
                && item.OptionalCosts.Count == 0 && item.DamageAmount == 0 && item.EffectRepeatCount == 1 && !item.SourceConfirmed;
        var max = HighCostPlaySpec(context.CardNo)!.RuneReadyCount ?? 0;
        return item.StackItemId.StartsWith($"ordered-high-cost-play-{context.CapturedTick}-", StringComparison.Ordinal)
            && item.TargetObjectIds.Count <= max && item.TargetObjectIds.Distinct(StringComparer.Ordinal).Count() == item.TargetObjectIds.Count
            && (item.TargetGenerations is null ? !context.Paid && item.TargetObjectIds.Count == 0
                : item.TargetGenerations.Count == item.TargetObjectIds.Count
                    && item.TargetObjectIds.All(id => item.TargetGenerations.TryGetValue(id, out var generation) && generation >= 0
                        && state.CardObjects.TryGetValue(id, out var rune) && IsRuneObject(id, rune) && rune.ObjectGeneration >= generation))
            && item.OptionalCosts.Count == 0 && item.DamageAmount == 0 && item.EffectRepeatCount == 1 && !item.SourceConfirmed;
    }

    private static bool CanPayHighCostExhaustion(MatchState state, StackItemState item)
    {
        if (item.HighCostPlayObserver is not { } context || !state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            || source.ObjectGeneration != context.Source.Generation || source.IsFaceDown) return false;
        if (HighCostSetsSourceBasePower(context.CardNo))
            return SourceObjectControlledByPlayerOrLegacyOwned(source, item.ControllerId)
                && IsObjectOnField(state.PlayerZones, source.ObjectId);
        if (HighCostPlaysSourceFromGraveyard(context.CardNo))
            return source.OwnerId == item.ControllerId && state.PlayerZones[item.ControllerId].Graveyard.Contains(source.ObjectId);
        return !source.IsExhausted && SourceObjectControlledByPlayerOrLegacyOwned(source, item.ControllerId)
            && (IsObjectOnField(state.PlayerZones, source.ObjectId)
                || state.PlayerZones[item.ControllerId].LegendZone.Contains(source.ObjectId));
    }

    private static string[] HighCostRuneTargets(MatchState state, StackItemState item)
        => state.PlayerZones.Values.SelectMany(z => z.Base.Concat(z.Battlefields)).Distinct(StringComparer.Ordinal)
            .Where(id => state.CardObjects.TryGetValue(id, out var rune) && !rune.IsFaceDown && IsRuneObject(id, rune)
                && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, id))
            .Order(StringComparer.Ordinal).ToArray();

    private static PendingCardChoiceState HighCostRuneChoice(MatchState state, StackItemState item)
    {
        if (HighCostSetsSourceBasePower(item.CardNo) || HighCostPlaysSourceFromGraveyard(item.CardNo))
            return new("HIGH-COST-RUNES:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId, 0, 0, [], [],
                HighCostPlaysSourceFromGraveyard(item.CardNo) ? "支付 1 黄色符能，或放弃从废牌堆打出此单位。" : "选择是否让来源本回合基础战力变为所打出卡牌的法力费用。",
                item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };
        var ids = HighCostRuneTargets(state, item);
        return new("HIGH-COST-RUNES:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId, 0,
            Math.Min(ids.Length, HighCostPlaySpec(item.CardNo!)!.RuneReadyCount ?? 0), ids, ids,
            "选择至多两枚符文，然后选择是否支付横置及适用的法盾费用。", item.SourceObjectId, item.EffectKind)
            { ResolvingStackItemId = item.StackItemId };
    }
    private static ResolutionResult PrepareHighCostRuneChoice(ResolutionResult result, StackItemState item)
    {
        if (!CanPayHighCostExhaustion(result.State, item)) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var choice = HighCostRuneChoice(result.State, item);
        if (choice.LegalObjectIds.Count == 0)
        {
            // No target choice is possible, but paying the optional exhaustion
            // cost is still a separate decision ("up to two" includes zero).
            var waiting = result.State with { PriorityPlayerId = null, ActivePlayerId = item.ControllerId, PassedPriorityPlayerIds = [] };
            var opened = ResolveTriggerTargetConfirmation(waiting, choice, [], advanceTick: false);
            return opened with { Events = result.Events.Concat(opened.Events).ToArray() };
        }
        var state = result.State with { PendingCardChoice = choice,
            ActivePlayerId = item.ControllerId, PriorityPlayerId = null, PassedPriorityPlayerIds = [] };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }
    internal static bool ValidHighCostRuneChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item?.HighCostPlayObserver is null || item.TargetGenerations is not null || !ValidHighCostStack(state, item)
            || !CanPayHighCostExhaustion(state, item)) return false;
        var expected = HighCostRuneChoice(state, item);
        return choice.ChoiceId == expected.ChoiceId && choice.PlayerId == expected.PlayerId && choice.SourceObjectId == expected.SourceObjectId
            && choice.EffectKind == expected.EffectKind && choice.ChoiceWindow == expected.ChoiceWindow
            && choice.RequiredCount == expected.RequiredCount && choice.MaxCount == expected.MaxCount
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds) && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    private static StackResolutionResult ResolveHighCostPlay(MatchState state, StackItemState item)
    {
        if (!ValidHighCostStack(state, item))
            throw new InvalidOperationException("Invalid high-cost play observer.");
        if (HighCostSetsSourceBasePower(item.CardNo))
        {
            if (item.HighCostPlayObserver is not { Paid: true, CardPlay: { } played }
                || !state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
                || source.ObjectGeneration != item.HighCostPlayObserver.Source.Generation
                || !IsObjectOnField(state.PlayerZones, source.ObjectId))
                return NoopStackResolutionResult(state);
            var printed = PrintedManaCost(played.CardNo);
            if (printed is not { } basePower) return NoopStackResolutionResult(state);
            var changed = TemporaryUnitEffectRules.SetNumber(source, TemporaryUnitEffectRules.BaseMightPrefix, basePower, retainZero: true);
            var recomputed = RecomputePowerAfterTemporaryBaseReplacement(state, changed, basePower);
            var baseCards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
            baseCards[source.ObjectId] = recomputed;
            return NoopStackResolutionResult(state) with { CardObjects = baseCards, Events = [new(
                "BASE_MIGHT_SET_UNTIL_END_OF_TURN", "设置单位本回合基础战力", new Dictionary<string, object?> {
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = item.SourceObjectId,
                    ["targetGeneration"] = source.ObjectGeneration, ["baseMight"] = basePower,
                    ["cardNo"] = item.CardNo, ["playedCardNo"] = played.CardNo })] };
        }
        if (HighCostPlaysSourceFromGraveyard(item.CardNo))
        {
            if (item.EffectPlayCompleted) return NoopStackResolutionResult(state);
            if (item.HighCostPlayObserver is not { Paid: true } context
                || !state.PlayerZones.TryGetValue(item.ControllerId, out var zones)
                || !zones.Graveyard.Contains(item.SourceObjectId)
                || !state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
                || source.ObjectGeneration != context.Source.Generation)
                return NoopStackResolutionResult(state);
            var parent = item with { TargetObjectIds = new[] { item.SourceObjectId }, TargetGenerations = null };
            var pending = BeginEffectPlay(state, parent, HighCostGraveyardPlayBehavior(context.CardNo));
            return pending with { Events = new[] { BuildTriggerResolvedEvent(new(item.StackItemId, item.ControllerId,
                item.SourceObjectId, item.EffectKind, context.CardPlay is null ? "ABILITY_RESOLVED" : "CARD_PLAY_COMPLETED") { HighCostPlayObserver = context }) }
                .Concat(pending.Events).ToArray() };
        }
        if (HighCostReadiesSource(item.CardNo))
        {
            var cardsToReady = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
            var readyEvents = new List<GameEvent>();
            if (cardsToReady.TryGetValue(item.SourceObjectId, out var source)
                && source.ObjectGeneration == item.HighCostPlayObserver!.Source.Generation
                && IsFaceUpNonStandbyUnit(source) && IsObjectOnField(state.PlayerZones, source.ObjectId))
            {
                var behavior = new CardBehaviorDefinition(item.CardNo!, "高费用打出触发活跃", 0, item.EffectKind, 0, 0, ReadiesTarget: true);
                cardsToReady[source.ObjectId] = ApplyReadyState(source, behavior, item, source.ObjectId,
                    out var ready, state.PlayerZones, cardsToReady);
                if (ready is not null) readyEvents.Add(ready);
            }
            return NoopStackResolutionResult(state) with { CardObjects = cardsToReady, Events = readyEvents };
        }
        if (item.HighCostPlayObserver is not { Paid: true }) throw new InvalidOperationException("Unpaid high-cost rune observer.");
        var legal = HighCostRuneTargets(state, item); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var events = new List<GameEvent>();
        foreach (var id in item.TargetObjectIds)
            if (legal.Contains(id) && cards[id].ObjectGeneration == item.TargetGenerations!.GetValueOrDefault(id, -1)
                && cards[id].IsExhausted)
            {
                cards[id] = cards[id] with { IsExhausted = false };
                events.Add(new("RUNE_READIED", "触发技能让选择的符文变为活跃状态", new Dictionary<string, object?> {
                    ["playerId"] = item.ControllerId, ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__"),
                    ["objectIds"] = new[] { id }, ["count"] = 1 }));
            }
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }

    internal static CardBehaviorDefinition HighCostGraveyardPlayBehavior(string cardNo)
        => new(cardNo, "高费用触发从废牌堆打出", 0, HighCostGraveyardPlayEffect, 0, 0,
            EffectPlaySourceZone: "GRAVEYARD", EffectPlayIgnoreBaseMana: true,
            EffectPlayIgnoreBasePower: true, EffectPlayDestination: "BASE");

    private static CardObjectState RecomputePowerAfterTemporaryBaseReplacement(MatchState state, CardObjectState card, int basePower)
    {
        var printed = PrintedCardFactory.TryCreate(card.ObjectId, card.OwnerId ?? "", card.CardNo, out var face)
            ? face.Power
            : card.Power - card.UntilEndOfTurnPowerModifier - TemporaryUnitEffectRules.BaseMightDelta(card);
        var delta = basePower - printed;
        return card with { Power = card.Power + delta, UntilEndOfTurnPowerModifier = card.UntilEndOfTurnPowerModifier + delta };
    }
}

using Riftbound.Contracts;

namespace Riftbound.Engine;

// A receipt belongs to one confirmed play, including all optional costs and reductions.
// Repeated executions are not additional plays and must not duplicate this receipt.
public sealed record CardPlayCostReceipt(int CardMana, int PaidMana);
public sealed record SpellTriggerContext(string CardNo, string Kind, long SourceGeneration,
    CardPlayCostReceipt Cost, string PlayedObjectId, long PlayedGeneration, string PlayedOwnerId, string PlayedCardNo,
    string? BattlefieldId = null);
public sealed record LinkedExileGroup(string SourceObjectId, long SourceGeneration, string ControllerId,
    IReadOnlyDictionary<string, long> Objects);

public sealed partial class CoreRuleEngine
{
    internal const string SpellTriggerEffect = "SPELL_TRIGGER";

    private static StackResolutionResult ResolveStackItemEffect(MatchState state, StackItemState item,
        bool confirmPermanent = false, bool deferCompletion = false,
        HashSet<string>? repeatDamageDestroyTargets = null, bool skipInsight = false)
    {
        var result = ResolveStackItemEffectCore(state, item, confirmPermanent, deferCompletion, repeatDamageDestroyTargets, skipInsight);
        result = FinishRecycledPlays(state, item, result, deferCompletion);
        if (result.CompletedCardPlayIds?.Contains(item.StackItemId) == true || deferCompletion || item.PlayCost is null || result.WinnerPlayerId is not null
            || result.PendingCardChoice is not null || result.PendingEffectPlay is not null
            || result.PendingPayment is not null || result.PendingHandChoice is not null
            || !CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior) || !IsSpellPlayBehavior(behavior)) return result;

        // Capture at completion, before state-based cleanup. A lethal source may still
        // have triggered; its later departure cannot erase the captured ability.
        var completed = state with { PlayerZones = result.PlayerZones, CardObjects = result.CardObjects,
            ObjectLocations = result.ObjectLocations ?? ReconcileObjectLocations(state.ObjectLocations, result.PlayerZones),
            UntilEndOfTurnEffects = result.UntilEndOfTurnEffects };
        var triggers = CaptureSpellTriggers(completed, item, behavior, out var markers);
        return result with { CompletedCardPlayIds = (result.CompletedCardPlayIds ?? []).Append(item.StackItemId).ToArray(),
            TriggerQueue = result.TriggerQueue.Concat(triggers).ToArray(),
            UntilEndOfTurnEffects = markers, Events = result.Events.Concat(triggers.Select(BuildTriggerQueuedEvent)).ToArray() };
    }

    private static TriggerSpec? SpellTriggerSpec(string cardNo, string kind)
    {
        if (SpellPlayedTriggerSpecRules.TryGetTrigger(cardNo, t => t.Kind == kind
                && (SpellPlayedTriggerSpecRules.IsUnitSpellPlayedPowerModifierTrigger(t)
                    || SpellPlayedTriggerSpecRules.IsUnitHighCostSpellPowerModifierTrigger(t)
                    || SpellPlayedTriggerSpecRules.IsLegendHighCostSpellDrawTrigger(t)
                    || SpellPlayedTriggerSpecRules.IsLegendHighCostSpellBanishCompletionTrigger(t)), out var spec)) return spec;
        return BattlefieldTriggerSpecRules.TryGetTrigger(cardNo, t => t.Kind == kind
            && (BattlefieldTriggerSpecRules.IsBattlefieldSpellPowerBonusTrigger(t)
                || BattlefieldTriggerSpecRules.IsBattlefieldFriendlySpellDrawTrigger(t)), out spec) ? spec : null;
    }

    internal static bool ValidSpellContext(SpellTriggerContext context, string effect, string? cardNo = null)
        => effect == SpellTriggerEffect && context.SourceGeneration >= 0 && context.PlayedGeneration >= 0
            && context.Cost is { CardMana: >= 0, PaidMana: >= 0 } && !string.IsNullOrWhiteSpace(context.PlayedObjectId)
            && !string.IsNullOrWhiteSpace(context.PlayedOwnerId) && !string.IsNullOrWhiteSpace(context.PlayedCardNo) && (cardNo is null || cardNo == context.CardNo)
            && SpellTriggerSpec(context.CardNo, context.Kind) is { } spec
            && context.Cost.CardMana >= spec.MinimumCardMana.GetValueOrDefault()
            && context.Cost.PaidMana >= spec.MinimumPaidMana.GetValueOrDefault()
            && (context.Kind is TriggerKinds.BattlefieldSpellPowerBonus or TriggerKinds.BattlefieldFriendlySpellDraw
                ? !string.IsNullOrWhiteSpace(context.BattlefieldId) : context.BattlefieldId is null);

    private static IReadOnlyList<TriggerQueueItemState> CaptureSpellTriggers(MatchState state,
        StackItemState played, CardBehaviorDefinition behavior, out IReadOnlyList<string> markers, bool targetSelection = false)
    {
        markers = state.UntilEndOfTurnEffects;
        if (played.PlayCost is null) return [];
        var queue = targetSelection ? new List<TriggerQueueItemState>() : BuildSpellPlayedInsightTriggers(state, played.ControllerId, behavior, played, played.PlayCost.PaidMana).ToList();
        var effects = state.UntilEndOfTurnEffects.ToList();
        var spell = state.CardObjects.GetValueOrDefault(played.SourceObjectId);
        var owner = spell?.OwnerId ?? played.ControllerId;
        var kinds = new[] { TriggerKinds.UnitSpellPlayedPowerModifier, TriggerKinds.UnitHighCostSpellPowerModifier,
            TriggerKinds.LegendHighCostSpellDrawOne, TriggerKinds.LegendHighCostSpellBanishCompletion,
            TriggerKinds.BattlefieldSpellPowerBonus, TriggerKinds.BattlefieldFriendlySpellDraw };
        foreach (var source in state.CardObjects.Values.OrderBy(c => c.ObjectId, StringComparer.Ordinal))
        {
            if (source.IsFaceDown || source.Tags.Contains(CardObjectTags.Standby)) continue;
            var field = BattlefieldLocalRules.Battlefield(state, source.ObjectId);
            var ownLegend = state.PlayerZones[played.ControllerId].LegendZone.Contains(source.ObjectId)
                && source.ControllerId == played.ControllerId;
            var ownUnit = source.ControllerId == played.ControllerId && IsFaceUpNonStandbyUnit(source)
                && IsObjectOnField(state.PlayerZones, source.ObjectId);
            foreach (var kind in kinds)
            {
                if ((kind == TriggerKinds.BattlefieldFriendlySpellDraw) != targetSelection) continue;
                if (source.CardNo is null || SpellTriggerSpec(source.CardNo, kind) is not { } spec) continue;
                if (kind is TriggerKinds.UnitSpellPlayedPowerModifier or TriggerKinds.UnitHighCostSpellPowerModifier ? !ownUnit
                    : kind is TriggerKinds.LegendHighCostSpellDrawOne or TriggerKinds.LegendHighCostSpellBanishCompletion ? !ownLegend : field is null) continue;
                if (played.PlayCost.CardMana < spec.MinimumCardMana.GetValueOrDefault()
                    || played.PlayCost.PaidMana < spec.MinimumPaidMana.GetValueOrDefault()) continue;
                if (kind == TriggerKinds.LegendHighCostSpellBanishCompletion
                    && (spell is null || !state.PlayerZones[owner].Graveyard.Contains(played.SourceObjectId))) continue;
                if (kind == TriggerKinds.BattlefieldFriendlySpellDraw)
                {
                    if (BattlefieldFriendlySpellDrawUsedThisTurn(state, played.ControllerId, source.ObjectId)
                        || !(played.RepeatExecutions?.SelectMany(e => e.TargetObjectIds) ?? played.TargetObjectIds).Any(id => state.CardObjects.GetValueOrDefault(id)?.ControllerId == played.ControllerId
                            && BattlefieldLocalRules.AtUnit(state, id)?.ObjectId == source.ObjectId)) continue;
                    effects.Add(BuildBattlefieldFriendlySpellDrawUsedEffectId(played.ControllerId, source.ObjectId));
                }
                var context = new SpellTriggerContext(source.CardNo, kind, source.ObjectGeneration,
                    played.PlayCost, played.SourceObjectId, spell?.ObjectGeneration ?? 0, owner, played.CardNo, field?.ObjectId);
                queue.Add(new($"spell-completed-{played.StackItemId}-{source.ObjectId}-{kind}", played.ControllerId,
                    source.ObjectId, SpellTriggerEffect, targetSelection ? "SPELL_TARGET_CHOSEN" : "SPELL_PLAY_COMPLETED", played.TimingContext) { SpellContext = context });
            }
        }
        markers = effects.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return queue;
    }

    private static bool NeedsSpellTriggerChoice(SpellTriggerContext context)
        => context.Kind is TriggerKinds.LegendHighCostSpellBanishCompletion or TriggerKinds.BattlefieldSpellPowerBonus;

    private static string[] SpellTriggerChoices(MatchState state, StackItemState item)
    {
        var context = item.SpellContext!;
        if (context.Kind == TriggerKinds.LegendHighCostSpellBanishCompletion)
            return state.PlayerZones[context.PlayedOwnerId].Graveyard.Contains(context.PlayedObjectId)
                && state.CardObjects.GetValueOrDefault(context.PlayedObjectId)?.ObjectGeneration == context.PlayedGeneration
                    ? [context.PlayedObjectId] : [];
        return state.CardObjects.Values.Where(c => c.ControllerId == item.ControllerId && IsFaceUpNonStandbyUnit(c)
                && BattlefieldLocalRules.AtUnit(state, c.ObjectId)?.ObjectId == context.BattlefieldId
                && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, c.ObjectId))
            .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();
    }

    private static ResolutionResult PrepareSpellTriggerConfirmation(ResolutionResult result)
    {
        var state = result.State;
        var item = state.StackItems.FirstOrDefault(i => i.SpellContext is { } context
            && NeedsSpellTriggerChoice(context) && i.TargetGenerations is null);
        if (item is null) return result;
        var legal = SpellTriggerChoices(state, item);
        if (legal.Length == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var choice = new PendingCardChoiceState("SPELL-TRIGGER:" + item.StackItemId, "SPELL_TRIGGER_CONFIRMATION",
            item.ControllerId, 0, 1, legal, legal,
            item.SpellContext!.Kind == TriggerKinds.LegendHighCostSpellBanishCompletion
                ? "确认放逐刚结算的法术，或不选以放弃；确认后对手可以响应"
                : "选择此处一名友方单位获得本回合 +1 战力，或不选以放弃；确认后对手可以响应",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };
        state = state with { PendingCardChoice = choice, ActivePlayerId = item.ControllerId, PriorityPlayerId = null };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    internal static bool ValidSpellTriggerChoice(MatchState state, PendingCardChoiceState choice)
        => state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId) is { SpellContext: { } context } item
            && ValidSpellContext(context, item.EffectKind, item.CardNo) && NeedsSpellTriggerChoice(context)
            && item.TargetGenerations is null && choice.ChoiceId == "SPELL-TRIGGER:" + item.StackItemId
            && choice.PlayerId == item.ControllerId && choice.SourceObjectId == item.SourceObjectId
            && choice.EffectKind == item.EffectKind && choice.RequiredCount == 0 && choice.MaxCount == 1
            && choice.ContextObjectIds.SequenceEqual(choice.LegalObjectIds)
            && choice.LegalObjectIds.SequenceEqual(SpellTriggerChoices(state, item));

    private static ResolutionResult ResolveSpellTriggerConfirmation(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        if (!ValidSpellTriggerChoice(state, choice)) return RejectWithCorePrompts(state, "触发技能确认已失效。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == choice.ResolvingStackItemId);
        var next = state with { Tick = state.Tick + 1, PendingCardChoice = null };
        if (selected.Count == 0) return DiscardUnconfirmedTrigger(new(true, null, next, [], ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next)), item);
        var confirmed = item with { TargetObjectIds = selected,
            TargetGenerations = selected.ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration) };
        next = next with { StackItems = state.StackItems.Select(i => i.StackItemId == item.StackItemId ? confirmed : i).ToArray(),
            ActivePlayerId = item.ControllerId, PriorityPlayerId = item.ControllerId, PassedPriorityPlayerIds = [] };
        return new(true, null, next, [new("TRIGGER_CONFIRMED", "触发技能已确认，可以响应", new Dictionary<string, object?>
            { ["sourceObjectId"] = item.SourceObjectId, ["effectKind"] = item.EffectKind })], ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveSpellTrigger(MatchState state, StackItemState item)
    {
        var context = item.SpellContext!;
        var spec = SpellTriggerSpec(context.CardNo, context.Kind)!;
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        var result = NoopStackResolutionResult(state);
        if (context.Kind == TriggerKinds.LegendHighCostSpellBanishCompletion)
        {
            var id = context.PlayedObjectId;
            if (item.TargetGenerations is null || !SpellTriggerChoices(state, item).Contains(id)) return result;
            var previous = cards[id];
            zones[context.PlayedOwnerId] = zones[context.PlayedOwnerId] with { Graveyard = zones[context.PlayedOwnerId].Graveyard.Where(x => x != id).ToArray() };
            MoveStackSpellToOwnerZone(zones, cards, item with { SourceObjectId = id, CardNo = previous.CardNo! }, "BANISHED");
            var groups = state.LinkedExiles.Where(g => !(g.SourceObjectId == item.SourceObjectId
                && g.SourceGeneration == context.SourceGeneration && g.ControllerId == item.ControllerId)).ToList();
            var references = state.LinkedExiles.FirstOrDefault(g => g.SourceObjectId == item.SourceObjectId
                && g.SourceGeneration == context.SourceGeneration && g.ControllerId == item.ControllerId)?.Objects
                .Where(e => cards.GetValueOrDefault(e.Key)?.ObjectGeneration == e.Value && zones.Values.Any(z => z.Banished.Contains(e.Key)))
                .ToDictionary(e => e.Key, e => e.Value) ?? new Dictionary<string, long>();
            references[id] = cards[id].ObjectGeneration;
            events.Add(new("CARD_BANISHED", "法术由关联技能放逐", new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id }));
            if (references.Count >= spec.BanishCount)
            {
                foreach (var pair in references)
                {
                    var card = cards[pair.Key]; var owner = card.OwnerId!;
                    zones[owner] = zones[owner] with { Banished = zones[owner].Banished.Where(x => x != pair.Key).ToArray() };
                    MoveStackSpellToOwnerZone(zones, cards, item with { SourceObjectId = pair.Key, CardNo = card.CardNo! }, "GRAVEYARD");
                }
                var called = CallRunes(zones, cards, item.ControllerId, spec.RuneCallCount!.Value);
                events.Add(new("RUNES_CALLED", "关联放逐完成后召出符文", new Dictionary<string, object?>
                    { ["playerId"] = item.ControllerId, ["count"] = called.CalledRuneObjectIds.Count, ["runeObjectIds"] = called.CalledRuneObjectIds.ToArray() }));
                var draw = ApplyDrawToPlayer(state, zones, state.PlayerScores, item.ControllerId, spec.DrawCount!.Value, state.RngCursor, events);
                result = result with { PlayerScores = draw.PlayerScores, WinnerPlayerId = draw.WinnerPlayerId, RngCursor = draw.RngCursor };
            }
            else groups.Add(new(item.SourceObjectId, context.SourceGeneration, item.ControllerId, references));
            result = result with { LinkedExiles = groups };
        }
        else if (spec.PowerDelta is { } amount)
        {
            var id = context.Kind == TriggerKinds.BattlefieldSpellPowerBonus ? item.TargetObjectIds.SingleOrDefault() : item.SourceObjectId;
            if (id is not null && cards.TryGetValue(id, out var card) && IsObjectOnField(zones, id)
                && card.ObjectGeneration == (context.Kind == TriggerKinds.BattlefieldSpellPowerBonus
                    ? item.TargetGenerations?.GetValueOrDefault(id, -1) : context.SourceGeneration)
                && (context.Kind != TriggerKinds.BattlefieldSpellPowerBonus || SpellTriggerChoices(state, item).Contains(id)))
            {
                cards[id] = ApplyDirectUntilEndPowerModifier(card, id, item.SourceObjectId, context.CardNo,
                    spec.Kind, "SPELL_PLAY_COMPLETED", amount, card.Power + amount);
                events.Add(new("POWER_MODIFIED_UNTIL_END_OF_TURN", "施法触发增加本回合战力", new Dictionary<string, object?>
                    { ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id, ["powerDelta"] = amount,
                        ["appliedPowerDelta"] = amount, ["resultingPower"] = cards[id].Power, ["reason"] = context.Kind }));
            }
        }
        else if (spec.DrawCount is > 0)
        {
            var draw = ApplyDrawToPlayer(state, zones, state.PlayerScores, item.ControllerId, spec.DrawCount.Value, state.RngCursor, events);
            result = result with { PlayerScores = draw.PlayerScores, WinnerPlayerId = draw.WinnerPlayerId, RngCursor = draw.RngCursor };
        }
        var payload = new Dictionary<string, object?> { ["playerId"] = item.ControllerId,
            ["sourceObjectId"] = item.SourceObjectId, ["triggerSourceObjectId"] = item.SourceObjectId,
            ["triggerSourceCardNo"] = context.CardNo, ["trigger"] = context.Kind,
            ["playedCardNo"] = context.PlayedCardNo, ["playedSourceObjectId"] = context.PlayedObjectId };
        if (spec.PowerDelta is { } delta) payload["powerDelta"] = delta;
        if (spec.DrawCount is { } count) payload["drawCount"] = count;
        if (context.BattlefieldId is not null) { payload["battlefieldObjectId"] = context.BattlefieldId; payload["battlefieldCardNo"] = context.CardNo; }
        if (item.TargetObjectIds.Count == 1) payload["targetObjectId"] = item.TargetObjectIds[0];
        var eventKind = context.BattlefieldId is not null ? "BATTLEFIELD_TRIGGER_RESOLVED"
            : context.Kind is TriggerKinds.LegendHighCostSpellDrawOne or TriggerKinds.LegendHighCostSpellBanishCompletion ? "LEGEND_TRIGGER_RESOLVED" : "TRIGGER_RESOLVED";
        events.Add(new(eventKind, "施法触发技能结算完成", payload));
        return result with { PlayerZones = zones, CardObjects = cards, Events = events,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }
}

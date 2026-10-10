using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record RecastTriggerContext(string CardNo, string Kind, long SourceGeneration, string BattlefieldId);
public sealed record AfterPlayRecycleInstruction(string SourceObjectId, string CardNo, string TriggerKind, long SourceGeneration);

public sealed partial class CoreRuleEngine
{
    internal const string RecastTriggerEffect = "EFFECT_PLAY_TRIGGER";

    private static TriggerSpec? RecastSpec(string cardNo, string kind)
    {
        if (kind is TriggerKinds.UnitConquestPlayLowCostGraveyardSpellRecycle or TriggerKinds.UnitConquestRecycleFriendlyPlayGraveyardMechanicalUnit
            && UnitConquestTriggerSpecRules.TryGetTrigger(cardNo, t => t.Kind == kind && UnitConquestTriggerSpecRules.IsSupportedUnitConquestTrigger(t), out var conquest)) return conquest;
        return kind != TriggerKinds.SourceUnitPlayedPlayLowCostGraveyardSpellRecycle ? null : SourceUnitPlayedTriggerSpecRules.TriggersForCard(cardNo).FirstOrDefault(t => t.Kind == kind
            && SourceUnitPlayedTriggerSpecRules.IsSupportedSourceUnitPlayedTrigger(t));
    }

    private static TriggerSpec? RecastSpec(StackItemState parent)
        => parent.RecastContext is { } context ? RecastSpec(context.CardNo, context.Kind)
            : parent.SourceConfirmed && CardBehaviorRegistry.TryGetByEffectKind(parent.EffectKind, out var unit)
                && unit.PlaysSourceToBaseAsUnit && unit.CardNo == parent.CardNo
                    ? RecastSpec(parent.CardNo, TriggerKinds.SourceUnitPlayedPlayLowCostGraveyardSpellRecycle) : null;

    internal static bool ValidRecastContext(RecastTriggerContext context, string effect, string? cardNo = null)
        => effect == RecastTriggerEffect && context.SourceGeneration >= 0 && !string.IsNullOrWhiteSpace(context.BattlefieldId) && (cardNo is null || context.CardNo == cardNo)
            && (context.Kind is TriggerKinds.UnitConquestPlayLowCostGraveyardSpellRecycle or TriggerKinds.UnitConquestRecycleFriendlyPlayGraveyardMechanicalUnit) && RecastSpec(context.CardNo, context.Kind) is not null;

    internal static bool EffectPlayAllowsBehavior(MatchState state, string player, CardBehaviorDefinition behavior)
        => state.PendingEffectPlay is { } p && p.PlayerId == player
            && (TryGetEffectPlayDefinition(p.Parent, out var definition) && definition.EffectPlayAllowsAnyCard
                ? behavior.PlaysSourceToBaseAsUnit || behavior.PlaysSourceToBaseAsEquipment || IsSpellPlayBehavior(behavior)
                : p.DestinationPolicy == "STACK" ? IsSpellPlayBehavior(behavior) : behavior.PlaysSourceToBaseAsUnit);

    private static bool RecastSourceAllowed(MatchState state, string player, CardObjectState card, TriggerSpec spec)
        => spec.Kind == TriggerKinds.UnitConquestRecycleFriendlyPlayGraveyardMechanicalUnit
            ? card.ControllerId == player && card.Tags.Contains(CardObjectTags.UnitCard) && card.Tags.Contains("机械")
                && CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var unit) && unit.PlaysSourceToBaseAsUnit
            : card.ControllerId == player && card.Tags.Contains(CardObjectTags.SpellCard)
            && CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var spell) && IsSpellPlayBehavior(spell)
            && (spec.MaximumPlayedCardManaCost is not { } max || EffectiveCardManaCost(card, spell) <= max)
            && (spec.RequiresPlayedCardManaCostLessThanCurrentScore != true || EffectiveCardManaCost(card, spell) < state.PlayerScores.GetValueOrDefault(player));

    private static IReadOnlyList<TriggerQueueItemState> CaptureConquestRecasts(MatchState state, string player,
        string battlefield, IReadOnlyList<string> units, long tick)
    {
        var queue = new List<TriggerQueueItemState>();
        foreach (var id in units.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var source) || source.ControllerId != player
                || BattlefieldLocalRules.AtUnit(state, id)?.ObjectId != battlefield || source.CardNo is null
                || (RecastSpec(source.CardNo, TriggerKinds.UnitConquestPlayLowCostGraveyardSpellRecycle)
                    ?? RecastSpec(source.CardNo, TriggerKinds.UnitConquestRecycleFriendlyPlayGraveyardMechanicalUnit)) is not { } spec) continue;
            var repeats = ResolveUnitConquestEffectRepeatCount(state.PlayerZones, state.CardObjects, state.ObjectLocations, player, battlefield, "BATTLEFIELD_CONQUERED");
            for (var i = 0; i < repeats; i++) queue.Add(new($"recast-{tick}-{battlefield}-{id}-{i}", player, id,
                RecastTriggerEffect, "BATTLEFIELD_CONQUERED", TimingStates.NeutralOpen)
                { RecastContext = new(source.CardNo, spec.Kind, source.ObjectGeneration, battlefield) });
        }
        return queue;
    }

    private static StackResolutionResult BeginRecastPlay(MatchState state, StackItemState parent)
    {
        var spec = RecastSpec(parent)!;
        var mechanical = spec.Kind == TriggerKinds.UnitConquestRecycleFriendlyPlayGraveyardMechanicalUnit;
        if (mechanical && parent.RecycledUnit is null) return BeginRecyclingChoice(state, parent);
        var sources = state.PlayerZones[parent.ControllerId].Graveyard
            .Where(id => state.CardObjects.TryGetValue(id, out var card) && RecastSourceAllowed(state, parent.ControllerId, card, spec))
            .ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration);
        // CN 419.3.c: no eligible card means there is no play to choose.
        if (sources.Count == 0) return ResolveStackItemEffect(state, parent with { EffectPlayCompleted = true });
        var pending = new PendingEffectPlayState($"EFFECT-PLAY:{state.Tick + 1}:{parent.StackItemId}", parent.ControllerId, parent,
            "GRAVEYARD", sources, !mechanical, false, parent.RecycledUnit?.Power ?? 0, mechanical ? "BASE" : "STACK", !mechanical);
        return NoopStackResolutionResult(state) with { PendingEffectPlay = pending };
    }

    private static AfterPlayRecycleInstruction? RecycleInstructionFor(MatchState state)
    {
        if (state.PendingEffectPlay is not { } pending || RecastSpec(pending.Parent) is not { } spec
            || spec.Kind == TriggerKinds.UnitConquestRecycleFriendlyPlayGraveyardMechanicalUnit) return null;
        return new(pending.Parent.SourceObjectId, pending.Parent.CardNo, spec.Kind,
            pending.Parent.RecastContext?.SourceGeneration ?? state.CardObjects.GetValueOrDefault(pending.Parent.SourceObjectId)?.ObjectGeneration ?? 0);
    }

    // Resolve and counter paths both leave the chain. Apply the originating instruction
    // before completed-play observers (Jhin), with one owner-correct zone transition.
    private static StackResolutionResult FinishRecycledPlays(MatchState before, StackItemState item,
        StackResolutionResult result, bool deferred)
    {
        var exited = before.StackItems.Where(s => result.CounteredStackItemIds.Contains(s.StackItemId)).ToList();
        if (!deferred && result.CompletedCardPlayIds?.Contains(item.StackItemId) != true && item.PlayCost is not null
            && result.PendingCardChoice is null && result.PendingEffectPlay is null && result.PendingPayment is null && result.PendingHandChoice is null)
            exited.Add(item);
        var marked = exited.DistinctBy(s => s.StackItemId).Where(s => s.AfterPlayRecycle is not null).ToArray();
        if (marked.Length == 0) return result;
        var zones = result.PlayerZones.ToDictionary(x => x.Key, x => x.Value);
        var cards = result.CardObjects.ToDictionary(x => x.Key, x => x.Value);
        var events = result.Events.ToList();
        foreach (var played in marked)
        {
            var instruction = played.AfterPlayRecycle!;
            if (!cards.TryGetValue(played.SourceObjectId, out var card)) continue;
            var owner = NonFieldDestinationOwner(zones, card, played.ControllerId);
            var own = zones[owner];
            if (!own.Graveyard.Contains(card.ObjectId) && !own.Banished.Contains(card.ObjectId) && !own.Hand.Contains(card.ObjectId)) continue;
            zones[owner] = own with { Graveyard = RemoveFromZone(own.Graveyard, card.ObjectId),
                Banished = RemoveFromZone(own.Banished, card.ObjectId), Hand = RemoveFromZone(own.Hand, card.ObjectId),
                MainDeck = own.MainDeck.Append(card.ObjectId).Distinct(StringComparer.Ordinal).ToArray() };
            ResetCardOutsidePlay(zones, cards, card.ObjectId, card, owner);
            events.Add(new("CARDS_RECYCLED", "按再次打出效果回收法术", new Dictionary<string, object?>
                { ["playerId"] = owner, ["sourceObjectId"] = instruction.SourceObjectId,
                    ["cardIds"] = new[] { card.ObjectId }, ["count"] = 1, ["reason"] = instruction.TriggerKind,
                    ["destinationZone"] = "MAIN_DECK" }));
        }
        return result with { PlayerZones = zones, CardObjects = cards, Events = events,
            ObjectLocations = ReconcileObjectLocations(result.ObjectLocations ?? before.ObjectLocations, zones) };
    }

    private static IReadOnlyList<GameEvent> RecastPlayedEvents(MatchState state, PendingEffectPlayState pending, PlayCardCommand command)
    {
        if (RecastSpec(pending.Parent) is not { } spec) return [];
        if (pending.Parent.RecycledUnit is not null) return [];
        var sourcePlay = spec.Kind == TriggerKinds.SourceUnitPlayedPlayLowCostGraveyardSpellRecycle;
        var card = state.CardObjects[command.SourceObjectId];
        var payload = new Dictionary<string, object?> { ["playerId"] = pending.PlayerId,
            ["sourceObjectId"] = pending.Parent.SourceObjectId, ["sourceCardNo"] = pending.Parent.CardNo,
            ["unitCardNo"] = pending.Parent.CardNo, ["effectId"] = spec.Kind, ["targetObjectId"] = command.SourceObjectId,
            ["reason"] = sourcePlay ? TriggerTimings.SourceUnitPlayed : "BATTLEFIELD_CONQUERED" };
        if (pending.Parent.RecastContext is { } context) payload["battlefieldObjectId"] = context.BattlefieldId;
        var play = new Dictionary<string, object?>(payload) { ["playedObjectId"] = command.SourceObjectId,
            ["playedCardNo"] = command.CardNo, ["playedCardManaCost"] = card.ManaCost,
            ["sourceZone"] = "GRAVEYARD", ["destinationZone"] = "STACK", ["ignorePlayManaCost"] = true, ["payPlayPowerCosts"] = true };
        if (command.TargetObjectIds.Count > 0) play["targetObjectIds"] = command.TargetObjectIds.ToArray();
        return [new(sourcePlay ? "SOURCE_UNIT_PLAYED_EFFECT_ACTIVATED" : "UNIT_CONQUEST_EFFECT_ACTIVATED", "已选择再次打出的法术", payload),
            new("CARD_PLAYED_FROM_GRAVEYARD", "从废牌堆正式打出法术，等待响应", play)];
    }

    internal static bool ValidRecastPending(MatchState state, PendingEffectPlayState pending)
        => RecastSpec(pending.Parent) is not null && pending.PlayerId == pending.Parent.ControllerId
            && state.Seats.ContainsKey(pending.PlayerId) && state.PlayerZones.TryGetValue(pending.PlayerId, out var zones)
            && (pending.Parent.RecastContext is not { } context || ValidRecastContext(context, pending.Parent.EffectKind, pending.Parent.CardNo))
            && (pending.Parent.RecastContext?.Kind == TriggerKinds.UnitConquestRecycleFriendlyPlayGraveyardMechanicalUnit) == (pending.Parent.RecycledUnit is not null)
            && pending.SourceZone == "GRAVEYARD" && !pending.IgnoreBasePower
            && (pending.Parent.RecycledUnit is { } recycled
                ? ValidRecycledUnit(pending.Parent) && pending.DestinationPolicy == "BASE" && !pending.IgnoreBaseMana
                    && pending.ManaReduction == recycled.Power && !pending.Optional
                : pending.DestinationPolicy == "STACK" && pending.IgnoreBaseMana && pending.ManaReduction == 0 && pending.Optional)
            && pending.ViewedCardIds is null && !pending.Parent.EffectPlayCompleted
            && pending.Sources.All(s => zones.Graveyard.Contains(s.Key)
                && state.CardObjects.TryGetValue(s.Key, out var card) && card.ObjectGeneration == s.Value);

    internal static bool ValidRecycleInstruction(AfterPlayRecycleInstruction instruction)
        => !string.IsNullOrWhiteSpace(instruction.SourceObjectId) && instruction.SourceGeneration >= 0
            && instruction.TriggerKind != TriggerKinds.UnitConquestRecycleFriendlyPlayGraveyardMechanicalUnit
            && RecastSpec(instruction.CardNo, instruction.TriggerKind) is not null;
}

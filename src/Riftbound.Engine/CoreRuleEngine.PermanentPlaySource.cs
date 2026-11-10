using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // The resolved play receipt already captures the source's entry generation.
    // Reuse it without exposing a link to a later private-zone incarnation.
    internal static ObjectBinding? PlayInstructionSource(MatchState state, StackItemState item)
        => state.CardPlays.Resolved.FirstOrDefault(play => play.PlayId == item.StackItemId
            && play.CardNo == item.CardNo && play.PlayerId == item.ControllerId
            && play.Source.ObjectId == item.SourceObjectId)?.Source;

    // CN 383.2.a.1: the paid optional cost is part of the trigger condition.
    // Use the same declaration predicate for capture, resolution and recovery.
    internal static bool SourceOptionalReadyPowerCostWasPaid(CardBehaviorDefinition behavior, StackItemState item)
    {
        var requiredTrait = RuneTrait.Normalize(behavior.SourceReadyPowerModifierAdditionalPowerTrait);
        return behavior.SourceReadyPowerModifierAdditionalPowerCost > 0
            && behavior.SourceReadyPowerModifierAmount != 0 && !string.IsNullOrWhiteSpace(requiredTrait)
            && item.OptionalCosts.Any(cost => TryParseSpendPowerOptionalCost(cost, out var amount, out var trait)
                && amount == behavior.SourceReadyPowerModifierAdditionalPowerCost
                && RuneTrait.Normalize(trait) == requiredTrait);
    }

    internal static ContinuousEffectState PublicPlayInstructionEffect(MatchState state, ContinuousEffectState effect, string viewer)
    {
        // Keep the full provenance in the authoritative ledger. A resolved
        // skill must not disclose its source's later private incarnation.
        if (effect.SourceObjectId is not { } id
            || ResolutionResult.VisibleObjectIdForViewer(state, id, viewer) != "HIDDEN") return effect;
        var capturedActivation = P4ActivatedAbilityCatalog.TryGetByEffectKind(effect.EffectKind, out var ability)
            && ability.SimpleEffect is not null;
        var capturedBattlefieldSkill = effect.EffectKind == UnitEventEffect
            && (UnitEventTimingMatches(UnitEventSpec(effect.SourceCardNo)?.Timing, "CONQUEST")
                || UnitEventTimingMatches(UnitEventSpec(effect.SourceCardNo)?.Timing, "HOLD"));
        var capturedGroupInstruction = effect.EffectKind == UnitEventEffect
            && UnitEventSpec(effect.SourceCardNo)?.Effect == "MIGHT_OTHER_FRIENDLIES";
        if (effect.EffectKind != BorrowedExhaustRules.Effect && !capturedActivation && !capturedBattlefieldSkill && !capturedGroupInstruction
            && effect.EffectKind is not (TriggerKinds.UnitMovedOtherFriendlyPowerModifier or TriggerKinds.UnitMovedGrantSourceKeywordsAndPower)
            && (effect.SourceCardNo is not { } no || !CardBehaviorRegistry.TryGetByCardNo(no, out var behavior)
                || !behavior.RecyclesSourceIfPlayedFromStandby)) return effect;
        return effect with { SourceObjectId = "HIDDEN" };
    }

    private static void ResolvePermanentPlaySourceInstructions(MatchState state, StackItemState item,
        CardBehaviorDefinition behavior, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, List<GameEvent> events)
    {
        // A printed instruction referring to "me" keeps the object that entered,
        // even when a response returns and replays the physical card (CN 124).
        if (!item.SourceConfirmed || PlayInstructionSource(state, item) is not { } source
            || !cards.TryGetValue(source.ObjectId, out var card)
            || card.ObjectGeneration != source.Generation || !IsFaceUpNonStandbyUnit(card)
            || !IsObjectOnField(zones, source.ObjectId)) return;
        if (behavior.AddsRegisteredLegendWhileSourceOnField)
            AddRegisteredLegend(state, item, source, zones, cards, events);
        if (behavior.GrantsSourceEphemeralIfOpponentTurn && state.TurnPlayerId != item.ControllerId)
        {
            cards[source.ObjectId] = card with {
                Tags = card.Tags.Append(CardObjectTags.Ephemeral).Distinct(StringComparer.Ordinal).ToArray(),
                GrantedTags = (card.GrantedTags ?? []).Append(CardObjectTags.Ephemeral).Distinct(StringComparer.Ordinal).ToArray() };
            events.Add(new("KEYWORD_GRANTED", "打出技能给予来源瞬息", new Dictionary<string, object?> {
                ["sourceObjectId"] = source.ObjectId, ["targetObjectId"] = source.ObjectId,
                ["keyword"] = CardObjectTags.Ephemeral }));
        }
        if (behavior.RecyclesSourceIfPlayedFromStandby && item.PlayedFromStandby is not null
            && TryMoveTargetToOwnerMainDeck(state, zones, cards, source.ObjectId, "BOTTOM", out var owner, out _))
            events.Add(CaptureRecycleObservers(new("CARDS_RECYCLED", "打出技能回收来源单位", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["ownerPlayerId"] = owner,
                ["sourceObjectId"] = source.ObjectId, ["cardIds"] = new[] { source.ObjectId },
                ["count"] = 1, ["destinationZone"] = "MAIN_DECK" }), zones, cards));
    }
}

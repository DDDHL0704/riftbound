using Riftbound.Contracts;

namespace Riftbound.Engine;

// Private, serialized continuation. The client receives a separate safe projection.
public sealed record PendingEffectPlayState(
    string ChoiceId, string PlayerId, StackItemState Parent, string SourceZone,
    IReadOnlyDictionary<string, long> Sources, bool IgnoreBaseMana, bool IgnoreBasePower,
    int ManaReduction, string DestinationPolicy, bool Optional, IReadOnlyList<string>? ViewedCardIds = null,
    bool IgnoreAllCosts = false, RevealedHandPlayContext? RevealedHand = null);

public sealed partial class CoreRuleEngine
{
    internal static bool TryGetEffectPlayDefinition(StackItemState parent, out CardBehaviorDefinition definition)
    {
        if (parent.LegendConquest is { Kind: "EXHAUST_DECK_PLAY" } context
            && ValidLegendConquest(context, parent.EffectKind, parent.CardNo)) {
            definition = new(parent.CardNo, "虚空遁地兽", 0, parent.EffectKind, 0, 0,
                MainDeckLookCount: LegendConquestDefinition(parent.CardNo)!.Value.Spec.RevealCount!.Value,
                EffectPlaySourceZone: "MAIN_DECK", EffectPlayOptional: true,
                EffectPlayAllowsAnyCard: true, EffectPlayRevealsCards: true);
            return true;
        }
        return CardBehaviorRegistry.TryGetByEffectKind(parent.EffectKind, out definition!);
    }

    private static bool EffectPlaySourceMatches(CardObjectState card, CardBehaviorDefinition definition)
        => CardBehaviorRegistry.GetAll().Any(b => b.CardNo == card.CardNo && (definition.EffectPlayAllowsAnyCard
            ? b.PlaysSourceToBaseAsUnit || b.PlaysSourceToBaseAsEquipment || IsSpellPlayBehavior(b)
            : b.PlaysSourceToBaseAsUnit));

    internal static bool IsPubliclyRevealedDeckCard(MatchState state, string id)
        => state.PendingEffectPlay is { SourceZone: "MAIN_DECK", ViewedCardIds: { } viewed } pending
            && TryGetEffectPlayDefinition(pending.Parent, out var definition) && definition.EffectPlayRevealsCards
            && viewed.Contains(id) && state.PlayerZones[pending.PlayerId].MainDeck.Contains(id);

    internal static bool ValidDeckEffectPlay(MatchState state, PendingEffectPlayState pending)
        => TryGetEffectPlayDefinition(pending.Parent, out var definition) && definition.CardNo == pending.Parent.CardNo
            && definition.EffectPlaySourceZone == "MAIN_DECK" && pending.SourceZone == "MAIN_DECK"
            && pending.PlayerId == pending.Parent.ControllerId && !pending.Parent.EffectPlayCompleted
            && (pending.Parent.LegendConquest is null || pending.Parent.TriggerCost is not null && ValidTriggerCostReceipt(state, pending.Parent))
            && state.PlayerZones.TryGetValue(pending.PlayerId, out var zones) && pending.ViewedCardIds is { } viewed
            && zones.MainDeck.Take(definition.MainDeckLookCount).SequenceEqual(viewed)
            && pending.ManaReduction == definition.EffectPlayManaReduction
            && pending.IgnoreBaseMana == definition.EffectPlayIgnoreBaseMana && pending.IgnoreBasePower == definition.EffectPlayIgnoreBasePower
            && pending.Optional == definition.EffectPlayOptional && pending.DestinationPolicy == definition.EffectPlayDestination
            && !pending.IgnoreAllCosts && pending.RevealedHand is null
            && pending.Sources.Keys.Order().SequenceEqual(viewed.Where(id => state.CardObjects.TryGetValue(id, out var c) && EffectPlaySourceMatches(c, definition)).Order())
            && pending.Sources.All(s => state.CardObjects[s.Key].ObjectGeneration == s.Value);

    internal static IReadOnlyList<string> EffectPlaySources(MatchState state, string playerId)
    {
        if (state.PendingEffectPlay is not { } pending)
            return state.PlayerZones.TryGetValue(playerId, out var handZones)
                ? handZones.Hand.Concat(handZones.ChampionZone).Distinct(StringComparer.Ordinal).ToArray() : [];
        if (pending.RevealedHand is { Choosing: true }) return [];
        if (pending.PlayerId != playerId || !state.PlayerZones.TryGetValue(playerId, out var zones)) return [];
        var sourceZone = pending.SourceZone switch { "HAND" => zones.Hand, "GRAVEYARD" => zones.Graveyard, "BANISHED" => zones.Banished, "MAIN_DECK" => zones.MainDeck, _ => [] };
        return pending.Sources.Where(x => sourceZone.Contains(x.Key, StringComparer.Ordinal)
            && state.CardObjects.TryGetValue(x.Key, out var card) && card.ObjectGeneration == x.Value
            && (RecastSpec(pending.Parent) is not { } spec || RecastSourceAllowed(state, playerId, card, spec)))
            .Select(x => x.Key).ToArray();
    }

    // Restore the same policy selected by the registered effect, never client-supplied waivers.
    internal static bool ValidGraveyardUnitEffectPlay(MatchState state, PendingEffectPlayState pending)
        => CardBehaviorRegistry.TryGetByEffectKind(pending.Parent.EffectKind, out var behavior)
            && behavior.CardNo == pending.Parent.CardNo && behavior.EffectPlaySourceZone == "GRAVEYARD"
            && pending.SourceZone == behavior.EffectPlaySourceZone && pending.PlayerId == pending.Parent.ControllerId
            && state.Seats.ContainsKey(pending.PlayerId) && state.PlayerZones.TryGetValue(pending.PlayerId, out var zones)
            && pending.IgnoreBaseMana == behavior.EffectPlayIgnoreBaseMana
            && pending.IgnoreBasePower == behavior.EffectPlayIgnoreBasePower
            && pending.ManaReduction == behavior.EffectPlayManaReduction
            && pending.DestinationPolicy == behavior.EffectPlayDestination && pending.Optional == behavior.EffectPlayOptional
            && pending.ViewedCardIds is null && !pending.Parent.EffectPlayCompleted
            && (!behavior.PlaysSourceToBaseAsUnit || pending.Parent.SourceConfirmed)
            && pending.Sources.Count > 0
            && pending.Sources.All(s => pending.Parent.TargetObjectIds.Contains(s.Key)
                && zones.Graveyard.Contains(s.Key) && state.CardObjects.TryGetValue(s.Key, out var card)
                && card.ObjectGeneration == s.Value && card.Tags.Contains(CardObjectTags.UnitCard)
                && CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var unit) && unit.PlaysSourceToBaseAsUnit
                && IsTargetManaCostAllowed(state, pending.PlayerId, s.Key, behavior));

    internal static CardBehaviorDefinition EffectPlayBehavior(MatchState state, string playerId, CardBehaviorDefinition behavior)
        => state.PendingEffectPlay is { } pending && pending.PlayerId == playerId
            ? pending.IgnoreAllCosts ? behavior with {
                IgnorePrintedPowerCost = true,
                RequiresDestroyFriendlyUnitAdditionalCost = false,
                RequiresDestroyFriendlyPowerfulUnitAdditionalCost = false,
                RequiresDestroyFriendlyTraitUnitAdditionalCost = false,
                RequiresReturnFriendlyEquipmentAdditionalCost = false
            } : behavior with { IgnorePrintedPowerCost = pending.IgnoreBasePower } : behavior;

    internal static IReadOnlyList<string> EffectPlayDestinations(MatchState state, string playerId)
    {
        var pending = state.PendingEffectPlay;
        if (pending is null || pending.PlayerId != playerId) return [];
        if (pending.RevealedHand is { } hand)
            return !hand.Choosing && IsForcedEffectPlayDestination(state, playerId, "BATTLEFIELD:" + hand.BattlefieldId)
                && !HasBattlefieldStaticPreventUnitPlayToBattlefield(state, playerId, "BATTLEFIELD:" + hand.BattlefieldId)
                ? ["BATTLEFIELD:" + hand.BattlefieldId] : [];
        if (pending.DestinationPolicy == "STACK") return ["BASE"];
        var destinations = new List<string>();
        if (pending.DestinationPolicy != "CONTROLLED_BATTLEFIELD") destinations.Add("BASE");
        if (pending.DestinationPolicy != "BASE")
            destinations.AddRange(state.CardObjects.Keys.Select(id => "BATTLEFIELD:" + id)
                .Where(destination => IsPlayCardUnitBattlefieldDestinationAllowed(state, playerId, destination)
                    && !HasBattlefieldStaticPreventUnitPlayToBattlefield(state, playerId, destination)));
        return destinations;
    }

    internal static IReadOnlyDictionary<string, object?>? EffectPlayView(MatchState state)
        => state.PendingEffectPlay is not { } p ? null : new Dictionary<string, object?> {
            ["choiceId"] = p.ChoiceId, ["playerId"] = p.PlayerId,
            ["sourceObjectId"] = p.Parent.SourceObjectId, ["sourceCardNo"] = p.Parent.CardNo,
            ["optional"] = p.Optional, ["step"] = p.RevealedHand is { Choosing: true } ? "REVEALED_HAND_CHOICE" : "PLAY_CARD",
            ["reason"] = EffectPlayReason(p),
            ["revealedCards"] = p.ViewedCardIds?.Where(id => IsPubliclyRevealedDeckCard(state, id))
                .Select(id => new { objectId = id, cardNo = state.CardObjects[id].CardNo }).ToArray() };

    internal static IReadOnlyDictionary<string, ActionPromptDto> BuildEffectPlayPrompts(MatchState state)
    {
        var p = state.PendingEffectPlay!;
        if (p.RevealedHand is { Choosing: true }) return BuildRevealedHandPlayPrompts(state, p);
        var own = ActionPromptBuilder.Build(state, p.PlayerId, true,
            EffectPlayReason(p), p.IgnoreAllCosts ? [CommandTypes.PlayCard, CommandTypes.Surrender]
                : [CommandTypes.PlayCard, CommandTypes.ActivateAbility, CommandTypes.TapRune, CommandTypes.RecycleRune, CommandTypes.Surrender]);
        if (p.ViewedCardIds is not null)
            own = own with { Candidates = own.Candidates!.Select(candidate => candidate.Action != CommandTypes.PlayCard ? candidate
                : candidate with { Metadata = new Dictionary<string, object?>(candidate.Metadata!)
                    { ["viewedCardsPublic"] = TryGetEffectPlayDefinition(p.Parent, out var definition) && definition.EffectPlayRevealsCards,
                      ["viewedCards"] = p.ViewedCardIds.Select(id => new ActionPromptChoiceDto(id, DeckChoiceLabel(state, id), IsPubliclyRevealedDeckCard(state, id) ? "已公开展示" : "仅你可见")).ToArray(),
                      ["reason"] = EffectPlayReason(p) } }).ToArray() };
        // A decline is a zero-card choice, using the existing native card-choice composer.
        if (CanFinishWithoutEffectPlay(state))
        {
            var choiceState = state with { PendingCardChoice = new(p.ChoiceId, "EFFECT_PLAY", p.PlayerId,
                0, 0, [], [], p.Optional ? "放弃此次再次打出" : "当前没有合法来源或目的地，继续结算", p.Parent.SourceObjectId, p.Parent.EffectKind) };
            var decline = ActionPromptBuilder.Build(choiceState, p.PlayerId, true, "继续结算", [CommandTypes.ChooseCards]).Candidates!.Single();
            decline = decline with { Label = p.Optional ? "放弃再次打出" : "继续结算", Enabled = true,
                SelectionSteps = [], CommandTemplate = new(CommandTypes.ChooseCards, [
                    new("choiceId", "candidateMetadata", Required: true, MetadataKey: "choiceId"),
                    new("choiceWindow", "candidateMetadata", Required: true, MetadataKey: "choiceWindow"),
                    new("chosenObjectIds", "selectedTargets", AsArray: true, OmitEmpty: false)]) };
            own = own with { Actions = own.Actions.Concat([CommandTypes.ChooseCards]).ToArray(),
                Candidates = own.Candidates!.Concat([decline]).ToArray() };
        }
        return state.Seats.Keys.ToDictionary(id => id, id => id == p.PlayerId ? own
            : ActionPromptBuilder.Build(state, id, false, "等待对手完成效果要求的再次打出" + (p.ViewedCardIds is { } shown && shown.Any(id => IsPubliclyRevealedDeckCard(state, id))
                ? "；已公开展示：" + string.Join("、", shown.Select(id => DeckChoiceLabel(state, id))) : ""), ["WAIT", CommandTypes.Surrender]));
    }

    internal static string EffectPlayReason(PendingEffectPlayState pending)
    {
        var name = TryGetEffectPlayDefinition(pending.Parent, out var card) || CardBehaviorRegistry.TryGetByCardNo(pending.Parent.CardNo, out card) ? card.DisplayName : "卡牌效果";
        var cost = pending.IgnoreAllCosts ? "忽略一切费用；不能支付额外费用；必须打到指定战场"
            : pending.IgnoreBasePower ? "忽略基础法力与符能，额外费用仍需支付"
            : pending.IgnoreBaseMana ? "忽略基础法力，仍需支付符能与额外费用" : pending.ManaReduction > 0 ? $"费用减少 {pending.ManaReduction}，仍需支付符能" : "正常支付法力、符能及额外费用";
        return $"《{name}》要求再次打出 · {cost}";
    }

    private static bool CanFinishWithoutEffectPlay(MatchState state)
    {
        var p = state.PendingEffectPlay!;
        if (p.Optional || EffectPlayDestinations(state, p.PlayerId).Count == 0) return true;
        var candidates = ActionPromptBuilder.Build(state, p.PlayerId, true, "再次打出", [CommandTypes.PlayCard]);
        if ((candidates.Candidates?.FirstOrDefault()?.Sources?.Count ?? 0) == 0) return true;
        // Only offer an impossible-payment continuation when even an upper bound
        // using every legal rune and temporary resource cannot cover the minimum.
        var pool = state.RunePools.GetValueOrDefault(p.PlayerId) ?? RunePool.Empty;
        var mana = pool.Mana;
        var traits = pool.PowerByTrait.ToDictionary(x => x.Key, x => x.Value);
        foreach (var id in state.PlayerZones[p.PlayerId].Base)
            if (state.CardObjects.TryGetValue(id, out var rune) && !rune.IsFaceDown
                && rune.Tags.Contains(CardObjectTags.RuneCard) && SourceObjectControlledByPlayerOrLegacyOwned(rune, p.PlayerId))
            {
                if (!rune.IsExhausted) mana++;
                if (TryGetRuneTrait(rune, out var trait)) traits[trait] = traits.GetValueOrDefault(trait) + 1;
            }
        // Temporary resources can have restrictions. Counting all is deliberately
        // generous: this proof must never declare a payable play impossible.
        var temporary = state.TemporaryPaymentResources.Where(x => x.OwnerPlayerId == p.PlayerId).ToArray();
        var available = pool with { Mana = mana, Power = pool.Power + temporary.Sum(x => x.RemainingPower),
            PowerByTrait = temporary.Aggregate((IReadOnlyDictionary<string, int>)traits, (a, x) => PrintedPowerCostRules.Combine(a, x.RemainingPowerByTrait)) };
        return EffectPlaySources(state, p.PlayerId).All(id => {
            var card = state.CardObjects[id];
            return !CardBehaviorRegistry.GetAll().Where(b => b.CardNo == card.CardNo && EffectPlayAllowsBehavior(state, p.PlayerId, b)).Any(b => {
                var behavior = EffectPlayBehavior(state, p.PlayerId, b);
                PrintedPowerCostRules.TrySelect(behavior.IgnorePrintedPowerCost ? "" : b.CardNo, null, available, 0,
                    new Dictionary<string, int>(), out var generic, out var typed);
                return MinimumPlayManaCost(state, p.PlayerId, behavior, id).Total <= available.Mana
                    && PaymentCostRules.PowerDeficit(available, generic, typed) == 0;
            });
        });
    }

    private static StackResolutionResult BeginEffectPlay(MatchState state, StackItemState parent, CardBehaviorDefinition behavior)
    {
        if (behavior.EffectPlaySourceZone == "OPPONENT_HAND") return BeginRevealedHandPlay(state, parent, behavior);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        var actor = parent.ControllerId;
        var sourceZone = behavior.EffectPlaySourceZone;
        var viewed = sourceZone == "MAIN_DECK" ? zones[actor].MainDeck.Take(behavior.MainDeckLookCount).ToArray() : null;
        if (viewed is { Length: 0 }) return ResolveStackItemEffect(state, parent with { EffectPlayCompleted = true });
        IReadOnlyList<string> sources = viewed ?? (sourceZone == "HAND" ? zones[actor].Hand : parent.TargetObjectIds.Where(id => !string.IsNullOrEmpty(id)).ToArray());
        if (sourceZone == "BANISHED")
        {
            sources = sources.Take(1).ToArray();
            if (sources.Count > 0 && cards.TryGetValue(sources[0], out var target))
            {
                var id = sources[0];
                actor = NonFieldDestinationOwner(zones, target, parent.ControllerId);
                DetachEquipmentFromRemovedHost(cards, id);
                foreach (var owner in zones.Keys.ToArray()) zones[owner] = zones[owner] with {
                    Base = RemoveFromZone(zones[owner].Base, id), Battlefields = RemoveFromZone(zones[owner].Battlefields, id) };
                zones[actor] = zones[actor] with { Banished = zones[actor].Banished.Concat([id]).Distinct().ToArray() };
                if (PrintedCardFactory.TryRestoreOutsidePlay(target, actor, out var printed)) cards[id] = printed with { ObjectGeneration = target.ObjectGeneration + 1 };
                else { cards.Remove(id); sources = []; } // A token ceases to exist outside play.
                events.Add(new("UNIT_BANISHED", "单位被放逐，等待其拥有者再次打出", new Dictionary<string, object?> {
                    ["sourceObjectId"] = parent.SourceObjectId, ["targetObjectId"] = id, ["ownerPlayerId"] = actor, ["destinationZone"] = "BANISHED" }));
            }
        }
        sources = sources.Where(id => cards.TryGetValue(id, out var card) && EffectPlaySourceMatches(card, behavior)).ToArray();
        if (viewed is { Length: > 0 } && behavior.EffectPlayRevealsCards)
            events.Add(new("CARDS_REVEALED", "展示主牌堆顶的卡牌，等待选择打出或放弃", new Dictionary<string, object?> {
                ["playerId"] = actor, ["sourceObjectId"] = parent.SourceObjectId, ["count"] = viewed.Length,
                ["cards"] = viewed.Select(id => new { objectId = id, cardNo = cards[id].CardNo }).ToArray() }));
        if (sources.Count == 0 && events.Count == 0 && viewed is null)
            return ResolveStackItemEffect(state, parent with { EffectPlayCompleted = true });
        var pending = new PendingEffectPlayState($"EFFECT-PLAY:{state.Tick + 1}:{parent.StackItemId}", actor, parent,
            sourceZone, sources.ToDictionary(id => id, id => cards[id].ObjectGeneration, StringComparer.Ordinal),
            behavior.EffectPlayIgnoreBaseMana, behavior.EffectPlayIgnoreBasePower, behavior.EffectPlayManaReduction,
            behavior.EffectPlayDestination, behavior.EffectPlayOptional, viewed);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events,
            PendingEffectPlay = pending, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }

    private static ResolutionResult ResolveEffectPlayCommand(MatchState state, PlayerIntent intent, GameCommand command)
    {
        var pending = state.PendingEffectPlay!;
        if (intent.PlayerId != pending.PlayerId)
            return RejectWithCorePrompts(state, "等待再次打出的执行者完成选择。", ErrorCodes.PhaseNotAllowed);
        if (pending.RevealedHand is { Choosing: true }) return ResolveRevealedHandSelection(state, intent, command, pending);
        if (command is ChooseCardsCommand decline)
        {
            if (decline.ChoiceId != pending.ChoiceId || decline.ChoiceWindow != "EFFECT_PLAY" || (decline.ChosenObjectIds?.Count ?? 0) != 0
                || !CanFinishWithoutEffectPlay(state))
                return RejectWithCorePrompts(state, "当前选择不能放弃或已经过期。", ErrorCodes.InvalidTarget);
            return FinishEffectPlay(state with { Tick = state.Tick + 1 }, intent, pending, []);
        }
        if (command is PlayCardCommand play)
        {
            if (!EffectPlaySources(state, intent.PlayerId).Contains(play.SourceObjectId)
                || !EffectPlayDestinations(state, intent.PlayerId).Contains(string.IsNullOrWhiteSpace(play.Destination) ? "BASE" : play.Destination)
                || IsAmbushPlayMode(play.Mode))
                return RejectWithCorePrompts(state, "再次打出的来源或位置已失效。", ErrorCodes.InvalidTarget);
            var result = ResolvePlayCard(state, intent, play);
            if (!result.Accepted) return result;
            result = ApplyEffectPlayFollowup(result, pending, play);
            return FinishEffectPlay(result.State, intent, pending, result.Events.Concat(RecastPlayedEvents(result.State, pending, play)).ToArray());
        }
        if (pending.IgnoreAllCosts) return RejectWithCorePrompts(state, "忽略一切费用时直接完成出牌，无需产费。", ErrorCodes.PhaseNotAllowed);
        if (command is TapRuneCommand tap) return ResolveTapRune(state, intent, tap);
        if (command is RecycleRuneCommand recycle) return ResolveRecycleRune(state, intent, recycle);
        if (command is ActivateAbilityCommand resource
            && P4ActivatedAbilityCatalog.TryGetByAbilityId(resource.AbilityId, out var ability)
            && ability.IsResourceSkill && ability.ReactionSpeed)
            return ResolveActivateAbility(state, intent, resource);
        return RejectWithCorePrompts(state, "请先完成效果要求的再次打出。", ErrorCodes.PhaseNotAllowed);
    }

    private static ResolutionResult FinishEffectPlay(MatchState state, PlayerIntent intent, PendingEffectPlayState pending, IReadOnlyList<GameEvent> playEvents)
    {
        if (pending.ViewedCardIds is { } viewed)
        {
            var zones = NormalizeZonesForSeats(state);
            var remaining = viewed.Where(id => zones[pending.PlayerId].MainDeck.Contains(id)).ToArray();
            var recycled = RandomizeForMainDeckBottom(remaining, state.Seed, state.RngCursor, pending.Parent.SourceObjectId);
            zones[pending.PlayerId] = zones[pending.PlayerId] with { MainDeck = zones[pending.PlayerId].MainDeck
                .Where(id => !remaining.Contains(id)).Concat(recycled).ToArray() };
            state = state with { PlayerZones = zones, RngCursor = state.RngCursor + (remaining.Length > 1 ? 1 : 0) };
            if (remaining.Length > 0)
                playEvents = playEvents.Append(new GameEvent("CARDS_RECYCLED", $"回收其余 {remaining.Length} 张查看的牌",
                    new Dictionary<string, object?> { ["playerId"] = pending.PlayerId,
                        ["sourceObjectId"] = pending.Parent.SourceObjectId, ["count"] = remaining.Length })).ToArray();
        }
        // Resume the parent immediately. Its remaining cleanup and observers still
        // run through the ordinary stack resolver, before responding to child triggers.
        var resumed = state with { Tick = state.Tick - 1, PendingEffectPlay = null,
            StackItems = state.StackItems.Concat([pending.Parent with { EffectPlayCompleted = true }]).ToArray() };
        var result = ResolvePassPriority(resumed, intent, forceResolve: true);
        return result with { Events = playEvents.Concat(result.Events).ToArray() };
    }
}

using Riftbound.Contracts;

namespace Riftbound.Engine;

// Private, serialized continuation. The client receives a separate safe projection.
public sealed record PendingEffectPlayState(
    string ChoiceId, string PlayerId, StackItemState Parent, string SourceZone,
    IReadOnlyDictionary<string, long> Sources, bool IgnoreBaseMana, bool IgnoreBasePower,
    int ManaReduction, string DestinationPolicy, bool Optional);

public sealed partial class CoreRuleEngine
{
    internal static IReadOnlyList<string> EffectPlaySources(MatchState state, string playerId)
    {
        if (state.PendingEffectPlay is not { } pending)
            return state.PlayerZones.TryGetValue(playerId, out var handZones)
                ? handZones.Hand.Concat(handZones.ChampionZone).Distinct(StringComparer.Ordinal).ToArray() : [];
        if (pending.PlayerId != playerId || !state.PlayerZones.TryGetValue(playerId, out var zones)) return [];
        var sourceZone = pending.SourceZone switch { "HAND" => zones.Hand, "GRAVEYARD" => zones.Graveyard, "BANISHED" => zones.Banished, _ => [] };
        return pending.Sources.Where(x => sourceZone.Contains(x.Key, StringComparer.Ordinal)
            && state.CardObjects.TryGetValue(x.Key, out var card) && card.ObjectGeneration == x.Value)
            .Select(x => x.Key).ToArray();
    }

    internal static CardBehaviorDefinition EffectPlayBehavior(MatchState state, string playerId, CardBehaviorDefinition behavior)
        => state.PendingEffectPlay is { } pending && pending.PlayerId == playerId
            ? behavior with { IgnorePrintedPowerCost = pending.IgnoreBasePower } : behavior;

    internal static IReadOnlyList<string> EffectPlayDestinations(MatchState state, string playerId)
    {
        var pending = state.PendingEffectPlay;
        if (pending is null || pending.PlayerId != playerId) return [];
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
            ["optional"] = p.Optional, ["step"] = "PLAY_CARD",
            ["reason"] = "效果要求再次打出：选择单位、位置与费用后确认。" };

    internal static IReadOnlyDictionary<string, ActionPromptDto> BuildEffectPlayPrompts(MatchState state)
    {
        var p = state.PendingEffectPlay!;
        var own = ActionPromptBuilder.Build(state, p.PlayerId, true,
            EffectPlayReason(p), [CommandTypes.PlayCard, CommandTypes.TapRune, CommandTypes.RecycleRune, CommandTypes.Surrender]);
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
            : ActionPromptBuilder.Build(state, id, false, "等待对手完成效果要求的再次打出", ["WAIT", CommandTypes.Surrender]));
    }

    internal static string EffectPlayReason(PendingEffectPlayState pending)
    {
        var name = CardBehaviorRegistry.TryGetByCardNo(pending.Parent.CardNo, out var card) ? card.DisplayName : "卡牌效果";
        var cost = pending.IgnoreBasePower ? "忽略基础法力与符能，额外费用仍需支付"
            : pending.IgnoreBaseMana ? "忽略基础法力，仍需支付符能与额外费用" : $"费用减少 {pending.ManaReduction}，仍需支付符能";
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
            return !CardBehaviorRegistry.GetAll().Where(b => b.CardNo == card.CardNo && b.PlaysSourceToBaseAsUnit).Any(b => {
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
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        var actor = parent.ControllerId;
        var sourceZone = behavior.EffectPlaySourceZone;
        IReadOnlyList<string> sources = sourceZone == "HAND" ? zones[actor].Hand : parent.TargetObjectIds.Where(id => !string.IsNullOrEmpty(id)).ToArray();
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
        sources = sources.Where(id => cards.TryGetValue(id, out var card) && card.Tags.Contains(CardObjectTags.UnitCard)
            && CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var unit) && unit.PlaysSourceToBaseAsUnit).ToArray();
        if (sources.Count == 0 && !behavior.EffectPlayOptional && events.Count == 0)
            return ResolveStackItemEffect(state, parent with { EffectPlayCompleted = true });
        var pending = new PendingEffectPlayState($"EFFECT-PLAY:{state.Tick + 1}:{parent.StackItemId}", actor, parent,
            sourceZone, sources.ToDictionary(id => id, id => cards[id].ObjectGeneration, StringComparer.Ordinal),
            behavior.EffectPlayIgnoreBaseMana, behavior.EffectPlayIgnoreBasePower, behavior.EffectPlayManaReduction,
            behavior.EffectPlayDestination, behavior.EffectPlayOptional);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events,
            PendingEffectPlay = pending, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }

    private static ResolutionResult ResolveEffectPlayCommand(MatchState state, PlayerIntent intent, GameCommand command)
    {
        var pending = state.PendingEffectPlay!;
        if (intent.PlayerId != pending.PlayerId)
            return RejectWithCorePrompts(state, "等待再次打出的执行者完成选择。", ErrorCodes.PhaseNotAllowed);
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
            return FinishEffectPlay(result.State, intent, pending, result.Events);
        }
        if (command is TapRuneCommand tap) return ResolveTapRune(state, intent, tap);
        if (command is RecycleRuneCommand recycle) return ResolveRecycleRune(state, intent, recycle);
        return RejectWithCorePrompts(state, "请先完成效果要求的再次打出。", ErrorCodes.PhaseNotAllowed);
    }

    private static ResolutionResult FinishEffectPlay(MatchState state, PlayerIntent intent, PendingEffectPlayState pending, IReadOnlyList<GameEvent> playEvents)
    {
        // Resume the parent immediately. Its remaining cleanup and observers still
        // run through the ordinary stack resolver, before responding to child triggers.
        var resumed = state with { Tick = state.Tick - 1, PendingEffectPlay = null,
            StackItems = state.StackItems.Concat([pending.Parent with { EffectPlayCompleted = true }]).ToArray() };
        var result = ResolvePassPriority(resumed, intent, forceResolve: true);
        return result with { Events = playEvents.Concat(result.Events).ToArray() };
    }
}

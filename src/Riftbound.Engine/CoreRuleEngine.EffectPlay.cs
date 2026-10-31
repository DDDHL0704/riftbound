using Riftbound.Contracts;

namespace Riftbound.Engine;

// Private, serialized continuation. The client receives a separate safe projection.
public sealed record PendingEffectPlayState(
    string ChoiceId, string PlayerId, StackItemState Parent, string SourceZone,
    IReadOnlyDictionary<string, long> Sources, bool IgnoreBaseMana, bool IgnoreBasePower,
    int ManaReduction, string DestinationPolicy, bool Optional, IReadOnlyList<string>? ViewedCardIds = null,
    bool IgnoreAllCosts = false, RevealedHandPlayContext? RevealedHand = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SourceReplayContinuation? SourceReplay { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ObjectLocationState? OriginalLocation { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ObjectBinding? OriginalBattlefield { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? SourcePlayerId { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal static bool TryGetEffectPlayDefinition(StackItemState parent, out CardBehaviorDefinition definition)
    {
        if (parent.LastBreathBattlefieldDamage is { Kind: TriggerKinds.UnitLastBreathBanishDeckTopPlay })
        {
            definition = new(parent.CardNo, "绝念放逐牌库顶并打出", 0, parent.EffectKind, 0, 0,
                EffectPlaySourceZone: "BANISHED", EffectPlayIgnoreBaseMana: true, EffectPlayOptional: true, EffectPlayAllowsAnyCard: true);
            return true;
        }
        if (parent.EmpowerInteraction is { } empowerment && EmpowerInteractionDefinition(empowerment.CardNo)?.Effect
            is "PLAY_GRAVEYARD_UNIT" or "MILL_THEN_PLAY_OPPONENT_UNIT") {
            definition = EmpowerInteractionPlayBehavior(empowerment); return true;
        }
        if (parent.GraveyardPlay is { } graveyard && parent.EffectKind == GraveyardPlayEffect
            && parent.CardNo == graveyard.CardNo && GraveyardPlaySpec(graveyard.CardNo) is not null)
        {
            definition = GraveyardPlayBehavior(graveyard);
            return true;
        }
        if (parent.HighCostPlayObserver is { } highCost
            && parent.EffectKind == HighCostGraveyardPlayEffect
            && HighCostPlaysSourceFromGraveyard(highCost.CardNo))
        {
            definition = HighCostGraveyardPlayBehavior(highCost.CardNo);
            return true;
        }
        if(parent.ObservedPlayPermission is { } observed)
        {
            definition=new(parent.CardNo,observed.Context.Instruction.Kind == "REVEAL" ? "展示期间放逐的卡牌" : "查看期间放逐的卡牌",0,ObservedDeckPlayEffect,0,0,
                EffectPlaySourceZone:"STACK",EffectPlayIgnoreBaseMana:true,EffectPlayIgnoreBasePower:true,EffectPlayOptional:true);
            return ObservationPlayCosts.Value.ContainsKey(observed.Context.CardNo);
        }
        if (parent.EndAbility is { Kind: TriggerKinds.TurnEndRevealUntilUnit } end && EndSpec(end.CardNo) is not null)
        {
            definition = new(end.CardNo, "结束步骤展示并打出", 0, parent.EffectKind, 0, 0,
                DeckPlayKind: "SINGLE_REVEAL_UNTIL_UNIT", EffectPlayRevealsCards: true,
                EffectPlayIgnoreBaseMana: true, EffectPlayIgnoreBasePower: true, EffectPlayIgnoreAllCosts: true);
            return true;
        }
        if (parent.DestroyDeckPlay is { Prepared: true } killed && DestroyDeckPlayDefinition(parent, out var ability))
        {
            definition = new(parent.CardNo, ability.DisplayName, 0, parent.EffectKind, 0, 0,
                MainDeckLookCount: ability.MainDeckLookCount, DeckPlayKind: "SINGLE_PRIVATE_LIMITED_UNIT_PLAY",
                DeckPlayMaximumUnitMight: killed.KilledMight is { } might ? might + ability.PlayPowerDelta : null,
                EffectPlayOptional: true, EffectPlayIgnoreBaseMana: true, EffectPlayIgnoreBasePower: true, EffectPlayIgnoreAllCosts: true);
            return true;
        }
        if (parent.FieldContext is { Kind: "ATTACK_DECK_PLAY" } attack && AttackDeckPlaySpec(attack.CardNo) is { } spec)
        {
            definition = new(attack.CardNo, "进攻展示并打出", 0, parent.EffectKind, 0, 0,
                MainDeckLookCount: spec.RevealCount!.Value, DeckPlayKind: "SINGLE_PUBLIC_PLAY",
                EffectPlayOptional: true, EffectPlayAllowsAnyCard: true, EffectPlayRevealsCards: true,
                EffectPlayDestination: "ATTACK_HERE_OR_NORMAL");
            return true;
        }
        if (parent.LegendConquest is { Kind: "EXHAUST_DECK_PLAY" } context
            && ValidLegendConquest(context, parent.EffectKind, parent.CardNo)) {
            definition = new(parent.CardNo, "虚空遁地兽", 0, parent.EffectKind, 0, 0,
                MainDeckLookCount: LegendConquestDefinition(parent.CardNo)!.Value.Spec.RevealCount!.Value,
                DeckPlayKind: "SINGLE_PUBLIC_PLAY", EffectPlayOptional: true,
                EffectPlayAllowsAnyCard: true, EffectPlayRevealsCards: true);
            return true;
        }
        return CardBehaviorRegistry.TryGetByEffectKind(parent.EffectKind, out definition!);
    }

    private static bool EffectPlaySourceMatches(CardObjectState card, CardBehaviorDefinition definition)
        => CardBehaviorRegistry.GetAll().Any(b => b.CardNo == card.CardNo && (definition.EffectPlayAllowsAnyCard
            ? b.PlaysSourceToBaseAsUnit || b.PlaysSourceToBaseAsEquipment || IsSpellPlayBehavior(b)
            : definition.DeckPlayKind == "SINGLE_PRIVATE_UNIT_OR_GEAR_PLAY"
            ? b.PlaysSourceToBaseAsUnit || b.PlaysSourceToBaseAsEquipment
            : b.PlaysSourceToBaseAsUnit));

    internal static bool IsPubliclyRevealedDeckCard(MatchState state, string id)
        => state.DeckObservationView is {Public:true} v && v.Cards.TryGetValue(id,out var observedGeneration)
            && state.CardObjects.TryGetValue(id,out var observedCard) && observedCard.ObjectGeneration==observedGeneration
            || state.StackItems.LastOrDefault(i => i.DeckPlaySequence is not null) is { DeckPlaySequence: { } sequence } item && DeckPlaySequenceReveals(item)
            && sequence.Viewed.Values.SelectMany(v => v).Any(binding => binding.ObjectId == id
                && state.CardObjects.TryGetValue(id, out var card) && card.ObjectGeneration == binding.Generation);

    internal static IReadOnlyList<string> EffectPlaySources(MatchState state, string playerId)
    {
        if (state.PendingEffectPlay is not { } pending)
            return state.PlayerZones.TryGetValue(playerId, out var handZones)
                ? handZones.Hand.Concat(handZones.ChampionZone).Concat(StandbyPlaySources(state, playerId))
                    .Concat(FlowPlaySources(state, playerId)).Concat(NormalGraveyardPlaySources(state, playerId)).Distinct(StringComparer.Ordinal).ToArray() : [];
        if (pending.RevealedHand is { Choosing: true }) return [];
        if (pending.PlayerId != playerId || !state.PlayerZones.TryGetValue(playerId, out var zones)) return [];
        if (pending.SourcePlayerId is { } sourcePlayer && state.PlayerZones.TryGetValue(sourcePlayer, out var sourceZones)) zones = sourceZones;
        var sourceZone = pending.SourceZone switch { "STACK" when pending.Parent.DeckPlaySequence is not null || pending.Parent.ObservedPlayPermission is not null => pending.Sources.Keys.Where(id => state.ObjectLocations.GetValueOrDefault(id)?.Zone == "STACK").ToArray(), "STACK" when pending.SourceReplay is not null => pending.Sources.Keys.Where(id => state.ObjectLocations.GetValueOrDefault(id)?.Zone == "STACK" && !state.StackItems.Any(i => i.SourceObjectId == id)).ToArray(), "HAND" => zones.Hand, "GRAVEYARD" => zones.Graveyard, "BANISHED" => zones.Banished, _ => [] };
        return pending.Sources.Where(x => sourceZone.Contains(x.Key, StringComparer.Ordinal)
            && state.CardObjects.TryGetValue(x.Key, out var card) && card.ObjectGeneration == x.Value
            && (RecastSpec(pending.Parent) is not { } spec || RecastSourceAllowed(state, playerId, card, spec)))
            .Select(x => x.Key).ToArray();
    }

    // Restore the same policy selected by the registered effect, never client-supplied waivers.
    internal static bool ValidGraveyardUnitEffectPlay(MatchState state, PendingEffectPlayState pending)
        => pending.Parent.EmpowerInteraction is not null ? ValidEmpowerInteractionPlay(state, pending)
            : pending.SourcePlayerId is null && TryGetEffectPlayDefinition(pending.Parent, out var behavior)
            && behavior.CardNo == pending.Parent.CardNo && behavior.EffectPlaySourceZone == "GRAVEYARD"
            && pending.SourceZone == behavior.EffectPlaySourceZone && pending.PlayerId == pending.Parent.ControllerId
            && state.Seats.ContainsKey(pending.PlayerId) && state.PlayerZones.TryGetValue(pending.PlayerId, out var zones)
            && pending.IgnoreBaseMana == behavior.EffectPlayIgnoreBaseMana
            && pending.IgnoreBasePower == behavior.EffectPlayIgnoreBasePower
            && pending.ManaReduction == behavior.EffectPlayManaReduction
            && pending.DestinationPolicy == behavior.EffectPlayDestination && pending.Optional == behavior.EffectPlayOptional
            && pending.ViewedCardIds is null && !pending.Parent.EffectPlayCompleted
            && (pending.Parent.GraveyardPlay is not { } graveyard
                || ValidGraveyardPlay(state, graveyard, pending.Parent.SourceObjectId, pending.PlayerId, pending.Parent.CardNo)
                    && ValidTriggerCostReceipt(state, pending.Parent) && pending.Parent.TriggerCost is not null
                    && pending.Sources.Count == 1 && pending.Sources.GetValueOrDefault(graveyard.Source.ObjectId, -1) == graveyard.Source.Generation)
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
                RequiresReturnFriendlyEquipmentAdditionalCost = false,
                MayReturnFriendlyEquipmentAdditionalCost = false,
                RequiresReturnFriendlyUnitAdditionalCost = false
            } : behavior with { IgnorePrintedPowerCost = pending.IgnoreBasePower } : behavior;

    internal static IReadOnlyList<string> EffectPlayDestinations(MatchState state, string playerId, CardBehaviorDefinition? behavior = null)
    {
        var ordinary = OrdinaryEffectPlayDestinations(state, playerId);
        IEnumerable<CardBehaviorDefinition> behaviors = behavior is not null ? [behavior] : EffectPlaySources(state, playerId)
            .Select(id => state.CardObjects.GetValueOrDefault(id)?.CardNo)
            .Select(no => CardBehaviorRegistry.TryGetByCardNo(no ?? "", out var found) ? found : null)
            .OfType<CardBehaviorDefinition>();
        return ordinary.Concat(behaviors.SelectMany(b => BattlefieldUnitPlayGrants(state, playerId, b))
                .Select(g => "BATTLEFIELD:" + g.Field.ObjectId))
            .Where(destination => behavior is null || UnitPlayLocationRestrictionAllows(state, playerId, behavior, destination))
            .Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> OrdinaryEffectPlayDestinations(MatchState state, string playerId)
    {
        var pending = state.PendingEffectPlay;
        if (pending is null || pending.PlayerId != playerId) return [];
        if (pending.DestinationPolicy == "ORIGINAL_LOCATION")
        {
            if (pending.OriginalLocation is { Zone: "BASE" } at)
                return at.PlayerId == playerId ? ["BASE"] : [];
            var destination = "BATTLEFIELD:" + pending.OriginalBattlefield?.ObjectId;
            return IsForcedEffectPlayDestination(state, playerId, destination)
                && !HasBattlefieldStaticPreventUnitPlayToBattlefield(state, playerId, destination) ? [destination] : [];
        }
        if (pending.Parent.FieldContext is { Kind: "ATTACK_DECK_PLAY" }
            && pending.Sources.Keys.SingleOrDefault() is { } chosen && state.CardObjects.TryGetValue(chosen, out var selected)
            && !selected.Tags.Contains(CardObjectTags.UnitCard)) return ["BASE"];
        // 811.1.d.3 also constrains a unit played by the original shadow controller through an effect.
        if (pending.Parent.PlayedFromStandby is { } origin && playerId == pending.Parent.ControllerId)
            return state.CardObjects.TryGetValue(origin.Battlefield.ObjectId, out var field)
                && field.ObjectGeneration == origin.Battlefield.Generation
                && field.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag)
                && !HasBattlefieldStaticPreventUnitPlayToBattlefield(state, playerId, "BATTLEFIELD:" + field.ObjectId)
                    ? ["BATTLEFIELD:" + field.ObjectId] : [];
        if (pending.RevealedHand is { } hand)
            return !hand.Choosing && IsForcedEffectPlayDestination(state, playerId, "BATTLEFIELD:" + hand.BattlefieldId)
                && !HasBattlefieldStaticPreventUnitPlayToBattlefield(state, playerId, "BATTLEFIELD:" + hand.BattlefieldId)
                ? ["BATTLEFIELD:" + hand.BattlefieldId] : [];
        if (pending.DestinationPolicy == "STACK") return ["BASE"];
        if (pending.DestinationPolicy == "ANY_BATTLEFIELD")
            return state.CardObjects.Keys.Select(id => "BATTLEFIELD:" + id)
                .Where(destination => IsForcedEffectPlayDestination(state, playerId, destination)
                    && !HasBattlefieldStaticPreventUnitPlayToBattlefield(state, playerId, destination)).ToArray();
        var destinations = new List<string>();
        if (pending.DestinationPolicy != "CONTROLLED_BATTLEFIELD") destinations.Add("BASE");
        if (pending.DestinationPolicy != "BASE")
            destinations.AddRange(state.CardObjects.Keys.Select(id => "BATTLEFIELD:" + id)
                .Where(destination => IsPlayCardUnitBattlefieldDestinationAllowed(state, playerId, destination)
                    && !HasBattlefieldStaticPreventUnitPlayToBattlefield(state, playerId, destination)));
        if (AttackDeckPlayHere(state, pending) is { } here && !destinations.Contains(here)
            && !HasBattlefieldStaticPreventUnitPlayToBattlefield(state, playerId, here)) destinations.Add(here);
        return destinations;
    }

    internal static IReadOnlyDictionary<string, object?>? EffectPlayView(MatchState state, string visibleSourceObjectId)
        => state.PendingEffectPlay is not { } p ? null : new Dictionary<string, object?> {
            ["choiceId"] = p.ChoiceId, ["playerId"] = p.PlayerId,
            ["sourceObjectId"] = visibleSourceObjectId, ["sourceCardNo"] = p.Parent.CardNo,
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
                : [CommandTypes.PlayCard, CommandTypes.LegendAct, CommandTypes.ActivateAbility, CommandTypes.TapRune, CommandTypes.RecycleRune, CommandTypes.Surrender]);
        if (p.ViewedCardIds is not null)
            own = own with { Candidates = own.Candidates!.Select(candidate => candidate.Action != CommandTypes.PlayCard ? candidate
                : candidate with { Metadata = new Dictionary<string, object?>(candidate.Metadata!)
                    { ["viewedCardsPublic"] = TryGetEffectPlayDefinition(p.Parent, out var definition) && definition.EffectPlayRevealsCards,
                      ["viewedCards"] = p.ViewedCardIds.Select(id => new ActionPromptChoiceDto(id, DeckChoiceLabel(state, id), IsPubliclyRevealedDeckCard(state, id) ? "已公开展示" : "仅你可见")).ToArray(),
                      ["reason"] = EffectPlayReason(p) } }).ToArray() };
        // The fee depends on the selected source/mode/optional costs. Reuse the
        // ordinary play composer; the server validates the completed declaration.
        if (!p.IgnoreAllCosts && own.Candidates!.Single(c => c.Action == CommandTypes.PlayCard) is { Enabled: true } play)
        {
            var refuse = play with { Action = CommandTypes.RefusePlayPayment, Label = "拒绝所选打出的资源费用",
                Reason = "选择来源、模式和额外费用；只有最终法力或符能费用非零时才可拒付。",
                CommandTemplate = play.CommandTemplate! with { CmdType = CommandTypes.RefusePlayPayment },
                Presentation = play.Presentation! with { Intent = "refuse-play-payment" } };
            own = own with { Actions = own.Actions.Append(CommandTypes.RefusePlayPayment).ToArray(),
                Candidates = own.Candidates!.Append(refuse).ToArray() };
        }
        // A decline is a zero-card choice, using the existing native card-choice composer.
        if (CanFinishWithoutEffectPlay(state))
        {
            var choiceState = state with { PendingCardChoice = new(p.ChoiceId, "EFFECT_PLAY", p.PlayerId,
                0, 0, [], [], p.Optional ? "放弃此次再次打出" : "拒绝支付资源费用或无法完成此次打出，继续结算", p.Parent.SourceObjectId, p.Parent.EffectKind) };
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
        var cost = pending.SourceReplay is not null || pending.Parent.ObservedPlayPermission is not null ? "以 1 通用符能替换基础费用，额外费用仍需支付" : pending.IgnoreAllCosts ? pending.Parent.DeckPlaySequence is not null
            ? "忽略一切费用，按正常步骤选择并打出" : "忽略一切费用；不能支付额外费用；必须打到指定战场"
            : pending.IgnoreBasePower ? "忽略基础法力与符能，额外费用仍需支付"
            : pending.IgnoreBaseMana ? "忽略基础法力，仍需支付符能与额外费用" : pending.ManaReduction > 0 ? $"费用减少 {pending.ManaReduction}，仍需支付符能" : "正常支付法力、符能及额外费用";
        return $"《{name}》要求再次打出 · {cost}";
    }

    private static bool CanFinishWithoutEffectPlay(MatchState state)
    {
        var p = state.PendingEffectPlay!;
        if (p.Optional || EffectPlayDestinations(state, p.PlayerId).Count == 0) return true;
        var choices = ActionPromptBuilder.PlayCardChoices(state, p.PlayerId)
            .Where(choice => EffectPlayAllowsBehavior(state, p.PlayerId, choice.Behavior)).ToArray();
        if (choices.Length == 0) return true;
        // CN445.2/445.2.a permits refusing a nonzero resource fee and
        // withdrawing that play, even when those resources are already pooled.
        // CN445.1.b does not permit refusing a zero fee. But the player can
        // choose a different, nonzero route and refuse that payment; the mere
        // presence of a free candidate cannot force that candidate to be played.
        // Optional discard, experience or sacrifice reductions are not assumed:
        // the player can decline those choices and refuse the default fee.
        return choices.Any(choice => {
            var behavior = choice.Behavior;
            var pool = PaymentCostRules.PowerPoolForPurpose(state.RunePools.GetValueOrDefault(p.PlayerId) ?? RunePool.Empty,
                PowerPurposeForPlay(behavior));
            // Compulsory total-power reductions also apply to a waived-base
            // play. A printed pip alone is not proof of a nonzero final fee.
            var hasPowerPrice = TrySelectPlayPowerCost(state, p.PlayerId, behavior, null, pool,
                SourceReplayPowerCost(state, p.PlayerId), new Dictionary<string, int>(), out var generic, out var typed);
            return !EffectPlayIgnoresAllCosts(state, p.PlayerId) && BattlefieldUnitPlayCostChoices(state, p.PlayerId, behavior).Count > 0
                || CalculatePlayManaCost(state, p.PlayerId, behavior, sourceObjectId: choice.SourceObjectId).Total > 0
                || hasPowerPrice && (generic > 0 || typed.Values.Any(amount => amount > 0));
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
        ObjectLocationState? originalLocation = null;
        ObjectBinding? originalBattlefield = null;
        IReadOnlyList<string> sources = sourceZone == "HAND" ? zones[actor].Hand : parent.TargetObjectIds.Where(id => !string.IsNullOrEmpty(id)).ToArray();
        if (sourceZone == "BANISHED")
        {
            sources = sources.Take(1).ToArray();
            if (sources.Count > 0 && cards.TryGetValue(sources[0], out var target))
            {
                var id = sources[0];
                if (behavior.EffectPlayDestination == "ORIGINAL_LOCATION")
                {
                    originalLocation = state.ObjectLocations.GetValueOrDefault(id);
                    if (originalLocation is { Zone: "BATTLEFIELD", BattlefieldObjectId: { } fieldId }
                        && BattlefieldLocalRules.Battlefield(state, fieldId) is { } field)
                        originalBattlefield = new(fieldId, field.ObjectGeneration);
                }
                actor = NonFieldDestinationOwner(zones, target, parent.ControllerId);
                DetachEquipmentFromRemovedHost(cards, id);
                foreach (var owner in zones.Keys.ToArray()) zones[owner] = zones[owner] with {
                    Base = RemoveFromZone(zones[owner].Base, id), Battlefields = RemoveFromZone(zones[owner].Battlefields, id) };
                zones[actor] = zones[actor] with { Banished = zones[actor].Banished.Concat([id]).Distinct().ToArray() };
                if (PrintedCardFactory.TryRestoreOutsidePlay(target, actor, out var printed)) cards[id] = printed with { ObjectGeneration = target.ObjectGeneration + 1 };
                else if (TokenObjectRules.IsToken(target))
                {
                    // CN 186.1: the token vanishes after entering banishment;
                    // it cannot be a source for the following play instruction.
                    cards.Remove(id);
                    zones[actor] = zones[actor] with { Banished = RemoveFromZone(zones[actor].Banished, id) };
                    sources = [];
                }
                events.Add(CaptureOwnedBanishObservers(new("UNIT_BANISHED", "单位被放逐，等待其拥有者再次打出", new Dictionary<string, object?> {
                    ["sourceObjectId"] = parent.SourceObjectId, ["targetObjectId"] = id, ["ownerPlayerId"] = actor, ["destinationZone"] = "BANISHED",
                    ["originalLocation"] = originalLocation, ["originalBattlefield"] = originalBattlefield,
                    ["parentStackItemId"] = parent.StackItemId, ["targetGeneration"] = target.ObjectGeneration }),
                    zones, cards, parent.ControllerId, [id]));
            }
        }
        sources = sources.Where(id => cards.TryGetValue(id, out var card) && EffectPlaySourceMatches(card, behavior)).ToArray();
        if (sources.Count == 0 && sourceZone == "BANISHED")
        {
            var resumed = state with { PlayerZones = zones, CardObjects = cards,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
            var resolved = ResolveStackItemEffect(resumed, parent with { EffectPlayCompleted = true });
            return resolved with { Events = events.Concat(resolved.Events).ToArray() };
        }
        if (sources.Count == 0 && events.Count == 0)
            return ResolveStackItemEffect(state, parent with { EffectPlayCompleted = true });
        var pending = new PendingEffectPlayState($"EFFECT-PLAY:{state.Tick + 1}:{parent.StackItemId}", actor, parent,
            sourceZone, sources.ToDictionary(id => id, id => cards[id].ObjectGeneration, StringComparer.Ordinal),
            behavior.EffectPlayIgnoreBaseMana, behavior.EffectPlayIgnoreBasePower, behavior.EffectPlayManaReduction,
            behavior.EffectPlayDestination, behavior.EffectPlayOptional) {
                OriginalLocation = originalLocation, OriginalBattlefield = originalBattlefield,
                SourcePlayerId = parent.EmpowerInteraction?.Opponent };
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
        if (command is RefusePlayPaymentCommand refusal)
            return ResolveRefusePlayPayment(state, intent, pending, refusal);
        if (command is PlayCardCommand play)
        {
            if (!EffectPlaySources(state, intent.PlayerId).Contains(play.SourceObjectId)
                || (!(CardBehaviorRegistry.TryGetByCardNo(play.CardNo, out var playedBehavior) && UsesDeclaredMovementDestination(playedBehavior))
                    && !EffectPlayDestinations(state, intent.PlayerId).Contains(string.IsNullOrWhiteSpace(play.Destination) ? "BASE" : play.Destination))
                || IsAmbushPlayMode(play.Mode))
                return RejectWithCorePrompts(state, "再次打出的来源或位置已失效。", ErrorCodes.InvalidTarget);
            var playState = pending.Parent.ObservedPlayPermission is not null ? state with {
                StackItems=state.StackItems.Where(s=>s.ObservedDeckPlay?.Card.ObjectId!=play.SourceObjectId).ToArray() }
                : pending.Parent.DeckPlaySequence is null ? state : state with {
                StackItems = state.StackItems.Where(s => s.DeckPlayPendingCard?.Card.ObjectId != play.SourceObjectId).ToArray() };
            var result = ResolvePlayCard(playState, intent, play);
            if (!result.Accepted) return pending.Parent.DeckPlaySequence is null && pending.Parent.ObservedPlayPermission is null ? result : result with {
                State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
            if (pending.Parent.DeckPlaySequence is not null)
            {
                var slotIndex = state.StackItems.Select((item, index) => (item, index))
                    .Single(p => p.item.DeckPlayPendingCard?.Card.ObjectId == play.SourceObjectId).index;
                var actual = result.State.StackItems.FirstOrDefault(s => s.SourceObjectId == play.SourceObjectId && !s.SourceConfirmed);
                if (actual is not null)
                {
                    var stack = result.State.StackItems.Where(s => s.StackItemId != actual.StackItemId).ToList();
                    stack.Insert(Math.Min(slotIndex, stack.Count), actual);
                    result = result with { State = result.State with { StackItems = stack } };
                }
            }
            result = ApplyEffectPlayFollowup(result, pending, play);
            return FinishEffectPlay(result.State, intent, pending, result.Events.Concat(RecastPlayedEvents(result.State, pending, play)).ToArray());
        }
        if (pending.IgnoreAllCosts) return RejectWithCorePrompts(state, "忽略一切费用时直接完成出牌，无需产费。", ErrorCodes.PhaseNotAllowed);
        if (command is LegendActCommand legendResource
            && TryGetLegendAbility(legendResource.AbilityId, out var legendDefinition)
            && legendDefinition.TimingKind == LegendAbilityTimingKinds.ResourceAction)
            return ResolveLegendAct(state, intent, legendResource);
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
        if(pending.Parent.ObservedPlayPermission is not null)return FinishObservedDeckPlay(state,pending,playEvents);
        if (pending.Parent.DeckPlaySequence is not null) return FinishDeckPlaySequence(state, intent, pending, playEvents);
        if (pending.SourceReplay is not null)
            pending = pending with { Parent = pending.Parent with { SourceReplayDecisionCompleted = true } };
        // Resume the parent immediately. Its remaining cleanup and observers still
        // run through the ordinary stack resolver, before responding to child triggers.
        var resumed = state with { Tick = state.Tick - 1, PendingEffectPlay = null,
            StackItems = state.StackItems.Concat([pending.Parent with { EffectPlayCompleted = true }]).ToArray() };
        var result = ResolvePassPriority(resumed, intent, forceResolve: true);
        return result with { Events = playEvents.Concat(result.Events).ToArray() };
    }
}

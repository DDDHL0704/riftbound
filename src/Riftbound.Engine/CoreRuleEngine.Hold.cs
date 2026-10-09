using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static readonly IReadOnlyDictionary<string, (string Kind, int Amount)> HeldRepresentatives =
        new Dictionary<string, (string, int)> {
            ["OGN·275/298"] = ("MINION", 1), ["OGN·280/298"] = ("DRAW", 1),
            ["OGN·281/298"] = ("RETURN_HERO", 1), ["OGN·283/298"] = ("BOON", 1),
            ["OGN·286/298"] = ("ACTIVATE_CONQUEST", 1), ["OGN·288/298"] = ("CHANNEL_OPTIONAL", 1), ["OGN·293/298"] = ("SEVEN_WIN", 7),
            ["OGN·066/298"] = ("SCORE", 1), ["OGN·067/298"] = ("RETURN_SELF", 1),
            ["SFD·058/221"] = ("LOOK_EQUIPMENT", 1),
            ["SFD·027/221"] = ("DRAW", 2), ["SFD·035/221"] = ("RETURN_PERMANENT", 1),
            ["SFD·089/221"] = ("ROBOT", 1), ["SFD·152/221"] = ("GOLD", 2),
            ["SFD·201/221"] = ("RENATA", 1), ["SFD·214/221"] = ("PAY_POWER_SCORE", 4), ["SFD·219/221"] = ("CHANNEL_ALL", 1),
            ["UNL-043/219"] = ("BOON_ALL", 1), ["UNL-060/219"] = ("DRAW", 1),
            ["UNL-087/219"] = ("BLUE_DELAYED_POWER", 1), ["UNL-193/219"] = ("VEX", 1),
            ["UNL-195/219"] = ("IVERN", 1), ["UNL-199/219"] = ("LEBLANC_DISCARD", 1), ["UNL-203/219"] = ("EXPERIENCE", 1), ["UNL-207/219"] = ("MOVE_BASE", 1),
            ["UNL-216/219"] = ("NEXT_ECHO", 1), ["UNL-219/219"] = ("UNIT_TAX", 1)
        };
    private static readonly Lazy<IReadOnlyDictionary<string, (string Kind, int Amount)>> HeldCards = new(() => {
        var groups = OfficialCardSourceIdentityGroups.BuildByRepresentativeCardNo(HeldRepresentatives.Keys);
        return HeldRepresentatives.SelectMany(pair => groups.GetValueOrDefault(pair.Key, [pair.Key])
                .Select(no => (no, pair.Value))).ToDictionary(x => x.no, x => x.Value, StringComparer.Ordinal);
    });
    private static (string Kind, int Amount)? HeldDefinition(string? cardNo) =>
        HeldCards.Value.TryGetValue(OfficialCardSourceIdentityGroups.NormalizeCardNo(cardNo), out var definition) ? definition : null;

    private static string[] HeldUnitsAt(MatchState state, string field, string? controller = null) => state.CardObjects
        .Where(e => e.Value.Tags.Contains(CardObjectTags.UnitCard) && !e.Value.IsFaceDown
            && !e.Value.Tags.Contains(CardObjectTags.Standby) && IsObjectOnField(state.PlayerZones, e.Key)
            && (controller is null || e.Value.ControllerId == controller)
            && state.ObjectLocations.TryGetValue(e.Key, out var location) && location.Zone == MoveUnitBattlefieldZone
            && location.BattlefieldObjectId == field).Select(e => e.Key).Order(StringComparer.Ordinal).ToArray();

    private static MatchState QueueHeldAbilities(MatchState state, IReadOnlyList<string> fields, List<GameEvent> events)
    {
        var queue = state.TriggerQueue.ToList();
        var delayed = state.DelayedResourceGains.ToList();
        var player = state.TurnPlayerId;
        foreach (var field in fields)
        {
            var units = HeldUnitsAt(state, field, player);
            var copies = 1 + units.Count(id => HeldDefinition(state.CardObjects[id].CardNo)?.Kind == "BLUE_DELAYED_POWER");
            var sources = new[] { field }.Concat(units).Concat(state.PlayerZones[player].LegendZone).Distinct().ToArray();
            foreach (var source in sources)
            {
                if (!state.CardObjects.TryGetValue(source, out var card) || card.IsFaceDown
                    || (source != field && card.ControllerId != player)) continue;
                var abilities = new List<(string Kind, int Amount)>();
                if (HeldDefinition(card.CardNo) is { } definition
                    && (definition.Kind != "SEVEN_WIN" || units.Length >= definition.Amount)) abilities.Add(definition);
                if (units.Contains(source) && CardResourceKeywordRules.HuntAmountFromTags(card.Tags) is > 0 and var hunt)
                    abilities.Add(("EXPERIENCE", hunt));
                foreach (var ability in abilities)
                    for (var copy = 0; copy < copies; copy++)
                    {
                        var id = $"held-{state.TurnNumber}-{state.Tick}-{field}-{source}-{ability.Kind}-{copy}";
                        if (ability.Kind == "BLUE_DELAYED_POWER")
                        {
                            delayed.Add(new(id, player, source, card.CardNo ?? "", field, state.TurnNumber, ability.Amount));
                            events.Add(new("DELAYED_ABILITY_CREATED", "已登记下一个主阶段的资源技能",
                                new Dictionary<string, object?> { ["triggerId"] = id, ["controllerId"] = player,
                                    ["sourceObjectId"] = source, ["battlefieldObjectId"] = field }));
                        }
                        else
                        {
                            var trigger = new TriggerQueueItemState(id, player, source, "HOLD_" + ability.Kind, "BATTLEFIELD_HELD") {
                                HeldContext = new(card.CardNo ?? "", field, ability.Kind, ability.Amount, card.ObjectGeneration) };
                            queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
                        }
                    }
            }
        }
        return state with { TriggerQueue = queue, DelayedResourceGains = delayed };
    }

    private static StackResolutionResult ResolveHeldStackItem(MatchState state, StackItemState item,
        IReadOnlyList<string>? choices = null)
    {
        var context = item.HeldContext!;
        if (context.Kind == "LOOK_EQUIPMENT" && TryGetDeckChoiceBehavior(item, out var deckBehavior))
        {
            if (!item.DeckChoiceCompleted) return BeginDeckChoice(state, item, deckBehavior);
            return new(state.PlayerZones, state.CardObjects, state.PlayerScores, state.PlayerExperience, state.RunePools,
                state.UntilEndOfTurnEffects, null,
                [new("TRIGGER_RESOLVED", "据守查看装备技能结算完成", new Dictionary<string, object?>
                    { ["sourceObjectId"] = item.SourceObjectId, ["controllerId"] = item.ControllerId,
                      ["effectKind"] = item.EffectKind, ["battlefieldObjectId"] = context.BattlefieldObjectId })],
                [], null, [], null, [], state.RngCursor);
        }
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var scores = state.PlayerScores;
        var experience = NormalizeExperienceForSeats(state);
        var effects = state.UntilEndOfTurnEffects;
        var player = item.ControllerId;
        var field = context.BattlefieldObjectId;
        var events = new List<GameEvent>();
        var triggers = new List<TriggerQueueItemState>();
        var rng = state.RngCursor;
        string? winner = null;
        PendingCardChoiceState? pending = null;
        PendingPaymentState? payment = null;
        var sourceExists = cards.TryGetValue(item.SourceObjectId, out var source)
            && source.ObjectGeneration == context.SourceGeneration
            && (IsObjectOnField(zones, item.SourceObjectId) || zones[player].LegendZone.Contains(item.SourceObjectId));
        string[] LegalChoices() => context.Kind switch {
            "BOON" => HeldUnitsAt(state, field),
            "MOVE_BASE" => state.ObjectLocations.Values.Where(l => l.BattlefieldObjectId is not null)
                .Select(l => l.BattlefieldObjectId!).Distinct().SelectMany(f => HeldUnitsAt(state, f)).Distinct().ToArray(),
            "RETURN_PERMANENT" => zones[player].Graveyard.Where(id => cards.TryGetValue(id, out var card)
                && (card.Tags.Contains(CardObjectTags.UnitCard) || card.Tags.Contains(CardObjectTags.EquipmentCard))).ToArray(),
            "RETURN_HERO" => zones[player].ChampionZone.Count == 0 ? zones[player].Graveyard.Where(id =>
                cards.TryGetValue(id, out var card) && IsSelectedChampionObjectId(state, player, id, card)).ToArray() : [],
            "CHANNEL_OPTIONAL" => zones[player].RuneDeck.Count > 0 ? [item.SourceObjectId] : [],
            "LEBLANC_DISCARD" => sourceExists && source!.ControllerId == player && !source.IsExhausted
                && HeldUnitsAt(state, field).Length > 0 ? zones[player].Hand.ToArray() : [],
            "LEBLANC_COPY" => sourceExists && source!.ControllerId == player && !source.IsExhausted
                && context.SelectedCostObjectId is not null && zones[player].Hand.Contains(context.SelectedCostObjectId)
                    ? HeldUnitsAt(state, field) : [],
            "VEX" or "RENATA" or "IVERN" => sourceExists && source!.ControllerId == player && !source.IsExhausted ? [item.SourceObjectId] : [],
            _ => []
        };
        var needsChoice = context.Kind is "BOON" or "MOVE_BASE" or "RETURN_PERMANENT" or "RETURN_HERO"
            or "CHANNEL_OPTIONAL" or "VEX" or "RENATA" or "IVERN" or "LEBLANC_DISCARD" or "LEBLANC_COPY";
        if (needsChoice && choices is null)
        {
            var legal = LegalChoices();
            if (legal.Length > 0)
                pending = new($"hold-choice-{state.Tick + 1}-{item.StackItemId}", "HOLD_EFFECT", player,
                    context.Kind is "BOON" or "LEBLANC_COPY" ? 1 : 0, 1, legal,
                    [field],
                    context.Kind == "BOON" ? "据守效果：选择此战场的一名单位获得增益" : "据守效果：选择执行，或不选并确认以放弃",
                    item.SourceObjectId, item.EffectKind) { HeldContext = context };
            return Result();
        }
        if (needsChoice && choices!.Count == 0) return Result();
        if (needsChoice && choices!.Any(id => !LegalChoices().Contains(id))) return Result();
        events.Add(new("TRIGGER_RESOLVED", "据守技能结算", new Dictionary<string, object?> {
            ["triggerId"] = item.StackItemId, ["controllerId"] = player, ["sourceObjectId"] = item.SourceObjectId,
            ["effectKind"] = item.EffectKind, ["battlefieldObjectId"] = field }));
        switch (context.Kind)
        {
            case "PAY_POWER_SCORE":
                payment = new($"hold-payment-{state.Tick + 1}-{item.StackItemId}", "HOLD_EFFECT", player,
                    powerCost: context.Amount, legalPaymentChoiceIds: ["PAY", DeclinePaymentChoiceId],
                    reason: "可选择支付 4 点任意符能，额外获得 1 分") { HeldContext = context };
                break;
            case "IVERN":
                if (TryResolveIvernLegendBrushTrigger(zones, cards, player, field, item.SourceObjectId,
                    "BATTLEFIELD_HELD_REPLACE_WITH_BRUSH", out var brushEvents)) events.AddRange(brushEvents);
                break;
            case "LEBLANC_DISCARD":
                pending = new($"hold-copy-{state.Tick + 1}-{item.StackItemId}", "HOLD_EFFECT", player, 1, 1,
                    HeldUnitsAt(state, field), [field], "选择此战场中要复制的单位；确认后支付弃牌及横置费用",
                    item.SourceObjectId, item.EffectKind) { HeldContext = context with { Kind = "LEBLANC_COPY", SelectedCostObjectId = choices![0] } };
                break;
            case "LEBLANC_COPY":
                if (TryResolveLeblancLegendImageTrigger(zones, cards, player, field, item.SourceObjectId, choices![0],
                    "BATTLEFIELD_HELD_CREATE_IMAGE", out var imageEvents, out var discarded, context.SelectedCostObjectId))
                {
                    events.AddRange(imageEvents);
                    effects = MarkPlayerDiscardedHandCardsThisTurn(effects, player, discarded);
                }
                break;
            case "ACTIVATE_CONQUEST":
                foreach (var unit in HeldUnitsAt(state, field))
                {
                    var unitCard = cards[unit];
                    if (!HasSupportedUnitConquestTriggerSpec(unitCard.CardNo)) continue;
                    var trigger = new TriggerQueueItemState($"activate-conquest-{state.Tick}-{unit}-{item.StackItemId}",
                        unitCard.ControllerId, unit, "HOLD_CONQUEST_UNIT", "BATTLEFIELD_HELD") {
                        HeldContext = new(unitCard.CardNo ?? "", field, "CONQUEST_UNIT", 0, unitCard.ObjectGeneration) };
                    triggers.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
                }
                break;
            case "CONQUEST_UNIT":
                TryResolveNaturalUnitConquestTriggerSpecs(state, zones, cards, scores, effects, player, field,
                    [item.SourceObjectId], 0, rng, events, out var conquestDraw, out var conquestEffects, out payment);
                scores = conquestDraw.PlayerScores; winner = conquestDraw.WinnerPlayerId; rng = conquestDraw.RngCursor;
                effects = conquestEffects;
                break;
            case "DRAW": Draw(context.Amount); break;
            case "MINION": case "ROBOT":
                CreateBattlefieldUnitTokensInBase(zones, cards, player, item.SourceObjectId,
                    context.Kind == "MINION" ? "随从" : "机器人", context.Kind == "MINION" ? 1 : 3, context.Amount, item.EffectKind, events);
                break;
            case "GOLD": case "RENATA":
                if (context.Kind == "RENATA") cards[item.SourceObjectId] = source! with { IsExhausted = true };
                for (var i = 0; i < context.Amount; i++)
                    CreateLegendEquipmentToken(zones, cards, player, item.SourceObjectId, item.EffectKind, "金币",
                        [CardObjectTags.EquipmentCard, "金币", "反应"], true, events);
                break;
            case "VEX":
                cards[item.SourceObjectId] = source! with { IsExhausted = true }; Draw(1); break;
            case "CHANNEL_ALL": case "CHANNEL_OPTIONAL":
                foreach (var recipient in context.Kind == "CHANNEL_ALL" ? state.Seats.Keys.Order().ToArray() : new[] { player })
                {
                    var called = CallRunes(zones, cards, recipient, context.Amount);
                    events.Add(new("RUNES_CALLED", "据守技能召出休眠符文", new Dictionary<string, object?> {
                        ["playerId"] = recipient, ["sourceObjectId"] = item.SourceObjectId,
                        ["count"] = called.CalledRuneObjectIds.Count, ["runeObjectIds"] = called.CalledRuneObjectIds }));
                }
                break;
            case "EXPERIENCE":
                experience = GainExperience(experience, player, context.Amount, item, events, item.SourceObjectId, context.CardNo); break;
            case "BOON": case "BOON_ALL":
                foreach (var id in context.Kind == "BOON" ? choices! : HeldUnitsAt(state, field))
                    GrantLegendBoon(cards, id, player, item.SourceObjectId, item.EffectKind, events);
                break;
            case "RETURN_SELF": if (sourceExists) ReturnToHand(item.SourceObjectId); break;
            case "RETURN_PERMANENT": ReturnToHand(choices![0]); break;
            case "RETURN_HERO":
                var hero = choices![0]; zones[player] = zones[player] with { Graveyard = RemoveFromZone(zones[player].Graveyard, hero), ChampionZone = [hero] };
                events.Add(new("HERO_RETURNED_TO_CHAMPION_ZONE", "英雄从废牌堆返回英雄区域", new Dictionary<string,object?> {
                    ["playerId"] = player, ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = hero }));
                break;
            case "MOVE_BASE":
                var target = choices![0]; var controller = cards[target].ControllerId!;
                var origin = state.ObjectLocations[target];
                var equipment = cards.Values.Where(c => c.AttachedToObjectId == target).Select(c => c.ObjectId).ToArray();
                foreach (var owner in zones.Keys.ToArray()) zones[owner] = zones[owner] with { Battlefields = RemoveFromZone(zones[owner].Battlefields, target) };
                zones[controller] = zones[controller] with { Base = zones[controller].Base.Concat([target]).ToArray() };
                cards[target] = cards[target] with { IsAttacking = false, IsDefending = false };
                events.Add(new("UNIT_MOVED_TO_BASE", "据守效果移动单位到其基地", new Dictionary<string,object?> {
                    ["playerId"] = controller, ["sourceObjectId"] = target, ["targetObjectId"] = target,
                    ["effectSourceObjectId"] = item.SourceObjectId, ["originZone"] = "BATTLEFIELD", ["destinationZone"] = "BASE",
                    ["origin"] = "BATTLEFIELD:" + origin.BattlefieldObjectId, ["destination"] = "BASE" }));
                events.AddRange(MoveAttachedEquipmentWithHost(zones, equipment, controller, target, "BASE"));
                var moved = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
                events.AddRange(ResolveUnitMovedCreateDormantGoldTrigger(zones, cards, controller, target, "BATTLEFIELD", "BASE"));
                var resource = BuildJhinMovementResourceTrigger(moved, controller, target, cards[target], "BATTLEFIELD:" + origin.BattlefieldObjectId, "BASE");
                if (resource is not null) { triggers.Add(resource); events.Add(BuildTriggerQueuedEvent(resource)); }
                break;
            case "SCORE":
                var nextScores = scores.ToDictionary(e => e.Key, e => e.Value); nextScores[player] += context.Amount; scores = nextScores;
                if (nextScores[player] >= EffectiveWinningScore(state)) winner = player;
                events.Add(new("SCORE_GAINED", "据守技能获得分数", new Dictionary<string, object?> { ["playerId"] = player, ["amount"] = context.Amount, ["score"] = nextScores[player], ["sourceObjectId"] = item.SourceObjectId }));
                break;
            case "SEVEN_WIN":
                if (HeldUnitsAt(state, field, player).Length >= context.Amount) {
                    winner = player;
                    events.Add(new("MATCH_WON", "宏伟广场据守条件达成", new Dictionary<string,object?> {
                        ["winnerPlayerId"] = player, ["reason"] = "BATTLEFIELD_HELD_SEVEN_UNITS_WIN",
                        ["battlefieldObjectId"] = field, ["requiredUnitCount"] = context.Amount }));
                }
                break;
            case "NEXT_ECHO":
                effects = EchoCostRules.AddBaseCostGrant(effects, player);
                break;
            case "UNIT_TAX": effects = AddUntilEndOfTurnEffect(effects, BuildBattlefieldHeldUnitCostIncreaseEffectId(player, context.Amount)); break;
            default: throw new InvalidOperationException($"Unimplemented captured Hold effect: {context.Kind}");
        }
        return Result();

        void Draw(int count)
        {
            var draw = ApplyDrawToPlayer(state, zones, scores, player, count, rng, events);
            scores = draw.PlayerScores; rng = draw.RngCursor; winner = draw.WinnerPlayerId;
        }
        void ReturnToHand(string id)
        {
            if (!cards.TryGetValue(id, out var card)) return;
            var owner = NonFieldDestinationOwner(zones, card, player);
            DetachEquipmentFromRemovedHost(cards, id);
            foreach (var p in zones.Keys.ToArray()) zones[p] = zones[p] with { Base = RemoveFromZone(zones[p].Base, id),
                Battlefields = RemoveFromZone(zones[p].Battlefields, id), Graveyard = RemoveFromZone(zones[p].Graveyard, id) };
            zones[owner] = zones[owner] with { Hand = zones[owner].Hand.Concat([id]).ToArray() };
            ResetCardOutsidePlay(zones, cards, id, card, owner);
            events.Add(new("CARD_RETURNED_TO_HAND", "据守效果将卡牌返回所属手牌", new Dictionary<string,object?> {
                ["playerId"] = owner, ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id }));
        }
        IReadOnlyDictionary<string, ObjectLocationState> HoldResultLocations()
        {
            var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
            foreach (var created in events.Where(e => e.Kind is "UNIT_TOKEN_CREATED" or "BATTLEFIELD_TOKEN_CREATED"))
                if (created.Payload.TryGetValue("tokenObjectId", out var token) && token is string tokenId
                    && created.Payload.TryGetValue("destinationZone", out var zone) && zone is string destination)
                    locations[tokenId] = new(player, destination, destination == "BATTLEFIELD" ? field : null);
            return locations;
        }
        StackResolutionResult Result() => new(zones, cards, scores, experience, state.RunePools, effects, winner,
            events, [], null, [], null, triggers, rng, PendingCardChoice: pending, PendingPayment: payment,
            ObjectLocations: HoldResultLocations());
    }

    private static bool IsSelectedChampionObjectId(MatchState state, string player, string id, CardObjectState card) =>
        state.PlayerDecklists.TryGetValue(player, out var deck)
        && OfficialCardSourceIdentityGroups.BuildByRepresentativeCardNo([deck.ChampionCardNo])
            .GetValueOrDefault(OfficialCardSourceIdentityGroups.NormalizeCardNo(deck.ChampionCardNo), [deck.ChampionCardNo])
            .Contains(OfficialCardSourceIdentityGroups.NormalizeCardNo(card.CardNo));

    private static ResolutionResult ResolveHeldChoice(MatchState state, PendingCardChoiceState pending, IReadOnlyList<string> choices)
    {
        var context = pending.HeldContext!;
        var item = new StackItemState(pending.ChoiceId, pending.PlayerId, pending.SourceObjectId, pending.EffectKind, context.CardNo) { HeldContext = context };
        var resolved = ResolveHeldStackItem(state, item, choices);
        var next = state with { Tick = state.Tick + 1, PendingCardChoice = resolved.PendingCardChoice, PlayerZones = resolved.PlayerZones,
            PendingPayment = resolved.PendingPayment, TriggerQueue = state.TriggerQueue.Concat(resolved.TriggerQueue).ToArray(),
            CardObjects = resolved.CardObjects, ObjectLocations = resolved.ObjectLocations!, PlayerScores = resolved.PlayerScores,
            PlayerExperience = resolved.PlayerExperience, UntilEndOfTurnEffects = resolved.UntilEndOfTurnEffects,
            RngCursor = resolved.RngCursor, WinnerPlayerId = resolved.WinnerPlayerId,
            Status = resolved.WinnerPlayerId is null ? state.Status : MatchStatuses.Finished };
        if (next.PendingCardChoice is null && next.PendingPayment is null) next = RestoreAfterHeldChoice(next);
        return new(true, null, next, resolved.Events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }
    private static MatchState RestoreAfterHeldChoice(MatchState state)
    {
        var priority = state.StackItems.LastOrDefault()?.ControllerId;
        return state with { ActivePlayerId = priority ?? state.TurnPlayerId, PriorityPlayerId = priority,
            PassedPriorityPlayerIds = [], TimingState = priority is null ? TimingStates.NeutralOpen : TimingStates.NeutralClosed };
    }

    private static ResolutionResult ResolveHeldPayment(MatchState state, PlayerIntent intent, PendingPaymentState pending,
        IReadOnlyList<string> choices, int rawCount)
    {
        if (choices.Count != 1 || rawCount != 1 || choices[0] is not ("PAY" or "DECLINE"))
            return RejectWithCorePrompts(state, "请选择支付或放弃。", ErrorCodes.InvalidTarget);
        var pools = state.RunePools.ToDictionary(e => e.Key, e => e.Value);
        var scores = state.PlayerScores.ToDictionary(e => e.Key, e => e.Value);
        var events = new List<GameEvent>();
        string? winner = null;
        if (choices[0] == "PAY")
        {
            var pool = pools.GetValueOrDefault(intent.PlayerId, RunePool.Empty);
            if (pool.TotalPower < pending.PowerCost)
                return RejectWithCorePrompts(state, "符能不足，可以先使用符文或反应资源技能。", ErrorCodes.InsufficientCost);
            var paid = PayPowerCost(pool, pending.PowerCost, new Dictionary<string, int>());
            pools[intent.PlayerId] = new(pool.Mana, paid.AnyPower, paid.PowerByTrait);
            scores[intent.PlayerId]++;
            if (scores[intent.PlayerId] >= EffectiveWinningScore(state)) winner = intent.PlayerId;
            events.Add(new("POWER_SPENT", "支付据守技能费用", new Dictionary<string, object?> {
                ["playerId"] = intent.PlayerId, ["powerCost"] = pending.PowerCost }));
            events.Add(new("SCORE_GAINED", "据守技能额外获得 1 分", new Dictionary<string, object?> {
                ["playerId"] = intent.PlayerId, ["amount"] = 1, ["score"] = scores[intent.PlayerId],
                ["sourceObjectId"] = pending.HeldContext!.BattlefieldObjectId }));
        }
        else events.Add(new("TRIGGER_PAYMENT_DECLINED", "放弃据守技能的可选支付", new Dictionary<string, object?> { ["playerId"] = intent.PlayerId }));
        var next = RestoreAfterHeldChoice(state with { Tick = state.Tick + 1, PendingPayment = null, RunePools = pools,
            PlayerScores = scores, WinnerPlayerId = winner, Status = winner is null ? state.Status : MatchStatuses.Finished });
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

}

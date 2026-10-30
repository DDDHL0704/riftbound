using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool ValidMovementInstructionShape(MovementObserverContext context)
    {
        var spec = UnitMovedTriggerSpecRules.MovementTriggers(context.CardNo).FirstOrDefault(t => t.Kind == context.Kind);
        if (spec?.MaximumControlledRunes is { } maximum
            ? context.ControlledRunesAtTrigger is not { } count || count < 0 || count > maximum
            : context.ControlledRunesAtTrigger is not null) return false;
        if (context.Kind == TriggerKinds.UnitFirstMovementMillPlayer)
            return context.MovementOrdinal == 1 && context.Mode is null
                && (context.SelectedPlayerId is null || !string.IsNullOrWhiteSpace(context.SelectedPlayerId));
        if (context.SelectedPlayerId is not null) return false;
        return context.Kind == TriggerKinds.UnitMovedAllPlayersDrawOrDiscard
            ? context.Mode is null or "DRAW" or "DISCARD" : context.Mode is null;
    }

    internal static bool NeedsMovementInstructionConfirmation(StackItemState item)
        => item.MovementObserver is { Kind: TriggerKinds.UnitFirstMovementMillPlayer, SelectedPlayerId: null }
            or { Kind: TriggerKinds.UnitMovedAllPlayersDrawOrDiscard, Mode: null };

    private static ResolutionResult PrepareMovementInstruction(ResolutionResult result, StackItemState item)
    {
        var state = result.State; var context = item.MovementObserver!;
        var choosePlayer = context.Kind == TriggerKinds.UnitFirstMovementMillPlayer;
        RuleChoiceOption[] options = choosePlayer
            ? state.Seats.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new RuleChoiceOption(p.Key, p.Value)).ToArray()
            : [new("DISCARD", "每名玩家各弃置一张手牌"), new("DRAW", "每名玩家各抽一张牌")];
        var selected = RuleChoices.Value!.Choose(item.ControllerId, choosePlayer ? "选择燃烧牌堆的玩家，确认后双方可以响应。"
            : "选择本次移动技能的效果，确认后双方可以响应。", options, kind: "MOVEMENT_TRIGGER_CONFIRMATION", preview: state);
        var confirmed = item with { MovementObserver = choosePlayer ? context with { SelectedPlayerId = selected } : context with { Mode = selected } };
        var next = state with { StackItems = state.StackItems.Select(i => i.StackItemId == item.StackItemId ? confirmed : i).ToArray() };
        return PrepareTriggerConfirmation(result with { State = next,
            Events = result.Events.Append(new GameEvent("MOVEMENT_TRIGGER_CHOICE_CONFIRMED", "已确认移动技能的模式或玩家", new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["stackItemId"] = item.StackItemId, ["selection"] = selected })).ToArray(),
            Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) });
    }

    private static StackResolutionResult ResolveMovementInstruction(MatchState state, StackItemState item, TriggerSpec spec, List<GameEvent> events)
    {
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(); var context = item.MovementObserver!;
        var result = NoopStackResolutionResult(state);
        if (context.Kind == TriggerKinds.UnitFirstMovementMillPlayer)
        {
            if (context.SelectedPlayerId is null || !state.Seats.ContainsKey(context.SelectedPlayerId)) throw new InvalidOperationException("Unconfirmed movement player.");
            MillMovementCards(zones, cards, context.SelectedPlayerId, item.SourceObjectId, spec.MillCount!.Value, events);
        }
        else if (context.Kind == TriggerKinds.UnitMovedAllPlayersDrawOrDiscard)
        {
            var players = state.Seats.Keys.OrderBy(p => p == state.TurnPlayerId ? 0 : 1).ThenBy(p => p, StringComparer.Ordinal).ToArray();
            if (context.Mode == "DRAW")
            {
                var scores = state.PlayerScores; var rng = state.RngCursor; var winner = state.WinnerPlayerId;
                foreach (var player in players) {
                    var drawState = state with { PlayerZones = zones, CardObjects = cards, PlayerScores = scores, RngCursor = rng };
                    var draw = ApplyDrawToPlayer(drawState, zones, scores, player, spec.DrawCount!.Value, rng, events, deferNormalVictory: true);
                    scores = draw.PlayerScores; rng = draw.RngCursor; winner = draw.WinnerPlayerId ?? winner;
                }
                result = result with { PlayerScores = scores, RngCursor = rng, WinnerPlayerId = winner };
            }
            else if (context.Mode == "DISCARD")
            {
                // Each player chooses privately before any simultaneous discard
                // is published. The decision checkpoint retains the original hand.
                var chosen = new List<(string Player, string Id)>();
                foreach (var player in players) {
                    var hand = zones[player].Hand.Order(StringComparer.Ordinal).ToList();
                    for (var i = 0; i < Math.Min(spec.DiscardCount!.Value, zones[player].Hand.Count); i++) {
                        var selected = RuleChoices.Value!.Choose(player, "每名玩家各弃置一张手牌。", hand.Select((id, index) =>
                            new RuleChoiceOption("HAND:" + index, cards[id].CardNo is { } no ? OfficialCardSourceIdentityGroups.DisplayName(no) : "手牌", [id])).ToArray(),
                            kind: "PRIVATE_HAND_DISCARD", preview: state);
                        var id = hand[int.Parse(selected["HAND:".Length..], System.Globalization.CultureInfo.InvariantCulture)];
                        chosen.Add((player, id)); hand.Remove(id);
                    }
                }
                var effects = state.UntilEndOfTurnEffects;
                foreach (var group in chosen.GroupBy(c => c.Player)) {
                    var ids = group.Select(c => c.Id).ToArray();
                    foreach (var id in ids) {
                        if (!TryDiscardCardFromHand(zones, cards, group.Key, id)) throw new InvalidOperationException("Movement discard no longer legal.");
                        events.Add(new("CARD_DISCARDED", "移动技能让各玩家弃置手牌", new Dictionary<string, object?> {
                            ["playerId"] = group.Key, ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id, ["destinationZone"] = "GRAVEYARD" }));
                    }
                    ResolveHandCardsDiscardedReadyPowerTriggers(zones, cards, group.Key, "CARD_DISCARDED", item.SourceObjectId, ids, events);
                    effects = MarkPlayerDiscardedHandCardsThisTurn(effects, group.Key, ids, zones);
                }
                result = result with { UntilEndOfTurnEffects = effects };
            }
            else throw new InvalidOperationException("Unconfirmed movement mode.");
        }
        else
        {
            var top = TakeControlledMainDeckPrefix(cards, item.ControllerId, zones[item.ControllerId].MainDeck, spec.MillCount!.Value);
            if (top.Length == spec.MillCount && RuleChoices.Value!.Choose(item.ControllerId, "是否燃烧牌堆，以此提升移动单位本回合的战力？",
                    [new("BURN", "燃烧并提升战力"), new("DECLINE", "放弃")], kind: "MOVEMENT_OPTIONAL_BURN", preview: state) == "BURN")
            {
                MillMovementCards(zones, cards, item.ControllerId, item.SourceObjectId, spec.MillCount.Value, events);
                if (cards.TryGetValue(context.Unit.ObjectId, out var unit) && unit.ObjectGeneration == context.Unit.Generation
                    && FieldObjectTypeRules.IsVisibleUnit(unit) && IsObjectOnField(zones, unit.ObjectId)) {
                    using var instruction = new MightInstructionScope(state, zones, cards, events);
                    var behavior = new CardBehaviorDefinition(context.CardNo, OfficialCardSourceIdentityGroups.DisplayName(context.CardNo), 0, spec.Kind, 0, 0);
                    cards[unit.ObjectId] = ApplyPowerModifier(state, unit, behavior, item, unit.ObjectId, spec.PowerDelta!.Value, out var modified);
                    events.Add(modified);
                }
            }
        }
        return result with { PlayerZones = zones, CardObjects = cards, Events = events,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }

    private static void MillMovementCards(Dictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        string player, string source, int count, List<GameEvent> events)
    {
        var ids = TakeControlledMainDeckPrefix(cards, player, zones[player].MainDeck, count);
        if (ids.Length == 0) return;
        zones[player] = zones[player] with { MainDeck = zones[player].MainDeck.Skip(ids.Length).ToArray(),
            Graveyard = [..zones[player].Graveyard, ..ids] };
        events.Add(new("CARDS_MILLED", "将主牌堆顶牌放入废牌堆", new Dictionary<string, object?> {
            ["playerId"] = player, ["sourceObjectId"] = source, ["cardIds"] = ids, ["count"] = ids.Length,
            ["sourceZone"] = TriggerZones.MainDeck, ["destinationZone"] = TriggerZones.Graveyard }));
    }
}

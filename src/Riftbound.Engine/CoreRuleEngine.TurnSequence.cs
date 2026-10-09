using Riftbound.Contracts;

namespace Riftbound.Engine;

// Captured when the ability triggers. Its controller and battlefield do not follow
// later source control changes, movement, or departure (CN 390–392).
public sealed record HeldTriggerContext(string CardNo, string BattlefieldObjectId, string Kind,
    int Amount = 0, long SourceGeneration = 0, string? SelectedCostObjectId = null);
public sealed record DelayedResourceGain(string Id, string ControllerId, string SourceObjectId,
    string SourceCardNo, string BattlefieldObjectId, int DueTurn, int Power);

public sealed partial class CoreRuleEngine
{
    private static ResolutionResult ResolveTurnStart(MatchState state)
    {
        var next = state with { Tick = state.Tick + 1, Phase = MatchPhases.TurnStart,
            TurnStartStep = state.TurnStartStep ?? "READY", ActivePlayerId = state.TurnPlayerId };
        return new(true, null, next, [new("TURN_START_BEGAN", "开始回合流程",
            new Dictionary<string, object?> { ["turnPlayerId"] = state.TurnPlayerId })],
            ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static bool TurnSequenceBlocked(MatchState state) => state.StackItems.Count > 0
        || state.TriggerQueue.Any(IsImmediateTrigger) || state.PendingPayment is not null
        || state.PendingHandChoice is not null || state.PendingCardChoice is not null || state.PendingEffectPlay is not null;

    private static ResolutionResult AdvanceTurnStartSequence(ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var state = result.State;
        var events = result.Events.ToList();
        while (state.Status == MatchStatuses.InProgress && state.TurnStartStep is not null && !TurnSequenceBlocked(state))
        {
            var stepStart = state;
            var stepEventIndex = events.Count;
            var player = state.TurnPlayerId;
            var zones = NormalizeZonesForSeats(state);
            var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
            switch (state.TurnStartStep)
            {
                case "READY":
                    events.AddRange(ReadyTurnPlayerObjectsAtTurnStart(zones, cards, player).Events);
                    state = state with { CardObjects = cards, TurnStartStep = "START" };
                    break;
                case "START":
                    var ephemeral = DestroyEphemeralObjectsAtTurnStart(zones, cards, state.ObjectLocations, player, state.Tick);
                    var damage = ApplyBattlefieldTurnStartDamageAllUnits(zones, cards, state.ObjectLocations, player, state.Tick, state.RunePools);
                    var startDraw = ApplyBattlefieldTurnStartDestroyUnitDraw(state with { PlayerZones = zones, CardObjects = cards },
                        zones, cards, player, state.RngCursor);
                    events.AddRange(ephemeral.Events); events.AddRange(damage.Events); events.AddRange(startDraw.Events);
                    state = state with { PlayerZones = zones, CardObjects = cards, RunePools = damage.RunePools.Count == 0 ? state.RunePools : damage.RunePools,
                        PlayerScores = startDraw.PlayerScores, WinnerPlayerId = startDraw.WinnerPlayerId,
                        Status = startDraw.WinnerPlayerId is null ? state.Status : MatchStatuses.Finished,
                        RngCursor = startDraw.RngCursor, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
                        DestroyedUnitOwnerIdsThisTurn = ephemeral.DestroyedUnitOwnerIds.Concat(damage.DestroyedUnitOwnerIds)
                            .Concat(startDraw.DestroyedUnitOwnerIds).Distinct().ToArray(),
                        TriggerQueue = state.TriggerQueue.Concat(damage.TriggerQueue).ToArray(), TurnStartStep = "SCORE" };
                    break;
                case "SCORE":
                    var firstScore = ApplyBattlefieldFirstTurnScore(state, player);
                    events.AddRange(firstScore.Events);
                    state = state with { PlayerScores = firstScore.PlayerScores, WinnerPlayerId = firstScore.WinnerPlayerId,
                        UntilEndOfTurnEffects = firstScore.UntilEndOfTurnEffects };
                    if (state.WinnerPlayerId is null)
                    {
                        // Capture before scoring changes anything. Prevented/replaced scoring still causes Hold.
                        var heldFields = zones.Values.SelectMany(z => z.Battlefields).Distinct(StringComparer.Ordinal)
                            .Where(id => cards.TryGetValue(id, out var card) && IsBattlefieldCardObject(card)
                                && EffectiveFieldControllerId(zones, id, card) == player
                                && !BattlefieldScoredThisTurn(state.UntilEndOfTurnEffects, id, player)).ToArray();
                        var heldScore = ApplyBattlefieldHeldScoresAtTurnStart(state, zones, cards, player);
                        events.AddRange(heldScore.Events);
                        state = state with { PlayerScores = heldScore.PlayerScores, WinnerPlayerId = heldScore.WinnerPlayerId,
                            UntilEndOfTurnEffects = heldScore.UntilEndOfTurnEffects, PlayerZones = zones, CardObjects = cards };
                        if (state.WinnerPlayerId is null)
                            state = QueueHeldAbilities(state, heldFields, events);
                    }
                    state = state with { TurnStartStep = "CHANNEL", Status = state.WinnerPlayerId is null ? state.Status : MatchStatuses.Finished };
                    break;
                case "CHANNEL":
                    var own = zones[player];
                    var runes = TakeControlledRuneDeckPrefix(cards, player, own.RuneDeck, RuneCallCount(state));
                    zones[player] = own with { RuneDeck = own.RuneDeck.Skip(runes.Length).ToArray(), Base = own.Base.Concat(runes).ToArray() };
                    var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
                    foreach (var id in runes) locations[id] = new(player, MoveUnitBaseZone);
                    events.Add(new("RUNES_CALLED", $"召出 {runes.Length} 张符文", new Dictionary<string, object?> { ["playerId"] = player, ["count"] = runes.Length }));
                    state = state with { PlayerZones = zones, ObjectLocations = locations, TurnStartStep = "DRAW" };
                    break;
                case "DRAW":
                    var draw = DrawOne(state, player, zones[player]);
                    zones[player] = zones[player] with { MainDeck = draw.MainDeck, Graveyard = draw.Graveyard,
                        Hand = zones[player].Hand.Concat(draw.DrawnCards).ToArray() };
                    events.AddRange(BuildTurnStartEvents(state, 0, draw, []).Where(e => e.Kind is not
                        ("TURN_START_BEGAN" or "RUNES_CALLED" or "RUNE_POOL_CLEARED" or "MAIN_PHASE_BEGAN")));
                    state = state with { PlayerZones = zones, PlayerScores = draw.PlayerScores, RngCursor = draw.RngCursor,
                        WinnerPlayerId = draw.WinnerPlayerId, Status = draw.WinnerPlayerId is null ? state.Status : MatchStatuses.Finished,
                        ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), TurnStartStep = "MAIN" };
                    // This existing Jinx family is kept separate from Hold. Its full start-trigger ordering is audited separately.
                    if (state.WinnerPlayerId is null && TryGetJinxTurnStartDrawCardNo(zones, cards, player, out var jinx))
                    {
                        events.Add(new("LEGEND_TRIGGER_RESOLVED", "暴走萝莉抽牌", new Dictionary<string, object?> {
                            ["playerId"] = player, ["legendCardNo"] = jinx, ["trigger"] = "JINX_TURN_START_DRAW_IF_HAND_BELOW_TWO" }));
                        var extra = ApplyDrawToPlayer(state, zones, state.PlayerScores, player, 1, state.RngCursor, events);
                        state = state with { PlayerZones = zones, PlayerScores = extra.PlayerScores, RngCursor = extra.RngCursor,
                            WinnerPlayerId = extra.WinnerPlayerId, Status = extra.WinnerPlayerId is null ? state.Status : MatchStatuses.Finished,
                            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
                    }
                    break;
                case "MAIN":
                    var pools = ClearRunePools(state).ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
                    events.Add(new("RUNE_POOL_CLEARED", "所有玩家的符文池已清空", new Dictionary<string, object?> { ["playerIds"] = state.Seats.Keys.Order().ToArray() }));
                    events.Add(new("MAIN_PHASE_BEGAN", "进入主阶段", new Dictionary<string, object?> { ["turnPlayerId"] = player }));
                    var due = state.DelayedResourceGains.Where(d => d.ControllerId == player && d.DueTurn <= state.TurnNumber).ToArray();
                    foreach (var delayed in due)
                    {
                        var pool = pools.GetValueOrDefault(delayed.ControllerId, RunePool.Empty);
                        pools[delayed.ControllerId] = pool with { Power = pool.Power + delayed.Power };
                        events.Add(new("POWER_GAINED", "延迟资源技能获得符能", new Dictionary<string, object?> {
                            ["playerId"] = delayed.ControllerId, ["sourceObjectId"] = delayed.SourceObjectId,
                            ["sourceCardNo"] = delayed.SourceCardNo, ["triggerId"] = delayed.Id,
                            ["generatedPower"] = delayed.Power, ["power"] = delayed.Power, ["resourceLifecycle"] = "rune-pool" }));
                    }
                    state = state with { RunePools = pools, DelayedResourceGains = state.DelayedResourceGains.Except(due).ToArray(),
                        TemporaryPaymentResources = [], TurnStartStep = null, Phase = MatchPhases.Main,
                        TimingState = TimingStates.NeutralOpen, ActivePlayerId = player, PriorityPlayerId = null,
                        PassedPriorityPlayerIds = [], FocusPlayerId = null };
                    var advance = AdvancePendingBattlefieldTasksAfterStateChange(state, player, result.State);
                    state = advance.State; events.AddRange(advance.Events);
                    break;
                default: throw new InvalidOperationException($"Unknown turn-start continuation: {state.TurnStartStep}");
            }
            var collected = RecordDrawTriggers(stepStart, new(true, null, state, events.Skip(stepEventIndex).ToArray(), ResolutionResult.BuildSnapshots(state), BuildCorePrompts(state)));
            events.RemoveRange(stepEventIndex, events.Count - stepEventIndex); events.AddRange(collected.Events);
            var published = PrepareTriggerConfirmation(PublishPendingTriggers(collected with { Events = [] }));
            state = published.State; events.AddRange(published.Events);
        }
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }
}

using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed class CoreRuleEngine : IRuleEngine
{
    private const int WinningScore = 8;

    private readonly IRuleEngine fallback = new PlaceholderRuleEngine();

    public ValueTask<ResolutionResult> ResolveAsync(
        MatchState state,
        PlayerIntent intent,
        GameCommand command,
        CancellationToken cancellationToken)
    {
        if (command is EndTurnCommand
            && string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(ResolveEndTurn(state, intent));
        }

        if (string.Equals(state.Phase, MatchPhases.TurnStart, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(ResolveTurnStart(state));
        }

        if (command is PlayCardCommand playCardCommand)
        {
            return ValueTask.FromResult(ResolvePlayCard(state, intent, playCardCommand));
        }

        if (command is PassPriorityCommand && CanPassPriority(state, intent.PlayerId))
        {
            return ValueTask.FromResult(ResolvePassPriority(state, intent));
        }

        if (command is PassFocusCommand && CanPassFocus(state, intent.PlayerId))
        {
            return ValueTask.FromResult(ResolvePassFocus(state, intent));
        }

        if (command is PassPriorityCommand
            && string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            && string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(RejectWithCorePrompts(
                state,
                "PASS_PRIORITY is not allowed without an open priority window.",
                ErrorCodes.PhaseNotAllowed));
        }

        if (command is PassFocusCommand)
        {
            return ValueTask.FromResult(RejectWithCorePrompts(
                state,
                "PASS_FOCUS is not allowed without an open spell duel focus window.",
                ErrorCodes.PhaseNotAllowed));
        }

        return fallback.ResolveAsync(state, intent, command, cancellationToken);
    }

    private static ResolutionResult ResolvePlayCard(
        MatchState state,
        PlayerIntent intent,
        PlayCardCommand command)
    {
        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || !string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || !string.Equals(state.TurnPlayerId, intent.PlayerId, StringComparison.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "PLAY_CARD is not allowed outside the turn player's ordinary open main phase.",
                ErrorCodes.PhaseNotAllowed);
        }

        if (!CardBehaviorRegistry.TryGetByCardNo(command.CardNo, out var behavior))
        {
            return RejectWithCorePrompts(
                state,
                $"Unsupported card behavior: {command.CardNo}",
                ErrorCodes.UnsupportedCardBehavior);
        }

        if (!state.PlayerZones.TryGetValue(intent.PlayerId, out var zones)
            || !zones.Hand.Contains(command.SourceObjectId, StringComparer.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "Source card is not in the player's hand.",
                ErrorCodes.CardNotInHand);
        }

        var targetObjectId = command.TargetObjectIds.Count == 1
            ? command.TargetObjectIds[0]
            : string.Empty;
        if (command.TargetObjectIds.Count != behavior.RequiredTargetCount
            || !IsBattlefieldObject(state, targetObjectId))
        {
            return RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires exactly one battlefield unit target.",
                ErrorCodes.InvalidTarget);
        }

        var runePools = state.RunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var currentPool = runePools.TryGetValue(intent.PlayerId, out var runePool) ? runePool : RunePool.Empty;
        if (currentPool.Mana < behavior.ManaCost)
        {
            return RejectWithCorePrompts(
                state,
                $"Not enough mana to play {behavior.DisplayName}.",
                ErrorCodes.InsufficientCost);
        }

        runePools[intent.PlayerId] = currentPool with
        {
            Mana = currentPool.Mana - behavior.ManaCost
        };

        var playerZones = NormalizeZonesForSeats(state);
        playerZones[intent.PlayerId] = zones with
        {
            Hand = zones.Hand
                .Where(cardId => !string.Equals(cardId, command.SourceObjectId, StringComparison.Ordinal))
                .ToArray()
        };

        var stackItem = new StackItemState(
            $"STACK-{state.Tick + 1}-{command.SourceObjectId}",
            intent.PlayerId,
            command.SourceObjectId,
            behavior.EffectKind,
            command.CardNo,
            [targetObjectId],
            behavior.DamageAmount);
        var nextState = state with
        {
            Tick = state.Tick + 1,
            ActivePlayerId = intent.PlayerId,
            TimingState = TimingStates.NeutralClosed,
            RunePools = runePools,
            PlayerZones = playerZones,
            PriorityPlayerId = intent.PlayerId,
            PassedPriorityPlayerIds = [],
            StackItems = state.StackItems.Concat([stackItem]).ToArray()
        };

        var events = new[]
        {
            new GameEvent(
                "CARD_PLAYED",
                $"{intent.PlayerId} 打出{behavior.DisplayName}",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["cardNo"] = command.CardNo
                }),
            new GameEvent(
                "COST_PAID",
                $"{intent.PlayerId} 支付 {behavior.ManaCost} 点费用",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["mana"] = behavior.ManaCost
                }),
            new GameEvent(
                "STACK_ITEM_ADDED",
                $"{behavior.DisplayName}加入结算链",
                new Dictionary<string, object?>
                {
                    ["stackItemId"] = stackItem.StackItemId,
                    ["controllerId"] = stackItem.ControllerId,
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["cardNo"] = stackItem.CardNo,
                    ["targetObjectIds"] = stackItem.TargetObjectIds.ToArray(),
                    ["effectKind"] = stackItem.EffectKind
                })
        };

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static bool CanPassPriority(MatchState state, string playerId)
    {
        return string.Equals(state.Status, MatchStatuses.InProgress, StringComparison.Ordinal)
            && state.StackItems.Count > 0
            && !string.IsNullOrWhiteSpace(state.PriorityPlayerId)
            && string.Equals(state.PriorityPlayerId, playerId, StringComparison.Ordinal);
    }

    private static bool CanPassFocus(MatchState state, string playerId)
    {
        return string.Equals(state.Status, MatchStatuses.InProgress, StringComparison.Ordinal)
            && state.StackItems.Count == 0
            && string.Equals(state.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(state.FocusPlayerId)
            && string.Equals(state.FocusPlayerId, playerId, StringComparison.Ordinal);
    }

    private static ResolutionResult ResolvePassPriority(MatchState state, PlayerIntent intent)
    {
        var passedPlayerIds = state.PassedPriorityPlayerIds
            .Concat([intent.PlayerId])
            .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        var seatPlayerIds = SeatPlayerIds(state);
        var events = new List<GameEvent>
        {
            new(
                "PRIORITY_PASSED",
                $"{intent.PlayerId} 让过优先行动权",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["priorityPlayerId"] = state.PriorityPlayerId
                })
        };

        MatchState nextState;
        if (seatPlayerIds.All(passedPlayerIds.Contains))
        {
            var resolvedItem = state.StackItems[^1];
            var remainingStack = state.StackItems.Take(state.StackItems.Count - 1).ToArray();
            var nextPriorityPlayerId = remainingStack.Length == 0
                ? null
                : remainingStack[^1].ControllerId;
            var stackResolution = ResolveStackItemEffect(state, resolvedItem);
            nextState = state with
            {
                Tick = state.Tick + 1,
                ActivePlayerId = nextPriorityPlayerId ?? state.TurnPlayerId,
                TimingState = remainingStack.Length == 0 ? TimingStates.NeutralOpen : state.TimingState,
                PriorityPlayerId = nextPriorityPlayerId,
                PassedPriorityPlayerIds = [],
                StackItems = remainingStack,
                PlayerZones = stackResolution.PlayerZones,
                CardObjects = stackResolution.CardObjects
            };
            events.Add(new GameEvent(
                "STACK_ITEM_RESOLVED",
                $"{resolvedItem.StackItemId} 结算",
                new Dictionary<string, object?>
                {
                    ["stackItemId"] = resolvedItem.StackItemId,
                    ["controllerId"] = resolvedItem.ControllerId,
                    ["sourceObjectId"] = resolvedItem.SourceObjectId,
                    ["effectKind"] = resolvedItem.EffectKind
                }));
            events.AddRange(stackResolution.Events);
        }
        else
        {
            var nextPriorityPlayerId = NextUnpassedPlayerId(state, intent.PlayerId, passedPlayerIds);
            nextState = state with
            {
                Tick = state.Tick + 1,
                ActivePlayerId = nextPriorityPlayerId,
                PriorityPlayerId = nextPriorityPlayerId,
                PassedPriorityPlayerIds = passedPlayerIds
                    .OrderBy(playerId => playerId, StringComparer.Ordinal)
                    .ToArray()
            };
        }

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static ResolutionResult ResolvePassFocus(MatchState state, PlayerIntent intent)
    {
        var passedPlayerIds = state.PassedFocusPlayerIds
            .Concat([intent.PlayerId])
            .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        var seatPlayerIds = SeatPlayerIds(state);
        var events = new List<GameEvent>
        {
            new(
                "FOCUS_PASSED",
                $"{intent.PlayerId} 让过焦点",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["focusPlayerId"] = state.FocusPlayerId
                })
        };

        MatchState nextState;
        if (seatPlayerIds.All(passedPlayerIds.Contains))
        {
            nextState = state with
            {
                Tick = state.Tick + 1,
                ActivePlayerId = state.TurnPlayerId,
                TimingState = TimingStates.NeutralOpen,
                FocusPlayerId = null,
                PassedFocusPlayerIds = []
            };
            events.Add(new GameEvent(
                "SPELL_DUEL_CLOSED",
                "所有玩家让过焦点，法术对决关闭",
                new Dictionary<string, object?>
                {
                    ["turnPlayerId"] = state.TurnPlayerId
                }));
        }
        else
        {
            var nextFocusPlayerId = NextUnpassedPlayerId(state, intent.PlayerId, passedPlayerIds);
            nextState = state with
            {
                Tick = state.Tick + 1,
                ActivePlayerId = nextFocusPlayerId,
                FocusPlayerId = nextFocusPlayerId,
                PassedFocusPlayerIds = passedPlayerIds
                    .OrderBy(playerId => playerId, StringComparer.Ordinal)
                    .ToArray()
            };
        }

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static ResolutionResult ResolveEndTurn(MatchState state, PlayerIntent intent)
    {
        var nextPlayerId = NextPlayerId(state);
        var cleanupResult = ApplyTurnEndCleanup(state);
        var nextTurnState = state with
        {
            TurnNumber = state.TurnNumber + 1,
            ActivePlayerId = nextPlayerId,
            TurnPlayerId = nextPlayerId,
            Phase = MatchPhases.TurnStart,
            TimingState = TimingStates.NeutralClosed,
            RunePools = ClearRunePools(state),
            CardObjects = cleanupResult.CardObjects
        };
        var turnStartResult = ResolveTurnStart(nextTurnState);
        var events = BuildTurnEndEvents(state, intent.PlayerId, nextPlayerId, cleanupResult)
            .Concat(turnStartResult.Events)
            .ToArray();

        return turnStartResult with
        {
            Events = events
        };
    }

    private static ResolutionResult ResolveTurnStart(MatchState state)
    {
        var turnPlayerId = state.TurnPlayerId;
        var playerZones = NormalizeZonesForSeats(state);
        var currentZones = playerZones.TryGetValue(turnPlayerId, out var zones)
            ? zones
            : PlayerZones.Empty;

        var calledRuneTarget = RuneCallCount(state);
        var calledRunes = currentZones.RuneDeck.Take(calledRuneTarget).ToArray();
        var remainingRuneDeck = currentZones.RuneDeck.Skip(calledRunes.Length).ToArray();
        var drawResult = DrawOne(state, turnPlayerId, currentZones);

        playerZones[turnPlayerId] = currentZones with
        {
            MainDeck = drawResult.MainDeck,
            RuneDeck = remainingRuneDeck,
            Hand = currentZones.Hand.Concat(drawResult.DrawnCards).ToArray(),
            Graveyard = drawResult.Graveyard,
            Base = currentZones.Base.Concat(calledRunes).ToArray()
        };

        var nextState = state with
        {
            Tick = state.Tick + 1,
            ActivePlayerId = turnPlayerId,
            Status = drawResult.WinnerPlayerId is null ? state.Status : MatchStatuses.Finished,
            Phase = drawResult.WinnerPlayerId is null ? MatchPhases.Main : state.Phase,
            TimingState = drawResult.WinnerPlayerId is null ? TimingStates.NeutralOpen : state.TimingState,
            RunePools = drawResult.WinnerPlayerId is null ? ClearRunePools(state) : state.RunePools,
            PlayerZones = playerZones,
            PlayerScores = drawResult.PlayerScores,
            WinnerPlayerId = drawResult.WinnerPlayerId
        };

        return new ResolutionResult(
            true,
            null,
            nextState,
            BuildTurnStartEvents(state, calledRunes.Length, drawResult),
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static ResolutionResult RejectWithCorePrompts(
        MatchState state,
        string error,
        string errorCode)
    {
        return new ResolutionResult(
            false,
            error,
            state,
            [],
            ResolutionResult.BuildSnapshots(state),
            BuildCorePrompts(state),
            errorCode);
    }

    private static Dictionary<string, PlayerZones> NormalizeZonesForSeats(MatchState state)
    {
        return state.Seats.Keys.ToDictionary(
            playerId => playerId,
            playerId => state.PlayerZones.TryGetValue(playerId, out var zones) ? zones : PlayerZones.Empty,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, RunePool> ClearRunePools(MatchState state)
    {
        return state.Seats.Keys.ToDictionary(
            playerId => playerId,
            _ => RunePool.Empty,
            StringComparer.Ordinal);
    }

    private static bool IsBattlefieldObject(MatchState state, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.PlayerZones.Values.Any(zones => zones.Battlefields.Contains(objectId, StringComparer.Ordinal));
    }

    private static StackResolutionResult ResolveStackItemEffect(MatchState state, StackItemState stackItem)
    {
        if (!CardBehaviorRegistry.TryGetByEffectKind(stackItem.EffectKind, out var behavior)
            || stackItem.TargetObjectIds.Count != 1
            || stackItem.DamageAmount <= 0)
        {
            return new StackResolutionResult(state.PlayerZones, state.CardObjects, []);
        }

        var targetObjectId = stackItem.TargetObjectIds[0];
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
            ? existingTarget
            : new CardObjectState(targetObjectId);
        cardObjects[targetObjectId] = targetState with
        {
            Damage = targetState.Damage + stackItem.DamageAmount
        };

        var playerZones = NormalizeZonesForSeats(state);
        if (playerZones.TryGetValue(stackItem.ControllerId, out var controllerZones)
            && !controllerZones.Graveyard.Contains(stackItem.SourceObjectId, StringComparer.Ordinal))
        {
            playerZones[stackItem.ControllerId] = controllerZones with
            {
                Graveyard = controllerZones.Graveyard.Concat([stackItem.SourceObjectId]).ToArray()
            };
        }

        return new StackResolutionResult(
            playerZones,
            cardObjects,
            [
                new GameEvent(
                    "DAMAGE_APPLIED",
                    $"{behavior.DisplayName}造成 {stackItem.DamageAmount} 点伤害",
                    new Dictionary<string, object?>
                    {
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["targetObjectId"] = targetObjectId,
                        ["damage"] = stackItem.DamageAmount
                    })
            ]);
    }

    private static string NextPlayerId(MatchState state)
    {
        var players = SeatPlayerIds(state);
        if (players.Length == 0)
        {
            return state.TurnPlayerId;
        }

        var turnPlayerIndex = Array.IndexOf(players, state.TurnPlayerId);
        if (turnPlayerIndex < 0)
        {
            return players[0];
        }

        return players[(turnPlayerIndex + 1) % players.Length];
    }

    private static string[] SeatPlayerIds(MatchState state)
    {
        return state.Seats
            .OrderBy(entry => entry.Value, StringComparer.Ordinal)
            .Select(entry => entry.Key)
            .ToArray();
    }

    private static string NextUnpassedPlayerId(
        MatchState state,
        string currentPlayerId,
        IReadOnlySet<string> passedPlayerIds)
    {
        var players = SeatPlayerIds(state);
        if (players.Length == 0)
        {
            return currentPlayerId;
        }

        var currentIndex = Array.IndexOf(players, currentPlayerId);
        if (currentIndex < 0)
        {
            return players.First(playerId => !passedPlayerIds.Contains(playerId));
        }

        for (var offset = 1; offset <= players.Length; offset++)
        {
            var candidate = players[(currentIndex + offset) % players.Length];
            if (!passedPlayerIds.Contains(candidate))
            {
                return candidate;
            }
        }

        return currentPlayerId;
    }

    private static CleanupResult ApplyTurnEndCleanup(MatchState state)
    {
        var damagedObjectIds = new List<string>();
        var expiredEffectIds = new List<string>();
        var cardObjects = state.CardObjects.ToDictionary(
            entry => entry.Key,
            entry =>
            {
                var objectState = entry.Value;
                var untilEndEffects = objectState.UntilEndOfTurnEffects
                    .Where(effectId => !string.IsNullOrWhiteSpace(effectId))
                    .ToArray();
                if (objectState.Damage <= 0 && untilEndEffects.Length == 0)
                {
                    return objectState;
                }

                if (objectState.Damage > 0)
                {
                    damagedObjectIds.Add(entry.Key);
                }
                expiredEffectIds.AddRange(untilEndEffects);

                return objectState with
                {
                    Damage = 0,
                    UntilEndOfTurnEffects = []
                };
            },
            StringComparer.Ordinal);

        return new CleanupResult(
            cardObjects,
            damagedObjectIds.OrderBy(objectId => objectId, StringComparer.Ordinal).ToArray(),
            expiredEffectIds.Distinct(StringComparer.Ordinal).OrderBy(effectId => effectId, StringComparer.Ordinal).ToArray(),
            damagedObjectIds.Count > 0 || expiredEffectIds.Count > 0);
    }

    private static int RuneCallCount(MatchState state)
    {
        return IsSecondActionPlayersFirstTurn(state) ? 3 : 2;
    }

    private static bool IsSecondActionPlayersFirstTurn(MatchState state)
    {
        return state.TurnNumber == 2
            && state.Seats.TryGetValue(state.TurnPlayerId, out var seat)
            && string.Equals(seat, "P2", StringComparison.Ordinal);
    }

    private static DrawResult DrawOne(MatchState state, string playerId, PlayerZones zones)
    {
        var playerScores = NormalizeScoresForSeats(state);
        var mainDeck = zones.MainDeck.ToList();
        var graveyard = zones.Graveyard.ToList();
        var drawnCards = new List<string>();
        var burnouts = new List<BurnoutResult>();
        string? winnerPlayerId = null;
        var remainingDrawCount = 1;

        while (remainingDrawCount > 0 && winnerPlayerId is null)
        {
            if (mainDeck.Count > 0)
            {
                drawnCards.Add(mainDeck[0]);
                mainDeck.RemoveAt(0);
                remainingDrawCount--;
                continue;
            }

            var opponentId = OpponentOf(state, playerId);
            if (opponentId is null)
            {
                break;
            }

            if (graveyard.Count > 0)
            {
                mainDeck.AddRange(graveyard);
                graveyard.Clear();
            }

            playerScores[opponentId] = playerScores.TryGetValue(opponentId, out var score) ? score + 1 : 1;
            burnouts.Add(new BurnoutResult(opponentId, playerScores[opponentId]));
            winnerPlayerId = WinningPlayerId(playerScores);
        }

        return new DrawResult(
            mainDeck.ToArray(),
            graveyard.ToArray(),
            drawnCards.ToArray(),
            burnouts,
            winnerPlayerId,
            playerScores);
    }

    private static string? WinningPlayerId(IReadOnlyDictionary<string, int> playerScores)
    {
        return playerScores
            .Where(candidate => candidate.Value >= WinningScore
                && playerScores
                    .Where(other => !string.Equals(other.Key, candidate.Key, StringComparison.Ordinal))
                    .All(other => candidate.Value > other.Value))
            .Select(candidate => candidate.Key)
            .FirstOrDefault();
    }

    private static Dictionary<string, int> NormalizeScoresForSeats(MatchState state)
    {
        return state.Seats.Keys.ToDictionary(
            playerId => playerId,
            playerId => state.PlayerScores.TryGetValue(playerId, out var score) ? score : 0,
            StringComparer.Ordinal);
    }

    private static string? OpponentOf(MatchState state, string playerId)
    {
        return state.Seats
            .OrderBy(entry => entry.Value, StringComparer.Ordinal)
            .Select(entry => entry.Key)
            .FirstOrDefault(candidate => !string.Equals(candidate, playerId, StringComparison.Ordinal));
    }

    private static IReadOnlyDictionary<string, ActionPromptDto> BuildCorePrompts(MatchState state)
    {
        if (state.Status != MatchStatuses.InProgress)
        {
            return ResolutionResult.BuildPrompts(state);
        }

        if (state.StackItems.Count > 0 && !string.IsNullOrWhiteSpace(state.PriorityPlayerId))
        {
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => new ActionPromptDto(
                playerId,
                string.Equals(playerId, state.PriorityPlayerId, StringComparison.Ordinal),
                string.Equals(playerId, state.PriorityPlayerId, StringComparison.Ordinal)
                    ? "当前玩家可让过优先行动权"
                    : "等待对手优先行动",
                string.Equals(playerId, state.PriorityPlayerId, StringComparison.Ordinal)
                    ? ["PASS_PRIORITY"]
                    : ["WAIT"]));
        }

        if (string.Equals(state.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(state.FocusPlayerId))
        {
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => new ActionPromptDto(
                playerId,
                string.Equals(playerId, state.FocusPlayerId, StringComparison.Ordinal),
                string.Equals(playerId, state.FocusPlayerId, StringComparison.Ordinal)
                    ? "当前玩家可让过焦点"
                    : "等待对手焦点行动",
                string.Equals(playerId, state.FocusPlayerId, StringComparison.Ordinal)
                    ? ["PASS_FOCUS"]
                    : ["WAIT"]));
        }

        return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => new ActionPromptDto(
            playerId,
            playerId == state.ActivePlayerId,
            playerId == state.ActivePlayerId ? "当前玩家普通开环行动" : "等待对手行动",
            playerId == state.ActivePlayerId ? ["END_TURN"] : ["WAIT"]));
    }

    private static IReadOnlyList<GameEvent> BuildTurnEndEvents(
        MatchState state,
        string playerId,
        string nextTurnPlayerId,
        CleanupResult cleanupResult)
    {
        List<GameEvent> events =
        [
            new GameEvent(
                "TURN_END_DECLARED",
                $"{playerId} 声明结束回合",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["turnPlayerId"] = state.TurnPlayerId
                }),
            new GameEvent(
                "TURN_END_CLEANUP_STARTED",
                $"{state.TurnPlayerId} 回合结束特殊清理开始",
                new Dictionary<string, object?>
                {
                    ["turnPlayerId"] = state.TurnPlayerId,
                    ["phase"] = state.Phase
                })
        ];

        if (cleanupResult.DamagedObjectIds.Count > 0)
        {
            events.Add(new GameEvent(
                "DAMAGE_REMOVED",
                "回合结束特殊清理移除单位伤害",
                new Dictionary<string, object?>
                {
                    ["objectIds"] = cleanupResult.DamagedObjectIds.ToArray(),
                    ["count"] = cleanupResult.DamagedObjectIds.Count
                }));
        }

        if (cleanupResult.ExpiredEffectIds.Count > 0)
        {
            events.Add(new GameEvent(
                "UNTIL_END_OF_TURN_EXPIRED",
                "期限为本回合内的效果同时失效",
                new Dictionary<string, object?>
                {
                    ["effectIds"] = cleanupResult.ExpiredEffectIds.ToArray(),
                    ["count"] = cleanupResult.ExpiredEffectIds.Count
                }));
        }

        events.Add(new GameEvent(
            "RUNE_POOL_CLEARED",
            "回合结束时所有玩家的符文池已清空",
            new Dictionary<string, object?>
            {
                ["playerIds"] = state.Seats.Keys.ToArray(),
                ["timing"] = MatchPhases.TurnEnd
            }));

        if (cleanupResult.RequiresFollowUpCleanup)
        {
            events.Add(new GameEvent(
                "CLEANUP_REPEATED",
                "特殊清理造成状态变化后进行一次常规清理检查",
                new Dictionary<string, object?>
                {
                    ["cleanupKind"] = "NORMAL",
                    ["previousCleanupKind"] = "TURN_END_SPECIAL"
                }));
        }

        events.Add(new GameEvent(
                "TURN_PLAYER_ADVANCED",
                $"回合推进至 {nextTurnPlayerId}",
                new Dictionary<string, object?>
                {
                    ["previousTurnPlayerId"] = state.TurnPlayerId,
                    ["turnPlayerId"] = nextTurnPlayerId,
                    ["turnNumber"] = state.TurnNumber + 1
                }));

        return events;
    }

    private static IReadOnlyList<GameEvent> BuildTurnStartEvents(
        MatchState state,
        int calledRuneCount,
        DrawResult drawResult)
    {
        List<GameEvent> events =
        [
            new GameEvent(
                "TURN_START_BEGAN",
                $"{state.TurnPlayerId} 开始回合开始流程",
                new Dictionary<string, object?>
                {
                    ["turnPlayerId"] = state.TurnPlayerId
                }),
            new GameEvent(
                "RUNES_CALLED",
                $"{state.TurnPlayerId} 召出 {calledRuneCount} 张符文",
                new Dictionary<string, object?>
                {
                    ["playerId"] = state.TurnPlayerId,
                    ["count"] = calledRuneCount
                })
        ];

        foreach (var (burnout, index) in drawResult.Burnouts.Select((burnout, index) => (burnout, index)))
        {
            events.Add(new GameEvent(
                "BURNOUT_APPLIED",
                $"{state.TurnPlayerId} 执行燃尽",
                new Dictionary<string, object?>
                {
                    ["playerId"] = state.TurnPlayerId,
                    ["scoredPlayerId"] = burnout.ScoredPlayerId,
                    ["scoredPlayerScore"] = burnout.ScoredPlayerScore,
                    ["burnoutIndex"] = index + 1
                }));
        }

        if (drawResult.WinnerPlayerId is not null)
        {
            events.Add(new GameEvent(
                "MATCH_WON",
                $"{drawResult.WinnerPlayerId} 达到获胜分数并获胜",
                new Dictionary<string, object?>
                {
                    ["winnerPlayerId"] = drawResult.WinnerPlayerId,
                    ["winningScore"] = WinningScore
                }));
            return events;
        }

        events.Add(new GameEvent(
            "CARD_DRAWN",
            $"{state.TurnPlayerId} 抽 {drawResult.DrawnCards.Count} 张牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = state.TurnPlayerId,
                ["count"] = drawResult.DrawnCards.Count
            }));
        events.Add(new GameEvent(
            "RUNE_POOL_CLEARED",
            "所有玩家的符文池已清空",
            new Dictionary<string, object?>
            {
                ["playerIds"] = state.Seats.Keys.ToArray()
            }));
        events.Add(new GameEvent(
            "MAIN_PHASE_BEGAN",
            $"{state.TurnPlayerId} 进入主阶段",
            new Dictionary<string, object?>
            {
                ["turnPlayerId"] = state.TurnPlayerId
            }));

        return events;
    }

    private sealed record DrawResult(
        IReadOnlyList<string> MainDeck,
        IReadOnlyList<string> Graveyard,
        IReadOnlyList<string> DrawnCards,
        IReadOnlyList<BurnoutResult> Burnouts,
        string? WinnerPlayerId,
        IReadOnlyDictionary<string, int> PlayerScores);

    private sealed record BurnoutResult(
        string ScoredPlayerId,
        int ScoredPlayerScore);

    private sealed record StackResolutionResult(
        IReadOnlyDictionary<string, PlayerZones> PlayerZones,
        IReadOnlyDictionary<string, CardObjectState> CardObjects,
        IReadOnlyList<GameEvent> Events);

    private sealed record CleanupResult(
        IReadOnlyDictionary<string, CardObjectState> CardObjects,
        IReadOnlyList<string> DamagedObjectIds,
        IReadOnlyList<string> ExpiredEffectIds,
        bool RequiresFollowUpCleanup);
}

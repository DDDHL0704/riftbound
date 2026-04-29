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
        if (!TryBuildPlayCardPlan(state, intent, command, out var plan, out var rejection))
        {
            return rejection;
        }

        var behavior = plan.Behavior;
        var targetObjectIds = plan.TargetObjectIds;
        var runePools = PayMana(state, intent.PlayerId, plan.TotalManaCost);
        var playerZones = RemoveSourceCardFromHand(state, intent.PlayerId, plan.SourceZones, command.SourceObjectId);

        var stackItem = new StackItemState(
            $"STACK-{state.Tick + 1}-{command.SourceObjectId}",
            intent.PlayerId,
            command.SourceObjectId,
            behavior.EffectKind,
            command.CardNo,
            targetObjectIds,
            behavior.DamageAmount,
            plan.EffectRepeatCount);
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
                    ["cardNo"] = command.CardNo,
                    ["mode"] = command.Mode
                }),
            new GameEvent(
                "COST_PAID",
                $"{intent.PlayerId} 支付 {plan.TotalManaCost} 点费用",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["mana"] = plan.TotalManaCost,
                    ["baseMana"] = behavior.ManaCost,
                    ["costReductionMana"] = plan.CostReductionMana,
                    ["optionalCosts"] = plan.OptionalCosts.ToArray()
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
                    ["effectKind"] = stackItem.EffectKind,
                    ["mode"] = command.Mode,
                    ["effectRepeatCount"] = stackItem.EffectRepeatCount
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

    private static bool TryBuildPlayCardPlan(
        MatchState state,
        PlayerIntent intent,
        PlayCardCommand command,
        out PlayCardPlan plan,
        out ResolutionResult rejection)
    {
        plan = default!;
        rejection = default!;

        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || !string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || !string.Equals(state.TurnPlayerId, intent.PlayerId, StringComparison.Ordinal))
        {
            rejection = RejectWithCorePrompts(
                state,
                "PLAY_CARD is not allowed outside the turn player's ordinary open main phase.",
                ErrorCodes.PhaseNotAllowed);
            return false;
        }

        if (!CardBehaviorRegistry.TryGetByCardNoAndMode(command.CardNo, command.Mode, out var behavior))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"Unsupported card behavior or mode: {command.CardNo} {command.Mode}",
                ErrorCodes.UnsupportedCardBehavior);
            return false;
        }

        if (!state.PlayerZones.TryGetValue(intent.PlayerId, out var zones)
            || !zones.Hand.Contains(command.SourceObjectId, StringComparer.Ordinal))
        {
            rejection = RejectWithCorePrompts(
                state,
                "Source card is not in the player's hand.",
                ErrorCodes.CardNotInHand);
            return false;
        }

        var targetObjectIds = NormalizeTargetObjectIds(command.TargetObjectIds);
        if (!HasValidTargetCount(behavior, targetObjectIds)
            || targetObjectIds.Any(targetObjectId =>
                !IsTargetObjectInScope(state, intent.PlayerId, targetObjectId, behavior.TargetScope)
                || !IsTargetPowerAllowed(state, targetObjectId, behavior)))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires {DescribeTargetCount(behavior)} {DescribeTargetScope(behavior.TargetScope)} target(s).",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (!TryBuildOptionalCostPlan(command.OptionalCosts, behavior, out var optionalCosts, out var extraManaCost, out var effectRepeatCount))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"Unsupported optional cost for {behavior.DisplayName}.",
                ErrorCodes.UnsupportedCardBehavior);
            return false;
        }

        var costReductionMana = ResolveCostReductionMana(state, intent.PlayerId, behavior);
        var totalManaCost = Math.Max(0, behavior.ManaCost - costReductionMana) + extraManaCost;
        var currentPool = state.RunePools.TryGetValue(intent.PlayerId, out var runePool) ? runePool : RunePool.Empty;
        if (currentPool.Mana < totalManaCost)
        {
            rejection = RejectWithCorePrompts(
                state,
                $"Not enough mana to play {behavior.DisplayName}.",
                ErrorCodes.InsufficientCost);
            return false;
        }

        plan = new PlayCardPlan(
            behavior,
            zones,
            targetObjectIds,
            totalManaCost,
            effectRepeatCount,
            optionalCosts,
            costReductionMana);
        return true;
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
                PlayerScores = stackResolution.PlayerScores,
                CardObjects = stackResolution.CardObjects,
                RngCursor = stackResolution.RngCursor,
                DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(
                    state.DestroyedUnitOwnerIdsThisTurn,
                    stackResolution.DestroyedUnitOwnerIds),
                Status = stackResolution.WinnerPlayerId is null ? state.Status : MatchStatuses.Finished,
                WinnerPlayerId = stackResolution.WinnerPlayerId ?? state.WinnerPlayerId
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
            CardObjects = cleanupResult.CardObjects,
            DestroyedUnitOwnerIdsThisTurn = []
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
            WinnerPlayerId = drawResult.WinnerPlayerId,
            DestroyedUnitOwnerIdsThisTurn = [],
            RngCursor = drawResult.RngCursor
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

    private static IReadOnlyList<string> NormalizeTargetObjectIds(IReadOnlyList<string> targetObjectIds)
    {
        return targetObjectIds
            .Where(targetObjectId => !string.IsNullOrWhiteSpace(targetObjectId))
            .Select(targetObjectId => targetObjectId.Trim())
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizeOptionalCosts(IReadOnlyList<string>? optionalCosts)
    {
        return (optionalCosts ?? [])
            .Where(optionalCost => !string.IsNullOrWhiteSpace(optionalCost))
            .Select(optionalCost => optionalCost.Trim())
            .ToArray();
    }

    private static IReadOnlyList<string> MergeDestroyedUnitOwnerIds(
        IReadOnlyList<string> existingOwnerIds,
        IReadOnlyList<string> newOwnerIds)
    {
        return existingOwnerIds
            .Concat(newOwnerIds)
            .Where(ownerId => !string.IsNullOrWhiteSpace(ownerId))
            .Select(ownerId => ownerId.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(ownerId => ownerId, StringComparer.Ordinal)
            .ToArray();
    }

    private static Dictionary<string, RunePool> PayMana(MatchState state, string playerId, int manaCost)
    {
        var runePools = state.RunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var currentPool = runePools.TryGetValue(playerId, out var runePool) ? runePool : RunePool.Empty;
        runePools[playerId] = currentPool with
        {
            Mana = currentPool.Mana - manaCost
        };

        return runePools;
    }

    private static Dictionary<string, PlayerZones> RemoveSourceCardFromHand(
        MatchState state,
        string playerId,
        PlayerZones zones,
        string sourceObjectId)
    {
        var playerZones = NormalizeZonesForSeats(state);
        playerZones[playerId] = zones with
        {
            Hand = zones.Hand
                .Where(cardId => !string.Equals(cardId, sourceObjectId, StringComparison.Ordinal))
                .ToArray()
        };

        return playerZones;
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

    private static bool IsTargetObjectInScope(MatchState state, string playerId, string objectId, string targetScope)
    {
        return targetScope switch
        {
            CardTargetScopes.AnyUnit => IsBattlefieldObject(state, objectId) || IsBaseObject(state, objectId),
            CardTargetScopes.BaseUnit => IsBaseObject(state, objectId),
            CardTargetScopes.OpponentGraveyardCard => IsOpponentGraveyardCard(state, playerId, objectId),
            _ => IsBattlefieldObject(state, objectId)
        };
    }

    private static bool IsOpponentGraveyardCard(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.PlayerZones.Any(entry =>
                !string.Equals(entry.Key, playerId, StringComparison.Ordinal)
                && entry.Value.Graveyard.Contains(objectId, StringComparer.Ordinal));
    }

    private static bool IsBaseObject(MatchState state, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.CardObjects.ContainsKey(objectId)
            && state.PlayerZones.Values.Any(zones => zones.Base.Contains(objectId, StringComparer.Ordinal));
    }

    private static bool IsTargetPowerAllowed(MatchState state, string objectId, CardBehaviorDefinition behavior)
    {
        return behavior.MaxTargetPower <= 0
            || (state.CardObjects.TryGetValue(objectId, out var targetState)
                && targetState.Power > 0
                && targetState.Power <= behavior.MaxTargetPower);
    }

    private static bool HasValidTargetCount(CardBehaviorDefinition behavior, IReadOnlyList<string> targetObjectIds)
    {
        var minTargetCount = MinTargetCount(behavior);
        return targetObjectIds.Count >= minTargetCount
            && targetObjectIds.Count <= behavior.RequiredTargetCount
            && (behavior.AllowsRepeatedTargets
                || targetObjectIds.Distinct(StringComparer.Ordinal).Count() == targetObjectIds.Count);
    }

    private static bool TryBuildOptionalCostPlan(
        IReadOnlyList<string>? optionalCosts,
        CardBehaviorDefinition behavior,
        out IReadOnlyList<string> normalizedOptionalCosts,
        out int extraManaCost,
        out int effectRepeatCount)
    {
        normalizedOptionalCosts = NormalizeOptionalCosts(optionalCosts);
        extraManaCost = 0;
        effectRepeatCount = 1;

        if (normalizedOptionalCosts.Count == 0)
        {
            return true;
        }

        if (normalizedOptionalCosts.Count == 1
            && string.Equals(normalizedOptionalCosts[0], "ECHO", StringComparison.Ordinal)
            && behavior.EchoManaCost > 0)
        {
            extraManaCost = behavior.EchoManaCost;
            effectRepeatCount = 2;
            return true;
        }

        return false;
    }

    private static int ResolveCostReductionMana(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        if (behavior.CostReductionMana <= 0)
        {
            return 0;
        }

        return behavior.CostReductionConditionKind switch
        {
            CardCostReductionConditionKinds.EnemyUnitDestroyedThisTurn
                => EnemyUnitDestroyedThisTurn(state, playerId) ? behavior.CostReductionMana : 0,
            _ => 0
        };
    }

    private static bool EnemyUnitDestroyedThisTurn(MatchState state, string playerId)
    {
        return state.DestroyedUnitOwnerIdsThisTurn.Any(ownerPlayerId =>
            !string.Equals(ownerPlayerId, playerId, StringComparison.Ordinal));
    }

    private static int MinTargetCount(CardBehaviorDefinition behavior)
    {
        return behavior.MinTargetCount < 0 ? behavior.RequiredTargetCount : behavior.MinTargetCount;
    }

    private static string DescribeTargetCount(CardBehaviorDefinition behavior)
    {
        var minTargetCount = MinTargetCount(behavior);
        return minTargetCount == behavior.RequiredTargetCount
            ? behavior.RequiredTargetCount.ToString()
            : $"{minTargetCount}-{behavior.RequiredTargetCount}";
    }

    private static string DescribeTargetScope(string targetScope)
    {
        return string.Equals(targetScope, CardTargetScopes.AnyUnit, StringComparison.Ordinal)
            ? "unit"
            : string.Equals(targetScope, CardTargetScopes.BaseUnit, StringComparison.Ordinal)
                ? "base unit"
                : string.Equals(targetScope, CardTargetScopes.OpponentGraveyardCard, StringComparison.Ordinal)
                    ? "opponent graveyard card"
                    : "battlefield unit";
    }

    private static StackResolutionResult ResolveStackItemEffect(MatchState state, StackItemState stackItem)
    {
        if (!CardBehaviorRegistry.TryGetByEffectKind(stackItem.EffectKind, out var behavior)
            || !HasValidTargetCount(behavior, stackItem.TargetObjectIds))
        {
            return new StackResolutionResult(state.PlayerZones, state.CardObjects, state.PlayerScores, null, [], [], state.RngCursor);
        }

        var playerZones = NormalizeZonesForSeats(state);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        var destroyedObjectIds = new List<string>();
        var destroyedUnitOwnerIds = new List<string>();
        var rngCursor = state.RngCursor;
        if (behavior.RecyclesTargets)
        {
            var recycleResult = RecycleTargetCards(
                state,
                playerZones,
                stackItem.ControllerId,
                stackItem.SourceObjectId,
                stackItem.TargetObjectIds);
            events.AddRange(recycleResult.Events);
            rngCursor = recycleResult.RngCursor;
        }
        else if (behavior.DamagesAllBattlefieldUnits)
        {
            var damageAmount = ResolveDamageAmount(state, stackItem, behavior);
            if (damageAmount > 0)
            {
                foreach (var targetObjectId in GetBattlefieldObjectIds(state))
                {
                    var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
                        ? existingTarget
                        : new CardObjectState(targetObjectId);

                    targetState = targetState with
                    {
                        Damage = targetState.Damage + damageAmount
                    };
                    cardObjects[targetObjectId] = targetState;
                    events.Add(new GameEvent(
                        "DAMAGE_APPLIED",
                        $"{behavior.DisplayName}造成 {damageAmount} 点伤害",
                        new Dictionary<string, object?>
                        {
                            ["sourceObjectId"] = stackItem.SourceObjectId,
                            ["targetObjectId"] = targetObjectId,
                            ["damage"] = damageAmount
                        }));
                }
            }
        }
        else if (behavior.RequiredTargetCount > 0)
        {
            for (var repeatIndex = 0; repeatIndex < stackItem.EffectRepeatCount; repeatIndex++)
            {
                foreach (var targetObjectId in stackItem.TargetObjectIds)
                {
                    var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
                        ? existingTarget
                        : new CardObjectState(targetObjectId);

                    var damageAmount = ResolveDamageAmount(state, stackItem, behavior);
                    if (damageAmount > 0)
                    {
                        targetState = targetState with
                        {
                            Damage = targetState.Damage + damageAmount
                        };
                        events.Add(new GameEvent(
                            "DAMAGE_APPLIED",
                            $"{behavior.DisplayName}造成 {damageAmount} 点伤害",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["damage"] = damageAmount
                            }));
                    }

                    if (!string.IsNullOrWhiteSpace(behavior.StatusEffectId))
                    {
                        targetState = targetState with
                        {
                            UntilEndOfTurnEffects = targetState.UntilEndOfTurnEffects
                                .Concat([behavior.StatusEffectId])
                                .Distinct(StringComparer.Ordinal)
                                .OrderBy(effectId => effectId, StringComparer.Ordinal)
                                .ToArray()
                        };
                        events.Add(new GameEvent(
                            "STATUS_EFFECT_APPLIED",
                            $"{behavior.DisplayName}施加{behavior.StatusEffectId}",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["effectId"] = behavior.StatusEffectId
                        }));
                    }

                    if (behavior.DestroysTarget
                        && TryDestroyTarget(playerZones, cardObjects, targetObjectId, out var ownerPlayerId))
                    {
                        events.Add(new GameEvent(
                            "UNIT_DESTROYED",
                            $"{behavior.DisplayName}摧毁单位",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["ownerPlayerId"] = ownerPlayerId,
                                ["destroyedByPlayerId"] = stackItem.ControllerId
                        }));
                        destroyedObjectIds.Add(targetObjectId);
                        destroyedUnitOwnerIds.Add(ownerPlayerId);
                        continue;
                    }

                    cardObjects[targetObjectId] = targetState;
                }
            }
        }

        var lethalCleanup = ApplyLethalDamageCleanup(playerZones, cardObjects, stackItem);
        events.AddRange(lethalCleanup.Events);
        destroyedObjectIds.AddRange(lethalCleanup.DestroyedObjectIds
            .Where(objectId => stackItem.TargetObjectIds.Contains(objectId, StringComparer.Ordinal)));
        destroyedUnitOwnerIds.AddRange(lethalCleanup.DestroyedUnitOwnerIds);

        var playerScores = state.PlayerScores;
        string? winnerPlayerId = null;
        if (playerZones.TryGetValue(stackItem.ControllerId, out var controllerZones))
        {
            if (ShouldDrawForBehavior(behavior, destroyedObjectIds))
            {
                var drawState = state with
                {
                    RngCursor = rngCursor
                };
                var drawResult = DrawCards(
                    drawState,
                    stackItem.ControllerId,
                    controllerZones,
                    behavior.DrawCount * stackItem.EffectRepeatCount);
                controllerZones = controllerZones with
                {
                    MainDeck = drawResult.MainDeck,
                    Hand = controllerZones.Hand.Concat(drawResult.DrawnCards).ToArray(),
                    Graveyard = drawResult.Graveyard
                };
                playerScores = drawResult.PlayerScores;
                winnerPlayerId = drawResult.WinnerPlayerId;
                rngCursor = drawResult.RngCursor;
                events.AddRange(BuildCardDrawEvents(stackItem.ControllerId, drawResult));
            }

            if (!controllerZones.Graveyard.Contains(stackItem.SourceObjectId, StringComparer.Ordinal))
            {
                controllerZones = controllerZones with
                {
                    Graveyard = controllerZones.Graveyard.Concat([stackItem.SourceObjectId]).ToArray()
                };
            }

            playerZones[stackItem.ControllerId] = controllerZones;
        }

        return new StackResolutionResult(
            playerZones,
            cardObjects,
            playerScores,
            winnerPlayerId,
            events,
            destroyedUnitOwnerIds
                .Distinct(StringComparer.Ordinal)
                .OrderBy(ownerId => ownerId, StringComparer.Ordinal)
                .ToArray(),
            rngCursor);
    }

    private static IReadOnlyList<string> GetBattlefieldObjectIds(MatchState state)
    {
        return state.PlayerZones
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Battlefields)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static RecycleResult RecycleTargetCards(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        string controllerId,
        string sourceObjectId,
        IReadOnlyList<string> targetObjectIds)
    {
        var events = new List<GameEvent>();
        var rngCursor = state.RngCursor;
        foreach (var (ownerPlayerId, zones) in playerZones
            .Where(entry => !string.Equals(entry.Key, controllerId, StringComparison.Ordinal)))
        {
            var recycledCardIds = targetObjectIds
                .Where(cardId => zones.Graveyard.Contains(cardId, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (recycledCardIds.Length == 0)
            {
                continue;
            }

            var randomizedCardIds = RandomizeForMainDeckBottom(
                recycledCardIds,
                state.Seed,
                rngCursor,
                sourceObjectId);
            if (recycledCardIds.Length > 1)
            {
                rngCursor++;
            }

            playerZones[ownerPlayerId] = zones with
            {
                MainDeck = zones.MainDeck.Concat(randomizedCardIds).ToArray(),
                Graveyard = zones.Graveyard
                    .Where(cardId => !recycledCardIds.Contains(cardId, StringComparer.Ordinal))
                    .ToArray()
            };
            events.Add(new GameEvent(
                "CARDS_RECYCLED",
                $"{ownerPlayerId} 回收 {recycledCardIds.Length} 张牌",
                new Dictionary<string, object?>
                {
                    ["playerId"] = ownerPlayerId,
                    ["sourceObjectId"] = sourceObjectId,
                    ["cardIds"] = randomizedCardIds,
                    ["count"] = randomizedCardIds.Count
                }));
        }

        return new RecycleResult(events, rngCursor);
    }

    private static IReadOnlyList<string> RandomizeForMainDeckBottom(
        IReadOnlyList<string> cardIds,
        long seed,
        long rngCursor,
        string sourceObjectId)
    {
        if (cardIds.Count <= 1)
        {
            return cardIds.ToArray();
        }

        return cardIds
            .Select((cardId, index) => new
            {
                CardId = cardId,
                Index = index,
                Order = StableHash($"{seed}:{rngCursor}:{sourceObjectId}:{cardId}:{index}")
            })
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.CardId)
            .ToArray();
    }

    private static ulong StableHash(string value)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        var hash = offsetBasis;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= prime;
        }

        return hash;
    }

    private static bool TryDestroyTarget(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        out string ownerPlayerId)
    {
        ownerPlayerId = string.Empty;
        foreach (var (playerId, zones) in playerZones)
        {
            var isInBase = zones.Base.Contains(targetObjectId, StringComparer.Ordinal);
            var isInBattlefield = zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal);
            if (!isInBase && !isInBattlefield)
            {
                continue;
            }

            playerZones[playerId] = zones with
            {
                Base = RemoveFromZone(zones.Base, targetObjectId),
                Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId),
                Graveyard = zones.Graveyard.Contains(targetObjectId, StringComparer.Ordinal)
                    ? zones.Graveyard
                    : zones.Graveyard.Concat([targetObjectId]).ToArray()
            };
            cardObjects.Remove(targetObjectId);
            ownerPlayerId = playerId;
            return true;
        }

        return false;
    }

    private static bool ShouldDrawForBehavior(
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> destroyedObjectIds)
    {
        if (behavior.DrawCount <= 0)
        {
            return false;
        }

        return behavior.DrawConditionKind switch
        {
            CardDrawConditionKinds.None => true,
            CardDrawConditionKinds.TargetDestroyedByThisEffect => destroyedObjectIds.Count > 0,
            _ => false
        };
    }

    private static LethalDamageCleanupResult ApplyLethalDamageCleanup(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        StackItemState stackItem)
    {
        var events = new List<GameEvent>();
        var destroyedObjectIds = new List<string>();
        var destroyedUnitOwnerIds = new List<string>();
        var lethalObjectIds = cardObjects
            .Where(entry => entry.Value.Power > 0
                && entry.Value.Damage > 0
                && entry.Value.Damage >= entry.Value.Power
                && IsObjectOnField(playerZones, entry.Key))
            .Select(entry => entry.Key)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();

        foreach (var objectId in lethalObjectIds)
        {
            if (!TryDestroyTarget(playerZones, cardObjects, objectId, out var ownerPlayerId))
            {
                continue;
            }

            destroyedObjectIds.Add(objectId);
            destroyedUnitOwnerIds.Add(ownerPlayerId);
            events.Add(new GameEvent(
                "UNIT_DESTROYED",
                "致命伤害摧毁单位",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["targetObjectId"] = objectId,
                    ["ownerPlayerId"] = ownerPlayerId,
                    ["destroyedByPlayerId"] = stackItem.ControllerId,
                    ["reason"] = "LETHAL_DAMAGE"
                }));
        }

        return new LethalDamageCleanupResult(events, destroyedObjectIds, destroyedUnitOwnerIds);
    }

    private static bool IsObjectOnField(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId)
    {
        return playerZones.Values.Any(zones =>
            zones.Base.Contains(objectId, StringComparer.Ordinal)
            || zones.Battlefields.Contains(objectId, StringComparer.Ordinal));
    }

    private static IReadOnlyList<string> RemoveFromZone(IReadOnlyList<string> zone, string objectId)
    {
        return zone
            .Where(existingObjectId => !string.Equals(existingObjectId, objectId, StringComparison.Ordinal))
            .ToArray();
    }

    private static DrawResult DrawCards(MatchState state, string playerId, PlayerZones zones, int drawCount)
    {
        DrawResult result = new(
            zones.MainDeck,
            zones.Graveyard,
            [],
            [],
            null,
            NormalizeScoresForSeats(state),
            state.RngCursor);
        var currentZones = zones;
        var drawnCards = new List<string>();
        var burnouts = new List<BurnoutResult>();

        for (var i = 0; i < drawCount && result.WinnerPlayerId is null; i++)
        {
            result = DrawOne(
                state with
                {
                    PlayerScores = result.PlayerScores,
                    RngCursor = result.RngCursor
                },
                playerId,
                currentZones);
            drawnCards.AddRange(result.DrawnCards);
            burnouts.AddRange(result.Burnouts);
            currentZones = currentZones with
            {
                MainDeck = result.MainDeck,
                Graveyard = result.Graveyard
            };
        }

        return result with
        {
            DrawnCards = drawnCards.ToArray(),
            Burnouts = burnouts.ToArray()
        };
    }

    private static IReadOnlyList<GameEvent> BuildCardDrawEvents(string playerId, DrawResult drawResult)
    {
        var events = new List<GameEvent>();
        foreach (var (burnout, index) in drawResult.Burnouts.Select((burnout, index) => (burnout, index)))
        {
            events.Add(new GameEvent(
                "BURNOUT_APPLIED",
                $"{playerId} 执行燃尽",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
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
            $"{playerId} 抽 {drawResult.DrawnCards.Count} 张牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["count"] = drawResult.DrawnCards.Count
            }));
        return events;
    }

    private static int ResolveDamageAmount(
        MatchState state,
        StackItemState stackItem,
        CardBehaviorDefinition behavior)
    {
        if (behavior.ConditionalDamageAmount > 0
            && DamageConditionApplies(state, stackItem.ControllerId, behavior.DamageConditionKind))
        {
            return behavior.ConditionalDamageAmount;
        }

        return stackItem.DamageAmount > 0 ? stackItem.DamageAmount : behavior.DamageAmount;
    }

    private static bool DamageConditionApplies(MatchState state, string controllerId, string conditionKind)
    {
        return conditionKind switch
        {
            CardDamageConditionKinds.ControllerHasFaceDownCard => ControllerControlsFaceDownCard(state, controllerId),
            _ => false
        };
    }

    private static bool ControllerControlsFaceDownCard(MatchState state, string controllerId)
    {
        if (!state.PlayerZones.TryGetValue(controllerId, out var zones))
        {
            return false;
        }

        return zones.Battlefields.Any(objectId =>
            state.CardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.IsFaceDown);
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
        var rngCursor = state.RngCursor;

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
                var recycledCards = RandomizeForMainDeckBottom(
                    graveyard,
                    state.Seed,
                    rngCursor,
                    $"BURNOUT:{playerId}");
                if (graveyard.Count > 1)
                {
                    rngCursor++;
                }

                mainDeck.AddRange(recycledCards);
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
            playerScores,
            rngCursor);
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
        IReadOnlyDictionary<string, int> PlayerScores,
        long RngCursor);

    private sealed record BurnoutResult(
        string ScoredPlayerId,
        int ScoredPlayerScore);

    private sealed record PlayCardPlan(
        CardBehaviorDefinition Behavior,
        PlayerZones SourceZones,
        IReadOnlyList<string> TargetObjectIds,
        int TotalManaCost,
        int EffectRepeatCount,
        IReadOnlyList<string> OptionalCosts,
        int CostReductionMana);

    private sealed record StackResolutionResult(
        IReadOnlyDictionary<string, PlayerZones> PlayerZones,
        IReadOnlyDictionary<string, CardObjectState> CardObjects,
        IReadOnlyDictionary<string, int> PlayerScores,
        string? WinnerPlayerId,
        IReadOnlyList<GameEvent> Events,
        IReadOnlyList<string> DestroyedUnitOwnerIds,
        long RngCursor);

    private sealed record RecycleResult(
        IReadOnlyList<GameEvent> Events,
        long RngCursor);

    private sealed record LethalDamageCleanupResult(
        IReadOnlyList<GameEvent> Events,
        IReadOnlyList<string> DestroyedObjectIds,
        IReadOnlyList<string> DestroyedUnitOwnerIds);

    private sealed record CleanupResult(
        IReadOnlyDictionary<string, CardObjectState> CardObjects,
        IReadOnlyList<string> DamagedObjectIds,
        IReadOnlyList<string> ExpiredEffectIds,
        bool RequiresFollowUpCleanup);
}

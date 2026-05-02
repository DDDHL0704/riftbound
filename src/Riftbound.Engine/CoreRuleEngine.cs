using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed class CoreRuleEngine : IRuleEngine
{
    private const int WinningScore = 8;
    private const string BanishIfDestroyedThisTurnEffectId = "BANISH_IF_DESTROYED_THIS_TURN";
    private const string RecallToBaseExhaustedIfDestroyedThisTurnEffectId = "RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN";
    private const string DamageReceivedDoubledThisTurnEffectId = "DAMAGE_RECEIVED_DOUBLED_THIS_TURN";
    private const string PreventNextDamageThisTurnEffectId = "PREVENT_NEXT_DAMAGE_THIS_TURN";
    private const string PreventSpellAndSkillDamageThisTurnEffectId = "PREVENT_SPELL_AND_SKILL_DAMAGE_THIS_TURN";
    private const string DestroyOnNextDamageThisTurnEffectId = "DESTROY_ON_NEXT_DAMAGE_THIS_TURN";
    private const string ExhaustFriendlyUnitOptionalCostPrefix = "EXHAUST_FRIENDLY_UNIT:";
    private const string DestroyFriendlyUnitAdditionalCostPrefix = "DESTROY_FRIENDLY_UNIT:";
    private const string DestroyFriendlyPowerfulUnitAdditionalCostPrefix = "DESTROY_FRIENDLY_POWERFUL_UNIT:";
    private const string DestroyFriendlyTraitUnitAdditionalCostPrefix = "DESTROY_FRIENDLY_TRAIT_UNIT:";
    private const string ReturnFriendlyEquipmentAdditionalCostPrefix = "RETURN_FRIENDLY_EQUIPMENT:";
    private const string DiscardHandCardOptionalCostPrefix = "DISCARD_HAND_CARD:";
    private const string SpendPowerOptionalCostPrefix = "SPEND_POWER:";
    private const string SpendExperienceOptionalCostPrefix = "SPEND_EXPERIENCE:";

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

        if (command is ActivateAbilityCommand)
        {
            return ValueTask.FromResult(RejectWithCorePrompts(
                state,
                "ACTIVATE_ABILITY is not implemented in P4 yet.",
                ErrorCodes.UnsupportedCommand));
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
        var runePools = PayRuneCosts(state, intent.PlayerId, plan.TotalManaCost, plan.TotalPowerCost);
        var playerExperience = PayExperienceCosts(state, intent.PlayerId, plan.TotalExperienceCost);
        var playerZones = RemoveSourceCardFromHand(state, intent.PlayerId, plan.SourceZones, command.SourceObjectId);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        var stackItem = new StackItemState(
            $"STACK-{state.Tick + 1}-{command.SourceObjectId}",
            intent.PlayerId,
            command.SourceObjectId,
            behavior.EffectKind,
            command.CardNo,
            targetObjectIds,
            behavior.DamageAmountFromOptionalPowerCost ? plan.TotalPowerCost : behavior.DamageAmount,
            plan.EffectRepeatCount,
            plan.OptionalCosts,
            playedAfterAnotherCardThisTurn: ControllerPlayedAnotherCardThisTurn(state, intent.PlayerId));
        var nextState = state with
        {
            Tick = state.Tick + 1,
            ActivePlayerId = intent.PlayerId,
            TimingState = TimingStates.NeutralClosed,
            RunePools = runePools,
            PlayerExperience = playerExperience,
            PlayerCardsPlayedThisTurn = IncrementPlayerCardsPlayedThisTurn(state, intent.PlayerId),
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            PriorityPlayerId = intent.PlayerId,
            PassedPriorityPlayerIds = [],
            FocusPlayerId = string.Equals(state.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                ? null
                : state.FocusPlayerId,
            PassedFocusPlayerIds = string.Equals(state.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                ? []
                : state.PassedFocusPlayerIds,
            StackItems = state.StackItems.Concat([stackItem]).ToArray()
        };
        var destroyedAdditionalCostOwnerIds = new List<string>();

        var events = new List<GameEvent>
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
                    ["power"] = plan.TotalPowerCost,
                    ["experience"] = plan.TotalExperienceCost,
                    ["baseMana"] = behavior.ManaCost,
                    ["costReductionMana"] = plan.CostReductionMana,
                    ["optionalCostManaReduction"] = plan.OptionalCostManaReduction,
                    ["spellshieldTaxMana"] = plan.SpellshieldTaxMana,
                    ["spellshieldTaxTargetObjectIds"] = plan.SpellshieldTaxTargetObjectIds.ToArray(),
                    ["optionalCosts"] = plan.OptionalCosts.ToArray()
                })
        };
        foreach (var optionalCostTargetObjectId in plan.ExhaustedOptionalCostTargetObjectIds)
        {
            var targetState = cardObjects.TryGetValue(optionalCostTargetObjectId, out var existingTarget)
                ? existingTarget
                : new CardObjectState(optionalCostTargetObjectId);
            targetState = ApplyExhaustState(
                targetState,
                behavior,
                stackItem,
                optionalCostTargetObjectId,
                out var exhaustedEvent);
            cardObjects[optionalCostTargetObjectId] = targetState;
            events.Add(exhaustedEvent);
        }

        foreach (var additionalCostTargetObjectId in plan.DestroyedAdditionalCostTargetObjectIds)
        {
            if (!TryDestroyTarget(playerZones, cardObjects, additionalCostTargetObjectId, out var removalResult))
            {
                continue;
            }

            events.Add(BuildFieldRemovalEvent(
                behavior.DisplayName,
                stackItem,
                additionalCostTargetObjectId,
                removalResult,
                "ADDITIONAL_COST"));
            if (removalResult.WasDestroyed)
            {
                destroyedAdditionalCostOwnerIds.Add(removalResult.OwnerPlayerId);
            }
        }

        foreach (var additionalCostTargetObjectId in plan.ReturnedAdditionalCostTargetObjectIds)
        {
            if (!TryReturnTargetToHand(playerZones, cardObjects, additionalCostTargetObjectId, out var returnedOwnerPlayerId, out var returnedWasEquipment))
            {
                continue;
            }

            events.Add(BuildReturnedToHandEvent(
                behavior.DisplayName,
                stackItem,
                additionalCostTargetObjectId,
                returnedOwnerPlayerId,
                returnedWasEquipment,
                "ADDITIONAL_COST"));
        }

        foreach (var discardedOptionalCostTargetObjectId in plan.DiscardedOptionalCostTargetObjectIds)
        {
            if (!TryDiscardCardFromHand(playerZones, intent.PlayerId, discardedOptionalCostTargetObjectId))
            {
                continue;
            }

            events.Add(new GameEvent(
                "CARD_DISCARDED",
                $"{behavior.DisplayName}弃置手牌作为额外费用",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["targetObjectId"] = discardedOptionalCostTargetObjectId,
                    ["reason"] = "OPTIONAL_COST",
                    ["destinationZone"] = "GRAVEYARD"
                }));
        }

        events.Add(
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
                    ["effectRepeatCount"] = stackItem.EffectRepeatCount,
                    ["playedAfterAnotherCardThisTurn"] = stackItem.PlayedAfterAnotherCardThisTurn
                }));

        nextState = nextState with
        {
            CardObjects = cardObjects,
            DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(
                state.DestroyedUnitOwnerIdsThisTurn,
                destroyedAdditionalCostOwnerIds)
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

        if (!CardBehaviorRegistry.TryGetByCardNoAndMode(command.CardNo, command.Mode, out var behavior))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"Unsupported card behavior or mode: {command.CardNo} {command.Mode}",
                ErrorCodes.UnsupportedCardBehavior);
            return false;
        }

        var timingDecision = CardPermissionKeywordRules.EvaluatePlayTiming(state, intent.PlayerId, behavior);
        if (!timingDecision.IsAllowed)
        {
            rejection = RejectWithCorePrompts(
                state,
                timingDecision.Reason,
                ErrorCodes.PhaseNotAllowed);
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
        if (!HasValidTargetCount(state, intent.PlayerId, behavior, targetObjectIds)
            || !HasValidTotalTargetPower(state, behavior, targetObjectIds)
            || !AreTargetsAfterFirstPowerLessThanFirstTarget(state, behavior, targetObjectIds)
            || !HasRequiredAnyTargetTag(state, behavior, targetObjectIds)
            || !AreAttachDetachTargetsAllowed(state, behavior, targetObjectIds)
            || !HasValidSacredJudgmentKeepTargets(state, command.SourceObjectId, behavior, targetObjectIds)
            || !HasValidEachPlayerTopFiveUnitTargets(state, behavior, targetObjectIds)
            || targetObjectIds.Where((targetObjectId, targetIndex) =>
                !IsTargetObjectInScope(state, intent.PlayerId, targetObjectId, behavior.TargetScope, targetIndex)
                || !IsMainDeckLookTargetAllowed(state, intent.PlayerId, targetObjectId, targetIndex, behavior)
                || !IsMainDeckTargetTagAllowed(state, targetObjectId, targetIndex, behavior)
                || !IsTargetRequiredTagAllowed(state, targetObjectId, behavior)
                || !IsTargetTagAllowed(state, targetObjectId, behavior)
                || !IsTargetManaCostAllowed(state, intent.PlayerId, targetObjectId, behavior)
                || !IsStackItemTargetConditionAllowed(state, intent.PlayerId, targetObjectId, targetIndex, targetObjectIds, behavior)
                || !IsTargetPowerAllowed(state, targetObjectId, behavior)).Any())
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires {DescribeTargetCount(state, intent.PlayerId, behavior)} {DescribeTargetScope(behavior.TargetScope)} target(s).",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (behavior.DiscardsTargetFromHand
            && targetObjectIds.Contains(command.SourceObjectId, StringComparer.Ordinal))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a different hand card to discard.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (!TryBuildOptionalCostPlan(
                command.OptionalCosts,
                behavior,
                out var optionalCosts,
                out var extraManaCost,
                out var extraPowerCost,
                out var experienceCost,
                out var optionalCostManaReduction,
                out var effectRepeatCount,
                out var exhaustedOptionalCostTargetObjectIds,
                out var destroyedAdditionalCostTargetObjectIds,
                out var returnedAdditionalCostTargetObjectIds,
                out var discardedOptionalCostTargetObjectIds))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"Unsupported optional cost for {behavior.DisplayName}.",
                ErrorCodes.UnsupportedCardBehavior);
            return false;
        }

        if (behavior.RequiresDestroyFriendlyUnitAdditionalCost
            && destroyedAdditionalCostTargetObjectIds.Count != 1)
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a friendly unit as an additional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (behavior.RequiresDestroyFriendlyPowerfulUnitAdditionalCost
            && destroyedAdditionalCostTargetObjectIds.Count != 1)
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a friendly powerful unit as an additional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (behavior.RequiresDestroyFriendlyTraitUnitAdditionalCost
            && destroyedAdditionalCostTargetObjectIds.Count != 1)
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a friendly Bird, Cat, Dog, or Poro unit as an additional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (behavior.RequiresReturnFriendlyEquipmentAdditionalCost
            && returnedAdditionalCostTargetObjectIds.Count != 1)
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a friendly equipment as an additional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (exhaustedOptionalCostTargetObjectIds.Any(targetObjectId =>
                !CanExhaustFriendlyUnitAsOptionalCost(state, intent.PlayerId, targetObjectId)))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires an active friendly unit for its optional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (behavior.RequiresDestroyFriendlyUnitAdditionalCost
            && destroyedAdditionalCostTargetObjectIds.Any(targetObjectId =>
                !CanDestroyFriendlyUnitAsAdditionalCost(state, intent.PlayerId, targetObjectId)))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a friendly unit as an additional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (behavior.RequiresDestroyFriendlyPowerfulUnitAdditionalCost
            && destroyedAdditionalCostTargetObjectIds.Any(targetObjectId =>
                !CanDestroyFriendlyPowerfulUnitAsAdditionalCost(state, intent.PlayerId, targetObjectId)))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a friendly powerful unit as an additional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (behavior.RequiresDestroyFriendlyTraitUnitAdditionalCost
            && destroyedAdditionalCostTargetObjectIds.Any(targetObjectId =>
                !CanDestroyFriendlyTraitUnitAsAdditionalCost(state, intent.PlayerId, targetObjectId)))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a friendly Bird, Cat, Dog, or Poro unit as an additional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (behavior.RequiresReturnFriendlyEquipmentAdditionalCost
            && returnedAdditionalCostTargetObjectIds.Any(targetObjectId =>
                !CanReturnFriendlyEquipmentAsAdditionalCost(state, intent.PlayerId, targetObjectId)))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a friendly equipment as an additional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (!AreTargetsManaCostAtMostDestroyedAdditionalCostUnit(
                state,
                behavior,
                targetObjectIds,
                destroyedAdditionalCostTargetObjectIds))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a graveyard unit with mana cost no greater than the destroyed unit.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (discardedOptionalCostTargetObjectIds.Any(targetObjectId =>
                !CanDiscardHandCardAsOptionalCost(state, intent.PlayerId, command.SourceObjectId, targetObjectId)))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a different hand card for its optional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        var costReductionMana = ResolveCostReductionMana(state, intent.PlayerId, behavior);
        var spellshieldTaxMana = ResolveSpellshieldTargetTaxMana(
            state,
            intent.PlayerId,
            behavior,
            targetObjectIds,
            out var spellshieldTaxTargetObjectIds);
        var totalManaCost = Math.Max(0, behavior.ManaCost - costReductionMana - optionalCostManaReduction)
            + extraManaCost
            + spellshieldTaxMana;
        var totalPowerCost = extraPowerCost;
        var totalExperienceCost = experienceCost;
        var currentPool = state.RunePools.TryGetValue(intent.PlayerId, out var runePool) ? runePool : RunePool.Empty;
        if (currentPool.Mana < totalManaCost)
        {
            rejection = RejectWithCorePrompts(
                state,
                $"Not enough mana to play {behavior.DisplayName}.",
                ErrorCodes.InsufficientCost);
            return false;
        }

        if (currentPool.Power < totalPowerCost)
        {
            rejection = RejectWithCorePrompts(
                state,
                $"Not enough power to play {behavior.DisplayName}.",
                ErrorCodes.InsufficientCost);
            return false;
        }

        var currentExperience = state.PlayerExperience.TryGetValue(intent.PlayerId, out var availableExperience)
            ? availableExperience
            : 0;
        if (currentExperience < totalExperienceCost)
        {
            rejection = RejectWithCorePrompts(
                state,
                $"Not enough experience to play {behavior.DisplayName}.",
                ErrorCodes.InsufficientCost);
            return false;
        }

        plan = new PlayCardPlan(
            behavior,
            zones,
            targetObjectIds,
            totalManaCost,
            totalPowerCost,
            totalExperienceCost,
            effectRepeatCount,
            optionalCosts,
            costReductionMana,
            optionalCostManaReduction,
            spellshieldTaxMana,
            spellshieldTaxTargetObjectIds,
            exhaustedOptionalCostTargetObjectIds,
            destroyedAdditionalCostTargetObjectIds,
            returnedAdditionalCostTargetObjectIds,
            discardedOptionalCostTargetObjectIds);
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
            var stackResolution = ResolveStackItemEffect(state, resolvedItem);
            var resolvedStack = stackResolution.StackItems ?? remainingStack;
            var nextStack = RemoveCounteredStackItems(resolvedStack, stackResolution.CounteredStackItemIds);
            var nextPriorityPlayerId = nextStack.Length == 0
                ? null
                : nextStack[^1].ControllerId;
            nextState = state with
            {
                Tick = state.Tick + 1,
                ActivePlayerId = nextPriorityPlayerId ?? state.TurnPlayerId,
                TimingState = nextStack.Length == 0 ? TimingStates.NeutralOpen : state.TimingState,
                PriorityPlayerId = nextPriorityPlayerId,
                PassedPriorityPlayerIds = [],
                StackItems = nextStack,
                PlayerZones = stackResolution.PlayerZones,
                PlayerScores = stackResolution.PlayerScores,
                PlayerExperience = stackResolution.PlayerExperience,
                CardObjects = stackResolution.CardObjects,
                UntilEndOfTurnEffects = stackResolution.UntilEndOfTurnEffects,
                RngCursor = stackResolution.RngCursor,
                DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(
                    state.DestroyedUnitOwnerIdsThisTurn,
                    stackResolution.DestroyedUnitOwnerIds),
                Status = stackResolution.WinnerPlayerId is null ? state.Status : MatchStatuses.Finished,
                WinnerPlayerId = stackResolution.WinnerPlayerId ?? state.WinnerPlayerId,
                ExtraTurnPlayerId = stackResolution.ExtraTurnPlayerId ?? state.ExtraTurnPlayerId
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
        var nextPlayerId = !string.IsNullOrWhiteSpace(state.ExtraTurnPlayerId)
            && state.Seats.ContainsKey(state.ExtraTurnPlayerId)
            ? state.ExtraTurnPlayerId
            : NextPlayerId(state);
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
            UntilEndOfTurnEffects = cleanupResult.UntilEndOfTurnEffects,
            DestroyedUnitOwnerIdsThisTurn = [],
            PlayerCardsPlayedThisTurn = new Dictionary<string, int>(StringComparer.Ordinal),
            ExtraTurnPlayerId = null
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
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var ephemeralCleanupResult = DestroyEphemeralObjectsAtTurnStart(
            playerZones,
            cardObjects,
            turnPlayerId,
            state.Tick);
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
            CardObjects = cardObjects,
            WinnerPlayerId = drawResult.WinnerPlayerId,
            DestroyedUnitOwnerIdsThisTurn = ephemeralCleanupResult.DestroyedUnitOwnerIds,
            RngCursor = drawResult.RngCursor
        };

        return new ResolutionResult(
            true,
            null,
            nextState,
            BuildTurnStartEvents(state, calledRunes.Length, drawResult, ephemeralCleanupResult.Events),
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static EphemeralCleanupResult DestroyEphemeralObjectsAtTurnStart(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string turnPlayerId,
        long currentTick)
    {
        if (!playerZones.TryGetValue(turnPlayerId, out var zones))
        {
            return EphemeralCleanupResult.Empty;
        }

        var controlledFieldObjectIds = zones.Base
            .Concat(zones.Battlefields)
            .Where(objectId => cardObjects.TryGetValue(objectId, out var objectState)
                && objectState.Tags.Contains(CardObjectTags.Ephemeral, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        if (controlledFieldObjectIds.Length == 0)
        {
            return EphemeralCleanupResult.Empty;
        }

        var events = new List<GameEvent>();
        var destroyedUnitOwnerIds = new List<string>();
        foreach (var objectId in controlledFieldObjectIds)
        {
            var pseudoStackItem = new StackItemState(
                $"TURN-START-{currentTick}-{objectId}",
                turnPlayerId,
                objectId,
                "EPHEMERAL_TURN_START_DESTROY",
                string.Empty,
                [],
                0);
            if (!TryDestroyTarget(playerZones, cardObjects, objectId, out var removalResult))
            {
                continue;
            }

            events.Add(BuildFieldRemovalEvent(
                CardObjectTags.Ephemeral,
                pseudoStackItem,
                objectId,
                removalResult,
                "EPHEMERAL_TURN_START"));
            if (removalResult.WasDestroyed && removalResult.WasUnit)
            {
                destroyedUnitOwnerIds.Add(removalResult.OwnerPlayerId);
            }
        }

        return new EphemeralCleanupResult(
            events,
            destroyedUnitOwnerIds
                .Distinct(StringComparer.Ordinal)
                .OrderBy(ownerId => ownerId, StringComparer.Ordinal)
                .ToArray());
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

    private static Dictionary<string, RunePool> PayRuneCosts(
        MatchState state,
        string playerId,
        int manaCost,
        int powerCost)
    {
        var runePools = state.RunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var currentPool = runePools.TryGetValue(playerId, out var runePool) ? runePool : RunePool.Empty;
        runePools[playerId] = currentPool with
        {
            Mana = currentPool.Mana - manaCost,
            Power = currentPool.Power - powerCost
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

    private static bool IsTargetObjectInScope(
        MatchState state,
        string playerId,
        string objectId,
        string targetScope,
        int targetIndex = 0)
    {
        return targetScope switch
        {
            CardTargetScopes.BattlefieldUnitOrEquipment => IsBattlefieldObject(state, objectId)
                || IsEquipmentObject(state, objectId),
            CardTargetScopes.AnyUnit => IsBattlefieldObject(state, objectId) || IsBaseObject(state, objectId),
            CardTargetScopes.BaseUnit => IsBaseObject(state, objectId),
            CardTargetScopes.FriendlyUnit => IsControlledFieldObject(state, playerId, objectId),
            CardTargetScopes.FriendlyUnitThenFriendlyUnit => IsControlledFieldObject(state, playerId, objectId),
            CardTargetScopes.FriendlyThenEnemyUnits => targetIndex == 0
                ? IsControlledFieldObject(state, playerId, objectId)
                : IsEnemyFieldObject(state, playerId, objectId),
            CardTargetScopes.UnitThenItsControllersWeapon => targetIndex == 0
                ? IsFieldUnitObject(state, objectId)
                : IsEquipmentObject(state, objectId),
            CardTargetScopes.FriendlyEquipmentThenEnemyEquipment => targetIndex == 0
                ? IsFriendlyEquipmentObject(state, playerId, objectId)
                : IsEnemyEquipmentObject(state, playerId, objectId),
            CardTargetScopes.FriendlyThenEnemyBattlefieldUnits => targetIndex == 0
                ? IsControlledFieldObject(state, playerId, objectId)
                : IsEnemyBattlefieldObject(state, playerId, objectId),
            CardTargetScopes.FriendlyBattlefieldThenEnemyBattlefieldUnits => targetIndex == 0
                ? IsControlledBattlefieldObject(state, playerId, objectId)
                : IsEnemyBattlefieldObject(state, playerId, objectId),
            CardTargetScopes.FriendlyBattlefieldUnitThenStackSpell => targetIndex == 0
                ? IsControlledBattlefieldObject(state, playerId, objectId)
                : IsStackSpellItem(state, objectId),
            CardTargetScopes.AnyUnitThenFriendlyMainDeckCard => targetIndex == 0
                ? IsBattlefieldObject(state, objectId) || IsBaseObject(state, objectId)
                : IsFriendlyMainDeckCard(state, playerId, objectId),
            CardTargetScopes.FriendlyBattlefieldUnit => IsControlledBattlefieldObject(state, playerId, objectId),
            CardTargetScopes.FriendlyHandCard => IsFriendlyHandCard(state, playerId, objectId),
            CardTargetScopes.AnyHandCard => IsAnyHandCard(state, objectId),
            CardTargetScopes.FriendlyHandCardThenBattlefieldUnit => targetIndex == 0
                ? IsFriendlyHandCard(state, playerId, objectId)
                : IsBattlefieldObject(state, objectId),
            CardTargetScopes.FriendlyMainDeckCard => IsFriendlyMainDeckCard(state, playerId, objectId),
            CardTargetScopes.FriendlyGraveyardCard => IsFriendlyGraveyardCard(state, playerId, objectId),
            CardTargetScopes.FriendlyBaseUnit => IsControlledBaseObject(state, playerId, objectId),
            CardTargetScopes.AttackingUnit => IsAttackingBattlefieldObject(state, objectId),
            CardTargetScopes.EnemyAttackingUnit => IsEnemyFieldObject(state, playerId, objectId)
                && IsAttackingBattlefieldObject(state, objectId),
            CardTargetScopes.EnemyBattlefieldUnit => IsEnemyBattlefieldObject(state, playerId, objectId),
            CardTargetScopes.EnemyUnit => IsEnemyFieldObject(state, playerId, objectId),
            CardTargetScopes.EnemyUnitThenEnemyUnit => IsEnemyFieldObject(state, playerId, objectId),
            CardTargetScopes.OpponentHandCard => IsOpponentHandCard(state, playerId, objectId),
            CardTargetScopes.OpponentGraveyardCard => IsOpponentGraveyardCard(state, playerId, objectId),
            CardTargetScopes.OpponentMainDeckTopCard => IsOpponentMainDeckTopCard(state, playerId, objectId),
            CardTargetScopes.AnyMainDeckTopFiveCard => IsAnyMainDeckTopCards(state, objectId, 5),
            CardTargetScopes.Equipment => IsEquipmentObject(state, objectId),
            CardTargetScopes.StackSpell => IsStackSpellItem(state, objectId),
            CardTargetScopes.SacredJudgmentKeepCard => IsSacredJudgmentKeepCandidate(state, objectId),
            _ => IsBattlefieldObject(state, objectId)
        };
    }

    private static bool IsControlledFieldObject(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.CardObjects.ContainsKey(objectId)
            && state.PlayerZones.TryGetValue(playerId, out var zones)
            && (zones.Base.Contains(objectId, StringComparer.Ordinal)
                || zones.Battlefields.Contains(objectId, StringComparer.Ordinal));
    }

    private static bool IsControlledBattlefieldObject(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.CardObjects.ContainsKey(objectId)
            && state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Battlefields.Contains(objectId, StringComparer.Ordinal);
    }

    private static bool IsControlledBaseObject(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.CardObjects.ContainsKey(objectId)
            && state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Base.Contains(objectId, StringComparer.Ordinal);
    }

    private static bool IsFriendlyHandCard(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Hand.Contains(objectId, StringComparer.Ordinal);
    }

    private static bool IsOpponentHandCard(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.PlayerZones.Any(entry =>
                !string.Equals(entry.Key, playerId, StringComparison.Ordinal)
                && entry.Value.Hand.Contains(objectId, StringComparer.Ordinal));
    }

    private static bool IsAnyHandCard(MatchState state, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.PlayerZones.Values.Any(zones => zones.Hand.Contains(objectId, StringComparer.Ordinal));
    }

    private static bool IsFriendlyMainDeckCard(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.MainDeck.Contains(objectId, StringComparer.Ordinal);
    }

    private static bool IsOpponentMainDeckTopCard(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.PlayerZones.Any(entry =>
                !string.Equals(entry.Key, playerId, StringComparison.Ordinal)
                && entry.Value.MainDeck.Count > 0
                && string.Equals(entry.Value.MainDeck[0], objectId, StringComparison.Ordinal));
    }

    private static bool IsAnyMainDeckTopCards(MatchState state, string objectId, int lookCount)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && lookCount > 0
            && state.PlayerZones.Values.Any(zones =>
                zones.MainDeck.Take(lookCount).Contains(objectId, StringComparer.Ordinal));
    }

    private static bool TryGetTopMainDeckOwner(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId,
        int lookCount,
        out string playerId)
    {
        playerId = string.Empty;
        if (string.IsNullOrWhiteSpace(objectId)
            || lookCount <= 0)
        {
            return false;
        }

        foreach (var (candidatePlayerId, zones) in playerZones)
        {
            if (zones.MainDeck
                .Take(lookCount)
                .Contains(objectId, StringComparer.Ordinal))
            {
                playerId = candidatePlayerId;
                return true;
            }
        }

        return false;
    }

    private static bool IsMainDeckLookTargetAllowed(
        MatchState state,
        string playerId,
        string objectId,
        int targetIndex,
        CardBehaviorDefinition behavior)
    {
        if (behavior.MainDeckLookCount <= 0)
        {
            return true;
        }

        if (behavior.PlaysEachPlayerTopFiveUnitToBase)
        {
            return true;
        }

        if (targetIndex < behavior.MainDeckLookTargetStartIndex)
        {
            return true;
        }

        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.MainDeck
                .Take(behavior.MainDeckLookCount)
                .Contains(objectId, StringComparer.Ordinal);
    }

    private static bool IsMainDeckTargetTagAllowed(
        MatchState state,
        string objectId,
        int targetIndex,
        CardBehaviorDefinition behavior)
    {
        if (string.IsNullOrWhiteSpace(behavior.MainDeckTargetRequiredTag)
            || targetIndex < behavior.MainDeckLookTargetStartIndex)
        {
            return true;
        }

        return CardObjectHasTag(state.CardObjects, objectId, behavior.MainDeckTargetRequiredTag);
    }

    private static bool IsTargetTagAllowed(
        MatchState state,
        string objectId,
        CardBehaviorDefinition behavior)
    {
        return string.IsNullOrWhiteSpace(behavior.TargetForbiddenTag)
            || !CardObjectHasTag(state.CardObjects, objectId, behavior.TargetForbiddenTag);
    }

    private static bool IsTargetRequiredTagAllowed(
        MatchState state,
        string objectId,
        CardBehaviorDefinition behavior)
    {
        return string.IsNullOrWhiteSpace(behavior.TargetRequiredTag)
            || CardObjectHasTag(state.CardObjects, objectId, behavior.TargetRequiredTag);
    }

    private static bool IsTargetManaCostAllowed(
        MatchState state,
        string playerId,
        string objectId,
        CardBehaviorDefinition behavior)
    {
        if (behavior.MaxTargetManaCost <= 0
            && !behavior.RequiresTargetManaCostAtMostControllerPower)
        {
            return true;
        }

        if (!TryGetTargetManaCost(state, objectId, behavior, out var targetManaCost))
        {
            return false;
        }

        if (behavior.MaxTargetManaCost > 0
            && targetManaCost > behavior.MaxTargetManaCost)
        {
            return false;
        }

        return !behavior.RequiresTargetManaCostAtMostControllerPower
            || state.RunePools.TryGetValue(playerId, out var runePool)
                && targetManaCost <= runePool.Power;
    }

    private static bool AreTargetsManaCostAtMostDestroyedAdditionalCostUnit(
        MatchState state,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds,
        IReadOnlyList<string> destroyedAdditionalCostTargetObjectIds)
    {
        if (!behavior.RequiresTargetManaCostAtMostDestroyedAdditionalCostUnit)
        {
            return true;
        }

        if (destroyedAdditionalCostTargetObjectIds.Count != 1
            || !TryGetTargetManaCost(state, destroyedAdditionalCostTargetObjectIds[0], behavior, out var destroyedUnitManaCost))
        {
            return false;
        }

        return targetObjectIds.All(targetObjectId =>
            TryGetTargetManaCost(state, targetObjectId, behavior, out var targetManaCost)
            && targetManaCost <= destroyedUnitManaCost);
    }

    private static bool TryGetTargetManaCost(
        MatchState state,
        string objectId,
        CardBehaviorDefinition behavior,
        out int targetManaCost)
    {
        targetManaCost = 0;
        if (string.Equals(behavior.TargetScope, CardTargetScopes.StackSpell, StringComparison.Ordinal))
        {
            return TryGetStackItemManaCost(state, objectId, out targetManaCost);
        }

        if (!state.CardObjects.TryGetValue(objectId, out var targetState))
        {
            return false;
        }

        targetManaCost = targetState.ManaCost;
        return true;
    }

    private static bool TryGetStackItemManaCost(MatchState state, string stackItemId, out int manaCost)
    {
        manaCost = 0;
        var stackItem = state.StackItems.FirstOrDefault(candidate =>
            string.Equals(candidate.StackItemId, stackItemId, StringComparison.Ordinal));
        if (stackItem is null
            || !CardBehaviorRegistry.TryGetByEffectKind(stackItem.EffectKind, out var targetBehavior))
        {
            return false;
        }

        manaCost = targetBehavior.ManaCost;
        return true;
    }

    private static bool IsStackItemTargetConditionAllowed(
        MatchState state,
        string playerId,
        string objectId,
        int targetIndex,
        IReadOnlyList<string> targetObjectIds,
        CardBehaviorDefinition behavior)
    {
        if (!behavior.RequiresTargetStackItemControlledByEnemy
            && !behavior.RequiresTargetStackItemTargetsFriendlyUnitOrEquipment
            && !behavior.RequiresTargetStackItemTargetsFirstTarget
            && !behavior.RequiresTargetStackItemTargetsNoOtherFriendlyUnits
            && !behavior.AppliesPowerModifierToFirstTargetFromSecondStackSpellManaCost)
        {
            return true;
        }

        if (!IsStackItemTargetIndex(behavior.TargetScope, targetIndex))
        {
            return true;
        }

        var stackItem = state.StackItems.FirstOrDefault(candidate =>
            string.Equals(candidate.StackItemId, objectId, StringComparison.Ordinal));
        if (stackItem is null)
        {
            return false;
        }

        if (behavior.RequiresTargetStackItemControlledByEnemy
            && string.Equals(stackItem.ControllerId, playerId, StringComparison.Ordinal))
        {
            return false;
        }

        if (behavior.RequiresTargetStackItemTargetsFriendlyUnitOrEquipment
            && !stackItem.TargetObjectIds.Any(targetObjectId =>
                IsFriendlyUnitOrEquipmentObject(state, playerId, targetObjectId)))
        {
            return false;
        }

        if (behavior.RequiresTargetStackItemTargetsFirstTarget
            && (targetObjectIds.Count == 0
                || !stackItem.TargetObjectIds.Contains(targetObjectIds[0], StringComparer.Ordinal)))
        {
            return false;
        }

        return !behavior.RequiresTargetStackItemTargetsNoOtherFriendlyUnits
            || !stackItem.TargetObjectIds.Any(targetObjectId =>
                targetObjectIds.Count > 0
                && !string.Equals(targetObjectId, targetObjectIds[0], StringComparison.Ordinal)
                && IsControlledFieldObject(state, playerId, targetObjectId)
                && CardObjectHasTag(state.CardObjects, targetObjectId, CardObjectTags.UnitCard));
    }

    private static bool IsStackItemTargetIndex(string targetScope, int targetIndex)
    {
        return string.Equals(targetScope, CardTargetScopes.StackSpell, StringComparison.Ordinal)
            ? targetIndex == 0
            : string.Equals(targetScope, CardTargetScopes.FriendlyBattlefieldUnitThenStackSpell, StringComparison.Ordinal)
                && targetIndex == 1;
    }

    private static int ResolveSpellshieldTargetTaxMana(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds,
        out IReadOnlyList<string> spellshieldTaxTargetObjectIds)
    {
        spellshieldTaxTargetObjectIds = [];
        if (behavior.PlaysSourceToBaseAsUnit
            || behavior.PlaysSourceToBaseAsEquipment
            || targetObjectIds.Count == 0)
        {
            return 0;
        }

        var taxedTargetObjectIds = new List<string>();
        var taxMana = 0;
        foreach (var targetObjectId in targetObjectIds)
        {
            if (!IsEnemyFieldObject(state, playerId, targetObjectId)
                || !state.CardObjects.TryGetValue(targetObjectId, out var targetState))
            {
                continue;
            }

            var targetTax = CardResourceKeywordRules.SpellshieldTaxFromTags(targetState.Tags);
            if (targetTax <= 0)
            {
                continue;
            }

            taxMana += targetTax;
            taxedTargetObjectIds.Add(targetObjectId);
        }

        spellshieldTaxTargetObjectIds = taxedTargetObjectIds.ToArray();
        return taxMana;
    }

    private static bool IsFriendlyUnitOrEquipmentObject(MatchState state, string playerId, string objectId)
    {
        return IsControlledFieldObject(state, playerId, objectId)
            && (CardObjectHasTag(state.CardObjects, objectId, CardObjectTags.UnitCard)
                || CardObjectHasTag(state.CardObjects, objectId, CardObjectTags.EquipmentCard));
    }

    private static bool AreAttachDetachTargetsAllowed(
        MatchState state,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds)
    {
        if (!behavior.AttachesOrDetachesSecondTargetEquipmentToFirstTarget)
        {
            return true;
        }

        if (targetObjectIds.Count != 2)
        {
            return false;
        }

        var unitObjectId = targetObjectIds[0];
        var equipmentObjectId = targetObjectIds[1];
        if (!IsFieldUnitObject(state, unitObjectId)
            || !IsEquipmentObject(state, equipmentObjectId)
            || !CardObjectHasTag(state.CardObjects, equipmentObjectId, "武装")
            || !TryGetFieldControllerId(state.PlayerZones, unitObjectId, out var unitControllerId)
            || !TryGetFieldControllerId(state.PlayerZones, equipmentObjectId, out var equipmentControllerId)
            || !string.Equals(unitControllerId, equipmentControllerId, StringComparison.Ordinal)
            || !state.CardObjects.TryGetValue(equipmentObjectId, out var equipmentState))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(equipmentState.AttachedToObjectId)
            || string.Equals(equipmentState.AttachedToObjectId, unitObjectId, StringComparison.Ordinal);
    }

    private static bool HasValidSacredJudgmentKeepTargets(
        MatchState state,
        string sourceObjectId,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds)
    {
        if (!behavior.RecyclesUnkeptSacredJudgmentCards)
        {
            return true;
        }

        var targetSet = targetObjectIds.ToHashSet(StringComparer.Ordinal);
        if (targetSet.Contains(sourceObjectId))
        {
            return false;
        }

        var categorizedTargetIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, zones) in state.PlayerZones)
        {
            if (!HasExactlyTwoSacredJudgmentTargets(
                    targetSet,
                    SacredJudgmentFieldUnitIds(state.CardObjects, zones),
                    categorizedTargetIds)
                || !HasExactlyTwoSacredJudgmentTargets(
                    targetSet,
                    SacredJudgmentEquipmentIds(state.CardObjects, zones),
                    categorizedTargetIds)
                || !HasExactlyTwoSacredJudgmentTargets(
                    targetSet,
                    SacredJudgmentRuneIds(state.CardObjects, zones),
                    categorizedTargetIds)
                || !HasExactlyTwoSacredJudgmentTargets(
                    targetSet,
                    zones.Hand.Where(cardId => !string.Equals(cardId, sourceObjectId, StringComparison.Ordinal)),
                    categorizedTargetIds))
            {
                return false;
            }
        }

        return targetSet.SetEquals(categorizedTargetIds);
    }

    private static bool HasExactlyTwoSacredJudgmentTargets(
        ISet<string> targetSet,
        IEnumerable<string> categoryObjectIds,
        ISet<string> categorizedTargetIds)
    {
        var selectedObjectIds = categoryObjectIds
            .Where(targetSet.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (var objectId in selectedObjectIds)
        {
            categorizedTargetIds.Add(objectId);
        }

        return selectedObjectIds.Length == 2;
    }

    private static bool HasValidEachPlayerTopFiveUnitTargets(
        MatchState state,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds)
    {
        if (!behavior.PlaysEachPlayerTopFiveUnitToBase)
        {
            return true;
        }

        var playerIds = SeatPlayerIds(state);
        if (targetObjectIds.Count != playerIds.Length)
        {
            return false;
        }

        var selectedPlayerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var targetObjectId in targetObjectIds)
        {
            if (!CardObjectHasTag(state.CardObjects, targetObjectId, CardObjectTags.UnitCard)
                || !TryGetTopMainDeckOwner(state.PlayerZones, targetObjectId, behavior.MainDeckLookCount, out var playerId)
                || !selectedPlayerIds.Add(playerId))
            {
                return false;
            }
        }

        return selectedPlayerIds.SetEquals(playerIds);
    }

    private static bool HasRequiredAnyTargetTag(
        MatchState state,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds)
    {
        return string.IsNullOrWhiteSpace(behavior.AnyTargetRequiredTag)
            || targetObjectIds.Any(targetObjectId =>
                CardObjectHasTag(state.CardObjects, targetObjectId, behavior.AnyTargetRequiredTag));
    }

    private static bool IsFriendlyGraveyardCard(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Graveyard.Contains(objectId, StringComparer.Ordinal);
    }

    private static bool IsEnemyFieldObject(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.CardObjects.ContainsKey(objectId)
            && state.PlayerZones.Any(entry =>
                !string.Equals(entry.Key, playerId, StringComparison.Ordinal)
                && (entry.Value.Base.Contains(objectId, StringComparer.Ordinal)
                    || entry.Value.Battlefields.Contains(objectId, StringComparer.Ordinal)));
    }

    private static bool IsEnemyBattlefieldObject(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.CardObjects.ContainsKey(objectId)
            && state.PlayerZones.Any(entry =>
                !string.Equals(entry.Key, playerId, StringComparison.Ordinal)
                && entry.Value.Battlefields.Contains(objectId, StringComparer.Ordinal));
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

    private static bool IsFieldUnitObject(MatchState state, string objectId)
    {
        return IsFieldObject(state.PlayerZones, objectId)
            && !CardObjectHasTag(state.CardObjects, objectId, CardObjectTags.EquipmentCard);
    }

    private static bool IsSacredJudgmentKeepCandidate(MatchState state, string objectId)
    {
        return state.PlayerZones.Values.Any(zones =>
            zones.Hand.Contains(objectId, StringComparer.Ordinal)
            || zones.Base.Contains(objectId, StringComparer.Ordinal)
            || zones.Battlefields.Contains(objectId, StringComparer.Ordinal));
    }

    private static bool IsEquipmentObject(MatchState state, string objectId)
    {
        return IsFieldObject(state.PlayerZones, objectId)
            && CardObjectHasTag(state.CardObjects, objectId, CardObjectTags.EquipmentCard);
    }

    private static bool IsStackSpellItem(MatchState state, string stackItemId)
    {
        return state.StackItems.Any(stackItem =>
            string.Equals(stackItem.StackItemId, stackItemId, StringComparison.Ordinal)
            && CardBehaviorRegistry.TryGetByEffectKind(stackItem.EffectKind, out var behavior)
            && !behavior.PlaysSourceToBaseAsUnit
            && !behavior.PlaysSourceToBaseAsEquipment);
    }

    private static bool IsFriendlyEquipmentObject(MatchState state, string playerId, string objectId)
    {
        return IsControlledFieldObject(state, playerId, objectId)
            && CardObjectHasTag(state.CardObjects, objectId, CardObjectTags.EquipmentCard);
    }

    private static bool IsEnemyEquipmentObject(MatchState state, string playerId, string objectId)
    {
        return IsEnemyFieldObject(state, playerId, objectId)
            && CardObjectHasTag(state.CardObjects, objectId, CardObjectTags.EquipmentCard);
    }

    private static bool IsAttackingBattlefieldObject(MatchState state, string objectId)
    {
        return IsBattlefieldObject(state, objectId)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.IsAttacking;
    }

    private static bool IsTargetPowerAllowed(MatchState state, string objectId, CardBehaviorDefinition behavior)
    {
        return behavior.MaxTargetPower <= 0
            || (state.CardObjects.TryGetValue(objectId, out var targetState)
                && targetState.Power > 0
                && targetState.Power <= behavior.MaxTargetPower);
    }

    private static bool HasValidTotalTargetPower(
        MatchState state,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds)
    {
        if (behavior.MaxTotalTargetPower <= 0)
        {
            return true;
        }

        var totalPower = 0;
        foreach (var targetObjectId in targetObjectIds)
        {
            if (!state.CardObjects.TryGetValue(targetObjectId, out var targetState)
                || targetState.Power <= 0)
            {
                return false;
            }

            totalPower += targetState.Power;
            if (totalPower > behavior.MaxTotalTargetPower)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreTargetsAfterFirstPowerLessThanFirstTarget(
        MatchState state,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds)
    {
        if (!behavior.RequiresTargetsAfterFirstPowerLessThanFirstTarget)
        {
            return true;
        }

        if (targetObjectIds.Count < 2
            || !state.CardObjects.TryGetValue(targetObjectIds[0], out var firstTargetState))
        {
            return false;
        }

        for (var targetIndex = 1; targetIndex < targetObjectIds.Count; targetIndex++)
        {
            if (!state.CardObjects.TryGetValue(targetObjectIds[targetIndex], out var targetState)
                || targetState.Power >= firstTargetState.Power)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasValidTargetCount(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds)
    {
        var targetCountConditionApplies = TargetCountConditionApplies(state, playerId, behavior);
        var minTargetCount = MinTargetCount(behavior, targetCountConditionApplies);
        var maxTargetCount = MaxTargetCount(state, playerId, behavior, targetCountConditionApplies);
        return targetObjectIds.Count >= minTargetCount
            && targetObjectIds.Count <= maxTargetCount
            && (behavior.AllowsRepeatedTargets
                || targetObjectIds.Distinct(StringComparer.Ordinal).Count() == targetObjectIds.Count);
    }

    private static bool HasValidResolvedTargetCount(
        CardBehaviorDefinition behavior,
        StackItemState stackItem)
    {
        var targetCountConditionApplies = TargetCountConditionApplies(behavior, stackItem);
        var minTargetCount = MinTargetCount(behavior, targetCountConditionApplies);
        var maxTargetCount = !targetCountConditionApplies
            ? 0
            : behavior.UsesFriendlyBattlefieldUnitCountAsMaxTargetCount
                ? stackItem.TargetObjectIds.Count
                : behavior.RequiredTargetCount;
        return stackItem.TargetObjectIds.Count >= minTargetCount
            && stackItem.TargetObjectIds.Count <= maxTargetCount
            && (behavior.AllowsRepeatedTargets
                || stackItem.TargetObjectIds.Distinct(StringComparer.Ordinal).Count() == stackItem.TargetObjectIds.Count);
    }

    private static bool TryBuildOptionalCostPlan(
        IReadOnlyList<string>? optionalCosts,
        CardBehaviorDefinition behavior,
        out IReadOnlyList<string> normalizedOptionalCosts,
        out int extraManaCost,
        out int extraPowerCost,
        out int experienceCost,
        out int optionalCostManaReduction,
        out int effectRepeatCount,
        out IReadOnlyList<string> exhaustedOptionalCostTargetObjectIds,
        out IReadOnlyList<string> destroyedAdditionalCostTargetObjectIds,
        out IReadOnlyList<string> returnedAdditionalCostTargetObjectIds,
        out IReadOnlyList<string> discardedOptionalCostTargetObjectIds)
    {
        normalizedOptionalCosts = NormalizeOptionalCosts(optionalCosts);
        extraManaCost = 0;
        extraPowerCost = 0;
        experienceCost = 0;
        optionalCostManaReduction = 0;
        effectRepeatCount = 1;
        exhaustedOptionalCostTargetObjectIds = [];
        destroyedAdditionalCostTargetObjectIds = [];
        returnedAdditionalCostTargetObjectIds = [];
        discardedOptionalCostTargetObjectIds = [];

        if (normalizedOptionalCosts.Count == 0)
        {
            return true;
        }

        if (CardInteractionKeywordRules.TryBuildEchoOptionalCost(
            normalizedOptionalCosts,
            behavior,
            out var echoExtraManaCost,
            out var echoEffectRepeatCount))
        {
            extraManaCost = echoExtraManaCost;
            effectRepeatCount = echoEffectRepeatCount;
            return true;
        }

        if (CardPermissionKeywordRules.TryBuildHasteReadyOptionalCost(
            normalizedOptionalCosts,
            behavior,
            out var hasteExtraManaCost,
            out var hasteExtraPowerCost))
        {
            extraManaCost = hasteExtraManaCost;
            extraPowerCost = hasteExtraPowerCost;
            return true;
        }

        if (normalizedOptionalCosts.Count == 1
            && behavior.EffectRepeatCountIfExhaustFriendlyUnitOptionalCost > 0
            && TryParseExhaustFriendlyUnitOptionalCost(normalizedOptionalCosts[0], out var exhaustedTargetObjectId))
        {
            effectRepeatCount = behavior.EffectRepeatCountIfExhaustFriendlyUnitOptionalCost;
            exhaustedOptionalCostTargetObjectIds = [exhaustedTargetObjectId];
            return true;
        }

        if (normalizedOptionalCosts.Count == 1
            && behavior.RequiresDestroyFriendlyUnitAdditionalCost
            && TryParseDestroyFriendlyUnitAdditionalCost(
                normalizedOptionalCosts[0],
                out var destroyedFriendlyUnitTargetObjectId))
        {
            destroyedAdditionalCostTargetObjectIds = [destroyedFriendlyUnitTargetObjectId];
            return true;
        }

        if (normalizedOptionalCosts.Count == 1
            && behavior.RequiresDestroyFriendlyPowerfulUnitAdditionalCost
            && TryParseDestroyFriendlyPowerfulUnitAdditionalCost(
                normalizedOptionalCosts[0],
                out var destroyedTargetObjectId))
        {
            destroyedAdditionalCostTargetObjectIds = [destroyedTargetObjectId];
            return true;
        }

        if (normalizedOptionalCosts.Count == 1
            && behavior.RequiresDestroyFriendlyTraitUnitAdditionalCost
            && TryParseDestroyFriendlyTraitUnitAdditionalCost(
                normalizedOptionalCosts[0],
                out var destroyedTraitTargetObjectId))
        {
            destroyedAdditionalCostTargetObjectIds = [destroyedTraitTargetObjectId];
            return true;
        }

        if (normalizedOptionalCosts.Count == 1
            && behavior.RequiresReturnFriendlyEquipmentAdditionalCost
            && TryParseReturnFriendlyEquipmentAdditionalCost(
                normalizedOptionalCosts[0],
                out var returnedEquipmentTargetObjectId))
        {
            returnedAdditionalCostTargetObjectIds = [returnedEquipmentTargetObjectId];
            return true;
        }

        if (normalizedOptionalCosts.Count == 1
            && behavior.DamageAmountFromOptionalPowerCost
            && TryParseSpendPowerOptionalCost(normalizedOptionalCosts[0], out var powerCost))
        {
            extraPowerCost = powerCost;
            return true;
        }

        if (normalizedOptionalCosts.Count == 1
            && behavior.OptionalExperienceCost > 0
            && behavior.ManaReductionIfExperiencePaid > 0
            && TryParseSpendExperienceOptionalCost(normalizedOptionalCosts[0], out var spendExperienceCost)
            && spendExperienceCost == behavior.OptionalExperienceCost)
        {
            experienceCost = spendExperienceCost;
            optionalCostManaReduction = behavior.ManaReductionIfExperiencePaid;
            return true;
        }

        if (normalizedOptionalCosts.Count == 1
            && behavior.EffectRepeatCountIfDiscardHandCardOptionalCost > 0
            && TryParseDiscardHandCardOptionalCost(
                normalizedOptionalCosts[0],
                out var discardedTargetObjectId))
        {
            effectRepeatCount = behavior.EffectRepeatCountIfDiscardHandCardOptionalCost;
            discardedOptionalCostTargetObjectIds = [discardedTargetObjectId];
            return true;
        }

        return false;
    }

    private static bool TryParseExhaustFriendlyUnitOptionalCost(string optionalCost, out string targetObjectId)
    {
        targetObjectId = string.Empty;
        if (!optionalCost.StartsWith(ExhaustFriendlyUnitOptionalCostPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        targetObjectId = optionalCost[ExhaustFriendlyUnitOptionalCostPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(targetObjectId);
    }

    private static bool TryParseDestroyFriendlyPowerfulUnitAdditionalCost(
        string optionalCost,
        out string targetObjectId)
    {
        targetObjectId = string.Empty;
        if (!optionalCost.StartsWith(DestroyFriendlyPowerfulUnitAdditionalCostPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        targetObjectId = optionalCost[DestroyFriendlyPowerfulUnitAdditionalCostPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(targetObjectId);
    }

    private static bool TryParseDestroyFriendlyUnitAdditionalCost(
        string optionalCost,
        out string targetObjectId)
    {
        targetObjectId = string.Empty;
        if (!optionalCost.StartsWith(DestroyFriendlyUnitAdditionalCostPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        targetObjectId = optionalCost[DestroyFriendlyUnitAdditionalCostPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(targetObjectId);
    }

    private static bool TryParseDestroyFriendlyTraitUnitAdditionalCost(
        string optionalCost,
        out string targetObjectId)
    {
        targetObjectId = string.Empty;
        if (!optionalCost.StartsWith(DestroyFriendlyTraitUnitAdditionalCostPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        targetObjectId = optionalCost[DestroyFriendlyTraitUnitAdditionalCostPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(targetObjectId);
    }

    private static bool TryParseReturnFriendlyEquipmentAdditionalCost(
        string optionalCost,
        out string targetObjectId)
    {
        targetObjectId = string.Empty;
        if (!optionalCost.StartsWith(ReturnFriendlyEquipmentAdditionalCostPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        targetObjectId = optionalCost[ReturnFriendlyEquipmentAdditionalCostPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(targetObjectId);
    }

    private static bool TryParseDiscardHandCardOptionalCost(
        string optionalCost,
        out string targetObjectId)
    {
        targetObjectId = string.Empty;
        if (!optionalCost.StartsWith(DiscardHandCardOptionalCostPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        targetObjectId = optionalCost[DiscardHandCardOptionalCostPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(targetObjectId);
    }

    private static bool TryParseSpendPowerOptionalCost(string optionalCost, out int powerCost)
    {
        powerCost = 0;
        if (!optionalCost.StartsWith(SpendPowerOptionalCostPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(
                optionalCost[SpendPowerOptionalCostPrefix.Length..].Trim(),
                out powerCost)
            && powerCost >= 0;
    }

    private static bool TryParseSpendExperienceOptionalCost(string optionalCost, out int experienceCost)
    {
        experienceCost = 0;
        if (!optionalCost.StartsWith(SpendExperienceOptionalCostPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(
                optionalCost[SpendExperienceOptionalCostPrefix.Length..].Trim(),
                out experienceCost)
            && experienceCost > 0;
    }

    private static bool CanExhaustFriendlyUnitAsOptionalCost(
        MatchState state,
        string playerId,
        string targetObjectId)
    {
        return IsControlledFieldObject(state, playerId, targetObjectId)
            && state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            && !targetState.IsExhausted;
    }

    private static bool CanDestroyFriendlyPowerfulUnitAsAdditionalCost(
        MatchState state,
        string playerId,
        string targetObjectId)
    {
        return IsControlledFieldObject(state, playerId, targetObjectId)
            && state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            && targetState.Power >= 5;
    }

    private static bool CanDestroyFriendlyUnitAsAdditionalCost(
        MatchState state,
        string playerId,
        string targetObjectId)
    {
        return IsControlledFieldObject(state, playerId, targetObjectId)
            && state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            && targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal);
    }

    private static bool CanDestroyFriendlyTraitUnitAsAdditionalCost(
        MatchState state,
        string playerId,
        string targetObjectId)
    {
        if (!IsControlledFieldObject(state, playerId, targetObjectId)
            || !state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            || !targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal))
        {
            return false;
        }

        return targetState.Tags.Contains("鸟类", StringComparer.Ordinal)
            || targetState.Tags.Contains("猫科", StringComparer.Ordinal)
            || targetState.Tags.Contains("犬形", StringComparer.Ordinal)
            || targetState.Tags.Contains("魄罗", StringComparer.Ordinal);
    }

    private static bool CanReturnFriendlyEquipmentAsAdditionalCost(
        MatchState state,
        string playerId,
        string targetObjectId)
    {
        return IsFriendlyEquipmentObject(state, playerId, targetObjectId);
    }

    private static bool CanDiscardHandCardAsOptionalCost(
        MatchState state,
        string playerId,
        string sourceObjectId,
        string targetObjectId)
    {
        return !string.Equals(sourceObjectId, targetObjectId, StringComparison.Ordinal)
            && state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Hand.Contains(targetObjectId, StringComparer.Ordinal);
    }

    private static int ResolveCostReductionMana(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        if (behavior.CostReductionMana <= 0
            || string.Equals(behavior.CostReductionConditionKind, CardCostReductionConditionKinds.None, StringComparison.Ordinal))
        {
            return 0;
        }

        return behavior.CostReductionConditionKind switch
        {
            CardCostReductionConditionKinds.EnemyUnitDestroyedThisTurn
                => EnemyUnitDestroyedThisTurn(state, playerId) ? behavior.CostReductionMana : 0,
            CardCostReductionConditionKinds.ControllerHighestUnitPower
                => Math.Min(behavior.CostReductionMana, HighestControlledUnitPower(state, playerId)),
            CardCostReductionConditionKinds.OpponentWithinThreeOfWinningScore
                => OpponentWithinWinningScoreDistance(state, playerId, 3) ? behavior.CostReductionMana : 0,
            CardCostReductionConditionKinds.ControllerControlsTaggedUnit
                => ControllerControlsTaggedUnit(state, playerId, behavior.CostReductionUnitTag)
                    ? behavior.CostReductionMana
                    : 0,
            CardCostReductionConditionKinds.ControllerPlayedAnotherCardThisTurn
                => ControllerPlayedAnotherCardThisTurn(state, playerId) ? behavior.CostReductionMana : 0,
            _ => 0
        };
    }

    private static bool ControllerPlayedAnotherCardThisTurn(MatchState state, string playerId)
    {
        return state.PlayerCardsPlayedThisTurn.TryGetValue(playerId, out var count) && count > 0;
    }

    private static bool ShouldGrantBoonToSourceUnit(
        CardBehaviorDefinition behavior,
        StackItemState stackItem)
    {
        if (!behavior.GrantsBoonToSourceUnit)
        {
            return false;
        }

        return behavior.SourceBoonConditionKind switch
        {
            CardSourceBoonConditionKinds.None => true,
            CardSourceBoonConditionKinds.PlayedAfterAnotherCardThisTurn
                => stackItem.PlayedAfterAnotherCardThisTurn,
            _ => false
        };
    }

    private static bool TargetCountConditionApplies(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        return behavior.TargetCountConditionKind switch
        {
            CardTargetCountConditionKinds.None => true,
            CardTargetCountConditionKinds.PlayedAfterAnotherCardThisTurn
                => ControllerPlayedAnotherCardThisTurn(state, playerId),
            _ => false
        };
    }

    private static bool TargetCountConditionApplies(
        CardBehaviorDefinition behavior,
        StackItemState stackItem)
    {
        return behavior.TargetCountConditionKind switch
        {
            CardTargetCountConditionKinds.None => true,
            CardTargetCountConditionKinds.PlayedAfterAnotherCardThisTurn
                => stackItem.PlayedAfterAnotherCardThisTurn,
            _ => false
        };
    }

    private static bool ShouldCreateBaseUnitTokens(
        CardBehaviorDefinition behavior,
        StackItemState stackItem)
    {
        return behavior.CreatedBaseUnitTokenConditionKind switch
        {
            CardTokenCreationConditionKinds.None => true,
            CardTokenCreationConditionKinds.PlayedAfterAnotherCardThisTurn
                => stackItem.PlayedAfterAnotherCardThisTurn,
            _ => false
        };
    }

    private static bool EnemyUnitDestroyedThisTurn(MatchState state, string playerId)
    {
        return state.DestroyedUnitOwnerIdsThisTurn.Any(ownerPlayerId =>
            !string.Equals(ownerPlayerId, playerId, StringComparison.Ordinal));
    }

    private static int HighestControlledUnitPower(MatchState state, string playerId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return 0;
        }

        return zones.Base
            .Concat(zones.Battlefields)
            .Select(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject) ? cardObject.Power : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static bool ControllerControlsTaggedUnit(MatchState state, string playerId, string requiredTag)
    {
        if (string.IsNullOrWhiteSpace(requiredTag)
            || !state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        return zones.Base
            .Concat(zones.Battlefields)
            .Any(objectId => CardObjectHasTag(state.CardObjects, objectId, requiredTag));
    }

    private static bool CardObjectHasTag(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string objectId,
        string requiredTag)
    {
        if (string.IsNullOrWhiteSpace(requiredTag))
        {
            return true;
        }

        return cardObjects.TryGetValue(objectId, out var cardObject)
            && ParseDelimitedValues(requiredTag)
                .All(tag => cardObject.Tags.Contains(tag, StringComparer.Ordinal));
    }

    private static bool OpponentWithinWinningScoreDistance(MatchState state, string playerId, int distance)
    {
        var opponentId = OpponentOf(state, playerId);
        return opponentId is not null
            && state.PlayerScores.TryGetValue(opponentId, out var opponentScore)
            && opponentScore >= WinningScore - distance;
    }

    private static int MinTargetCount(
        CardBehaviorDefinition behavior,
        bool targetCountConditionApplies = true)
    {
        if (!targetCountConditionApplies)
        {
            return 0;
        }

        return behavior.MinTargetCount < 0 ? behavior.RequiredTargetCount : behavior.MinTargetCount;
    }

    private static int MaxTargetCount(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        bool targetCountConditionApplies = true)
    {
        if (!targetCountConditionApplies)
        {
            return 0;
        }

        if (!behavior.UsesFriendlyBattlefieldUnitCountAsMaxTargetCount)
        {
            return behavior.RequiredTargetCount;
        }

        return state.PlayerZones.TryGetValue(playerId, out var zones)
            ? zones.Battlefields.Count(objectId => state.CardObjects.ContainsKey(objectId))
            : 0;
    }

    private static string DescribeTargetCount(MatchState state, string playerId, CardBehaviorDefinition behavior)
    {
        var targetCountConditionApplies = TargetCountConditionApplies(state, playerId, behavior);
        var minTargetCount = MinTargetCount(behavior, targetCountConditionApplies);
        var maxTargetCount = MaxTargetCount(state, playerId, behavior, targetCountConditionApplies);
        return minTargetCount == maxTargetCount
            ? maxTargetCount.ToString()
            : $"{minTargetCount}-{maxTargetCount}";
    }

    private static string DescribeTargetScope(string targetScope)
    {
        return string.Equals(targetScope, CardTargetScopes.AnyUnit, StringComparison.Ordinal)
            ? "unit"
            : string.Equals(targetScope, CardTargetScopes.BaseUnit, StringComparison.Ordinal)
                ? "base unit"
                : string.Equals(targetScope, CardTargetScopes.BattlefieldUnitOrEquipment, StringComparison.Ordinal)
                    ? "battlefield unit or equipment"
            : string.Equals(targetScope, CardTargetScopes.FriendlyUnit, StringComparison.Ordinal)
                ? "friendly unit"
                : string.Equals(targetScope, CardTargetScopes.FriendlyUnitThenFriendlyUnit, StringComparison.Ordinal)
                    ? "friendly unit then another friendly unit"
                    : string.Equals(targetScope, CardTargetScopes.FriendlyThenEnemyUnits, StringComparison.Ordinal)
                        ? "friendly unit then enemy unit"
                        : string.Equals(targetScope, CardTargetScopes.FriendlyEquipmentThenEnemyEquipment, StringComparison.Ordinal)
                            ? "friendly equipment then enemy equipment"
                            : string.Equals(targetScope, CardTargetScopes.FriendlyThenEnemyBattlefieldUnits, StringComparison.Ordinal)
                                ? "friendly unit then enemy battlefield unit"
                                : string.Equals(targetScope, CardTargetScopes.FriendlyBattlefieldThenEnemyBattlefieldUnits, StringComparison.Ordinal)
                                    ? "friendly battlefield unit then enemy battlefield unit"
                                    : string.Equals(targetScope, CardTargetScopes.FriendlyBattlefieldUnitThenStackSpell, StringComparison.Ordinal)
                                        ? "friendly battlefield unit then spell on the stack"
                                        : string.Equals(targetScope, CardTargetScopes.FriendlyBattlefieldUnit, StringComparison.Ordinal)
                                            ? "friendly battlefield unit"
                                            : string.Equals(targetScope, CardTargetScopes.AnyUnitThenFriendlyMainDeckCard, StringComparison.Ordinal)
                                                ? "unit then friendly main deck card"
                                                : string.Equals(targetScope, CardTargetScopes.FriendlyHandCard, StringComparison.Ordinal)
                                                    ? "friendly hand card"
                                                    : string.Equals(targetScope, CardTargetScopes.FriendlyHandCardThenBattlefieldUnit, StringComparison.Ordinal)
                                                        ? "friendly hand card then battlefield unit"
                                                        : string.Equals(targetScope, CardTargetScopes.FriendlyMainDeckCard, StringComparison.Ordinal)
                                                            ? "friendly main deck card"
                                                            : string.Equals(targetScope, CardTargetScopes.FriendlyGraveyardCard, StringComparison.Ordinal)
                                                                ? "friendly graveyard card"
                                                                : string.Equals(targetScope, CardTargetScopes.FriendlyBaseUnit, StringComparison.Ordinal)
                                                                    ? "friendly base unit"
                                                                    : string.Equals(targetScope, CardTargetScopes.AttackingUnit, StringComparison.Ordinal)
                                                                        ? "attacking unit"
                                                                        : string.Equals(targetScope, CardTargetScopes.EnemyAttackingUnit, StringComparison.Ordinal)
                                                                            ? "enemy attacking unit"
                                                                            : string.Equals(targetScope, CardTargetScopes.EnemyBattlefieldUnit, StringComparison.Ordinal)
                                                                                ? "enemy battlefield unit"
                                                                                : string.Equals(targetScope, CardTargetScopes.EnemyUnit, StringComparison.Ordinal)
                                                                                    ? "enemy unit"
                                                                                    : string.Equals(targetScope, CardTargetScopes.EnemyUnitThenEnemyUnit, StringComparison.Ordinal)
                                                                                        ? "enemy unit then another enemy unit"
                                                                                        : string.Equals(targetScope, CardTargetScopes.OpponentHandCard, StringComparison.Ordinal)
                                                                                            ? "opponent hand card"
                                                                                            : string.Equals(targetScope, CardTargetScopes.OpponentGraveyardCard, StringComparison.Ordinal)
                                                                                                ? "opponent graveyard card"
                                                                                                : string.Equals(targetScope, CardTargetScopes.OpponentMainDeckTopCard, StringComparison.Ordinal)
                                                                                                    ? "opponent main deck top card"
                                                                                                    : string.Equals(targetScope, CardTargetScopes.AnyMainDeckTopFiveCard, StringComparison.Ordinal)
                                                                                                        ? "main deck top five card"
                                                                                                        : string.Equals(targetScope, CardTargetScopes.Equipment, StringComparison.Ordinal)
                                                                                                            ? "equipment"
                                                                                                            : string.Equals(targetScope, CardTargetScopes.StackSpell, StringComparison.Ordinal)
                                                                                                                ? "spell on the stack"
                                                                                                                : string.Equals(targetScope, CardTargetScopes.UnitThenItsControllersWeapon, StringComparison.Ordinal)
                                                                                                                    ? "unit and its controller's weapon"
                                                                                                                    : string.Equals(targetScope, CardTargetScopes.SacredJudgmentKeepCard, StringComparison.Ordinal)
                                                                                                                        ? "cards kept for Judgment Day"
                                                                                                                        : "battlefield unit";
    }

    private static StackResolutionResult ResolveStackItemEffect(MatchState state, StackItemState stackItem)
    {
        if (!CardBehaviorRegistry.TryGetByEffectKind(stackItem.EffectKind, out var behavior)
            || !HasValidResolvedTargetCount(behavior, stackItem))
        {
            return new StackResolutionResult(
                state.PlayerZones,
                state.CardObjects,
                state.PlayerScores,
                state.PlayerExperience,
                state.UntilEndOfTurnEffects,
                null,
                [],
                [],
                null,
                [],
                null,
                state.RngCursor);
        }

        var playerZones = NormalizeZonesForSeats(state);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var untilEndOfTurnEffects = state.UntilEndOfTurnEffects
            .Where(effectId => !string.IsNullOrWhiteSpace(effectId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(effectId => effectId, StringComparer.Ordinal)
            .ToList();
        var events = new List<GameEvent>();
        var destroyedObjectIds = new List<string>();
        var destroyedUnitOwnerIds = new List<string>();
        var counteredStackItemIds = new List<string>();
        var targetControllerDrawRecipientIds = new List<string>();
        var damageTriggeredDestroyTargetObjectIds = new HashSet<string>(StringComparer.Ordinal);
        var rngCursor = state.RngCursor;
        var playerScores = state.PlayerScores;
        var playerExperience = NormalizeExperienceForSeats(state);
        string? winnerPlayerId = null;
        string? extraTurnPlayerId = null;
        int? drawCountOverride = null;
        var preventDamageFromThisStackItem = ShouldPreventSpellOrSkillDamage(state, behavior);
        StackItemState[]? updatedStackItems = null;

        if (behavior.PlaysSourceToBaseAsEquipment)
        {
            PlaySourceEquipmentToBase(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                events);
        }

        if (behavior.PlaysSourceToBaseAsUnit)
        {
            PlaySourceUnitToBase(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                state.PlayerExperience,
                events);
        }

        if (behavior.GainExperienceOnPlay > 0)
        {
            playerExperience = GainExperience(
                playerExperience,
                stackItem.ControllerId,
                behavior.GainExperienceOnPlay,
                stackItem,
                events);
        }

        if (behavior.GainExperienceOnPlayPerFriendlyFieldUnit > 0)
        {
            var experienceAmount = CountControlledFieldUnitObjects(
                playerZones,
                cardObjects,
                stackItem.ControllerId) * behavior.GainExperienceOnPlayPerFriendlyFieldUnit;
            if (experienceAmount > 0)
            {
                playerExperience = GainExperience(
                    playerExperience,
                    stackItem.ControllerId,
                    experienceAmount,
                    stackItem,
                    events);
            }
        }

        if (behavior.LevelDrawOnPlayCount > 0
            && ControllerMeetsLevelExperienceThreshold(behavior, stackItem.ControllerId, playerExperience))
        {
            var drawApplication = ApplyDrawToPlayer(
                state,
                playerZones,
                playerScores,
                stackItem.ControllerId,
                behavior.LevelDrawOnPlayCount,
                rngCursor,
                events);
            playerScores = drawApplication.PlayerScores;
            winnerPlayerId = drawApplication.WinnerPlayerId;
            rngCursor = drawApplication.RngCursor;
        }

        if (behavior.RemovesDamageFromAllFriendlyBattlefieldUnits)
        {
            RemoveDamageFromFriendlyBattlefieldUnits(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                events);
        }

        if (behavior.CountersTargetStackSpell)
        {
            foreach (var targetStackItemId in stackItem.TargetObjectIds)
            {
                if (!TryCounterStackItem(
                        state,
                        playerZones,
                        targetStackItemId,
                        stackItem,
                        behavior,
                        out var counteredEvent))
                {
                    continue;
                }

                counteredStackItemIds.Add(targetStackItemId);
                events.Add(counteredEvent);
            }
        }

        if (behavior.GainsControlOfTargetStackSpell
            && TryGainControlOfTargetStackSpell(
                state.StackItems,
                behavior,
                stackItem,
                out var controlledStackItems,
                out var stackControlEvent))
        {
            updatedStackItems = controlledStackItems;
            events.Add(stackControlEvent);
        }

        if (behavior.AppliesPowerModifierToFirstTargetFromSecondStackSpellManaCost
            && stackItem.TargetObjectIds.Count >= 2
            && cardObjects.TryGetValue(stackItem.TargetObjectIds[0], out var dynamicPowerTargetState)
            && TryGetStackItemManaCost(state, stackItem.TargetObjectIds[1], out var stackSpellManaCost)
            && stackSpellManaCost != 0)
        {
            var modifiedFirstTargetState = ApplyPowerModifier(
                dynamicPowerTargetState,
                behavior,
                stackItem,
                stackItem.TargetObjectIds[0],
                stackSpellManaCost,
                out var powerEvent);
            cardObjects[stackItem.TargetObjectIds[0]] = modifiedFirstTargetState;
            events.Add(powerEvent);
        }

        if (behavior.AppliesPowerModifierToSourceUnit
            && behavior.PowerModifierAmount != 0
            && cardObjects.TryGetValue(stackItem.SourceObjectId, out var sourceUnitState))
        {
            var modifiedSourceUnitState = ApplyPowerModifier(
                sourceUnitState,
                behavior,
                stackItem,
                stackItem.SourceObjectId,
                behavior.PowerModifierAmount,
                out var powerEvent);
            cardObjects[stackItem.SourceObjectId] = modifiedSourceUnitState;
            events.Add(powerEvent);
        }

        if (ShouldGrantBoonToSourceUnit(behavior, stackItem)
            && cardObjects.TryGetValue(stackItem.SourceObjectId, out var sourceUnitStateForBoon))
        {
            var nextSourceUnitState = ApplyBoon(
                sourceUnitStateForBoon,
                behavior,
                stackItem,
                stackItem.SourceObjectId,
                out var boonEvents);
            cardObjects[stackItem.SourceObjectId] = nextSourceUnitState;
            events.AddRange(boonEvents);
        }

        if (behavior.BanishesAllFriendlyGraveyardUnits)
        {
            BanishFriendlyGraveyardUnits(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                events);
        }

        if (behavior.PreventsAllSpellAndSkillDamageThisTurn)
        {
            untilEndOfTurnEffects = untilEndOfTurnEffects
                .Append(PreventSpellAndSkillDamageThisTurnEffectId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(effectId => effectId, StringComparer.Ordinal)
                .ToList();
            events.Add(new GameEvent(
                "STATUS_EFFECT_APPLIED",
                $"{behavior.DisplayName}无效化本回合法术或技能伤害",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["effectId"] = PreventSpellAndSkillDamageThisTurnEffectId,
                    ["scope"] = "SPELL_OR_SKILL_DAMAGE_THIS_TURN"
                }));
        }

        if (behavior.AttachesOrDetachesSecondTargetEquipmentToFirstTarget
            && TryAttachOrDetachSecondTargetEquipmentToFirstTarget(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                out var attachmentEvent))
        {
            events.Add(attachmentEvent);
        }

        if (behavior.DiscardsTargetFromHand)
        {
            foreach (var targetObjectId in stackItem.TargetObjectIds)
            {
                if (!TryDiscardCardFromHand(playerZones, stackItem.ControllerId, targetObjectId))
                {
                    continue;
                }

                var payload = new Dictionary<string, object?>
                {
                    ["playerId"] = stackItem.ControllerId,
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["targetObjectId"] = targetObjectId,
                    ["destinationZone"] = "GRAVEYARD"
                };
                if (cardObjects.TryGetValue(targetObjectId, out var discardedObjectState)
                    && discardedObjectState.ManaCost > 0)
                {
                    payload["discardedManaCost"] = discardedObjectState.ManaCost;
                }

                events.Add(new GameEvent(
                    "CARD_DISCARDED",
                    $"{behavior.DisplayName}弃置手牌",
                    payload));
            }
        }

        if (behavior.DiscardsTargetFromOwnerHand)
        {
            foreach (var targetObjectId in stackItem.TargetObjectIds)
            {
                if (!TryDiscardCardFromAnyHand(playerZones, targetObjectId, out var ownerPlayerId))
                {
                    continue;
                }

                events.Add(new GameEvent(
                    "CARD_DISCARDED",
                    $"{behavior.DisplayName}弃置目标手牌",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = ownerPlayerId,
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["targetObjectId"] = targetObjectId,
                        ["discardedByPlayerId"] = stackItem.ControllerId,
                        ["destinationZone"] = "GRAVEYARD"
                    }));
            }
        }

        if (behavior.DrawsControllerAndOtherPlayers)
        {
            foreach (var drawPlayerId in ControllerAndOtherPlayerIds(state, stackItem.ControllerId))
            {
                var drawApplication = ApplyDrawToPlayer(
                    state,
                    playerZones,
                    playerScores,
                    drawPlayerId,
                    behavior.DrawCount * stackItem.EffectRepeatCount,
                    rngCursor,
                    events);
                playerScores = drawApplication.PlayerScores;
                winnerPlayerId = drawApplication.WinnerPlayerId;
                rngCursor = drawApplication.RngCursor;

                if (winnerPlayerId is not null)
                {
                    break;
                }
            }

            drawCountOverride = 0;
        }
        else if (behavior.CallsRuneForControllerAndOtherPlayers)
        {
            foreach (var runePlayerId in ControllerAndOtherPlayerIds(state, stackItem.ControllerId))
            {
                var runeCallResult = CallRunes(
                    playerZones,
                    cardObjects,
                    runePlayerId,
                    behavior.RuneCallCount);
                events.Add(new GameEvent(
                    "RUNES_CALLED",
                    $"{runePlayerId} 召出 {runeCallResult.CalledRuneObjectIds.Count} 张符文",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = runePlayerId,
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["count"] = runeCallResult.CalledRuneObjectIds.Count,
                        ["runeObjectIds"] = runeCallResult.CalledRuneObjectIds.ToArray()
                    }));
            }
        }
        else if (behavior.PlaysEachPlayerTopFiveUnitToBase)
        {
            var topDeckPlayResult = PlayEachPlayerTopFiveUnitsToBase(
                state,
                playerZones,
                cardObjects,
                stackItem.ControllerId,
                stackItem.SourceObjectId,
                stackItem.TargetObjectIds,
                behavior.MainDeckLookCount,
                rngCursor);
            events.AddRange(topDeckPlayResult.Events);
            rngCursor = topDeckPlayResult.RngCursor;
            drawCountOverride = 0;
        }
        else if (behavior.DrawsSelectedMainDeckTarget)
        {
            var topDeckSelectionResult = DrawSelectedMainDeckTargetsAndRecycleRest(
                state,
                playerZones,
                stackItem.ControllerId,
                stackItem.SourceObjectId,
                MainDeckTargetObjectIds(stackItem.TargetObjectIds, behavior),
                behavior.MainDeckLookCount,
                behavior.RecyclesUnselectedMainDeckLookCards);
            events.AddRange(topDeckSelectionResult.Events);
            rngCursor = topDeckSelectionResult.RngCursor;
            drawCountOverride = 0;
        }
        else if (behavior.RecyclesUnkeptSacredJudgmentCards)
        {
            var recycleResult = RecycleUnkeptSacredJudgmentCards(
                state,
                playerZones,
                cardObjects,
                stackItem.SourceObjectId,
                stackItem.TargetObjectIds,
                rngCursor);
            events.AddRange(recycleResult.Events);
            rngCursor = recycleResult.RngCursor;
        }
        else if (behavior.RecyclesTargets)
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
        else if (behavior.RuneCallCount > 0)
        {
            if (behavior.DrawsBeforeRuneCall)
            {
                var preRuneDrawCount = ResolveDrawCount(playerZones, cardObjects, stackItem.ControllerId, behavior);
                if (ShouldDrawForBehavior(behavior, stackItem, destroyedObjectIds, preRuneDrawCount))
                {
                    foreach (var drawPlayerId in DrawRecipientPlayerIds(behavior, stackItem.ControllerId, targetControllerDrawRecipientIds))
                    {
                        var drawApplication = ApplyDrawToPlayer(
                            state,
                            playerZones,
                            playerScores,
                            drawPlayerId,
                            preRuneDrawCount * stackItem.EffectRepeatCount,
                            rngCursor,
                            events);
                        playerScores = drawApplication.PlayerScores;
                        winnerPlayerId = drawApplication.WinnerPlayerId;
                        rngCursor = drawApplication.RngCursor;

                        if (winnerPlayerId is not null)
                        {
                            break;
                        }
                    }
                }

                drawCountOverride = 0;
            }

            if (winnerPlayerId is null)
            {
                var runeCallResult = CallRunes(
                    playerZones,
                    cardObjects,
                    stackItem.ControllerId,
                    behavior.RuneCallCount);
                events.Add(new GameEvent(
                    "RUNES_CALLED",
                    $"{stackItem.ControllerId} 召出 {runeCallResult.CalledRuneObjectIds.Count} 张符文",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = stackItem.ControllerId,
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["count"] = runeCallResult.CalledRuneObjectIds.Count,
                        ["runeObjectIds"] = runeCallResult.CalledRuneObjectIds.ToArray()
                    }));
                if (runeCallResult.CalledRuneObjectIds.Count < behavior.RuneCallCount
                    && behavior.DrawCountIfRuneCallFails > 0)
                {
                    drawCountOverride = behavior.DrawCountIfRuneCallFails;
                }
            }
        }
        else if (behavior.DestroysAllUnits)
        {
            foreach (var targetObjectId in GetFieldUnitObjectIds(playerZones))
            {
                if (!TryDestroyTarget(playerZones, cardObjects, targetObjectId, out var removalResult))
                {
                    continue;
                }

                events.Add(BuildFieldRemovalEvent(
                    behavior.DisplayName,
                    stackItem,
                    targetObjectId,
                    removalResult));
                if (!removalResult.WasDestroyed)
                {
                    continue;
                }

                destroyedObjectIds.Add(targetObjectId);
                if (removalResult.WasUnit)
                {
                    destroyedUnitOwnerIds.Add(removalResult.OwnerPlayerId);
                }
            }
        }
        else if (behavior.DestroysAllEquipment)
        {
            foreach (var targetObjectId in GetFieldEquipmentObjectIds(playerZones, cardObjects))
            {
                if (!TryDestroyTarget(playerZones, cardObjects, targetObjectId, out var removalResult))
                {
                    continue;
                }

                events.Add(BuildFieldRemovalEvent(
                    behavior.DisplayName,
                    stackItem,
                    targetObjectId,
                    removalResult));
                destroyedObjectIds.Add(targetObjectId);
            }
        }
        else if (behavior.ReturnsAllUnitsToHand)
        {
            foreach (var targetObjectId in GetFieldUnitObjectIds(playerZones))
            {
                if (!TryReturnTargetToHand(playerZones, cardObjects, targetObjectId, out var returnedOwnerPlayerId, out _))
                {
                    continue;
                }

                events.Add(new GameEvent(
                    "UNIT_RETURNED_TO_HAND",
                    $"{behavior.DisplayName}让单位返回手牌",
                    new Dictionary<string, object?>
                    {
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["targetObjectId"] = targetObjectId,
                        ["ownerPlayerId"] = returnedOwnerPlayerId
                    }));
            }
        }
        else if (behavior.ReturnsAllFieldObjectsToHand)
        {
            foreach (var targetObjectId in GetFieldObjectIds(playerZones))
            {
                if (!TryReturnTargetToHand(playerZones, cardObjects, targetObjectId, out var returnedOwnerPlayerId, out var returnedWasEquipment))
                {
                    continue;
                }

                events.Add(BuildReturnedToHandEvent(
                    behavior.DisplayName,
                    stackItem,
                    targetObjectId,
                    returnedOwnerPlayerId,
                    returnedWasEquipment));
            }
        }
        else if (behavior.AppliesStatusEffectToAllUnits)
        {
            ApplyStatusEffectsToFieldUnits(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                events);
        }
        else if (behavior.ExhaustsAllFriendlyUnits)
        {
            for (var repeatIndex = 0; repeatIndex < stackItem.EffectRepeatCount; repeatIndex++)
            {
                foreach (var targetObjectId in GetControlledFieldUnitObjectIds(playerZones, cardObjects, stackItem.ControllerId))
                {
                    var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
                        ? existingTarget
                        : new CardObjectState(targetObjectId);

                    targetState = ApplyExhaustState(
                        targetState,
                        behavior,
                        stackItem,
                        targetObjectId,
                        out var exhaustedEvent);
                    cardObjects[targetObjectId] = targetState;
                    events.Add(exhaustedEvent);
                }
            }

            if (behavior.DamagesAllBattlefieldUnits)
            {
                ApplyDamageToBattlefieldUnits(
                    playerZones,
                    cardObjects,
                    behavior,
                    stackItem,
                    ResolveDamageAmount(state, stackItem, behavior),
                    events,
                    damageTriggeredDestroyTargetObjectIds,
                    preventDamageFromThisStackItem,
                    PreventSpellAndSkillDamageThisTurnEffectId);
            }
        }
        else if (behavior.DamagesAllBattlefieldUnits)
        {
            ApplyDamageToBattlefieldUnits(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                ResolveDamageAmount(state, stackItem, behavior),
                events,
                damageTriggeredDestroyTargetObjectIds,
                preventDamageFromThisStackItem,
                PreventSpellAndSkillDamageThisTurnEffectId);
        }
        else if (behavior.DamagesAllEnemyCombatUnits)
        {
            ApplyDamageToEnemyCombatUnits(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                ResolveDamageAmount(state, stackItem, behavior),
                events,
                damageTriggeredDestroyTargetObjectIds,
                preventDamageFromThisStackItem,
                PreventSpellAndSkillDamageThisTurnEffectId);
        }
        else if (behavior.DamagesAllEnemyBattlefieldUnits)
        {
            ApplyDamageToEnemyBattlefieldUnits(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                ResolveDamageAmount(state, stackItem, behavior),
                events,
                damageTriggeredDestroyTargetObjectIds,
                preventDamageFromThisStackItem,
                PreventSpellAndSkillDamageThisTurnEffectId);
        }
        else if (behavior.ModifiesAllFriendlyBattlefieldUnits
            || behavior.ModifiesAllEnemyBattlefieldUnits)
        {
            ApplyBattlefieldPowerModifiers(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                events);
        }
        else if (behavior.ModifiesAllFriendlyUnits
            && behavior.PowerModifierAmount != 0)
        {
            for (var repeatIndex = 0; repeatIndex < stackItem.EffectRepeatCount; repeatIndex++)
            {
                foreach (var targetObjectId in GetControlledFieldUnitObjectIds(playerZones, cardObjects, stackItem.ControllerId))
                {
                    if (!CardObjectHasTag(cardObjects, targetObjectId, behavior.PowerModifierUnitTag))
                    {
                        continue;
                    }

                    var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
                        ? existingTarget
                        : new CardObjectState(targetObjectId);

                    targetState = ApplyPowerModifier(
                        targetState,
                        behavior,
                        stackItem,
                        targetObjectId,
                        behavior.PowerModifierAmount,
                        out var powerEvent);
                    cardObjects[targetObjectId] = targetState;
                    events.Add(powerEvent);
                }
            }
        }
        else if (behavior.ModifiesAllEnemyUnits
            && behavior.PowerModifierAmount != 0)
        {
            for (var repeatIndex = 0; repeatIndex < stackItem.EffectRepeatCount; repeatIndex++)
            {
                foreach (var targetObjectId in GetEnemyFieldUnitObjectIds(playerZones, cardObjects, stackItem.ControllerId))
                {
                    if (!CardObjectHasTag(cardObjects, targetObjectId, behavior.PowerModifierUnitTag))
                    {
                        continue;
                    }

                    var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
                        ? existingTarget
                        : new CardObjectState(targetObjectId);

                    targetState = ApplyPowerModifier(
                        targetState,
                        behavior,
                        stackItem,
                        targetObjectId,
                        behavior.PowerModifierAmount,
                        out var powerEvent);
                    cardObjects[targetObjectId] = targetState;
                    events.Add(powerEvent);
                }
            }
        }
        else if (behavior.GrantsBoonToAllFriendlyUnits)
        {
            foreach (var targetObjectId in GetControlledFieldUnitObjectIds(playerZones, cardObjects, stackItem.ControllerId))
            {
                var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
                    ? existingTarget
                    : new CardObjectState(targetObjectId);

                targetState = ApplyBoon(
                    targetState,
                    behavior,
                    stackItem,
                    targetObjectId,
                    out var boonEvents);
                cardObjects[targetObjectId] = targetState;
                events.AddRange(boonEvents);
            }
        }
        else if (behavior.ReadiesAllFriendlyUnits)
        {
            for (var repeatIndex = 0; repeatIndex < stackItem.EffectRepeatCount; repeatIndex++)
            {
                foreach (var targetObjectId in GetControlledFieldUnitObjectIds(playerZones, cardObjects, stackItem.ControllerId))
                {
                    var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
                        ? existingTarget
                        : new CardObjectState(targetObjectId);

                    targetState = ApplyReadyState(
                        targetState,
                        behavior,
                        stackItem,
                        targetObjectId,
                        out var readyEvent);
                    cardObjects[targetObjectId] = targetState;
                    events.Add(readyEvent);
                }
            }
        }
        else if (behavior.CreatedBaseUnitTokenCount > 0
            && !behavior.CreatedBaseUnitTokenCopiesFirstTarget
            && ShouldCreateBaseUnitTokens(behavior, stackItem))
        {
            CreateBaseUnitTokens(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                events);
        }
        else if (behavior.DiscardsAllPlayersHandsThenDraws)
        {
            foreach (var discardPlayerId in SeatPlayerIds(state))
            {
                if (!TryDiscardPlayerHand(playerZones, discardPlayerId, out var discardedObjectIds)
                    || discardedObjectIds.Count == 0)
                {
                    continue;
                }

                events.Add(new GameEvent(
                    "CARDS_DISCARDED",
                    $"{behavior.DisplayName}弃置玩家手牌",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = discardPlayerId,
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["count"] = discardedObjectIds.Count,
                        ["objectIds"] = discardedObjectIds.ToArray(),
                        ["destinationZone"] = "GRAVEYARD"
                    }));
            }

            var perPlayerDrawCount = ResolveDrawCount(playerZones, cardObjects, stackItem.ControllerId, behavior);
            foreach (var drawPlayerId in SeatPlayerIds(state))
            {
                var drawApplication = ApplyDrawToPlayer(
                    state,
                    playerZones,
                    playerScores,
                    drawPlayerId,
                    perPlayerDrawCount * stackItem.EffectRepeatCount,
                    rngCursor,
                    events);
                playerScores = drawApplication.PlayerScores;
                winnerPlayerId = drawApplication.WinnerPlayerId;
                rngCursor = drawApplication.RngCursor;

                if (winnerPlayerId is not null)
                {
                    break;
                }
            }

            drawCountOverride = 0;
        }
        else if (behavior.ReturnsGraveyardTargetToHand)
        {
            foreach (var targetObjectId in stackItem.TargetObjectIds)
            {
                if (!TryReturnGraveyardCardToHand(playerZones, stackItem.ControllerId, targetObjectId))
                {
                    continue;
                }

                events.Add(new GameEvent(
                    "CARD_RETURNED_TO_HAND",
                    $"{behavior.DisplayName}让废牌堆里的牌返回手牌",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = stackItem.ControllerId,
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["targetObjectId"] = targetObjectId,
                        ["sourceZone"] = "GRAVEYARD",
                        ["destinationZone"] = "HAND"
                }));
            }
        }
        else if (behavior.PlaysGraveyardTargetToBase)
        {
            foreach (var targetObjectId in stackItem.TargetObjectIds)
            {
                if (!TryPlayGraveyardCardToBase(playerZones, cardObjects, stackItem.ControllerId, targetObjectId))
                {
                    continue;
                }

                events.Add(new GameEvent(
                    "UNIT_PLAYED_TO_BASE",
                    $"{behavior.DisplayName}打出废牌堆里的单位到基地",
                    new Dictionary<string, object?>
                    {
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["targetObjectId"] = targetObjectId,
                        ["ownerPlayerId"] = stackItem.ControllerId,
                        ["sourceZone"] = "GRAVEYARD",
                        ["destinationZone"] = "BASE"
                    }));
            }
        }
        else if (behavior.PlaysHandTargetToBase)
        {
            foreach (var targetObjectId in stackItem.TargetObjectIds)
            {
                if (!TryPlayHandCardToBase(
                        playerZones,
                        cardObjects,
                        targetObjectId,
                        behavior.StatusEffectId,
                        out var ownerPlayerId,
                        out _))
                {
                    continue;
                }

                events.Add(new GameEvent(
                    "UNIT_PLAYED_TO_BASE",
                    $"{behavior.DisplayName}打出手牌里的单位到基地",
                    new Dictionary<string, object?>
                    {
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["targetObjectId"] = targetObjectId,
                        ["ownerPlayerId"] = ownerPlayerId,
                        ["sourceZone"] = "HAND",
                        ["destinationZone"] = "BASE"
                    }));

                if (!string.IsNullOrWhiteSpace(behavior.StatusEffectId))
                {
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
            }
        }
        else if (behavior.PlaysOpponentTopMainDeckUnitToBase)
        {
            foreach (var targetObjectId in stackItem.TargetObjectIds)
            {
                if (!TryPlayOpponentTopMainDeckUnitToBase(
                        playerZones,
                        cardObjects,
                        stackItem.ControllerId,
                        targetObjectId,
                        out var ownerPlayerId,
                        out _))
                {
                    continue;
                }

                events.Add(new GameEvent(
                    "UNIT_PLAYED_TO_BASE",
                    $"{behavior.DisplayName}打出对手主牌堆顶部单位到己方基地",
                    new Dictionary<string, object?>
                    {
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["targetObjectId"] = targetObjectId,
                        ["ownerPlayerId"] = ownerPlayerId,
                        ["playedByPlayerId"] = stackItem.ControllerId,
                        ["sourceZone"] = "MAIN_DECK",
                        ["destinationZone"] = "BASE"
                    }));
            }
        }
        else if (behavior.DealsMutualTargetPowerDamage
            && stackItem.TargetObjectIds.Count >= 2)
        {
            var firstTargetObjectId = stackItem.TargetObjectIds[0];
            var secondTargetObjectId = stackItem.TargetObjectIds[1];

            if (behavior.MovesFirstTargetToSecondTargetLocation
                && TryMoveFirstTargetToSecondTargetLocation(
                    playerZones,
                    firstTargetObjectId,
                    secondTargetObjectId,
                    out var destinationPlayerId,
                    out var destinationZone))
            {
                events.Add(new GameEvent(
                    "UNIT_MOVED_TO_UNIT_LOCATION",
                    $"{behavior.DisplayName}让单位移动到另一名单位所在位置",
                    new Dictionary<string, object?>
                    {
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["targetObjectId"] = firstTargetObjectId,
                        ["destinationTargetObjectId"] = secondTargetObjectId,
                        ["destinationPlayerId"] = destinationPlayerId,
                        ["destinationZone"] = destinationZone
                    }));
            }

            if (behavior.PowerModifierAmount != 0
                && cardObjects.TryGetValue(firstTargetObjectId, out var currentFirstTargetStateForModifier))
            {
                var modifiedFirstTargetState = ApplyPowerModifier(
                    currentFirstTargetStateForModifier,
                    behavior,
                    stackItem,
                    firstTargetObjectId,
                    behavior.PowerModifierAmount,
                    out var powerEvent);
                cardObjects[firstTargetObjectId] = modifiedFirstTargetState;
                events.Add(powerEvent);
            }

            for (var repeatIndex = 0; repeatIndex < stackItem.EffectRepeatCount; repeatIndex++)
            {
                var firstTargetPower = cardObjects.TryGetValue(firstTargetObjectId, out var firstTargetState)
                    ? Math.Max(0, firstTargetState.Power)
                    : 0;
                var secondTargetPower = cardObjects.TryGetValue(secondTargetObjectId, out var secondTargetState)
                    ? Math.Max(0, secondTargetState.Power)
                    : 0;

                if (firstTargetPower > 0
                    && cardObjects.ContainsKey(secondTargetObjectId))
                {
                    var damageApplication = ApplyDamageToCardObject(
                        cardObjects,
                        secondTargetObjectId,
                        firstTargetPower,
                        damageTriggeredDestroyTargetObjectIds,
                        preventDamageFromThisStackItem,
                        PreventSpellAndSkillDamageThisTurnEffectId);
                    events.Add(new GameEvent(
                        "DAMAGE_APPLIED",
                        $"{behavior.DisplayName}造成单位互斗伤害",
                        BuildDamagePayload(
                            stackItem.SourceObjectId,
                            secondTargetObjectId,
                            damageApplication,
                            firstTargetObjectId)));
                }

                if (secondTargetPower > 0
                    && cardObjects.ContainsKey(firstTargetObjectId))
                {
                    var damageApplication = ApplyDamageToCardObject(
                        cardObjects,
                        firstTargetObjectId,
                        secondTargetPower,
                        damageTriggeredDestroyTargetObjectIds,
                        preventDamageFromThisStackItem,
                        PreventSpellAndSkillDamageThisTurnEffectId);
                    events.Add(new GameEvent(
                        "DAMAGE_APPLIED",
                        $"{behavior.DisplayName}造成单位互斗伤害",
                        BuildDamagePayload(
                            stackItem.SourceObjectId,
                            firstTargetObjectId,
                            damageApplication,
                            secondTargetObjectId)));
                }
            }
        }
        else if (behavior.DealsSourceAndTargetPowerDamage
            && stackItem.TargetObjectIds.Count >= 1)
        {
            var targetObjectId = stackItem.TargetObjectIds[0];
            var sourcePower = cardObjects.TryGetValue(stackItem.SourceObjectId, out var sourceState)
                ? Math.Max(0, sourceState.Power)
                : 0;
            var targetPower = cardObjects.TryGetValue(targetObjectId, out var targetState)
                ? Math.Max(0, targetState.Power)
                : 0;

            if (sourcePower > 0
                && cardObjects.ContainsKey(targetObjectId))
            {
                var damageApplication = ApplyDamageToCardObject(
                    cardObjects,
                    targetObjectId,
                    sourcePower,
                    damageTriggeredDestroyTargetObjectIds,
                    preventDamageFromThisStackItem,
                    PreventSpellAndSkillDamageThisTurnEffectId);
                events.Add(new GameEvent(
                    "DAMAGE_APPLIED",
                    $"{behavior.DisplayName}造成源单位互斗伤害",
                    BuildDamagePayload(
                        stackItem.SourceObjectId,
                        targetObjectId,
                        damageApplication,
                        stackItem.SourceObjectId)));
            }

            if (targetPower > 0
                && cardObjects.ContainsKey(stackItem.SourceObjectId))
            {
                var damageApplication = ApplyDamageToCardObject(
                    cardObjects,
                    stackItem.SourceObjectId,
                    targetPower,
                    damageTriggeredDestroyTargetObjectIds,
                    preventDamageFromThisStackItem,
                    PreventSpellAndSkillDamageThisTurnEffectId);
                events.Add(new GameEvent(
                    "DAMAGE_APPLIED",
                    $"{behavior.DisplayName}造成目标单位互斗伤害",
                    BuildDamagePayload(
                        stackItem.SourceObjectId,
                        stackItem.SourceObjectId,
                        damageApplication,
                        targetObjectId)));
            }
        }
        else if (behavior.DamagesSecondTargetByFirstTargetPower
            && stackItem.TargetObjectIds.Count >= 2)
        {
            var firstTargetObjectId = stackItem.TargetObjectIds[0];
            var damagedTargetObjectId = stackItem.TargetObjectIds[1];

            var damageAmount = cardObjects.TryGetValue(firstTargetObjectId, out var firstTargetState)
                ? Math.Max(0, firstTargetState.Power)
                : 0;
            if (damageAmount > 0
                && IsFieldObject(playerZones, damagedTargetObjectId)
                && cardObjects.ContainsKey(damagedTargetObjectId))
            {
                var damageApplication = ApplyDamageToCardObject(
                    cardObjects,
                    damagedTargetObjectId,
                    damageAmount,
                    damageTriggeredDestroyTargetObjectIds,
                    preventDamageFromThisStackItem,
                    PreventSpellAndSkillDamageThisTurnEffectId);
                events.Add(new GameEvent(
                    "DAMAGE_APPLIED",
                    $"{behavior.DisplayName}按友方单位战力造成伤害",
                    BuildDamagePayload(
                        stackItem.SourceObjectId,
                        damagedTargetObjectId,
                        damageApplication,
                        firstTargetObjectId)));
            }
        }
        else if (behavior.ReadiesFirstTargetAndDamagesSecondByFirstPower
            && stackItem.TargetObjectIds.Count >= 2)
        {
            var readyTargetObjectId = stackItem.TargetObjectIds[0];
            var damagedTargetObjectId = stackItem.TargetObjectIds[1];

            if (cardObjects.TryGetValue(readyTargetObjectId, out var readyTargetState))
            {
                var readiedTargetState = ApplyReadyState(
                    readyTargetState,
                    behavior,
                    stackItem,
                    readyTargetObjectId,
                    out var readyEvent);
                cardObjects[readyTargetObjectId] = readiedTargetState;
                events.Add(readyEvent);

                var damageAmount = Math.Max(0, readiedTargetState.Power);
                if (damageAmount > 0
                    && IsFieldObject(playerZones, damagedTargetObjectId)
                    && cardObjects.ContainsKey(damagedTargetObjectId))
                {
                    var damageApplication = ApplyDamageToCardObject(
                        cardObjects,
                        damagedTargetObjectId,
                        damageAmount,
                        damageTriggeredDestroyTargetObjectIds,
                        preventDamageFromThisStackItem,
                        PreventSpellAndSkillDamageThisTurnEffectId);
                    events.Add(new GameEvent(
                        "DAMAGE_APPLIED",
                        $"{behavior.DisplayName}按友方单位战力造成伤害",
                        BuildDamagePayload(
                            stackItem.SourceObjectId,
                            damagedTargetObjectId,
                            damageApplication,
                            readyTargetObjectId)));
                }
            }
        }
        else if (behavior.SetsFirstTargetPowerToSecondIfLower
            && stackItem.TargetObjectIds.Count >= 2)
        {
            var modifiedTargetObjectId = stackItem.TargetObjectIds[0];
            var comparisonTargetObjectId = stackItem.TargetObjectIds[1];
            if (cardObjects.TryGetValue(modifiedTargetObjectId, out var modifiedTargetState)
                && cardObjects.TryGetValue(comparisonTargetObjectId, out var comparisonTargetState)
                && modifiedTargetState.Power < comparisonTargetState.Power)
            {
                var powerModifierAmount = comparisonTargetState.Power - modifiedTargetState.Power;
                var nextTargetState = ApplyPowerModifier(
                    modifiedTargetState,
                    behavior,
                    stackItem,
                    modifiedTargetObjectId,
                    powerModifierAmount,
                    out var powerEvent);
                cardObjects[modifiedTargetObjectId] = nextTargetState;
                events.Add(powerEvent);
            }
        }
        else if (behavior.SwapsTargetPowersUntilEndOfTurn
            && stackItem.TargetObjectIds.Count >= 2)
        {
            var firstTargetObjectId = stackItem.TargetObjectIds[0];
            var secondTargetObjectId = stackItem.TargetObjectIds[1];
            if (cardObjects.TryGetValue(firstTargetObjectId, out var firstTargetState)
                && cardObjects.TryGetValue(secondTargetObjectId, out var secondTargetState))
            {
                var firstPowerDelta = secondTargetState.Power - firstTargetState.Power;
                var secondPowerDelta = firstTargetState.Power - secondTargetState.Power;

                if (firstPowerDelta != 0)
                {
                    var nextFirstTargetState = ApplyPowerModifier(
                        firstTargetState,
                        behavior,
                        stackItem,
                        firstTargetObjectId,
                        firstPowerDelta,
                        out var firstPowerEvent);
                    cardObjects[firstTargetObjectId] = nextFirstTargetState;
                    events.Add(firstPowerEvent);
                }

                if (secondPowerDelta != 0)
                {
                    var nextSecondTargetState = ApplyPowerModifier(
                        secondTargetState,
                        behavior,
                        stackItem,
                        secondTargetObjectId,
                        secondPowerDelta,
                        out var secondPowerEvent);
                    cardObjects[secondTargetObjectId] = nextSecondTargetState;
                    events.Add(secondPowerEvent);
                }
            }
        }
        else if (behavior.DamagesAllEnemyBattlefieldUnitsByFirstTargetPower
            && stackItem.TargetObjectIds.Count >= 1)
        {
            var firstTargetObjectId = stackItem.TargetObjectIds[0];
            var damageAmount = cardObjects.TryGetValue(firstTargetObjectId, out var firstTargetState)
                ? Math.Max(0, firstTargetState.Power)
                : 0;
            ApplyDamageToEnemyBattlefieldUnits(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                damageAmount,
                events,
                damageTriggeredDestroyTargetObjectIds,
                preventDamageFromThisStackItem,
                PreventSpellAndSkillDamageThisTurnEffectId);

            if (behavior.MovesTargetToBattlefield
                && TryMoveTargetToControllerBattlefield(playerZones, stackItem.ControllerId, firstTargetObjectId))
            {
                events.Add(new GameEvent(
                    "UNIT_MOVED_TO_BATTLEFIELD",
                    $"{behavior.DisplayName}让单位移动到战场",
                    new Dictionary<string, object?>
                    {
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["targetObjectId"] = firstTargetObjectId,
                        ["controllerId"] = stackItem.ControllerId,
                        ["destinationZone"] = "BATTLEFIELD"
                    }));
            }
        }
        else if (behavior.DestroysFirstTargetAndBuffsSecondByDestroyedPower
            && stackItem.TargetObjectIds.Count >= 2)
        {
            var destroyedTargetObjectId = stackItem.TargetObjectIds[0];
            var buffedTargetObjectId = stackItem.TargetObjectIds[1];
            var destroyedPower = cardObjects.TryGetValue(destroyedTargetObjectId, out var destroyedTargetState)
                ? Math.Max(0, destroyedTargetState.Power)
                : 0;

            if (TryDestroyTarget(playerZones, cardObjects, destroyedTargetObjectId, out var removalResult))
            {
                events.Add(BuildFieldRemovalEvent(
                    behavior.DisplayName,
                    stackItem,
                    destroyedTargetObjectId,
                    removalResult));
                if (removalResult.WasDestroyed)
                {
                    destroyedObjectIds.Add(destroyedTargetObjectId);
                    if (removalResult.WasUnit)
                    {
                        destroyedUnitOwnerIds.Add(removalResult.OwnerPlayerId);
                    }
                }
            }

            if (destroyedPower > 0
                && cardObjects.TryGetValue(buffedTargetObjectId, out var buffedTargetState))
            {
                var modifiedTargetState = ApplyPowerModifier(
                    buffedTargetState,
                    behavior,
                    stackItem,
                    buffedTargetObjectId,
                    destroyedPower,
                    out var powerEvent);
                cardObjects[buffedTargetObjectId] = modifiedTargetState;
                events.Add(powerEvent);
            }
        }
        else if (behavior.MovesFirstTargetToSecondTargetLocation
            && stackItem.TargetObjectIds.Count >= 2
            && TryMoveFirstTargetToSecondTargetLocation(
                playerZones,
                stackItem.TargetObjectIds[0],
                stackItem.TargetObjectIds[1],
                out var destinationPlayerId,
                out var destinationZone))
        {
            events.Add(new GameEvent(
                "UNIT_MOVED_TO_UNIT_LOCATION",
                $"{behavior.DisplayName}让单位移动到另一名单位所在位置",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["targetObjectId"] = stackItem.TargetObjectIds[0],
                    ["destinationTargetObjectId"] = stackItem.TargetObjectIds[1],
                    ["destinationPlayerId"] = destinationPlayerId,
                    ["destinationZone"] = destinationZone
                }));
        }
        else if (behavior.SwapsTargetLocations
            && stackItem.TargetObjectIds.Count >= 2
            && TrySwapTargetLocations(
                playerZones,
                stackItem.TargetObjectIds[0],
                stackItem.TargetObjectIds[1],
                out var firstDestinationPlayerId,
                out var firstDestinationZone,
                out var secondDestinationPlayerId,
                out var secondDestinationZone))
        {
            events.Add(new GameEvent(
                "UNIT_LOCATIONS_SWAPPED",
                $"{behavior.DisplayName}交换两名单位位置",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["firstTargetObjectId"] = stackItem.TargetObjectIds[0],
                    ["secondTargetObjectId"] = stackItem.TargetObjectIds[1],
                    ["firstDestinationPlayerId"] = firstDestinationPlayerId,
                    ["firstDestinationZone"] = firstDestinationZone,
                    ["secondDestinationPlayerId"] = secondDestinationPlayerId,
                    ["secondDestinationZone"] = secondDestinationZone
                }));
        }
        else if (behavior.RequiredTargetCount > 0
            || behavior.UsesFriendlyBattlefieldUnitCountAsMaxTargetCount)
        {
            for (var repeatIndex = 0; repeatIndex < stackItem.EffectRepeatCount; repeatIndex++)
            {
                for (var targetIndex = 0; targetIndex < stackItem.TargetObjectIds.Count; targetIndex++)
                {
                    var targetObjectId = stackItem.TargetObjectIds[targetIndex];
                    if (!IsFieldObject(playerZones, targetObjectId))
                    {
                        continue;
                    }

                    var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
                        ? existingTarget
                        : new CardObjectState(targetObjectId);

                    var damageAmount = ShouldApplyDamageToTarget(behavior, targetIndex)
                        ? ResolveDamageAmount(state, stackItem, behavior, targetObjectId)
                        : 0;
                    if (behavior.BanishesIfDestroyedThisTurn)
                    {
                        targetState = targetState with
                        {
                            UntilEndOfTurnEffects = targetState.UntilEndOfTurnEffects
                                .Concat([BanishIfDestroyedThisTurnEffectId])
                                .Distinct(StringComparer.Ordinal)
                                .OrderBy(effectId => effectId, StringComparer.Ordinal)
                                .ToArray()
                        };
                        events.Add(new GameEvent(
                            "DESTROY_REPLACEMENT_EFFECT_APPLIED",
                            $"{behavior.DisplayName}设置本回合摧毁改为放逐",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["effectId"] = BanishIfDestroyedThisTurnEffectId
                            }));
                        cardObjects[targetObjectId] = targetState;
                    }

                    if (damageAmount > 0)
                    {
                        var damageApplication = ApplyDamageToCardObject(
                            cardObjects,
                            targetObjectId,
                            damageAmount,
                            damageTriggeredDestroyTargetObjectIds,
                            preventDamageFromThisStackItem,
                            PreventSpellAndSkillDamageThisTurnEffectId);
                        targetState = cardObjects[targetObjectId];
                        events.Add(new GameEvent(
                            "DAMAGE_APPLIED",
                            $"{behavior.DisplayName}造成 {damageApplication.DamageAmount} 点伤害",
                            BuildDamagePayload(stackItem.SourceObjectId, targetObjectId, damageApplication)));
                    }

                    var statusEffectIds = ShouldApplyStatusEffectsToTarget(behavior, targetIndex)
                        ? ResolveStatusEffectIds(behavior)
                        : Array.Empty<string>();
                    if (statusEffectIds.Length > 0)
                    {
                        var primaryStatusEffectId = statusEffectIds[0];
                        if (behavior.ReturnsTargetToHandIfAlreadyHasStatusEffect
                            && targetState.UntilEndOfTurnEffects.Contains(primaryStatusEffectId, StringComparer.Ordinal)
                            && TryReturnTargetToHand(playerZones, cardObjects, targetObjectId, out var statusReturnOwnerPlayerId, out _))
                        {
                            events.Add(new GameEvent(
                                "UNIT_RETURNED_TO_HAND",
                                $"{behavior.DisplayName}让已受状态影响的单位返回手牌",
                                new Dictionary<string, object?>
                                {
                                    ["sourceObjectId"] = stackItem.SourceObjectId,
                                    ["targetObjectId"] = targetObjectId,
                                    ["ownerPlayerId"] = statusReturnOwnerPlayerId,
                                    ["statusEffectId"] = primaryStatusEffectId
                                }));
                            continue;
                        }

                        if (behavior.DestroysTargetIfAlreadyHasStatusEffect
                            && targetState.UntilEndOfTurnEffects.Contains(primaryStatusEffectId, StringComparer.Ordinal)
                            && TryDestroyTarget(playerZones, cardObjects, targetObjectId, out var statusRemovalResult))
                        {
                            events.Add(BuildFieldRemovalEvent(
                                behavior.DisplayName,
                                stackItem,
                                targetObjectId,
                                statusRemovalResult,
                                "ALREADY_HAS_STATUS_EFFECT"));
                            if (statusRemovalResult.WasDestroyed)
                            {
                                destroyedObjectIds.Add(targetObjectId);
                                targetControllerDrawRecipientIds.Add(statusRemovalResult.OwnerPlayerId);
                                if (statusRemovalResult.WasUnit)
                                {
                                    destroyedUnitOwnerIds.Add(statusRemovalResult.OwnerPlayerId);
                                }
                            }

                            continue;
                        }

                        foreach (var statusEffectId in statusEffectIds)
                        {
                            targetState = targetState with
                            {
                                UntilEndOfTurnEffects = targetState.UntilEndOfTurnEffects
                                    .Concat([statusEffectId])
                                    .Distinct(StringComparer.Ordinal)
                                    .OrderBy(effectId => effectId, StringComparer.Ordinal)
                                    .ToArray()
                            };
                            events.Add(new GameEvent(
                                "STATUS_EFFECT_APPLIED",
                                $"{behavior.DisplayName}施加{statusEffectId}",
                                new Dictionary<string, object?>
                                {
                                    ["sourceObjectId"] = stackItem.SourceObjectId,
                                    ["targetObjectId"] = targetObjectId,
                                    ["effectId"] = statusEffectId
                                }));
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(behavior.TargetAddedTag))
                    {
                        targetState = ApplyTargetTag(
                            targetState,
                            behavior,
                            stackItem,
                            targetObjectId,
                            out var tagEvent);
                        events.Add(tagEvent);
                    }

                    if (behavior.GrantsBoon)
                    {
                        targetState = ApplyBoon(
                            targetState,
                            behavior,
                            stackItem,
                            targetObjectId,
                            out var boonEvents);
                        events.AddRange(boonEvents);
                    }

                    var powerModifierAmount = ShouldApplyPowerModifierToTarget(behavior, targetIndex)
                        ? ResolvePowerModifierAmount(
                            behavior,
                            targetIndex,
                            playerZones,
                            cardObjects,
                            stackItem.ControllerId,
                            targetObjectId)
                        : 0;
                    if (powerModifierAmount != 0
                        && PowerModifierConditionApplies(targetState, behavior))
                    {
                        targetState = ApplyPowerModifier(
                            targetState,
                            behavior,
                            stackItem,
                            targetObjectId,
                            powerModifierAmount,
                            out var powerEvent);
                        events.Add(powerEvent);
                    }

                    if (behavior.ReadiesTarget)
                    {
                        targetState = ApplyReadyState(
                            targetState,
                            behavior,
                            stackItem,
                            targetObjectId,
                            out var readyEvent);
                        events.Add(readyEvent);
                    }

                    cardObjects[targetObjectId] = targetState;

                    if (behavior.DestroysTarget
                        && TryDestroyTarget(playerZones, cardObjects, targetObjectId, out var removalResult))
                    {
                        events.Add(BuildFieldRemovalEvent(
                            behavior.DisplayName,
                            stackItem,
                            targetObjectId,
                            removalResult));
                        if (removalResult.WasDestroyed)
                        {
                            destroyedObjectIds.Add(targetObjectId);
                            targetControllerDrawRecipientIds.Add(removalResult.OwnerPlayerId);
                            if (removalResult.WasUnit)
                            {
                                destroyedUnitOwnerIds.Add(removalResult.OwnerPlayerId);
                            }

                            var conditionalEquipmentTokenCount = ConditionalEquipmentTokenCountForDestroyedUnit(
                                behavior,
                                stackItem.ControllerId,
                                removalResult);
                            if (conditionalEquipmentTokenCount > 0)
                            {
                                CreateBaseEquipmentTokens(
                                    playerZones,
                                    cardObjects,
                                    behavior,
                                    stackItem,
                                    events,
                                    conditionalEquipmentTokenCount);
                            }
                        }

                        continue;
                    }

                    if (behavior.ReturnsTargetToHand
                        && TryReturnTargetToHand(playerZones, cardObjects, targetObjectId, out var returnedOwnerPlayerId, out var returnedWasEquipment))
                    {
                        events.Add(BuildReturnedToHandEvent(
                            behavior.DisplayName,
                            stackItem,
                            targetObjectId,
                            returnedOwnerPlayerId,
                            returnedWasEquipment));
                        if (behavior.RuneCallCountAfterTargetReturn > 0)
                        {
                            var runeCallResult = CallRunes(
                                playerZones,
                                cardObjects,
                                returnedOwnerPlayerId,
                                behavior.RuneCallCountAfterTargetReturn);
                            events.Add(new GameEvent(
                                "RUNES_CALLED",
                                $"{returnedOwnerPlayerId} 召出 {runeCallResult.CalledRuneObjectIds.Count} 张符文",
                                new Dictionary<string, object?>
                                {
                                    ["playerId"] = returnedOwnerPlayerId,
                                    ["sourceObjectId"] = stackItem.SourceObjectId,
                                    ["count"] = runeCallResult.CalledRuneObjectIds.Count,
                                    ["runeObjectIds"] = runeCallResult.CalledRuneObjectIds.ToArray()
                                }));
                        }

                        continue;
                    }

                    if (ShouldApplyBanishPlayToTarget(behavior, targetIndex)
                        && (behavior.BanishesTargetThenPlaysToBase || behavior.BanishesTargetThenPlaysToBattlefield)
                        && TryBanishTargetThenPlayToOwnerField(
                            playerZones,
                            cardObjects,
                            targetObjectId,
                            behavior.BanishesTargetThenPlaysToBattlefield ? "BATTLEFIELD" : "BASE",
                            out var rescuedOwnerPlayerId))
                    {
                        var playedDestinationZone = behavior.BanishesTargetThenPlaysToBattlefield ? "BATTLEFIELD" : "BASE";
                        events.Add(new GameEvent(
                            "UNIT_BANISHED",
                            $"{behavior.DisplayName}放逐单位",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["ownerPlayerId"] = rescuedOwnerPlayerId,
                                ["destinationZone"] = "BANISHED"
                            }));
                        events.Add(new GameEvent(
                            behavior.BanishesTargetThenPlaysToBattlefield ? "UNIT_PLAYED_TO_BATTLEFIELD" : "UNIT_PLAYED_TO_BASE",
                            behavior.BanishesTargetThenPlaysToBattlefield
                                ? $"{behavior.DisplayName}将单位打出到战场"
                                : $"{behavior.DisplayName}将单位打出到基地",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["ownerPlayerId"] = rescuedOwnerPlayerId,
                                ["destinationZone"] = playedDestinationZone
                            }));
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(behavior.TargetOwnerMainDeckDestination)
                        && TryMoveTargetToOwnerMainDeck(
                            playerZones,
                            cardObjects,
                            targetObjectId,
                            behavior.TargetOwnerMainDeckDestination,
                            out var deckOwnerPlayerId,
                            out var deckPosition))
                    {
                        events.Add(new GameEvent(
                            "UNIT_RETURNED_TO_DECK",
                            $"{behavior.DisplayName}让单位放到所属者主牌堆",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["ownerPlayerId"] = deckOwnerPlayerId,
                                ["destinationZone"] = "MAIN_DECK",
                                ["deckPosition"] = deckPosition
                            }));
                        continue;
                    }

                    if (behavior.GainsControlOfTargetToBase
                        && TryGainControlOfTargetToBase(
                            playerZones,
                            cardObjects,
                            stackItem.ControllerId,
                            targetObjectId,
                            behavior.ExhaustsControlledTarget,
                            out var previousControllerId,
                            out var controlledTargetState))
                    {
                        events.Add(new GameEvent(
                            "UNIT_CONTROL_GAINED",
                            $"{behavior.DisplayName}获得单位控制权并召回",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["controllerId"] = stackItem.ControllerId,
                                ["previousControllerId"] = previousControllerId,
                                ["destinationZone"] = "BASE",
                                ["isExhausted"] = controlledTargetState.IsExhausted
                            }));
                        continue;
                    }

                    if (behavior.GainsControlOfTargetToBattlefield
                        && TryGainControlOfTargetToBattlefield(
                            playerZones,
                            cardObjects,
                            stackItem.ControllerId,
                            targetObjectId,
                            out var previousBattlefieldControllerId,
                            out var battleControlledTargetState))
                    {
                        events.Add(new GameEvent(
                            "UNIT_CONTROL_GAINED",
                            $"{behavior.DisplayName}获得战场单位控制权",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["controllerId"] = stackItem.ControllerId,
                                ["previousControllerId"] = previousBattlefieldControllerId,
                                ["destinationZone"] = "BATTLEFIELD",
                                ["isExhausted"] = battleControlledTargetState.IsExhausted
                            }));
                        continue;
                    }

                    if (behavior.MovesTargetToBase
                        && targetIndex >= behavior.MoveToBaseTargetStartIndex
                        && TryMoveTargetToOwnerBase(playerZones, targetObjectId, out var movedOwnerPlayerId))
                    {
                        events.Add(new GameEvent(
                            "UNIT_MOVED_TO_BASE",
                            $"{behavior.DisplayName}让单位移动到所属基地",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["ownerPlayerId"] = movedOwnerPlayerId
                            }));
                        continue;
                    }

                    if (behavior.MovesTargetsToOwnerBattlefields
                        && TryMoveTargetToOwnerBattlefield(playerZones, targetObjectId, out var ownerBattlefieldPlayerId))
                    {
                        events.Add(new GameEvent(
                            "UNIT_MOVED_TO_BATTLEFIELD",
                            $"{behavior.DisplayName}让单位移动到战场",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["ownerPlayerId"] = ownerBattlefieldPlayerId,
                                ["destinationZone"] = "BATTLEFIELD"
                            }));
                        continue;
                    }

                    if (behavior.MovesTargetToBattlefield
                        && TryMoveTargetToControllerBattlefield(playerZones, stackItem.ControllerId, targetObjectId))
                    {
                        events.Add(new GameEvent(
                            "UNIT_MOVED_TO_BATTLEFIELD",
                            $"{behavior.DisplayName}让单位移动到战场",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["controllerId"] = stackItem.ControllerId,
                                ["destinationZone"] = "BATTLEFIELD"
                            }));
                        continue;
                    }

                    cardObjects[targetObjectId] = targetState;
                }

                if (behavior.OtherEnemyBattlefieldUnitsDamageAmount > 0)
                {
                    ApplyDamageToOtherEnemyBattlefieldUnits(
                        playerZones,
                        cardObjects,
                        behavior,
                        stackItem,
                        behavior.OtherEnemyBattlefieldUnitsDamageAmount,
                        stackItem.TargetObjectIds,
                        events,
                        damageTriggeredDestroyTargetObjectIds,
                        preventDamageFromThisStackItem,
                        PreventSpellAndSkillDamageThisTurnEffectId);
                }
            }
        }

        if (behavior.CreatedBaseUnitTokenCount > 0
            && behavior.CreatedBaseUnitTokenCopiesFirstTarget)
        {
            CreateBaseUnitTokens(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                events);
        }

        if (behavior.CreatedBaseEquipmentTokenCount > 0)
        {
            CreateBaseEquipmentTokens(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                events);
        }

        if (behavior.RecyclesSelectedMainDeckTargets)
        {
            var recycleResult = RecycleSelectedMainDeckTargets(
                state,
                playerZones,
                stackItem.ControllerId,
                stackItem.SourceObjectId,
                MainDeckTargetObjectIds(stackItem.TargetObjectIds, behavior),
                behavior.MainDeckLookCount);
            events.AddRange(recycleResult.Events);
            rngCursor = recycleResult.RngCursor;
        }

        var lethalCleanup = ApplyLethalDamageCleanup(
            playerZones,
            cardObjects,
            stackItem,
            damageTriggeredDestroyTargetObjectIds);
        events.AddRange(lethalCleanup.Events);
        destroyedObjectIds.AddRange(lethalCleanup.DestroyedObjectIds
            .Where(objectId => stackItem.TargetObjectIds.Contains(objectId, StringComparer.Ordinal)));
        destroyedUnitOwnerIds.AddRange(lethalCleanup.DestroyedUnitOwnerIds);

        var drawCount = drawCountOverride ?? ResolveDrawCount(playerZones, cardObjects, stackItem.ControllerId, behavior);
        if (ShouldDrawForBehavior(behavior, stackItem, destroyedObjectIds, drawCount))
        {
            foreach (var drawPlayerId in DrawRecipientPlayerIds(behavior, stackItem.ControllerId, targetControllerDrawRecipientIds))
            {
                var drawApplication = ApplyDrawToPlayer(
                    state,
                    playerZones,
                    playerScores,
                    drawPlayerId,
                    drawCount * stackItem.EffectRepeatCount,
                    rngCursor,
                    events);
                playerScores = drawApplication.PlayerScores;
                winnerPlayerId = drawApplication.WinnerPlayerId;
                rngCursor = drawApplication.RngCursor;

                if (winnerPlayerId is not null)
                {
                    break;
                }
            }
        }

        if (behavior.SchedulesExtraTurnForController)
        {
            extraTurnPlayerId = stackItem.ControllerId;
            events.Add(new GameEvent(
                "EXTRA_TURN_SCHEDULED",
                $"{behavior.DisplayName}安排额外回合",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["playerId"] = stackItem.ControllerId
                }));
        }

        if (!behavior.PlaysSourceToBaseAsEquipment
            && !behavior.PlaysSourceToBaseAsUnit
            && playerZones.TryGetValue(stackItem.ControllerId, out var controllerZones))
        {
            if (behavior.BanishesSourceOnResolution)
            {
                if (!controllerZones.Banished.Contains(stackItem.SourceObjectId, StringComparer.Ordinal))
                {
                    controllerZones = controllerZones with
                    {
                        Banished = controllerZones.Banished.Concat([stackItem.SourceObjectId]).ToArray()
                    };
                }
            }
            else if (!controllerZones.Graveyard.Contains(stackItem.SourceObjectId, StringComparer.Ordinal))
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
            playerExperience,
            untilEndOfTurnEffects.ToArray(),
            winnerPlayerId,
            events,
            destroyedUnitOwnerIds
                .Distinct(StringComparer.Ordinal)
                .OrderBy(ownerId => ownerId, StringComparer.Ordinal)
                .ToArray(),
            updatedStackItems,
            counteredStackItemIds
                .Distinct(StringComparer.Ordinal)
                .OrderBy(stackItemId => stackItemId, StringComparer.Ordinal)
                .ToArray(),
            extraTurnPlayerId,
            rngCursor);
    }

    private static StackItemState[] RemoveCounteredStackItems(
        IReadOnlyList<StackItemState> stackItems,
        IReadOnlyList<string> counteredStackItemIds)
    {
        if (counteredStackItemIds.Count == 0)
        {
            return stackItems.ToArray();
        }

        return stackItems
            .Where(stackItem => !counteredStackItemIds.Contains(stackItem.StackItemId, StringComparer.Ordinal))
            .ToArray();
    }

    private static bool TryCounterStackItem(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        string targetStackItemId,
        StackItemState counteringStackItem,
        CardBehaviorDefinition counteringBehavior,
        out GameEvent counteredEvent)
    {
        counteredEvent = default!;
        var targetStackItem = state.StackItems.FirstOrDefault(stackItem =>
            string.Equals(stackItem.StackItemId, targetStackItemId, StringComparison.Ordinal));
        if (targetStackItem is null
            || !CardBehaviorRegistry.TryGetByEffectKind(targetStackItem.EffectKind, out var targetBehavior)
            || targetBehavior.PlaysSourceToBaseAsUnit
            || targetBehavior.PlaysSourceToBaseAsEquipment
            || !playerZones.TryGetValue(targetStackItem.ControllerId, out var targetControllerZones))
        {
            return false;
        }

        var destinationZone = string.Equals(
            counteringBehavior.CounteredStackItemDestination,
            CardCounteredStackItemDestinationZones.Hand,
            StringComparison.Ordinal)
            ? CardCounteredStackItemDestinationZones.Hand
            : CardCounteredStackItemDestinationZones.Graveyard;

        if (string.Equals(destinationZone, CardCounteredStackItemDestinationZones.Hand, StringComparison.Ordinal))
        {
            if (!targetControllerZones.Hand.Contains(targetStackItem.SourceObjectId, StringComparer.Ordinal))
            {
                playerZones[targetStackItem.ControllerId] = targetControllerZones with
                {
                    Hand = targetControllerZones.Hand.Concat([targetStackItem.SourceObjectId]).ToArray()
                };
            }
        }
        else if (!targetControllerZones.Graveyard.Contains(targetStackItem.SourceObjectId, StringComparer.Ordinal))
        {
            playerZones[targetStackItem.ControllerId] = targetControllerZones with
            {
                Graveyard = targetControllerZones.Graveyard.Concat([targetStackItem.SourceObjectId]).ToArray()
            };
        }

        counteredEvent = new GameEvent(
            "STACK_ITEM_COUNTERED",
            $"{counteringStackItem.SourceObjectId}无效化法术",
            new Dictionary<string, object?>
            {
                ["stackItemId"] = targetStackItem.StackItemId,
                ["counteredByStackItemId"] = counteringStackItem.StackItemId,
                ["sourceObjectId"] = targetStackItem.SourceObjectId,
                ["controllerId"] = targetStackItem.ControllerId,
                ["counteredByPlayerId"] = counteringStackItem.ControllerId,
                ["destinationZone"] = destinationZone
            });
        return true;
    }

    private static bool TryGainControlOfTargetStackSpell(
        IReadOnlyList<StackItemState> stackItems,
        CardBehaviorDefinition behavior,
        StackItemState gainingStackItem,
        out StackItemState[] updatedStackItems,
        out GameEvent controlEvent)
    {
        updatedStackItems = [];
        controlEvent = default!;
        if (gainingStackItem.TargetObjectIds.Count == 0)
        {
            return false;
        }

        var targetStackItemId = gainingStackItem.TargetObjectIds[0];
        var targetStackItem = stackItems.FirstOrDefault(stackItem =>
            string.Equals(stackItem.StackItemId, targetStackItemId, StringComparison.Ordinal));
        if (targetStackItem is null)
        {
            return false;
        }

        updatedStackItems = stackItems
            .Where(stackItem => !string.Equals(stackItem.StackItemId, gainingStackItem.StackItemId, StringComparison.Ordinal))
            .Select(stackItem => string.Equals(stackItem.StackItemId, targetStackItemId, StringComparison.Ordinal)
                ? stackItem with { ControllerId = gainingStackItem.ControllerId }
                : stackItem)
            .ToArray();

        controlEvent = new GameEvent(
            "STACK_ITEM_CONTROL_GAINED",
            $"{behavior.DisplayName}获得法术控制权",
            new Dictionary<string, object?>
            {
                ["stackItemId"] = targetStackItem.StackItemId,
                ["sourceObjectId"] = gainingStackItem.SourceObjectId,
                ["targetSourceObjectId"] = targetStackItem.SourceObjectId,
                ["previousControllerId"] = targetStackItem.ControllerId,
                ["controllerId"] = gainingStackItem.ControllerId
            });
        return true;
    }

    private static IEnumerable<string> SacredJudgmentFieldUnitIds(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        PlayerZones zones)
    {
        return zones.Base
            .Concat(zones.Battlefields)
            .Where(objectId => CardObjectHasTag(cardObjects, objectId, CardObjectTags.UnitCard));
    }

    private static IEnumerable<string> SacredJudgmentEquipmentIds(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        PlayerZones zones)
    {
        return zones.Base
            .Concat(zones.Battlefields)
            .Where(objectId => CardObjectHasTag(cardObjects, objectId, CardObjectTags.EquipmentCard));
    }

    private static IEnumerable<string> SacredJudgmentRuneIds(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        PlayerZones zones)
    {
        return zones.Base
            .Where(objectId => CardObjectHasTag(cardObjects, objectId, CardObjectTags.RuneCard));
    }

    private static void ApplyStatusEffectsToFieldUnits(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IDictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        ICollection<GameEvent> events)
    {
        var statusEffectIds = ResolveStatusEffectIds(behavior);
        if (statusEffectIds.Length == 0)
        {
            return;
        }

        foreach (var targetObjectId in GetFieldUnitObjectIds(playerZones))
        {
            var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
                ? existingTarget
                : new CardObjectState(targetObjectId);

            foreach (var statusEffectId in statusEffectIds)
            {
                targetState = targetState with
                {
                    UntilEndOfTurnEffects = targetState.UntilEndOfTurnEffects
                        .Concat([statusEffectId])
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(effectId => effectId, StringComparer.Ordinal)
                        .ToArray()
                };
                events.Add(new GameEvent(
                    "STATUS_EFFECT_APPLIED",
                    $"{behavior.DisplayName}施加{statusEffectId}",
                    new Dictionary<string, object?>
                    {
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["targetObjectId"] = targetObjectId,
                        ["effectId"] = statusEffectId
                    }));
            }

            cardObjects[targetObjectId] = targetState;
        }
    }

    private static IReadOnlyList<string> DrawRecipientPlayerIds(
        CardBehaviorDefinition behavior,
        string controllerId,
        IReadOnlyList<string> targetControllerPlayerIds)
    {
        return behavior.DrawRecipientKind switch
        {
            CardDrawRecipientKinds.TargetController => targetControllerPlayerIds
                .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray(),
            _ => [controllerId]
        };
    }

    private static int ConditionalEquipmentTokenCountForDestroyedUnit(
        CardBehaviorDefinition behavior,
        string controllerId,
        FieldRemovalResult removalResult)
    {
        if (!removalResult.WasDestroyed || !removalResult.WasUnit)
        {
            return 0;
        }

        return string.Equals(removalResult.OwnerPlayerId, controllerId, StringComparison.Ordinal)
            ? behavior.CreatedBaseEquipmentTokenCountIfDestroyedFriendlyUnit
            : behavior.CreatedBaseEquipmentTokenCountIfDestroyedEnemyUnit;
    }

    private static IReadOnlyList<string> GetBattlefieldObjectIds(
        IReadOnlyDictionary<string, PlayerZones> playerZones)
    {
        return playerZones
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Battlefields)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> GetEnemyCombatObjectIds(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string controllerId)
    {
        return playerZones
            .Where(entry => !string.Equals(entry.Key, controllerId, StringComparison.Ordinal))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Battlefields)
            .Where(objectId => cardObjects.TryGetValue(objectId, out var objectState)
                && (objectState.IsAttacking || objectState.IsDefending))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> GetEnemyBattlefieldObjectIds(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string controllerId)
    {
        return playerZones
            .Where(entry => !string.Equals(entry.Key, controllerId, StringComparison.Ordinal))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Battlefields)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> GetFieldUnitObjectIds(
        IReadOnlyDictionary<string, PlayerZones> playerZones)
    {
        return playerZones
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Base.Concat(entry.Value.Battlefields))
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> GetFieldObjectIds(
        IReadOnlyDictionary<string, PlayerZones> playerZones)
    {
        return playerZones
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Base.Concat(entry.Value.Battlefields))
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> GetFieldEquipmentObjectIds(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects)
    {
        return playerZones
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Base.Concat(entry.Value.Battlefields))
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Where(objectId => CardObjectHasTag(cardObjects, objectId, CardObjectTags.EquipmentCard))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> GetControlledFieldUnitObjectIds(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId)
    {
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.Base
            .Concat(zones.Battlefields)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Where(objectId => !CardObjectHasTag(cardObjects, objectId, CardObjectTags.EquipmentCard))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static int CountControlledFieldUnitObjects(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId)
    {
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return 0;
        }

        return zones.Base
            .Concat(zones.Battlefields)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal)
            .Count(objectId => CardObjectHasTag(cardObjects, objectId, CardObjectTags.UnitCard));
    }

    private static IReadOnlyList<string> GetEnemyFieldUnitObjectIds(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId)
    {
        return playerZones
            .Where(entry => !string.Equals(entry.Key, playerId, StringComparison.Ordinal))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Base.Concat(entry.Value.Battlefields))
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Where(objectId => !CardObjectHasTag(cardObjects, objectId, CardObjectTags.EquipmentCard))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> GetControlledBattlefieldUnitObjectIds(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string playerId)
    {
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.Battlefields
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsFieldObject(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && playerZones.Values.Any(zones =>
                zones.Base.Contains(objectId, StringComparer.Ordinal)
                || zones.Battlefields.Contains(objectId, StringComparer.Ordinal));
    }

    private static bool TryGetFieldControllerId(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId,
        out string controllerId)
    {
        foreach (var (playerId, zones) in playerZones)
        {
            if (zones.Base.Contains(objectId, StringComparer.Ordinal)
                || zones.Battlefields.Contains(objectId, StringComparer.Ordinal))
            {
                controllerId = playerId;
                return true;
            }
        }

        controllerId = string.Empty;
        return false;
    }

    private static bool TryAttachOrDetachSecondTargetEquipmentToFirstTarget(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        out GameEvent attachmentEvent)
    {
        attachmentEvent = default!;
        if (stackItem.TargetObjectIds.Count < 2)
        {
            return false;
        }

        var unitObjectId = stackItem.TargetObjectIds[0];
        var equipmentObjectId = stackItem.TargetObjectIds[1];
        if (!IsFieldObject(playerZones, unitObjectId)
            || !IsFieldObject(playerZones, equipmentObjectId)
            || !cardObjects.TryGetValue(equipmentObjectId, out var equipmentState)
            || !equipmentState.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
            || !equipmentState.Tags.Contains("武装", StringComparer.Ordinal)
            || !TryGetFieldControllerId(playerZones, unitObjectId, out var unitControllerId)
            || !TryGetFieldControllerId(playerZones, equipmentObjectId, out var equipmentControllerId)
            || !string.Equals(unitControllerId, equipmentControllerId, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(equipmentState.AttachedToObjectId, unitObjectId, StringComparison.Ordinal))
        {
            cardObjects[equipmentObjectId] = equipmentState with
            {
                AttachedToObjectId = null
            };
            attachmentEvent = new GameEvent(
                "EQUIPMENT_DETACHED",
                $"{behavior.DisplayName}卸除武装",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["unitObjectId"] = unitObjectId,
                    ["equipmentObjectId"] = equipmentObjectId,
                    ["controllerId"] = unitControllerId
                });
            return true;
        }

        if (!string.IsNullOrWhiteSpace(equipmentState.AttachedToObjectId))
        {
            return false;
        }

        cardObjects[equipmentObjectId] = equipmentState with
        {
            AttachedToObjectId = unitObjectId
        };
        attachmentEvent = new GameEvent(
            "EQUIPMENT_ATTACHED",
            $"{behavior.DisplayName}贴附武装",
            new Dictionary<string, object?>
            {
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["unitObjectId"] = unitObjectId,
                ["equipmentObjectId"] = equipmentObjectId,
                ["controllerId"] = unitControllerId,
                ["attachedToObjectId"] = unitObjectId
            });
        return true;
    }

    private static void ApplyDamageToBattlefieldUnits(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        int damageAmount,
        List<GameEvent> events,
        ISet<string> damageTriggeredDestroyTargetObjectIds,
        bool preventDamage = false,
        string preventionEffectId = "")
    {
        if (damageAmount <= 0)
        {
            return;
        }

        foreach (var targetObjectId in GetBattlefieldObjectIds(playerZones))
        {
            var damageApplication = ApplyDamageToCardObject(
                cardObjects,
                targetObjectId,
                damageAmount,
                damageTriggeredDestroyTargetObjectIds,
                preventDamage,
                preventionEffectId);
            events.Add(new GameEvent(
                "DAMAGE_APPLIED",
                $"{behavior.DisplayName}造成 {damageApplication.DamageAmount} 点伤害",
                BuildDamagePayload(stackItem.SourceObjectId, targetObjectId, damageApplication)));
        }
    }

    private static void ApplyBattlefieldPowerModifiers(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        List<GameEvent> events)
    {
        for (var repeatIndex = 0; repeatIndex < stackItem.EffectRepeatCount; repeatIndex++)
        {
            if (behavior.ModifiesAllFriendlyBattlefieldUnits
                && behavior.PowerModifierAmount != 0)
            {
                foreach (var targetObjectId in GetControlledBattlefieldUnitObjectIds(playerZones, stackItem.ControllerId))
                {
                    if (!cardObjects.TryGetValue(targetObjectId, out var targetState))
                    {
                        continue;
                    }

                    cardObjects[targetObjectId] = ApplyPowerModifier(
                        targetState,
                        behavior,
                        stackItem,
                        targetObjectId,
                        behavior.PowerModifierAmount,
                        out var powerEvent);
                    events.Add(powerEvent);
                }
            }

            var enemyPowerModifierAmount = behavior.SecondaryPowerModifierAmount != 0
                ? behavior.SecondaryPowerModifierAmount
                : behavior.PowerModifierAmount;
            if (behavior.ModifiesAllEnemyBattlefieldUnits
                && enemyPowerModifierAmount != 0)
            {
                foreach (var targetObjectId in GetEnemyBattlefieldObjectIds(playerZones, stackItem.ControllerId))
                {
                    if (!cardObjects.TryGetValue(targetObjectId, out var targetState))
                    {
                        continue;
                    }

                    cardObjects[targetObjectId] = ApplyPowerModifier(
                        targetState,
                        behavior,
                        stackItem,
                        targetObjectId,
                        enemyPowerModifierAmount,
                        out var powerEvent);
                    events.Add(powerEvent);
                }
            }
        }
    }

    private static void ApplyDamageToEnemyCombatUnits(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        int damageAmount,
        List<GameEvent> events,
        ISet<string> damageTriggeredDestroyTargetObjectIds,
        bool preventDamage = false,
        string preventionEffectId = "")
    {
        if (damageAmount <= 0)
        {
            return;
        }

        foreach (var targetObjectId in GetEnemyCombatObjectIds(playerZones, cardObjects, stackItem.ControllerId))
        {
            var damageApplication = ApplyDamageToCardObject(
                cardObjects,
                targetObjectId,
                damageAmount,
                damageTriggeredDestroyTargetObjectIds,
                preventDamage,
                preventionEffectId);
            events.Add(new GameEvent(
                "DAMAGE_APPLIED",
                $"{behavior.DisplayName}造成 {damageApplication.DamageAmount} 点伤害",
                BuildDamagePayload(stackItem.SourceObjectId, targetObjectId, damageApplication)));
        }
    }

    private static void ApplyDamageToEnemyBattlefieldUnits(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        int damageAmount,
        List<GameEvent> events,
        ISet<string> damageTriggeredDestroyTargetObjectIds,
        bool preventDamage = false,
        string preventionEffectId = "")
    {
        if (damageAmount <= 0)
        {
            return;
        }

        foreach (var targetObjectId in GetEnemyBattlefieldObjectIds(playerZones, stackItem.ControllerId))
        {
            var damageApplication = ApplyDamageToCardObject(
                cardObjects,
                targetObjectId,
                damageAmount,
                damageTriggeredDestroyTargetObjectIds,
                preventDamage,
                preventionEffectId);
            events.Add(new GameEvent(
                "DAMAGE_APPLIED",
                $"{behavior.DisplayName}造成 {damageApplication.DamageAmount} 点伤害",
                BuildDamagePayload(stackItem.SourceObjectId, targetObjectId, damageApplication)));
        }
    }

    private static void ApplyDamageToOtherEnemyBattlefieldUnits(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        int damageAmount,
        IReadOnlyCollection<string> excludedObjectIds,
        List<GameEvent> events,
        ISet<string> damageTriggeredDestroyTargetObjectIds,
        bool preventDamage = false,
        string preventionEffectId = "")
    {
        if (damageAmount <= 0)
        {
            return;
        }

        foreach (var targetObjectId in GetEnemyBattlefieldObjectIds(playerZones, stackItem.ControllerId))
        {
            if (excludedObjectIds.Contains(targetObjectId, StringComparer.Ordinal))
            {
                continue;
            }

            var damageApplication = ApplyDamageToCardObject(
                cardObjects,
                targetObjectId,
                damageAmount,
                damageTriggeredDestroyTargetObjectIds,
                preventDamage,
                preventionEffectId);
            events.Add(new GameEvent(
                "DAMAGE_APPLIED",
                $"{behavior.DisplayName}造成 {damageApplication.DamageAmount} 点溅射伤害",
                BuildDamagePayload(stackItem.SourceObjectId, targetObjectId, damageApplication)));
        }
    }

    private static DamageApplicationResult ApplyDamageToCardObject(
        Dictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        int damageAmount,
        ISet<string>? damageTriggeredDestroyTargetObjectIds = null,
        bool preventDamage = false,
        string preventionEffectId = "")
    {
        if (damageAmount <= 0)
        {
            return new DamageApplicationResult(0, 0, false, string.Empty);
        }

        var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
            ? existingTarget
            : new CardObjectState(targetObjectId);
        var preventsWithTargetEffect = !preventDamage
            && targetState.UntilEndOfTurnEffects.Contains(
                PreventNextDamageThisTurnEffectId,
                StringComparer.Ordinal);
        var preventsDamage = preventDamage || preventsWithTargetEffect;
        var adjustedDamageAmount = preventsDamage
            ? 0
            : targetState.UntilEndOfTurnEffects.Contains(DamageReceivedDoubledThisTurnEffectId, StringComparer.Ordinal)
                ? damageAmount * 2
                : damageAmount;
        var triggersDestroyOnDamage = adjustedDamageAmount > 0
            && targetState.UntilEndOfTurnEffects.Contains(
                DestroyOnNextDamageThisTurnEffectId,
                StringComparer.Ordinal);
        var nextEffects = targetState.UntilEndOfTurnEffects
            .Where(effectId => !preventsWithTargetEffect
                || !string.Equals(effectId, PreventNextDamageThisTurnEffectId, StringComparison.Ordinal))
            .Where(effectId => !triggersDestroyOnDamage
                || !string.Equals(effectId, DestroyOnNextDamageThisTurnEffectId, StringComparison.Ordinal))
            .ToArray();

        cardObjects[targetObjectId] = targetState with
        {
            Damage = targetState.Damage + adjustedDamageAmount,
            UntilEndOfTurnEffects = nextEffects
        };
        if (triggersDestroyOnDamage)
        {
            damageTriggeredDestroyTargetObjectIds?.Add(targetObjectId);
        }

        return new DamageApplicationResult(
            adjustedDamageAmount,
            damageAmount,
            preventsDamage,
            preventsWithTargetEffect
                ? PreventNextDamageThisTurnEffectId
                : preventDamage
                    ? preventionEffectId
                    : string.Empty);
    }

    private static Dictionary<string, object?> BuildDamagePayload(
        string sourceObjectId,
        string targetObjectId,
        DamageApplicationResult damageApplication,
        string damageSourceObjectId = "")
    {
        var payload = new Dictionary<string, object?>
        {
            ["sourceObjectId"] = sourceObjectId,
            ["targetObjectId"] = targetObjectId,
            ["damage"] = damageApplication.DamageAmount
        };

        if (!string.IsNullOrWhiteSpace(damageSourceObjectId))
        {
            payload["damageSourceObjectId"] = damageSourceObjectId;
        }

        if (damageApplication.Prevented)
        {
            payload["preventedDamage"] = damageApplication.OriginalDamageAmount;
            payload["preventionEffectId"] = damageApplication.PreventionEffectId;
        }

        return payload;
    }

    private static void CreateBaseUnitTokens(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        List<GameEvent> events)
    {
        if (!playerZones.TryGetValue(stackItem.ControllerId, out var zones))
        {
            return;
        }

        var copiedTargetObjectId = behavior.CreatedBaseUnitTokenCopiesFirstTarget
            ? stackItem.TargetObjectIds.FirstOrDefault() ?? string.Empty
            : string.Empty;
        CardObjectState? copiedTargetState = null;
        var copiesTarget = !string.IsNullOrWhiteSpace(copiedTargetObjectId)
            && cardObjects.TryGetValue(copiedTargetObjectId, out copiedTargetState);
        if (behavior.CreatedBaseUnitTokenCopiesFirstTarget
            && !copiesTarget)
        {
            return;
        }

        var tokenPower = copiedTargetState is not null
            ? copiedTargetState.Power
            : behavior.CreatedBaseUnitTokenPower;
        if (!behavior.CreatedBaseUnitTokenCopiesFirstTarget
            && tokenPower <= 0)
        {
            return;
        }

        var tokenCount = behavior.CreatedBaseUnitTokenCount * Math.Max(1, stackItem.EffectRepeatCount);
        var tokenTags = copiedTargetState is not null
            ? copiedTargetState.Tags
                .Concat(ParseDelimitedValues(behavior.CreatedBaseUnitTokenTags))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray()
            : ParseDelimitedValues(behavior.CreatedBaseUnitTokenTags);
        var createdTokenObjectIds = new List<string>();
        for (var tokenIndex = 0; tokenIndex < tokenCount; tokenIndex++)
        {
            var tokenObjectId = NextTokenObjectId(
                playerZones,
                cardObjects,
                stackItem.SourceObjectId,
                tokenIndex + 1);
            createdTokenObjectIds.Add(tokenObjectId);
            cardObjects[tokenObjectId] = new CardObjectState(
                tokenObjectId,
                power: tokenPower,
                tags: tokenTags);
            var payload = new Dictionary<string, object?>
            {
                ["playerId"] = stackItem.ControllerId,
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["tokenObjectId"] = tokenObjectId,
                ["tokenName"] = behavior.CreatedBaseUnitTokenName,
                ["power"] = tokenPower,
                ["destinationZone"] = "BASE"
            };
            if (copiesTarget)
            {
                payload["copiedTargetObjectId"] = copiedTargetObjectId;
            }

            if (tokenTags.Count > 0)
            {
                payload["tokenTags"] = tokenTags;
            }

            events.Add(new GameEvent(
                "UNIT_TOKEN_CREATED",
                $"{behavior.DisplayName}打出单位指示物到基地",
                payload));
        }

        playerZones[stackItem.ControllerId] = zones with
        {
            Base = zones.Base.Concat(createdTokenObjectIds).ToArray()
        };
    }

    private static void CreateBaseEquipmentTokens(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        List<GameEvent> events,
        int tokenCountOverride = 0)
    {
        if (!playerZones.TryGetValue(stackItem.ControllerId, out var zones))
        {
            return;
        }

        var baseTokenCount = tokenCountOverride > 0
            ? tokenCountOverride
            : behavior.CreatedBaseEquipmentTokenCount;
        var tokenCount = baseTokenCount * Math.Max(1, stackItem.EffectRepeatCount);
        var tokenTags = ParseDelimitedValues(behavior.CreatedBaseEquipmentTokenTags);
        var createdTokenObjectIds = new List<string>();
        for (var tokenIndex = 0; tokenIndex < tokenCount; tokenIndex++)
        {
            var tokenObjectId = NextTokenObjectId(
                playerZones,
                cardObjects,
                stackItem.SourceObjectId,
                tokenIndex + 1);
            createdTokenObjectIds.Add(tokenObjectId);
            cardObjects[tokenObjectId] = new CardObjectState(
                tokenObjectId,
                isExhausted: behavior.CreatedBaseEquipmentTokenIsExhausted,
                tags: tokenTags);
            var payload = new Dictionary<string, object?>
            {
                ["playerId"] = stackItem.ControllerId,
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["tokenObjectId"] = tokenObjectId,
                ["tokenName"] = behavior.CreatedBaseEquipmentTokenName,
                ["destinationZone"] = "BASE",
                ["isExhausted"] = behavior.CreatedBaseEquipmentTokenIsExhausted
            };
            if (tokenTags.Count > 0)
            {
                payload["tokenTags"] = tokenTags;
            }

            events.Add(new GameEvent(
                "EQUIPMENT_TOKEN_CREATED",
                $"{behavior.DisplayName}打出装备指示物到基地",
                payload));
        }

        playerZones[stackItem.ControllerId] = zones with
        {
            Base = zones.Base.Concat(createdTokenObjectIds).ToArray()
        };
    }

    private static void PlaySourceEquipmentToBase(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        List<GameEvent> events)
    {
        if (!playerZones.TryGetValue(stackItem.ControllerId, out var zones))
        {
            return;
        }

        var existingState = cardObjects.TryGetValue(stackItem.SourceObjectId, out var sourceState)
            ? sourceState
            : new CardObjectState(stackItem.SourceObjectId);
        var equipmentState = existingState with
        {
            IsExhausted = existingState.IsExhausted || behavior.SourceEquipmentIsExhausted,
            Tags = existingState.Tags
                .Concat([CardObjectTags.EquipmentCard])
                .Concat(ParseDelimitedValues(behavior.SourceEquipmentTags))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray()
        };
        cardObjects[stackItem.SourceObjectId] = equipmentState;

        playerZones[stackItem.ControllerId] = zones with
        {
            Base = zones.Base.Contains(stackItem.SourceObjectId, StringComparer.Ordinal)
                ? zones.Base
                : zones.Base.Concat([stackItem.SourceObjectId]).ToArray()
        };

        events.Add(new GameEvent(
            "EQUIPMENT_PLAYED_TO_BASE",
            $"{behavior.DisplayName}打出装备到基地",
            CreateEquipmentPlayedPayload(
                stackItem,
                behavior,
                equipmentState)));
    }

    private static Dictionary<string, object?> CreateEquipmentPlayedPayload(
        StackItemState stackItem,
        CardBehaviorDefinition behavior,
        CardObjectState equipmentState)
    {
        var payload = new Dictionary<string, object?>
            {
                ["playerId"] = stackItem.ControllerId,
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["equipmentObjectId"] = stackItem.SourceObjectId,
                ["equipmentName"] = behavior.DisplayName,
                ["destinationZone"] = "BASE",
                ["tags"] = equipmentState.Tags.ToArray()
            };
        if (equipmentState.IsExhausted)
        {
            payload["isExhausted"] = true;
        }

        return payload;
    }

    private static void PlaySourceUnitToBase(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        IReadOnlyDictionary<string, int> playerExperience,
        List<GameEvent> events)
    {
        if (!playerZones.TryGetValue(stackItem.ControllerId, out var zones))
        {
            return;
        }

        var existingState = cardObjects.TryGetValue(stackItem.SourceObjectId, out var sourceState)
            ? sourceState
            : new CardObjectState(stackItem.SourceObjectId);
        var baseUnitPower = behavior.SourceUnitPower > 0
            ? behavior.SourceUnitPower
            : existingState.Power;
        var unitPower = behavior.AddsControllerGraveyardCountToSourceUnitPower
            ? baseUnitPower + zones.Graveyard.Count
            : baseUnitPower;
        var levelApplies = ControllerMeetsLevelExperienceThreshold(
            behavior,
            stackItem.ControllerId,
            playerExperience);
        if (levelApplies)
        {
            unitPower += behavior.LevelSourceUnitPowerBonus;
        }

        var hasteReadyOptionalCostPaid = CardPermissionKeywordRules.IsHasteReadyOptionalCostPaid(
            behavior,
            stackItem.OptionalCosts);
        var unitState = existingState with
        {
            Power = unitPower,
            IsExhausted = existingState.IsExhausted || (behavior.SourceUnitIsExhausted && !hasteReadyOptionalCostPaid),
            Tags = existingState.Tags
                .Concat([CardObjectTags.UnitCard])
                .Concat(ParseDelimitedValues(behavior.SourceUnitTags))
                .Concat(levelApplies ? ParseDelimitedValues(behavior.LevelSourceUnitTags) : [])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray()
        };
        cardObjects[stackItem.SourceObjectId] = unitState;

        playerZones[stackItem.ControllerId] = zones with
        {
            Base = zones.Base.Contains(stackItem.SourceObjectId, StringComparer.Ordinal)
                ? zones.Base
                : zones.Base.Concat([stackItem.SourceObjectId]).ToArray()
        };

        events.Add(new GameEvent(
            "UNIT_PLAYED_TO_BASE",
            $"{behavior.DisplayName}打出单位到基地",
            CreateUnitPlayedPayload(
                stackItem,
                behavior,
                unitState,
            hasteReadyOptionalCostPaid)));
    }

    private static bool ControllerMeetsLevelExperienceThreshold(
        CardBehaviorDefinition behavior,
        string playerId,
        IReadOnlyDictionary<string, int> playerExperience)
    {
        return behavior.LevelExperienceThreshold > 0
            && playerExperience.TryGetValue(playerId, out var experience)
            && experience >= behavior.LevelExperienceThreshold;
    }

    private static Dictionary<string, object?> CreateUnitPlayedPayload(
        StackItemState stackItem,
        CardBehaviorDefinition behavior,
        CardObjectState unitState,
        bool hasteReadyOptionalCostPaid)
    {
        var payload = new Dictionary<string, object?>
        {
            ["playerId"] = stackItem.ControllerId,
            ["sourceObjectId"] = stackItem.SourceObjectId,
            ["unitObjectId"] = stackItem.SourceObjectId,
            ["unitName"] = behavior.DisplayName,
            ["destinationZone"] = "BASE",
            ["power"] = unitState.Power,
            ["tags"] = unitState.Tags.ToArray()
        };
        if (unitState.IsExhausted)
        {
            payload["isExhausted"] = true;
        }

        if (hasteReadyOptionalCostPaid)
        {
            payload["isExhausted"] = unitState.IsExhausted;
            payload["hasteReadyOptionalCostPaid"] = true;
        }

        return payload;
    }

    private static void BanishFriendlyGraveyardUnits(
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        List<GameEvent> events)
    {
        if (!playerZones.TryGetValue(stackItem.ControllerId, out var zones))
        {
            return;
        }

        var banishedCardIds = zones.Graveyard
            .Where(cardId => CardObjectHasTag(cardObjects, cardId, CardObjectTags.UnitCard))
            .ToArray();
        if (banishedCardIds.Length == 0)
        {
            return;
        }

        var banishedCardIdSet = banishedCardIds.ToHashSet(StringComparer.Ordinal);
        playerZones[stackItem.ControllerId] = zones with
        {
            Graveyard = zones.Graveyard
                .Where(cardId => !banishedCardIdSet.Contains(cardId))
                .ToArray(),
            Banished = zones.Banished
                .Concat(banishedCardIds.Where(cardId => !zones.Banished.Contains(cardId, StringComparer.Ordinal)))
                .ToArray()
        };

        events.Add(new GameEvent(
            "CARDS_BANISHED",
            $"{behavior.DisplayName}放逐废牌堆单位牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = stackItem.ControllerId,
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["cardIds"] = banishedCardIds,
                ["count"] = banishedCardIds.Length,
                ["fromZone"] = "GRAVEYARD",
                ["destinationZone"] = "BANISHED"
            }));
    }

    private static IReadOnlyList<string> ParseDelimitedValues(string values)
    {
        if (string.IsNullOrWhiteSpace(values))
        {
            return [];
        }

        return values
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string NextTokenObjectId(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string sourceObjectId,
        int initialTokenNumber)
    {
        var tokenNumber = Math.Max(1, initialTokenNumber);
        while (true)
        {
            var candidate = $"{sourceObjectId}-TOKEN-{tokenNumber:000}";
            if (!cardObjects.ContainsKey(candidate)
                && !IsObjectIdInAnyZone(playerZones, candidate))
            {
                return candidate;
            }

            tokenNumber++;
        }
    }

    private static bool IsObjectIdInAnyZone(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId)
    {
        return playerZones.Values.Any(zones =>
            zones.MainDeck.Contains(objectId, StringComparer.Ordinal)
            || zones.RuneDeck.Contains(objectId, StringComparer.Ordinal)
            || zones.Hand.Contains(objectId, StringComparer.Ordinal)
            || zones.Base.Contains(objectId, StringComparer.Ordinal)
            || zones.Battlefields.Contains(objectId, StringComparer.Ordinal)
            || zones.Graveyard.Contains(objectId, StringComparer.Ordinal)
            || zones.Banished.Contains(objectId, StringComparer.Ordinal)
            || zones.LegendZone.Contains(objectId, StringComparer.Ordinal)
            || zones.ChampionZone.Contains(objectId, StringComparer.Ordinal));
    }

    private static CardObjectState ApplyPowerModifier(
        CardObjectState targetState,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        string targetObjectId,
        int powerModifierAmount,
        out GameEvent powerEvent)
    {
        var previousPower = targetState.Power;
        var resultingPower = Math.Max(
            behavior.MinimumPowerAfterModifier,
            previousPower + powerModifierAmount);
        var appliedPowerDelta = resultingPower - previousPower;
        var nextTargetState = targetState with
        {
            Power = resultingPower,
            UntilEndOfTurnPowerModifier = targetState.UntilEndOfTurnPowerModifier
                + appliedPowerDelta
        };
        powerEvent = new GameEvent(
            "POWER_MODIFIED_UNTIL_END_OF_TURN",
            $"{behavior.DisplayName}临时修正战力",
            new Dictionary<string, object?>
            {
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["targetObjectId"] = targetObjectId,
                ["powerDelta"] = powerModifierAmount,
                ["appliedPowerDelta"] = appliedPowerDelta,
                ["minimumPower"] = behavior.MinimumPowerAfterModifier,
                ["resultingPower"] = nextTargetState.Power
            });

        return nextTargetState;
    }

    private static CardObjectState ApplyBoon(
        CardObjectState targetState,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        string targetObjectId,
        out IReadOnlyList<GameEvent> boonEvents)
    {
        if (targetState.Tags.Contains(CardObjectTags.Boon, StringComparer.Ordinal))
        {
            boonEvents = [];
            return targetState;
        }

        var nextTargetState = targetState with
        {
            Power = targetState.Power + 1,
            Tags = targetState.Tags
                .Concat([CardObjectTags.Boon])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray()
        };
        boonEvents =
        [
            new GameEvent(
                "OBJECT_TAG_ADDED",
                $"{behavior.DisplayName}给予增益标签",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["targetObjectId"] = targetObjectId,
                    ["tag"] = CardObjectTags.Boon
                }),
            new GameEvent(
                "BOON_GRANTED",
                $"{behavior.DisplayName}给予增益",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["targetObjectId"] = targetObjectId,
                    ["powerDelta"] = 1,
                    ["resultingPower"] = nextTargetState.Power
                })
        ];

        return nextTargetState;
    }

    private static CardObjectState ApplyTargetTag(
        CardObjectState targetState,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        string targetObjectId,
        out GameEvent tagEvent)
    {
        var nextTargetState = targetState with
        {
            Tags = targetState.Tags
                .Concat([behavior.TargetAddedTag])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray()
        };
        tagEvent = new GameEvent(
            "OBJECT_TAG_ADDED",
            $"{behavior.DisplayName}给予对象标签",
            new Dictionary<string, object?>
            {
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["targetObjectId"] = targetObjectId,
                ["tag"] = behavior.TargetAddedTag
            });

        return nextTargetState;
    }

    private static CardObjectState ApplyReadyState(
        CardObjectState targetState,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        string targetObjectId,
        out GameEvent readyEvent)
    {
        var wasExhausted = targetState.IsExhausted;
        var nextTargetState = targetState with
        {
            IsExhausted = false
        };
        readyEvent = new GameEvent(
            "UNIT_READIED",
            $"{behavior.DisplayName}让单位变为活跃状态",
            new Dictionary<string, object?>
            {
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["targetObjectId"] = targetObjectId,
                ["wasExhausted"] = wasExhausted,
                ["isExhausted"] = nextTargetState.IsExhausted
            });

        return nextTargetState;
    }

    private static CardObjectState ApplyExhaustState(
        CardObjectState targetState,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        string targetObjectId,
        out GameEvent exhaustedEvent)
    {
        var wasExhausted = targetState.IsExhausted;
        var nextTargetState = targetState with
        {
            IsExhausted = true
        };
        exhaustedEvent = new GameEvent(
            "UNIT_EXHAUSTED",
            $"{behavior.DisplayName}让单位变为休眠状态",
            new Dictionary<string, object?>
            {
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["targetObjectId"] = targetObjectId,
                ["wasExhausted"] = wasExhausted,
                ["isExhausted"] = nextTargetState.IsExhausted
            });

        return nextTargetState;
    }

    private static int ResolvePowerModifierAmount(
        CardBehaviorDefinition behavior,
        int targetIndex,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string controllerId,
        string targetObjectId)
    {
        if (behavior.UsesTargetCurrentPowerAsPowerModifier
            && cardObjects.TryGetValue(targetObjectId, out var targetState))
        {
            return targetState.Power;
        }

        if (behavior.UsesEnemyBattlefieldUnitCountAsPowerModifierMultiplier)
        {
            return behavior.PowerModifierAmount * CountEnemyBattlefieldUnits(playerZones, cardObjects, controllerId);
        }

        if (!string.IsNullOrWhiteSpace(behavior.PowerModifierControlledUnitTags))
        {
            return CountControlledUnitTagKinds(
                playerZones,
                cardObjects,
                controllerId,
                behavior.PowerModifierControlledUnitTags);
        }

        return targetIndex == 1 && behavior.SecondaryPowerModifierAmount != 0
            ? behavior.SecondaryPowerModifierAmount
            : behavior.PowerModifierAmount;
    }

    private static int CountEnemyBattlefieldUnits(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId)
    {
        return playerZones
            .Where(entry => !string.Equals(entry.Key, playerId, StringComparison.Ordinal))
            .SelectMany(entry => entry.Value.Battlefields)
            .Count(objectId => cardObjects.ContainsKey(objectId));
    }

    private static int CountControlledUnitTagKinds(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        string delimitedTags)
    {
        var trackedTags = delimitedTags
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
        if (trackedTags.Count == 0)
        {
            return 0;
        }

        var presentTags = new HashSet<string>(StringComparer.Ordinal);
        foreach (var objectId in GetControlledFieldUnitObjectIds(playerZones, cardObjects, playerId))
        {
            if (!cardObjects.TryGetValue(objectId, out var cardObject))
            {
                continue;
            }

            foreach (var tag in cardObject.Tags)
            {
                if (trackedTags.Contains(tag))
                {
                    presentTags.Add(tag);
                }
            }
        }

        return presentTags.Count;
    }

    private static string[] ResolveStatusEffectIds(CardBehaviorDefinition behavior)
    {
        return new[] { behavior.StatusEffectId, behavior.SecondaryStatusEffectId }
            .Where(statusEffectId => !string.IsNullOrWhiteSpace(statusEffectId))
            .ToArray();
    }

    private static bool ShouldApplyStatusEffectsToTarget(CardBehaviorDefinition behavior, int targetIndex)
    {
        return behavior.StatusEffectTargetIndex < 0 || behavior.StatusEffectTargetIndex == targetIndex;
    }

    private static bool ShouldApplyDamageToTarget(CardBehaviorDefinition behavior, int targetIndex)
    {
        return behavior.DamageTargetIndex < 0 || behavior.DamageTargetIndex == targetIndex;
    }

    private static bool ShouldPreventSpellOrSkillDamage(
        MatchState state,
        CardBehaviorDefinition behavior)
    {
        if (!state.UntilEndOfTurnEffects.Contains(
                PreventSpellAndSkillDamageThisTurnEffectId,
                StringComparer.Ordinal))
        {
            return false;
        }

        return !behavior.PlaysSourceToBaseAsUnit
            && !behavior.PlaysSourceToBaseAsEquipment;
    }

    private static bool ShouldApplyBanishPlayToTarget(CardBehaviorDefinition behavior, int targetIndex)
    {
        return behavior.BanishPlayTargetIndex < 0 || behavior.BanishPlayTargetIndex == targetIndex;
    }

    private static bool ShouldApplyPowerModifierToTarget(CardBehaviorDefinition behavior, int targetIndex)
    {
        return behavior.PowerModifierTargetIndex < 0 || behavior.PowerModifierTargetIndex == targetIndex;
    }

    private static bool PowerModifierConditionApplies(
        CardObjectState targetState,
        CardBehaviorDefinition behavior)
    {
        return behavior.PowerModifierConditionKind switch
        {
            CardPowerModifierConditionKinds.TargetIsAttacking => targetState.IsAttacking,
            CardPowerModifierConditionKinds.TargetIsDefending => targetState.IsDefending,
            _ => true
        };
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
                .Where(cardId =>
                    zones.Graveyard.Contains(cardId, StringComparer.Ordinal)
                    || zones.Hand.Contains(cardId, StringComparer.Ordinal))
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
                    .ToArray(),
                Hand = zones.Hand
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

    private static RecycleResult RecycleUnkeptSacredJudgmentCards(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string sourceObjectId,
        IReadOnlyList<string> keptObjectIds,
        long rngCursor)
    {
        var events = new List<GameEvent>();
        var keptSet = keptObjectIds.ToHashSet(StringComparer.Ordinal);
        foreach (var (playerId, zones) in playerZones.ToArray())
        {
            var recycledCardIds = SacredJudgmentFieldUnitIds(cardObjects, zones)
                .Concat(SacredJudgmentEquipmentIds(cardObjects, zones))
                .Concat(SacredJudgmentRuneIds(cardObjects, zones))
                .Concat(zones.Hand)
                .Where(cardId => !keptSet.Contains(cardId))
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

            playerZones[playerId] = zones with
            {
                MainDeck = zones.MainDeck.Concat(randomizedCardIds).ToArray(),
                Hand = zones.Hand
                    .Where(cardId => !recycledCardIds.Contains(cardId, StringComparer.Ordinal))
                    .ToArray(),
                Base = zones.Base
                    .Where(cardId => !recycledCardIds.Contains(cardId, StringComparer.Ordinal))
                    .ToArray(),
                Battlefields = zones.Battlefields
                    .Where(cardId => !recycledCardIds.Contains(cardId, StringComparer.Ordinal))
                    .ToArray()
            };
            events.Add(new GameEvent(
                "CARDS_RECYCLED",
                $"{playerId} 回收 {recycledCardIds.Length} 张牌",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = sourceObjectId,
                    ["cardIds"] = randomizedCardIds,
                    ["count"] = randomizedCardIds.Count
                }));
        }

        return new RecycleResult(events, rngCursor);
    }

    private static RecycleResult PlayEachPlayerTopFiveUnitsToBase(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string controllerId,
        string sourceObjectId,
        IReadOnlyList<string> selectedObjectIds,
        int lookCount,
        long rngCursor)
    {
        var events = new List<GameEvent>();
        if (lookCount <= 0)
        {
            return new RecycleResult(events, rngCursor);
        }

        var selectedByPlayerId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var selectedObjectId in selectedObjectIds)
        {
            if (TryGetTopMainDeckOwner(playerZones, selectedObjectId, lookCount, out var playerId))
            {
                selectedByPlayerId[playerId] = selectedObjectId;
            }
        }

        foreach (var playerId in SeatPlayerIds(state))
        {
            if (!selectedByPlayerId.TryGetValue(playerId, out var selectedObjectId)
                || !playerZones.TryGetValue(playerId, out var zones))
            {
                continue;
            }

            var viewedCardIds = zones.MainDeck.Take(lookCount).ToArray();
            var unselectedCardIds = viewedCardIds
                .Where(cardId => !string.Equals(cardId, selectedObjectId, StringComparison.Ordinal))
                .ToArray();
            var removedCardIds = viewedCardIds.ToHashSet(StringComparer.Ordinal);
            var randomizedRecycledCardIds = RandomizeForMainDeckBottom(
                unselectedCardIds,
                state.Seed,
                rngCursor,
                sourceObjectId);
            if (unselectedCardIds.Length > 1)
            {
                rngCursor++;
            }

            playerZones[playerId] = zones with
            {
                MainDeck = zones.MainDeck
                    .Where(cardId => !removedCardIds.Contains(cardId))
                    .Concat(randomizedRecycledCardIds)
                    .ToArray()
            };

            if (randomizedRecycledCardIds.Count > 0)
            {
                events.Add(new GameEvent(
                    "CARDS_RECYCLED",
                    $"{playerId} 回收 {randomizedRecycledCardIds.Count} 张牌",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = playerId,
                        ["sourceObjectId"] = sourceObjectId,
                        ["cardIds"] = randomizedRecycledCardIds,
                        ["count"] = randomizedRecycledCardIds.Count
                    }));
            }
        }

        foreach (var playerId in PlayerIdsStartingAfter(state, controllerId))
        {
            if (!selectedByPlayerId.TryGetValue(playerId, out var selectedObjectId)
                || !TryPutSelectedMainDeckUnitToBase(
                    playerZones,
                    cardObjects,
                    playerId,
                    selectedObjectId,
                    out _))
            {
                continue;
            }

            events.Add(new GameEvent(
                "UNIT_PLAYED_TO_BASE",
                $"{playerId} 打出主牌堆选择的单位到基地",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = sourceObjectId,
                    ["targetObjectId"] = selectedObjectId,
                    ["ownerPlayerId"] = playerId,
                    ["playedByPlayerId"] = playerId,
                    ["sourceZone"] = "MAIN_DECK",
                    ["destinationZone"] = "BASE"
                }));
        }

        return new RecycleResult(events, rngCursor);
    }

    private static bool TryPutSelectedMainDeckUnitToBase(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string targetObjectId,
        out CardObjectState targetState)
    {
        targetState = cardObjects.TryGetValue(targetObjectId, out var existingTargetState)
            ? existingTargetState
            : new CardObjectState(targetObjectId);
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        playerZones[playerId] = zones with
        {
            Base = zones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                ? zones.Base
                : zones.Base.Concat([targetObjectId]).ToArray()
        };

        targetState = targetState with
        {
            Damage = 0,
            Power = Math.Max(0, targetState.Power - targetState.UntilEndOfTurnPowerModifier),
            UntilEndOfTurnEffects = [],
            UntilEndOfTurnPowerModifier = 0,
            IsExhausted = false
        };
        cardObjects[targetObjectId] = targetState;
        return true;
    }

    private static IReadOnlyList<string> MainDeckTargetObjectIds(
        IReadOnlyList<string> targetObjectIds,
        CardBehaviorDefinition behavior)
    {
        return targetObjectIds
            .Skip(Math.Max(0, behavior.MainDeckLookTargetStartIndex))
            .ToArray();
    }

    private static RecycleResult RecycleSelectedMainDeckTargets(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        string playerId,
        string sourceObjectId,
        IReadOnlyList<string> selectedObjectIds,
        int lookCount)
    {
        var events = new List<GameEvent>();
        var rngCursor = state.RngCursor;
        if (lookCount <= 0
            || selectedObjectIds.Count == 0
            || !playerZones.TryGetValue(playerId, out var zones))
        {
            return new RecycleResult(events, rngCursor);
        }

        var viewedCardIds = zones.MainDeck.Take(lookCount).ToArray();
        var recycledCardIds = selectedObjectIds
            .Where(cardId => viewedCardIds.Contains(cardId, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (recycledCardIds.Length == 0)
        {
            return new RecycleResult(events, rngCursor);
        }

        var randomizedRecycledCardIds = RandomizeForMainDeckBottom(
            recycledCardIds,
            state.Seed,
            rngCursor,
            sourceObjectId);
        if (recycledCardIds.Length > 1)
        {
            rngCursor++;
        }

        var recycledCardIdSet = recycledCardIds.ToHashSet(StringComparer.Ordinal);
        playerZones[playerId] = zones with
        {
            MainDeck = zones.MainDeck
                .Where(cardId => !recycledCardIdSet.Contains(cardId))
                .Concat(randomizedRecycledCardIds)
                .ToArray()
        };

        events.Add(new GameEvent(
            "CARDS_RECYCLED",
            $"{playerId} 回收 {randomizedRecycledCardIds.Count} 张牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = sourceObjectId,
                ["count"] = randomizedRecycledCardIds.Count
            }));

        return new RecycleResult(events, rngCursor);
    }

    private static RecycleResult DrawSelectedMainDeckTargetsAndRecycleRest(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        string playerId,
        string sourceObjectId,
        IReadOnlyList<string> selectedObjectIds,
        int lookCount,
        bool recycleUnselectedCards)
    {
        var events = new List<GameEvent>();
        var rngCursor = state.RngCursor;
        if (lookCount <= 0
            || !playerZones.TryGetValue(playerId, out var zones))
        {
            return new RecycleResult(events, rngCursor);
        }

        var viewedCardIds = zones.MainDeck.Take(lookCount).ToArray();
        var selectedCardIds = selectedObjectIds
            .Where(cardId => viewedCardIds.Contains(cardId, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var unselectedCardIds = recycleUnselectedCards
            ? viewedCardIds
                .Where(cardId => !selectedCardIds.Contains(cardId, StringComparer.Ordinal))
                .ToArray()
            : Array.Empty<string>();
        if (selectedCardIds.Length == 0
            && unselectedCardIds.Length == 0)
        {
            return new RecycleResult(events, rngCursor);
        }

        var removedCardIds = selectedCardIds
            .Concat(unselectedCardIds)
            .ToHashSet(StringComparer.Ordinal);
        var randomizedRecycledCardIds = RandomizeForMainDeckBottom(
            unselectedCardIds,
            state.Seed,
            rngCursor,
            sourceObjectId);
        if (unselectedCardIds.Length > 1)
        {
            rngCursor++;
        }

        playerZones[playerId] = zones with
        {
            MainDeck = zones.MainDeck
                .Where(cardId => !removedCardIds.Contains(cardId))
                .Concat(randomizedRecycledCardIds)
                .ToArray(),
            Hand = zones.Hand.Concat(selectedCardIds).ToArray()
        };

        if (selectedCardIds.Length > 0)
        {
            events.Add(new GameEvent(
                "CARD_DRAWN",
                $"{playerId} 抽 {selectedCardIds.Length} 张牌",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = sourceObjectId,
                    ["count"] = selectedCardIds.Length
                }));
        }

        if (randomizedRecycledCardIds.Count > 0)
        {
            events.Add(new GameEvent(
                "CARDS_RECYCLED",
                $"{playerId} 回收 {randomizedRecycledCardIds.Count} 张牌",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = sourceObjectId,
                    ["count"] = randomizedRecycledCardIds.Count
                }));
        }

        return new RecycleResult(events, rngCursor);
    }

    private static RuneCallResult CallRunes(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        int runeCallCount)
    {
        if (runeCallCount <= 0
            || !playerZones.TryGetValue(playerId, out var zones))
        {
            return new RuneCallResult([]);
        }

        var calledRunes = zones.RuneDeck.Take(runeCallCount).ToArray();
        if (calledRunes.Length == 0)
        {
            return new RuneCallResult([]);
        }

        playerZones[playerId] = zones with
        {
            RuneDeck = zones.RuneDeck.Skip(calledRunes.Length).ToArray(),
            Base = zones.Base.Concat(calledRunes).ToArray()
        };

        foreach (var runeObjectId in calledRunes)
        {
            var runeState = cardObjects.TryGetValue(runeObjectId, out var existingRuneState)
                ? existingRuneState
                : new CardObjectState(runeObjectId);
            cardObjects[runeObjectId] = runeState with
            {
                IsExhausted = true
            };
        }

        return new RuneCallResult(calledRunes);
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
        out FieldRemovalResult removalResult)
    {
        removalResult = FieldRemovalResult.Empty;
        foreach (var (playerId, zones) in playerZones)
        {
            var isInBase = zones.Base.Contains(targetObjectId, StringComparer.Ordinal);
            var isInBattlefield = zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal);
            if (!isInBase && !isInBattlefield)
            {
                continue;
            }

            var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTargetState)
                ? existingTargetState
                : new CardObjectState(targetObjectId);
            var wasEquipment = targetState.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal);
            var wasUnit = !wasEquipment
                || targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal);
            var shouldRecallToBase = targetState.UntilEndOfTurnEffects.Contains(
                RecallToBaseExhaustedIfDestroyedThisTurnEffectId,
                StringComparer.Ordinal);
            var shouldBanish = !shouldRecallToBase
                && targetState.UntilEndOfTurnEffects.Contains(
                    BanishIfDestroyedThisTurnEffectId,
                    StringComparer.Ordinal);
            if (shouldRecallToBase)
            {
                playerZones[playerId] = zones with
                {
                    Base = zones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                        ? zones.Base
                        : zones.Base.Concat([targetObjectId]).ToArray(),
                    Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId)
                };
                cardObjects[targetObjectId] = targetState with
                {
                    Damage = 0,
                    IsExhausted = true,
                    UntilEndOfTurnEffects = targetState.UntilEndOfTurnEffects
                        .Where(effectId => !string.Equals(
                            effectId,
                            RecallToBaseExhaustedIfDestroyedThisTurnEffectId,
                            StringComparison.Ordinal))
                        .ToArray()
                };
                removalResult = new FieldRemovalResult(
                    playerId,
                    "BASE",
                    false,
                    true,
                    wasEquipment,
                    wasUnit);
                return true;
            }

            playerZones[playerId] = zones with
            {
                Base = RemoveFromZone(zones.Base, targetObjectId),
                Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId),
                Graveyard = shouldBanish || zones.Graveyard.Contains(targetObjectId, StringComparer.Ordinal)
                    ? zones.Graveyard
                    : zones.Graveyard.Concat([targetObjectId]).ToArray(),
                Banished = shouldBanish && !zones.Banished.Contains(targetObjectId, StringComparer.Ordinal)
                    ? zones.Banished.Concat([targetObjectId]).ToArray()
                    : zones.Banished
            };
            cardObjects.Remove(targetObjectId);
            removalResult = new FieldRemovalResult(
                playerId,
                shouldBanish ? "BANISHED" : "GRAVEYARD",
                shouldBanish,
                false,
                wasEquipment,
                wasUnit);
            return true;
        }

        return false;
    }

    private static GameEvent BuildFieldRemovalEvent(
        string displayName,
        StackItemState stackItem,
        string targetObjectId,
        FieldRemovalResult removalResult,
        string? reason = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["sourceObjectId"] = stackItem.SourceObjectId,
            ["targetObjectId"] = targetObjectId,
            ["ownerPlayerId"] = removalResult.OwnerPlayerId,
            ["destroyedByPlayerId"] = stackItem.ControllerId,
            ["destinationZone"] = removalResult.DestinationZone
        };
        if (!string.IsNullOrWhiteSpace(reason))
        {
            payload["reason"] = reason;
        }

        if (removalResult.WasBanished)
        {
            payload["replacementEffectId"] = BanishIfDestroyedThisTurnEffectId;
        }

        if (removalResult.WasRecalledToBase)
        {
            payload["replacementEffectId"] = RecallToBaseExhaustedIfDestroyedThisTurnEffectId;
            return new GameEvent("UNIT_RECALLED_TO_BASE", $"{displayName}改为休眠召回单位", payload);
        }

        if (removalResult.WasBanished)
        {
            return new GameEvent("UNIT_BANISHED", $"{displayName}改为放逐单位", payload);
        }

        return removalResult.WasEquipment
            ? new GameEvent("EQUIPMENT_DESTROYED", $"{displayName}摧毁装备", payload)
            : new GameEvent("UNIT_DESTROYED", $"{displayName}摧毁单位", payload);
    }

    private static GameEvent BuildReturnedToHandEvent(
        string displayName,
        StackItemState stackItem,
        string targetObjectId,
        string ownerPlayerId,
        bool wasEquipment,
        string reason = "")
    {
        var payload = new Dictionary<string, object?>
        {
            ["sourceObjectId"] = stackItem.SourceObjectId,
            ["targetObjectId"] = targetObjectId,
            ["ownerPlayerId"] = ownerPlayerId
        };
        if (!string.IsNullOrWhiteSpace(reason))
        {
            payload["reason"] = reason;
        }

        return new GameEvent(
            wasEquipment ? "EQUIPMENT_RETURNED_TO_HAND" : "UNIT_RETURNED_TO_HAND",
            wasEquipment ? $"{displayName}让装备返回手牌" : $"{displayName}让单位返回手牌",
            payload);
    }

    private static bool TryReturnTargetToHand(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        out string ownerPlayerId,
        out bool wasEquipment)
    {
        ownerPlayerId = string.Empty;
        wasEquipment = false;
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
                Hand = zones.Hand.Contains(targetObjectId, StringComparer.Ordinal)
                    ? zones.Hand
                    : zones.Hand.Concat([targetObjectId]).ToArray()
            };
            wasEquipment = cardObjects.TryGetValue(targetObjectId, out var targetState)
                && targetState.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal);
            cardObjects.Remove(targetObjectId);
            ownerPlayerId = playerId;
            return true;
        }

        return false;
    }

    private static bool TryMoveTargetToOwnerMainDeck(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        string destination,
        out string ownerPlayerId,
        out string deckPosition)
    {
        ownerPlayerId = string.Empty;
        deckPosition = NormalizeMainDeckDestination(destination);
        if (string.IsNullOrWhiteSpace(deckPosition))
        {
            return false;
        }

        foreach (var (playerId, zones) in playerZones)
        {
            var isInBase = zones.Base.Contains(targetObjectId, StringComparer.Ordinal);
            var isInBattlefield = zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal);
            if (!isInBase && !isInBattlefield)
            {
                continue;
            }

            var remainingMainDeck = RemoveFromZone(zones.MainDeck, targetObjectId);
            var mainDeck = string.Equals(deckPosition, "TOP", StringComparison.Ordinal)
                ? new[] { targetObjectId }.Concat(remainingMainDeck).ToArray()
                : remainingMainDeck.Concat([targetObjectId]).ToArray();
            playerZones[playerId] = zones with
            {
                Base = RemoveFromZone(zones.Base, targetObjectId),
                Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId),
                MainDeck = mainDeck
            };
            cardObjects.Remove(targetObjectId);
            ownerPlayerId = playerId;
            return true;
        }

        return false;
    }

    private static string NormalizeMainDeckDestination(string destination)
    {
        var normalizedDestination = destination.Trim().ToUpperInvariant();
        return normalizedDestination is "TOP" or "BOTTOM" ? normalizedDestination : string.Empty;
    }

    private static bool TryBanishTargetThenPlayToOwnerField(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        string destinationZone,
        out string ownerPlayerId)
    {
        ownerPlayerId = string.Empty;
        var playToBattlefield = string.Equals(destinationZone, "BATTLEFIELD", StringComparison.Ordinal);
        foreach (var (playerId, zones) in playerZones)
        {
            var isInBase = zones.Base.Contains(targetObjectId, StringComparer.Ordinal);
            var isInBattlefield = zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal);
            if (!isInBase && !isInBattlefield)
            {
                continue;
            }

            var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTargetState)
                ? existingTargetState
                : new CardObjectState(targetObjectId);
            playerZones[playerId] = zones with
            {
                Base = RemoveFromZone(zones.Base, targetObjectId),
                Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId),
                Banished = zones.Banished.Contains(targetObjectId, StringComparer.Ordinal)
                    ? zones.Banished
                    : zones.Banished.Concat([targetObjectId]).ToArray()
            };

            var banishedZones = playerZones[playerId];
            playerZones[playerId] = banishedZones with
            {
                Banished = RemoveFromZone(banishedZones.Banished, targetObjectId),
                Base = playToBattlefield || banishedZones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                    ? banishedZones.Base
                    : banishedZones.Base.Concat([targetObjectId]).ToArray(),
                Battlefields = !playToBattlefield || banishedZones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal)
                    ? banishedZones.Battlefields
                    : banishedZones.Battlefields.Concat([targetObjectId]).ToArray()
            };
            cardObjects[targetObjectId] = targetState with
            {
                Damage = 0,
                Power = Math.Max(0, targetState.Power - targetState.UntilEndOfTurnPowerModifier),
                UntilEndOfTurnEffects = [],
                UntilEndOfTurnPowerModifier = 0,
                IsExhausted = false
            };
            ownerPlayerId = playerId;
            return true;
        }

        return false;
    }

    private static bool TryDiscardCardFromHand(
        Dictionary<string, PlayerZones> playerZones,
        string playerId,
        string targetObjectId)
    {
        if (!playerZones.TryGetValue(playerId, out var zones)
            || !zones.Hand.Contains(targetObjectId, StringComparer.Ordinal))
        {
            return false;
        }

        playerZones[playerId] = zones with
        {
            Hand = RemoveFromZone(zones.Hand, targetObjectId),
            Graveyard = zones.Graveyard.Contains(targetObjectId, StringComparer.Ordinal)
                ? zones.Graveyard
                : zones.Graveyard.Concat([targetObjectId]).ToArray()
        };
        return true;
    }

    private static bool TryDiscardCardFromAnyHand(
        Dictionary<string, PlayerZones> playerZones,
        string targetObjectId,
        out string ownerPlayerId)
    {
        ownerPlayerId = string.Empty;
        foreach (var (playerId, zones) in playerZones)
        {
            if (!zones.Hand.Contains(targetObjectId, StringComparer.Ordinal))
            {
                continue;
            }

            playerZones[playerId] = zones with
            {
                Hand = RemoveFromZone(zones.Hand, targetObjectId),
                Graveyard = zones.Graveyard.Contains(targetObjectId, StringComparer.Ordinal)
                    ? zones.Graveyard
                    : zones.Graveyard.Concat([targetObjectId]).ToArray()
            };
            ownerPlayerId = playerId;
            return true;
        }

        return false;
    }

    private static bool TryDiscardPlayerHand(
        Dictionary<string, PlayerZones> playerZones,
        string playerId,
        out IReadOnlyList<string> discardedObjectIds)
    {
        discardedObjectIds = [];
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        discardedObjectIds = zones.Hand
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .ToArray();
        playerZones[playerId] = zones with
        {
            Hand = [],
            Graveyard = zones.Graveyard
                .Concat(discardedObjectIds.Where(objectId =>
                    !zones.Graveyard.Contains(objectId, StringComparer.Ordinal)))
                .ToArray()
        };
        return true;
    }

    private static bool TryReturnGraveyardCardToHand(
        Dictionary<string, PlayerZones> playerZones,
        string playerId,
        string targetObjectId)
    {
        if (!playerZones.TryGetValue(playerId, out var zones)
            || !zones.Graveyard.Contains(targetObjectId, StringComparer.Ordinal))
        {
            return false;
        }

        playerZones[playerId] = zones with
        {
            Graveyard = RemoveFromZone(zones.Graveyard, targetObjectId),
            Hand = zones.Hand.Contains(targetObjectId, StringComparer.Ordinal)
                ? zones.Hand
                : zones.Hand.Concat([targetObjectId]).ToArray()
        };
        return true;
    }

    private static bool TryGainControlOfTargetToBase(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string controllerId,
        string targetObjectId,
        bool isExhausted,
        out string previousControllerId,
        out CardObjectState controlledTargetState)
    {
        previousControllerId = string.Empty;
        controlledTargetState = cardObjects.TryGetValue(targetObjectId, out var existingTargetState)
            ? existingTargetState
            : new CardObjectState(targetObjectId);
        if (!playerZones.ContainsKey(controllerId))
        {
            return false;
        }

        foreach (var (playerId, zones) in playerZones)
        {
            if (string.Equals(playerId, controllerId, StringComparison.Ordinal)
                || !zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal))
            {
                continue;
            }

            playerZones[playerId] = zones with
            {
                Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId)
            };
            var controllerZones = playerZones[controllerId];
            playerZones[controllerId] = controllerZones with
            {
                Base = controllerZones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                    ? controllerZones.Base
                    : controllerZones.Base.Concat([targetObjectId]).ToArray()
            };
            controlledTargetState = controlledTargetState with
            {
                IsExhausted = controlledTargetState.IsExhausted || isExhausted
            };
            cardObjects[targetObjectId] = controlledTargetState;
            previousControllerId = playerId;
            return true;
        }

        return false;
    }

    private static bool TryPlayGraveyardCardToBase(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string targetObjectId)
    {
        if (!playerZones.TryGetValue(playerId, out var zones)
            || !zones.Graveyard.Contains(targetObjectId, StringComparer.Ordinal))
        {
            return false;
        }

        playerZones[playerId] = zones with
        {
            Graveyard = RemoveFromZone(zones.Graveyard, targetObjectId),
            Base = zones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                ? zones.Base
                : zones.Base.Concat([targetObjectId]).ToArray()
        };

        var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTargetState)
            ? existingTargetState
            : new CardObjectState(targetObjectId);
        cardObjects[targetObjectId] = targetState with
        {
            Damage = 0,
            Power = Math.Max(0, targetState.Power - targetState.UntilEndOfTurnPowerModifier),
            UntilEndOfTurnEffects = [],
            UntilEndOfTurnPowerModifier = 0,
            IsExhausted = false
        };
        return true;
    }

    private static bool TryPlayHandCardToBase(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        string statusEffectId,
        out string ownerPlayerId,
        out CardObjectState targetState)
    {
        ownerPlayerId = string.Empty;
        targetState = cardObjects.TryGetValue(targetObjectId, out var existingTargetState)
            ? existingTargetState
            : new CardObjectState(targetObjectId);

        foreach (var (playerId, zones) in playerZones)
        {
            if (!zones.Hand.Contains(targetObjectId, StringComparer.Ordinal))
            {
                continue;
            }

            playerZones[playerId] = zones with
            {
                Hand = RemoveFromZone(zones.Hand, targetObjectId),
                Base = zones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                    ? zones.Base
                    : zones.Base.Concat([targetObjectId]).ToArray()
            };

            targetState = targetState with
            {
                Damage = 0,
                Power = Math.Max(0, targetState.Power - targetState.UntilEndOfTurnPowerModifier),
                UntilEndOfTurnEffects = string.IsNullOrWhiteSpace(statusEffectId)
                    ? []
                    : targetState.UntilEndOfTurnEffects
                        .Concat([statusEffectId])
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(effectId => effectId, StringComparer.Ordinal)
                        .ToArray(),
                UntilEndOfTurnPowerModifier = 0,
                IsExhausted = false
            };
            cardObjects[targetObjectId] = targetState;
            ownerPlayerId = playerId;
            return true;
        }

        return false;
    }

    private static bool TryPlayOpponentTopMainDeckUnitToBase(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string controllerId,
        string targetObjectId,
        out string ownerPlayerId,
        out CardObjectState targetState)
    {
        ownerPlayerId = string.Empty;
        targetState = cardObjects.TryGetValue(targetObjectId, out var existingTargetState)
            ? existingTargetState
            : new CardObjectState(targetObjectId);
        if (!playerZones.ContainsKey(controllerId))
        {
            return false;
        }

        foreach (var (playerId, zones) in playerZones)
        {
            if (string.Equals(playerId, controllerId, StringComparison.Ordinal)
                || zones.MainDeck.Count == 0
                || !string.Equals(zones.MainDeck[0], targetObjectId, StringComparison.Ordinal))
            {
                continue;
            }

            playerZones[playerId] = zones with
            {
                MainDeck = RemoveFromZone(zones.MainDeck, targetObjectId)
            };

            var controllerZones = playerZones[controllerId];
            playerZones[controllerId] = controllerZones with
            {
                Base = controllerZones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                    ? controllerZones.Base
                    : controllerZones.Base.Concat([targetObjectId]).ToArray()
            };

            targetState = targetState with
            {
                Damage = 0,
                Power = Math.Max(0, targetState.Power - targetState.UntilEndOfTurnPowerModifier),
                UntilEndOfTurnEffects = [],
                UntilEndOfTurnPowerModifier = 0,
                IsExhausted = false
            };
            cardObjects[targetObjectId] = targetState;
            ownerPlayerId = playerId;
            return true;
        }

        return false;
    }

    private static bool TryGainControlOfTargetToBattlefield(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string controllerId,
        string targetObjectId,
        out string previousControllerId,
        out CardObjectState controlledTargetState)
    {
        previousControllerId = string.Empty;
        controlledTargetState = cardObjects.TryGetValue(targetObjectId, out var existingTargetState)
            ? existingTargetState
            : new CardObjectState(targetObjectId);
        if (!playerZones.ContainsKey(controllerId))
        {
            return false;
        }

        foreach (var (playerId, zones) in playerZones)
        {
            if (string.Equals(playerId, controllerId, StringComparison.Ordinal)
                || !zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal))
            {
                continue;
            }

            playerZones[playerId] = zones with
            {
                Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId)
            };
            var controllerZones = playerZones[controllerId];
            playerZones[controllerId] = controllerZones with
            {
                Battlefields = controllerZones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal)
                    ? controllerZones.Battlefields
                    : controllerZones.Battlefields.Concat([targetObjectId]).ToArray()
            };
            cardObjects[targetObjectId] = controlledTargetState;
            previousControllerId = playerId;
            return true;
        }

        return false;
    }

    private static bool TryMoveTargetToOwnerBase(
        Dictionary<string, PlayerZones> playerZones,
        string targetObjectId,
        out string ownerPlayerId)
    {
        ownerPlayerId = string.Empty;
        foreach (var (playerId, zones) in playerZones)
        {
            if (!zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal))
            {
                continue;
            }

            playerZones[playerId] = zones with
            {
                Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId),
                Base = zones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                    ? zones.Base
                    : zones.Base.Concat([targetObjectId]).ToArray()
            };
            ownerPlayerId = playerId;
            return true;
        }

        return false;
    }

    private static void RemoveDamageFromFriendlyBattlefieldUnits(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        List<GameEvent> events)
    {
        if (!playerZones.TryGetValue(stackItem.ControllerId, out var zones))
        {
            return;
        }

        var damagedObjectIds = zones.Battlefields
            .Where(objectId => cardObjects.TryGetValue(objectId, out var state)
                && state.Damage > 0
                && state.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (damagedObjectIds.Length == 0)
        {
            return;
        }

        foreach (var objectId in damagedObjectIds)
        {
            cardObjects[objectId] = cardObjects[objectId] with { Damage = 0 };
        }

        events.Add(new GameEvent(
            "DAMAGE_REMOVED",
            $"{behavior.DisplayName}移除己方战场单位伤害",
            new Dictionary<string, object?>
            {
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["playerId"] = stackItem.ControllerId,
                ["objectIds"] = damagedObjectIds,
                ["count"] = damagedObjectIds.Length
            }));
    }

    private static bool TryMoveTargetToOwnerBattlefield(
        Dictionary<string, PlayerZones> playerZones,
        string targetObjectId,
        out string ownerPlayerId)
    {
        ownerPlayerId = string.Empty;
        foreach (var (playerId, zones) in playerZones)
        {
            if (!zones.Base.Contains(targetObjectId, StringComparer.Ordinal))
            {
                continue;
            }

            playerZones[playerId] = zones with
            {
                Base = RemoveFromZone(zones.Base, targetObjectId),
                Battlefields = zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal)
                    ? zones.Battlefields
                    : zones.Battlefields.Concat([targetObjectId]).ToArray()
            };
            ownerPlayerId = playerId;
            return true;
        }

        return false;
    }

    private static bool TryMoveTargetToControllerBattlefield(
        Dictionary<string, PlayerZones> playerZones,
        string controllerId,
        string targetObjectId)
    {
        if (!playerZones.TryGetValue(controllerId, out var zones)
            || !zones.Base.Contains(targetObjectId, StringComparer.Ordinal))
        {
            return false;
        }

        playerZones[controllerId] = zones with
        {
            Base = RemoveFromZone(zones.Base, targetObjectId),
            Battlefields = zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal)
                ? zones.Battlefields
                : zones.Battlefields.Concat([targetObjectId]).ToArray()
        };
        return true;
    }

    private static bool TryMoveFirstTargetToSecondTargetLocation(
        Dictionary<string, PlayerZones> playerZones,
        string movedObjectId,
        string destinationObjectId,
        out string destinationPlayerId,
        out string destinationZone)
    {
        destinationPlayerId = string.Empty;
        destinationZone = string.Empty;

        if (string.Equals(movedObjectId, destinationObjectId, StringComparison.Ordinal))
        {
            return false;
        }

        var movedLocation = FindFieldObjectLocation(playerZones, movedObjectId);
        var targetLocation = FindFieldObjectLocation(playerZones, destinationObjectId);
        if (movedLocation is null || targetLocation is null)
        {
            return false;
        }

        if (string.Equals(movedLocation.Value.PlayerId, targetLocation.Value.PlayerId, StringComparison.Ordinal)
            && string.Equals(movedLocation.Value.Zone, targetLocation.Value.Zone, StringComparison.Ordinal))
        {
            return false;
        }

        RemoveFieldObjectFromLocation(playerZones, movedLocation.Value.PlayerId, movedLocation.Value.Zone, movedObjectId);
        AddFieldObjectToLocation(playerZones, targetLocation.Value.PlayerId, targetLocation.Value.Zone, movedObjectId);
        destinationPlayerId = targetLocation.Value.PlayerId;
        destinationZone = targetLocation.Value.Zone;
        return true;
    }

    private static bool TrySwapTargetLocations(
        Dictionary<string, PlayerZones> playerZones,
        string firstObjectId,
        string secondObjectId,
        out string firstDestinationPlayerId,
        out string firstDestinationZone,
        out string secondDestinationPlayerId,
        out string secondDestinationZone)
    {
        firstDestinationPlayerId = string.Empty;
        firstDestinationZone = string.Empty;
        secondDestinationPlayerId = string.Empty;
        secondDestinationZone = string.Empty;

        if (string.Equals(firstObjectId, secondObjectId, StringComparison.Ordinal))
        {
            return false;
        }

        var firstLocation = FindFieldObjectLocation(playerZones, firstObjectId);
        var secondLocation = FindFieldObjectLocation(playerZones, secondObjectId);
        if (firstLocation is null || secondLocation is null)
        {
            return false;
        }

        if (string.Equals(firstLocation.Value.PlayerId, secondLocation.Value.PlayerId, StringComparison.Ordinal)
            && string.Equals(firstLocation.Value.Zone, secondLocation.Value.Zone, StringComparison.Ordinal))
        {
            return false;
        }

        RemoveFieldObjectFromLocation(playerZones, firstLocation.Value.PlayerId, firstLocation.Value.Zone, firstObjectId);
        RemoveFieldObjectFromLocation(playerZones, secondLocation.Value.PlayerId, secondLocation.Value.Zone, secondObjectId);
        AddFieldObjectToLocation(playerZones, secondLocation.Value.PlayerId, secondLocation.Value.Zone, firstObjectId);
        AddFieldObjectToLocation(playerZones, firstLocation.Value.PlayerId, firstLocation.Value.Zone, secondObjectId);

        firstDestinationPlayerId = secondLocation.Value.PlayerId;
        firstDestinationZone = secondLocation.Value.Zone;
        secondDestinationPlayerId = firstLocation.Value.PlayerId;
        secondDestinationZone = firstLocation.Value.Zone;
        return true;
    }

    private static (string PlayerId, string Zone)? FindFieldObjectLocation(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId)
    {
        foreach (var (playerId, zones) in playerZones)
        {
            if (zones.Base.Contains(objectId, StringComparer.Ordinal))
            {
                return (playerId, "BASE");
            }

            if (zones.Battlefields.Contains(objectId, StringComparer.Ordinal))
            {
                return (playerId, "BATTLEFIELD");
            }
        }

        return null;
    }

    private static void RemoveFieldObjectFromLocation(
        Dictionary<string, PlayerZones> playerZones,
        string playerId,
        string zone,
        string objectId)
    {
        var zones = playerZones[playerId];
        playerZones[playerId] = string.Equals(zone, "BASE", StringComparison.Ordinal)
            ? zones with { Base = RemoveFromZone(zones.Base, objectId) }
            : zones with { Battlefields = RemoveFromZone(zones.Battlefields, objectId) };
    }

    private static void AddFieldObjectToLocation(
        Dictionary<string, PlayerZones> playerZones,
        string playerId,
        string zone,
        string objectId)
    {
        var zones = playerZones[playerId];
        playerZones[playerId] = string.Equals(zone, "BASE", StringComparison.Ordinal)
            ? zones with
            {
                Base = zones.Base.Contains(objectId, StringComparer.Ordinal)
                    ? zones.Base
                    : zones.Base.Concat([objectId]).ToArray()
            }
            : zones with
            {
                Battlefields = zones.Battlefields.Contains(objectId, StringComparer.Ordinal)
                    ? zones.Battlefields
                    : zones.Battlefields.Concat([objectId]).ToArray()
            };
    }

    private static bool ShouldDrawForBehavior(
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        IReadOnlyList<string> destroyedObjectIds,
        int drawCount)
    {
        if (drawCount <= 0)
        {
            return false;
        }

        return behavior.DrawConditionKind switch
        {
            CardDrawConditionKinds.None => true,
            CardDrawConditionKinds.TargetDestroyedByThisEffect => destroyedObjectIds.Count > 0,
            CardDrawConditionKinds.PlayedAfterAnotherCardThisTurn => stackItem.PlayedAfterAnotherCardThisTurn,
            _ => false
        };
    }

    private static int ResolveDrawCount(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string controllerId,
        CardBehaviorDefinition behavior)
    {
        return behavior.DynamicDrawCountKind switch
        {
            CardDynamicDrawCountKinds.ControllerPowerfulUnits => CountControlledUnitsWithPowerAtLeast(
                playerZones,
                cardObjects,
                controllerId,
                5),
            _ => behavior.DrawCount
        };
    }

    private static int CountControlledUnitsWithPowerAtLeast(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        int minimumPower)
    {
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return 0;
        }

        return zones.Base
            .Concat(zones.Battlefields)
            .Count(objectId => cardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Power >= minimumPower);
    }

    private static LethalDamageCleanupResult ApplyLethalDamageCleanup(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        StackItemState stackItem,
        IReadOnlySet<string> damageTriggeredDestroyTargetObjectIds)
    {
        var events = new List<GameEvent>();
        var destroyedObjectIds = new List<string>();
        var destroyedUnitOwnerIds = new List<string>();
        var lethalObjectIds = cardObjects
            .Where(entry => entry.Value.Power > 0
                && ((entry.Value.Damage > 0
                        && entry.Value.Damage >= entry.Value.Power)
                    || damageTriggeredDestroyTargetObjectIds.Contains(entry.Key))
                && IsObjectOnField(playerZones, entry.Key))
            .Select(entry => entry.Key)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();

        foreach (var objectId in lethalObjectIds)
        {
            if (!TryDestroyTarget(playerZones, cardObjects, objectId, out var removalResult))
            {
                continue;
            }

            var destroyReason = damageTriggeredDestroyTargetObjectIds.Contains(objectId)
                ? "DAMAGE_TRIGGERED_DESTROY"
                : "LETHAL_DAMAGE";
            events.Add(BuildFieldRemovalEvent(
                destroyReason == "DAMAGE_TRIGGERED_DESTROY" ? "伤害触发效果" : "致命伤害",
                stackItem,
                objectId,
                removalResult,
                destroyReason));
            if (!removalResult.WasDestroyed)
            {
                continue;
            }

            destroyedObjectIds.Add(objectId);
            if (removalResult.WasUnit)
            {
                destroyedUnitOwnerIds.Add(removalResult.OwnerPlayerId);
            }
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

    private static DrawApplicationResult ApplyDrawToPlayer(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, int> playerScores,
        string playerId,
        int drawCount,
        long rngCursor,
        List<GameEvent> events)
    {
        if (drawCount <= 0
            || !playerZones.TryGetValue(playerId, out var drawPlayerZones))
        {
            return new DrawApplicationResult(playerScores, null, rngCursor);
        }

        var drawState = state with
        {
            PlayerScores = playerScores,
            RngCursor = rngCursor
        };
        var drawResult = DrawCards(
            drawState,
            playerId,
            drawPlayerZones,
            drawCount);
        drawPlayerZones = drawPlayerZones with
        {
            MainDeck = drawResult.MainDeck,
            Hand = drawPlayerZones.Hand.Concat(drawResult.DrawnCards).ToArray(),
            Graveyard = drawResult.Graveyard
        };
        playerZones[playerId] = drawPlayerZones;
        events.AddRange(BuildCardDrawEvents(playerId, drawResult));

        return new DrawApplicationResult(
            drawResult.PlayerScores,
            drawResult.WinnerPlayerId,
            drawResult.RngCursor);
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
        CardBehaviorDefinition behavior,
        string? targetObjectId = null)
    {
        if (behavior.DamageAmountFromFirstTargetManaCost
            && stackItem.TargetObjectIds.Count > 0
            && state.CardObjects.TryGetValue(stackItem.TargetObjectIds[0], out var firstTargetState))
        {
            return firstTargetState.ManaCost;
        }

        if (behavior.ConditionalDamageAmount > 0
            && DamageConditionApplies(state, stackItem.ControllerId, behavior.DamageConditionKind, targetObjectId))
        {
            return behavior.ConditionalDamageAmount;
        }

        return stackItem.DamageAmount > 0 ? stackItem.DamageAmount : behavior.DamageAmount;
    }

    private static bool DamageConditionApplies(
        MatchState state,
        string controllerId,
        string conditionKind,
        string? targetObjectId)
    {
        return conditionKind switch
        {
            CardDamageConditionKinds.ControllerHasFaceDownCard => ControllerControlsFaceDownCard(state, controllerId),
            CardDamageConditionKinds.TargetIsAttacking
                => !string.IsNullOrWhiteSpace(targetObjectId)
                    && state.CardObjects.TryGetValue(targetObjectId, out var targetState)
                    && targetState.IsAttacking,
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

    private static string[] ControllerAndOtherPlayerIds(MatchState state, string controllerId)
    {
        return new[] { controllerId }
            .Concat(SeatPlayerIds(state).Where(playerId => !string.Equals(playerId, controllerId, StringComparison.Ordinal)))
            .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] PlayerIdsStartingAfter(MatchState state, string playerId)
    {
        var players = SeatPlayerIds(state);
        if (players.Length == 0)
        {
            return [];
        }

        var playerIndex = Array.IndexOf(players, playerId);
        if (playerIndex < 0)
        {
            return players;
        }

        return players
            .Skip(playerIndex + 1)
            .Concat(players.Take(playerIndex + 1))
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
        var expiredPowerModifierObjectIds = new List<string>();
        var expiredGlobalEffectIds = state.UntilEndOfTurnEffects
            .Where(effectId => !string.IsNullOrWhiteSpace(effectId))
            .ToArray();
        var cardObjects = state.CardObjects.ToDictionary(
            entry => entry.Key,
            entry =>
            {
                var objectState = entry.Value;
                var untilEndEffects = objectState.UntilEndOfTurnEffects
                    .Where(effectId => !string.IsNullOrWhiteSpace(effectId))
                    .ToArray();
                if (objectState.Damage <= 0
                    && untilEndEffects.Length == 0
                    && objectState.UntilEndOfTurnPowerModifier == 0)
                {
                    return objectState;
                }

                if (objectState.Damage > 0)
                {
                    damagedObjectIds.Add(entry.Key);
                }
                expiredEffectIds.AddRange(untilEndEffects);
                if (objectState.UntilEndOfTurnPowerModifier != 0)
                {
                    expiredPowerModifierObjectIds.Add(entry.Key);
                }

                return objectState with
                {
                    Damage = 0,
                    UntilEndOfTurnEffects = [],
                    Power = Math.Max(0, objectState.Power - objectState.UntilEndOfTurnPowerModifier),
                    UntilEndOfTurnPowerModifier = 0
                };
            },
            StringComparer.Ordinal);

        return new CleanupResult(
            cardObjects,
            [],
            damagedObjectIds.OrderBy(objectId => objectId, StringComparer.Ordinal).ToArray(),
            expiredEffectIds
                .Concat(expiredGlobalEffectIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(effectId => effectId, StringComparer.Ordinal)
                .ToArray(),
            expiredPowerModifierObjectIds.OrderBy(objectId => objectId, StringComparer.Ordinal).ToArray(),
            damagedObjectIds.Count > 0
                || expiredEffectIds.Count > 0
                || expiredGlobalEffectIds.Length > 0
                || expiredPowerModifierObjectIds.Count > 0);
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

    private static Dictionary<string, int> GainExperience(
        IReadOnlyDictionary<string, int> currentExperience,
        string playerId,
        int amount,
        StackItemState stackItem,
        List<GameEvent> events)
    {
        var playerExperience = currentExperience.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        playerExperience[playerId] = playerExperience.TryGetValue(playerId, out var current)
            ? current + amount
            : amount;
        events.Add(new GameEvent(
            "EXPERIENCE_GAINED",
            $"{playerId} 获得 {amount} 经验",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["cardNo"] = stackItem.CardNo,
                ["amount"] = amount,
                ["totalExperience"] = playerExperience[playerId]
            }));

        return playerExperience;
    }

    private static Dictionary<string, int> NormalizeExperienceForSeats(MatchState state)
    {
        return state.Seats.Keys.ToDictionary(
            playerId => playerId,
            playerId => state.PlayerExperience.TryGetValue(playerId, out var experience) ? experience : 0,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> PayExperienceCosts(
        MatchState state,
        string playerId,
        int experienceCost)
    {
        if (experienceCost <= 0)
        {
            return state.PlayerExperience;
        }

        var playerExperience = NormalizeExperienceForSeats(state);
        playerExperience[playerId] = Math.Max(
            0,
            playerExperience.TryGetValue(playerId, out var currentExperience)
                ? currentExperience - experienceCost
                : 0);
        return playerExperience;
    }

    private static IReadOnlyDictionary<string, int> IncrementPlayerCardsPlayedThisTurn(
        MatchState state,
        string playerId)
    {
        var playerCardsPlayedThisTurn = state.PlayerCardsPlayedThisTurn.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        playerCardsPlayedThisTurn[playerId] = playerCardsPlayedThisTurn.TryGetValue(playerId, out var count)
            ? count + 1
            : 1;
        return playerCardsPlayedThisTurn;
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

        if (cleanupResult.ExpiredPowerModifierObjectIds.Count > 0)
        {
            events.Add(new GameEvent(
                "POWER_MODIFIER_EXPIRED",
                "期限为本回合内的战力修正失效",
                new Dictionary<string, object?>
                {
                    ["objectIds"] = cleanupResult.ExpiredPowerModifierObjectIds.ToArray(),
                    ["count"] = cleanupResult.ExpiredPowerModifierObjectIds.Count
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
        DrawResult drawResult,
        IReadOnlyList<GameEvent> preRuneCallEvents)
    {
        List<GameEvent> events =
        [
            new GameEvent(
                "TURN_START_BEGAN",
                $"{state.TurnPlayerId} 开始回合开始流程",
                new Dictionary<string, object?>
                {
                    ["turnPlayerId"] = state.TurnPlayerId
                })
        ];

        events.AddRange(preRuneCallEvents);
        events.Add(
            new GameEvent(
                "RUNES_CALLED",
                $"{state.TurnPlayerId} 召出 {calledRuneCount} 张符文",
                new Dictionary<string, object?>
                {
                    ["playerId"] = state.TurnPlayerId,
                    ["count"] = calledRuneCount
                }));

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

    private sealed record EphemeralCleanupResult(
        IReadOnlyList<GameEvent> Events,
        IReadOnlyList<string> DestroyedUnitOwnerIds)
    {
        public static EphemeralCleanupResult Empty { get; } = new([], []);
    }

    private sealed record DrawApplicationResult(
        IReadOnlyDictionary<string, int> PlayerScores,
        string? WinnerPlayerId,
        long RngCursor);

    private sealed record BurnoutResult(
        string ScoredPlayerId,
        int ScoredPlayerScore);

    private sealed record DamageApplicationResult(
        int DamageAmount,
        int OriginalDamageAmount,
        bool Prevented,
        string PreventionEffectId);

    private sealed record PlayCardPlan(
        CardBehaviorDefinition Behavior,
        PlayerZones SourceZones,
        IReadOnlyList<string> TargetObjectIds,
        int TotalManaCost,
        int TotalPowerCost,
        int TotalExperienceCost,
        int EffectRepeatCount,
        IReadOnlyList<string> OptionalCosts,
        int CostReductionMana,
        int OptionalCostManaReduction,
        int SpellshieldTaxMana,
        IReadOnlyList<string> SpellshieldTaxTargetObjectIds,
        IReadOnlyList<string> ExhaustedOptionalCostTargetObjectIds,
        IReadOnlyList<string> DestroyedAdditionalCostTargetObjectIds,
        IReadOnlyList<string> ReturnedAdditionalCostTargetObjectIds,
        IReadOnlyList<string> DiscardedOptionalCostTargetObjectIds);

    private sealed record StackResolutionResult(
        IReadOnlyDictionary<string, PlayerZones> PlayerZones,
        IReadOnlyDictionary<string, CardObjectState> CardObjects,
        IReadOnlyDictionary<string, int> PlayerScores,
        IReadOnlyDictionary<string, int> PlayerExperience,
        IReadOnlyList<string> UntilEndOfTurnEffects,
        string? WinnerPlayerId,
        IReadOnlyList<GameEvent> Events,
        IReadOnlyList<string> DestroyedUnitOwnerIds,
        IReadOnlyList<StackItemState>? StackItems,
        IReadOnlyList<string> CounteredStackItemIds,
        string? ExtraTurnPlayerId,
        long RngCursor);

    private sealed record RecycleResult(
        IReadOnlyList<GameEvent> Events,
        long RngCursor);

    private sealed record RuneCallResult(
        IReadOnlyList<string> CalledRuneObjectIds);

    private sealed record FieldRemovalResult(
        string OwnerPlayerId,
        string DestinationZone,
        bool WasBanished,
        bool WasRecalledToBase,
        bool WasEquipment,
        bool WasUnit)
    {
        public bool WasDestroyed => !WasBanished && !WasRecalledToBase;

        public static FieldRemovalResult Empty { get; } = new(string.Empty, string.Empty, false, false, false, false);
    }

    private sealed record LethalDamageCleanupResult(
        IReadOnlyList<GameEvent> Events,
        IReadOnlyList<string> DestroyedObjectIds,
        IReadOnlyList<string> DestroyedUnitOwnerIds);

    private sealed record CleanupResult(
        IReadOnlyDictionary<string, CardObjectState> CardObjects,
        IReadOnlyList<string> UntilEndOfTurnEffects,
        IReadOnlyList<string> DamagedObjectIds,
        IReadOnlyList<string> ExpiredEffectIds,
        IReadOnlyList<string> ExpiredPowerModifierObjectIds,
        bool RequiresFollowUpCleanup);
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static string? ValidateAuthoritativeStateNullableTextValue(
        string valueLabel,
        string? value,
        List<string> errors)
    {
        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"authoritative state {valueLabel} is blank");
            return null;
        }

        var normalizedValue = value.Trim();
        if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
        {
            errors.Add($"authoritative state {valueLabel} {normalizedValue} has surrounding whitespace");
        }

        return normalizedValue;
    }

    private static void ValidateAuthoritativeStateResourceValues(
        MatchState authoritativeState,
        List<string> errors)
    {
        ValidateAuthoritativeStateRunePoolValues(authoritativeState.RunePools, errors);
        ValidateAuthoritativeStateIntegerMapValues(
            "score",
            authoritativeState.PlayerScores,
            requirePositive: false,
            errors);
        ValidateAuthoritativeStateIntegerMapValues(
            "experience",
            authoritativeState.PlayerExperience,
            requirePositive: false,
            errors);
        ValidateAuthoritativeStateIntegerMapValues(
            "cards played this turn",
            authoritativeState.PlayerCardsPlayedThisTurn,
            requirePositive: true,
            errors);
    }

    private static void ValidateAuthoritativeStateRunePoolValues(
        IReadOnlyDictionary<string, RunePool>? runePools,
        List<string> errors)
    {
        if (runePools is null)
        {
            return;
        }

        foreach (var (playerId, runePool) in runePools.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                continue;
            }

            var playerLabel = playerId.Trim();
            if (runePool is null)
            {
                errors.Add($"authoritative state rune pool for {playerLabel} is required");
                continue;
            }

            if (!runePool.HasSupportedAmounts)
                errors.Add($"authoritative state rune pool for {playerLabel} contains unsupported resource amounts");
            if (runePool.SpellOnlyMana < 0)
                errors.Add($"authoritative state rune pool for {playerLabel} spell-only mana cannot be negative");
            if (runePool.DuelOnlyMana < 0)
                errors.Add($"authoritative state rune pool for {playerLabel} duel-only mana cannot be negative");

            if (runePool.Mana < 0)
            {
                errors.Add(
                    $"authoritative state rune pool for {playerLabel} mana {runePool.Mana} cannot be negative");
            }

            if (runePool.Power < 0)
            {
                errors.Add(
                    $"authoritative state rune pool for {playerLabel} power {runePool.Power} cannot be negative");
            }

            ValidateAuthoritativeStateTraitPowerValues(
                $"rune pool for {playerLabel}",
                "power trait",
                runePool.PowerByTrait,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateIntegerMapValues(
        string valueLabel,
        IReadOnlyDictionary<string, int>? values,
        bool requirePositive,
        List<string> errors)
    {
        if (values is null)
        {
            return;
        }

        foreach (var (playerId, value) in values.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                continue;
            }

            var playerLabel = playerId.Trim();
            if (requirePositive)
            {
                if (value <= 0)
                {
                    errors.Add(
                        $"authoritative state {valueLabel} for {playerLabel} {value} must be positive");
                }

                continue;
            }

            if (value < 0)
            {
                errors.Add(
                    $"authoritative state {valueLabel} for {playerLabel} {value} cannot be negative");
            }
        }
    }

    private static void ValidateAuthoritativeStateTraitPowerValues(
        string ownerLabel,
        string traitLabel,
        IReadOnlyDictionary<string, int>? values,
        List<string> errors)
    {
        if (values is null)
        {
            errors.Add($"authoritative state {ownerLabel} {traitLabel} map is required");
            return;
        }

        var seenTraits = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (trait, value) in values.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(trait))
            {
                errors.Add($"authoritative state {ownerLabel} {traitLabel} is required");
                continue;
            }

            var normalizedTrait = RuneTrait.Normalize(trait);
            var trimmedTrait = trait.Trim();
            if (!string.Equals(trait, trimmedTrait, StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state {ownerLabel} {traitLabel} {normalizedTrait} has surrounding whitespace");
            }

            RejectAuthoritativeStateRedactionSentinel(
                $"{ownerLabel} {traitLabel}",
                trimmedTrait,
                errors);

            if (value <= 0)
            {
                errors.Add(
                    $"authoritative state {ownerLabel} {traitLabel} {normalizedTrait} value {value} must be positive");
            }

            if (!seenTraits.Add(normalizedTrait))
            {
                errors.Add($"authoritative state {ownerLabel} {traitLabel} {normalizedTrait} is duplicated");
            }
        }
    }

    private static void ValidateAuthoritativeStatePaymentKindValues(
        string resourceLabel,
        IReadOnlyList<string>? allowedPaymentKinds,
        List<string> errors)
    {
        if (allowedPaymentKinds is null)
        {
            errors.Add(
                $"authoritative state temporary payment resource {resourceLabel} allowed payment kind list is required");
            return;
        }

        var seenPaymentKinds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var paymentKind in allowedPaymentKinds)
        {
            if (string.IsNullOrWhiteSpace(paymentKind))
            {
                errors.Add(
                    $"authoritative state temporary payment resource {resourceLabel} allowed payment kind is required");
                continue;
            }

            var normalizedPaymentKind = paymentKind.Trim();
            if (!string.Equals(paymentKind, normalizedPaymentKind, StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state temporary payment resource {resourceLabel} allowed payment kind {normalizedPaymentKind} has surrounding whitespace");
            }

            RejectAuthoritativeStateRedactionSentinel(
                $"temporary payment resource {resourceLabel} allowed payment kind",
                normalizedPaymentKind,
                errors);

            if (!seenPaymentKinds.Add(normalizedPaymentKind))
            {
                errors.Add(
                    $"authoritative state temporary payment resource {resourceLabel} allowed payment kind {normalizedPaymentKind} is duplicated");
            }
        }
    }

    private static void ValidateAuthoritativeStateStackAndTriggerValues(
        MatchState authoritativeState,
        List<string> errors)
    {
        if (authoritativeState.DuelChainFocus is { } suspended)
        {
            if (string.IsNullOrWhiteSpace(suspended.FocusPlayerId)
                || !authoritativeState.Seats.ContainsKey(suspended.FocusPlayerId)
                || suspended.PassedFocusPlayerIds is null
                || suspended.PassedFocusPlayerIds.Count >= authoritativeState.Seats.Count
                || suspended.PassedFocusPlayerIds.Distinct(StringComparer.Ordinal).Count() != suspended.PassedFocusPlayerIds.Count
                || suspended.PassedFocusPlayerIds.Any(id => id == suspended.FocusPlayerId || !authoritativeState.Seats.ContainsKey(id))
                || authoritativeState.Status != MatchStatuses.InProgress || authoritativeState.Phase != MatchPhases.Main
                || authoritativeState.TimingState != TimingStates.SpellDuelClosed || authoritativeState.FocusPlayerId is not null
                || authoritativeState.StackItems.Count == 0 && authoritativeState.TriggerQueue.Count == 0
                    && authoritativeState.PendingHandChoice is null && authoritativeState.PendingCardChoice is null
                    && authoritativeState.PendingPayment is null && authoritativeState.PendingEffectPlay is null
                    && authoritativeState.PendingRuleChoice is null && authoritativeState.ObservedDeckPlays is not { Count: > 0 })
                errors.Add("invalid duel chain focus continuation");
        }
        var knownObjectIds = BuildAuthoritativeStateKnownObjectIds(authoritativeState);
        var objectCardNos = BuildAuthoritativeStateObjectCardNoIndex(authoritativeState);
        var objectPowers = BuildAuthoritativeStateObjectPowerIndex(authoritativeState);
        var objectControllers = BuildAuthoritativeStateObjectControllerIndex(authoritativeState);
        var objectFaceDowns = BuildAuthoritativeStateObjectFaceDownIndex(authoritativeState);
        var objectTags = BuildAuthoritativeStateObjectTagIndex(authoritativeState);
        var battlefieldStateObjectIds = BuildAuthoritativeStateBattlefieldStateObjectIds(authoritativeState);
        var playerBattlefieldObjectIdsByPlayer =
            BuildAuthoritativeStatePlayerBattlefieldObjectIdsByPlayer(authoritativeState.PlayerZones);
        var playerBaseObjectIdsByPlayer =
            BuildAuthoritativeStatePlayerBaseObjectIdsByPlayer(authoritativeState.PlayerZones);
        var playerGraveyardObjectIdsByPlayer =
            BuildAuthoritativeStatePlayerGraveyardObjectIdsByPlayer(authoritativeState.PlayerZones);
        var playerFieldObjectIdsByPlayer =
            BuildAuthoritativeStatePlayerFieldObjectIdsByPlayer(authoritativeState.PlayerZones);
        ValidateAuthoritativeStateStackItemValues(authoritativeState.StackItems, errors);
        ValidateTargetedPowerAbilityStackValues(authoritativeState, errors);
        foreach (var item in authoritativeState.StackItems.Where(i => i is not null))
            if (!CoreRuleEngine.ValidSimpleActivation(authoritativeState, item)) errors.Add($"stack item {item.StackItemId} printed activation context is invalid");
        ValidateAuthoritativeStateTriggerQueueValues(
            authoritativeState.TriggerQueue,
            knownObjectIds,
            objectCardNos,
            objectPowers,
            objectControllers,
            objectFaceDowns,
            objectTags,
            authoritativeState.ObjectLocations,
            playerBattlefieldObjectIdsByPlayer,
            playerBaseObjectIdsByPlayer,
            playerGraveyardObjectIdsByPlayer,
            playerFieldObjectIdsByPlayer,
            battlefieldStateObjectIds,
            authoritativeState.Tick,
            authoritativeState.TurnNumber,
            errors,
            authoritativeState);
    }

    private static void ValidateAuthoritativeStatePendingPaymentValues(
        PendingPaymentState? pendingPayment,
        List<string> errors)
    {
        if (pendingPayment is null)
        {
            return;
        }

        var paymentId = ValidateAuthoritativeStateRequiredText(
            "pending payment id",
            pendingPayment.PaymentId,
            errors);
        var paymentLabel = paymentId ?? "<unknown>";
        RejectAuthoritativeStateRedactionSentinel("pending payment id", paymentId, errors);

        var paymentWindow = ValidateAuthoritativeStateRequiredText(
            $"pending payment {paymentLabel} window",
            pendingPayment.PaymentWindow,
            errors);
        RejectAuthoritativeStateRedactionSentinel(
            $"pending payment {paymentLabel} window",
            paymentWindow,
            errors);

        var reason = ValidateAuthoritativeStateOptionalTextValue(
            $"pending payment {paymentLabel} reason",
            pendingPayment.Reason,
            errors);
        RejectAuthoritativeStateRedactionSentinel(
            $"pending payment {paymentLabel} reason",
            reason,
            errors);

        if (pendingPayment.ManaCost < 0)
        {
            errors.Add(
                $"authoritative state pending payment {paymentLabel} mana cost {pendingPayment.ManaCost} cannot be negative");
        }

        if (pendingPayment.PowerCost < 0)
        {
            errors.Add(
                $"authoritative state pending payment {paymentLabel} power cost {pendingPayment.PowerCost} cannot be negative");
        }

        ValidateAuthoritativeStateTraitPowerValues(
            $"pending payment {paymentLabel}",
            "power cost trait",
            pendingPayment.PowerCostByTrait,
            errors);
        ValidateAuthoritativeStateStringListValues(
            $"pending payment {paymentLabel} legal payment choice",
            pendingPayment.LegalPaymentChoiceIds,
            errors,
            rejectDuplicates: true,
            rejectRedactionSentinel: true);
        ValidateAuthoritativeStateStringListValues(
            $"pending payment {paymentLabel} payment resource action",
            pendingPayment.PaymentResourceActionIds,
            errors,
            rejectDuplicates: true,
            rejectRedactionSentinel: true);
    }

    private static void ValidateAuthoritativeStatePendingHandChoiceValues(
        PendingHandChoiceState? pendingHandChoice,
        List<string> errors)
    {
        if (pendingHandChoice is null)
        {
            return;
        }

        var choiceId = ValidateAuthoritativeStateRequiredText(
            "pending hand choice id",
            pendingHandChoice.ChoiceId,
            errors);
        var choiceLabel = choiceId ?? "<unknown>";
        RejectAuthoritativeStateRedactionSentinel("pending hand choice id", choiceId, errors);

        var choiceWindow = ValidateAuthoritativeStateRequiredText(
            $"pending hand choice {choiceLabel} window",
            pendingHandChoice.ChoiceWindow,
            errors);
        RejectAuthoritativeStateRedactionSentinel(
            $"pending hand choice {choiceLabel} window",
            choiceWindow,
            errors);

        var reason = ValidateAuthoritativeStateOptionalTextValue(
            $"pending hand choice {choiceLabel} reason",
            pendingHandChoice.Reason,
            errors);
        RejectAuthoritativeStateRedactionSentinel(
            $"pending hand choice {choiceLabel} reason",
            reason,
            errors);

        var sourceObjectId = ValidateAuthoritativeStateOptionalTextValue(
            $"pending hand choice {choiceLabel} source object",
            pendingHandChoice.SourceObjectId,
            errors);
        RejectAuthoritativeStateRedactionSentinel(
            $"pending hand choice {choiceLabel} source object",
            sourceObjectId,
            errors);

        var effectKind = ValidateAuthoritativeStateOptionalTextValue(
            $"pending hand choice {choiceLabel} effect kind",
            pendingHandChoice.EffectKind,
            errors);
        RejectAuthoritativeStateRedactionSentinel(
            $"pending hand choice {choiceLabel} effect kind",
            effectKind,
            errors);

        if (pendingHandChoice.RequiredCount < 1)
        {
            errors.Add(
                $"authoritative state pending hand choice {choiceLabel} required count {pendingHandChoice.RequiredCount} is invalid");
        }

        if (pendingHandChoice.MaxCount < pendingHandChoice.RequiredCount)
        {
            errors.Add(
                $"authoritative state pending hand choice {choiceLabel} max count {pendingHandChoice.MaxCount} is less than required count {pendingHandChoice.RequiredCount}");
        }

        ValidateAuthoritativeStateStringListValues(
            $"pending hand choice {choiceLabel} legal object",
            pendingHandChoice.LegalObjectIds,
            errors,
            rejectDuplicates: true,
            rejectRedactionSentinel: true);

        if (pendingHandChoice.LegalObjectIds is not null && pendingHandChoice.RequiredCount > 0)
        {
            var distinctLegalObjectCount = pendingHandChoice.LegalObjectIds
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (distinctLegalObjectCount < pendingHandChoice.RequiredCount)
            {
                errors.Add(
                    $"authoritative state pending hand choice {choiceLabel} legal object count {distinctLegalObjectCount} is less than required count {pendingHandChoice.RequiredCount}");
            }
        }
    }

    private static void ValidateAuthoritativeStatePendingCardChoiceValues(
        PendingCardChoiceState? pendingCardChoice,
        List<string> errors)
    {
        if (pendingCardChoice is null)
        {
            return;
        }

        var choiceId = ValidateAuthoritativeStateRequiredText(
            "pending card choice id",
            pendingCardChoice.ChoiceId,
            errors);
        var choiceLabel = choiceId ?? "<unknown>";
        RejectAuthoritativeStateRedactionSentinel("pending card choice id", choiceId, errors);

        var choiceWindow = ValidateAuthoritativeStateRequiredText(
            $"pending card choice {choiceLabel} window",
            pendingCardChoice.ChoiceWindow,
            errors);
        RejectAuthoritativeStateRedactionSentinel(
            $"pending card choice {choiceLabel} window",
            choiceWindow,
            errors);

        var reason = ValidateAuthoritativeStateOptionalTextValue(
            $"pending card choice {choiceLabel} reason",
            pendingCardChoice.Reason,
            errors);
        RejectAuthoritativeStateRedactionSentinel(
            $"pending card choice {choiceLabel} reason",
            reason,
            errors);

        var sourceObjectId = ValidateAuthoritativeStateOptionalTextValue(
            $"pending card choice {choiceLabel} source object",
            pendingCardChoice.SourceObjectId,
            errors);
        RejectAuthoritativeStateRedactionSentinel(
            $"pending card choice {choiceLabel} source object",
            sourceObjectId,
            errors);

        var effectKind = ValidateAuthoritativeStateOptionalTextValue(
            $"pending card choice {choiceLabel} effect kind",
            pendingCardChoice.EffectKind,
            errors);
        RejectAuthoritativeStateRedactionSentinel(
            $"pending card choice {choiceLabel} effect kind",
            effectKind,
            errors);

        if (pendingCardChoice.RequiredCount < 0)
        {
            errors.Add(
                $"authoritative state pending card choice {choiceLabel} required count {pendingCardChoice.RequiredCount} is invalid");
        }

        if (pendingCardChoice.MaxCount < pendingCardChoice.RequiredCount)
        {
            errors.Add(
                $"authoritative state pending card choice {choiceLabel} max count {pendingCardChoice.MaxCount} is less than required count {pendingCardChoice.RequiredCount}");
        }

        ValidateAuthoritativeStateStringListValues(
            $"pending card choice {choiceLabel} legal object",
            pendingCardChoice.LegalObjectIds,
            errors,
            rejectDuplicates: true,
            rejectRedactionSentinel: true);
        ValidateAuthoritativeStateStringListValues(
            $"pending card choice {choiceLabel} context object",
            pendingCardChoice.ContextObjectIds,
            errors,
            rejectDuplicates: true,
            rejectRedactionSentinel: true);

        var distinctLegalObjectCount = pendingCardChoice.LegalObjectIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (distinctLegalObjectCount < pendingCardChoice.RequiredCount)
        {
            errors.Add(
                $"authoritative state pending card choice {choiceLabel} legal object count {distinctLegalObjectCount} is less than required count {pendingCardChoice.RequiredCount}");
        }

        if (pendingCardChoice.ContextObjectIds.Count == 0 && !CoreRuleEngine.AllowsEmptyContextCardChoice(pendingCardChoice))
        {
            errors.Add($"authoritative state pending card choice {choiceLabel} context object count is invalid");
        }
    }

    private static void ValidateAuthoritativeStateTriggerQueueValues(
        IReadOnlyList<TriggerQueueItemState>? triggerQueue,
        IReadOnlySet<string> knownObjectIds,
        IReadOnlyDictionary<string, string?> objectCardNos,
        IReadOnlyDictionary<string, int> objectPowers,
        IReadOnlyDictionary<string, string> objectControllers,
        IReadOnlyDictionary<string, bool> objectFaceDowns,
        IReadOnlyDictionary<string, IReadOnlySet<string>> objectTags,
        IReadOnlyDictionary<string, ObjectLocationState> objectLocations,
        IReadOnlyDictionary<string, IReadOnlySet<string>> playerBattlefieldObjectIdsByPlayer,
        IReadOnlyDictionary<string, IReadOnlySet<string>> playerBaseObjectIdsByPlayer,
        IReadOnlyDictionary<string, IReadOnlySet<string>> playerGraveyardObjectIdsByPlayer,
        IReadOnlyDictionary<string, IReadOnlySet<string>> playerFieldObjectIdsByPlayer,
        IReadOnlySet<string> battlefieldStateObjectIds,
        long currentTick,
        int currentTurnNumber,
        List<string> errors,
        MatchState authoritativeState)
    {
        if (triggerQueue is null)
        {
            return;
        }

        var seenTriggerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var trigger in triggerQueue.OrderBy(item => item?.TriggerId ?? string.Empty, StringComparer.Ordinal))
        {
            if (trigger is null)
            {
                continue;
            }

            var triggerId = ValidateAuthoritativeStateRequiredText(
                "trigger queue item id",
                trigger.TriggerId,
                errors);
            var triggerLabel = triggerId ?? "<unknown>";
            if (string.Equals(triggerId, "HIDDEN", StringComparison.Ordinal))
            {
                errors.Add("authoritative state trigger queue item id must not be redacted");
            }

            if (triggerId is not null && !seenTriggerIds.Add(triggerId))
            {
                errors.Add($"authoritative state trigger queue item {triggerId} is duplicated");
            }

            if (string.Equals(trigger.ControllerId, "HIDDEN", StringComparison.Ordinal))
            {
                errors.Add($"authoritative state trigger queue item {triggerLabel} controller player must not be redacted");
            }

            var effectKind = ValidateAuthoritativeStateRequiredText(
                $"trigger queue item {triggerLabel} effect kind",
                trigger.EffectKind,
                errors);
            if (string.Equals(effectKind, "HIDDEN", StringComparison.Ordinal))
            {
                errors.Add($"authoritative state trigger queue item {triggerLabel} effect kind must not be redacted");
            }

            var triggeredEventKind = ValidateAuthoritativeStateRequiredText(
                $"trigger queue item {triggerLabel} triggered event kind",
                trigger.TriggeredByEventKind,
                errors);
            if (string.Equals(triggeredEventKind, "HIDDEN", StringComparison.Ordinal))
            {
                errors.Add($"authoritative state trigger queue item {triggerLabel} triggered event kind must not be redacted");
            }

            ValidateTriggerQueueTriggeredEventKindKnownValue(
                $"authoritative state trigger queue item {triggerLabel}",
                triggeredEventKind,
                errors);

            var sourceObjectId = ValidateAuthoritativeStateOptionalTextValue(
                $"trigger queue item {triggerLabel} source object",
                trigger.SourceObjectId,
                errors);
            if (sourceObjectId is not null && sourceObjectId.Length == 0)
            {
                errors.Add($"authoritative state trigger queue item {triggerLabel} source object is required");
            }
            else if (string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal))
            {
                errors.Add($"authoritative state trigger queue item {triggerLabel} source object must not be redacted");
            }

            ValidateTriggerQueueBlueSentinelDelayedResourceContext(
                $"authoritative state trigger queue item {triggerLabel}",
                triggerId,
                trigger.ControllerId,
                sourceObjectId,
                sourceVisibility: null,
                objectControllers,
                "authoritative state object registry",
                objectCardNos,
                "authoritative state object registry",
                objectFaceDowns,
                "authoritative state object registry",
                objectTags,
                "authoritative state object registry",
                objectLocations,
                "authoritative state object locations",
                playerBattlefieldObjectIdsByPlayer,
                "authoritative state player zones",
                battlefieldStateObjectIds,
                "authoritative state battlefield states",
                currentTurnNumber,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueJhinMovementResourceContext(
                $"authoritative state trigger queue item {triggerLabel}",
                triggerId,
                trigger.ControllerId,
                sourceObjectId,
                objectControllers,
                "authoritative state object registry",
                objectCardNos,
                "authoritative state object registry",
                objectFaceDowns,
                "authoritative state object registry",
                objectTags,
                "authoritative state object registry",
                objectLocations,
                "authoritative state object locations",
                battlefieldStateObjectIds,
                "authoritative state battlefield states",
                effectKind,
                triggeredEventKind,
                currentTick,
                errors);
            ValidateTriggerQueueKogmawLastBreathContext(
                $"authoritative state trigger queue item {triggerLabel}",
                triggerId,
                trigger.ControllerId,
                sourceObjectId,
                sourceVisibility: null,
                objectControllers,
                "authoritative state object registry",
                knownObjectIds,
                "object registry",
                objectCardNos,
                "authoritative state object registry",
                objectFaceDowns,
                "authoritative state object registry",
                objectTags,
                "authoritative state object registry",
                objectLocations,
                "authoritative state object locations",
                battlefieldStateObjectIds,
                "authoritative state battlefield states",
                effectKind,
                triggeredEventKind,
                errors, authoritativeState);
            ValidateTriggerQueueOgsLuxHighCostSpellContext(
                $"authoritative state trigger queue item {triggerLabel}",
                triggerId,
                trigger.ControllerId,
                sourceObjectId,
                sourceVisibility: null,
                objectControllers,
                "authoritative state object registry",
                objectCardNos,
                "authoritative state object registry",
                objectFaceDowns,
                "authoritative state object registry",
                objectTags,
                "authoritative state object registry",
                playerFieldObjectIdsByPlayer,
                "authoritative state player zones",
                objectLocations,
                "authoritative state object locations",
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueTeemoOnPlaySelfPowerContext(
                $"authoritative state trigger queue item {triggerLabel}",
                triggerId,
                trigger.ControllerId,
                sourceObjectId,
                sourceVisibility: null,
                objectControllers,
                "authoritative state object registry",
                objectCardNos,
                "authoritative state object registry",
                objectFaceDowns,
                "authoritative state object registry",
                objectTags,
                "authoritative state object registry",
                playerBaseObjectIdsByPlayer,
                "authoritative state player zones",
                objectLocations,
                "authoritative state object locations",
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueWatchfulSentinelLastBreathDrawContext(
                $"authoritative state trigger queue item {triggerLabel}",
                triggerId,
                sourceVisibility: null,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueUnsungHeroLastBreathPowerfulDrawContext(
                $"authoritative state trigger queue item {triggerLabel}",
                triggerId,
                sourceVisibility: null,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueScoutingWarhawkLastBreathCallRuneContext(
                $"authoritative state trigger queue item {triggerLabel}",
                triggerId,
                sourceVisibility: null,
                effectKind,
                triggeredEventKind,
                errors,
            (HasValidatedPublicLastBreathRuneCallEffect(authoritativeState, triggerId, effectKind) || (HasValidatedPublicLastBreathDrawEffect(authoritativeState, triggerId, effectKind) || HasValidatedPublicLastBreathBattlefieldDamageEffect(authoritativeState, triggerId, effectKind))));
            ValidateTriggerQueueSadPoroLastBreathDrawContext(
                $"authoritative state trigger queue item {triggerLabel}",
                triggerId,
                sourceVisibility: null,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueLoyalPoroLastBreathDrawContext(
                $"authoritative state trigger queue item {triggerLabel}",
                triggerId,
                sourceVisibility: null,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueHonestBrokerLastBreathCreateGoldContext(
                $"authoritative state trigger queue item {triggerLabel}",
                triggerId,
                sourceVisibility: null,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueMechanicalTricksterLastBreathCreateMinionsContext(
                $"authoritative state trigger queue item {triggerLabel}",
                triggerId,
                sourceVisibility: null,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueUndercoverAgentLastBreathContext(
                $"authoritative state trigger queue item {triggerLabel}",
                triggerId,
                sourceVisibility: null,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueIroncladVanguardLastBreathCreateRobotsContext(
                $"authoritative state trigger queue item {triggerLabel}",
                triggerId,
                sourceVisibility: null,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueMuddyDredgerLastBreathCreateWarhawkContext(
                $"authoritative state trigger queue item {triggerLabel}",
                triggerId,
                sourceVisibility: null,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueStandardLastBreathSourceObjectIdContext(
                $"authoritative state trigger queue item {triggerLabel}",
                triggerId,
                trigger.ControllerId,
                sourceObjectId,
                effectKind,
                objectControllers,
                "authoritative state object registry",
                objectCardNos,
                "authoritative state object registry",
                objectPowers,
                "authoritative state object registry",
                objectFaceDowns,
                "authoritative state object registry",
                objectTags,
                "authoritative state object registry",
                objectLocations,
                "authoritative state object locations",
                playerBaseObjectIdsByPlayer,
                "authoritative state player zones",
                playerGraveyardObjectIdsByPlayer,
                "authoritative state player zones",
                errors,
                (HasValidatedPublicLastBreathUnitCreationEffect(authoritativeState, triggerId, effectKind) || (HasValidatedPublicLastBreathRuneCallEffect(authoritativeState, triggerId, effectKind) || (HasValidatedPublicLastBreathDrawEffect(authoritativeState, triggerId, effectKind) || HasValidatedPublicLastBreathBattlefieldDamageEffect(authoritativeState, triggerId, effectKind)))));
        }
    }

    private static void ValidateAuthoritativeStateStringListValues(
        string itemLabel,
        IReadOnlyList<string>? values,
        List<string> errors,
        bool rejectDuplicates = false,
        bool rejectRedactionSentinel = false)
    {
        if (values is null)
        {
            errors.Add($"authoritative state {itemLabel} list is required");
            return;
        }

        var seenValues = rejectDuplicates
            ? new HashSet<string>(StringComparer.Ordinal)
            : null;
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"authoritative state {itemLabel} is required");
                continue;
            }

            var normalizedValue = value.Trim();
            if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
            {
                errors.Add($"authoritative state {itemLabel} {normalizedValue} has surrounding whitespace");
            }

            if (rejectRedactionSentinel
                && string.Equals(normalizedValue, "HIDDEN", StringComparison.Ordinal))
            {
                errors.Add($"authoritative state {itemLabel} must not be redacted");
            }

            if (seenValues is not null && !seenValues.Add(normalizedValue))
            {
                errors.Add($"authoritative state {itemLabel} {normalizedValue} is duplicated");
            }
        }
    }

    private static void ValidateAuthoritativeStateResolutionHistory(
        MatchState authoritativeState,
        List<string> errors)
    {
        ValidateAuthoritativeStateBattlefieldResolutionMetadata(
            authoritativeState.BattlefieldResolutions,
            authoritativeState.Tick,
            errors);
        ValidateAuthoritativeStateBattleResolutionMetadata(
            authoritativeState.BattleResolutions,
            authoritativeState.Tick,
            errors);
    }

    private static void ValidateAuthoritativeStateBattlefieldResolutionMetadata(
        IReadOnlyList<BattlefieldResolutionState>? battlefieldResolutions,
        long authoritativeTick,
        List<string> errors)
    {
        if (battlefieldResolutions is null)
        {
            errors.Add("authoritative state battlefield resolutions list is required");
            return;
        }

        ValidateRetainedResolutionHistoryListCount(
            battlefieldResolutions.Count,
            "authoritative state battlefield resolutions",
            errors);
        ValidateRetainedResolutionHistoryStateTickOrder(
            battlefieldResolutions,
            resolution => resolution?.ResolutionId,
            resolution => resolution?.Tick,
            authoritativeTick,
            "authoritative state battlefield resolutions",
            "battlefield resolution",
            errors);
        var seenResolutionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resolution in battlefieldResolutions.OrderBy(
            item => item?.ResolutionId ?? string.Empty,
            StringComparer.Ordinal))
        {
            if (resolution is null)
            {
                errors.Add("authoritative state battlefield resolution is required");
                continue;
            }

            var resolutionId = ValidateAuthoritativeStateResolutionId(
                "battlefield resolution",
                resolution.ResolutionId,
                seenResolutionIds,
                errors);
            var diagnosticResolutionId = string.IsNullOrEmpty(resolutionId) ? "<missing>" : resolutionId;
            ValidateAuthoritativeStateResolutionTick(
                "battlefield resolution",
                resolutionId,
                resolution.Tick,
                authoritativeTick,
                errors);
            var kind = ValidateAuthoritativeStateResolutionText(
                "battlefield resolution",
                resolutionId,
                "kind",
                resolution.Kind,
                errors,
                IsKnownBattlefieldResolutionKind);
            var reason = ValidateAuthoritativeStateResolutionText(
                "battlefield resolution",
                resolutionId,
                "reason",
                resolution.Reason,
                errors,
                IsKnownBattlefieldResolutionReason);
            ValidateBattlefieldResolutionKindReasonCompatibility(
                kind,
                reason,
                $"authoritative state battlefield resolution {diagnosticResolutionId}",
                errors);
            ValidateBattlefieldResolutionCombatPlayerAvailability(
                kind,
                resolution.PlayerId,
                $"authoritative state battlefield resolution {diagnosticResolutionId}",
                "player",
                errors);
            ValidateBattlefieldResolutionControlControllerCompatibility(
                kind,
                reason,
                resolution.PlayerId,
                resolution.ControllerId,
                $"authoritative state battlefield resolution {diagnosticResolutionId}",
                "player",
                "controller",
                errors);
            ValidateBattlefieldResolutionControlPreviousControllerCompatibility(
                kind,
                reason,
                resolution.PreviousControllerId,
                resolution.ControllerId,
                $"authoritative state battlefield resolution {diagnosticResolutionId}",
                "previous controller",
                "controller",
                errors);
            ValidateBattlefieldResolutionCombatControllerAbsence(
                kind,
                resolution.PreviousControllerId,
                resolution.ControllerId,
                $"authoritative state battlefield resolution {diagnosticResolutionId}",
                "previous controller",
                "controller",
                errors);
            ValidateAuthoritativeStateResolutionText(
                "battlefield resolution",
                resolutionId,
                "battlefield object",
                resolution.BattlefieldObjectId,
                errors);
            var sourceObjectId = ValidateAuthoritativeStateNullableTextValue(
                $"battlefield resolution {diagnosticResolutionId} source object",
                resolution.SourceObjectId,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"battlefield resolution {diagnosticResolutionId} source object",
                sourceObjectId,
                errors);
            var participantObjectIds = ValidateAuthoritativeStateResolutionTextList(
                "battlefield resolution",
                resolutionId,
                "participant object",
                resolution.ParticipantObjectIds,
                errors,
                requireList: false);
            ValidateBattlefieldResolutionCombatParticipantAvailability(
                kind,
                sourceObjectId,
                participantObjectIds,
                $"authoritative state battlefield resolution {diagnosticResolutionId}",
                "source object",
                "participant object",
                errors);
            ValidateBattlefieldResolutionControlParticipantAbsence(
                kind,
                sourceObjectId,
                participantObjectIds,
                $"authoritative state battlefield resolution {diagnosticResolutionId}",
                "source object",
                "participant object",
                errors);
            var relatedEventKinds = ValidateAuthoritativeStateResolutionTextList(
                "battlefield resolution",
                resolutionId,
                "related event kind",
                resolution.RelatedEventKinds,
                errors,
                isKnownValue: IsKnownBattlefieldResolutionRelatedEventKind);
            ValidateBattlefieldResolutionRelatedEventKindCompatibility(
                kind,
                relatedEventKinds,
                $"authoritative state battlefield resolution {diagnosticResolutionId}",
                errors);
        }
    }

    private static void ValidateAuthoritativeStateBattleResolutionMetadata(
        IReadOnlyList<BattleResolutionState>? battleResolutions,
        long authoritativeTick,
        List<string> errors)
    {
        if (battleResolutions is null)
        {
            errors.Add("authoritative state battle resolutions list is required");
            return;
        }

        ValidateRetainedResolutionHistoryListCount(
            battleResolutions.Count,
            "authoritative state battle resolutions",
            errors);
        ValidateRetainedResolutionHistoryStateTickOrder(
            battleResolutions,
            resolution => resolution?.ResolutionId,
            resolution => resolution?.Tick,
            authoritativeTick,
            "authoritative state battle resolutions",
            "battle resolution",
            errors);
        var seenResolutionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resolution in battleResolutions.OrderBy(
            item => item?.ResolutionId ?? string.Empty,
            StringComparer.Ordinal))
        {
            if (resolution is null)
            {
                errors.Add("authoritative state battle resolution is required");
                continue;
            }

            var resolutionId = ValidateAuthoritativeStateResolutionId(
                "battle resolution",
                resolution.ResolutionId,
                seenResolutionIds,
                errors);
            ValidateAuthoritativeStateResolutionTick(
                "battle resolution",
                resolutionId,
                resolution.Tick,
                authoritativeTick,
                errors);
            var kind = ValidateAuthoritativeStateResolutionText(
                "battle resolution",
                resolutionId,
                "kind",
                resolution.Kind,
                errors,
                IsKnownBattleResolutionKind);
            var reason = ValidateAuthoritativeStateResolutionText(
                "battle resolution",
                resolutionId,
                "reason",
                resolution.Reason,
                errors,
                IsKnownBattleResolutionReason);
            var diagnosticResolutionLabel =
                $"authoritative state battle resolution {(string.IsNullOrEmpty(resolutionId) ? "<missing>" : resolutionId)}";
            ValidateBattleResolutionKindReasonCompatibility(
                kind,
                reason,
                diagnosticResolutionLabel,
                errors);
            ValidateBattleResolutionPlayerRoleCompatibility(
                kind,
                resolution.AttackingPlayerId,
                resolution.DefendingPlayerId,
                resolution.WinnerPlayerId,
                diagnosticResolutionLabel,
                errors);
            ValidateAuthoritativeStateResolutionText(
                "battle resolution",
                resolutionId,
                "battlefield object",
                resolution.BattlefieldId,
                errors);
            var attackerObjectIds = ValidateAuthoritativeStateResolutionTextList(
                "battle resolution",
                resolutionId,
                "attacker object",
                resolution.AttackerObjectIds,
                errors,
                requireList: false);
            ValidateBattleResolutionParticipantObjectListCount(
                attackerObjectIds,
                diagnosticResolutionLabel,
                "attacker object",
                errors);
            var defenderObjectIds = ValidateAuthoritativeStateResolutionTextList(
                "battle resolution",
                resolutionId,
                "defender object",
                resolution.DefenderObjectIds,
                errors,
                requireList: false);
            ValidateBattleResolutionParticipantObjectListCount(
                defenderObjectIds,
                diagnosticResolutionLabel,
                "defender object",
                errors);
            var survivingAttackerObjectIds = ValidateAuthoritativeStateResolutionTextList(
                "battle resolution",
                resolutionId,
                "surviving attacker object",
                resolution.SurvivingAttackerObjectIds,
                errors,
                requireList: false);
            var survivingDefenderObjectIds = ValidateAuthoritativeStateResolutionTextList(
                "battle resolution",
                resolutionId,
                "surviving defender object",
                resolution.SurvivingDefenderObjectIds,
                errors,
                requireList: false);
            var destroyedObjectIds = ValidateAuthoritativeStateResolutionTextList(
                "battle resolution",
                resolutionId,
                "destroyed object",
                resolution.DestroyedObjectIds,
                errors,
                requireList: false);
            ValidateBattleResolutionResultListObjectMembership(
                attackerObjectIds,
                defenderObjectIds,
                survivingAttackerObjectIds,
                survivingDefenderObjectIds,
                destroyedObjectIds,
                diagnosticResolutionLabel,
                errors);
            ValidateBattleResolutionNoResultSurvivorReasonCompatibility(
                kind,
                reason,
                survivingAttackerObjectIds,
                survivingDefenderObjectIds,
                diagnosticResolutionLabel,
                errors);
            ValidateBattleResolutionClosedWinnerSurvivorCompatibility(
                kind,
                resolution.AttackingPlayerId,
                resolution.DefendingPlayerId,
                resolution.WinnerPlayerId,
                survivingAttackerObjectIds,
                survivingDefenderObjectIds,
                diagnosticResolutionLabel,
                errors);
            var relatedEventKinds = ValidateAuthoritativeStateResolutionTextList(
                "battle resolution",
                resolutionId,
                "related event kind",
                resolution.RelatedEventKinds,
                errors,
                isKnownValue: IsKnownBattleResolutionRelatedEventKind);
            ValidateBattleResolutionRelatedEventKindCompatibility(
                kind,
                reason,
                relatedEventKinds,
                diagnosticResolutionLabel,
                errors);
        }
    }

    private static void ValidateRetainedResolutionHistoryStateTickOrder<TResolution>(
        IReadOnlyList<TResolution?> resolutions,
        Func<TResolution?, string?> readResolutionId,
        Func<TResolution?, long?> readTick,
        long authoritativeTick,
        string listLabel,
        string resolutionLabel,
        List<string> errors)
    {
        long? previousTick = null;
        foreach (var resolution in resolutions)
        {
            var tick = readTick(resolution);
            if (tick is null || tick < 0 || tick > authoritativeTick)
            {
                continue;
            }

            if (previousTick is long previous && tick > previous)
            {
                var resolutionId = readResolutionId(resolution)?.Trim();
                var diagnosticResolutionId = string.IsNullOrEmpty(resolutionId) ? "<missing>" : resolutionId;
                errors.Add(
                    $"{listLabel} ticks must be retained newest-first: {resolutionLabel} {diagnosticResolutionId} tick {tick} appears after earlier tick {previous}");
            }

            previousTick = tick;
        }
    }

    private static string ValidateAuthoritativeStateResolutionId(
        string resolutionLabel,
        string? resolutionId,
        ISet<string> seenResolutionIds,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(resolutionId))
        {
            errors.Add($"authoritative state {resolutionLabel} id is required");
            return string.Empty;
        }

        var normalizedResolutionId = resolutionId.Trim();
        if (!string.Equals(resolutionId, normalizedResolutionId, StringComparison.Ordinal))
        {
            errors.Add(
                $"authoritative state {resolutionLabel} {normalizedResolutionId} id has surrounding whitespace");
        }

        if (!seenResolutionIds.Add(normalizedResolutionId))
        {
            errors.Add($"authoritative state {resolutionLabel} {normalizedResolutionId} id is duplicated");
        }

        RejectAuthoritativeStateRedactionSentinel($"{resolutionLabel} id", normalizedResolutionId, errors);

        return normalizedResolutionId;
    }

    private static void ValidateAuthoritativeStateResolutionTick(
        string resolutionLabel,
        string resolutionId,
        long tick,
        long authoritativeTick,
        List<string> errors)
    {
        var diagnosticResolutionId = string.IsNullOrEmpty(resolutionId) ? "<missing>" : resolutionId;
        if (tick < 0)
        {
            errors.Add(
                $"authoritative state {resolutionLabel} {diagnosticResolutionId} tick {tick} cannot be negative");
        }

        if (tick > authoritativeTick)
        {
            errors.Add(
                $"authoritative state {resolutionLabel} {diagnosticResolutionId} tick {tick} is after authoritative state tick {authoritativeTick}");
        }
    }

    private static string? ValidateAuthoritativeStateResolutionText(
        string resolutionLabel,
        string resolutionId,
        string valueLabel,
        string? value,
        List<string> errors,
        Func<string, bool>? isKnownValue = null)
    {
        var diagnosticResolutionId = string.IsNullOrEmpty(resolutionId) ? "<missing>" : resolutionId;
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(
                $"authoritative state {resolutionLabel} {diagnosticResolutionId} {valueLabel} is required");
            return null;
        }

        var normalizedValue = value.Trim();
        if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
        {
            errors.Add(
                $"authoritative state {resolutionLabel} {diagnosticResolutionId} {valueLabel} {normalizedValue} has surrounding whitespace");
        }

        RejectAuthoritativeStateRedactionSentinel(
            $"{resolutionLabel} {diagnosticResolutionId} {valueLabel}",
            normalizedValue,
            errors);

        if (isKnownValue is not null && !isKnownValue(normalizedValue))
        {
            errors.Add(
                $"authoritative state {resolutionLabel} {diagnosticResolutionId} {valueLabel} {normalizedValue} is invalid");
        }

        return normalizedValue;
    }

    private static IReadOnlyList<string>? ValidateAuthoritativeStateResolutionTextList(
        string resolutionLabel,
        string resolutionId,
        string valueLabel,
        IReadOnlyList<string>? values,
        List<string> errors,
        bool requireList = true,
        Func<string, bool>? isKnownValue = null)
    {
        var diagnosticResolutionId = string.IsNullOrEmpty(resolutionId) ? "<missing>" : resolutionId;
        if (values is null)
        {
            if (requireList)
            {
                errors.Add(
                    $"authoritative state {resolutionLabel} {diagnosticResolutionId} {valueLabel} list is required");
            }

            return null;
        }

        var seenValues = new HashSet<string>(StringComparer.Ordinal);
        var normalizedValues = new List<string>();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add(
                    $"authoritative state {resolutionLabel} {diagnosticResolutionId} {valueLabel} is required");
                continue;
            }

            var normalizedValue = value.Trim();
            normalizedValues.Add(normalizedValue);
            if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state {resolutionLabel} {diagnosticResolutionId} {valueLabel} {normalizedValue} has surrounding whitespace");
            }

            RejectAuthoritativeStateRedactionSentinel(
                $"{resolutionLabel} {diagnosticResolutionId} {valueLabel}",
                normalizedValue,
                errors);

            if (isKnownValue is not null && !isKnownValue(normalizedValue))
            {
                errors.Add(
                    $"authoritative state {resolutionLabel} {diagnosticResolutionId} {valueLabel} {normalizedValue} is invalid");
            }

            if (!seenValues.Add(normalizedValue))
            {
                errors.Add(
                    $"authoritative state {resolutionLabel} {diagnosticResolutionId} {valueLabel} {normalizedValue} is duplicated");
            }
        }

        return normalizedValues;
    }

    private static void ValidateAuthoritativeStatePlayerPointers(
        MatchState authoritativeState,
        List<string> errors)
    {
        var seatPlayerIds = authoritativeState.Seats.Keys
            .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
            .Select(playerId => playerId.Trim())
            .ToHashSet(StringComparer.Ordinal);

        ValidateAuthoritativeStateRequiredPlayerPointer(
            "active player",
            authoritativeState.ActivePlayerId,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStateRequiredPlayerPointer(
            "turn player",
            authoritativeState.TurnPlayerId,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStateOptionalPlayerPointer(
            "priority player",
            authoritativeState.PriorityPlayerId,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStateOptionalPlayerPointer(
            "focus player",
            authoritativeState.FocusPlayerId,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStateOptionalPlayerPointer(
            "winner player",
            authoritativeState.WinnerPlayerId,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStateOptionalPlayerPointer(
            "opening second action player",
            authoritativeState.OpeningSecondActionPlayerId,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStateOptionalPlayerPointer(
            "extra turn player",
            authoritativeState.ExtraTurnPlayerId,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerList(
            "ready player",
            authoritativeState.ReadyPlayerIds,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerList(
            "passed priority player",
            authoritativeState.PassedPriorityPlayerIds,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerList(
            "passed focus player",
            authoritativeState.PassedFocusPlayerIds,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerList(
            "mulligan completed player",
            authoritativeState.MulliganCompletedPlayerIds,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerList(
            "destroyed unit owner",
            authoritativeState.DestroyedUnitOwnerIdsThisTurn,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerMap(
            "rune pool player",
            authoritativeState.RunePools,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerMap(
            "zone player",
            authoritativeState.PlayerZones,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerMap(
            "score player",
            authoritativeState.PlayerScores,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerMap(
            "experience player",
            authoritativeState.PlayerExperience,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerMap(
            "cards played player",
            authoritativeState.PlayerCardsPlayedThisTurn,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerMap(
            "decklist player",
            authoritativeState.PlayerDecklists,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStateDecklistValues(authoritativeState.PlayerDecklists, errors);
        ValidateAuthoritativeStateMapKeys(
            "card object map key",
            authoritativeState.CardObjects,
            errors,
            rejectRedactionSentinel: true);
        ValidateAuthoritativeStateMapKeys(
            "object location map key",
            authoritativeState.ObjectLocations,
            errors,
            rejectRedactionSentinel: true);
        ValidateAuthoritativeStateCardObjectIdentities(authoritativeState.CardObjects, errors);
        ValidateAuthoritativeStateCardObjectValues(authoritativeState.CardObjects, errors);
        foreach (var (id, card) in authoritativeState.CardObjects)
            if (card is not null && card.MovementCountThisTurn > 0
                && (!card.Tags.Contains(CardObjectTags.UnitCard)
                    || !authoritativeState.PlayerZones.Values.Any(z => z.Base.Contains(id) || z.Battlefields.Contains(id))))
                errors.Add($"authoritative state card object {id} movement count requires a unit on the field");
        foreach (var (id, card) in authoritativeState.CardObjects)
            if (card?.DamageMarkedByPlayerIds is { } players && (players.Count == 0 || card.Damage <= 0
                || players.Count > card.Damage || players.Any(player => player is null || !seatPlayerIds.Contains(player))
                || !players.SequenceEqual(players.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
                || !card.Tags.Contains(CardObjectTags.UnitCard)
                || !authoritativeState.PlayerZones.Values.Any(z => z.Base.Contains(id) || z.Battlefields.Contains(id))))
                errors.Add($"authoritative state card object {id} has invalid damage marking players");
        foreach (var (id, card) in authoritativeState.CardObjects)
            if (card?.GrantedTags is { } granted && (granted.Count == 0 || granted.Any(t => string.IsNullOrWhiteSpace(t) || !card.Tags.Contains(t))
                || !granted.SequenceEqual(granted.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
                || !authoritativeState.PlayerZones.Values.Any(z => z.Base.Contains(id) || z.Battlefields.Contains(id))))
                errors.Add($"authoritative state card object {id} has invalid granted tags");
        ValidateAuthoritativeStateCardObjectPlayers(authoritativeState.CardObjects, seatPlayerIds, errors);
        ValidateAuthoritativeStateObjectLocationPlayers(authoritativeState.ObjectLocations, seatPlayerIds, errors);
        ValidateAuthoritativeStateObjectLocationZones(authoritativeState.ObjectLocations, errors);
        ValidateAuthoritativeStateObjectLocationPlayerZones(
            authoritativeState.PlayerZones,
            authoritativeState.ObjectLocations,
            errors);
        ValidateAuthoritativeStateStackPlayers(authoritativeState.StackItems, seatPlayerIds, errors);
        ValidateAuthoritativeStateTriggerQueuePlayers(authoritativeState.TriggerQueue, seatPlayerIds, errors);
        ValidateAuthoritativeStatePendingPaymentPlayer(authoritativeState.PendingPayment, seatPlayerIds, errors);
        ValidateAuthoritativeStatePendingHandChoicePlayer(authoritativeState.PendingHandChoice, seatPlayerIds, errors);
        ValidateAuthoritativeStatePendingCardChoicePlayer(authoritativeState.PendingCardChoice, seatPlayerIds, errors);
        ValidateAuthoritativeStateBattlefieldResolutionPlayers(
            authoritativeState.BattlefieldResolutions,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStateBattleResolutionPlayers(
            authoritativeState.BattleResolutions,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStateObjectReferences(authoritativeState, errors);
    }

    private static void ValidateAuthoritativeStateRequiredPlayerPointer(
        string pointerName,
        string? playerId,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            errors.Add($"authoritative state {pointerName} is required");
            return;
        }

        var normalizedPlayerId = playerId.Trim();
        if (!string.Equals(playerId, normalizedPlayerId, StringComparison.Ordinal))
        {
            errors.Add($"authoritative state {pointerName} {normalizedPlayerId} has surrounding whitespace");
        }

        RejectAuthoritativeStateRedactionSentinel(pointerName, normalizedPlayerId, errors);

        if (!seatPlayerIds.Contains(normalizedPlayerId))
        {
            errors.Add(
                $"authoritative state {pointerName} {normalizedPlayerId} is missing from seats; {FormatAuthoritativeStateExpectedPlayerDetail(seatPlayerIds, normalizedPlayerId)}");
        }
    }

    private static void ValidateAuthoritativeStatePlayerList(
        string listMemberName,
        IReadOnlyList<string>? playerIds,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (playerIds is null)
        {
            errors.Add($"authoritative state {listMemberName} list is required");
            return;
        }

        var seenPlayerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var playerId in playerIds)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                errors.Add($"authoritative state {listMemberName} id is required");
                continue;
            }

            var normalizedPlayerId = playerId.Trim();
            if (!string.Equals(playerId, normalizedPlayerId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state {listMemberName} {normalizedPlayerId} has surrounding whitespace");
            }

            RejectAuthoritativeStateRedactionSentinel(listMemberName, normalizedPlayerId, errors);

            if (!seatPlayerIds.Contains(normalizedPlayerId))
            {
                errors.Add(
                    $"authoritative state {listMemberName} {normalizedPlayerId} is missing from seats; {FormatAuthoritativeStateExpectedPlayerDetail(seatPlayerIds, normalizedPlayerId)}");
            }

            if (!seenPlayerIds.Add(normalizedPlayerId))
            {
                errors.Add($"authoritative state {listMemberName} {normalizedPlayerId} is duplicated");
            }
        }
    }

    private static void ValidateAuthoritativeStatePlayerMap<TValue>(
        string mapKeyName,
        IReadOnlyDictionary<string, TValue>? values,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (values is null)
        {
            errors.Add($"authoritative state {mapKeyName} map is required");
            return;
        }

        var seenPlayerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var playerId in values.Keys.OrderBy(playerId => playerId, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                errors.Add($"authoritative state {mapKeyName} id is required");
                continue;
            }

            var normalizedPlayerId = playerId.Trim();
            if (!string.Equals(playerId, normalizedPlayerId, StringComparison.Ordinal))
            {
                errors.Add($"authoritative state {mapKeyName} {normalizedPlayerId} has surrounding whitespace");
            }

            RejectAuthoritativeStateRedactionSentinel(mapKeyName, normalizedPlayerId, errors);

            if (!seatPlayerIds.Contains(normalizedPlayerId))
            {
                errors.Add(
                    $"authoritative state {mapKeyName} {normalizedPlayerId} is missing from seats; {FormatAuthoritativeStateExpectedPlayerDetail(seatPlayerIds, normalizedPlayerId)}");
            }

            if (!seenPlayerIds.Add(normalizedPlayerId))
            {
                errors.Add($"authoritative state {mapKeyName} {normalizedPlayerId} is duplicated");
            }
        }
    }

    private static void ValidateAuthoritativeStateDecklistValues(
        IReadOnlyDictionary<string, OfficialDecklist>? playerDecklists,
        List<string> errors)
    {
        if (playerDecklists is null)
        {
            return;
        }

        foreach (var (playerId, decklist) in playerDecklists.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var playerLabel = string.IsNullOrWhiteSpace(playerId) ? "<unknown>" : playerId.Trim();
            if (decklist is null)
            {
                errors.Add($"authoritative state decklist for {playerLabel} is required");
                continue;
            }

            var legendCardNo = ValidateAuthoritativeStateRequiredText(
                $"decklist for {playerLabel} legend card no",
                decklist.LegendCardNo,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"decklist for {playerLabel} legend card no",
                legendCardNo,
                errors);

            var championCardNo = ValidateAuthoritativeStateRequiredText(
                $"decklist for {playerLabel} champion card no",
                decklist.ChampionCardNo,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"decklist for {playerLabel} champion card no",
                championCardNo,
                errors);

            ValidateAuthoritativeStateStringListValues(
                $"decklist for {playerLabel} main deck card",
                decklist.MainDeck,
                errors,
                rejectRedactionSentinel: true);
            ValidateAuthoritativeStateStringListValues(
                $"decklist for {playerLabel} rune deck card",
                decklist.RuneDeck,
                errors,
                rejectRedactionSentinel: true);
            ValidateAuthoritativeStateStringListValues(
                $"decklist for {playerLabel} battlefield card",
                decklist.Battlefields,
                errors,
                rejectRedactionSentinel: true);
        }
    }

    private static void ValidateAuthoritativeStateCardObjectPlayers(
        IReadOnlyDictionary<string, CardObjectState>? cardObjects,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (cardObjects is null)
        {
            errors.Add("authoritative state card objects map is required");
            return;
        }

        foreach (var (objectId, cardObject) in cardObjects.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (cardObject is null)
            {
                errors.Add($"authoritative state card object {objectId} is required");
                continue;
            }

            ValidateAuthoritativeStateOptionalObjectPlayer(
                $"card object {objectId} owner player",
                cardObject.OwnerId,
                seatPlayerIds,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"card object {objectId} owner player",
                cardObject.OwnerId,
                errors);
            ValidateAuthoritativeStateOptionalObjectPlayer(
                $"card object {objectId} controller player",
                cardObject.ControllerId,
                seatPlayerIds,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"card object {objectId} controller player",
                cardObject.ControllerId,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateMapKeys<TValue>(
        string mapKeyLabel,
        IReadOnlyDictionary<string, TValue>? values,
        List<string> errors,
        bool rejectRedactionSentinel = false)
    {
        if (values is null)
        {
            return;
        }

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in values.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add($"authoritative state {mapKeyLabel} is required");
                continue;
            }

            var normalizedKey = key.Trim();
            if (!string.Equals(key, normalizedKey, StringComparison.Ordinal))
            {
                errors.Add($"authoritative state {mapKeyLabel} {normalizedKey} has surrounding whitespace");
            }

            if (rejectRedactionSentinel
                && string.Equals(normalizedKey, "HIDDEN", StringComparison.Ordinal))
            {
                errors.Add($"authoritative state {mapKeyLabel} must not be redacted");
            }

            if (!seenKeys.Add(normalizedKey))
            {
                errors.Add($"authoritative state {mapKeyLabel} {normalizedKey} is duplicated");
            }
        }
    }
}

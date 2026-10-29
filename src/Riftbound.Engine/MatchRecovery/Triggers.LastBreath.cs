using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static void ValidateTriggerQueueUnsungHeroLastBreathPowerfulDrawContext(
        string payloadLabel,
        string? triggerId,
        string? sourceVisibility,
        string? effectKind,
        string? triggeredEventKind,
        List<string> errors)
    {
        ValidateTriggerQueueStandardLastBreathContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            UnsungHeroLastBreathPowerfulDrawEffectKindForRecovery,
            "unsung hero last-breath powerful draw",
            errors);
    }

    private static void ValidateTriggerQueueScoutingWarhawkLastBreathCallRuneContext(
        string payloadLabel,
        string? triggerId,
        string? sourceVisibility,
        string? effectKind,
        string? triggeredEventKind,
        List<string> errors,
        bool hasValidatedCapturedSource = false)
    {
        ValidateTriggerQueueStandardLastBreathContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            ScoutingWarhawkLastBreathCallRuneEffectKindForRecovery,
            "scouting warhawk last-breath call rune",
            errors,
            hasValidatedCapturedSource);
    }

    private static void ValidateTriggerQueueSadPoroLastBreathDrawContext(
        string payloadLabel,
        string? triggerId,
        string? sourceVisibility,
        string? effectKind,
        string? triggeredEventKind,
        List<string> errors)
    {
        ValidateTriggerQueueStandardLastBreathContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            SadPoroLastBreathDrawEffectKindForRecovery,
            "sad poro last-breath draw",
            errors);
    }

    private static void ValidateTriggerQueueLoyalPoroLastBreathDrawContext(
        string payloadLabel,
        string? triggerId,
        string? sourceVisibility,
        string? effectKind,
        string? triggeredEventKind,
        List<string> errors)
    {
        ValidateTriggerQueueStandardLastBreathContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            LoyalPoroLastBreathDrawEffectKindForRecovery,
            "loyal poro last-breath draw",
            errors);
    }

    private static void ValidateTriggerQueueHonestBrokerLastBreathCreateGoldContext(
        string payloadLabel,
        string? triggerId,
        string? sourceVisibility,
        string? effectKind,
        string? triggeredEventKind,
        List<string> errors)
    {
        ValidateTriggerQueueStandardLastBreathContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            HonestBrokerLastBreathCreateGoldEffectKindForRecovery,
            "honest broker last-breath create gold",
            errors);
    }

    private static void ValidateTriggerQueueMechanicalTricksterLastBreathCreateMinionsContext(
        string payloadLabel,
        string? triggerId,
        string? sourceVisibility,
        string? effectKind,
        string? triggeredEventKind,
        List<string> errors)
    {
        ValidateTriggerQueueStandardLastBreathContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            MechanicalTricksterLastBreathCreateMinionsEffectKindForRecovery,
            "mechanical trickster last-breath create minions",
            errors);
    }

    private static void ValidateTriggerQueueUndercoverAgentLastBreathContext(
        string payloadLabel,
        string? triggerId,
        string? sourceVisibility,
        string? effectKind,
        string? triggeredEventKind,
        List<string> errors)
    {
        ValidateTriggerQueueStandardLastBreathContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            UndercoverAgentLastBreathEffectKindForRecovery,
            "undercover agent last-breath",
            errors);
    }

    private static void ValidateTriggerQueueIroncladVanguardLastBreathCreateRobotsContext(
        string payloadLabel,
        string? triggerId,
        string? sourceVisibility,
        string? effectKind,
        string? triggeredEventKind,
        List<string> errors)
    {
        ValidateTriggerQueueStandardLastBreathContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            IroncladVanguardLastBreathCreateRobotsEffectKindForRecovery,
            "ironclad vanguard last-breath create robots",
            errors);
    }

    private static void ValidateTriggerQueueMuddyDredgerLastBreathCreateWarhawkContext(
        string payloadLabel,
        string? triggerId,
        string? sourceVisibility,
        string? effectKind,
        string? triggeredEventKind,
        List<string> errors)
    {
        ValidateTriggerQueueStandardLastBreathContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            MuddyDredgerLastBreathCreateWarhawkEffectKindForRecovery,
            "muddy dredger last-breath create warhawk",
            errors);
    }

    private static void ValidateTriggerQueueStandardLastBreathContext(
        string payloadLabel,
        string? triggerId,
        string? sourceVisibility,
        string? effectKind,
        string? triggeredEventKind,
        string expectedEffectKind,
        string diagnosticName,
        List<string> errors,
        bool hasValidatedCapturedSource = false)
    {
        if (triggerId is null || !IsStandardLastBreathTriggerForRecovery(triggerId, effectKind, expectedEffectKind))
        {
            return;
        }

        if (!IsStandardLastBreathTriggerIdForRecovery(triggerId, expectedEffectKind))
        {
            errors.Add($"{payloadLabel} {diagnosticName} trigger id is invalid");
            return;
        }

        if (!hasValidatedCapturedSource)
        {
            ValidateTriggerQueueSourceVisibilityMustBeVisible(
                payloadLabel,
                diagnosticName,
                sourceVisibility,
                errors);
        }

        ValidateTriggerQueueEffectKindMustBe(
            payloadLabel,
            diagnosticName,
            effectKind,
            expectedEffectKind,
            errors);

        ValidateTriggerQueueTriggeredEventKindMustBe(
            payloadLabel,
            diagnosticName,
            triggeredEventKind,
            "UNIT_DESTROYED",
            errors);
    }

    private static bool IsStandardLastBreathTriggerForRecovery(
        string triggerId,
        string? effectKind,
        string expectedEffectKind)
    {
        return IsStandardLastBreathTriggerIdForRecovery(triggerId, expectedEffectKind)
            || string.Equals(effectKind, expectedEffectKind, StringComparison.Ordinal);
    }

    private static bool IsStandardLastBreathTriggerIdForRecovery(string triggerId, string expectedEffectKind)
    {
        return triggerId.StartsWith(StandardTriggerIdPrefixForRecovery, StringComparison.Ordinal)
            && triggerId.EndsWith(
                $"-{expectedEffectKind}",
                StringComparison.Ordinal);
    }

    private static void ValidateTriggerQueueStandardLastBreathSourceObjectIdContext(
        string payloadLabel,
        string? triggerId,
        string? controllerId,
        string? sourceObjectId,
        string? effectKind,
        IReadOnlyDictionary<string, string>? objectControllers,
        string objectControllerLabel,
        IReadOnlyDictionary<string, string?>? objectCardNos,
        string objectCardNoLabel,
        IReadOnlyDictionary<string, int>? objectPowers,
        string objectPowerLabel,
        IReadOnlyDictionary<string, bool>? objectFaceDowns,
        string objectFaceDownLabel,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? objectTags,
        string objectTagLabel,
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations,
        string objectLocationLabel,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? playerBaseObjectIdsByPlayer,
        string playerBaseObjectIdsLabel,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? playerGraveyardObjectIdsByPlayer,
        string playerGraveyardObjectIdsLabel,
        List<string> errors,
        bool hasValidatedCapturedLastBreath = false)
    {
        // The death controller belongs to the captured skill, even after the card returns to its owner.
        if (hasValidatedCapturedLastBreath) return;

        ValidateTriggerQueueStandardLastBreathSourceObjectIdContext(
            payloadLabel,
            triggerId,
            controllerId,
            sourceObjectId,
            effectKind,
            objectControllers,
            objectControllerLabel,
            objectCardNos,
            objectCardNoLabel,
            objectPowers,
            objectPowerLabel,
            objectFaceDowns,
            objectFaceDownLabel,
            objectTags,
            objectTagLabel,
            objectLocations,
            objectLocationLabel,
            playerBaseObjectIdsByPlayer,
            playerBaseObjectIdsLabel,
            playerGraveyardObjectIdsByPlayer,
            playerGraveyardObjectIdsLabel,
            WatchfulSentinelLastBreathDrawEffectKindForRecovery,
            "watchful sentinel last-breath draw",
            errors);
        ValidateTriggerQueueStandardLastBreathSourceObjectIdContext(
            payloadLabel,
            triggerId,
            controllerId,
            sourceObjectId,
            effectKind,
            objectControllers,
            objectControllerLabel,
            objectCardNos,
            objectCardNoLabel,
            objectPowers,
            objectPowerLabel,
            objectFaceDowns,
            objectFaceDownLabel,
            objectTags,
            objectTagLabel,
            objectLocations,
            objectLocationLabel,
            playerBaseObjectIdsByPlayer,
            playerBaseObjectIdsLabel,
            playerGraveyardObjectIdsByPlayer,
            playerGraveyardObjectIdsLabel,
            UnsungHeroLastBreathPowerfulDrawEffectKindForRecovery,
            "unsung hero last-breath powerful draw",
            errors);
        ValidateTriggerQueueStandardLastBreathSourceObjectIdContext(
            payloadLabel,
            triggerId,
            controllerId,
            sourceObjectId,
            effectKind,
            objectControllers,
            objectControllerLabel,
            objectCardNos,
            objectCardNoLabel,
            objectPowers,
            objectPowerLabel,
            objectFaceDowns,
            objectFaceDownLabel,
            objectTags,
            objectTagLabel,
            objectLocations,
            objectLocationLabel,
            playerBaseObjectIdsByPlayer,
            playerBaseObjectIdsLabel,
            playerGraveyardObjectIdsByPlayer,
            playerGraveyardObjectIdsLabel,
            ScoutingWarhawkLastBreathCallRuneEffectKindForRecovery,
            "scouting warhawk last-breath call rune",
            errors);
        ValidateTriggerQueueStandardLastBreathSourceObjectIdContext(
            payloadLabel,
            triggerId,
            controllerId,
            sourceObjectId,
            effectKind,
            objectControllers,
            objectControllerLabel,
            objectCardNos,
            objectCardNoLabel,
            objectPowers,
            objectPowerLabel,
            objectFaceDowns,
            objectFaceDownLabel,
            objectTags,
            objectTagLabel,
            objectLocations,
            objectLocationLabel,
            playerBaseObjectIdsByPlayer,
            playerBaseObjectIdsLabel,
            playerGraveyardObjectIdsByPlayer,
            playerGraveyardObjectIdsLabel,
            SadPoroLastBreathDrawEffectKindForRecovery,
            "sad poro last-breath draw",
            errors);
        ValidateTriggerQueueStandardLastBreathSourceObjectIdContext(
            payloadLabel,
            triggerId,
            controllerId,
            sourceObjectId,
            effectKind,
            objectControllers,
            objectControllerLabel,
            objectCardNos,
            objectCardNoLabel,
            objectPowers,
            objectPowerLabel,
            objectFaceDowns,
            objectFaceDownLabel,
            objectTags,
            objectTagLabel,
            objectLocations,
            objectLocationLabel,
            playerBaseObjectIdsByPlayer,
            playerBaseObjectIdsLabel,
            playerGraveyardObjectIdsByPlayer,
            playerGraveyardObjectIdsLabel,
            LoyalPoroLastBreathDrawEffectKindForRecovery,
            "loyal poro last-breath draw",
            errors);
        ValidateTriggerQueueStandardLastBreathSourceObjectIdContext(
            payloadLabel,
            triggerId,
            controllerId,
            sourceObjectId,
            effectKind,
            objectControllers,
            objectControllerLabel,
            objectCardNos,
            objectCardNoLabel,
            objectPowers,
            objectPowerLabel,
            objectFaceDowns,
            objectFaceDownLabel,
            objectTags,
            objectTagLabel,
            objectLocations,
            objectLocationLabel,
            playerBaseObjectIdsByPlayer,
            playerBaseObjectIdsLabel,
            playerGraveyardObjectIdsByPlayer,
            playerGraveyardObjectIdsLabel,
            HonestBrokerLastBreathCreateGoldEffectKindForRecovery,
            "honest broker last-breath create gold",
            errors);
        ValidateTriggerQueueStandardLastBreathSourceObjectIdContext(
            payloadLabel,
            triggerId,
            controllerId,
            sourceObjectId,
            effectKind,
            objectControllers,
            objectControllerLabel,
            objectCardNos,
            objectCardNoLabel,
            objectPowers,
            objectPowerLabel,
            objectFaceDowns,
            objectFaceDownLabel,
            objectTags,
            objectTagLabel,
            objectLocations,
            objectLocationLabel,
            playerBaseObjectIdsByPlayer,
            playerBaseObjectIdsLabel,
            playerGraveyardObjectIdsByPlayer,
            playerGraveyardObjectIdsLabel,
            MechanicalTricksterLastBreathCreateMinionsEffectKindForRecovery,
            "mechanical trickster last-breath create minions",
            errors);
        ValidateTriggerQueueStandardLastBreathSourceObjectIdContext(
            payloadLabel,
            triggerId,
            controllerId,
            sourceObjectId,
            effectKind,
            objectControllers,
            objectControllerLabel,
            objectCardNos,
            objectCardNoLabel,
            objectPowers,
            objectPowerLabel,
            objectFaceDowns,
            objectFaceDownLabel,
            objectTags,
            objectTagLabel,
            objectLocations,
            objectLocationLabel,
            playerBaseObjectIdsByPlayer,
            playerBaseObjectIdsLabel,
            playerGraveyardObjectIdsByPlayer,
            playerGraveyardObjectIdsLabel,
            UndercoverAgentLastBreathEffectKindForRecovery,
            "undercover agent last-breath",
            errors);
        ValidateTriggerQueueStandardLastBreathSourceObjectIdContext(
            payloadLabel,
            triggerId,
            controllerId,
            sourceObjectId,
            effectKind,
            objectControllers,
            objectControllerLabel,
            objectCardNos,
            objectCardNoLabel,
            objectPowers,
            objectPowerLabel,
            objectFaceDowns,
            objectFaceDownLabel,
            objectTags,
            objectTagLabel,
            objectLocations,
            objectLocationLabel,
            playerBaseObjectIdsByPlayer,
            playerBaseObjectIdsLabel,
            playerGraveyardObjectIdsByPlayer,
            playerGraveyardObjectIdsLabel,
            IroncladVanguardLastBreathCreateRobotsEffectKindForRecovery,
            "ironclad vanguard last-breath create robots",
            errors);
        ValidateTriggerQueueStandardLastBreathSourceObjectIdContext(
            payloadLabel,
            triggerId,
            controllerId,
            sourceObjectId,
            effectKind,
            objectControllers,
            objectControllerLabel,
            objectCardNos,
            objectCardNoLabel,
            objectPowers,
            objectPowerLabel,
            objectFaceDowns,
            objectFaceDownLabel,
            objectTags,
            objectTagLabel,
            objectLocations,
            objectLocationLabel,
            playerBaseObjectIdsByPlayer,
            playerBaseObjectIdsLabel,
            playerGraveyardObjectIdsByPlayer,
            playerGraveyardObjectIdsLabel,
            MuddyDredgerLastBreathCreateWarhawkEffectKindForRecovery,
            "muddy dredger last-breath create warhawk",
            errors);
    }

    private static void ValidateTriggerQueueStandardLastBreathSourceObjectIdContext(
        string payloadLabel,
        string? triggerId,
        string? controllerId,
        string? sourceObjectId,
        string? effectKind,
        IReadOnlyDictionary<string, string>? objectControllers,
        string objectControllerLabel,
        IReadOnlyDictionary<string, string?>? objectCardNos,
        string objectCardNoLabel,
        IReadOnlyDictionary<string, int>? objectPowers,
        string objectPowerLabel,
        IReadOnlyDictionary<string, bool>? objectFaceDowns,
        string objectFaceDownLabel,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? objectTags,
        string objectTagLabel,
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations,
        string objectLocationLabel,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? playerBaseObjectIdsByPlayer,
        string playerBaseObjectIdsLabel,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? playerGraveyardObjectIdsByPlayer,
        string playerGraveyardObjectIdsLabel,
        string expectedEffectKind,
        string diagnosticName,
        List<string> errors)
    {
        if (triggerId is null
            || !IsStandardLastBreathTriggerForRecovery(triggerId, effectKind, expectedEffectKind)
            || !IsStandardLastBreathTriggerIdForRecovery(triggerId, expectedEffectKind)
            || string.IsNullOrWhiteSpace(sourceObjectId)
            || string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal))
        {
            return;
        }

        var sourceObjectSuffix = $"-{sourceObjectId}-{expectedEffectKind}";
        if (!triggerId.EndsWith(sourceObjectSuffix, StringComparison.Ordinal))
        {
            AddTriggerQueueSourceObjectIdBeforeEffectKindMismatch(
                payloadLabel,
                diagnosticName,
                sourceObjectId,
                triggerId,
                expectedEffectKind,
                GetTriggerQueueKnownObjectIdsForSourceContext(objectControllers, objectCardNos, objectLocations),
                errors);
            return;
        }

        if (triggerId.Length - sourceObjectSuffix.Length <= StandardTriggerIdPrefixForRecovery.Length)
        {
            errors.Add(
                $"{payloadLabel} {diagnosticName} trigger id stack item id is required before source object id {sourceObjectId}");
        }

        if (objectLocations is not null
            && objectLocations.TryGetValue(sourceObjectId, out var sourceLocation)
            && !string.IsNullOrWhiteSpace(sourceLocation.Zone)
            && !string.Equals(sourceLocation.Zone.Trim(), "GRAVEYARD", StringComparison.Ordinal))
        {
            var sourceZone = sourceLocation.Zone.Trim();
            errors.Add(
                $"{payloadLabel} {diagnosticName} source object id {sourceObjectId} location zone {sourceZone} must be GRAVEYARD in {objectLocationLabel}; {FormatExpectedActualForRecovery("GRAVEYARD", sourceZone)}");
        }

        if (objectLocations is not null
            && objectLocations.TryGetValue(sourceObjectId, out var sourceGraveyardLocation)
            && !string.IsNullOrWhiteSpace(sourceGraveyardLocation.Zone)
            && string.Equals(sourceGraveyardLocation.Zone.Trim(), "GRAVEYARD", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(sourceGraveyardLocation.PlayerId)
            && !string.IsNullOrWhiteSpace(controllerId)
            && !string.Equals(controllerId, "HIDDEN", StringComparison.Ordinal)
            && !string.Equals(sourceGraveyardLocation.PlayerId.Trim(), controllerId, StringComparison.Ordinal))
        {
            var sourceLocationPlayerId = sourceGraveyardLocation.PlayerId.Trim();
            AddTriggerQueueTriggerControllerMismatch(
                payloadLabel,
                diagnosticName,
                $"source object id {sourceObjectId} location player id",
                sourceLocationPlayerId,
                controllerId,
                objectLocationLabel,
                errors);
        }

        if (objectLocations is not null
            && objectLocations.TryGetValue(sourceObjectId, out sourceGraveyardLocation)
            && !string.IsNullOrWhiteSpace(sourceGraveyardLocation.Zone)
            && string.Equals(sourceGraveyardLocation.Zone.Trim(), "GRAVEYARD", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(sourceGraveyardLocation.PlayerId)
            && playerGraveyardObjectIdsByPlayer is not null
            && playerGraveyardObjectIdsByPlayer.TryGetValue(
                sourceGraveyardLocation.PlayerId.Trim(),
                out var graveyardObjectIds)
            && !graveyardObjectIds.Contains(sourceObjectId))
        {
            var sourceLocationPlayerId = sourceGraveyardLocation.PlayerId.Trim();
            errors.Add(
                $"{payloadLabel} {diagnosticName} source object id {sourceObjectId} location player id {sourceLocationPlayerId} must include source object in {playerGraveyardObjectIdsLabel} graveyard");
        }

        if (controllerId is not null
            && !string.Equals(controllerId, "HIDDEN", StringComparison.Ordinal)
            && objectControllers is not null
            && objectControllers.TryGetValue(sourceObjectId, out var sourceControllerId)
            && !string.Equals(sourceControllerId, controllerId, StringComparison.Ordinal))
        {
            AddTriggerQueueTriggerControllerMismatch(
                payloadLabel,
                diagnosticName,
                $"source object id {sourceObjectId} controller id",
                sourceControllerId,
                controllerId,
                objectControllerLabel,
                errors);
        }

        if (objectCardNos is not null
            && objectCardNos.TryGetValue(sourceObjectId, out var sourceCardNo))
        {
            if (!IsStandardLastBreathSourceTriggerSpecForRecovery(
                    expectedEffectKind,
                    sourceCardNo,
                    out var expectedTriggerSpecLabel))
            {
                var sourceCardNoLabel = sourceCardNo is null ? "<null>" : sourceCardNo;
                AddTriggerQueueSourceCardSpecMismatch(
                    payloadLabel,
                    diagnosticName,
                    sourceObjectId,
                    sourceCardNoLabel,
                    expectedTriggerSpecLabel,
                    objectCardNoLabel,
                    errors);
            }
        }


        var hasKnownPoroBaseAllyContext = false;
        var hasOtherFriendlyFaceUpBaseUnit = false;
        if (string.Equals(expectedEffectKind, SadPoroLastBreathDrawEffectKindForRecovery, StringComparison.Ordinal)
            || string.Equals(expectedEffectKind, LoyalPoroLastBreathDrawEffectKindForRecovery, StringComparison.Ordinal))
        {
            hasKnownPoroBaseAllyContext = TryHasOtherFriendlyFaceUpBaseUnitForStandardLastBreathForRecovery(
                controllerId,
                sourceObjectId,
                playerBaseObjectIdsByPlayer,
                objectControllers,
                objectFaceDowns,
                objectTags,
                out hasOtherFriendlyFaceUpBaseUnit);
        }

        if (string.Equals(expectedEffectKind, SadPoroLastBreathDrawEffectKindForRecovery, StringComparison.Ordinal)
            && hasKnownPoroBaseAllyContext
            && hasOtherFriendlyFaceUpBaseUnit)
        {
            errors.Add(
                $"{payloadLabel} {diagnosticName} source object id {sourceObjectId} must be isolated from other friendly face-up units in {playerBaseObjectIdsLabel} base for controller id {controllerId}; {FormatExpectedActualForRecovery(false, hasOtherFriendlyFaceUpBaseUnit)}");
        }

        if (string.Equals(expectedEffectKind, LoyalPoroLastBreathDrawEffectKindForRecovery, StringComparison.Ordinal)
            && hasKnownPoroBaseAllyContext
            && !hasOtherFriendlyFaceUpBaseUnit)
        {
            errors.Add(
                $"{payloadLabel} {diagnosticName} source object id {sourceObjectId} requires another friendly face-up unit in {playerBaseObjectIdsLabel} base for controller id {controllerId}; {FormatExpectedActualForRecovery(true, hasOtherFriendlyFaceUpBaseUnit)}");
        }

        if (objectTags is not null
            && objectTags.TryGetValue(sourceObjectId, out var sourceTags)
            && sourceTags.Contains(CardObjectTags.EquipmentCard)
            && !sourceTags.Contains(CardObjectTags.UnitCard))
        {
            errors.Add(
                $"{payloadLabel} {diagnosticName} source object id {sourceObjectId} must not be an equipment card in {objectTagLabel}; {FormatExpectedActualForRecovery("<non-equipment card>", CardObjectTags.EquipmentCard)}");
        }

        if (!IsWatchfulSentinelLastBreathDrawEffectForRecovery(expectedEffectKind)
            && objectTags is not null
            && objectTags.TryGetValue(sourceObjectId, out var unitSourceTags)
            && !unitSourceTags.Contains(CardObjectTags.UnitCard))
        {
            errors.Add(
                $"{payloadLabel} {diagnosticName} source object id {sourceObjectId} must be a unit card in {objectTagLabel}; {FormatExpectedActualForRecovery(CardObjectTags.UnitCard, FormatObjectTagsForRecovery(unitSourceTags))}");
        }

        if (!IsWatchfulSentinelLastBreathDrawEffectForRecovery(expectedEffectKind)
            && objectFaceDowns is not null
            && objectFaceDowns.TryGetValue(sourceObjectId, out var isFaceDown)
            && isFaceDown)
        {
            errors.Add(
                $"{payloadLabel} {diagnosticName} source object id {sourceObjectId} must not be face down in {objectFaceDownLabel}; {FormatExpectedActualForRecovery(false, isFaceDown)}");
        }

    }

    private static bool TryHasOtherFriendlyFaceUpBaseUnitForStandardLastBreathForRecovery(
        string? controllerId,
        string sourceObjectId,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? playerBaseObjectIdsByPlayer,
        IReadOnlyDictionary<string, string>? objectControllers,
        IReadOnlyDictionary<string, bool>? objectFaceDowns,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? objectTags,
        out bool hasOtherFriendlyFaceUpBaseUnit)
    {
        hasOtherFriendlyFaceUpBaseUnit = false;

        if (string.IsNullOrWhiteSpace(controllerId)
            || string.Equals(controllerId, "HIDDEN", StringComparison.Ordinal)
            || playerBaseObjectIdsByPlayer is null
            || objectTags is null)
        {
            return false;
        }

        if (!playerBaseObjectIdsByPlayer.TryGetValue(controllerId, out var baseObjectIds))
        {
            return true;
        }

        hasOtherFriendlyFaceUpBaseUnit = baseObjectIds.Any(candidateObjectId =>
            IsOtherFriendlyFaceUpBaseUnitForStandardLastBreathForRecovery(
                candidateObjectId,
                controllerId,
                sourceObjectId,
                objectControllers,
                objectFaceDowns,
                objectTags));
        return true;
    }

    private static bool IsOtherFriendlyFaceUpBaseUnitForStandardLastBreathForRecovery(
        string candidateObjectId,
        string controllerId,
        string sourceObjectId,
        IReadOnlyDictionary<string, string>? objectControllers,
        IReadOnlyDictionary<string, bool>? objectFaceDowns,
        IReadOnlyDictionary<string, IReadOnlySet<string>> objectTags)
    {
        if (string.IsNullOrWhiteSpace(candidateObjectId)
            || string.Equals(candidateObjectId, sourceObjectId, StringComparison.Ordinal))
        {
            return false;
        }

        if (objectControllers is not null
            && objectControllers.TryGetValue(candidateObjectId, out var candidateControllerId)
            && !string.Equals(candidateControllerId, controllerId, StringComparison.Ordinal))
        {
            return false;
        }

        if (objectFaceDowns is not null
            && objectFaceDowns.TryGetValue(candidateObjectId, out var isFaceDown)
            && isFaceDown)
        {
            return false;
        }

        return objectTags.TryGetValue(candidateObjectId, out var candidateTags)
            && candidateTags.Contains(CardObjectTags.UnitCard);
    }

    private static bool IsWatchfulSentinelLastBreathDrawEffectForRecovery(string effectKind)
    {
        return string.Equals(effectKind, WatchfulSentinelLastBreathDrawEffectKindForRecovery, StringComparison.Ordinal);
    }

    private static bool IsStandardLastBreathSourceTriggerSpecForRecovery(
        string expectedEffectKind,
        string? sourceCardNo,
        out string expectedTriggerSpecLabel)
    {
        if (string.Equals(expectedEffectKind, WatchfulSentinelLastBreathDrawEffectKindForRecovery, StringComparison.Ordinal))
        {
            expectedTriggerSpecLabel = WatchfulSentinelLastBreathDrawSourceTriggerSpecLabelForRecovery;
            return UnitDestroyedTriggerSpecRules.TryGetTrigger(
                sourceCardNo,
                UnitDestroyedTriggerSpecRules.IsLastBreathDrawOneTrigger,
                out _);
        }

        if (string.Equals(expectedEffectKind, SadPoroLastBreathDrawEffectKindForRecovery, StringComparison.Ordinal))
        {
            expectedTriggerSpecLabel = SadPoroLastBreathDrawSourceTriggerSpecLabelForRecovery;
            return UnitDestroyedTriggerSpecRules.TryGetTrigger(
                sourceCardNo,
                UnitDestroyedTriggerSpecRules.IsLastBreathDrawIfAloneTrigger,
                out _);
        }

        if (string.Equals(expectedEffectKind, LoyalPoroLastBreathDrawEffectKindForRecovery, StringComparison.Ordinal))
        {
            expectedTriggerSpecLabel = LoyalPoroLastBreathDrawSourceTriggerSpecLabelForRecovery;
            return UnitDestroyedTriggerSpecRules.TryGetTrigger(
                sourceCardNo,
                UnitDestroyedTriggerSpecRules.IsLastBreathDrawIfNotAloneTrigger,
                out _);
        }

        if (string.Equals(expectedEffectKind, UnsungHeroLastBreathPowerfulDrawEffectKindForRecovery, StringComparison.Ordinal))
        {
            expectedTriggerSpecLabel = UnsungHeroLastBreathPowerfulDrawSourceTriggerSpecLabelForRecovery;
            return UnitDestroyedTriggerSpecRules.TryGetTrigger(
                sourceCardNo,
                UnitDestroyedTriggerSpecRules.IsLastBreathPowerfulDrawTrigger,
                out _);
        }

        if (string.Equals(expectedEffectKind, ScoutingWarhawkLastBreathCallRuneEffectKindForRecovery, StringComparison.Ordinal))
        {
            expectedTriggerSpecLabel = ScoutingWarhawkLastBreathCallRuneSourceTriggerSpecLabelForRecovery;
            return UnitDestroyedTriggerSpecRules.TryGetTrigger(
                sourceCardNo,
                UnitDestroyedTriggerSpecRules.IsLastBreathCallRuneOneTrigger,
                out _);
        }

        if (string.Equals(expectedEffectKind, HonestBrokerLastBreathCreateGoldEffectKindForRecovery, StringComparison.Ordinal))
        {
            expectedTriggerSpecLabel = HonestBrokerLastBreathCreateGoldSourceTriggerSpecLabelForRecovery;
            return UnitDestroyedTriggerSpecRules.TryGetTrigger(
                sourceCardNo,
                UnitDestroyedTriggerSpecRules.IsLastBreathCreateDormantGoldTrigger,
                out _);
        }

        if (string.Equals(expectedEffectKind, UndercoverAgentLastBreathEffectKindForRecovery, StringComparison.Ordinal))
        {
            expectedTriggerSpecLabel = UndercoverAgentLastBreathDiscardDrawSourceTriggerSpecLabelForRecovery;
            return UnitDestroyedTriggerSpecRules.TryGetTrigger(
                sourceCardNo,
                UnitDestroyedTriggerSpecRules.IsLastBreathDiscardDrawTrigger,
                out _);
        }

        if (string.Equals(expectedEffectKind, MechanicalTricksterLastBreathCreateMinionsEffectKindForRecovery, StringComparison.Ordinal)
            || string.Equals(expectedEffectKind, IroncladVanguardLastBreathCreateRobotsEffectKindForRecovery, StringComparison.Ordinal)
            || string.Equals(expectedEffectKind, MuddyDredgerLastBreathCreateWarhawkEffectKindForRecovery, StringComparison.Ordinal))
        {
            expectedTriggerSpecLabel = UnitLastBreathCreateBaseUnitSourceTriggerSpecLabelForRecovery;
            return UnitDestroyedTriggerSpecRules.TryGetTrigger(
                sourceCardNo,
                trigger => UnitDestroyedTriggerSpecRules.IsLastBreathCreateBaseUnitTrigger(trigger)
                    && string.Equals(trigger.Kind, expectedEffectKind, StringComparison.Ordinal),
                out _);
        }

        expectedTriggerSpecLabel = string.Empty;
        return true;
    }

    private static void ValidateTriggerQueueControllerPlayerMembership(
        string payloadLabel,
        string? controllerId,
        IReadOnlySet<string>? knownPlayerIds,
        string knownPlayerLabel,
        List<string> errors)
    {
        if (controllerId is null
            || knownPlayerIds is null
            || string.Equals(controllerId, "HIDDEN", StringComparison.Ordinal))
        {
            return;
        }

        if (!knownPlayerIds.Contains(controllerId))
        {
            var expectedPlayerIds = knownPlayerIds
                .OrderBy(knownPlayerId => knownPlayerId, StringComparer.Ordinal)
                .ToArray();
            errors.Add(
                $"{payloadLabel} controller id {controllerId} is missing from {knownPlayerLabel}; {FormatExpectedActualForRecovery(expectedPlayerIds, controllerId)}");
        }
    }

    private static void ValidateTriggerQueueVisibleSourceObjectMembership(
        string payloadLabel,
        string? sourceObjectId,
        string? sourceVisibility,
        IReadOnlySet<string>? knownObjectIds,
        string knownObjectLabel,
        List<string> errors)
    {
        if (!string.Equals(sourceVisibility, "VISIBLE", StringComparison.Ordinal)
            || sourceObjectId is null
            || knownObjectIds is null
            || string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal))
        {
            return;
        }

        if (!knownObjectIds.Contains(sourceObjectId))
        {
            var expectedObjectIds = knownObjectIds
                .OrderBy(knownObjectId => knownObjectId, StringComparer.Ordinal)
                .ToArray();
            errors.Add(
                $"{payloadLabel} visible source object id {sourceObjectId} is missing from {knownObjectLabel}; {FormatExpectedActualForRecovery(expectedObjectIds, sourceObjectId)}");
        }
    }

    private static void ValidateTriggerQueuePromptFieldAbsence(
        object? triggerPayload,
        string payloadLabel,
        List<string> errors)
    {
        if (TryReadObjectValue(triggerPayload, "summary", out _))
        {
            errors.Add($"{payloadLabel} summary must be absent from timing trigger queue item");
        }

        if (TryReadObjectValue(triggerPayload, "visibleText", out _))
        {
            errors.Add($"{payloadLabel} visible text must be absent from timing trigger queue item");
        }
    }

    private static void ValidateTimingOrderTriggerPromptFieldAbsence(
        object? timingPayload,
        string timingLabel,
        List<string> errors)
    {
        ValidateTimingPromptFieldAbsence(timingPayload, "orderingPlayerId", timingLabel, errors);
        ValidateTimingPromptFieldAbsence(timingPayload, "orderedTriggerIds", timingLabel, errors);
        ValidateTimingPromptFieldAbsence(timingPayload, "triggerIds", timingLabel, errors);
        ValidateTimingPromptFieldAbsence(timingPayload, "triggers", timingLabel, errors);
        ValidateTimingPromptFieldAbsence(timingPayload, "triggerChoices", timingLabel, errors);
        ValidateTimingPromptFieldAbsence(timingPayload, "legalOrderingConstraints", timingLabel, errors);
        ValidateTimingPromptFieldAbsence(timingPayload, "triggeredByEventKind", timingLabel, errors);
        ValidateTimingPromptFieldAbsence(timingPayload, "orderingState", timingLabel, errors);
    }

    private static void ValidateTimingPromptFieldAbsence(
        object? timingPayload,
        string key,
        string timingLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(timingPayload, key, out _))
        {
            return;
        }

        errors.Add($"{timingLabel} order-trigger prompt field {key} must be absent");
    }

    private static bool OptionalStringSnapshotValueMatches(object? payload, string key, string? expected)
    {
        var expectedText = expected ?? string.Empty;
        if (!TryReadObjectValue(payload, key, out var value))
        {
            return string.IsNullOrEmpty(expectedText);
        }

        return TryReadOptionalStringValue(value, out var actual)
            && string.Equals(actual, expectedText, StringComparison.Ordinal);
    }

    private static bool OptionalIntSnapshotValueMatches(object? payload, string key, int? expected)
    {
        if (!TryReadObjectValue(payload, key, out var value))
        {
            return !expected.HasValue;
        }

        return expected.HasValue
            && TryReadIntValue(value, out var actual)
            && actual == expected.Value;
    }

    private static bool OptionalStringListSnapshotValueMatches(
        object? payload,
        string key,
        IReadOnlyList<string>? expected)
    {
        IReadOnlyList<string> expectedValues = expected ?? [];
        if (!TryReadObjectValue(payload, key, out var value))
        {
            return expectedValues.Count == 0;
        }

        return TryReadStringListValue(value, out var actual)
            && StringListsEqual(actual, expectedValues);
    }

    private static void ValidateSpectatorPendingPaymentPowerTraitPayloadPropertyNames(
        object? costPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(costPayload, "powerByTrait", out var powerByTraitPayload))
        {
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            powerByTraitPayload,
            "spectator replay frame timing pending payment power cost traits",
            errors);
    }

    private static void ValidateSpectatorPendingHandChoicePayload(
        IReadOnlyDictionary<string, object?> timing,
        MatchState authoritativeState,
        List<string> errors)
    {
        var authoritativeChoice = authoritativeState.PendingHandChoice;
        if (!timing.TryGetValue("pendingHandChoice", out var choicePayload))
        {
            errors.Add("spectator replay frame timing pending hand choice is required");
            AddMissingSpectatorPendingHandChoiceCountDiagnostics(authoritativeChoice, errors);
            return;
        }

        if (authoritativeChoice is null)
        {
            if (!IsNullSnapshotPayloadValue(choicePayload))
            {
                errors.Add("spectator replay frame timing pending hand choice must be empty when authoritative state has no pending hand choice");
            }

            return;
        }

        if (IsNullSnapshotPayloadValue(choicePayload))
        {
            errors.Add("spectator replay frame timing pending hand choice is required");
            AddMissingSpectatorPendingHandChoiceCountDiagnostics(authoritativeChoice, errors);
            return;
        }

        if (!IsSnapshotPlayerPayloadObject(choicePayload))
        {
            errors.Add("spectator replay frame timing pending hand choice payload is required");
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            choicePayload,
            "spectator replay frame timing pending hand choice",
            errors);
        ValidateSpectatorPendingHandChoicePayloadValues(choicePayload, errors);

        ValidateSpectatorPendingHandChoiceRequiredStringAuthoritativeValue(
            choicePayload,
            "choiceId",
            "spectator replay frame timing pending hand choice id does not match authoritative state pending hand choice id",
            authoritativeChoice.ChoiceId,
            errors);
        ValidateSpectatorPendingHandChoiceRequiredStringAuthoritativeValue(
            choicePayload,
            "choiceWindow",
            "spectator replay frame timing pending hand choice window does not match authoritative state pending hand choice window",
            authoritativeChoice.ChoiceWindow,
            errors);
        ValidateSpectatorPendingHandChoiceRequiredStringAuthoritativeValue(
            choicePayload,
            "playerId",
            "spectator replay frame timing pending hand choice player does not match authoritative state pending hand choice player",
            authoritativeChoice.PlayerId,
            errors);
        ValidateSpectatorPendingHandChoiceRequiredIntAuthoritativeValue(
            choicePayload,
            "requiredCount",
            "spectator replay frame timing pending hand choice required count does not match authoritative state pending hand choice required count",
            authoritativeChoice.RequiredCount,
            errors);
        ValidateSpectatorPendingHandChoiceRequiredIntAuthoritativeValue(
            choicePayload,
            "maxCount",
            "spectator replay frame timing pending hand choice max count does not match authoritative state pending hand choice max count",
            authoritativeChoice.MaxCount,
            errors);
        ValidateSpectatorPendingHandChoiceRequiredStringAuthoritativeValue(
            choicePayload,
            "reason",
            "spectator replay frame timing pending hand choice reason does not match authoritative state pending hand choice reason",
            authoritativeChoice.Reason,
            errors);
        ValidateSpectatorPendingHandChoiceRequiredStringAuthoritativeValue(
            choicePayload,
            "sourceObjectId",
            "spectator replay frame timing pending hand choice source object does not match authoritative state pending hand choice source object",
            authoritativeChoice.MovementObserver is null && authoritativeChoice.LastBreathDraw is null ? authoritativeChoice.SourceObjectId
                : ResolutionResult.VisibleObjectIdForViewer(authoritativeState, authoritativeChoice.SourceObjectId, string.Empty),
            errors);
        ValidateSpectatorPendingHandChoiceRequiredStringAuthoritativeValue(
            choicePayload,
            "effectKind",
            "spectator replay frame timing pending hand choice effect kind does not match authoritative state pending hand choice effect kind",
            authoritativeChoice.EffectKind,
            errors);
        ValidateSpectatorPendingHandChoiceRequiredStringAuthoritativeValue(
            choicePayload,
            "choiceState",
            "spectator replay frame timing pending hand choice state does not match authoritative spectator pending hand choice state",
            "WAITING_FOR_CHOICE",
            errors);

        if (TryReadObjectValue(choicePayload, "legalObjectIds", out _))
        {
            errors.Add("spectator replay frame timing pending hand choice legal object ids must be redacted");
        }
    }

    private static void ValidateSpectatorPendingHandChoiceRequiredStringAuthoritativeValue(
        object? choicePayload,
        string key,
        string mismatchMessage,
        string? expected,
        List<string> errors)
    {
        var readable = TryReadObjectString(choicePayload, key, out var actual);
        if (readable
            && string.Equals(actual, expected, StringComparison.Ordinal))
        {
            return;
        }

        AddSpectatorPendingHandChoiceAuthoritativeMismatch(
            mismatchMessage,
            expected,
            readable ? actual : FormatUnreadableObjectValue(choicePayload, key),
            errors);
    }

    private static void ValidateSpectatorPendingHandChoiceRequiredIntAuthoritativeValue(
        object? choicePayload,
        string key,
        string mismatchMessage,
        int expected,
        List<string> errors)
    {
        var readable = TryReadObjectInt(choicePayload, key, out var actual);
        if (readable && actual == expected)
        {
            return;
        }

        AddSpectatorPendingHandChoiceAuthoritativeMismatch(
            mismatchMessage,
            expected,
            readable ? actual : FormatUnreadableObjectValue(choicePayload, key),
            errors);
    }

    private static void AddSpectatorPendingHandChoiceAuthoritativeMismatch(
        string mismatchMessage,
        object? expected,
        object? actual,
        List<string> errors)
    {
        errors.Add($"{mismatchMessage}; {FormatExpectedActualForRecovery(expected, actual)}");
    }

    private static void AddMissingSpectatorPendingHandChoiceCountDiagnostics(
        PendingHandChoiceState? authoritativeChoice,
        List<string> errors)
    {
        if (authoritativeChoice is null)
        {
            return;
        }

        errors.Add(
            $"spectator replay frame timing pending hand choice required count 0 does not match authoritative state pending hand choice required count {authoritativeChoice.RequiredCount}");
        errors.Add(
            $"spectator replay frame timing pending hand choice max count 0 does not match authoritative state pending hand choice max count {authoritativeChoice.MaxCount}");
    }

    private static void ValidateSpectatorPendingCardChoicePayload(
        IReadOnlyDictionary<string, object?> timing,
        MatchState authoritativeState,
        List<string> errors)
    {
        var authoritativeChoice = authoritativeState.PendingCardChoice;
        if (!timing.TryGetValue("pendingCardChoice", out var choicePayload))
        {
            errors.Add("spectator replay frame timing pending card choice is required");
            AddMissingSpectatorPendingCardChoiceCountDiagnostics(authoritativeChoice, errors);
            return;
        }

        if (authoritativeChoice is null)
        {
            if (!IsNullSnapshotPayloadValue(choicePayload))
            {
                errors.Add("spectator replay frame timing pending card choice must be empty when authoritative state has no pending card choice");
            }

            return;
        }

        if (IsNullSnapshotPayloadValue(choicePayload))
        {
            errors.Add("spectator replay frame timing pending card choice is required");
            AddMissingSpectatorPendingCardChoiceCountDiagnostics(authoritativeChoice, errors);
            return;
        }

        if (!IsSnapshotPlayerPayloadObject(choicePayload))
        {
            errors.Add("spectator replay frame timing pending card choice payload is required");
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            choicePayload,
            "spectator replay frame timing pending card choice",
            errors);
        ValidatePendingCardChoicePayloadValues(
            choicePayload,
            "spectator replay frame timing pending card choice",
            value => string.Equals(value, "WAITING_FOR_CHOICE", StringComparison.Ordinal),
            errors, authoritativeChoice.DeckContext is not null);
        if (authoritativeChoice.DeckContext is not null)
            ValidatePrivateDeckChoiceEnvelope(choicePayload, authoritativeChoice, "spectator replay frame timing pending card choice", errors);

        ValidateSpectatorPendingHandChoiceRequiredStringAuthoritativeValue(
            choicePayload,
            "choiceId",
            "spectator replay frame timing pending card choice id does not match authoritative state pending card choice id",
            authoritativeChoice.ChoiceId,
            errors);
        ValidateSpectatorPendingHandChoiceRequiredStringAuthoritativeValue(
            choicePayload,
            "choiceWindow",
            "spectator replay frame timing pending card choice window does not match authoritative state pending card choice window",
            authoritativeChoice.ChoiceWindow,
            errors);
        ValidateSpectatorPendingHandChoiceRequiredStringAuthoritativeValue(
            choicePayload,
            "playerId",
            "spectator replay frame timing pending card choice player does not match authoritative state pending card choice player",
            authoritativeChoice.PlayerId,
            errors);
        if (authoritativeChoice.DeckContext is null) {
            ValidateSpectatorPendingHandChoiceRequiredIntAuthoritativeValue(choicePayload,"requiredCount",
                "spectator replay frame timing pending card choice required count does not match authoritative state pending card choice required count",
                authoritativeChoice.RequiredCount,errors);
            ValidateSpectatorPendingHandChoiceRequiredIntAuthoritativeValue(choicePayload,"maxCount",
                "spectator replay frame timing pending card choice max count does not match authoritative state pending card choice max count",
                authoritativeChoice.MaxCount,errors);
        }
        ValidateSpectatorPendingHandChoiceRequiredStringAuthoritativeValue(
            choicePayload,
            "reason",
            "spectator replay frame timing pending card choice reason does not match authoritative state pending card choice reason",
            authoritativeChoice.Reason,
            errors);
        ValidateSpectatorPendingHandChoiceRequiredStringAuthoritativeValue(
            choicePayload,
            "sourceObjectId",
            "spectator replay frame timing pending card choice source object does not match authoritative state pending card choice source object",
            ResolutionResult.VisibleObjectIdForViewer(authoritativeState, authoritativeChoice.SourceObjectId, string.Empty),
            errors);
        ValidateSpectatorPendingHandChoiceRequiredStringAuthoritativeValue(
            choicePayload,
            "effectKind",
            "spectator replay frame timing pending card choice effect kind does not match authoritative state pending card choice effect kind",
            authoritativeChoice.EffectKind,
            errors);
        ValidateSpectatorPendingHandChoiceRequiredStringAuthoritativeValue(
            choicePayload,
            "choiceState",
            "spectator replay frame timing pending card choice state does not match authoritative spectator pending card choice state",
            "WAITING_FOR_CHOICE",
            errors);
        if (authoritativeChoice.DeckContext is null)
            ValidateSpectatorPendingHandChoiceRequiredIntAuthoritativeValue(choicePayload,"legalCount",
                "spectator replay frame timing pending card choice legal count does not match authoritative state pending card choice legal count",
                authoritativeChoice.LegalObjectIds.Count,errors);
        ValidateSpectatorPendingHandChoiceRequiredIntAuthoritativeValue(
            choicePayload,
            "contextCount",
            "spectator replay frame timing pending card choice context count does not match authoritative state pending card choice context count",
            authoritativeChoice.ContextObjectIds.Count,
            errors);

        if (TryReadObjectValue(choicePayload, "legalObjectIds", out _))
        {
            errors.Add("spectator replay frame timing pending card choice legal object ids must be redacted");
        }

        if (TryReadObjectValue(choicePayload, "contextObjectIds", out _))
        {
            errors.Add("spectator replay frame timing pending card choice context object ids must be redacted");
        }
    }

    private static void AddMissingSpectatorPendingCardChoiceCountDiagnostics(
        PendingCardChoiceState? authoritativeChoice,
        List<string> errors)
    {
        if (authoritativeChoice is null)
        {
            return;
        }

        errors.Add(
            $"spectator replay frame timing pending card choice required count 0 does not match authoritative state pending card choice required count {authoritativeChoice.RequiredCount}");
        errors.Add(
            $"spectator replay frame timing pending card choice max count 0 does not match authoritative state pending card choice max count {authoritativeChoice.MaxCount}");
    }

    private static void ValidateSpectatorPendingHandChoicePayloadValues(
        object? choicePayload,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing pending hand choice";
        ValidatePendingHandChoicePayloadValues(
            choicePayload,
            payloadLabel,
            value => string.Equals(value, "WAITING_FOR_CHOICE", StringComparison.Ordinal),
            errors);
    }

    private static void ValidatePendingHandChoicePayloadValues(
        object? choicePayload,
        string payloadLabel,
        Func<string, bool> isKnownChoiceState,
        List<string> errors)
    {
        if (TryReadObjectValue(choicePayload, "lastBreathDraw", out _))
            errors.Add("public hand choice exposes a private last-breath draw birth receipt");
        ValidateSnapshotPayloadRequiredStringValue(
            choicePayload,
            "choiceId",
            payloadLabel,
            "id",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            choicePayload,
            "choiceWindow",
            payloadLabel,
            "window",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            choicePayload,
            "playerId",
            payloadLabel,
            "player id",
            errors);
        var requiredCount = ValidateSnapshotPayloadRequiredPositiveIntValue(
            choicePayload,
            "requiredCount",
            payloadLabel,
            "required count",
            errors);
        var maxCount = ValidateSnapshotPayloadRequiredPositiveIntValue(
            choicePayload,
            "maxCount",
            payloadLabel,
            "max count",
            errors);
        if (requiredCount is not null
            && maxCount is not null
            && maxCount.Value < requiredCount.Value)
        {
            errors.Add($"{payloadLabel} max count {maxCount.Value} is less than required count {requiredCount.Value}");
        }

        ValidateSnapshotPayloadOptionalStringValue(
            choicePayload,
            "reason",
            payloadLabel,
            "reason",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            choicePayload,
            "sourceObjectId",
            payloadLabel,
            "source object id",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            choicePayload,
            "effectKind",
            payloadLabel,
            "effect kind",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            choicePayload,
            "choiceState",
            payloadLabel,
            "state",
            errors,
            isKnownChoiceState);
    }

    private static void ValidatePendingCardChoicePayloadValues(
        object? choicePayload,
        string payloadLabel,
        Func<string, bool> isKnownChoiceState,
        List<string> errors,
        bool privateDeckChoice = false)
    {
        ValidateSnapshotPayloadRequiredStringValue(
            choicePayload,
            "choiceId",
            payloadLabel,
            "id",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            choicePayload,
            "choiceWindow",
            payloadLabel,
            "window",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            choicePayload,
            "playerId",
            payloadLabel,
            "player id",
            errors);
        var requiredCount = privateDeckChoice ? null : ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            choicePayload,
            "requiredCount",
            payloadLabel,
            "required count",
            errors);
        var maxCount = privateDeckChoice ? null : ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            choicePayload,
            "maxCount",
            payloadLabel,
            "max count",
            errors);
        if (requiredCount is not null
            && maxCount is not null
            && maxCount.Value < requiredCount.Value)
        {
            errors.Add($"{payloadLabel} max count {maxCount.Value} is less than required count {requiredCount.Value}");
        }

        ValidateSnapshotPayloadOptionalStringValue(
            choicePayload,
            "reason",
            payloadLabel,
            "reason",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            choicePayload,
            "sourceObjectId",
            payloadLabel,
            "source object id",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            choicePayload,
            "effectKind",
            payloadLabel,
            "effect kind",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            choicePayload,
            "choiceState",
            payloadLabel,
            "state",
            errors,
            isKnownChoiceState);
        if (!privateDeckChoice) ValidateSnapshotPayloadRequiredNonNegativeIntValue(choicePayload,"legalCount",payloadLabel,"legal count",errors);
        ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            choicePayload,
            "contextCount",
            payloadLabel,
            "context count",
            errors);
    }

    private static void ValidatePrivateDeckChoiceEnvelope(object? payload, PendingCardChoiceState choice, string label, List<string> errors)
    {
        foreach (var key in new[] {"requiredCount","maxCount","legalCount","legalObjectIds","cardChoices","viewedCards"})
            if (TryReadObjectValue(payload,key,out _)) errors.Add($"{label} private deck choice exposes {key}");
        foreach (var (key,expected) in new Dictionary<string,string> {
            ["choiceId"]=choice.ChoiceId,["choiceWindow"]=choice.ChoiceWindow,["playerId"]=choice.PlayerId,["choiceState"]="WAITING_FOR_CHOICE" })
            if (!TryReadObjectString(payload,key,out var actual) || actual!=expected)
                errors.Add($"{label} private deck choice {key} differs from authoritative choice");
    }

    private static void ValidateSpectatorRuleQueueCoveragePayloads(
        IReadOnlyDictionary<string, object?> timing,
        MatchState authoritativeState,
        List<string> errors)
    {
        var authoritativeCoverage = BuildRuleQueueCoverageRecoveryRows(authoritativeState);
        if (!timing.TryGetValue("ruleQueueCoverage", out var coveragePayload)
            || IsNullSnapshotPayloadValue(coveragePayload))
        {
            errors.Add("spectator replay frame timing rule queue coverage is required");
            return;
        }

        if (!TryReadObjectListValue(coveragePayload, out var spectatorCoverage))
        {
            errors.Add("spectator replay frame timing rule queue coverage payload is required");
            return;
        }

        if (spectatorCoverage.Count != authoritativeCoverage.Count)
        {
            errors.Add(
                $"spectator replay frame timing rule queue coverage count {spectatorCoverage.Count} does not match authoritative state rule queue coverage count {authoritativeCoverage.Count}");
        }

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spectatorCoverageItem in spectatorCoverage)
        {
            if (!IsSnapshotPlayerPayloadObject(spectatorCoverageItem))
            {
                errors.Add("spectator replay frame timing rule queue coverage payload is required");
                continue;
            }

            const string payloadLabel = "spectator replay frame timing rule queue coverage item";
            ValidateSnapshotPayloadObjectPropertyNames(
                spectatorCoverageItem,
                payloadLabel,
                errors);
            var key = ValidateSnapshotPayloadRequiredStringValue(
                spectatorCoverageItem,
                "key",
                payloadLabel,
                "key",
                errors,
                IsKnownRuleQueueCoverageKey,
                KnownRuleQueueCoverageKeys);
            if (key is not null && !seenKeys.Add(key))
            {
                errors.Add($"{payloadLabel} key {key} is duplicated");
            }

            var liveCount = ValidateSnapshotPayloadRequiredNonNegativeIntValue(
                spectatorCoverageItem,
                "liveCount",
                payloadLabel,
                "live count",
                errors);
            var evidenceKeys = ValidateSnapshotPayloadRequiredStringListValues(
                spectatorCoverageItem,
                "evidenceKeys",
                payloadLabel,
                "evidence key",
                errors,
                IsKnownRuleQueueCoverageEvidenceKey);
            if (evidenceKeys is not null)
            {
                var duplicateEvidenceKeys = evidenceKeys
                    .GroupBy(value => value, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .ToArray();
                foreach (var duplicate in duplicateEvidenceKeys)
                {
                    errors.Add($"{payloadLabel} evidence key {duplicate} is duplicated");
                }
            }

            if (liveCount is > 0 && evidenceKeys is { Count: 0 })
            {
                errors.Add($"{payloadLabel} evidence key list must not be empty when live count is positive");
            }

            if (liveCount == 0 && evidenceKeys is { Count: > 0 })
            {
                errors.Add($"{payloadLabel} evidence key list must be empty when live count is zero");
            }
        }

        if (!StringListsEqual(
                ExtractObjectStringValues(spectatorCoverage, "key"),
                authoritativeCoverage.Select(item => item.Key).ToArray()))
        {
            errors.Add("spectator replay frame timing rule queue coverage keys disagree with authoritative state rule queue coverage keys");
        }

        if (!IntListsEqual(
                ExtractObjectIntValues(spectatorCoverage, "liveCount"),
                authoritativeCoverage.Select(item => item.LiveCount).ToArray()))
        {
            errors.Add("spectator replay frame timing rule queue coverage live counts disagree with authoritative state rule queue coverage live counts");
        }

        if (!StringListCollectionsEqual(
                ExtractObjectStringListValues(spectatorCoverage, "evidenceKeys"),
                authoritativeCoverage.Select(item => item.EvidenceKeys).ToArray()))
        {
            errors.Add("spectator replay frame timing rule queue coverage evidence keys disagree with authoritative state rule queue coverage evidence keys");
        }
    }

    private static IReadOnlyList<RuleQueueCoverageRecoveryRow> BuildRuleQueueCoverageRecoveryRows(MatchState state)
    {
        var pendingBattleTaskCount = state.PendingTaskQueue.Tasks.Count(task => IsRuleQueueCoverageBattleTaskKind(task.Kind));
        var pendingPaymentTaskCount = state.PendingTaskQueue.Tasks.Count(task => IsRuleQueueCoveragePaymentTaskKind(task.Kind));
        var liveBattleCount =
            (state.BattleState.IsActive ? 1 : 0)
            + state.BattlefieldTasks.Count
            + pendingBattleTaskCount
            + state.BattlefieldResolutions.Count
            + state.BattleResolutions.Count;
        var livePaymentCount = (state.PendingPayment is null ? 0 : 1) + pendingPaymentTaskCount;
        var liveWindowCount = string.IsNullOrWhiteSpace(state.TurnWindow.State) ? 0 : 1;

        return
        [
            new RuleQueueCoverageRecoveryRow(
                "stack",
                state.StackItems.Count,
                RuleQueueCoverageEvidenceKeys(("stack", state.StackItems.Count))),
            new RuleQueueCoverageRecoveryRow(
                "trigger",
                state.TriggerQueue.Count,
                RuleQueueCoverageEvidenceKeys(("triggerQueue", state.TriggerQueue.Count))),
            new RuleQueueCoverageRecoveryRow(
                "battle",
                liveBattleCount,
                RuleQueueCoverageEvidenceKeys(
                    ("battle", state.BattleState.IsActive ? 1 : 0),
                    ("battlefieldTasks", state.BattlefieldTasks.Count),
                    ("pendingTaskQueue", pendingBattleTaskCount),
                    ("battlefieldResolutions", state.BattlefieldResolutions.Count),
                    ("battleResolutions", state.BattleResolutions.Count))),
            new RuleQueueCoverageRecoveryRow(
                "window",
                liveWindowCount,
                RuleQueueCoverageEvidenceKeys(("turnWindow", liveWindowCount))),
            new RuleQueueCoverageRecoveryRow(
                "payment",
                livePaymentCount,
                RuleQueueCoverageEvidenceKeys(
                    ("pendingPayment", state.PendingPayment is null ? 0 : 1),
                    ("pendingTaskQueue", pendingPaymentTaskCount)))
        ];
    }

    private static IReadOnlyList<string> RuleQueueCoverageEvidenceKeys(params (string Key, int Count)[] evidence)
    {
        return evidence
            .Where(item => item.Count > 0)
            .Select(item => item.Key)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsRuleQueueCoverageBattleTaskKind(string kind)
    {
        return kind.StartsWith("BATTLE", StringComparison.Ordinal)
            || string.Equals(kind, "START_BATTLE", StringComparison.Ordinal);
    }

    private static bool IsRuleQueueCoveragePaymentTaskKind(string kind)
    {
        return kind.StartsWith("PAYMENT", StringComparison.Ordinal)
            || string.Equals(kind, "COST_PAID", StringComparison.Ordinal);
    }

    private static void ValidateSpectatorBattlefieldTaskPayloads(
        IReadOnlyDictionary<string, object?> timing,
        MatchState authoritativeState,
        List<string> errors)
    {
        var authoritativeBattlefieldTasks = authoritativeState.BattlefieldTasks;
        if (!timing.TryGetValue("battlefieldTasks", out var battlefieldTasksPayload)
            || IsNullSnapshotPayloadValue(battlefieldTasksPayload))
        {
            errors.Add("spectator replay frame timing battlefield tasks are required");
            if (authoritativeBattlefieldTasks.Count > 0)
            {
                errors.Add(
                    $"spectator replay frame timing battlefield task count 0 does not match authoritative state battlefield task count {authoritativeBattlefieldTasks.Count}");
            }

            return;
        }

        if (!TryReadObjectListValue(battlefieldTasksPayload, out var spectatorBattlefieldTasks))
        {
            errors.Add("spectator replay frame timing battlefield tasks payload is required");
            return;
        }

        var validateAuthoritativeBattlefieldTaskParity = spectatorBattlefieldTasks.Count == authoritativeBattlefieldTasks.Count;
        if (!validateAuthoritativeBattlefieldTaskParity)
        {
            errors.Add(
                $"spectator replay frame timing battlefield task count {spectatorBattlefieldTasks.Count} does not match authoritative state battlefield task count {authoritativeBattlefieldTasks.Count}");
        }

        var seenTaskIds = new HashSet<string>(StringComparer.Ordinal);
        var seatPlayerIds = BuildNormalizedPlayerIdSet(authoritativeState.Seats.Keys);
        var knownObjectIds = BuildAuthoritativeStateKnownObjectIds(authoritativeState);
        var objectControllers = BuildAuthoritativeStateObjectControllerIndex(authoritativeState);
        var objectTags = BuildAuthoritativeStateObjectTagIndex(authoritativeState);
        var battlefieldStateObjectIds = BuildAuthoritativeStateBattlefieldStateObjectIds(authoritativeState);
        var battlefieldStateOccupantObjectIds = BuildAuthoritativeStateBattlefieldStateOccupantObjectIds(authoritativeState);
        var knownStackItemIds = BuildKnownStackItemIds(authoritativeState.StackItems.Select(item => item?.StackItemId));
        var requiredTaskKindsByBattlefield =
            BuildAuthoritativeStateRequiredBattlefieldTaskKindsByBattlefield(authoritativeBattlefieldTasks);
        var authoritativeBattlefieldTasksByKey =
            BuildAuthoritativeStateBattlefieldTasksByKey(authoritativeBattlefieldTasks);
        foreach (var spectatorBattlefieldTask in spectatorBattlefieldTasks)
        {
            if (!IsSnapshotPlayerPayloadObject(spectatorBattlefieldTask))
            {
                errors.Add("spectator replay frame timing battlefield task payload is required");
                continue;
            }

            ValidateSnapshotPayloadObjectPropertyNames(
                spectatorBattlefieldTask,
                "spectator replay frame timing battlefield task item",
                errors);
            ValidateSpectatorBattlefieldTaskPayloadScalarValues(
                spectatorBattlefieldTask,
                seenTaskIds,
                seatPlayerIds,
                knownObjectIds,
                objectTags,
                battlefieldStateObjectIds,
                errors);
            ValidateSpectatorBattlefieldTaskPayloadListValues(
                spectatorBattlefieldTask,
                seatPlayerIds,
                knownObjectIds,
                authoritativeState.ObjectLocations,
                objectControllers,
                objectTags,
                battlefieldStateOccupantObjectIds,
                knownStackItemIds,
                errors);
        }

        ValidateBattlefieldTaskKindSet(
            spectatorBattlefieldTasks,
            requiredTaskKindsByBattlefield,
            "spectator replay frame timing battlefield task item",
            "authoritative state battlefield tasks",
            errors);
        ValidateSpectatorBattlefieldTaskKeyedAuthoritativeValues(
            spectatorBattlefieldTasks,
            authoritativeBattlefieldTasksByKey,
            errors);

        if (!validateAuthoritativeBattlefieldTaskParity)
        {
            return;
        }

        if (!StringListsEqual(
                ExtractObjectStringValues(spectatorBattlefieldTasks, "taskId"),
                authoritativeBattlefieldTasks.Select(task => task.TaskId).ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task ids disagree with authoritative state battlefield task ids");
        }

        if (!StringListsEqual(
                ExtractObjectStringValues(spectatorBattlefieldTasks, "kind"),
                authoritativeBattlefieldTasks.Select(task => task.Kind).ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task kinds disagree with authoritative state battlefield task kinds");
        }

        if (!StringListsEqual(
                ExtractObjectStringValues(spectatorBattlefieldTasks, "status"),
                authoritativeBattlefieldTasks.Select(task => task.Status).ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task statuses disagree with authoritative state battlefield task statuses");
        }

        if (!StringListsEqual(
                ExtractObjectStringValues(spectatorBattlefieldTasks, "reason"),
                authoritativeBattlefieldTasks.Select(task => task.Reason).ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task reasons disagree with authoritative state battlefield task reasons");
        }

        if (!StringListsEqual(
                ExtractObjectStringValues(spectatorBattlefieldTasks, "battlefieldObjectId"),
                authoritativeBattlefieldTasks.Select(task => task.BattlefieldObjectId).ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task battlefield object ids disagree with authoritative state battlefield task battlefield object ids");
        }

        if (!StringListCollectionsEqual(
                ExtractObjectStringListValues(spectatorBattlefieldTasks, "participantControllerIds"),
                authoritativeBattlefieldTasks.Select(task => task.ParticipantControllerIds).ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task participant controller ids disagree with authoritative state battlefield task participant controller ids");
        }

        if (!StringListCollectionsEqual(
                ExtractObjectStringListValues(spectatorBattlefieldTasks, "participantObjectIds"),
                authoritativeBattlefieldTasks.Select(task => task.ParticipantObjectIds).ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task participant object ids disagree with authoritative state battlefield task participant object ids");
        }

        if (!StringListsEqual(
                ExtractObjectOptionalStringValues(spectatorBattlefieldTasks, "actingPlayerId"),
                authoritativeBattlefieldTasks.Select(task => task.ActingPlayerId ?? string.Empty).ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task acting players disagree with authoritative state battlefield task acting players");
        }

        var spectatorBattlefieldTaskStackItemIds = ExtractObjectStringListValues(
            spectatorBattlefieldTasks,
            "stackItemIds");
        var authoritativeBattlefieldTaskStackItemIds = authoritativeBattlefieldTasks
            .Select(task => task.StackItemIds)
            .ToArray();
        if (!StringListCollectionsEqual(
                spectatorBattlefieldTaskStackItemIds,
                authoritativeBattlefieldTaskStackItemIds))
        {
            errors.Add(
                $"spectator replay frame timing battlefield task stack item ids disagree with authoritative state battlefield task stack item ids; {FormatExpectedActualForRecovery(authoritativeBattlefieldTaskStackItemIds, spectatorBattlefieldTaskStackItemIds)}");
        }

        if (!StringListsEqual(
                ExtractObjectOptionalStringValues(spectatorBattlefieldTasks, "spellDuelId"),
                authoritativeBattlefieldTasks
                    .Select(task => string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal)
                        ? BattleLifecycleIds.SpellDuelIdForBattlefield(task.BattlefieldObjectId) ?? string.Empty
                        : string.Empty)
                    .ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task spell duel ids disagree with authoritative state battlefield task spell duel ids");
        }

        if (!StringListsEqual(
                ExtractObjectOptionalStringValues(spectatorBattlefieldTasks, "battleId"),
                authoritativeBattlefieldTasks
                    .Select(task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                        ? BattleLifecycleIds.BattleIdForBattlefield(task.BattlefieldObjectId) ?? string.Empty
                        : string.Empty)
                    .ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task battle ids disagree with authoritative state battlefield task battle ids");
        }
    }

    private static void ValidateSpectatorBattlefieldTaskPayloadScalarValues(
        object? spectatorBattlefieldTask,
        HashSet<string> seenTaskIds,
        IReadOnlySet<string> seatPlayerIds,
        IReadOnlySet<string> knownObjectIds,
        IReadOnlyDictionary<string, IReadOnlySet<string>> objectTags,
        IReadOnlySet<string> battlefieldStateObjectIds,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing battlefield task item";
        var taskId = ValidateSnapshotPayloadRequiredStringValue(
            spectatorBattlefieldTask,
            "taskId",
            payloadLabel,
            "task id",
            errors);
        if (taskId is not null && !seenTaskIds.Add(taskId))
        {
            errors.Add($"{payloadLabel} task id {taskId} is duplicated");
        }

        var kind = ValidateSnapshotPayloadRequiredStringValue(
            spectatorBattlefieldTask,
            "kind",
            payloadLabel,
            "kind",
            errors,
            IsKnownBattlefieldTaskKind);
        var status = ValidateSnapshotPayloadRequiredStringValue(
            spectatorBattlefieldTask,
            "status",
            payloadLabel,
            "status",
            errors,
            IsKnownBattlefieldTaskStatus);
        var reason = ValidateSnapshotPayloadRequiredStringValue(
            spectatorBattlefieldTask,
            "reason",
            payloadLabel,
            "reason",
            errors,
            IsKnownBattlefieldTaskReason);
        var battlefieldObjectId = ValidateSnapshotPayloadRequiredStringValue(
            spectatorBattlefieldTask,
            "battlefieldObjectId",
            payloadLabel,
            "battlefield object id",
            errors);
        ValidateTimingOptionalObjectReferenceWithExpectedDetails(
            spectatorBattlefieldTask,
            "battlefieldObjectId",
            $"{payloadLabel} battlefield object id",
            knownObjectIds,
            "object registry",
            errors);
        ValidateBattlefieldTaskBattlefieldObjectCardMembership(
            spectatorBattlefieldTask,
            $"{payloadLabel} battlefield object id",
            objectTags,
            errors);
        ValidateBattlefieldTaskBattlefieldStateMembership(
            spectatorBattlefieldTask,
            $"{payloadLabel} battlefield object id",
            battlefieldStateObjectIds,
            "authoritative state battlefield states",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            spectatorBattlefieldTask,
            "actingPlayerId",
            payloadLabel,
            "acting player id",
            errors);
        ValidateTimingOptionalPlayerReference(
            spectatorBattlefieldTask,
            "actingPlayerId",
            $"{payloadLabel} acting player id",
            seatPlayerIds,
            "seats",
            errors);
        var hasSpellDuelIdPayload = TryReadObjectValue(spectatorBattlefieldTask, "spellDuelId", out var spellDuelIdPayload)
            && !IsNullSnapshotPayloadValue(spellDuelIdPayload);
        var spellDuelId = ValidateSnapshotPayloadOptionalStringValue(
            spectatorBattlefieldTask,
            "spellDuelId",
            payloadLabel,
            "spell duel id",
            errors);
        var hasBattleIdPayload = TryReadObjectValue(spectatorBattlefieldTask, "battleId", out var battleIdPayload)
            && !IsNullSnapshotPayloadValue(battleIdPayload);
        var battleId = ValidateSnapshotPayloadOptionalStringValue(
            spectatorBattlefieldTask,
            "battleId",
            payloadLabel,
            "battle id",
            errors);
        ValidateBattlefieldTaskDerivedTaskId(
            payloadLabel,
            taskId,
            kind,
            battlefieldObjectId,
            errors);
        ValidateBattlefieldTaskDerivedIdentity(
            payloadLabel,
            kind,
            battlefieldObjectId,
            spellDuelId,
            hasSpellDuelIdPayload,
            battleId,
            hasBattleIdPayload,
            errors);
        ValidateBattlefieldTaskKindSpecificIdentityExclusivity(payloadLabel, kind, spellDuelId, battleId, errors);
        ValidateBattlefieldTaskKindConsistency(payloadLabel, kind, status, reason, errors);
    }

    private static void ValidateSpectatorBattlefieldTaskPayloadListValues(
        object? spectatorBattlefieldTask,
        IReadOnlySet<string> seatPlayerIds,
        IReadOnlySet<string> knownObjectIds,
        IReadOnlyDictionary<string, ObjectLocationState> objectLocations,
        IReadOnlyDictionary<string, string> objectControllers,
        IReadOnlyDictionary<string, IReadOnlySet<string>> objectTags,
        IReadOnlyDictionary<string, IReadOnlySet<string>> battlefieldStateOccupantObjectIds,
        IReadOnlySet<string> knownStackItemIds,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing battlefield task item";
        ValidateSpectatorRequiredStringListPayloadShape(
            spectatorBattlefieldTask,
            payloadLabel,
            "participantControllerIds",
            "participant controller id",
            errors);
        ValidateSnapshotPayloadStringListValues(
            spectatorBattlefieldTask,
            "participantControllerIds",
            payloadLabel,
            "participant controller id",
            errors);
        ValidateTimingPlayerReferenceList(
            spectatorBattlefieldTask,
            "participantControllerIds",
            $"{payloadLabel} participant controller id",
            seatPlayerIds,
            "seats",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            spectatorBattlefieldTask,
            payloadLabel,
            "participantObjectIds",
            "participant object id",
            errors);
        ValidateSnapshotPayloadStringListValues(
            spectatorBattlefieldTask,
            "participantObjectIds",
            payloadLabel,
            "participant object id",
            errors);
        ValidateTimingObjectReferenceListWithExpectedDetails(
            spectatorBattlefieldTask,
            "participantObjectIds",
            $"{payloadLabel} participant object id",
            knownObjectIds,
            "object registry",
            errors);
        ValidateBattlefieldTaskParticipantObjectBattlefieldMembership(
            spectatorBattlefieldTask,
            $"{payloadLabel} participant object id",
            objectLocations,
            errors);
        ValidateBattlefieldTaskParticipantObjectUnitMembership(
            spectatorBattlefieldTask,
            $"{payloadLabel} participant object id",
            objectTags,
            errors);
        ValidateBattlefieldTaskParticipantObjectBattlefieldStateOccupantMembership(
            spectatorBattlefieldTask,
            $"{payloadLabel} participant object id",
            battlefieldStateOccupantObjectIds,
            "authoritative state battlefield state occupants",
            errors);
        ValidateBattlefieldTaskParticipantControllerObjectConsistency(
            spectatorBattlefieldTask,
            $"{payloadLabel} participant controller id",
            objectControllers,
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            spectatorBattlefieldTask,
            payloadLabel,
            "stackItemIds",
            "stack item id",
            errors);
        ValidateSnapshotPayloadStringListValues(
            spectatorBattlefieldTask,
            "stackItemIds",
            payloadLabel,
            "stack item id",
            errors);
        ValidateTimingStackItemReferenceList(
            spectatorBattlefieldTask,
            "stackItemIds",
            $"{payloadLabel} stack item id",
            knownStackItemIds,
            "authoritative stack items",
            errors);
    }

    private static bool BattlefieldObjectPairsEqual(
        IReadOnlyList<(string PlayerId, string ObjectId)> left,
        IReadOnlyList<(string PlayerId, string ObjectId)> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index].PlayerId, right[index].PlayerId, StringComparison.Ordinal)
                || !string.Equals(left[index].ObjectId, right[index].ObjectId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateSnapshotPlayerCoverage(
        RecoveredPlayerView view,
        IEnumerable<string> expectedPlayerIds,
        List<string> errors)
    {
        var expectedPlayerIdList = expectedPlayerIds
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var actualPlayerIds = view.Snapshot.Players.Keys
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();

        foreach (var expectedPlayerId in expectedPlayerIdList)
        {
            if (!view.Snapshot.Players.ContainsKey(expectedPlayerId))
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} is missing player {expectedPlayerId}; {FormatExpectedActualForRecovery(expectedPlayerIdList, actualPlayerIds)}");
            }
        }
    }

    private static void ValidateSnapshotActivePlayer(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(view.Snapshot.ActivePlayerId))
        {
            errors.Add($"snapshot for {view.PlayerId} active player is required");
            return;
        }

        if (!view.Snapshot.Players.ContainsKey(view.Snapshot.ActivePlayerId))
        {
            var expectedPlayerIds = view.Snapshot.Players.Keys
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray();
            errors.Add(
                $"snapshot for {view.PlayerId} active player {view.Snapshot.ActivePlayerId} is missing from players; {FormatExpectedActualForRecovery(expectedPlayerIds, view.Snapshot.ActivePlayerId)}");
        }
    }

    private static void ValidatePlayerViewAgreement(
        IReadOnlyDictionary<string, RecoveredPlayerView> playerViews,
        List<string> errors)
    {
        var viewsWithSnapshots = playerViews.Values
            .Where(view => view.Snapshot is not null)
            .ToArray();
        if (viewsWithSnapshots.Length <= 1)
        {
            return;
        }

        var baseline = viewsWithSnapshots
            .OrderBy(view => view.PlayerId, StringComparer.Ordinal)
            .First();
        var baselineSnapshot = baseline.Snapshot;
        var baselineSnapshotTick = baseline.SnapshotTick;
        var baselineSnapshotEventSequence = baseline.SnapshotEventSequence;
        var baselineSeats = ExtractSeats(baselineSnapshot);
        foreach (var view in viewsWithSnapshots)
        {
            if (view.SnapshotTick != baselineSnapshotTick)
            {
                errors.Add($"snapshot for {view.PlayerId} disagrees on row tick");
            }

            if (view.SnapshotEventSequence != baselineSnapshotEventSequence)
            {
                errors.Add($"snapshot for {view.PlayerId} disagrees on event sequence");
            }

            if (view.Snapshot.TurnNumber != baselineSnapshot.TurnNumber)
            {
                errors.Add($"snapshot for {view.PlayerId} disagrees on turn number");
            }

            if (!string.Equals(view.Snapshot.ActivePlayerId, baselineSnapshot.ActivePlayerId, StringComparison.Ordinal))
            {
                errors.Add($"snapshot for {view.PlayerId} disagrees on active player");
            }

            var seats = ExtractSeats(view.Snapshot);
            if (!SeatsEqual(baselineSeats, seats))
            {
                errors.Add($"snapshot for {view.PlayerId} disagrees on player seats");
            }
        }
    }
}

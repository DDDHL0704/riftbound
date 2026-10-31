using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;
internal static partial class ActionPromptBuilder
{

    private static bool PromptHasRequiredDestinationChoices(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        return !behavior.PlaysSourceToBaseAsUnit
            || (PlayCardDestinationChoicesForBehavior(state, playerId, behavior)?.Count ?? 0) > 0;
    }

    private static bool PromptHasRequiredTargetChoices(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        if (!CoreRuleEngine.HasMandatoryPlayAdditionalCostChoices(state, playerId, behavior))
            return false;
        var targetCountConditionApplies = PromptTargetCountConditionApplies(state, playerId, behavior);
        var minTargetCount = PromptMinTargetCount(behavior, targetCountConditionApplies);
        if (minTargetCount == 0)
        {
            return true;
        }

        for (var targetIndex = 0; targetIndex < minTargetCount; targetIndex++)
        {
            if (PromptTargetChoicesForIndex(state, playerId, behavior, targetIndex).Count == 0)
            {
                return false;
            }
        }

        return !PlayCardRequiresServerTargetSelection(state, playerId, behavior)
            || PlayCardLegalTargetSelections(state, playerId, behavior).Count > 0;
    }

    private static bool PlayCardRequiresServerTargetSelection(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        var targetCountConditionApplies = PromptTargetCountConditionApplies(state, playerId, behavior);
        var maxTargetCount = PromptMaxTargetCount(state, playerId, behavior, targetCountConditionApplies);
        if (maxTargetCount == 0)
        {
            return false;
        }

        var choicesByIndex = Enumerable.Range(0, maxTargetCount)
            .Select(targetIndex => PromptTargetChoicesForIndex(state, playerId, behavior, targetIndex)
                .Select(choice => choice.Id)
                .Distinct(StringComparer.Ordinal)
                .ToArray())
            .ToArray();
        return PlayCardHasServerTargetSelectionConstraint(state, playerId, behavior, choicesByIndex);
    }

    private static bool PromptTargetCountConditionApplies(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        return behavior.TargetCountConditionKind switch
        {
            CardTargetCountConditionKinds.None => true,
            CardTargetCountConditionKinds.PlayedAfterAnotherCardThisTurn
                => state.PlayerCardsPlayedThisTurn.TryGetValue(playerId, out var count) && count > 0,
            _ => false
        };
    }

    private static int PromptMinTargetCount(
        CardBehaviorDefinition behavior,
        bool targetCountConditionApplies)
    {
        if (CoreRuleEngine.IsDeferredDeckChoice(behavior) || CoreRuleEngine.HasIndependentPermanentTargets(behavior)) return 0;
        if (!targetCountConditionApplies)
        {
            return 0;
        }

        if (IsAgileDirectPlayAttachRepresentative(behavior))
        {
            return 1;
        }

        return behavior.MinTargetCount < 0 ? behavior.RequiredTargetCount : behavior.MinTargetCount;
    }

    private static int PromptMaxTargetCount(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        bool targetCountConditionApplies)
    {
        if (CoreRuleEngine.IsDeferredDeckChoice(behavior) || CoreRuleEngine.HasIndependentPermanentTargets(behavior)) return 0;
        if (!targetCountConditionApplies)
        {
            return 0;
        }

        if (behavior.TargetEffectAdditionalPowerCost > 0
            && !PromptTargetEffectAdditionalCostAvailable(state, playerId, behavior))
        {
            return 0;
        }

        if (IsAgileDirectPlayAttachRepresentative(behavior))
        {
            return 1;
        }

        if (behavior.AnyNumberOfTargets) return CoreRuleEngine.AnyNumberTargetCount(state, playerId, behavior);

        if (!behavior.UsesFriendlyBattlefieldUnitCountAsMaxTargetCount)
        {
            return behavior.RequiredTargetCount;
        }

        return state.PlayerZones.TryGetValue(playerId, out var zones)
            ? zones.Battlefields.Count(objectId => state.CardObjects.ContainsKey(objectId))
            : 0;
    }

    private static IReadOnlyList<ActionPromptChoiceDto> PromptFriendlyMainDeckLookTargetChoices(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.MainDeck
            .Take(behavior.MainDeckLookCount)
            .Where(objectId => IsPromptKnownCardObject(state, objectId))
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
                && (string.IsNullOrWhiteSpace(behavior.MainDeckTargetRequiredTag)
                    || cardObject.Tags.Contains(behavior.MainDeckTargetRequiredTag, StringComparer.Ordinal)))
            .Select(objectId => ObjectChoice(state, objectId, "己方主牌堆查看候选"))
            .ToArray();
    }

    private static IReadOnlyList<IReadOnlyList<string>> PlayCardLegalTargetSelections(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        // Unbounded grouped targets use the server quote for each submitted set;
        // enumerating every permutation here grows factorially with zero-power units.
        if (behavior.AnyNumberOfTargets) return [];
        var targetCountConditionApplies = PromptTargetCountConditionApplies(state, playerId, behavior);
        var minTargetCount = PromptMinTargetCount(behavior, targetCountConditionApplies);
        var maxTargetCount = PromptMaxTargetCount(state, playerId, behavior, targetCountConditionApplies);
        if (maxTargetCount == 0)
        {
            return [];
        }

        var choicesByIndex = Enumerable.Range(0, maxTargetCount)
            .Select(targetIndex => PromptTargetChoicesForIndex(state, playerId, behavior, targetIndex)
                .Select(choice => choice.Id)
                .Distinct(StringComparer.Ordinal)
                .ToArray())
            .ToArray();
        if (!PlayCardHasServerTargetSelectionConstraint(state, playerId, behavior, choicesByIndex))
        {
            return [];
        }

        var legalSelections = new List<IReadOnlyList<string>>();
        for (var targetCount = minTargetCount; targetCount <= maxTargetCount; targetCount++)
        {
            if (targetCount == 0)
            {
                var emptySelection = Array.Empty<string>();
                if (IsLegalPlayCardTargetSelection(state, playerId, behavior, emptySelection))
                {
                    legalSelections.Add(emptySelection);
                }

                continue;
            }

            if (choicesByIndex.Take(targetCount).Any(choices => choices.Length == 0))
            {
                continue;
            }

            BuildTargetSelections(0, targetCount, []);
        }

        return legalSelections;

        void BuildTargetSelections(int targetIndex, int targetCount, List<string> currentSelection)
        {
            if (targetIndex >= targetCount)
            {
                var selection = currentSelection.ToArray();
                if (IsLegalPlayCardTargetSelection(state, playerId, behavior, selection))
                {
                    legalSelections.Add(selection);
                }

                return;
            }

            foreach (var choiceId in choicesByIndex[targetIndex])
            {
                if (!behavior.AllowsRepeatedTargets
                    && currentSelection.Contains(choiceId, StringComparer.Ordinal))
                {
                    continue;
                }

                currentSelection.Add(choiceId);
                BuildTargetSelections(targetIndex + 1, targetCount, currentSelection);
                currentSelection.RemoveAt(currentSelection.Count - 1);
            }
        }
    }

    private static bool PromptHasValidSwapTargetLocations(
        MatchState state,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds)
    {
        if (!behavior.SwapsTargetLocations
            || targetObjectIds.Count < 2)
        {
            return true;
        }

        if (string.Equals(targetObjectIds[0], targetObjectIds[1], StringComparison.Ordinal)
            || !TryFindLegendActionFieldObjectLocation(state.PlayerZones, targetObjectIds[0], out var firstLocation)
            || !TryFindLegendActionFieldObjectLocation(state.PlayerZones, targetObjectIds[1], out var secondLocation))
        {
            return false;
        }

        return !string.Equals(firstLocation.PlayerId, secondLocation.PlayerId, StringComparison.Ordinal)
            || !string.Equals(firstLocation.Zone, secondLocation.Zone, StringComparison.Ordinal);
    }

    private static IEnumerable<string> PromptTargetCandidateIds(
        MatchState state,
        string playerId,
        string targetScope,
        int targetIndex)
    {
        if (string.Equals(targetScope, CardTargetScopes.StackSpell, StringComparison.Ordinal)
            || (targetScope is CardTargetScopes.FriendlyBattlefieldUnitThenStackSpell or CardTargetScopes.FriendlyUnitThenStackSpell
                && targetIndex > 0))
        {
            return state.StackItems.Select(item => item.StackItemId);
        }

        if (string.Equals(targetScope, CardTargetScopes.FriendlyHandCard, StringComparison.Ordinal)
            || string.Equals(targetScope, CardTargetScopes.FriendlyHandCardThenBattlefieldUnit, StringComparison.Ordinal)
                && targetIndex == 0
            || string.Equals(targetScope, CardTargetScopes.AnyHandCard, StringComparison.Ordinal))
        {
            return state.PlayerZones.TryGetValue(playerId, out var zones) ? zones.Hand : [];
        }

        if (string.Equals(targetScope, CardTargetScopes.FriendlyGraveyardCard, StringComparison.Ordinal))
        {
            return state.PlayerZones.TryGetValue(playerId, out var zones) ? zones.Graveyard : [];
        }

        if (targetScope == CardTargetScopes.AnyGraveyardCard)
        {
            return state.PlayerZones.Values.SelectMany(zones => zones.Graveyard).Distinct(StringComparer.Ordinal);
        }

        if (string.Equals(targetScope, CardTargetScopes.OpponentGraveyardCard, StringComparison.Ordinal))
        {
            return state.PlayerZones
                .Where(entry => !string.Equals(entry.Key, playerId, StringComparison.Ordinal))
                .SelectMany(entry => entry.Value.Graveyard);
        }

        if (string.Equals(targetScope, CardTargetScopes.Legend, StringComparison.Ordinal))
        {
            return state.PlayerZones.Values.SelectMany(zones => zones.LegendZone);
        }

        if (string.Equals(targetScope, CardTargetScopes.FriendlyMainDeckCard, StringComparison.Ordinal)
            || string.Equals(targetScope, CardTargetScopes.OpponentHandCard, StringComparison.Ordinal)
            || string.Equals(targetScope, CardTargetScopes.OpponentMainDeckTopCard, StringComparison.Ordinal)
            || string.Equals(targetScope, CardTargetScopes.AnyMainDeckTopFiveCard, StringComparison.Ordinal)
            || string.Equals(targetScope, CardTargetScopes.SacredJudgmentKeepCard, StringComparison.Ordinal))
        {
            return [];
        }

        return PublicBoardObjects(state);
    }

    private static bool IsPromptBattlefieldObject(MatchState state, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && IsPromptKnownCardObject(state, objectId)
            && state.PlayerZones.Values.Any(zones => zones.Battlefields.Contains(objectId, StringComparer.Ordinal))
            && IsPromptFieldObjectControlledByZonePlayer(state, objectId);
    }

    private static bool IsPromptBaseObject(MatchState state, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && IsPromptKnownCardObject(state, objectId)
            && state.PlayerZones.Values.Any(zones => zones.Base.Contains(objectId, StringComparer.Ordinal))
            && IsPromptFieldObjectControlledByZonePlayer(state, objectId);
    }

    private static bool IsPromptFieldUnitObject(MatchState state, string objectId)
    {
        return (IsPromptBaseObject(state, objectId) || IsPromptBattlefieldObject(state, objectId))
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && FieldObjectTypeRules.IsVisibleUnit(cardObject);
    }

    private static bool IsPromptFieldObjectControlledByZonePlayer(MatchState state, string objectId)
    {
        return TryFindLegendActionFieldObjectLocation(state.PlayerZones, objectId, out var location)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, location.PlayerId);
    }

    private static bool IsPromptFieldUnitObjectControlledByZonePlayer(MatchState state, string objectId)
    {
        return IsPromptFieldUnitObject(state, objectId)
            && IsPromptFieldObjectControlledByZonePlayer(state, objectId);
    }

    private static bool IsPromptPlayCardTargetAllowed(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        string objectId,
        int targetIndex)
    {
        return IsPromptEffectSpecificTargetAllowed(state, playerId, behavior, objectId)
            && TemporaryUnitEffectRules.ThreatAllowsTarget(state, playerId, objectId, behavior)
            && TargetProtectionRules.IsLegalPlayCardSpellOrSkillTarget(state, playerId, behavior, objectId)
            && IsPromptTargetRequiredTagAllowed(state, objectId, behavior)
            && IsPromptTargetForbiddenTagAllowed(state, objectId, behavior)
            && IsPromptTargetPowerAllowed(state, objectId, behavior);
    }

    private static bool IsPromptEffectSpecificTargetAllowed(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        string objectId)
    {
        if (RequiresVisibleFieldUnitPrimitiveTarget(behavior))
        {
            return IsPromptFieldUnitObjectControlledByZonePlayer(state, objectId);
        }

        return true;
    }

    private static bool RequiresVisibleFieldUnitPrimitiveTarget(CardBehaviorDefinition behavior)
    {
        return BehaviorUsesVisibleFieldUnitPrimitive(behavior)
            && IsVisibleFieldUnitPrimitiveTargetScope(behavior.TargetScope);
    }

    private static bool BehaviorUsesVisibleFieldUnitPrimitive(CardBehaviorDefinition behavior)
    {
        return behavior.MovesTargetToBase
            || behavior.ReturnsTargetToHand
            || behavior.DestroysTarget
            || behavior.GainsControlOfTargetToBase
            || behavior.GainsControlOfTargetToBattlefield
            || behavior.SwapsTargetPowersUntilEndOfTurn
            || behavior.ExhaustsTarget
            || !string.IsNullOrWhiteSpace(behavior.StatusEffectId)
            || !string.IsNullOrWhiteSpace(behavior.SecondaryStatusEffectId);
    }

    private static bool IsPromptTargetRequiredTagAllowed(
        MatchState state,
        string objectId,
        CardBehaviorDefinition behavior)
    {
        return string.IsNullOrWhiteSpace(behavior.TargetRequiredTag)
            || PromptCardObjectHasTags(state.CardObjects, objectId, behavior.TargetRequiredTag);
    }

    private static bool IsPromptTargetForbiddenTagAllowed(
        MatchState state,
        string objectId,
        CardBehaviorDefinition behavior)
    {
        return string.IsNullOrWhiteSpace(behavior.TargetForbiddenTag)
            || !PromptCardObjectHasTags(state.CardObjects, objectId, behavior.TargetForbiddenTag);
    }

    private static bool IsPromptTargetPowerAllowed(
        MatchState state,
        string objectId,
        CardBehaviorDefinition behavior)
    {
        var maxTargetPower = behavior.MaxTargetPower > 0
            ? behavior.MaxTargetPower
            : behavior.MaxTotalTargetPower;
        return maxTargetPower <= 0
            || (state.CardObjects.TryGetValue(objectId, out var targetState)
                && CoreRuleEngine.ResolveCurrentFieldUnitPower(state, targetState) <= maxTargetPower);
    }

    private static bool IsPromptControlledFieldObject(MatchState state, string playerId, string objectId)
    {
        return IsPromptFieldUnitObject(state, objectId)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            && TryFindLegendActionFieldObjectLocation(state.PlayerZones, objectId, out var location)
            && string.Equals(location.PlayerId, playerId, StringComparison.Ordinal);
    }

    private static bool IsPromptEnemyFieldObject(MatchState state, string playerId, string objectId)
    {
        return IsPromptFieldUnitObject(state, objectId)
            && IsPromptOpponentControlledFieldObject(state, playerId, objectId);
    }

    private static bool IsPromptControlledBattlefieldObject(MatchState state, string playerId, string objectId)
    {
        return IsPromptBattlefieldObject(state, objectId)
            && IsPromptControlledFieldObject(state, playerId, objectId);
    }

    private static bool IsPromptEnemyBattlefieldObject(MatchState state, string playerId, string objectId)
    {
        return IsPromptBattlefieldObject(state, objectId)
            && IsPromptEnemyFieldObject(state, playerId, objectId);
    }

    private static bool IsPromptControlledBaseObject(MatchState state, string playerId, string objectId)
    {
        return IsPromptBaseObject(state, objectId)
            && IsPromptControlledFieldObject(state, playerId, objectId);
    }

    private static bool IsPromptEquipmentObject(MatchState state, string objectId)
    {
        return (IsPromptBaseObject(state, objectId) || IsPromptBattlefieldObject(state, objectId))
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
            && !cardObject.IsFaceDown;
    }

    private static bool IsPromptLegendObject(MatchState state, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && IsPromptKnownCardObject(state, objectId)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && state.PlayerZones.Any(entry => entry.Value.LegendZone.Contains(objectId, StringComparer.Ordinal)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, entry.Key));
    }

    private static bool IsPromptFriendlyEquipmentObject(MatchState state, string playerId, string objectId)
    {
        return IsPromptEquipmentObject(state, objectId)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            && TryFindLegendActionFieldObjectLocation(state.PlayerZones, objectId, out var location)
            && string.Equals(location.PlayerId, playerId, StringComparison.Ordinal);
    }

    private static bool IsPromptEnemyEquipmentObject(MatchState state, string playerId, string objectId)
    {
        return IsPromptEquipmentObject(state, objectId)
            && IsPromptOpponentControlledFieldObject(state, playerId, objectId);
    }

    private static bool IsPromptOpponentControlledFieldObject(MatchState state, string playerId, string objectId)
    {
        return TryFindLegendActionFieldObjectLocation(state.PlayerZones, objectId, out var location)
            && !string.Equals(location.PlayerId, playerId, StringComparison.Ordinal)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, location.PlayerId);
    }

    private static bool IsPromptFriendlyHandCard(MatchState state, string playerId, string objectId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Hand.Contains(objectId, StringComparer.Ordinal)
            && IsPromptKnownCardObjectControlledByPlayerOrLegacyOwned(state, playerId, objectId);
    }

    private static bool IsPromptHandCardControlledByPlayerOrLegacyOwned(
        MatchState state,
        string playerId,
        string objectId)
    {
        return !state.CardObjects.TryGetValue(objectId, out var cardObject)
            || SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId);
    }

    private static bool IsPromptFriendlyGraveyardCard(MatchState state, string playerId, string objectId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Graveyard.Contains(objectId, StringComparer.Ordinal)
            && IsPromptKnownCardObjectControlledByPlayerOrLegacyOwned(state, playerId, objectId);
    }

    private static bool IsPromptOpponentGraveyardCard(MatchState state, string playerId, string objectId)
    {
        return state.PlayerZones
            .Where(entry => !string.Equals(entry.Key, playerId, StringComparison.Ordinal))
            .Any(entry => entry.Value.Graveyard.Contains(objectId, StringComparer.Ordinal)
                && IsPromptKnownCardObjectControlledByPlayerOrLegacyOwned(state, entry.Key, objectId));
    }

    private static bool IsPromptKnownCardObject(MatchState state, string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo);
    }

    private static bool IsPromptKnownCardObjectControlledByPlayerOrLegacyOwned(
        MatchState state,
        string playerId,
        string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId);
    }

    private static bool IsPromptAttackingBattlefieldObject(MatchState state, string objectId)
    {
        return IsPromptBattlefieldObject(state, objectId)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.IsAttacking;
    }

    private static bool IsPromptStackSpellItem(MatchState state, string objectId)
    {
        return state.StackItems.Any(item => string.Equals(item.StackItemId, objectId, StringComparison.Ordinal));
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? TargetsFor(
        MatchState state,
        string playerId,
        string action)
    {
        return action switch
        {
            "PLAY_CARD" => PlayCardTargetChoices(state, playerId),
            "ACTIVATE_ABILITY" => ActivateAbilitySourceRequirements(state, playerId)
                .SelectMany(requirement => requirement.TargetChoicesByIndex.Values)
                .SelectMany(choices => choices)
                .GroupBy(choice => choice.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray(),
            "ASSEMBLE_EQUIPMENT" => AssembleEquipmentSourceRequirements(state, playerId)
                .SelectMany(requirement => requirement.TargetChoices)
                .GroupBy(choice => choice.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray(),
            "REVEAL_CARD" => RevealCardSourceRequirements(state, playerId)
                .SelectMany(requirement => requirement.TargetChoicesByIndex.Values)
                .SelectMany(choices => choices)
                .GroupBy(choice => choice.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray(),
            "DECLARE_BATTLE" => DeclareBattleSourceRequirements(state, playerId)
                .SelectMany(requirement => requirement.TargetChoicesByIndex.Values)
                .SelectMany(choices => choices)
                .GroupBy(choice => choice.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray(),
            "LEGEND_ACT" => LegendActionSourceRequirements(state, playerId)
                .SelectMany(requirement => requirement.TargetChoicesByIndex.Values)
                .SelectMany(choices => choices)
                .GroupBy(choice => choice.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray(),
            _ => null
        };
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? PlayCardTargetChoices(MatchState state, string playerId)
    {
        var choices = PlayablePlayCardBehaviors(state, playerId)
            .SelectMany(behavior => PlayCardTopLevelTargetChoicesForBehavior(state, playerId, behavior))
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IEnumerable<ActionPromptChoiceDto> PlayCardTopLevelTargetChoicesForBehavior(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        var targetCountConditionApplies = PromptTargetCountConditionApplies(state, playerId, behavior);
        var maxTargetCount = PromptMaxTargetCount(state, playerId, behavior, targetCountConditionApplies);
        var choicesByIndex = Enumerable.Range(0, maxTargetCount)
            .Select(targetIndex => PromptTargetChoicesForIndex(state, playerId, behavior, targetIndex))
            .ToArray();
        var choiceIdsByIndex = choicesByIndex
            .Select(choices => choices
                .Select(choice => choice.Id)
                .Distinct(StringComparer.Ordinal)
                .ToArray())
            .ToArray();
        if (!PlayCardHasServerTargetSelectionConstraint(state, playerId, behavior, choiceIdsByIndex))
        {
            return choicesByIndex.SelectMany(choices => choices);
        }

        var legalTargetIds = PlayCardLegalTargetSelections(state, playerId, behavior)
            .SelectMany(selection => selection)
            .ToHashSet(StringComparer.Ordinal);
        return choicesByIndex
            .SelectMany(choices => choices)
            .Where(choice => legalTargetIds.Contains(choice.Id));
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? DestinationsFor(
        MatchState state,
        string playerId,
        string action)
    {
        return action switch
        {
            "PLAY_CARD" => PlayCardDestinationChoices(state, playerId),
            "HIDE_CARD" => [
                .. HideCardSourceRequirements(state, playerId)
                    .SelectMany(requirement => requirement.DestinationChoices)
                    .GroupBy(choice => choice.Id, StringComparer.Ordinal)
                    .Select(group => group.First())
            ],
            "REVEAL_CARD" => [
                .. RevealCardSourceRequirements(state, playerId)
                    .SelectMany(requirement => requirement.DestinationChoices)
                    .GroupBy(choice => choice.Id, StringComparer.Ordinal)
                    .Select(group => group.First())
            ],
            "MOVE_UNIT" => MoveUnitDestinationChoices(state, playerId),
            "DECLARE_BATTLE" => DeclareBattleDestinationChoices(state, playerId),
            _ => null
        };
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? MoveUnitDestinationChoices(
        MatchState state,
        string playerId)
    {
        var choices = MoveUnitSourceRequirements(state, playerId)
            .SelectMany(requirement => requirement.DestinationChoices)
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? PlayCardDestinationChoices(MatchState state, string playerId)
    {
        var choices = PlayablePlayCardBehaviors(state, playerId)
            .SelectMany(behavior => PlayCardDestinationChoicesForBehavior(state, playerId, behavior) ?? [])
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? DeclareBattleDestinationChoices(MatchState state, string playerId)
    {
        if (ResolutionResult.ActiveStartBattleTask(state) is { BattlefieldObjectId.Length: > 0 } activeStartBattleTask)
        {
            return IsPromptBattlefieldCardObject(state, activeStartBattleTask.BattlefieldObjectId)
                ? [ObjectChoice(state, activeStartBattleTask.BattlefieldObjectId, "当前争夺战场战斗任务")]
                : null;
        }

        var choices = new[]
            {
                new ActionPromptChoiceDto($"BATTLEFIELD:{playerId}-MAIN", "己方主战场", "默认战斗战场")
            }
            .Concat(PublicBattlefieldCardObjects(state)
                .Select(objectId => ObjectChoice(state, objectId, "公开战场牌")))
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? ModesFor(
        MatchState state,
        string playerId,
        string action)
    {
        return action switch
        {
            "PLAY_CARD" => PlayCardModeChoices(state, playerId),
            "REVEAL_CARD" => RevealCardModeChoices(state, playerId),
            "ACTIVATE_ABILITY" => ActivateAbilityModeChoices(state, playerId),
            "LEGEND_ACT" => LegendActionModeChoices(state, playerId),
            _ => null
        };
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? PlayCardModeChoices(MatchState state, string playerId)
    {
        var choices = PlayablePlayCardBehaviors(state, playerId)
            .Select(behavior => behavior.Mode)
            .Where(mode => !string.IsNullOrWhiteSpace(mode))
            .Distinct(StringComparer.Ordinal)
            .Select(mode => new ActionPromptChoiceDto(mode, PlayCardModeLabel(mode)))
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? RevealCardModeChoices(MatchState state, string playerId)
    {
        var choices = RevealCardSourceRequirements(state, playerId)
            .GroupBy(requirement => requirement.Mode, StringComparer.Ordinal)
            .Select(group => new ActionPromptChoiceDto(
                group.Key,
                group.First().ModeLabel,
                "server-filtered standby reveal mode"))
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? ActivateAbilityModeChoices(MatchState state, string playerId)
    {
        var choices = ActivateAbilitySourceRequirements(state, playerId)
            .GroupBy(requirement => requirement.AbilityInstanceId ?? requirement.AbilityId, StringComparer.Ordinal)
            .Select(group => new ActionPromptChoiceDto(
                group.Key,
                group.First().AbilityLabel,
                "server-filtered implemented ability"))
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? LegendActionModeChoices(MatchState state, string playerId)
    {
        var choices = LegendActionSourceRequirements(state, playerId)
            .GroupBy(requirement => requirement.AbilityId, StringComparer.Ordinal)
            .Select(group => new ActionPromptChoiceDto(
                group.Key,
                group.First().AbilityLabel,
                "server-filtered implemented legend ability"))
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? OptionalCostsFor(
        MatchState state,
        string playerId,
        string action)
    {
        return action switch
        {
            "PLAY_CARD" => PlayCardOptionalCostChoices(state, playerId),
            "HIDE_CARD" => HideCardOptionalCostChoices(state, playerId),
            "REVEAL_CARD" => RevealCardOptionalCostChoices(state, playerId),
            "MOVE_UNIT" => MoveUnitOptionalCostChoices(state, playerId),
            "ASSEMBLE_EQUIPMENT" => AssembleEquipmentOptionalCostChoices(state, playerId),
            "DECLARE_BATTLE" => DeclareBattleOptionalCostChoices(state, playerId),
            "ACTIVATE_ABILITY" => ActivateAbilityOptionalCostChoices(state, playerId),
            "LEGEND_ACT" => LegendActionOptionalCostChoices(state, playerId),
            _ => null
        };
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? MoveUnitOptionalCostChoices(
        MatchState state,
        string playerId)
    {
        var choices = MoveUnitSourceRequirements(state, playerId)
            .SelectMany(requirement => requirement.OptionalCostChoices)
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? AssembleEquipmentOptionalCostChoices(
        MatchState state,
        string playerId)
    {
        var choices = AssembleEquipmentSourceRequirements(state, playerId)
            .SelectMany(requirement => requirement.OptionalCostChoices)
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? DeclareBattleOptionalCostChoices(
        MatchState state,
        string playerId)
    {
        return DeclareBattleSourceRequirements(state, playerId).Count > 0
            ? DeclareBattleRequiredOptionalCostChoices()
                .ToArray()
            : null;
    }

    private static IReadOnlyList<ActionPromptChoiceDto> DeclareBattleRequiredOptionalCostChoices()
    {
        return [new ActionPromptChoiceDto("COMBAT_ASSIGNMENT", "战斗分配", "服务端当前代表路径必需")];
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? LegendActionOptionalCostChoices(
        MatchState state,
        string playerId)
    {
        var choices = LegendActionSourceRequirements(state, playerId)
            .SelectMany(requirement => requirement.OptionalCostChoices)
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? PlayCardOptionalCostChoices(MatchState state, string playerId)
    {
        var choices = PlayablePlayCardBehaviorSources(state, playerId)
            .SelectMany(entry => PlayCardOptionalCostChoicesForBehavior(
                state,
                playerId,
                entry.Behavior,
                entry.SourceObjectId))
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<DeclareBattlePromptRequirement> DeclareBattleSourceRequirements(
        MatchState state,
        string playerId)
    {
        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || !string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || !string.Equals(state.ActivePlayerId, playerId, StringComparison.Ordinal)
            || state.StackItems.Count > 0
            || !state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        // A battle needs opposing units at one physical battlefield (CN 461).
        // Abstract legacy fixtures without physical occupancy retain their compatibility path.
        if (state.BattlefieldStates.Values.Any(field => field.OccupantObjectIds.Count > 0)
            && state.BattlefieldStates.Values.All(field => !field.Contested)
            && state.PlayerZones.Values.SelectMany(zones => zones.Battlefields)
                .Where(id => state.CardObjects.TryGetValue(id, out var card) && card.Tags.Contains(CardObjectTags.UnitCard))
                .All(id => state.ObjectLocations.TryGetValue(id, out var location)
                    && !string.IsNullOrWhiteSpace(location.BattlefieldObjectId)))
        {
            return [];
        }

        var activeStartBattleTask = ResolutionResult.ActiveStartBattleTask(state);
        var activeBattlefieldObjectId = activeStartBattleTask?.BattlefieldObjectId ?? string.Empty;
        var defenderCandidates = OpposingBattlefieldObjects(state, playerId)
            .Where(entry => string.IsNullOrWhiteSpace(activeBattlefieldObjectId)
                || IsObjectLocatedAtBattlefield(state, entry.ObjectId, activeBattlefieldObjectId))
            .Where(entry => IsFaceUpBattlefieldUnitForBattle(state, entry.PlayerId, entry.ObjectId))
            .Select(entry => new
            {
                entry.ObjectId,
                Choice = ObjectChoice(state, entry.ObjectId, "服务端合法防守单位")
            })
            .ToArray();
        var defenderChoices = defenderCandidates
            .Select(entry => entry.Choice)
            .ToArray();
        var battlefieldChoices = string.IsNullOrWhiteSpace(activeBattlefieldObjectId)
            ? DeclareBattleDestinationChoices(state, playerId)?.ToArray() ?? []
            : IsPromptBattlefieldCardObject(state, activeBattlefieldObjectId)
                ? [ObjectChoice(state, activeBattlefieldObjectId, "当前争夺战场战斗任务")]
                : [];
        if (defenderChoices.Length == 0 || battlefieldChoices.Length == 0)
        {
            return [];
        }

        var attackerCandidates = zones.Battlefields
            .Where(objectId => string.IsNullOrWhiteSpace(activeBattlefieldObjectId)
                || IsObjectLocatedAtBattlefield(state, objectId, activeBattlefieldObjectId))
            .Where(objectId => IsFaceUpBattlefieldUnitForBattle(state, playerId, objectId))
            .Where(objectId => state.CardObjects.ContainsKey(objectId))
            .Select(objectId => new
            {
                ObjectId = objectId,
                CardObject = state.CardObjects[objectId],
                Choice = ObjectChoice(state, objectId, "服务端合法攻击单位")
            })
            .ToArray();

        return attackerCandidates
            .Select(attacker =>
            {
                var alternateAttackerChoices = attackerCandidates
                    .Where(candidate => !string.Equals(candidate.ObjectId, attacker.ObjectId, StringComparison.Ordinal))
                    .Select(candidate => candidate.Choice)
                    .ToArray();
                var maxAttackerCount = attackerCandidates.Length;
                var allParticipantsRequired = !string.IsNullOrWhiteSpace(activeBattlefieldObjectId);
                var attackerChoicesByIndex = new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal)
                {
                    ["0"] = [attacker.Choice]
                };
                for (var index = 1; index < maxAttackerCount; index++)
                {
                    attackerChoicesByIndex[index.ToString(System.Globalization.CultureInfo.InvariantCulture)] = alternateAttackerChoices;
                }

                var maxDefenderCount = defenderChoices.Length;
                var targetChoicesByIndex = new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal)
                {
                    ["0"] = defenderChoices
                };
                for (var index = 1; index < maxDefenderCount; index++)
                {
                    targetChoicesByIndex[index.ToString(System.Globalization.CultureInfo.InvariantCulture)] = defenderChoices;
                }
                var triggerBattlefieldObjectId = DeclareBattleTriggerBattlefieldObjectId(
                    state,
                    activeBattlefieldObjectId,
                    attacker.ObjectId);
                var defenderObjectIds = defenderCandidates.Select(candidate => candidate.ObjectId).ToArray();
                var defendingPlayerId = DeclareBattleDefendingPlayerId(state, defenderObjectIds);
                var battlefieldDefendEffectTargetChoicesByIndex = DeclareBattleBattlefieldDefendEffectTargetChoicesByIndex(
                    state,
                    defendingPlayerId,
                    triggerBattlefieldObjectId,
                    defenderObjectIds);
                var defendTriggerTargetChoicesByIndex = DeclareBattleDefendTriggerTargetChoicesByIndex(
                    state,
                    playerId,
                    triggerBattlefieldObjectId,
                    defenderObjectIds);
                var battlefieldTargetChoicesByIndex = MergeDeclareBattlefieldTargetChoicesByIndex(
                    battlefieldDefendEffectTargetChoicesByIndex,
                    defendTriggerTargetChoicesByIndex);
                var minBattlefieldTargetCount = battlefieldTargetChoicesByIndex.Count;
                var maxBattlefieldTargetCount = battlefieldTargetChoicesByIndex.Count;

                var paymentResourceChoices = DeclareBattleHeldScorePaymentResourceChoices(state, battlefieldChoices);
                var paymentResourcePowerByChoice = DeclareBattleHeldScorePaymentResourcePowerByChoice(
                    state,
                    paymentResourceChoices);

                return new DeclareBattlePromptRequirement(
                    attacker.ObjectId,
                    attacker.CardObject.CardNo ?? string.Empty,
                    attacker.Choice.Label,
                    allParticipantsRequired ? maxAttackerCount : 1,
                    maxAttackerCount,
                    allParticipantsRequired ? $"全部 {maxAttackerCount} 名进攻单位" : $"最多 {maxAttackerCount} 名进攻单位",
                    attackerChoicesByIndex,
                    allParticipantsRequired ? maxDefenderCount : 1,
                    maxDefenderCount,
                    allParticipantsRequired ? $"全部 {maxDefenderCount} 名防守单位" : $"最多 {maxDefenderCount} 名防守单位",
                    targetChoicesByIndex,
                    minBattlefieldTargetCount,
                    maxBattlefieldTargetCount,
                    battlefieldDefendEffectTargetChoicesByIndex.Count == 0 && defendTriggerTargetChoicesByIndex.Count == 1
                        ? CardTargetScopes.EnemyUnitAtSourceBattlefield
                        : string.Empty,
                    battlefieldTargetChoicesByIndex,
                    battlefieldChoices,
                    DeclareBattleRequiredOptionalCostChoices()
                                .ToArray(),
                    paymentResourceChoices,
                    paymentResourcePowerByChoice,
                    DeclareBattleHeldScoreAvailablePower(state, battlefieldChoices),
                    DeclareBattleHeldScoreAvailablePowerWithPaymentResources(
                        state,
                        battlefieldChoices,
                        paymentResourceChoices),
                    ["COMBAT_ASSIGNMENT"],
                    true,
                    null);
            })
            .ToArray();
    }

    private static string DeclareBattleDefendingPlayerId(MatchState state, IReadOnlyList<string> defenderObjectIds)
    {
        foreach (var defenderObjectId in defenderObjectIds)
        {
            if (state.CardObjects.TryGetValue(defenderObjectId, out var defenderState))
            {
                if (!string.IsNullOrWhiteSpace(defenderState.ControllerId))
                {
                    return defenderState.ControllerId;
                }

                if (!string.IsNullOrWhiteSpace(defenderState.OwnerId))
                {
                    return defenderState.OwnerId;
                }
            }

            if (state.ObjectLocations.TryGetValue(defenderObjectId, out var location)
                && !string.IsNullOrWhiteSpace(location.PlayerId))
            {
                return location.PlayerId;
            }
        }

        return string.Empty;
    }

    private static string DeclareBattleTriggerBattlefieldObjectId(
        MatchState state,
        string activeBattlefieldObjectId,
        string attackerObjectId)
    {
        if (!string.IsNullOrWhiteSpace(activeBattlefieldObjectId))
        {
            return activeBattlefieldObjectId;
        }

        return state.ObjectLocations.TryGetValue(attackerObjectId, out var location)
            && string.Equals(location.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            ? location.BattlefieldObjectId ?? string.Empty
            : string.Empty;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> DeclareBattleBattlefieldDefendEffectTargetChoicesByIndex(
        MatchState state,
        string defendingPlayerId,
        string battlefieldObjectId,
        IReadOnlyList<string> defenderObjectIds)
    {
        if (string.IsNullOrWhiteSpace(defendingPlayerId)
            || string.IsNullOrWhiteSpace(battlefieldObjectId)
            || !state.CardObjects.TryGetValue(battlefieldObjectId, out var battlefieldState)
            || !IsPromptBattlefieldCardObject(battlefieldState)
            || !SourceObjectControlledByPlayerOrLegacyOwned(battlefieldState, defendingPlayerId)
            || battlefieldState.CardNo == "OGN·279/298"
            || !BattlefieldTriggerSpecRules.TryGetTrigger(
                battlefieldState.CardNo,
                BattlefieldTriggerSpecRules.IsDeclareBattlePromptDefenderUnitTargetBattlefieldTrigger,
                out var trigger))
        {
            return new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal);
        }

        var choices = DeclareBattleBattlefieldDefendEffectTargetObjectIds(
                state,
                defendingPlayerId,
                battlefieldObjectId,
                defenderObjectIds,
                trigger.TargetScope ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .Select(objectId => ObjectChoice(state, objectId, "战场防守效果防守单位"))
            .ToArray();

        return choices.Length == 0
            ? new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal)
            : new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal)
            {
                ["0"] = choices
            };
    }

    private static IEnumerable<string> DeclareBattleBattlefieldDefendEffectTargetObjectIds(
        MatchState state,
        string defendingPlayerId,
        string battlefieldObjectId,
        IReadOnlyList<string> defenderObjectIds,
        string targetScope)
    {
        if (string.Equals(targetScope, TriggerTargetScopes.DefenderUnitAtThisBattlefield, StringComparison.Ordinal))
        {
            return defenderObjectIds
                .Where(objectId => state.CardObjects.TryGetValue(objectId, out var defenderState)
                    && defenderState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                    && SourceObjectControlledByPlayerOrLegacyOwned(defenderState, defendingPlayerId));
        }

        if (string.Equals(targetScope, TriggerTargetScopes.FriendlyUnitAtThisBattlefield, StringComparison.Ordinal))
        {
            return state.ObjectLocations
                .Where(entry => string.Equals(entry.Value.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
                    && string.Equals(entry.Value.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal))
                .Select(entry => entry.Key)
                .Where(objectId => state.CardObjects.TryGetValue(objectId, out var objectState)
                    && objectState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                    && SourceObjectControlledByPlayerOrLegacyOwned(objectState, defendingPlayerId));
        }

        return [];
    }

    // Unit defend targets are confirmed independently by the defender when the role is acquired.
    private static IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> DeclareBattleDefendTriggerTargetChoicesByIndex(
        MatchState state, string playerId, string battlefieldObjectId, IReadOnlyList<string> defenderObjectIds)
        => new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> MergeDeclareBattlefieldTargetChoicesByIndex(
        params IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>[] choiceGroups)
    {
        var merged = new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal);
        var index = 0;
        foreach (var choiceGroup in choiceGroups)
        {
            foreach (var choices in choiceGroup
                .OrderBy(entry => int.TryParse(entry.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : int.MaxValue)
                .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => entry.Value)
                .Where(choices => choices.Count > 0))
            {
                merged[index.ToString(CultureInfo.InvariantCulture)] = choices;
                index++;
            }
        }

        return merged;
    }

    private static IReadOnlyList<ActionPromptChoiceDto> DeclareBattleHeldScorePaymentResourceChoices(
        MatchState state,
        IReadOnlyList<ActionPromptChoiceDto> battlefieldChoices)
    {
        var choices = new Dictionary<string, ActionPromptChoiceDto>(StringComparer.Ordinal);
        foreach (var battlefieldChoice in DeclareBattleHeldScoreBattlefieldChoices(state, battlefieldChoices))
        {
            var battlefieldObjectId = battlefieldChoice.Id;
            if (!state.CardObjects.TryGetValue(battlefieldObjectId, out var battlefieldState)
                || !BattlefieldTriggerSpecRules.TryGetTrigger(
                    battlefieldState.CardNo,
                    BattlefieldTriggerSpecRules.IsBattlefieldHeldPayPowerScoreTrigger,
                    out var trigger)
                || trigger.PowerCost.GetValueOrDefault() <= 0)
            {
                continue;
            }
            var powerCost = trigger.PowerCost.GetValueOrDefault();

            var paymentPlayerId = ResolveBattlefieldPaymentControllerId(state, battlefieldObjectId, battlefieldState);
            if (string.IsNullOrWhiteSpace(paymentPlayerId)
                || state.UntilEndOfTurnEffects.Contains(
                    BattlefieldTaskMarkers.ScoreGainedThisTurn(battlefieldObjectId, paymentPlayerId),
                    StringComparer.Ordinal))
            {
                continue;
            }

            var runePool = state.RunePools.TryGetValue(paymentPlayerId, out var currentPool)
                ? currentPool
                : RunePool.Empty;
            if (PaymentCostRules.CanPayPowerCost(
                    runePool,
                    powerCost,
                    new Dictionary<string, int>(StringComparer.Ordinal)))
            {
                continue;
            }

            var recycleChoices = DeclareBattleHeldScoreRecycleRuneChoices(state, paymentPlayerId, runePool, powerCost);
            foreach (var choice in recycleChoices)
            {
                choices.TryAdd(choice.Id, choice);
            }

            var runePoolWithRecycleChoices = ApplyPromptRecycleRuneChoicesToPool(
                state,
                runePool,
                recycleChoices);
        }

        return choices.Values.OrderBy(choice => choice.Id, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<ActionPromptChoiceDto> DeclareBattleHeldScoreBattlefieldChoices(
        MatchState state,
        IReadOnlyList<ActionPromptChoiceDto> battlefieldChoices)
    {
        var directChoices = battlefieldChoices
            .Where(choice => state.CardObjects.ContainsKey(choice.Id));
        return directChoices
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static IReadOnlyList<ActionPromptChoiceDto> DeclareBattleHeldScoreRecycleRuneChoices(
        MatchState state,
        string paymentPlayerId,
        RunePool runePool,
        int powerCost)
    {
        if (!state.PlayerZones.TryGetValue(paymentPlayerId, out var zones))
        {
            return [];
        }

        return zones.Base
            .Where(objectId => IsRecycleRuneSource(state, paymentPlayerId, objectId))
            .Where(objectId => RecycleRuneCanHelpGenericPowerCost(
                state,
                objectId,
                runePool,
                powerCost))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .Select(objectId =>
            {
                var choice = ObjectChoice(state, objectId, "payment resource action: recycle rune for battlefield held score power");
                return new ActionPromptChoiceDto(
                    $"{RecycleRunePaymentOptionalCostPrefix}{objectId}",
                    $"回收符文支付：{choice.Label}",
                    choice.Reason);
            })
            .ToArray();
    }

    private static RunePool ApplyPromptRecycleRuneChoicesToPool(
        MatchState state,
        RunePool runePool,
        IReadOnlyList<ActionPromptChoiceDto> recycleChoices)
    {
        var powerByTrait = runePool.PowerByTrait.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        foreach (var choice in recycleChoices)
        {
            if (!choice.Id.StartsWith(RecycleRunePaymentOptionalCostPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var objectId = choice.Id[RecycleRunePaymentOptionalCostPrefix.Length..];
            if (!state.CardObjects.TryGetValue(objectId, out var runeState)
                || !TryGetRuneTrait(runeState, out var runeTrait))
            {
                continue;
            }

            powerByTrait[runeTrait] = powerByTrait.TryGetValue(runeTrait, out var current)
                ? current + BasicRuneRecyclePowerGain
                : BasicRuneRecyclePowerGain;
        }

        return runePool with
        {
            PowerByTrait = powerByTrait
        };
    }

    private static bool RecycleRuneCanHelpGenericPowerCost(
        MatchState state,
        string runeObjectId,
        RunePool runePool,
        int genericPowerCost)
    {
        return !PaymentCostRules.CanPayPowerCost(
                runePool,
                genericPowerCost,
                new Dictionary<string, int>(StringComparer.Ordinal))
            && state.CardObjects.TryGetValue(runeObjectId, out var runeState)
            && TryGetRuneTrait(runeState, out _);
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> DeclareBattleHeldScorePaymentResourcePowerByChoice(
        MatchState state,
        IReadOnlyList<ActionPromptChoiceDto> choices)
    {
        var metadata = choices.ToDictionary(
            choice => choice.Id,
            choice =>
            {
                var trait = string.Empty;
                if (choice.Id.StartsWith(RecycleRunePaymentOptionalCostPrefix, StringComparison.Ordinal))
                {
                    var objectId = choice.Id[RecycleRunePaymentOptionalCostPrefix.Length..];
                    if (state.CardObjects.TryGetValue(objectId, out var runeState)
                        && TryGetRuneTrait(runeState, out var runeTrait))
                    {
                        trait = runeTrait;
                    }
                }

                return (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["trait"] = trait,
                    ["power"] = BasicRuneRecyclePowerGain
                };
            },
            StringComparer.Ordinal);

        return metadata;
    }

    private static int DeclareBattleHeldScoreAvailablePower(
        MatchState state,
        IReadOnlyList<ActionPromptChoiceDto> battlefieldChoices)
    {
        return DeclareBattleHeldScoreBattlefieldChoices(state, battlefieldChoices)
            .Select(choice => state.CardObjects.TryGetValue(choice.Id, out var battlefieldState)
                ? ResolveBattlefieldPaymentControllerId(state, choice.Id, battlefieldState)
                : string.Empty)
            .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
            .Select(playerId => state.RunePools.TryGetValue(playerId, out var runePool) ? runePool.TotalPower : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static int DeclareBattleHeldScoreAvailablePowerWithPaymentResources(
        MatchState state,
        IReadOnlyList<ActionPromptChoiceDto> battlefieldChoices,
        IReadOnlyList<ActionPromptChoiceDto> paymentResourceChoices)
    {
        return DeclareBattleHeldScoreAvailablePower(state, battlefieldChoices)
            + DeclareBattleHeldScorePaymentResourcePowerByChoice(state, paymentResourceChoices)
                .Values
                .Sum(metadata =>
                {
                    var genericPower = metadata.TryGetValue("power", out var power) && power is int amount ? amount : 0;
                    var typedPower = metadata.TryGetValue("powerByTrait", out var powerByTrait)
                        && powerByTrait is IReadOnlyDictionary<string, int> typedPowerByTrait
                            ? typedPowerByTrait.Values.Sum()
                            : 0;
                    return genericPower + typedPower;
                });
    }

    private static string ResolveBattlefieldPaymentControllerId(
        MatchState state,
        string battlefieldObjectId,
        CardObjectState battlefieldState)
    {
        if (!string.IsNullOrWhiteSpace(battlefieldState.ControllerId))
        {
            return battlefieldState.ControllerId;
        }

        if (!string.IsNullOrWhiteSpace(battlefieldState.OwnerId))
        {
            return battlefieldState.OwnerId;
        }

        return state.PlayerZones
            .FirstOrDefault(entry => entry.Value.Battlefields.Contains(battlefieldObjectId, StringComparer.Ordinal))
            .Key ?? string.Empty;
    }

    private static bool IsObjectLocatedAtBattlefield(
        MatchState state,
        string objectId,
        string battlefieldObjectId)
    {
        return state.ObjectLocations.TryGetValue(objectId, out var location)
            && string.Equals(location.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
            && string.Equals(location.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal);
    }

    private static IEnumerable<CardBehaviorDefinition> PlayablePlayCardBehaviors(MatchState state, string playerId)
    {
        return PlayablePlayCardBehaviorSources(state, playerId).Select(entry => entry.Behavior);
    }

    private static IEnumerable<(string SourceObjectId, CardBehaviorDefinition Behavior)> PlayablePlayCardBehaviorSources(
        MatchState state,
        string playerId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return CoreRuleEngine.EffectPlaySources(state, playerId)
            .Where(objectId => IsImplementedPlayableHandSource(state, playerId, objectId))
            .SelectMany(objectId => PlayCardPromptBehaviorsForSource(state, playerId, objectId)
                .Select(behavior => (SourceObjectId: objectId, Behavior: behavior)));
    }

    private static IReadOnlyList<ActionPromptChoiceDto> SourceStealEnemyEquipmentChoices(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        var optionalCostPrefix = behavior.SourceStealEnemyEquipmentOptionalCostPrefix.Trim();
        return EnemyBoardObjects(state, playerId)
            .Where(objectId => IsPromptAkshanStealEquipmentChoice(state, playerId, objectId))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .Select(objectId =>
            {
                var choice = ObjectChoice(state, objectId, "implemented Akshan orange extra equipment steal");
                return new ActionPromptChoiceDto(
                    $"{optionalCostPrefix}{objectId}",
                    $"{behavior.DisplayName}夺取装备：{choice.Label}",
                    choice.Reason);
            })
            .ToArray();
    }

    private static bool IsPromptAkshanStealEquipmentChoice(
        MatchState state,
        string playerId,
        string objectId)
    {
        return TryFindLegendActionFieldObjectLocation(state.PlayerZones, objectId, out var location)
            && !string.Equals(location.PlayerId, playerId, StringComparison.Ordinal)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
            && !cardObject.IsFaceDown
            && !SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, location.PlayerId)
            && !string.Equals(cardObject.OwnerId, playerId, StringComparison.Ordinal);
    }

    private static IReadOnlyList<ActionPromptChoiceDto> PlayCardDiscardHandManaReductionOptionalCostChoices(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        string? sourceObjectId)
    {
        if (behavior.ManaReductionIfDiscardHandCardOptionalCost <= 0
            || !state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.Hand
            .Where(objectId => CanPromptDiscardHandCardAsOptionalCost(state, playerId, sourceObjectId, objectId))
            .Select(objectId => new ActionPromptChoiceDto(
                $"DISCARD_HAND_CARD:{objectId}",
                $"弃置 {PromptHandCardLabel(state, objectId)}：费用减少 {behavior.ManaReductionIfDiscardHandCardOptionalCost}",
                $"{behavior.DisplayName}的可选额外费用"))
            .ToArray();
    }

    private static string PromptHandCardLabel(MatchState state, string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            ? cardObject.CardNo
            : "一张手牌";
    }

    private static bool IsPromptSpellPlayBehavior(CardBehaviorDefinition behavior)
    {
        return !behavior.PlaysSourceToBaseAsUnit
            && !behavior.PlaysSourceToBaseAsEquipment;
    }

    private static int AvailablePlayMana(MatchState state, RunePool pool, CardBehaviorDefinition behavior)
        => PaymentCostRules.AvailableMana(pool, CoreRuleEngine.ManaPurposeForPlay(behavior), state: state);

    private static bool CanPromptLuxSpellOnlyResourceSource(MatchState state, string playerId, string objectId)
        => CoreRuleEngine.CanUseLuxSpellOnlyResourceSource(state, playerId, objectId)
            && CoreRuleEngine.CanGainSpellOnlyMana(state.RunePools.GetValueOrDefault(playerId, RunePool.Empty), P4ActivatedAbilityCatalog.LuxGeneratedMana);

    private static IReadOnlyDictionary<string, int> CandidatePowerByTrait(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> byChoice)
        => byChoice.Where(p => p.Key.StartsWith(RecycleRunePaymentOptionalCostPrefix, StringComparison.Ordinal)
                && p.Value.GetValueOrDefault("trait") is string && p.Value.GetValueOrDefault("power") is int)
            .GroupBy(p => RuneTrait.Normalize((string)p.Value["trait"]!), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => checked((int)g.Sum(p => (long)(int)p.Value["power"]!)), StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, object?> WithPowerCapacity(Dictionary<string, object?> view,
        RunePaymentReachability reachability)
    {
        if (reachability.CapacityBinds)
        {
            view.Remove("availablePowerByTraitWithPaymentResources");
            view["paymentResourcePowerCapacity"] = reachability.Capacity;
        }
        return view;
    }

    private static IReadOnlyDictionary<string, int> PlayCardAvailablePowerByTrait(
        RunePool runePool,
        IReadOnlyDictionary<string, int> paymentResourcePowerByTrait)
    {
        return new RunePaymentReachability(runePool, paymentResourcePowerByTrait).TraitUpperBounds();
    }

    private static bool CanPayPlayPowerCosts(MatchState state, string playerId, RunePool pool, IReadOnlyDictionary<string, int> resources,
        CardBehaviorDefinition behavior, int amount, string trait, int? maximumAdditionalPower = null, IReadOnlyList<string>? targets = null)
    {
        pool = PaymentCostRules.PowerPoolForPurpose(pool, CoreRuleEngine.PowerPurposeForPlay(behavior));
        var extraTyped = string.IsNullOrWhiteSpace(trait) ? new Dictionary<string, int>()
            : new Dictionary<string, int> { [trait] = amount };
        var extraGeneric = (long)CoreRuleEngine.SourceReplayPowerCost(state, playerId)
            + (string.IsNullOrWhiteSpace(trait) ? amount : 0);
        if (extraGeneric > int.MaxValue) return false;
        return CoreRuleEngine.CanReachPlayPowerCost(state, playerId, behavior,
            new RunePaymentReachability(pool, resources, maximumAdditionalPower), (int)extraGeneric, extraTyped, targets: targets);
    }

    private static bool CanPayHasteReadyPowerCost(
        MatchState state,
        string playerId,
        RunePool runePool,
        IReadOnlyDictionary<string, int> paymentResourcePowerByTrait,
        CardBehaviorDefinition behavior)
        => CanPayPlayPowerCosts(state, playerId, runePool, paymentResourcePowerByTrait, behavior,
            behavior.HasteReadyPowerCost, HasteReadyPowerTrait(behavior));

    private static bool CanPaySourceDrawOptionalPowerCost(
        MatchState state,
        string playerId,
        RunePool runePool,
        IReadOnlyDictionary<string, int> paymentResourcePowerByTrait,
        CardBehaviorDefinition behavior)
    {
        if (behavior.SourceDrawAdditionalPowerCost <= 0
            || behavior.SourceDrawCountIfOptionalPowerCostPaid <= 0)
        {
            return false;
        }

        var sourceDrawPowerTrait = RuneTrait.Normalize(behavior.SourceDrawAdditionalPowerTrait);
        if (string.IsNullOrWhiteSpace(sourceDrawPowerTrait))
        {
            return false;
        }

        return CanPayPlayPowerCosts(state, playerId, runePool, paymentResourcePowerByTrait, behavior,
            behavior.SourceDrawAdditionalPowerCost, sourceDrawPowerTrait);
    }

    private static bool PromptTargetEffectAdditionalCostAvailable(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var paymentResourcePowerByTrait = PlayCardPaymentResourcePowerByTraitForBehavior(state, playerId, behavior);
        return CanPromptTargetEffectAdditionalCost(
            state,
            playerId,
            runePool,
            paymentResourcePowerByTrait,
            behavior,
            null);
    }

    private static bool CanPayTargetEffectAdditionalPowerCost(
        MatchState state,
        string playerId,
        RunePool runePool,
        IReadOnlyDictionary<string, int> paymentResourcePowerByTrait,
        CardBehaviorDefinition behavior)
    {
        if (behavior.TargetEffectAdditionalPowerCost <= 0)
        {
            return false;
        }

        var targetEffectPowerTrait = RuneTrait.Normalize(behavior.TargetEffectAdditionalPowerTrait);
        if (string.IsNullOrWhiteSpace(targetEffectPowerTrait))
        {
            return false;
        }

        return CanPayPlayPowerCosts(state, playerId, runePool, paymentResourcePowerByTrait, behavior,
            behavior.TargetEffectAdditionalPowerCost, targetEffectPowerTrait);
    }

    private static string TargetEffectAdditionalManaCostLabel(CardBehaviorDefinition behavior)
    {
        var companionCost = behavior.TargetEffectAdditionalPowerCost > 0
            ? $"，需同时支付 {behavior.TargetEffectAdditionalPowerCost} {RuneTraitLabel(RuneTrait.Normalize(behavior.TargetEffectAdditionalPowerTrait))}符能"
            : string.Empty;
        return $"额外支付 {behavior.TargetEffectAdditionalManaCost} 法力{companionCost}";
    }

    private static string TargetEffectAdditionalPowerCostLabel(
        CardBehaviorDefinition behavior,
        string powerTrait)
    {
        var companionCost = behavior.TargetEffectAdditionalManaCost > 0
            ? $"，需同时支付 {behavior.TargetEffectAdditionalManaCost} 法力"
            : string.Empty;
        var effectLabel = behavior.PowerModifierAmount != 0
            ? $"一名单位本回合战力 {FormatSignedNumber(behavior.PowerModifierAmount)}"
            : behavior.DamageAmount > 0
                ? $"对一名单位造成 {behavior.DamageAmount} 点伤害"
                : "执行目标效果";
        return $"额外支付 {behavior.TargetEffectAdditionalPowerCost} {RuneTraitLabel(powerTrait)}符能{companionCost}：{effectLabel}";
    }

    private static string FormatSignedNumber(int value)
    {
        return value > 0
            ? $"+{value}"
            : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool CanPaySourceReadyOptionalCost(
        MatchState state,
        string playerId,
        RunePool runePool,
        IReadOnlyDictionary<string, int> paymentResourcePowerByTrait,
        CardBehaviorDefinition behavior)
    {
        if (behavior.SourceReadyAdditionalPowerCost <= 0)
        {
            return false;
        }

        var sourceReadyTrait = RuneTrait.Normalize(behavior.SourceReadyAdditionalPowerTrait);
        if (string.IsNullOrWhiteSpace(sourceReadyTrait))
        {
            return false;
        }

        return CanPayPlayPowerCosts(state, playerId, runePool, paymentResourcePowerByTrait, behavior,
            behavior.SourceReadyAdditionalPowerCost, sourceReadyTrait);
    }

    private static bool CanPromptSourceReadyOptionalCost(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        return behavior.SourceReadyAdditionalPowerCost > 0
            && !string.IsNullOrWhiteSpace(RuneTrait.Normalize(behavior.SourceReadyAdditionalPowerTrait))
            && PromptSourceReadyConditionApplies(state, playerId, behavior.SourceReadyConditionKind);
    }

    private static bool PromptSourceReadyConditionApplies(
        MatchState state,
        string playerId,
        string conditionKind)
    {
        return conditionKind switch
        {
            CardSourceReadyConditionKinds.None => true,
            CardSourceReadyConditionKinds.ControllerPlayedSpellThisTurn => PromptPlayerPlayedSpellThisTurn(state, playerId),
            _ => false
        };
    }

    private static string SourceReadyOptionalCostReason(string conditionKind)
    {
        return conditionKind switch
        {
            CardSourceReadyConditionKinds.ControllerPlayedSpellThisTurn => "本回合已打出过法术",
            _ => "可选额外费用"
        };
    }

    private static int PromptMaximumWardPower(MatchState state, string playerId, CardBehaviorDefinition behavior)
    {
        if (behavior.RequiredTargetCount <= 0 || behavior.PlaysSourceToBaseAsUnit || behavior.PlaysSourceToBaseAsEquipment
            || behavior.IgnoresSpellshield) return 0;
        var taxes = state.PlayerZones.Where(z => z.Key != playerId).SelectMany(z => z.Value.Base.Concat(z.Value.Battlefields))
            .Distinct().Select(id => SpellshieldTaxPowerForTarget(state, playerId, id)).ToArray();
        return Math.Max(taxes.Sum(), taxes.DefaultIfEmpty().Max() * behavior.RequiredTargetCount)
            * (1 + EchoCostRules.Available(state, playerId, behavior).Count);
    }

    private static bool PlayCardBehaviorMayNeedPaymentResource(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        string? sourceObjectId = null)
    {
        return CoreRuleEngine.SourceReplayPowerCost(state, playerId) > 0
            || PromptMinimumManaCost(state, playerId, behavior, sourceObjectId) > 0
            || PromptMaximumWardPower(state, playerId, behavior) > 0
            || PrintedPowerCostRules.ForPlay(behavior).Amount > 0
            || EchoCostRules.Available(state, playerId, behavior).Any(cost => cost.GenericPower > 0
                || cost.TypedPower.Count > 0 || cost.PrintedCardNo is not null)
            || behavior.DamageAmountFromOptionalPowerCost
            || behavior.SourceDrawAdditionalPowerCost > 0
            || behavior.SourceReadyPowerModifierAdditionalPowerCost > 0
            || behavior.OptionalPowerModifier is not null
            || behavior.TargetEffectAdditionalPowerCost > 0
            || behavior.HasteReadyPowerCost > 0
            || CanPromptSourceReadyOptionalCost(state, playerId, behavior)
            || HasSourceStealEnemyEquipmentOptionalCost(behavior)
            || PlayCardLuxSpellOnlyResourceChoicesForBehavior(state, playerId, behavior, sourceObjectId).Count > 0
            || PromptRenektonPaymentChoices(state, playerId).Count > 0;
    }

    private static IReadOnlyList<ActionPromptChoiceDto> PlayCardLuxSpellOnlyResourceChoicesForBehavior(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        string? sourceObjectId)
    {
        if (!IsPromptSpellPlayBehavior(behavior))
        {
            return [];
        }

        var pool = state.RunePools.GetValueOrDefault(playerId, RunePool.Empty);
        if (!pool.HasSupportedAmounts || (long)pool.TotalStoredMana + P4ActivatedAbilityCatalog.LuxGeneratedMana > int.MaxValue) return [];
        var sources = PromptLuxSpellOnlyResourceSourceObjectIds(state, playerId, sourceObjectId);

        return sources
            .Select(objectId =>
            {
                var choice = ObjectChoice(state, objectId, "payment resource action: Lux spell-only mana");
                return new ActionPromptChoiceDto(
                    $"{P4ActivatedAbilityCatalog.LuxSpellOnlyResourceActionPrefix}{objectId}",
                    $"拉克丝法术费用：{choice.Label}",
                    $"横置拉克丝获得 {P4ActivatedAbilityCatalog.LuxGeneratedMana} 点只能支付法术的法力");
            })
            .ToArray();
    }

    private static bool TryParseLuxSpellOnlyResourceActionId(string actionId, out string sourceObjectId)
    {
        sourceObjectId = string.Empty;
        if (string.IsNullOrWhiteSpace(actionId)
            || !actionId.StartsWith(P4ActivatedAbilityCatalog.LuxSpellOnlyResourceActionPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        sourceObjectId = actionId[P4ActivatedAbilityCatalog.LuxSpellOnlyResourceActionPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(sourceObjectId);
    }

    private static string HasteReadyPowerTrait(CardBehaviorDefinition behavior)
    {
        return RuneTrait.Normalize(behavior.HasteReadyPowerTrait);
    }

    private static bool TryGetRuneTrait(CardObjectState runeState, out string runeTrait)
    {
        foreach (var tag in runeState.Tags)
        {
            if (!tag.StartsWith("COLOR:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var trait = RuneTrait.Normalize(tag["COLOR:".Length..]);
            if (!string.IsNullOrWhiteSpace(trait))
            {
                runeTrait = trait;
                return true;
            }
        }

        runeTrait = string.Empty;
        return false;
    }

    private static string RuneTraitLabel(string trait)
    {
        return trait switch
        {
            RuneTrait.Red => "红色",
            RuneTrait.Green => "绿色",
            RuneTrait.Blue => "蓝色",
            RuneTrait.Yellow => "黄色",
            RuneTrait.Orange => "橙色",
            RuneTrait.Purple => "紫色",
            _ => trait
        };
    }

    private static string PlayCardModeLabel(string mode)
    {
        return mode switch
        {
            "AMBUSH" => "伏击",
            "HASTE_READY" => "急速活跃",
            "READY_LEGEND" => "活跃传奇",
            "EXHAUST_LEGEND" => "休眠传奇",
            "BATTLEFIELD_UNIT_POWER_MINUS_4" => "战场单位战力 -4",
            "DRAW_1" => "抽 1 张",
            "SELF_BOON" => "给予我增益",
            "DAMAGE_2" => "造成 2 点伤害",
            "BASE_UNIT_DAMAGE_4" => "对基地单位造成 4 点伤害",
            "DESTROY_EQUIPMENT" => "摧毁一件装备",
            _ => string.IsNullOrWhiteSpace(mode) ? "默认" : mode
        };
    }

    private static IReadOnlyDictionary<string, object?>? MetadataFor(
        MatchState state,
        string playerId,
        string action)
    {
        return action switch
        {
            "MULLIGAN" => new Dictionary<string, object?>
            {
                ["sourcePolicy"] = "opening-hand-cards",
                ["minSelectionCount"] = 0,
                ["maxSelectionCount"] = OfficialDeckValidator.MaximumMulliganCount
            },
            "PLAY_CARD" => PlayCardMetadataFor(state, playerId),
            "PAY_COST" => PayCostMetadataFor(state, playerId),
            "CHOOSE_HAND_CARDS" => HandChoiceMetadataFor(state, playerId),
            "CHOOSE_CARDS" => CardChoiceMetadataFor(state, playerId),
            "HIDE_CARD" => HideCardMetadataFor(state, playerId),
            "REVEAL_CARD" => RevealCardMetadataFor(state, playerId),
            "TAP_RUNE" => new Dictionary<string, object?>
            {
                ["sourcePolicy"] = "ready-controlled-base-rune",
                ["maxBatchSize"] = 12,
                ["resourceGain"] = "1-mana"
            },
            "RECYCLE_RUNE" => new Dictionary<string, object?>
            {
                ["sourcePolicy"] = "controlled-trait-base-rune",
                ["maxBatchSize"] = 12,
                ["resourceGain"] = "1-matching-trait-power",
                ["destination"] = "rune-deck-bottom"
            },
            "ACTIVATE_ABILITY" => ActivateAbilityMetadataFor(state, playerId),
            "MOVE_UNIT" => MoveUnitMetadataFor(state, playerId),
            "ASSEMBLE_EQUIPMENT" => AssembleEquipmentMetadataFor(state, playerId),
            "DECLARE_BATTLE" => DeclareBattleMetadataFor(state, playerId),
            "ASSIGN_COMBAT_DAMAGE" => AssignCombatDamageMetadataFor(state, playerId),
            "ASSIGN_EFFECT_DAMAGE" => CoreRuleEngine.EffectDamageMetadata(state, playerId),
            "ORDER_TRIGGERS" => OrderTriggersMetadataFor(state, playerId),
            "LEGEND_ACT" => LegendActionMetadataFor(state, playerId),
            _ => null
        };
    }

    private static IReadOnlyDictionary<string, object?> HandChoiceMetadataFor(MatchState state, string playerId)
    {
        var choice = state.PendingHandChoice;
        if (choice is null)
        {
            return new Dictionary<string, object?>
            {
                ["serverHandChoiceState"] = "WAIT"
            };
        }

        var ownChoice = string.Equals(choice.PlayerId, playerId, StringComparison.Ordinal);
        var metadata = new Dictionary<string, object?>
        {
            ["choiceId"] = choice.ChoiceId,
            ["choiceWindow"] = choice.ChoiceWindow,
            ["choosingPlayerId"] = choice.PlayerId,
            ["requiredCount"] = choice.RequiredCount,
            ["drawCount"] = choice.DrawCount,
            ["maxCount"] = choice.MaxCount,
            ["reason"] = choice.Reason,
            ["sourceObjectId"] = choice.LastBreathDraw is null && choice.StandbyObserver is null ? choice.SourceObjectId
                : ResolutionResult.VisibleObjectIdForViewer(state, choice.SourceObjectId, playerId),
            ["effectKind"] = choice.EffectKind,
            ["serverHandChoiceState"] = ownChoice ? "PENDING" : "WAITING"
        };

        if (ownChoice)
        {
            metadata["handChoices"] = PendingHandChoiceDtos(state, choice);
            metadata["legalObjectIds"] = choice.LegalObjectIds;
        }

        return metadata;
    }

    private static IReadOnlyList<ActionPromptChoiceDto> PendingHandChoiceDtos(
        MatchState state,
        PendingHandChoiceState choice)
    {
        return choice.LegalObjectIds
            .Where(objectId => IsPromptHandCardControlledByPlayerOrLegacyOwned(state, choice.PlayerId, objectId))
            .Select(objectId => ObjectChoice(state, objectId, "可弃置手牌"))
            .ToArray();
    }

    private static IReadOnlyDictionary<string, object?> OrderTriggersMetadataFor(MatchState state, string playerId)
    {
        if (!ResolutionResult.HasOpenOrderTriggersWindow(state))
        {
            return new Dictionary<string, object?>
            {
                ["orderingState"] = "WAIT"
            };
        }

        var orderingPlayerId = ResolutionResult.OrderTriggersOrderingPlayerId(state) ?? string.Empty;
        var triggerIds = ResolutionResult.OrderTriggerIds(state);
        var legalResolutionTriggerIds = ResolutionResult.OrderTriggerLegalResolutionIds(state);
        return new Dictionary<string, object?>
        {
            ["orderingPlayerId"] = orderingPlayerId,
            ["orderedTriggerIds"] = legalResolutionTriggerIds,
            ["reorderableTriggerIds"] = state.TriggerQueue.Where(t => t.ControllerId == orderingPlayerId)
                .Select(t => t.TriggerId).ToArray(),
            ["triggerIds"] = triggerIds,
            ["triggers"] = ResolutionResult.OrderTriggerViews(state, playerId),
            ["triggerChoices"] = ResolutionResult.OrderTriggerChoices(state, playerId),
            ["legalOrderingConstraints"] = ResolutionResult.OrderTriggerLegalOrderingConstraints(state),
            ["triggeredByEventKind"] = state.TriggerQueue.FirstOrDefault()?.TriggeredByEventKind ?? string.Empty,
            ["orderingState"] = string.Equals(playerId, orderingPlayerId, StringComparison.Ordinal)
                ? "PENDING_ORDER"
                : "WAITING_FOR_ORDER"
        };
    }

    private static Dictionary<string, object?> PendingPaymentCostView(PendingPaymentState payment)
        => payment.CostView();

    private static IReadOnlyList<ActionPromptChoiceDto> PendingPaymentChoiceDtos(MatchState state, PendingPaymentState payment)
    {
        return PendingPaymentSpendChoiceIds(payment)
            .Select(choiceId => new ActionPromptChoiceDto(choiceId, state.PendingRuleChoice?.Request.Options.FirstOrDefault(o => o.Id == choiceId)?.Label ?? PaymentChoiceLabel(choiceId), "服务端支付候选", state.PendingRuleChoice?.Request.Options.FirstOrDefault(o => o.Id == choiceId)?.ObjectIds))
            .ToArray();
    }

    private static IReadOnlyList<ActionPromptChoiceDto> PendingPaymentResourceChoiceDtos(
        MatchState state,
        PendingPaymentState payment)
    {
        return PendingPaymentResourceActionIds(state, payment)
            .Select(choiceId =>
            {
                var label = PaymentChoiceLabel(choiceId);
                if (TryParseRecycleRunePaymentActionId(choiceId, out var objectId)
                    && state.CardObjects.TryGetValue(objectId, out var runeState))
                {
                    var objectChoice = ObjectChoice(state, objectId, "pending payment resource action: recycle rune");
                    label = $"回收符文支付：{objectChoice.Label}";
                }


                return new ActionPromptChoiceDto(choiceId, label, "服务端支付资源候选");
            })
            .ToArray();
    }

    private static IReadOnlyList<string> PendingPaymentSpendChoiceIds(PendingPaymentState payment)
    {
        return payment.LegalPaymentChoiceIds
            .Where(choiceId => payment.PaymentWindow == CoreRuleEngine.RuleChoiceWindow || !IsRecycleRunePaymentActionId(choiceId))
            .ToArray();
    }

    private static IReadOnlyList<string> PendingPaymentResourceActionIds(PendingPaymentState payment)
    {
        return payment.PaymentResourceActionIds
            .Concat(payment.LegalPaymentChoiceIds.Where(IsRecycleRunePaymentActionId))
            .Where(choiceId => !string.IsNullOrWhiteSpace(choiceId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> PendingPaymentResourceActionIds(
        MatchState state,
        PendingPaymentState payment)
    {
        return PendingPaymentResourceActionIds(payment)

            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;
public sealed partial record MatchState
{

    private static bool SourceObjectPowerStaticAuraApplies(
        MatchState state,
        string objectId,
        CardObjectState cardObject,
        StaticAuraSpec aura)
    {
        if (!string.IsNullOrWhiteSpace(aura.TargetFilter)
            && !StaticAuraSpecRules.TargetMatchesFilter(aura, cardObject))
        {
            return false;
        }

        if (aura.RequiredPlayerExperience.HasValue
            || string.Equals(aura.Kind, StaticAuraKinds.SourceObjectPower, StringComparison.Ordinal))
        {
            var controllerId = EffectiveFieldControllerId(state, objectId, cardObject);
            if (string.IsNullOrWhiteSpace(controllerId)
                || !StaticAuraControllerRequirementsSatisfied(aura, state, controllerId))
            {
                return false;
            }
        }

        return !string.Equals(aura.Kind, StaticAuraKinds.SourceObjectPower, StringComparison.Ordinal)
            || !StaticAuraSpecRules.IsSourceObjectPowerAuraAlreadyMaterialized(cardObject, aura);
    }

    private static IReadOnlyList<ContinuousEffectState> BuildSourceObjectKeywordStaticAuraEffects(
        MatchState state,
        string objectId,
        CardObjectState cardObject)
    {
        var effects = new List<ContinuousEffectState>();
        if (string.IsNullOrWhiteSpace(cardObject.CardNo)
            || cardObject.IsFaceDown
            || !cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || !TryFindFieldObjectLocation(state.PlayerZones, objectId, out var fieldLocation)
            || !IsPublicFieldObjectLocationCompatible(state, objectId, fieldLocation.Zone))
        {
            return effects;
        }

        var dependencyObjectIds = PublicFieldDependencyObjectIds(state, [objectId]);
        foreach (var aura in StaticAuraSpecRules.GetStaticAuras(cardObject.CardNo)
            .Where(StaticAuraSpecRules.IsSourceObjectKeywordStaticAura))
        {
            if (!SourceObjectKeywordStaticAuraApplies(state, objectId, cardObject, aura))
            {
                continue;
            }

            var metadata = SourceObjectKeywordStaticAuraProjectionMetadata(aura);
            effects.Add(new ContinuousEffectState(
                $"{metadata.EffectIdPrefix}:{objectId}:{aura.GrantedKeyword}",
                "OBJECT",
                ContinuousEffectLayers.RuleText,
                aura.Duration,
                objectId,
                objectId,
                0,
                ResolveBasePower(cardObject),
                cardObject.Power,
                aura.Kind,
                cardObject.CardNo,
                metadata.SourcePath,
                true,
                LayerEngineFoundationResiduals(),
                Condition: metadata.Condition,
                Lifecycle: metadata.Lifecycle,
                ParticipantObjectIds: [objectId],
                SourceDependencyObjectIds: dependencyObjectIds,
                TargetDependencyObjectIds: dependencyObjectIds,
                ParticipantDependencyObjectIds: dependencyObjectIds));
        }

        return effects;
    }

    private static bool SourceObjectKeywordStaticAuraApplies(
        MatchState state,
        string objectId,
        CardObjectState cardObject,
        StaticAuraSpec aura)
    {
        if (!string.IsNullOrWhiteSpace(aura.TargetFilter)
            && !StaticAuraSpecRules.TargetMatchesFilter(aura, cardObject))
        {
            return false;
        }

        if (aura.RequiredPlayerExperience.HasValue)
        {
            var controllerId = EffectiveFieldControllerId(state, objectId, cardObject);
            if (string.IsNullOrWhiteSpace(controllerId)
                || !StaticAuraControllerRequirementsSatisfied(aura, state, controllerId))
            {
                return false;
            }
        }

        return true;
    }

    private static (
        string EffectIdPrefix,
        string SourcePath,
        string Condition,
        string Lifecycle) SourceObjectKeywordStaticAuraProjectionMetadata(StaticAuraSpec aura)
    {
        if (string.Equals(aura.Kind, StaticAuraKinds.SourceObjectFilteredKeyword, StringComparison.Ordinal))
        {
            return (
                "RULE_TEXT:SOURCE_OBJECT_FILTERED_KEYWORD",
                "CoreRuleEngine.HasSourceObjectFilteredStaticKeyword",
                "SOURCE_PUBLIC_FIELD_UNIT_MATCHES_FILTER",
                "RECOMPUTED_FROM_CURRENT_SOURCE_OBJECT_TAGS");
        }

        if (string.Equals(aura.Kind, StaticAuraKinds.SourceObjectLevelKeyword, StringComparison.Ordinal))
        {
            return (
                "RULE_TEXT:SOURCE_OBJECT_LEVEL_KEYWORD",
                "CoreRuleEngine.HasSourceObjectLevelStaticKeyword",
                "SOURCE_PUBLIC_FIELD_UNIT_AND_CONTROLLER_EXPERIENCE",
                "RECOMPUTED_FROM_CURRENT_CONTROLLER_EXPERIENCE");
        }

        return (
            $"RULE_TEXT:SOURCE_OBJECT_KEYWORD:{aura.Kind}",
            "CoreRuleEngine.HasSourceObjectKeywordStaticAura",
            "SOURCE_PUBLIC_FIELD_UNIT_MATCHES_SOURCE_OBJECT_SCOPE",
            "RECOMPUTED_FROM_CURRENT_SOURCE_OBJECT_STATE");
    }

    private static IReadOnlyList<ContinuousEffectState> BuildSourceBattleStatePowerStaticAuraEffects(
        MatchState state,
        string objectId,
        CardObjectState cardObject)
    {
        var effects = new List<ContinuousEffectState>();
        if (string.IsNullOrWhiteSpace(cardObject.CardNo)
            || cardObject.IsFaceDown
            || !cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || (!cardObject.IsAttacking && !cardObject.IsDefending)
            || !TryFindBattlefieldUnitLocation(state, objectId, cardObject, out _))
        {
            return effects;
        }

        var sourceDependencyObjectIds = PublicFieldDependencyObjectIds(state, [objectId]);
        foreach (var aura in StaticAuraSpecRules.GetStaticAuras(cardObject.CardNo)
            .Where(StaticAuraSpecRules.IsSourceBattleStatePowerStaticAura))
        {
            var participantObjectIds = SourceBattleStatePowerStaticAuraParticipantObjectIds(
                state,
                objectId,
                cardObject,
                aura);
            if (participantObjectIds.Count == 0)
            {
                continue;
            }

            var participantDependencyObjectIds = PublicFieldDependencyObjectIds(state, participantObjectIds);
            var metadata = SourceBattleStatePowerStaticAuraProjectionMetadata(aura);
            effects.Add(new ContinuousEffectState(
                $"{metadata.EffectIdPrefix}:{objectId}",
                "OBJECT",
                ContinuousEffectLayers.StaticAura,
                aura.Duration,
                objectId,
                objectId,
                aura.PowerDeltaPerParticipant,
                cardObject.Power,
                cardObject.Power + aura.PowerDeltaPerParticipant,
                aura.Kind,
                cardObject.CardNo,
                metadata.SourcePath,
                true,
                LayerEngineFoundationResiduals(),
                Condition: metadata.Condition,
                Lifecycle: metadata.Lifecycle,
                ParticipantObjectIds: participantObjectIds,
                SourceDependencyObjectIds: sourceDependencyObjectIds,
                TargetDependencyObjectIds: sourceDependencyObjectIds,
                ParticipantDependencyObjectIds: participantDependencyObjectIds));
        }

        return effects;
    }

    private static IReadOnlyList<string> SourceBattleStatePowerStaticAuraParticipantObjectIds(
        MatchState state,
        string objectId,
        CardObjectState cardObject,
        StaticAuraSpec aura)
    {
        if (string.Equals(
                aura.ParticipantScope,
                StaticAuraParticipantScopes.AttackingBattlefieldPublicUnits,
                StringComparison.Ordinal))
        {
            if (!cardObject.IsAttacking)
            {
                return [];
            }

            var participantObjectIds = BattleAttackerObjectIds(state);
            return participantObjectIds.Count >= aura.RequiredAttackingUnitCount.GetValueOrDefault(2)
                && participantObjectIds.Contains(objectId, StringComparer.Ordinal)
                ? participantObjectIds
                : [];
        }

        if (string.Equals(
                aura.ParticipantScope,
                StaticAuraParticipantScopes.BattlefieldPublicUnits,
                StringComparison.Ordinal))
        {
            var sideParticipantObjectIds = cardObject.IsAttacking
                ? BattleAttackerObjectIds(state)
                : BattleDefenderObjectIds(state);
            var requiredCount = cardObject.IsAttacking
                ? aura.RequiredAttackingUnitCount.GetValueOrDefault(1)
                : aura.RequiredDefendingUnitCount.GetValueOrDefault(1);
            if (sideParticipantObjectIds.Count != requiredCount
                || !sideParticipantObjectIds.Contains(objectId, StringComparer.Ordinal))
            {
                return [];
            }

            return BattleParticipantObjectIds(state);
        }

        if (string.Equals(
                aura.ParticipantScope,
                StaticAuraParticipantScopes.ReadyEnemyBattlefieldPublicUnits,
                StringComparison.Ordinal))
        {
            if (!cardObject.IsAttacking)
            {
                return [];
            }

            var participantObjectIds = BattleReadyDefenderObjectIds(state);
            return participantObjectIds.Count >= aura.RequiredReadyEnemyUnitCount.GetValueOrDefault(1)
                ? participantObjectIds
                : [];
        }

        return [];
    }

    private static (
        string EffectIdPrefix,
        string SourcePath,
        string Condition,
        string Lifecycle) SourceBattleStatePowerStaticAuraProjectionMetadata(StaticAuraSpec aura)
    {
        if (string.Equals(aura.Kind, StaticAuraKinds.SourceAttackingWithAnotherUnitPower, StringComparison.Ordinal))
        {
            return (
                "STATIC_AURA:SOURCE_ATTACKING_WITH_ANOTHER_UNIT_POWER",
                "CoreRuleEngine.ResolveSourceAttackingWithAnotherUnitPowerBonus",
                "SOURCE_ATTACKING_WITH_REQUIRED_ATTACKER_COUNT",
                "RECOMPUTED_FROM_CURRENT_BATTLE_ATTACKER_LOCATIONS");
        }

        if (string.Equals(aura.Kind, StaticAuraKinds.SourceLoneBattlePower, StringComparison.Ordinal))
        {
            return (
                "STATIC_AURA:SOURCE_LONE_BATTLE_POWER",
                "CoreRuleEngine.ResolveSourceLoneBattlePowerBonus",
                "SOURCE_ATTACKING_OR_DEFENDING_ALONE",
                "RECOMPUTED_FROM_CURRENT_BATTLE_PARTICIPANT_LOCATIONS");
        }

        if (string.Equals(aura.Kind, StaticAuraKinds.SourceAttackingReadyEnemyUnitPower, StringComparison.Ordinal))
        {
            return (
                "STATIC_AURA:SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER",
                "CoreRuleEngine.ResolveSourceAttackingReadyEnemyUnitPowerBonus",
                "SOURCE_ATTACKING_AND_READY_ENEMY_PUBLIC_UNITS_AT_SAME_BATTLEFIELD",
                "RECOMPUTED_FROM_CURRENT_SAME_BATTLEFIELD_READY_ENEMY_UNIT_LOCATIONS");
        }

        return (
            $"STATIC_AURA:{aura.Kind}",
            "CoreRuleEngine.ResolveSourceBattleStatePowerStaticAuraBonus",
            "SOURCE_BATTLE_STATE_POWER_SCOPE_MATCHES",
            "RECOMPUTED_FROM_CURRENT_BATTLE_STATE");
    }

    private static IReadOnlyList<ContinuousEffectState> BuildSameBattlefieldOtherFriendlyUnitsKeywordAuraEffects(MatchState state)
    {
        var effects = new List<ContinuousEffectState>();
        foreach (var entry in state.CardObjects.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var sourceObjectId = entry.Key;
            var source = entry.Value;
            if (string.IsNullOrWhiteSpace(source.CardNo)
                || source.IsFaceDown
                || !source.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                || !TryFindBattlefieldUnitLocation(state, sourceObjectId, source, out var battlefieldObjectId))
            {
                continue;
            }

            var controllerId = EffectiveFieldControllerId(state, sourceObjectId, source);
            if (string.IsNullOrWhiteSpace(controllerId))
            {
                continue;
            }

            var participantObjectIds = SameBattlefieldOtherFriendlyUnitObjectIds(
                state,
                sourceObjectId,
                battlefieldObjectId,
                controllerId);
            foreach (var aura in StaticAuraSpecRules.GetStaticAuras(source.CardNo)
                .Where(StaticAuraSpecRules.IsSameBattlefieldOtherFriendlyKeywordStaticAura))
            {
                if (string.IsNullOrWhiteSpace(aura.GrantedKeyword))
                {
                    continue;
                }

                foreach (var participantObjectId in participantObjectIds)
                {
                    if (!state.CardObjects.TryGetValue(participantObjectId, out var participant)
                        || !SameBattlefieldOtherFriendlyKeywordStaticAuraAppliesToParticipant(aura, participant))
                    {
                        continue;
                    }

                    effects.Add(new ContinuousEffectState(
                        $"RULE_TEXT:SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD:{sourceObjectId}:{participantObjectId}:{aura.GrantedKeyword}",
                        "OBJECT",
                        ContinuousEffectLayers.RuleText,
                        aura.Duration,
                        participantObjectId,
                        sourceObjectId));
                }
            }
        }

        return effects;
    }

    private static bool SameBattlefieldOtherFriendlyKeywordStaticAuraAppliesToParticipant(
        StaticAuraSpec aura,
        CardObjectState participant)
    {
        return string.IsNullOrWhiteSpace(aura.TargetFilter)
            || StaticAuraSpecRules.TargetMatchesFilter(aura, participant);
    }

    private static IReadOnlyList<ContinuousEffectState> BuildSameBattlefieldOtherFriendlyPowerStaticAuraEffects(MatchState state)
    {
        var effects = new List<ContinuousEffectState>();
        foreach (var entry in state.CardObjects.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var sourceObjectId = entry.Key;
            var source = entry.Value;
            if (string.IsNullOrWhiteSpace(source.CardNo)
                || source.IsFaceDown
                || !source.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                || !TryFindBattlefieldUnitLocation(state, sourceObjectId, source, out var battlefieldObjectId))
            {
                continue;
            }

            var controllerId = EffectiveFieldControllerId(state, sourceObjectId, source);
            if (string.IsNullOrWhiteSpace(controllerId))
            {
                continue;
            }

            foreach (var aura in StaticAuraSpecRules.GetStaticAuras(source.CardNo)
                .Where(StaticAuraSpecRules.IsSameBattlefieldOtherFriendlyPowerStaticAura))
            {
                var participantObjectIds = SameBattlefieldOtherFriendlyPowerStaticAuraParticipantObjectIds(
                    state,
                    sourceObjectId,
                    battlefieldObjectId,
                    controllerId,
                    aura);
                if (participantObjectIds.Count == 0)
                {
                    continue;
                }

                var sourceDependencyObjectIds = PublicFieldDependencyObjectIds(state, [sourceObjectId]);
                var participantDependencyObjectIds = PublicFieldDependencyObjectIds(state, participantObjectIds);
                var metadata = SameBattlefieldOtherFriendlyPowerStaticAuraProjectionMetadata(aura);
                foreach (var participantObjectId in participantObjectIds)
                {
                    var participant = state.CardObjects[participantObjectId];
                    var targetDependencyObjectIds = PublicFieldDependencyObjectIds(state, [participantObjectId]);
                    effects.Add(new ContinuousEffectState(
                        $"{metadata.EffectIdPrefix}:{sourceObjectId}:{participantObjectId}",
                        "BATTLEFIELD",
                        ContinuousEffectLayers.StaticAura,
                        aura.Duration,
                        participantObjectId,
                        sourceObjectId,
                        aura.PowerDeltaPerParticipant,
                        participant.Power,
                        participant.Power + aura.PowerDeltaPerParticipant,
                        aura.Kind,
                        source.CardNo,
                        metadata.SourcePath,
                        true,
                        LayerEngineFoundationResiduals(),
                        Condition: metadata.Condition,
                        Lifecycle: metadata.Lifecycle,
                        ParticipantObjectIds: participantObjectIds,
                        SourceDependencyObjectIds: sourceDependencyObjectIds,
                        TargetDependencyObjectIds: targetDependencyObjectIds,
                        ParticipantDependencyObjectIds: participantDependencyObjectIds));
                }
            }
        }

        return effects;
    }

    private static IReadOnlyList<string> SameBattlefieldOtherFriendlyPowerStaticAuraParticipantObjectIds(
        MatchState state,
        string sourceObjectId,
        string battlefieldObjectId,
        string controllerId,
        StaticAuraSpec aura)
    {
        if (string.Equals(aura.TargetScope, StaticAuraTargetScopes.SameBattlefieldOtherFriendlyUnits, StringComparison.Ordinal))
        {
            return SameBattlefieldOtherFriendlyUnitObjectIds(
                state,
                sourceObjectId,
                battlefieldObjectId,
                controllerId);
        }

        if (string.Equals(
                aura.TargetScope,
                StaticAuraTargetScopes.SameBattlefieldOtherFriendlyFilteredUnits,
                StringComparison.Ordinal))
        {
            return SameBattlefieldOtherFriendlyFilteredUnitObjectIds(
                state,
                sourceObjectId,
                battlefieldObjectId,
                controllerId,
                aura);
        }

        return [];
    }

    private static (
        string EffectIdPrefix,
        string SourcePath,
        string Condition,
        string Lifecycle) SameBattlefieldOtherFriendlyPowerStaticAuraProjectionMetadata(StaticAuraSpec aura)
    {
        if (string.Equals(aura.Kind, StaticAuraKinds.SameBattlefieldOtherFriendlyUnitsPowerPlusOne, StringComparison.Ordinal))
        {
            return (
                "STATIC_AURA:SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_POWER_PLUS_ONE",
                "CoreRuleEngine.ResolveSameBattlefieldOtherFriendlyUnitsPowerBonus",
                "SOURCE_AND_OTHER_FRIENDLY_PUBLIC_UNITS_AT_SAME_BATTLEFIELD",
                "DERIVED_FROM_CURRENT_SAME_BATTLEFIELD_FRIENDLY_UNIT_LOCATIONS");
        }

        if (string.Equals(aura.Kind, StaticAuraKinds.SameBattlefieldOtherFriendlyFilteredUnitsPower, StringComparison.Ordinal))
        {
            return (
                "STATIC_AURA:SAME_BATTLEFIELD_OTHER_FRIENDLY_FILTERED_UNITS_POWER",
                "CoreRuleEngine.ResolveSameBattlefieldOtherFriendlyFilteredUnitsPowerBonus",
                "SOURCE_AND_OTHER_FRIENDLY_FILTERED_PUBLIC_UNITS_AT_SAME_BATTLEFIELD",
                "DERIVED_FROM_CURRENT_SAME_BATTLEFIELD_FILTERED_FRIENDLY_UNIT_LOCATIONS");
        }

        return (
            $"STATIC_AURA:{aura.Kind}",
            "CoreRuleEngine.ResolveSameBattlefieldOtherFriendlyPowerStaticAuraBonus",
            $"SOURCE_{aura.TargetScope}_AT_SAME_BATTLEFIELD",
            "DERIVED_FROM_CURRENT_SAME_BATTLEFIELD_FRIENDLY_UNIT_LOCATIONS");
    }

    private static IReadOnlyList<ContinuousEffectState> BuildPublicFieldFriendlyPowerStaticAuraEffects(MatchState state)
    {
        var effects = new List<ContinuousEffectState>();
        foreach (var sourceObjectId in PublicStaticAuraSourceObjectIds(state))
        {
            if (!state.CardObjects.TryGetValue(sourceObjectId, out var source)
                || string.IsNullOrWhiteSpace(source.CardNo)
                || source.IsFaceDown)
            {
                continue;
            }

            foreach (var aura in StaticAuraSpecRules.GetStaticAuras(source.CardNo)
                .Where(StaticAuraSpecRules.IsPublicFieldFriendlyPowerStaticAura))
            {
                if (!TryGetPublicFieldFriendlyPowerStaticAuraSourceControllerId(
                        state,
                        sourceObjectId,
                        source,
                        aura,
                        out var controllerId)
                    || !StaticAuraControllerRequirementsSatisfied(aura, state, controllerId))
                {
                    continue;
                }

                var participantObjectIds = PublicFieldFriendlyPowerStaticAuraParticipantObjectIds(
                    state,
                    sourceObjectId,
                    controllerId,
                    aura);
                if (participantObjectIds.Count == 0)
                {
                    continue;
                }

                var sourceDependencyObjectIds = PublicFieldFriendlyPowerStaticAuraSourceDependencyObjectIds(
                    state,
                    [sourceObjectId],
                    aura);
                var participantDependencyObjectIds = PublicFieldDependencyObjectIds(state, participantObjectIds);
                var metadata = PublicFieldFriendlyPowerStaticAuraProjectionMetadata(aura);
                foreach (var participantObjectId in participantObjectIds)
                {
                    var participant = state.CardObjects[participantObjectId];
                    var targetDependencyObjectIds = PublicFieldDependencyObjectIds(state, [participantObjectId]);
                    effects.Add(new ContinuousEffectState(
                        $"{metadata.EffectIdPrefix}:{sourceObjectId}:{participantObjectId}",
                        "OBJECT",
                        ContinuousEffectLayers.StaticAura,
                        aura.Duration,
                        participantObjectId,
                        sourceObjectId,
                        aura.PowerDeltaPerParticipant,
                        participant.Power,
                        participant.Power + aura.PowerDeltaPerParticipant,
                        aura.Kind,
                        source.CardNo,
                        metadata.SourcePath,
                        true,
                        LayerEngineFoundationResiduals(),
                        Condition: metadata.Condition,
                        Lifecycle: metadata.Lifecycle,
                        ParticipantObjectIds: participantObjectIds,
                        SourceDependencyObjectIds: sourceDependencyObjectIds,
                        TargetDependencyObjectIds: targetDependencyObjectIds,
                        ParticipantDependencyObjectIds: participantDependencyObjectIds));
                }
            }
        }

        return effects;
    }

    private static bool TryGetPublicFieldFriendlyPowerStaticAuraSourceControllerId(
        MatchState state,
        string sourceObjectId,
        CardObjectState source,
        StaticAuraSpec aura,
        out string controllerId)
    {
        controllerId = string.Empty;
        if (string.Equals(aura.TargetScope, StaticAuraTargetScopes.FriendlyUnits, StringComparison.Ordinal))
        {
            controllerId = EffectivePublicStaticAuraSourceControllerId(state, sourceObjectId, source);
            return !string.IsNullOrWhiteSpace(controllerId);
        }

        if ((string.Equals(aura.TargetScope, StaticAuraTargetScopes.OtherFriendlyUnits, StringComparison.Ordinal)
                || string.Equals(aura.TargetScope, StaticAuraTargetScopes.FriendlyFilteredUnits, StringComparison.Ordinal))
            && source.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && TryFindFieldObjectLocation(state.PlayerZones, sourceObjectId, out var sourceLocation)
            && IsPublicFieldObjectLocationCompatible(state, sourceObjectId, sourceLocation.Zone))
        {
            controllerId = EffectiveFieldControllerId(state, sourceObjectId, source);
            return !string.IsNullOrWhiteSpace(controllerId);
        }

        return false;
    }

    private static IReadOnlyList<string> PublicFieldFriendlyPowerStaticAuraParticipantObjectIds(
        MatchState state,
        string sourceObjectId,
        string controllerId,
        StaticAuraSpec aura)
    {
        if (string.Equals(aura.TargetScope, StaticAuraTargetScopes.FriendlyUnits, StringComparison.Ordinal))
        {
            return FriendlyPublicFieldUnitObjectIds(state, controllerId);
        }

        if (string.Equals(aura.TargetScope, StaticAuraTargetScopes.OtherFriendlyUnits, StringComparison.Ordinal))
        {
            return OtherFriendlyPublicFieldUnitObjectIds(state, sourceObjectId, controllerId);
        }

        if (string.Equals(aura.TargetScope, StaticAuraTargetScopes.FriendlyFilteredUnits, StringComparison.Ordinal))
        {
            return FriendlyFilteredPublicFieldUnitObjectIds(state, controllerId, aura);
        }

        return [];
    }

    private static IReadOnlyList<string> PublicFieldFriendlyPowerStaticAuraSourceDependencyObjectIds(
        MatchState state,
        IReadOnlyList<string> sourceObjectIds,
        StaticAuraSpec aura)
    {
        return string.Equals(aura.TargetScope, StaticAuraTargetScopes.FriendlyUnits, StringComparison.Ordinal)
            ? PublicStaticAuraDependencyObjectIds(state, sourceObjectIds)
            : PublicFieldDependencyObjectIds(state, sourceObjectIds);
    }

    private static (
        string EffectIdPrefix,
        string SourcePath,
        string Condition,
        string Lifecycle) PublicFieldFriendlyPowerStaticAuraProjectionMetadata(StaticAuraSpec aura)
    {
        if (string.Equals(aura.Kind, StaticAuraKinds.FriendlyUnitsPower, StringComparison.Ordinal))
        {
            return (
                "STATIC_AURA:FRIENDLY_UNITS_POWER",
                "CoreRuleEngine.ResolveFriendlyUnitsPowerBonus",
                "SOURCE_PUBLIC_STATIC_AURA_AND_FRIENDLY_PUBLIC_UNITS",
                "DERIVED_FROM_CURRENT_PUBLIC_FIELD_FRIENDLY_UNIT_LOCATIONS");
        }

        if (string.Equals(aura.Kind, StaticAuraKinds.OtherFriendlyUnitsPower, StringComparison.Ordinal))
        {
            return (
                "STATIC_AURA:OTHER_FRIENDLY_UNITS_POWER",
                "CoreRuleEngine.ResolveOtherFriendlyUnitsPowerBonus",
                "SOURCE_PUBLIC_FIELD_UNIT_AND_OTHER_FRIENDLY_PUBLIC_UNITS",
                "DERIVED_FROM_CURRENT_PUBLIC_FIELD_FRIENDLY_UNIT_LOCATIONS");
        }

        if (string.Equals(aura.Kind, StaticAuraKinds.FriendlyFilteredUnitsPower, StringComparison.Ordinal))
        {
            return (
                "STATIC_AURA:FRIENDLY_FILTERED_UNITS_POWER",
                "CoreRuleEngine.ResolveFriendlyFilteredUnitsPowerBonus",
                "SOURCE_PUBLIC_FIELD_UNIT_AND_FRIENDLY_FILTERED_PUBLIC_UNITS",
                "DERIVED_FROM_CURRENT_PUBLIC_FIELD_FILTERED_UNIT_LOCATIONS");
        }

        return (
            $"STATIC_AURA:{aura.Kind}",
            "CoreRuleEngine.ResolvePublicFieldFriendlyPowerStaticAuraBonus",
            $"SOURCE_{aura.TargetScope}_AND_PUBLIC_FIELD_FRIENDLY_UNITS",
            "DERIVED_FROM_CURRENT_PUBLIC_FIELD_FRIENDLY_UNIT_LOCATIONS");
    }

    private static IReadOnlyList<ContinuousEffectState> BuildOtherFriendlyUnitsKeywordAuraEffects(MatchState state)
    {
        var effects = new List<ContinuousEffectState>();
        foreach (var sourceObjectId in PublicStaticAuraSourceObjectIds(state))
        {
            if (!state.CardObjects.TryGetValue(sourceObjectId, out var source)
                || string.IsNullOrWhiteSpace(source.CardNo)
                || source.IsFaceDown
                || !source.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal))
            {
                continue;
            }

            var controllerId = EffectivePublicStaticAuraSourceControllerId(state, sourceObjectId, source);
            if (string.IsNullOrWhiteSpace(controllerId))
            {
                continue;
            }

            foreach (var aura in StaticAuraSpecRules.GetStaticAuras(source.CardNo)
                .Where(StaticAuraSpecRules.IsPublicFieldFriendlyKeywordStaticAura)
                .Where(aura => string.Equals(
                    aura.TargetScope,
                    StaticAuraTargetScopes.OtherFriendlyUnits,
                    StringComparison.Ordinal)))
            {
                if (string.IsNullOrWhiteSpace(aura.GrantedKeyword))
                {
                    continue;
                }

                var participantObjectIds = OtherFriendlyPublicFieldUnitObjectIds(
                    state,
                    sourceObjectId,
                    controllerId);
                foreach (var participantObjectId in participantObjectIds)
                {
                    effects.Add(new ContinuousEffectState(
                        $"RULE_TEXT:OTHER_FRIENDLY_UNITS_KEYWORD:{sourceObjectId}:{participantObjectId}:{aura.GrantedKeyword}",
                        "OBJECT",
                        ContinuousEffectLayers.RuleText,
                        aura.Duration,
                        participantObjectId,
                        sourceObjectId));
                }
            }
        }

        return effects;
    }

    private static IReadOnlyList<ContinuousEffectState> BuildFriendlyFilteredUnitsKeywordAuraEffects(MatchState state)
    {
        var effects = new List<ContinuousEffectState>();
        foreach (var sourceObjectId in PublicStaticAuraSourceObjectIds(state))
        {
            if (!state.CardObjects.TryGetValue(sourceObjectId, out var source)
                || string.IsNullOrWhiteSpace(source.CardNo)
                || source.IsFaceDown)
            {
                continue;
            }

            var controllerId = EffectivePublicStaticAuraSourceControllerId(state, sourceObjectId, source);
            if (string.IsNullOrWhiteSpace(controllerId))
            {
                continue;
            }

            foreach (var aura in StaticAuraSpecRules.GetStaticAuras(source.CardNo)
                .Where(StaticAuraSpecRules.IsPublicFieldFriendlyKeywordStaticAura)
                .Where(aura => string.Equals(
                    aura.TargetScope,
                    StaticAuraTargetScopes.FriendlyFilteredUnits,
                    StringComparison.Ordinal)))
            {
                if (string.IsNullOrWhiteSpace(aura.GrantedKeyword))
                {
                    continue;
                }

                var participantObjectIds = FriendlyFilteredPublicFieldUnitObjectIds(
                    state,
                    controllerId,
                    aura);
                foreach (var participantObjectId in participantObjectIds)
                {
                    effects.Add(new ContinuousEffectState(
                        $"RULE_TEXT:FRIENDLY_FILTERED_UNITS_KEYWORD:{sourceObjectId}:{participantObjectId}:{aura.GrantedKeyword}",
                        "OBJECT",
                        ContinuousEffectLayers.RuleText,
                        aura.Duration,
                        participantObjectId,
                        sourceObjectId));
                }
            }
        }

        return effects;
    }

    private static IReadOnlyList<ContinuousEffectState> BuildBattlefieldPowerStaticAuraEffects(MatchState state)
    {
        var effects = new List<ContinuousEffectState>();
        foreach (var battlefieldObjectId in state.PlayerZones
            .SelectMany(entry => entry.Value.Battlefields)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(battlefieldObjectId, out var battlefield)
                || battlefield.IsFaceDown
                || !battlefield.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal)
                || !IsObjectLocationCompatibleWithBattlefield(state, battlefieldObjectId, battlefieldObjectId))
            {
                continue;
            }

            foreach (var aura in StaticAuraSpecRules.GetStaticAuras(battlefield.CardNo).Where(StaticAuraSpecRules.IsBattlefieldPowerStaticAura))
            {
                effects.AddRange(BuildBattlefieldPowerStaticAuraEffects(state, battlefieldObjectId, aura));
            }
        }

        return effects;
    }

    internal static IReadOnlyList<ContinuousEffectState> BuildBattlefieldPowerStaticAuraEffects(
        MatchState state, string battlefieldObjectId, StaticAuraSpec aura)
    {
        if (BattlefieldLocalRules.Battlefield(state, battlefieldObjectId) is not { } battlefield
            || !StaticAuraSpecRules.IsBattlefieldPowerStaticAura(aura)) return [];
        var effects = new List<ContinuousEffectState>();
        var layerState = CoreRuleEngine.IsCurrentKeywordPowerFilter(aura)
            ? CoreRuleEngine.RecomputeRuleDerivedKeywords(state) : state;
        var participantObjectIds = BattlefieldScopedStaticAuraParticipantObjectIds(
            layerState,
            battlefieldObjectId,
            aura);
        var participantDependencyObjectIds = PublicFieldDependencyObjectIds(state, participantObjectIds);
        var metadata = BattlefieldPowerStaticAuraProjectionMetadata(aura);

        foreach (var participantObjectId in participantObjectIds)
        {
            var participant = state.CardObjects[participantObjectId];
            var keywordSources = CoreRuleEngine.IsCurrentKeywordPowerFilter(aura)
                ? CoreRuleEngine.InspectCurrentKeywordFilter(layerState, aura, layerState.CardObjects[participantObjectId]).PublicKeywordSourceIds : [];
            var sourceDependencyObjectIds = PublicStaticAuraDependencyObjectIds(state, new[] { battlefieldObjectId }.Concat(keywordSources));
            var targetDependencyObjectIds = PublicFieldDependencyObjectIds(state, [participantObjectId]);
            effects.Add(new ContinuousEffectState(
                $"{metadata.EffectIdPrefix}:{battlefieldObjectId}:{participantObjectId}",
                "BATTLEFIELD",
                ContinuousEffectLayers.StaticAura,
                metadata.Duration,
                participantObjectId,
                battlefieldObjectId,
                aura.PowerDeltaPerParticipant,
                participant.Power,
                participant.Power + aura.PowerDeltaPerParticipant,
                aura.Kind,
                battlefield.CardNo,
                metadata.SourcePath,
                true,
                LayerEngineFoundationResiduals(),
                Condition: metadata.Condition,
                Lifecycle: metadata.Lifecycle,
                ParticipantObjectIds: participantObjectIds,
                SourceDependencyObjectIds: sourceDependencyObjectIds,
                TargetDependencyObjectIds: targetDependencyObjectIds,
                ParticipantDependencyObjectIds: participantDependencyObjectIds));
        }
        return effects;
    }

    private static IReadOnlyList<string> BattlefieldScopedStaticAuraParticipantObjectIds(
        MatchState state,
        string battlefieldObjectId,
        StaticAuraSpec aura)
    {
        if (string.Equals(aura.TargetScope, StaticAuraTargetScopes.SameBattlefieldUnits, StringComparison.Ordinal))
        {
            return BattlefieldStaticAuraParticipantObjectIds(state, battlefieldObjectId);
        }

        if (string.Equals(aura.TargetScope, StaticAuraTargetScopes.SameBattlefieldFilteredUnits, StringComparison.Ordinal))
        {
            return BattlefieldFilteredStaticAuraParticipantObjectIds(
                state,
                battlefieldObjectId,
                aura);
        }

        return [];
    }

    private static (
        string EffectIdPrefix,
        string Duration,
        string SourcePath,
        string Condition,
        string Lifecycle) BattlefieldPowerStaticAuraProjectionMetadata(StaticAuraSpec aura)
    {
        if (string.Equals(aura.Kind, StaticAuraKinds.BattlefieldAllUnitsPowerPlusOne, StringComparison.Ordinal))
        {
            return (
                "STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE",
                "WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD",
                "CoreRuleEngine.ResolveBattlefieldAllUnitsPowerBonus",
                "SOURCE_BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE_AND_PARTICIPANT_UNIT_AT_BATTLEFIELD",
                "DERIVED_FROM_CURRENT_BATTLEFIELD_OBJECT_LOCATIONS");
        }

        if (string.Equals(aura.Kind, StaticAuraKinds.BattlefieldFilteredUnitsPower, StringComparison.Ordinal))
        {
            return (
                "STATIC_AURA:BATTLEFIELD_FILTERED_UNITS_POWER",
                aura.Duration,
                "CoreRuleEngine.ResolveBattlefieldFilteredUnitsPowerBonus",
                "SOURCE_BATTLEFIELD_FILTERED_UNITS_POWER_AND_PARTICIPANT_UNIT_AT_BATTLEFIELD",
                "DERIVED_FROM_CURRENT_BATTLEFIELD_FILTERED_OBJECT_LOCATIONS");
        }

        return (
            $"STATIC_AURA:{aura.Kind}",
            aura.Duration,
            "CoreRuleEngine.ResolveBattlefieldPowerStaticAuraBonus",
            $"SOURCE_BATTLEFIELD_{aura.TargetScope}_POWER_AND_PARTICIPANT_UNIT_AT_BATTLEFIELD",
            "DERIVED_FROM_CURRENT_BATTLEFIELD_OBJECT_LOCATIONS");
    }

    private static IReadOnlyList<ContinuousEffectState> BuildBattlefieldKeywordAuraEffects(MatchState state)
    {
        var effects = new List<ContinuousEffectState>();
        foreach (var battlefieldObjectId in state.PlayerZones
            .SelectMany(entry => entry.Value.Battlefields)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(battlefieldObjectId, out var battlefield)
                || battlefield.IsFaceDown
                || !battlefield.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal)
                || !IsObjectLocationCompatibleWithBattlefield(state, battlefieldObjectId, battlefieldObjectId))
            {
                continue;
            }

            foreach (var aura in StaticAuraSpecRules.GetStaticAuras(battlefield.CardNo).Where(StaticAuraSpecRules.IsBattlefieldKeywordStaticAura))
            {
                var participantObjectIds = BattlefieldScopedStaticAuraParticipantObjectIds(
                    state,
                    battlefieldObjectId,
                    aura);
                var effectIdPrefix = BattlefieldKeywordStaticAuraEffectIdPrefix(aura);

                foreach (var participantObjectId in participantObjectIds)
                {
                    effects.Add(new ContinuousEffectState(
                        $"{effectIdPrefix}:{battlefieldObjectId}:{participantObjectId}:{aura.GrantedKeyword}",
                        "OBJECT",
                        ContinuousEffectLayers.RuleText,
                        aura.Duration,
                        participantObjectId,
                        battlefieldObjectId));
                }
            }
        }

        return effects;
    }

    private static string BattlefieldKeywordStaticAuraEffectIdPrefix(StaticAuraSpec aura)
    {
        if (string.Equals(aura.TargetScope, StaticAuraTargetScopes.SameBattlefieldUnits, StringComparison.Ordinal))
        {
            return "RULE_TEXT:BATTLEFIELD_ALL_UNITS_KEYWORD";
        }

        if (string.Equals(aura.TargetScope, StaticAuraTargetScopes.SameBattlefieldFilteredUnits, StringComparison.Ordinal))
        {
            return "RULE_TEXT:BATTLEFIELD_FILTERED_UNITS_KEYWORD";
        }

        return $"RULE_TEXT:{aura.Kind}";
    }

    private static IReadOnlyList<ContinuousEffectState> BuildBattlefieldIsolatedDefenderKeywordModifierAuraEffects(MatchState state)
    {
        var effects = new List<ContinuousEffectState>();
        var battleState = state.BattleState;
        if (!battleState.IsActive
            || string.IsNullOrWhiteSpace(battleState.BattlefieldObjectId))
        {
            return effects;
        }

        foreach (var battlefieldObjectId in state.PlayerZones
            .SelectMany(entry => entry.Value.Battlefields)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal))
        {
            if (!string.Equals(battleState.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal)
                || !state.CardObjects.TryGetValue(battlefieldObjectId, out var battlefield)
                || battlefield.IsFaceDown
                || !battlefield.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal)
                || !IsObjectLocationCompatibleWithBattlefield(state, battlefieldObjectId, battlefieldObjectId))
            {
                continue;
            }

            var defenderObjectIds = battleState.DefenderObjectIds
                .Where(objectId => state.CardObjects.TryGetValue(objectId, out var participant)
                    && participant.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                    && !participant.IsFaceDown
                    && TryFindFieldObjectLocation(state.PlayerZones, objectId, out var fieldLocation)
                    && string.Equals(fieldLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                    && IsPublicFieldObjectLocationCompatible(state, objectId, "BATTLEFIELD")
                    && state.ObjectLocations.TryGetValue(objectId, out var objectLocation)
                    && string.Equals(objectLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                    && string.Equals(objectLocation.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(objectId => objectId, StringComparer.Ordinal)
                .ToArray();
            if (defenderObjectIds.Length != 1)
            {
                continue;
            }

            var participantObjectId = defenderObjectIds[0];
            foreach (var aura in StaticAuraSpecRules.GetStaticAuras(battlefield.CardNo)
                .Where(StaticAuraSpecRules.IsBattlefieldIsolatedDefenderKeywordModifierStaticAura))
            {
                if (string.IsNullOrWhiteSpace(aura.GrantedKeyword))
                {
                    continue;
                }

                effects.Add(new ContinuousEffectState(
                    $"RULE_TEXT:BATTLEFIELD_ISOLATED_DEFENDER_KEYWORD_MODIFIER:{battlefieldObjectId}:{participantObjectId}:{aura.GrantedKeyword}",
                    "OBJECT",
                    ContinuousEffectLayers.RuleText,
                    aura.Duration,
                    participantObjectId,
                    battlefieldObjectId));
            }
        }

        return effects;
    }

    private static bool TryFindBattlefieldUnitLocation(
        MatchState state,
        string objectId,
        CardObjectState cardObject,
        out string battlefieldObjectId)
    {
        battlefieldObjectId = string.Empty;
        if (!cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || cardObject.IsFaceDown
            || !TryFindFieldObjectLocation(state.PlayerZones, objectId, out var fieldLocation)
            || !string.Equals(fieldLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            || !state.ObjectLocations.TryGetValue(objectId, out var objectLocation)
            || !string.Equals(objectLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(objectLocation.BattlefieldObjectId))
        {
            return false;
        }

        battlefieldObjectId = objectLocation.BattlefieldObjectId;
        return true;
    }

    private static IReadOnlyList<string> BattleAttackerObjectIds(MatchState state)
    {
        return state.BattleState.AttackerObjectIds
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && !cardObject.IsFaceDown
                && TryFindFieldObjectLocation(state.PlayerZones, objectId, out var fieldLocation)
                && string.Equals(fieldLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                && IsPublicFieldObjectLocationCompatible(state, objectId, "BATTLEFIELD"))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> BattleDefenderObjectIds(MatchState state)
    {
        return state.BattleState.DefenderObjectIds
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && !cardObject.IsFaceDown
                && TryFindFieldObjectLocation(state.PlayerZones, objectId, out var fieldLocation)
                && string.Equals(fieldLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                && IsPublicFieldObjectLocationCompatible(state, objectId, "BATTLEFIELD"))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> BattleParticipantObjectIds(MatchState state)
    {
        return BattleAttackerObjectIds(state)
            .Concat(BattleDefenderObjectIds(state))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> BattleReadyDefenderObjectIds(MatchState state)
    {
        return BattleDefenderObjectIds(state)
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && !cardObject.IsExhausted)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> SameBattlefieldOtherFriendlyUnitObjectIds(
        MatchState state,
        string sourceObjectId,
        string battlefieldObjectId,
        string controllerId)
    {
        return state.ObjectLocations
            .Where(entry => !string.Equals(entry.Key, sourceObjectId, StringComparison.Ordinal)
                && string.Equals(entry.Value.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                && string.Equals(entry.Value.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal)
                && state.CardObjects.TryGetValue(entry.Key, out var participant)
                && participant.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && !participant.IsFaceDown
                && TryFindFieldObjectLocation(state.PlayerZones, entry.Key, out var fieldLocation)
                && string.Equals(fieldLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                && IsPublicFieldObjectLocationCompatible(state, entry.Key, "BATTLEFIELD")
                && string.Equals(
                    EffectiveFieldControllerId(state, entry.Key, participant),
                    controllerId,
                    StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> SameLocationOtherFriendlyUnitObjectIds(
        MatchState state,
        string sourceObjectId,
        (string PlayerId, string Zone) sourceFieldLocation,
        string controllerId)
    {
        return state.PlayerZones
            .SelectMany(entry => entry.Value.Base.Concat(entry.Value.Battlefields))
            .Distinct(StringComparer.Ordinal)
            .Where(objectId => !string.Equals(objectId, sourceObjectId, StringComparison.Ordinal)
                && state.CardObjects.TryGetValue(objectId, out var participant)
                && participant.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && !participant.IsFaceDown
                && TryFindFieldObjectLocation(state.PlayerZones, objectId, out var participantFieldLocation)
                && IsSamePublicFieldLocation(state, sourceObjectId, objectId, sourceFieldLocation, participantFieldLocation)
                && IsPublicFieldObjectLocationCompatible(state, objectId, participantFieldLocation.Zone)
                && string.Equals(
                    EffectiveFieldControllerId(state, objectId, participant),
                    controllerId,
                    StringComparison.Ordinal))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsSamePublicFieldLocation(
        MatchState state,
        string sourceObjectId,
        string participantObjectId,
        (string PlayerId, string Zone) sourceFieldLocation,
        (string PlayerId, string Zone) participantFieldLocation)
    {
        if (!string.Equals(sourceFieldLocation.Zone, participantFieldLocation.Zone, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(sourceFieldLocation.Zone, "BASE", StringComparison.Ordinal))
        {
            return string.Equals(sourceFieldLocation.PlayerId, participantFieldLocation.PlayerId, StringComparison.Ordinal);
        }

        if (!string.Equals(sourceFieldLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            || !state.ObjectLocations.TryGetValue(sourceObjectId, out var sourceLocation)
            || !state.ObjectLocations.TryGetValue(participantObjectId, out var participantLocation)
            || !string.Equals(sourceLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            || !string.Equals(participantLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(sourceLocation.BattlefieldObjectId))
        {
            return false;
        }

        return string.Equals(
            sourceLocation.BattlefieldObjectId,
            participantLocation.BattlefieldObjectId,
            StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> SameBattlefieldFriendlyFilteredUnitObjectIds(
        MatchState state,
        string battlefieldObjectId,
        string controllerId,
        StaticAuraSpec aura)
    {
        return state.ObjectLocations
            .Where(entry => string.Equals(entry.Value.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                && string.Equals(entry.Value.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal)
                && state.CardObjects.TryGetValue(entry.Key, out var participant)
                && participant.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && !participant.IsFaceDown
                && StaticAuraSpecRules.TargetMatchesFilter(aura, participant)
                && TryFindFieldObjectLocation(state.PlayerZones, entry.Key, out var fieldLocation)
                && string.Equals(fieldLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                && IsPublicFieldObjectLocationCompatible(state, entry.Key, "BATTLEFIELD")
                && string.Equals(
                    EffectiveFieldControllerId(state, entry.Key, participant),
                    controllerId,
                    StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> SameBattlefieldOtherFriendlyFilteredUnitObjectIds(
        MatchState state,
        string sourceObjectId,
        string battlefieldObjectId,
        string controllerId,
        StaticAuraSpec aura)
    {
        return state.ObjectLocations
            .Where(entry => !string.Equals(entry.Key, sourceObjectId, StringComparison.Ordinal)
                && string.Equals(entry.Value.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                && string.Equals(entry.Value.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal)
                && state.CardObjects.TryGetValue(entry.Key, out var participant)
                && participant.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && !participant.IsFaceDown
                && StaticAuraSpecRules.TargetMatchesFilter(aura, participant)
                && TryFindFieldObjectLocation(state.PlayerZones, entry.Key, out var fieldLocation)
                && string.Equals(fieldLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                && IsPublicFieldObjectLocationCompatible(state, entry.Key, "BATTLEFIELD")
                && string.Equals(
                    EffectiveFieldControllerId(state, entry.Key, participant),
                    controllerId,
                    StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> OtherFriendlyPublicFieldUnitObjectIds(
        MatchState state,
        string sourceObjectId,
        string controllerId)
    {
        return state.PlayerZones
            .SelectMany(entry => entry.Value.Base.Concat(entry.Value.Battlefields))
            .Distinct(StringComparer.Ordinal)
            .Where(objectId => !string.Equals(objectId, sourceObjectId, StringComparison.Ordinal)
                && state.CardObjects.TryGetValue(objectId, out var candidate)
                && candidate.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && !candidate.IsFaceDown
                && TryFindFieldObjectLocation(state.PlayerZones, objectId, out var fieldLocation)
                && IsPublicFieldObjectLocationCompatible(state, objectId, fieldLocation.Zone)
                && string.Equals(
                    EffectiveFieldControllerId(state, objectId, candidate),
                    controllerId,
                    StringComparison.Ordinal))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> FriendlyPublicFieldUnitObjectIds(
        MatchState state,
        string controllerId)
    {
        return state.PlayerZones
            .SelectMany(entry => entry.Value.Base.Concat(entry.Value.Battlefields))
            .Distinct(StringComparer.Ordinal)
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var candidate)
                && candidate.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && !candidate.IsFaceDown
                && TryFindFieldObjectLocation(state.PlayerZones, objectId, out var fieldLocation)
                && IsPublicFieldObjectLocationCompatible(state, objectId, fieldLocation.Zone)
                && string.Equals(
                    EffectiveFieldControllerId(state, objectId, candidate),
                    controllerId,
                    StringComparison.Ordinal))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> FriendlyFilteredPublicFieldUnitObjectIds(
        MatchState state,
        string controllerId,
        StaticAuraSpec aura)
    {
        return state.PlayerZones
            .SelectMany(entry => entry.Value.Base.Concat(entry.Value.Battlefields))
            .Distinct(StringComparer.Ordinal)
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var candidate)
                && candidate.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && !candidate.IsFaceDown
                && TryFindFieldObjectLocation(state.PlayerZones, objectId, out var fieldLocation)
                && IsPublicFieldObjectLocationCompatible(state, objectId, fieldLocation.Zone)
                && StaticAuraSpecRules.TargetMatchesFilter(aura, candidate)
                && string.Equals(
                    EffectiveFieldControllerId(state, objectId, candidate),
                    controllerId,
                    StringComparison.Ordinal))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> BattlefieldStaticAuraParticipantObjectIds(
        MatchState state,
        string battlefieldObjectId)
    {
        var locatedParticipantObjectIds = state.ObjectLocations
            .Where(entry => string.Equals(entry.Value.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                && string.Equals(entry.Value.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal)
                && state.CardObjects.TryGetValue(entry.Key, out var participant)
                && participant.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && !participant.IsFaceDown
                && TryFindFieldObjectLocation(state.PlayerZones, entry.Key, out _))
            .Select(entry => entry.Key)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        if (locatedParticipantObjectIds.Length > 0)
        {
            return locatedParticipantObjectIds;
        }

        return state.PlayerZones
            .SelectMany(entry => entry.Value.Battlefields)
            .Where(objectId => !string.Equals(objectId, battlefieldObjectId, StringComparison.Ordinal)
                && state.CardObjects.TryGetValue(objectId, out var participant)
                && participant.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && !participant.IsFaceDown
                && IsObjectLocationCompatibleWithBattlefield(state, objectId, battlefieldObjectId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> BattlefieldFilteredStaticAuraParticipantObjectIds(
        MatchState state,
        string battlefieldObjectId,
        StaticAuraSpec aura)
    {
        if (CoreRuleEngine.IsCurrentKeywordPowerFilter(aura)) state = CoreRuleEngine.RecomputeRuleDerivedKeywords(state);
        var locatedParticipantObjectIds = state.ObjectLocations
            .Where(entry => string.Equals(entry.Value.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                && string.Equals(entry.Value.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal)
                && state.CardObjects.TryGetValue(entry.Key, out var participant)
                && participant.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && !participant.IsFaceDown
                && CoreRuleEngine.BattlefieldStaticAuraAppliesToParticipant(state, aura, participant)
                && TryFindFieldObjectLocation(state.PlayerZones, entry.Key, out _))
            .Select(entry => entry.Key)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        if (locatedParticipantObjectIds.Length > 0)
        {
            return locatedParticipantObjectIds;
        }

        return state.PlayerZones
            .SelectMany(entry => entry.Value.Battlefields)
            .Where(objectId => !string.Equals(objectId, battlefieldObjectId, StringComparison.Ordinal)
                && state.CardObjects.TryGetValue(objectId, out var participant)
                && participant.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && !participant.IsFaceDown
                && CoreRuleEngine.BattlefieldStaticAuraAppliesToParticipant(state, aura, participant)
                && IsObjectLocationCompatibleWithBattlefield(state, objectId, battlefieldObjectId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> PublicFieldDependencyObjectIds(
        MatchState state,
        IEnumerable<string?> objectIds)
    {
        return objectIds
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Select(objectId => objectId!.Trim())
            .Distinct(StringComparer.Ordinal)
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && !cardObject.IsFaceDown
                && TryFindFieldObjectLocation(state.PlayerZones, objectId, out var location)
                && IsPublicFieldObjectLocationCompatible(state, objectId, location.Zone))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> PublicStaticAuraDependencyObjectIds(
        MatchState state,
        IEnumerable<string?> objectIds)
    {
        return objectIds
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Select(objectId => objectId!.Trim())
            .Distinct(StringComparer.Ordinal)
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && !cardObject.IsFaceDown
                && TryFindPublicStaticAuraSourceLocation(state.PlayerZones, objectId, out var location)
                && (string.Equals(location.Zone, "LEGEND", StringComparison.Ordinal)
                    || IsPublicFieldObjectLocationCompatible(state, objectId, location.Zone)))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }
}

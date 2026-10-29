using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static void ValidateContinuousEffectStaticAuraObjectSourceTargetConsistency(
        string effectLabel,
        string? scope,
        string? layer,
        string? duration,
        string? targetObjectId,
        string? sourceObjectId,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || duration is null
            || targetObjectId is null
            || sourceObjectId is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !IsKnownContinuousEffectDuration(duration)
            || !string.Equals(scope, "OBJECT", StringComparison.Ordinal)
            || !string.Equals(layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            || !string.Equals(duration, "WHILE_SOURCE_ON_PUBLIC_FIELD", StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer)
            || string.Equals(targetObjectId, sourceObjectId, StringComparison.Ordinal))
        {
            return;
        }

        errors.Add(
            $"{effectLabel} object static aura target object id {targetObjectId} must match source object id {sourceObjectId}");
    }

    private static void ValidateContinuousEffectStaticAuraBattlefieldFilteredSourceCardSpecConsistency(
        string effectLabel,
        string? sourceCardNo,
        List<string> errors)
    {
        if (sourceCardNo is null
            || StaticAuraSpecRules.GetStaticAuras(sourceCardNo)
                .Any(StaticAuraSpecRules.IsBattlefieldFilteredUnitsPowerStaticAura))
        {
            return;
        }

        errors.Add(
            $"{effectLabel} battlefield static aura source card no must reference a BehaviorSpec battlefield filtered power aura");
    }

    private static void ValidateContinuousEffectStaticAuraSameBattlefieldFriendlyFilteredCountSourceCardSpecConsistency(
        string effectLabel,
        string? sourceCardNo,
        List<string> errors)
    {
        if (sourceCardNo is null
            || StaticAuraSpecRules.GetStaticAuras(sourceCardNo)
                .Any(StaticAuraSpecRules.IsSameBattlefieldFriendlyFilteredUnitCountToSourcePowerStaticAura))
        {
            return;
        }

        errors.Add(
            $"{effectLabel} object static aura source card no must reference a BehaviorSpec same-battlefield friendly filtered count-to-source power aura");
    }

    private static void ValidateContinuousEffectStaticAuraSourceObjectFilteredSourceCardSpecConsistency(
        string effectLabel,
        string? sourceCardNo,
        List<string> errors)
    {
        if (sourceCardNo is null
            || StaticAuraSpecRules.GetStaticAuras(sourceCardNo)
                .Any(StaticAuraSpecRules.IsSourceObjectFilteredPowerStaticAura))
        {
            return;
        }

        errors.Add(
            $"{effectLabel} object static aura source card no must reference a BehaviorSpec source-object filtered power aura");
    }

    private static void ValidateContinuousEffectStaticAuraSourceObjectPowerSourceCardSpecConsistency(
        string effectLabel,
        string? sourceCardNo,
        List<string> errors)
    {
        if (sourceCardNo is null
            || StaticAuraSpecRules.GetStaticAuras(sourceCardNo)
                .Any(StaticAuraSpecRules.IsSourceObjectUnfilteredPowerStaticAura))
        {
            return;
        }

        errors.Add(
            $"{effectLabel} object static aura source card no must reference a BehaviorSpec source-object power aura");
    }

    private static void ValidateContinuousEffectStaticAuraSameLocationOtherFriendlySourceCardSpecConsistency(
        string effectLabel,
        string? sourceCardNo,
        List<string> errors)
    {
        if (sourceCardNo is null
            || StaticAuraSpecRules.GetStaticAuras(sourceCardNo)
                .Any(StaticAuraSpecRules.IsSameLocationOtherFriendlyUnitPowerStaticAura))
        {
            return;
        }

        errors.Add(
            $"{effectLabel} object static aura source card no must reference a BehaviorSpec same-location other-friendly source power aura");
    }

    private static void ValidateContinuousEffectStaticAuraSameBattlefieldOtherFriendlySourceCardSpecConsistency(
        string effectLabel,
        string? sourceCardNo,
        List<string> errors)
    {
        if (sourceCardNo is null
            || StaticAuraSpecRules.GetStaticAuras(sourceCardNo)
                .Any(StaticAuraSpecRules.IsSameBattlefieldOtherFriendlyUnitsPowerStaticAura))
        {
            return;
        }

        errors.Add(
            $"{effectLabel} battlefield static aura source card no must reference a BehaviorSpec same-battlefield other-friendly power aura");
    }

    private static void ValidateContinuousEffectStaticAuraSameBattlefieldOtherFriendlyFilteredSourceCardSpecConsistency(
        string effectLabel,
        string? sourceCardNo,
        List<string> errors)
    {
        if (sourceCardNo is null
            || StaticAuraSpecRules.GetStaticAuras(sourceCardNo)
                .Any(StaticAuraSpecRules.IsSameBattlefieldOtherFriendlyFilteredUnitsPowerStaticAura))
        {
            return;
        }

        errors.Add(
            $"{effectLabel} battlefield static aura source card no must reference a BehaviorSpec same-battlefield filtered other-friendly power aura");
    }

    private static void ValidateContinuousEffectStaticAuraOtherFriendlySourceCardSpecConsistency(
        string effectLabel,
        string? sourceCardNo,
        List<string> errors)
    {
        if (sourceCardNo is null
            || StaticAuraSpecRules.GetStaticAuras(sourceCardNo)
                .Any(StaticAuraSpecRules.IsOtherFriendlyUnitsPowerStaticAura))
        {
            return;
        }

        errors.Add(
            $"{effectLabel} object static aura source card no must reference a BehaviorSpec other-friendly power aura");
    }

    private static void ValidateContinuousEffectStaticAuraFriendlyUnitsSourceCardSpecConsistency(
        string effectLabel,
        string? sourceCardNo,
        List<string> errors)
    {
        if (sourceCardNo is null
            || StaticAuraSpecRules.GetStaticAuras(sourceCardNo)
                .Any(StaticAuraSpecRules.IsFriendlyUnitsPowerStaticAura))
        {
            return;
        }

        errors.Add(
            $"{effectLabel} object static aura source card no must reference a BehaviorSpec friendly units power aura");
    }

    private static void ValidateContinuousEffectStaticAuraFriendlyFilteredSourceCardSpecConsistency(
        string effectLabel,
        string? sourceCardNo,
        List<string> errors)
    {
        if (sourceCardNo is null
            || StaticAuraSpecRules.GetStaticAuras(sourceCardNo)
                .Any(StaticAuraSpecRules.IsFriendlyFilteredUnitsPowerStaticAura))
        {
            return;
        }

        errors.Add(
            $"{effectLabel} object static aura source card no must reference a BehaviorSpec friendly filtered power aura");
    }

    private static void ValidateContinuousEffectStaticAuraSourceAttackingWithAnotherUnitSourceCardSpecConsistency(
        string effectLabel,
        string? sourceCardNo,
        List<string> errors)
    {
        if (sourceCardNo is null
            || StaticAuraSpecRules.GetStaticAuras(sourceCardNo).Any(
                StaticAuraSpecRules.IsSourceAttackingWithAnotherUnitPowerStaticAura))
        {
            return;
        }

        errors.Add(
            $"{effectLabel} object static aura source card no must reference a BehaviorSpec source attacking-with-another-unit power aura");
    }

    private static void ValidateContinuousEffectStaticAuraSourceLoneBattleSourceCardSpecConsistency(
        string effectLabel,
        string? sourceCardNo,
        List<string> errors)
    {
        if (sourceCardNo is null
            || StaticAuraSpecRules.GetStaticAuras(sourceCardNo).Any(
                StaticAuraSpecRules.IsSourceLoneBattlePowerStaticAura))
        {
            return;
        }

        errors.Add(
            $"{effectLabel} object static aura source card no must reference a BehaviorSpec source lone-battle power aura");
    }

    private static void ValidateContinuousEffectStaticAuraSourceAttackingReadyEnemyUnitSourceCardSpecConsistency(
        string effectLabel,
        string? sourceCardNo,
        List<string> errors)
    {
        if (sourceCardNo is null
            || StaticAuraSpecRules.GetStaticAuras(sourceCardNo).Any(
                StaticAuraSpecRules.IsSourceAttackingReadyEnemyUnitPowerStaticAura))
        {
            return;
        }

        errors.Add(
            $"{effectLabel} object static aura source card no must reference a BehaviorSpec source attacking ready-enemy-unit power aura");
    }

    private static void ValidateContinuousEffectStaticAuraMetadataValue(
        string effectLabel,
        string? actual,
        string expected,
        string diagnosticLabel,
        List<string> errors)
    {
        if (actual is null || string.Equals(actual, expected, StringComparison.Ordinal))
        {
            return;
        }

        errors.Add($"{effectLabel} {diagnosticLabel} must be {expected}");
    }
}

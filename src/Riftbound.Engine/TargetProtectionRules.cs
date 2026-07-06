using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class TargetProtectionRules
{
    public static bool IsLegalPlayCardSpellOrSkillTarget(
        MatchState state,
        string sourceControllerId,
        CardBehaviorDefinition behavior,
        string targetObjectId)
    {
        if (!IsSpellOrSkillPlayCardBehavior(behavior))
        {
            return true;
        }

        return !IsProtectedFromEnemySpellOrSkillTarget(state, sourceControllerId, targetObjectId);
    }

    public static bool IsLegalActivatedSkillTarget(
        MatchState state,
        string sourceControllerId,
        string targetObjectId)
    {
        return !IsProtectedFromEnemySpellOrSkillTarget(state, sourceControllerId, targetObjectId);
    }

    private static bool IsSpellOrSkillPlayCardBehavior(CardBehaviorDefinition behavior)
    {
        return !behavior.PlaysSourceToBaseAsUnit
            && !behavior.PlaysSourceToBaseAsEquipment;
    }

    private static bool IsProtectedFromEnemySpellOrSkillTarget(
        MatchState state,
        string sourceControllerId,
        string targetObjectId)
    {
        if (string.IsNullOrWhiteSpace(sourceControllerId)
            || string.IsNullOrWhiteSpace(targetObjectId)
            || !TryFindPublicFieldLocation(state.PlayerZones, targetObjectId, out var targetLocation)
            || string.Equals(targetLocation.PlayerId, sourceControllerId, StringComparison.Ordinal)
            || !state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            || !IsVisibleSourceUnit(targetState)
            || !ObjectControlledByPlayerOrLegacyOwned(targetState, targetLocation.PlayerId)
            || !CardStaticAbilitySpecRules.TryGetStaticAbility(
                targetState.CardNo,
                CardStaticAbilitySpecRules.IsSourceUnitEnemySpellSkillTargetProtectionAbility,
                out var ability))
        {
            return false;
        }

        return !ability.RequiredPlayerExperience.HasValue
            || state.PlayerExperience.TryGetValue(targetLocation.PlayerId, out var experience)
                && experience >= ability.RequiredPlayerExperience.Value;
    }

    private static bool IsVisibleSourceUnit(CardObjectState targetState)
    {
        return !targetState.IsFaceDown
            && targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && !targetState.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal)
            && !targetState.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
            && !targetState.Tags.Contains(CardObjectTags.SpellCard, StringComparer.Ordinal)
            && !targetState.Tags.Contains(CardObjectTags.RuneCard, StringComparer.Ordinal);
    }

    private static bool ObjectControlledByPlayerOrLegacyOwned(CardObjectState cardObject, string playerId)
    {
        return string.Equals(cardObject.ControllerId, playerId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(cardObject.ControllerId)
                && string.Equals(cardObject.OwnerId, playerId, StringComparison.Ordinal);
    }

    private static bool TryFindPublicFieldLocation(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId,
        out (string PlayerId, string Zone) location)
    {
        foreach (var (playerId, zones) in playerZones)
        {
            if (zones.Base.Contains(objectId, StringComparer.Ordinal))
            {
                location = (playerId, "BASE");
                return true;
            }

            if (zones.Battlefields.Contains(objectId, StringComparer.Ordinal))
            {
                location = (playerId, "BATTLEFIELD");
                return true;
            }
        }

        location = default;
        return false;
    }
}

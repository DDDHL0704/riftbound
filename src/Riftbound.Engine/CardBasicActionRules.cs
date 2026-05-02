using Riftbound.Contracts;

namespace Riftbound.Engine;

public static class CardBasicActionNames
{
    public const string Draw = "draw";
    public const string Damage = "damage";
    public const string Destroy = "destroy";
    public const string Stun = "stun";
    public const string Move = "move";
    public const string Recall = "recall";
    public const string Recycle = "recycle";
    public const string Banish = "banish";
    public const string TempMight = "temp_might";
    public const string Boon = "boon";
    public const string Experience = "experience";
}

public static class CardBasicActionProfileStatuses
{
    public const string RecognizedCovered = "recognized-covered";
    public const string MixedDeferred = "mixed-deferred";
    public const string NotApplicable = "not-applicable";
}

public sealed record CardBasicActionProfile(
    bool HasDraw,
    bool HasDamage,
    bool HasDestroy,
    bool HasStun,
    bool HasMove,
    bool HasRecall,
    bool HasRecycle,
    bool HasBanish,
    bool HasTempMight,
    bool HasBoon,
    bool HasExperience,
    IReadOnlyList<string> PrimitiveActions,
    IReadOnlyList<string> DelegatedP2Actions,
    IReadOnlyList<string> DeferredActions,
    string Status,
    string Reason);

public static class CardBasicActionRules
{
    public static CardBasicActionProfile BuildProfile(
        BehaviorSpec spec,
        CardBehaviorDefinition? behavior)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var hasDraw = HasTemplate(spec, BehaviorTemplateIds.Draw)
            || behavior?.DrawCount > 0
            || behavior?.LevelDrawOnPlayCount > 0;
        var hasDamage = HasTemplate(spec, BehaviorTemplateIds.Damage) || HasDamageBehavior(behavior);
        var hasDestroy = HasTemplate(spec, BehaviorTemplateIds.Destroy) || HasDestroyBehavior(behavior);
        var hasStun = HasTemplate(spec, BehaviorTemplateIds.Stun)
            || !string.IsNullOrWhiteSpace(behavior?.StatusEffectId);
        var hasMove = HasTemplate(spec, BehaviorTemplateIds.Move) || HasMoveBehavior(behavior);
        var hasRecall = HasTemplate(spec, BehaviorTemplateIds.Recall) || HasRecallBehavior(behavior);
        var hasRecycle = HasRecycleBehavior(behavior) || spec.OfficialText.Contains("回收", StringComparison.Ordinal);
        var hasBanish = HasBanishBehavior(behavior) || spec.OfficialText.Contains("放逐", StringComparison.Ordinal);
        var hasTempMight = HasTemplate(spec, BehaviorTemplateIds.TempMight)
            || behavior?.PowerModifierAmount != 0
            || behavior?.SecondaryPowerModifierAmount != 0;
        var hasBoon = HasBoonBehavior(behavior) || spec.OfficialText.Contains(CardObjectTags.Boon, StringComparison.Ordinal);
        var hasExperience = HasTemplate(spec, BehaviorTemplateIds.GainExperience)
            || spec.OfficialText.Contains("经验", StringComparison.Ordinal);
        var hasDrawPrimitive = behavior?.DrawCount > 0 || behavior?.LevelDrawOnPlayCount > 0;
        var hasDamagePrimitive = behavior?.DamageAmount > 0;
        var hasDestroyPrimitive = behavior?.DestroysTarget == true;
        var hasStunPrimitive = !string.IsNullOrWhiteSpace(behavior?.StatusEffectId);
        var hasTempMightPrimitive = behavior?.PowerModifierAmount != 0;

        var primitiveActions = new List<string>();
        AddIf(primitiveActions, hasDrawPrimitive, CardBasicActionNames.Draw);
        AddIf(primitiveActions, hasDamagePrimitive, CardBasicActionNames.Damage);
        AddIf(primitiveActions, hasDestroyPrimitive, CardBasicActionNames.Destroy);
        AddIf(primitiveActions, hasStunPrimitive, CardBasicActionNames.Stun);
        AddIf(primitiveActions, hasTempMightPrimitive, CardBasicActionNames.TempMight);

        var delegatedActions = new List<string>();
        AddIf(delegatedActions, hasMove && HasMoveBehavior(behavior), CardBasicActionNames.Move);
        AddIf(delegatedActions, hasRecall && HasRecallBehavior(behavior), CardBasicActionNames.Recall);
        AddIf(delegatedActions, hasRecycle && HasRecycleBehavior(behavior), CardBasicActionNames.Recycle);
        AddIf(delegatedActions, hasBanish && HasBanishBehavior(behavior), CardBasicActionNames.Banish);
        AddIf(delegatedActions, hasBoon && HasBoonBehavior(behavior), CardBasicActionNames.Boon);
        AddIf(delegatedActions, hasExperience && HasExperienceBehavior(behavior), CardBasicActionNames.Experience);

        var deferredActions = new List<string>();
        AddIf(deferredActions, hasDraw && !hasDrawPrimitive, CardBasicActionNames.Draw);
        AddIf(deferredActions, hasDamage && !hasDamagePrimitive, CardBasicActionNames.Damage);
        AddIf(deferredActions, hasDestroy && !hasDestroyPrimitive, CardBasicActionNames.Destroy);
        AddIf(deferredActions, hasStun && !hasStunPrimitive, CardBasicActionNames.Stun);
        AddIf(deferredActions, hasMove && !HasMoveBehavior(behavior), CardBasicActionNames.Move);
        AddIf(deferredActions, hasRecall && !HasRecallBehavior(behavior), CardBasicActionNames.Recall);
        AddIf(deferredActions, hasRecycle && !HasRecycleBehavior(behavior), CardBasicActionNames.Recycle);
        AddIf(deferredActions, hasBanish && !HasBanishBehavior(behavior), CardBasicActionNames.Banish);
        AddIf(deferredActions, hasTempMight && !hasTempMightPrimitive, CardBasicActionNames.TempMight);
        AddIf(deferredActions, hasBoon && !HasBoonBehavior(behavior), CardBasicActionNames.Boon);
        AddIf(deferredActions, hasExperience && !HasExperienceBehavior(behavior), CardBasicActionNames.Experience);

        var hasAnyAction = hasDraw
            || hasDamage
            || hasDestroy
            || hasStun
            || hasMove
            || hasRecall
            || hasRecycle
            || hasBanish
            || hasTempMight
            || hasBoon
            || hasExperience;
        var status = !hasAnyAction
            ? CardBasicActionProfileStatuses.NotApplicable
            : deferredActions.Count > 0
                ? CardBasicActionProfileStatuses.MixedDeferred
                : CardBasicActionProfileStatuses.RecognizedCovered;

        return new CardBasicActionProfile(
            hasDraw,
            hasDamage,
            hasDestroy,
            hasStun,
            hasMove,
            hasRecall,
            hasRecycle,
            hasBanish,
            hasTempMight,
            hasBoon,
            hasExperience,
            primitiveActions,
            delegatedActions,
            deferredActions,
            status,
            status switch
            {
                CardBasicActionProfileStatuses.RecognizedCovered =>
                    "P4.10/P4.19 recognize these basic actions as covered by primitive plans or existing audited P2 behavior paths.",
                CardBasicActionProfileStatuses.MixedDeferred =>
                    "P4.10/P4.19 recognize these basic actions, but at least one requested action remains deferred, most commonly trigger-based experience, experience spend, or a non-audited branch.",
                _ =>
                    "Card does not expose P4 basic action surfaces through P3 BehaviorSpec or the P2 behavior path."
            });
    }

    private static bool HasTemplate(
        BehaviorSpec spec,
        string templateId)
    {
        return spec.TemplateIds.Any(candidate => string.Equals(candidate, templateId, StringComparison.Ordinal));
    }

    private static bool HasDamageBehavior(CardBehaviorDefinition? behavior)
    {
        return behavior is not null
            && (behavior.DamageAmount > 0
                || behavior.ConditionalDamageAmount > 0
                || behavior.DamagesAllBattlefieldUnits
                || behavior.DamagesAllEnemyCombatUnits
                || behavior.DamagesAllEnemyBattlefieldUnits
                || behavior.DamagesAllEnemyBattlefieldUnitsByFirstTargetPower
                || behavior.OtherEnemyBattlefieldUnitsDamageAmount > 0
                || behavior.DealsMutualTargetPowerDamage
                || behavior.DealsSourceAndTargetPowerDamage
                || behavior.DamagesSecondTargetByFirstTargetPower
                || behavior.ReadiesFirstTargetAndDamagesSecondByFirstPower
                || behavior.DamageAmountFromFirstTargetManaCost
                || behavior.DamageAmountFromOptionalPowerCost);
    }

    private static bool HasDestroyBehavior(CardBehaviorDefinition? behavior)
    {
        return behavior is not null
            && (behavior.DestroysTarget
                || behavior.DestroysAllEquipment
                || behavior.DestroysAllUnits
                || behavior.DestroysTargetIfAlreadyHasStatusEffect
                || behavior.DestroysFirstTargetAndBuffsSecondByDestroyedPower);
    }

    private static bool HasMoveBehavior(CardBehaviorDefinition? behavior)
    {
        return behavior is not null
            && (behavior.MovesTargetToBase
                || behavior.MovesTargetToBattlefield
                || behavior.MovesFirstTargetToSecondTargetLocation
                || behavior.MovesTargetsToOwnerBattlefields
                || behavior.SwapsTargetLocations);
    }

    private static bool HasRecallBehavior(CardBehaviorDefinition? behavior)
    {
        return behavior is not null
            && (behavior.ReturnsTargetToHand
                || behavior.ReturnsAllUnitsToHand
                || behavior.ReturnsAllFieldObjectsToHand
                || behavior.ReturnsTargetToHandIfAlreadyHasStatusEffect
                || behavior.ReturnsGraveyardTargetToHand
                || behavior.RuneCallCountAfterTargetReturn > 0
                || behavior.GainsControlOfTargetToBase);
    }

    private static bool HasRecycleBehavior(CardBehaviorDefinition? behavior)
    {
        return behavior is not null
            && (behavior.RecyclesTargets
                || behavior.RecyclesSelectedMainDeckTargets
                || behavior.RecyclesUnselectedMainDeckLookCards
                || behavior.RecyclesUnkeptSacredJudgmentCards);
    }

    private static bool HasBanishBehavior(CardBehaviorDefinition? behavior)
    {
        return behavior is not null
            && (behavior.BanishesIfDestroyedThisTurn
                || behavior.BanishesTargetThenPlaysToBase
                || behavior.BanishesTargetThenPlaysToBattlefield
                || behavior.BanishesAllFriendlyGraveyardUnits
                || behavior.BanishesSourceOnResolution);
    }

    private static bool HasBoonBehavior(CardBehaviorDefinition? behavior)
    {
        return behavior is not null
            && (behavior.GrantsBoon
                || behavior.GrantsBoonToAllFriendlyUnits
                || behavior.GrantsBoonToSourceUnit
                || string.Equals(behavior.TargetAddedTag, CardObjectTags.Boon, StringComparison.Ordinal));
    }

    private static bool HasExperienceBehavior(CardBehaviorDefinition? behavior)
    {
        return behavior is not null
            && (behavior.GainExperienceOnPlay > 0
                || behavior.GainExperienceOnPlayPerFriendlyFieldUnit > 0
                || behavior.OptionalExperienceCost > 0);
    }

    private static void AddIf(
        List<string> values,
        bool condition,
        string value)
    {
        if (condition && !values.Contains(value, StringComparer.Ordinal))
        {
            values.Add(value);
        }
    }
}

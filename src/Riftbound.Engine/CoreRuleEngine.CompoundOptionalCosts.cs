namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // Structural validation of an already confirmed unit's target-trigger fee.
    // The original command replay proves the grant at payment time; a current
    // aura scan here would revoke CN805.2.b's paid fact after a source departed.
    internal static bool TryBuildConfirmedPermanentTargetAdditionalCost(
        StackItemState item, CardBehaviorDefinition behavior,
        out int mana, out IReadOnlyDictionary<string, int> typed)
    {
        mana = 0;
        typed = new Dictionary<string, int>();
        var haste = item.OptionalCosts.Where(CardPermissionKeywordRules.IsHasteReadyCostChoice).ToArray();
        if (haste.Length == 0)
            return TryBuildTargetEffectAdditionalCost(item.OptionalCosts, behavior, out mana, out typed);
        if (!item.SourceConfirmed || !behavior.PlaysSourceToBaseAsUnit || item.CardNo != behavior.CardNo)
            return false;
        var hasteBehavior = behavior;
        if (!CardPermissionKeywordRules.HasSourceKeyword(behavior, CardPermissionKeywordNames.Haste))
        {
            if (item.PlayedFromHand) return false;
            hasteBehavior = WithGrantedHasteCost(behavior);
        }
        if (!CardPermissionKeywordRules.TryBuildHasteReadyOptionalCost(haste, hasteBehavior, out _, out _))
            return false;
        var targetCosts = item.OptionalCosts.Where(cost => !CardPermissionKeywordRules.IsHasteReadyCostChoice(cost)).ToArray();
        return TryBuildTargetEffectAdditionalCost(targetCosts, behavior, out mana, out typed);
    }
}

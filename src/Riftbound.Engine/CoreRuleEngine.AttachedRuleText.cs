using Riftbound.Contracts;

namespace Riftbound.Engine;

internal sealed record CopiedRulePower(RuleTextInstance Instance, string Kind, int Delta, int Index);

public sealed partial class CoreRuleEngine
{
    internal static IReadOnlyList<CopiedRulePower> CopiedRuleTextPowerContributions(MatchState state, CardObjectState unit)
    {
        var contributions = new List<CopiedRulePower>();
        foreach (var instance in AttachedRuleTextRules.Copies(state, unit))
        {
            foreach (var (aura, index) in StaticAuraSpecRules.GetStaticAuras(instance.CardNo).Select((a, i) => (a, i)))
            {
                if (StaticAuraSpecRules.IsSourceObjectPowerStaticAura(aura) && SourceObjectKeywordStaticAuraApplies(state, unit, aura))
                    contributions.Add(new(instance, aura.Kind, StaticAuraSpecRules.SourceObjectPowerDelta(aura, state, unit), index));
                else if (StaticAuraSpecRules.IsSourceParticipantCountPowerStaticAura(aura))
                {
                    var controller = EffectiveFieldControllerId(state.PlayerZones, unit.ObjectId, unit);
                    var count = aura.ParticipantScope == StaticAuraParticipantScopes.FriendlyPublicFieldEquipment
                        ? CountControlledPublicFieldEquipmentObjects(state.PlayerZones, state.CardObjects, controller)
                        : CountSourceParticipantCountPowerStaticAuraParticipants(state, state.PlayerZones, unit.ObjectId, controller, aura);
                    contributions.Add(new(instance, aura.Kind, SourceParticipantCountPowerDelta(aura, count), index));
                }
            }
        }
        return contributions;
    }

    internal static int CopiedRuleTextKeywordAmount(MatchState state, CardObjectState unit, string keyword)
        => AttachedRuleTextRules.Copies(state, unit).Sum(instance => CopiedRuleTextKeywordAmount(state, unit, instance, keyword));

    internal static int CopiedRuleTextKeywordAmount(MatchState state, CardObjectState unit, RuleTextInstance instance, string keyword)
    {
        var tags = AttachedRuleTextRules.PrintedKeywords(instance.CardNo);
        var auras = StaticAuraSpecRules.GetStaticAuras(instance.CardNo).Where(StaticAuraSpecRules.IsSourceObjectKeywordStaticAura)
            .Where(a => SourceObjectKeywordStaticAuraApplies(state, unit, a));
        if (keyword == CardResourceKeywordNames.Spellshield)
            return CardResourceKeywordRules.SpellshieldTaxFromTags(tags) + auras.Sum(a => GrantedResourceKeywordAmount(a, keyword));
        return Math.Max(CardCombatKeywordRules.KeywordAmount(tags, keyword),
            auras.Select(a => GrantedCombatKeywordAmount(a, keyword)).DefaultIfEmpty(0).Max());
    }
}

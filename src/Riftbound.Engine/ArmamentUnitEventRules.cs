using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record UnitEventAbilitySpec
{
    [JsonIgnore] public bool GrantedByArmament { get; init; }
}

// Lower-box text is granted to the host. It is neither the equipment's own
// printed text nor an extra printed-text instance for copies such as Song.
internal static class ArmamentUnitEventRules
{
    internal const string FixedDamageEffect = "GRANTED_FIXED_DAMAGE_HERE";
    internal const string AreaDamageEffect = "DAMAGE_ENEMIES_HERE";
    private static readonly Lazy<IReadOnlyDictionary<string, UnitEventAbilitySpec>> Specs = new(() =>
        OfficialArmamentCatalog.All.Select(definition => {
            var match = Regex.Match(definition.EffectText.Trim(),
                @"^当我进攻或防守时[，,]\s*对此处的一名敌方单位造成(?<amount>[1-9][0-9]*)点伤害[。.]?$",
                RegexOptions.CultureInvariant);
            var area = Regex.Match(definition.EffectText.Trim(),
                @"^当我进攻(?:及|或)防守时[，,]\s*对此处的所有敌方单位各造成(?<amount>[1-9][0-9]*)点伤害[。.]?$",
                RegexOptions.CultureInvariant);
            var spec = match.Success
                ? new UnitEventAbilitySpec("ATTACK_OR_DEFEND", FixedDamageEffect, int.Parse(match.Groups["amount"].Value))
                : area.Success ? new UnitEventAbilitySpec("ATTACK_OR_DEFEND", AreaDamageEffect, int.Parse(area.Groups["amount"].Value)) : null;
            if (spec is null && TriggerParser.Parse(definition.EffectText).FirstOrDefault(t =>
                    t.Kind == TriggerKinds.UnitBattlefieldHeldCreateEquipment && t.Timing == TriggerTimings.BattlefieldHeld
                    && t.TargetScope == TriggerTargetScopes.SourceUnit && t.CreatedTokenCount > 0
                    && t.CreatedTokenExhausted == true && t.CreatedTokenDestination == TriggerTokenDestinations.OwnerBase) is { } held
                && P6TokenFactoryCatalog.GetAll().FirstOrDefault(f => f.Tags.Contains(CardObjectTags.EquipmentCard)
                    && f.TokenFamilyName == held.CreatedTokenName) is { } factory)
                spec = new("HOLD", "CREATE_EQUIPMENT_TOKEN", held.CreatedTokenCount!.Value,
                    CreatedTokenFactoryCardNo: factory.CardNo);
            return (definition.CardNo, Spec: spec is null ? null : spec with { GrantedByArmament = true });
        }).Where(row => row.Spec is not null).ToDictionary(row => row.CardNo, row => row.Spec!, StringComparer.Ordinal));

    internal static UnitEventAbilitySpec? Spec(string no)
        => Specs.Value.GetValueOrDefault(no) ?? ArmamentLastBreathRules.Spec(no);

    internal static IEnumerable<RuleTextInstance> Instances(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState host)
        => ArmamentRuleTextRules.Instances(zones, cards, host, no => Spec(no) is not null);

    internal static bool ValidCaptured(MatchState state, RuleTextInstanceBinding origin, string hostId,
        long generation, string no)
        => ArmamentRuleTextRules.ValidCaptured(state, origin, hostId, generation, no, candidate => Spec(candidate) is not null);

    internal static string CaptureHash(UnitEventContext context)
    {
        var binding = MatchStateHasher.HashValue(new {
            context.CardNo, context.SourceGeneration, context.FaceRevision, context.CapturedTick,
            context.Timing, context.TokenFactoryCardNo, context.Origin });
        return context.ArmamentDeath is null ? binding
            : MatchStateHasher.HashValue(new { binding, context.ArmamentDeath });
    }
}

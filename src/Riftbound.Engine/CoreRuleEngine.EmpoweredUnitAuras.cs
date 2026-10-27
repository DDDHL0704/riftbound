using Riftbound.CardCatalog;

namespace Riftbound.Engine;

internal sealed record EmpoweredUnitAuraContribution(string SourceId, RuleTextInstance Instance, int Index, int Delta);

public sealed partial class CoreRuleEngine
{
    internal const string EmpoweredUnitAuraEffect = "EMPOWERED_FRIENDLY_UNIT_POWER";
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<EmpoweredUnitAuraSpec>>> EmpoweredUnitAuraProfiles = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards.ToDictionary(card => card.CardNo,
            card => EmpowerParser.FriendlyEmpoweredUnitAuras(card.CardEffect), StringComparer.Ordinal));

    internal static IReadOnlyList<EmpoweredUnitAuraSpec> EmpoweredUnitAuras(string? cardNo)
        => cardNo is not null && EmpoweredUnitAuraProfiles.Value.TryGetValue(cardNo, out var specs) ? specs : [];

    internal static IReadOnlyList<EmpoweredUnitAuraContribution> EmpoweredUnitAuraContributions(MatchState state, CardObjectState target)
    {
        if (!target.IsEmpowered || !IsFaceUpNonStandbyUnit(target) || !IsObjectOnField(state.PlayerZones, target.ObjectId)) return [];
        var controller = EffectiveFieldControllerId(state.PlayerZones, target.ObjectId, target);
        var result = new List<EmpoweredUnitAuraContribution>();
        foreach (var source in state.CardObjects.Values.OrderBy(card => card.ObjectId, StringComparer.Ordinal)) {
            if (!source.IsEmpowered || !IsFaceUpNonStandbyUnit(source) || !IsObjectOnField(state.PlayerZones, source.ObjectId)
                || EffectiveFieldControllerId(state.PlayerZones, source.ObjectId, source) != controller) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
                foreach (var (spec, index) in EmpoweredUnitAuras(instance.CardNo).Select((spec, index) => (spec, index)))
                    result.Add(new(source.ObjectId, instance, index, spec.Might));
        }
        return result;
    }
}

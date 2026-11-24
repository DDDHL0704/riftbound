using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class RuntimeStaticCardSpecs
{
    internal static readonly Lazy<(string CardNo, IReadOnlyList<StaticAbilitySpec> StaticAbilities, IReadOnlyList<StaticAuraSpec> StaticAuras)[]>
        Entries = new(BuildEntries);

    private static (string CardNo, IReadOnlyList<StaticAbilitySpec> StaticAbilities, IReadOnlyList<StaticAuraSpec> StaticAuras)[] BuildEntries()
    {
        return RuntimeCardBehaviorSpecs.Entries
            .Where(spec => spec.StaticAbilities.Count > 0 || spec.StaticAuras.Count > 0)
            .Select(spec => (spec.CardNo, spec.StaticAbilities, spec.StaticAuras))
            .ToArray();
    }
}

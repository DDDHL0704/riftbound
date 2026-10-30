using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record SpellNameDeclaration(string Name, string CardNo, ObjectBinding Source, long Tick);

public sealed partial class CoreRuleEngine
{
    // A name is public rules input, independent of either player's deck contents.
    private static readonly Lazy<IReadOnlyDictionary<string, string>> OfficialSpellNames = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards.Concat(VendettaCardSources.GetOfficialCards())
            .Where(c => c.CardCategoryNameList.Any(n => n.Contains("法术", StringComparison.Ordinal)))
            .DistinctBy(c => c.CardNo).ToDictionary(c => c.CardNo, c => c.CardName, StringComparer.Ordinal));

    internal static bool IsDeclaredSpellNameProhibited(MatchState state, string player, CardBehaviorDefinition spell)
        => OfficialSpellNames.Value.TryGetValue(spell.CardNo, out var name) && state.CardObjects.Values.Any(source =>
            IsFaceUpNonStandbyUnit(source) && IsObjectOnField(state.PlayerZones, source.ObjectId)
            && source.DeclaredSpellName is { } declaration && declaration.Source == new ObjectBinding(source.ObjectId, source.ObjectGeneration)
            && declaration.Name == name && state.ObjectLocations.TryGetValue(source.ObjectId, out var at) && at.Zone == "BATTLEFIELD"
            && EffectiveFieldControllerId(state.PlayerZones, source.ObjectId, source) != player
            && StaticLimitsFor(state, source).Any(spec => spec.DeclaresSpellNameAndProhibitsItOnEnemyBattlefield));

    internal static bool ValidSpellNameDeclaration(MatchState state, CardObjectState card)
        => card.DeclaredSpellName is not { } declaration || declaration.Source == new ObjectBinding(card.ObjectId, card.ObjectGeneration)
            && declaration.Tick > 0 && declaration.Tick <= state.Tick && IsObjectOnField(state.PlayerZones, card.ObjectId)
            && OfficialSpellNames.Value.Values.Contains(declaration.Name, StringComparer.Ordinal)
            && CardBehaviorRegistry.TryGetByCardNo(declaration.CardNo, out var source)
            && source.SourceStaticLimits?.DeclaresSpellNameAndProhibitsItOnEnemyBattlefield == true;

    private static StackResolutionResult ResolveSpellNameDeclaration(MatchState state, StackItemState item)
    {
        var binding = item.PermanentPlaySource ?? throw new InvalidOperationException("Missing declaration source binding.");
        var name = RuleChoices.Value!.Choose(item.ControllerId, "宣告一个法术名称。",
            OfficialSpellNames.Value.Values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
                .Select(name => new RuleChoiceOption(name, name)).ToArray(), kind: "DECLARE_SPELL_NAME", preview: state);
        var cards = state.CardObjects.ToDictionary();
        if (cards.TryGetValue(binding.ObjectId, out var current) && current.ObjectGeneration == binding.Generation
            && IsObjectOnField(state.PlayerZones, current.ObjectId))
            cards[current.ObjectId] = current with { DeclaredSpellName = new(name, item.CardNo, binding,
                RuleChoices.Value.CompletionTick) };
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = [
            new("SPELL_NAME_DECLARED", "宣告法术名称", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                ["sourceCardNo"] = item.CardNo, ["declaredSpellName"] = name })] };
    }
}

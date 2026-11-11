using System.Text.RegularExpressions;
using Riftbound.CardCatalog;

namespace Riftbound.Engine;

// CN 820.1: each Echo is an independent optional additional cost. A granted
// base-cost Echo includes printed power; mana reductions never remove power.
public static class EchoCostRules
{
    public const string GrantPrefix = "BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO:";
    public sealed record Cost(string Id, int Mana, int GenericPower,
        IReadOnlyDictionary<string, int> TypedPower, string? PrintedCardNo = null);
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> SourceFamilies = new(() =>
        OfficialCardSourceIdentityGroups.BuildByRepresentativeCardNo(["UNL-146/219"]));
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<Cost>>> Printed = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards
            .Select(card => (card.CardNo, Costs: ParsePrintedCosts(card.CardEffect)))
            .Where(entry => entry.Costs.Count > 0)
            .ToDictionary(entry => entry.CardNo, entry => entry.Costs, StringComparer.Ordinal));

    private static IReadOnlyList<Cost> ParsePrintedCosts(string text)
    {
        var costs = new List<Cost>();
        foreach (Match match in Regex.Matches(text.Replace("\\n", "\n"), @"(?:^|\n)\{\{回响([^}]*)\}\}([^\n]*)"))
        {
            var suffix = Regex.Match(match.Groups[2].Value, @"^[^（(]*").Value.Trim();
            string[] declarations;
            if (match.Groups[1].Value.Length == 0 && suffix.StartsWith('—'))
                declarations = suffix[1..].Trim().Split('/');
            else declarations = [match.Groups[1].Value + suffix];
            foreach (var declaration in declarations)
            {
                var symbols = Regex.Replace(declaration, @"[{}\s]|支付|和", "");
                // Non-resource costs keep their existing authoritative cost path.
                // Never silently interpret an unsupported declaration as free Echo.
                if (!Regex.IsMatch(symbols, @"^(\d+)?(红色|绿色|蓝色|黄色|橙色|紫色|A)*$") || symbols.Length == 0)
                    return [];
                var number = Regex.Match(symbols, @"^\d+");
                var mana = number.Success ? int.Parse(number.Value) : 0;
                var typed = new Dictionary<string, int>(StringComparer.Ordinal);
                var generic = 0;
                foreach (Match symbol in Regex.Matches(symbols[number.Length..], @"红色|绿色|蓝色|黄色|橙色|紫色|A"))
                {
                    var trait = symbol.Value switch {
                        "红色" => "red", "绿色" => "green", "蓝色" => "blue",
                        "黄色" => "yellow", "橙色" => "orange", "紫色" => "purple", _ => "" };
                    if (trait.Length == 0) generic++;
                    else typed[trait] = typed.GetValueOrDefault(trait) + 1;
                }
                costs.Add(new(costs.Count == 0 ? EchoOptionalCostNames.Echo : $"ECHO:PRINTED:{costs.Count + 1}", mana, generic, typed));
            }
        }
        return costs;
    }

    // Deck effects need a resumable private selection; do not route placeholder
    // no-selection behavior through a paid repeat.
    public static bool SupportsRepeatResolution(CardBehaviorDefinition behavior)
        => behavior.DeckPlayKind.Length > 0 || CoreRuleEngine.SupportsSeparateExecution(behavior)
            || CoreRuleEngine.IsDeferredDeckChoice(behavior) || CoreRuleEngine.SupportsInsightRepeat(behavior);

    public static IReadOnlyList<Cost> PrintedCostsFor(string cardNo) => Printed.Value.GetValueOrDefault(cardNo) ?? [];
    public static Cost? PrintedFor(string cardNo) => PrintedCostsFor(cardNo).FirstOrDefault();
    public static bool IsEcho(string id) => id == "ECHO" || id.StartsWith("ECHO:", StringComparison.Ordinal);
    public static bool IsGrant(string effect, string player)
        => effect == GrantPrefix + player || effect.StartsWith(GrantPrefix + player + ":", StringComparison.Ordinal);

    public static IReadOnlyList<Cost> Available(MatchState state, string player, CardBehaviorDefinition behavior)
    {
        if (behavior.PlaysSourceToBaseAsUnit || behavior.PlaysSourceToBaseAsEquipment) return [];
        var costs = new List<Cost>();
        costs.AddRange(PrintedCostsFor(behavior.CardNo));
        var grants = state.UntilEndOfTurnEffects.Count(effect => IsGrant(effect, player));
        for (var index = 0; index < grants; index++)
            costs.Add(new(costs.Count == 0 ? "ECHO" : $"ECHO:GRANTED:{index + 1}",
                behavior.ManaCost, 0, new Dictionary<string, int>(), behavior.CardNo));
        // Syndra is 'in the showdown' only at that showdown's battlefield.
        // Being controlled by the active player, in base, or at another field is insufficient.
        if (state.SpellDuelState is { IsActive: true, BattlefieldObjectId: { } field })
        {
            foreach (var card in state.CardObjects.Values.OrderBy(card => card.ObjectId, StringComparer.Ordinal))
                if (card.ControllerId == player && !card.IsFaceDown
                    && SourceFamilies.Value["UNL-146/219"].Contains(card.CardNo, StringComparer.Ordinal)
                    && BattlefieldLocalRules.AtUnit(state, card.ObjectId)?.ObjectId == field)
                    costs.Add(new($"ECHO:SOURCE:{card.ObjectId}", 2, 0, new Dictionary<string, int> { ["purple"] = 1 }));
        }
        return costs;
    }

    public static IReadOnlyList<string> AddBaseCostGrant(IReadOnlyList<string> effects, string player)
    {
        var id = GrantPrefix + player;
        var index = 1;
        while (effects.Contains(id, StringComparer.Ordinal)) id = $"{GrantPrefix}{player}:{index++}";
        return effects.Append(id).ToArray();
    }

    public static IReadOnlyList<Cost> Selected(MatchState state, string player, CardBehaviorDefinition behavior,
        IReadOnlyList<string> choices)
        => Available(state, player, behavior).Where(cost => choices.Contains(cost.Id, StringComparer.Ordinal)).ToArray();

    public static int Reduction(MatchState state, string player)
        => BattlefieldLocalRules.ControlledBy(state, player).Sum(card =>
            BattlefieldStaticAbilitySpecRules.TryGetAbility(card.CardNo,
                BattlefieldStaticAbilitySpecRules.IsBattlefieldEchoCostReductionAbility, out var ability)
                ? Math.Max(0, ability.Amount) : 0);

    public static IReadOnlyList<string> PrintedCosts(IEnumerable<Cost> costs)
        => costs.Where(cost => cost.PrintedCardNo is not null).Select(cost => cost.PrintedCardNo!).ToArray();
}

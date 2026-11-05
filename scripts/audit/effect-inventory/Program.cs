using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;

if (args[0] == "--probes")
{
    var texts = JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(args[1]))!;
    var excluded = new HashSet<string> { "RuleTextParser", "KeywordParser", "CostParser", "TargetParser",
        "TriggerParser", "ReplacementParser", "ActivatedAbilityParser", "StaticAbilityParser", "StaticAuraParser", "EffectPhraseParser" };
    var methods = typeof(EmpowerParser).Assembly.GetTypes()
        .Where(t => t.Name.EndsWith("Parser") && !excluded.Contains(t.Name))
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
        .Where(m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string)
            && m.ReturnType != typeof(string) && m.ReturnType != typeof(void))
        .OrderBy(m => m.DeclaringType!.Name).ThenBy(m => m.Name).ToArray();
    var probes = new List<object>();
    foreach (var method in methods)
    {
        var baseline = JsonSerializer.Serialize(method.Invoke(null, [""]));
        foreach (var text in texts.Distinct(StringComparer.Ordinal))
        {
            var value = method.Invoke(null, [text]);
            if (JsonSerializer.Serialize(value) == baseline) continue;
            if (value is IReadOnlyList<EmpowerSpec> empower)
            {
                var supported = typeof(CardBehaviorRegistry).Assembly.GetType("Riftbound.Engine.EmpowerRules")!
                    .GetMethod("SupportedCost", BindingFlags.NonPublic | BindingFlags.Static)!;
                if (!empower.SelectMany(s => s.Alternatives).All(cost => (bool)supported.Invoke(null, [cost])!)) continue;
            }
            probes.Add(new { text, method = method.DeclaringType!.Name + "." + method.Name, value });
        }
    }
    await File.WriteAllTextAsync(args[2], JsonSerializer.Serialize(probes, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    Console.WriteLine($"Exported {probes.Count} clause-specific parser results");
    return;
}

// Export the actual loaded catalogs, not the broad per-card "Implemented" flag.
var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
var behaviors = CardBehaviorRegistry.GetAll();
var merged = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(catalog.Cards,
    behaviors.Select(b => new ImplementedCardBehavior(b.CardNo, b.EffectKind, b.DisplayName,
        CardBehaviorRegistry.TriggerEffectKinds(b))).ToArray());
var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, FunctionalUnitBuilder.Build(catalog.Cards), merged)
    .ToDictionary(s => s.CardNo);
var activations = P4ActivatedAbilityCatalog.GetAll();
var legendAbilities = typeof(LegendActionAbilityCatalog).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name.EndsWith("AbilityId"))
    .Select(f => (Id: (string)f.GetRawConstantValue()!, Sources: LegendActionAbilityCatalog.SourceCardNosForAbility(
        (string)f.GetRawConstantValue()!))).ToArray();
var predicates = typeof(CardBehaviorRegistry).Assembly.GetTypes()
    .Where(t => t.Name.EndsWith("SpecRules"))
    .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
    .Where(m => m.Name.StartsWith("Is") && m.ReturnType == typeof(bool)
        && m.GetParameters().Length == 1
        && new[] { typeof(TriggerSpec), typeof(StaticAuraSpec), typeof(StaticAbilitySpec), typeof(ReplacementSpec) }
            .Contains(m.GetParameters()[0].ParameterType))
    .OrderBy(m => m.DeclaringType!.Name).ThenBy(m => m.Name).ToArray();
string[] Predicates(object node) => predicates
    .Where(m => m.GetParameters()[0].ParameterType == node.GetType() && (bool)m.Invoke(null, [node])!)
    .Select(m => m.DeclaringType!.Name + "." + m.Name).ToArray();
CardBehaviorDefinition[] ActiveBehaviors(string cardNo)
{
    if (!CardBehaviorRegistry.TryGetByCardNo(cardNo, out var primary)) return [];
    return new[] { primary }.Concat(behaviors.Where(b => b.CardNo == cardNo && b.Mode.Length > 0)
        .Select(b => CardBehaviorRegistry.TryGetByCardNoAndMode(cardNo, b.Mode, out var selected) ? selected : null)
        .Where(b => b is not null).Cast<CardBehaviorDefinition>()).Distinct().ToArray();
}
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
};
var cards = catalog.Cards.OrderBy(c => c.CardNo, StringComparer.Ordinal).Select(card =>
{
    var spec = specs[card.CardNo];
    return new
    {
        card.Id, card.CardNo, card.CardName, card.SubTitle, card.CardCategory, card.CardCategoryName,
        card.CardEffect, card.Hero, card.Energy, card.Power, card.ReturnEnergy, card.Tag,
        card.CardColorList, spec.FunctionalUnitId,
        behaviors = ActiveBehaviors(card.CardNo),
        tokenTags = P6TokenFactoryCatalog.TryGetByCardNo(card.CardNo, out var factory)
            ? factory.CreateObject("audit-token", "audit-player", "audit-player").Tags.ToArray() : [],
        activations = activations.Where(a => P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(a, card.CardNo)).ToArray(),
        legendActivations = legendAbilities.Where(a => a.Sources.Contains(card.CardNo)).Select(a => a.Id).ToArray(),
        spec.Keywords, spec.ActivatedAbilities, spec.Effects,
        triggers = spec.Triggers.Select(node => new { node, predicates = Predicates(node) }).ToArray(),
        staticAbilities = spec.StaticAbilities.Select(node => new { node, predicates = Predicates(node) }).ToArray(),
        staticAuras = spec.StaticAuras.Select(node => new { node, predicates = Predicates(node) }).ToArray(),
        replacements = spec.Replacements.Select(node => new { node, predicates = Predicates(node) }).ToArray()
    };
}).ToArray();
var constants = new[] { typeof(BehaviorSpec).Assembly, typeof(CardBehaviorRegistry).Assembly }.SelectMany(a => a.GetTypes())
    .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => new { symbol = t.Name + "." + f.Name, value = (string)f.GetRawConstantValue()! })).ToArray();
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { schemaVersion = 1, catalog.Source, cards, constants }, options));
Console.WriteLine($"Exported {cards.Length} official prints to {output}");

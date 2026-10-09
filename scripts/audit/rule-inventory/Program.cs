using System.Text.Json;
using System.Text.Encodings.Web;
using Riftbound.CardCatalog;
using Riftbound.Engine;

// Inventory only: parser/registry presence never establishes complete gameplay support.
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
var catalog = await OfficialCardCatalog.LoadDefaultAsync();
var registry = CardBehaviorRegistry.GetAll();
var merged = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(catalog.Cards,
    registry.Select(b => new ImplementedCardBehavior(b.CardNo, b.EffectKind, b.DisplayName, CardBehaviorRegistry.TriggerEffectKinds(b))).ToArray());
var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, FunctionalUnitBuilder.Build(catalog.Cards), merged);
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
var cards = specs.Select(s => new
{
    s.CardNo, s.CardName, s.CardCategoryName, s.OfficialText,
    registryStatus = s.Status, registryReason = s.Reason,
    registeredPlayEffects = registry.Where(b => b.CardNo == s.CardNo).Select(b => b.EffectKind).ToArray(),
    triggerKinds = s.Triggers.Select(t => t.Kind).ToArray(),
    staticAbilityKinds = s.StaticAbilities.Select(a => a.Kind).ToArray(),
    staticAuraKinds = s.StaticAuras.Select(a => a.Kind).ToArray(),
    replacementKinds = s.Replacements.Select(a => a.Kind).ToArray(),
    fullGameplayVerification = "NOT_ESTABLISHED_BY_THIS_INVENTORY"
}).ToArray();
File.WriteAllText(Path.Combine(output, "active-card-rule-inventory.json"), JsonSerializer.Serialize(cards, options));
var root = Directory.GetCurrentDirectory();
var partialFixtures = Directory.EnumerateFiles(Path.Combine(root, "tests/Riftbound.ConformanceTests/Fixtures"), "*.json")
    .Where(p => { var t = File.ReadAllText(p); return t.Contains("暂缓") || t.Contains("deferred", StringComparison.OrdinalIgnoreCase); })
    .Select(p => Path.GetRelativePath(root, p)).Order().ToArray();
var upstream = JsonDocument.Parse(File.ReadAllText(args[1])).RootElement.GetProperty("cards").EnumerateArray();
var scopeVerified = new HashSet<string> { "OGN·297/298", "OGN·295/298", "SFD·216/221", "UNL-213/219", "OGN·296/298", "SFD·211/221", "SFD·213/221", "OGN·276/298", "OGN·276a/298" };
var battlefields = upstream.Where(c => c.GetProperty("cardCategoryList").EnumerateArray().Any(t => t.GetString() == "battlefield")).Select(c =>
{
    var number = c.GetProperty("cardNo").GetString()!;
    var spec = specs.FirstOrDefault(s => s.CardNo == number);
    return new { cardNo = number, cardName = c.GetProperty("cardName").GetString(), officialText = c.GetProperty("cardEffect").GetString(),
        inActiveCatalog = spec is not null,
        review = scopeVerified.Contains(number) ? "LOCAL_OR_CONTROL_SCOPE_RETESTED; NOT_ALL_RULE_COMBINATIONS"
            : spec is null ? "OUTSIDE_ACTIVE_CATALOG; NOT_VERIFIED_PLAYABLE" : "REQUIRES_INDEPENDENT_GAMEPLAY_REVIEW",
        registeredTriggers = spec?.Triggers.Select(t => t.Kind).ToArray() ?? [],
        registeredStaticAbilities = spec?.StaticAbilities.Select(a => a.Kind).ToArray() ?? [],
        registeredStaticAuras = spec?.StaticAuras.Select(a => a.Kind).ToArray() ?? [] };
}).ToArray();
File.WriteAllText(Path.Combine(output, "official-battlefield-review.json"), JsonSerializer.Serialize(battlefields, options));
var summary = new
{
    activeCatalogEntries = cards.Length, officialBattlefieldEntries = battlefields.Length,
    activeBattlefieldEntries = battlefields.Count(b => b.inActiveCatalog),
    registryStatusCounts = specs.GroupBy(s => s.Status).ToDictionary(g => g.Key, g => g.Count()),
    historicalPartialFixtureCount = partialFixtures.Length, historicalPartialFixtures = partialFixtures,
    warning = "This is a structural inventory and review queue, not a card coverage certificate. Historical fixture notes can be stale; each card needs official-text-to-runtime outcome review.",
    independentlyRetestedThisIteration = scopeVerified.Order().ToArray()
};
File.WriteAllText(Path.Combine(output, "inventory-summary.json"), JsonSerializer.Serialize(summary, options));
Console.WriteLine(JsonSerializer.Serialize(new { entries = cards.Length, battlefields = battlefields.Length, activeBattlefields = battlefields.Count(b => b.inActiveCatalog), partialFixtures = partialFixtures.Length }));

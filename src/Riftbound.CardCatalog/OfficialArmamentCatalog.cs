using System.Text.Json;
using System.Text.RegularExpressions;
namespace Riftbound.CardCatalog;

public sealed record OfficialArmamentDefinition(string CardNo, string SourceCardNo, int BaseMightBonus, string EffectText, string CardImageUrl)
{
    public bool CopiesHostPrintedRulesText { get; init; }
    public bool CopiesChosenFriendlyUnit { get; init; }
    // While attached: destroy this equipment instead, then heal, exhaust and recall the host.
    public bool ReplacesHostDestruction { get; init; }
    public IReadOnlyList<string>? GrantedKeywords { get; init; }
    public int SpellSkillDamageBonus { get; init; }
    public IReadOnlyList<string>? GrantedTraits { get; init; }
    public IReadOnlyList<string>? FriendlyUnitsHereKeywords { get; init; }
}

// The official gallery stores the lower armament box separately from rules text.
public static class OfficialArmamentCatalog
{
    private sealed record Document(IReadOnlyList<OfficialArmamentDefinition> Records);
    private static readonly Lazy<IReadOnlyDictionary<string, OfficialArmamentDefinition>> Definitions = new(() =>
    {
        var path = Path.Combine(Path.GetDirectoryName(OfficialCardCatalog.DefaultCatalogPath())!, "armaments.json");
        var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("Official armament source data is missing.");
        return document.Records.Select(record => record with {
            GrantedKeywords = record.GrantedKeywords ?? StandaloneKeywords(record.EffectText),
            GrantedTraits = record.GrantedTraits ?? HostTraits(record.EffectText),
            FriendlyUnitsHereKeywords = record.FriendlyUnitsHereKeywords ?? FriendlyHereKeywords(record.EffectText),
            SpellSkillDamageBonus = SpellSkillBonus(record.EffectText)
        }).ToDictionary(record => Normalize(record.CardNo), StringComparer.Ordinal);
    });
    // Only a complete keyword line is unconditional. Keywords mentioned by
    // triggers, level clauses or reminder text are not host keyword grants.
    private static IReadOnlyList<string> StandaloneKeywords(string text)
        => Regex.Matches(text, @"(?:^|\r?\n)\{\{(?<keyword>[^{}]+)\}\}(?:（[^）]*）)?[。]?(?=\r?$|\n)")
            .SelectMany(match => KeywordParser.Parse("{{" + match.Groups["keyword"].Value + "}}", "")
                .Where(keyword => keyword.RawText == match.Groups["keyword"].Value))
            .Select(keyword => keyword.RawText).Distinct(StringComparer.Ordinal).ToArray();

    // Only complete unconditional lower-box lines grant these characteristics.
    private static IReadOnlyList<string> HostTraits(string text)
        => Regex.Matches(text, @"(?:^|\r?\n)我拥有[“""](?<trait>[^“”""\r\n]+)[”""]属性[。]?(?=\r?$|\n)")
            .Select(match => match.Groups["trait"].Value).Distinct(StringComparer.Ordinal).ToArray();

    private static IReadOnlyList<string> FriendlyHereKeywords(string text)
        => Regex.Matches(text, @"(?:^|\r?\n)你在此处的单位获得\{\{(?<keyword>[^{}]+)\}\}[。]?(?:（[^）]*）)?[。]?(?=\r?$|\n)")
            .SelectMany(match => KeywordParser.Parse("{{" + match.Groups["keyword"].Value + "}}", "")
                .Where(keyword => keyword.RawText == match.Groups["keyword"].Value))
            .Select(keyword => keyword.RawText).Distinct(StringComparer.Ordinal).ToArray();

    private static int SpellSkillBonus(string text)
    {
        var match = Regex.Match(text, @"^你的法术和技能造成的伤害\+(?<amount>[1-9][0-9]*)（如果已贴附此牌）[。]?$" );
        return match.Success && int.TryParse(match.Groups["amount"].Value, out var amount) ? amount : 0;
    }

    public static IReadOnlyList<OfficialArmamentDefinition> All => Definitions.Value.Values.OrderBy(r => r.CardNo, StringComparer.Ordinal).ToArray();
    public static OfficialArmamentDefinition? Find(string? cardNo) => Definitions.Value.GetValueOrDefault(Normalize(cardNo));
    private static string Normalize(string? cardNo) => (cardNo ?? "").Trim().Replace('·', '-').ToUpperInvariant();
}

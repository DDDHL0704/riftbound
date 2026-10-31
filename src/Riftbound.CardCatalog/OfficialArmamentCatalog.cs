using System.Text.Json;
namespace Riftbound.CardCatalog;

public sealed record OfficialArmamentDefinition(string CardNo, string SourceCardNo, int BaseMightBonus, string EffectText, string CardImageUrl)
{
    public bool CopiesHostPrintedRulesText { get; init; }
    public bool CopiesChosenFriendlyUnit { get; init; }
    // While attached: destroy this equipment instead, then heal, exhaust and recall the host.
    public bool ReplacesHostDestruction { get; init; }
    public IReadOnlyList<string>? GrantedKeywords { get; init; }
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
        return document.Records.ToDictionary(record => Normalize(record.CardNo), StringComparer.Ordinal);
    });
    public static IReadOnlyList<OfficialArmamentDefinition> All => Definitions.Value.Values.OrderBy(r => r.CardNo, StringComparer.Ordinal).ToArray();
    public static OfficialArmamentDefinition? Find(string? cardNo) => Definitions.Value.GetValueOrDefault(Normalize(cardNo));
    private static string Normalize(string? cardNo) => (cardNo ?? "").Trim().Replace('·', '-').ToUpperInvariant();
}

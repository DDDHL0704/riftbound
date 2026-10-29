using System.Collections.Immutable;

namespace Riftbound.CardCatalog;

// Admit only implemented printings from the official snapshot, not the whole set.
public static class VendettaCardSources
{
    public const string SnapshotPath = "data/official/upstream/2026-10-16/source-refresh-20261015T160523Z/card-catalog.zh-CN.json";
    private const string ResourceName = "Riftbound.CardCatalog.VendettaSourceSnapshot";
    private static readonly Lazy<ImmutableArray<OfficialCard>> Cards = new(Load);

    public static IReadOnlyList<OfficialCard> GetAdmittedCards() => Cards.Value;

    private static ImmutableArray<OfficialCard> Load()
    {
        using var stream = typeof(VendettaCardSources).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The official VEN snapshot is missing.");
        var catalog = OfficialCardCatalogLoader.LoadFromStreamAsync(stream, SnapshotPath).GetAwaiter().GetResult();
        return new[] { (Id: 37805L, CardNo: "VEN·108"), (Id: 37709L, CardNo: "VEN·022"), (Id: 37759L, CardNo: "VEN·067"), (Id: 37983L, CardNo: "VEN·044"), (Id: 37791L, CardNo: "VEN·094"), (Id: 37799L, CardNo: "VEN·102"), (Id: 37787L, CardNo: "VEN·091"), (Id: 37694L, CardNo: "VEN·009"), (Id: 37773L, CardNo: "VEN·079"), (Id: 37774L, CardNo: "VEN·080"), (Id: 37706L, CardNo: "VEN·020"), (Id: 37712L, CardNo: "VEN·024"), (Id: 37716L, CardNo: "VEN·028"), (Id: 37765L, CardNo: "VEN·071"), (Id: 37821L, CardNo: "VEN·121"), (Id: 37704L, CardNo: "VEN·019"), (Id: 37705L, CardNo: "VEN·019a"),

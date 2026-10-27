using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Riftbound.CardCatalog;

public sealed record DisarmFriendlyGrantSource(string RequiredUnitTag, int Value);

public sealed record DisarmSourceSpecification(
    long SourceId, string CardNo, string CardName, string EffectiveText,
    int PrintedValue, ImmutableArray<string> UnitTags,
    ImmutableArray<DisarmFriendlyGrantSource> FriendlyGrants);

// Source-only keyword facts. These prints are not runtime catalog entries or
// executable behavior registrations; timing, grants and history belong to the engine.
public static class DisarmSourceSpecifications
{
    public const string SnapshotPath =
        "data/official/upstream/2026-10-28/radiance-disarm/card-catalog.zh-CN.json";
    private const string ResourceName = "Riftbound.CardCatalog.DisarmSourceSnapshot";
    private static readonly Regex PrintedKeyword = new(
        @"(?:^|\n)\s*\{\{缴械(?<value>[0-9]*)\}\}", RegexOptions.CultureInvariant);
    private static readonly Regex FriendlyGrant = new(
        @"(?:^|\n)\s*你的“(?<tag>[^”]+)”具有\{\{缴械(?<value>[0-9]*)\}\}", RegexOptions.CultureInvariant);
    private static readonly byte[] Snapshot = ReadSnapshot();
    private static readonly FrozenDictionary<string, DisarmSourceSpecification> ByCardNo = Load();

    public static string SnapshotSha256 { get; } =
        Convert.ToHexString(SHA256.HashData(Snapshot)).ToLowerInvariant();

    public static bool TryGet(string cardNo, out DisarmSourceSpecification specification)
        => ByCardNo.TryGetValue(cardNo, out specification!);

    public static IReadOnlyList<DisarmSourceSpecification> GetAll()
        => ByCardNo.Values.OrderBy(spec => spec.CardNo, StringComparer.Ordinal).ToArray();

    private static byte[] ReadSnapshot()
    {
        using var stream = typeof(DisarmSourceSpecifications).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The retained Disarm source snapshot is missing.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    private static FrozenDictionary<string, DisarmSourceSpecification> Load()
    {
        using var document = JsonDocument.Parse(Snapshot);
        return document.RootElement.GetProperty("cards").EnumerateArray()
            .Select(card =>
            {
                var printed = card.GetProperty("cardEffect").GetString()!;
                var text = card.TryGetProperty("errata", out var errata)
                    && !string.IsNullOrWhiteSpace(errata.GetString()) ? errata.GetString()! : printed;
                var value = PrintedKeyword.Matches(text).Sum(match => Amount(match.Groups["value"].Value));
                var grants = FriendlyGrant.Matches(text)
                    .Select(match => new DisarmFriendlyGrantSource(match.Groups["tag"].Value,
                        Amount(match.Groups["value"].Value))).ToImmutableArray();
                return new DisarmSourceSpecification(card.GetProperty("id").GetInt64(),
                    card.GetProperty("cardNo").GetString()!, card.GetProperty("cardName").GetString()!, text,
                    value, (card.GetProperty("tag").GetString() ?? "")
                        .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToImmutableArray(), grants);
            }).ToFrozenDictionary(spec => spec.CardNo, StringComparer.Ordinal);
    }

    private static int Amount(string text)
        => text.Length == 0 ? 1 : int.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
}

using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Riftbound.CardCatalog;

public sealed record DisarmFriendlyGrantSource(string RequiredUnitTag, int Value);

public sealed record DisarmSourceSpecification(
    long SourceId, string CardNo, string CardName, string EffectiveText,
    int PrintedValue, ImmutableArray<string> UnitTags,
    ImmutableArray<DisarmFriendlyGrantSource> FriendlyGrants);

// Exact keyword facts do not themselves admit complete cards or register play
// behavior. Timing, cumulative grants and captured history belong to the engine.
public static class DisarmSourceSpecifications
{
    public const string SnapshotPath = ScopedOfficialCardSources.SnapshotPath;
    private static readonly Regex PrintedKeyword = new(
        @"(?:^|\n)\s*\{\{缴械(?<value>[0-9]*)\}\}", RegexOptions.CultureInvariant);
    private static readonly Regex FriendlyGrant = new(
        @"(?:^|\n)\s*你的“(?<tag>[^”]+)”具有\{\{缴械(?<value>[0-9]*)\}\}", RegexOptions.CultureInvariant);
    private static readonly FrozenDictionary<string, DisarmSourceSpecification> ByCardNo = Load();

    public static string SnapshotSha256 => ScopedOfficialCardSources.SnapshotSha256;

    public static bool TryGet(string cardNo, out DisarmSourceSpecification specification)
        => ByCardNo.TryGetValue(cardNo, out specification!);

    public static IReadOnlyList<DisarmSourceSpecification> GetAll()
        => ByCardNo.Values.OrderBy(spec => spec.CardNo, StringComparer.Ordinal).ToArray();

    private static FrozenDictionary<string, DisarmSourceSpecification> Load()
        => ScopedOfficialCardSources.GetAll().Select(card =>
        {
            var text = card.CardEffect;
            var value = PrintedKeyword.Matches(text).Sum(match => Amount(match.Groups["value"].Value));
            var grants = FriendlyGrant.Matches(text)
                .Select(match => new DisarmFriendlyGrantSource(match.Groups["tag"].Value,
                    Amount(match.Groups["value"].Value))).ToImmutableArray();
            return new DisarmSourceSpecification(card.Id, card.CardNo, card.CardName, text,
                value, card.Tag.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToImmutableArray(), grants);
        }).ToFrozenDictionary(spec => spec.CardNo, StringComparer.Ordinal);

    private static int Amount(string text)
        => text.Length == 0 ? 1 : int.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
}

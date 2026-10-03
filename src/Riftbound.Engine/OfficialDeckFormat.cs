using Riftbound.CardCatalog;

namespace Riftbound.Engine;

public enum OfficialDeckFormat
{
    // Historical mechanics fixtures are independent of tournament legality.
    CoreRules,
    ChinaStandard20260724
}

public static class ChinaStandardBanList
{
    public const string Version = "CN-STANDARD-2026-07-24";
    public const string SourceUrl = "https://cdn.playloltcg.com/lol/2026/07/2026-07-17/d91ee4548d224dfb9c7a3f5390cf0573.pdf";
    public const string SourceSha256 = "e0326381f003a3e5d0a12db9521dc15eb1cc4ed0456188cee9fc46e0eb6d5ef7";

    private static readonly HashSet<string> BannedNames = new(StringComparer.Ordinal)
    {
        "预判攻势", "战或逃", "废料堆", "劫掠船巷", "幻梦之树", "力量方尖碑",
        "隐秘追踪者", "荣耀竞技场", "攀圣长阶"
    };

    public static bool IsBanned(OfficialCard card) =>
        BannedNames.Contains(card.CardName.Trim())
        || (card.CardName.Trim() == "德莱文" && card.SubTitle.Trim() == "血斧飞旋");

    public static bool IsAllowed(OfficialCard card, OfficialDeckFormat format) => format switch
    {
        OfficialDeckFormat.CoreRules => true,
        OfficialDeckFormat.ChinaStandard20260724 => !IsBanned(card),
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };
}

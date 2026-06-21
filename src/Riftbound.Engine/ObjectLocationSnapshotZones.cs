using System;
using System.Linq;

namespace Riftbound.Engine;

internal static class ObjectLocationSnapshotZones
{
    public static (string ZoneKind, string ZoneLabel) Describe(
        ObjectLocationState location,
        CardObjectState? cardObject,
        bool includeObjectClassHints)
    {
        var hasRuneHint = includeObjectClassHints
            && cardObject is not null
            && cardObject.Tags.Contains(CardObjectTags.RuneCard, StringComparer.Ordinal);
        var hasBattlefieldHint = includeObjectClassHints
            && cardObject is not null
            && cardObject.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal);

        return location.Zone switch
        {
            "BANISHED" => ("banished", "放逐区"),
            "BASE" when hasRuneHint => ("rune", "已抽出符文"),
            "BASE" => ("base", "基地"),
            "BATTLEFIELD" when hasBattlefieldHint => ("battlefield-site", "战场牌"),
            "BATTLEFIELD" => ("battlefield", "战场"),
            "CHAMPION" => ("champion", "英雄区"),
            "GRAVEYARD" => ("graveyard", "已打出牌堆"),
            "HAND" => ("hand", "手牌"),
            "LEGEND" => ("legend", "传奇区"),
            "MAIN_DECK" => ("main-deck", "主牌库"),
            "RUNE_DECK" => ("rune-deck", "符文牌堆"),
            "STACK" => ("stack", "结算链"),
            _ => ("unknown", "服务端区域")
        };
    }

    public static bool IsKnownZoneKind(string zoneKind)
    {
        return zoneKind switch
        {
            "main-deck"
                or "rune-deck"
                or "hand"
                or "base"
                or "rune"
                or "battlefield"
                or "battlefield-site"
                or "graveyard"
                or "banished"
                or "legend"
                or "champion"
                or "stack"
                or "unknown" => true,
            _ => false
        };
    }
}

namespace Riftbound.CardCatalog;

// Reviewed CN scope as of 2026-10-14. Keep the acquired snapshot unchanged;
// the runtime catalog and its identity use the applicable official wording.
internal static class ReviewedCardErrata
{
    // UNL errata version 260403, DOC-10-P003-B005/B006/B007.
    // https://cdn.playloltcg.com/lol/2026/04/2026-04-15/破限系列_勘误汇总_260403.pdf
    // SHA-256: 1dbb6a746d334390617e121cc0281a0bf606d7729e0a5dcf60f9ddcff14f775f
    internal static string EffectiveText(long id, string cardNo, string printedText) => id == 34615 && cardNo == "UNL-081/219"
        ? "{{待命}}（支付{{A}}正面朝下放置此牌，之后可支付{{0}}将其当作反应牌打出。）\n"
            + "{{瞬息}}（在我控制者的下个开始阶段开始时，结算得分之前将我摧毁。）\n"
            + "当你打出我时，在此处打出两名“映像”。然后进行一次：它们变为我的复制体。"
        // CN DOC-12 p3, SFD errata 260114; current API errata agrees.
        // SHA256 13ef6ec857b2b7d693d0e83f388c37f02a87bfb08886676e70e6bd69f8e7935d.
        : id == 33075 && cardNo == "SFD·003/221"
            ? "{{迅捷}}（可在你的回合或法术对决中打出。）\n{{回响1}}（你可以选择支付此额外费用，以重复此法术效果。）\n让一名单位本回合内获得{{强攻2}}。（如果它是进攻方，则{{S}}+2。）"
        : (id == 31473 && cardNo == "OGN·235/298" || id == 33346 && cardNo == "SFD·237/221"
            || id == 33347 && cardNo == "SFD·237*/221")
            // Current CN DOC-16-P005-B003, SHA256 1e8fc5c52f1c98a4b7d759e4c048f11cfae882ce87826bf87a86bc475c26036c.
            ? printedText.Replace("每当你回收任意数量的卡牌时", "每当你将任意数量的卡牌回收到自己的主牌堆时", StringComparison.Ordinal)
            : printedText;
}

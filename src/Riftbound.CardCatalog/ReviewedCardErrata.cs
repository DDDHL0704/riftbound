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
        : printedText;
}

import { AppRoute } from "../app/router";
import { Button } from "../components/ui/Button";
import { buildStarterDeck } from "../services/starterDeck";

export function DecksPage({ onNavigate }: { onNavigate: (route: AppRoute) => void }) {
  const deck = buildStarterDeck();
  return (
    <div className="page-grid">
      <section className="page-header">
        <div>
          <span className="eyebrow">卡组管理</span>
          <h1>本地测试卡组</h1>
          <p>服务端尚未提供卡组保存 REST。本页先提供正式 `SUBMIT_DECK` 所需的本地合法测试牌组，后续发现缺口会补服务端能力。</p>
        </div>
        <Button onClick={() => onNavigate({ name: "lobby" })}>用此卡组进入大厅</Button>
      </section>
      <section className="deck-summary">
        <article><strong>传奇</strong><span>{deck.legendCardNo}</span></article>
        <article><strong>英雄</strong><span>{deck.championCardNo}</span></article>
        <article><strong>主牌堆</strong><span>{deck.mainDeck.length} 张</span></article>
        <article><strong>符文牌堆</strong><span>{deck.runeDeck.length} 张</span></article>
        <article><strong>战场池</strong><span>{deck.battlefields.join("、")}</span></article>
      </section>
    </div>
  );
}

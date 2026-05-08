import { Search } from "lucide-react";
import { useMemo, useState } from "react";
import { AppRoute } from "../app/router";
import { CardDetailDrawer } from "../components/cards/CardDetailDrawer";
import { InspectedCard } from "../components/cards/CardFace";
import { Button } from "../components/ui/Button";
import { StatusPill } from "../components/ui/StatusPill";
import { buildStarterDeck } from "../services/starterDeck";
import { useCatalog } from "../stores/catalogStore";
import { conformanceLabel, statusLabel } from "../utils/formatters";

export function DecksPage({ onNavigate }: { onNavigate: (route: AppRoute) => void }) {
  const { specByNo } = useCatalog();
  const deck = buildStarterDeck();
  const [query, setQuery] = useState("");
  const [inspectedCard, setInspectedCard] = useState<InspectedCard | undefined>();
  const sections = useMemo(
    () => [
      { title: "传奇", entries: countCards([deck.legendCardNo]) },
      { title: "英雄", entries: countCards([deck.championCardNo]) },
      { title: "主牌堆", entries: countCards(deck.mainDeck) },
      { title: "符文牌堆", entries: countCards(deck.runeDeck) },
      { title: "战场池", entries: countCards(deck.battlefields) }
    ],
    [deck]
  );
  const normalizedQuery = query.trim().toLowerCase();

  return (
    <div className="page-grid">
      <section className="page-header">
        <div>
          <span className="eyebrow">卡组管理</span>
          <h1>本地测试卡组</h1>
          <p>服务端尚未提供卡组保存接口。本页只展示提交卡组会使用的本地测试牌组；正式合法性以服务端提交结果为准。</p>
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
      <section className="filter-bar">
        <label>
          <span><Search size={16} /> 搜索卡组</span>
          <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="卡名、编号、类型" />
        </label>
        <div className="deck-legality-note">
          <StatusPill tone="warn">等待服务端验证</StatusPill>
          <span>前端不本地判定卡组是否合法，只展示待提交内容。</span>
        </div>
      </section>
      <section className="deck-list">
        {sections.map((section) => {
          const entries = section.entries.filter((entry) => matchesDeckQuery(entry.cardNo, specByNo[entry.cardNo], normalizedQuery));
          return (
            <article className="deck-section" key={section.title}>
              <header>
                <div>
                  <span className="eyebrow">{section.title}</span>
                  <h2>{entries.reduce((total, entry) => total + entry.count, 0)} 张</h2>
                </div>
              </header>
              <div className="deck-card-list">
                {entries.length === 0 && <span className="empty-hint">没有匹配卡牌。</span>}
                {entries.map((entry) => {
                  const spec = specByNo[entry.cardNo];
                  return (
                    <button
                      className="deck-card-row"
                      key={`${section.title}-${entry.cardNo}`}
                      onClick={() => setInspectedCard({ spec })}
                      type="button"
                    >
                      <strong>{spec?.cardName ?? "服务端图鉴未提供"}</strong>
                      <span>{entry.cardNo}</span>
                      <span>{entry.count} 张</span>
                      <span>{spec?.cardCategoryName ?? "未知类型"}</span>
                      <span>{spec ? conformanceLabel(spec.conformanceTier) : "缺少服务端证据"}</span>
                      <span>{spec ? statusLabel(spec.status) : "未知"}</span>
                    </button>
                  );
                })}
              </div>
            </article>
          );
        })}
      </section>
      <CardDetailDrawer card={inspectedCard} onClose={() => setInspectedCard(undefined)} />
    </div>
  );
}

type DeckEntry = {
  cardNo: string;
  count: number;
};

function countCards(cardNos: string[]): DeckEntry[] {
  const counts = new Map<string, number>();
  for (const cardNo of cardNos) {
    counts.set(cardNo, (counts.get(cardNo) ?? 0) + 1);
  }

  return Array.from(counts, ([cardNo, count]) => ({ cardNo, count }));
}

function matchesDeckQuery(cardNo: string, spec: { cardName?: string; cardCategoryName?: string } | undefined, query: string): boolean {
  if (!query) {
    return true;
  }

  return `${cardNo} ${spec?.cardName ?? ""} ${spec?.cardCategoryName ?? ""}`.toLowerCase().includes(query);
}

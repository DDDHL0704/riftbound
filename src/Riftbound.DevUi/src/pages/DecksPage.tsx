import { ArrowRight, CheckCircle2, ClipboardPaste, FileText, RotateCcw, Search, XCircle } from "lucide-react";
import { useMemo, useState } from "react";
import type { CSSProperties } from "react";
import { AppRoute } from "../app/router";
import { CardDetailDrawer } from "../components/cards/CardDetailDrawer";
import { InspectedCard } from "../components/cards/CardFace";
import { Button } from "../components/ui/Button";
import { StatusPill } from "../components/ui/StatusPill";
import { buildStarterDeck } from "../services/starterDeck";
import { useCatalog } from "../stores/catalogStore";
import { SubmitDeckCommand } from "../types/protocol";
import { conformanceLabel, statusLabel } from "../utils/formatters";
import {
  countDeckCards,
  deckToImportText,
  defaultStarterDeck,
  parseDeckImport,
  serializeStarterDeck,
  starterDeckOverrideKey,
  starterDeckOverrideQueryKey,
  summarizeStarterDeck
} from "../utils/starterDeck";

export function DecksPage({ onNavigate }: { onNavigate: (route: AppRoute) => void }) {
  const { specByNo } = useCatalog();
  const [deck, setDeck] = useState(() => buildStarterDeck());
  const [importText, setImportText] = useState(() => deckToImportText(buildStarterDeck()));
  const [importMessage, setImportMessage] = useState("当前摘要来自本地 starter 或已缓存的导入构筑。");
  const [query, setQuery] = useState("");
  const [inspectedCard, setInspectedCard] = useState<InspectedCard | undefined>();
  const importPreview = useMemo(() => (importText.trim() ? parseDeckImport(importText) : undefined), [importText]);
  const validImportPreview = importPreview?.ok ? importPreview : undefined;
  const importIssues = importPreview && !importPreview.ok ? importPreview.issues : [];
  const previewSummary = useMemo(
    () => (validImportPreview ? summarizeStarterDeck(validImportPreview.deck) : undefined),
    [validImportPreview]
  );
  const currentSummary = useMemo(() => summarizeStarterDeck(deck), [deck]);
  const currentCommandPreview = useMemo(() => JSON.stringify(deck, null, 2), [deck]);
  const sections = useMemo(
    () => [
      { title: "传奇", entries: countDeckCards([deck.legendCardNo]) },
      { title: "英雄", entries: countDeckCards([deck.championCardNo]) },
      { title: "主牌堆", entries: countDeckCards(deck.mainDeck) },
      { title: "符文牌堆", entries: countDeckCards(deck.runeDeck) },
      { title: "战场池", entries: countDeckCards(deck.battlefields) }
    ],
    [deck]
  );
  const normalizedQuery = query.trim().toLowerCase();
  const importTone = importPreview ? (importPreview.ok ? "good" : "bad") : "neutral";
  const importStatusLabel = importPreview ? (importPreview.ok ? "结构可导入" : "结构无效") : "等待粘贴";

  function applyImport() {
    const result = parseDeckImport(importText);
    if (!result.ok) {
      setImportMessage(`导入未应用：${result.issues[0]?.message ?? "请检查格式。"}`);
      return;
    }

    setDeck(result.deck);
    persistStarterDeckOverride(result.deck);
    setImportText(deckToImportText(result.deck));
    const summary = summarizeStarterDeck(result.deck);
    setImportMessage(`已更新当前待提交构筑：主牌堆 ${summary.mainDeck} 张，符文 ${summary.runeDeck} 张，战场 ${summary.battlefields} 张。`);
  }

  function resetToDefaultDeck() {
    const nextDeck = defaultStarterDeck();
    clearStarterDeckOverride();
    setDeck(nextDeck);
    setImportText(deckToImportText(nextDeck));
    setImportMessage("已恢复本地默认 starter 构筑。");
  }

  function loadCurrentDeckIntoEditor() {
    setImportText(deckToImportText(deck));
    setImportMessage("已将当前待提交构筑载入粘贴区。");
  }

  return (
    <div className="page-grid" style={deckWireStyles.page}>
      <section style={deckWireStyles.header}>
        <div>
          <span style={deckWireStyles.eyebrow}>DECK INTAKE</span>
          <h1 style={deckWireStyles.h1}>构筑导入工作台</h1>
          <p style={deckWireStyles.bodyCopy}>先把粘贴内容整理成 SUBMIT_DECK 命令，再交给服务端做权威合法性判断。</p>
        </div>
        <Button icon={<ArrowRight size={16} />} onClick={() => onNavigate({ name: "lobby" })} style={deckWireStyles.primaryButton}>
          用当前构筑进入大厅
        </Button>
      </section>
      <section aria-label="导入流程" style={deckWireStyles.flow}>
        <article style={deckWireStyles.flowStep}>
          <strong>01</strong>
          <span>粘贴 JSON 或分区文本</span>
        </article>
        <article style={deckWireStyles.flowStep}>
          <strong>02</strong>
          <span>前端只做结构反馈</span>
        </article>
        <article style={deckWireStyles.flowStep}>
          <strong>03</strong>
          <span>提交时以服务端为准</span>
        </article>
      </section>
      <section style={deckWireStyles.importShell}>
        <div style={deckWireStyles.importEditor}>
          <header style={deckWireStyles.panelHeader}>
            <div>
              <span style={deckWireStyles.eyebrow}>PASTE</span>
              <h2 style={deckWireStyles.h2}>导入入口</h2>
            </div>
            <StatusPill tone={importTone}>{importStatusLabel}</StatusPill>
          </header>
          <textarea
            aria-label="粘贴构筑"
            onChange={(event) => setImportText(event.target.value)}
            placeholder={[
              "legend: UNL-181/219",
              "champion: UNL-022/219",
              "main:",
              "3 ARC-001/006",
              "runes:",
              "1 OGN·007/298",
              "battlefields:",
              "1 OGN·275/298"
            ].join("\n")}
            spellCheck={false}
            style={deckWireStyles.textarea}
            value={importText}
          />
          <div style={deckWireStyles.actionRow}>
            <Button disabled={!validImportPreview} icon={<ClipboardPaste size={16} />} onClick={applyImport} style={deckWireStyles.primaryButton}>
              导入为当前构筑
            </Button>
            <Button icon={<FileText size={16} />} onClick={loadCurrentDeckIntoEditor} style={deckWireStyles.secondaryButton} variant="secondary">
              载入当前
            </Button>
            <Button icon={<RotateCcw size={16} />} onClick={resetToDefaultDeck} style={deckWireStyles.secondaryButton} variant="ghost">
              恢复默认
            </Button>
          </div>
        </div>
        <aside style={deckWireStyles.feedbackPanel}>
          <header style={deckWireStyles.panelHeader}>
            <div>
              <span style={deckWireStyles.eyebrow}>FEEDBACK</span>
              <h2 style={deckWireStyles.h2}>导入反馈</h2>
            </div>
            {validImportPreview ? <CheckCircle2 size={24} /> : <XCircle size={24} />}
          </header>
          <p style={deckWireStyles.bodyCopy}>{importMessage}</p>
          {previewSummary ? (
            <div style={deckWireStyles.previewGrid}>
              <span style={deckWireStyles.previewCell}>主牌堆 {previewSummary.mainDeck} 张 / {previewSummary.distinctMainDeck} 种</span>
              <span style={deckWireStyles.previewCell}>符文 {previewSummary.runeDeck} 张 / {previewSummary.distinctRuneDeck} 种</span>
              <span style={deckWireStyles.previewCell}>战场 {previewSummary.battlefields} 张 / {previewSummary.distinctBattlefields} 种</span>
              <span style={deckWireStyles.previewCell}>格式 {validImportPreview?.format.toUpperCase() ?? "TEXT"}</span>
            </div>
          ) : (
            <ul style={deckWireStyles.issueList}>
              {importIssues.length === 0 && <li>粘贴后会在这里显示结构校验结果。</li>}
              {importIssues.map((issue, index) => (
                <li key={`${issue.field}-${index}`}>{issue.message}</li>
              ))}
            </ul>
          )}
          <div style={deckWireStyles.authorityNote}>
            <StatusPill tone="warn">服务端权威</StatusPill>
            <span>这里不判定卡牌数量、同名上限、颜色或规则合法性。</span>
          </div>
        </aside>
      </section>
      <section aria-label="当前将提交到服务端的构筑摘要" className="deck-summary" style={deckWireStyles.summaryGrid}>
        <article style={deckWireStyles.summaryBox}><strong>传奇</strong><span style={deckWireStyles.summaryText}>{currentSummary.legendCardNo}</span></article>
        <article style={deckWireStyles.summaryBox}><strong>英雄</strong><span style={deckWireStyles.summaryText}>{currentSummary.championCardNo}</span></article>
        <article style={deckWireStyles.summaryBox}><strong>主牌堆</strong><span style={deckWireStyles.summaryText}>{currentSummary.mainDeck} 张 / {currentSummary.distinctMainDeck} 种</span></article>
        <article style={deckWireStyles.summaryBox}><strong>符文牌堆</strong><span style={deckWireStyles.summaryText}>{currentSummary.runeDeck} 张 / {currentSummary.distinctRuneDeck} 种</span></article>
        <article style={deckWireStyles.summaryBox}><strong>战场池</strong><span style={deckWireStyles.summaryText}>{deck.battlefields.join("、")}</span></article>
      </section>
      <section style={deckWireStyles.commandPreview}>
        <div>
          <span style={deckWireStyles.eyebrow}>SERVER COMMAND</span>
          <h2 style={deckWireStyles.h2}>当前将提交的 deck 命令</h2>
        </div>
        <pre aria-label="当前将提交的构筑 JSON" style={deckWireStyles.commandCode} tabIndex={0}>{currentCommandPreview}</pre>
      </section>
      <section className="filter-bar" style={deckWireStyles.filterBar}>
        <label>
          <span style={deckWireStyles.filterLabel}><Search size={16} /> 搜索卡组</span>
          <input
            onChange={(event) => setQuery(event.target.value)}
            placeholder="卡名、编号、类型"
            style={deckWireStyles.input}
            value={query}
          />
        </label>
        <div className="deck-legality-note" style={deckWireStyles.legalityNote}>
          <StatusPill tone="warn">等待服务端验证</StatusPill>
          <span>前端不本地判定卡组是否合法，只展示待提交内容。</span>
        </div>
      </section>
      <section className="deck-list">
        {sections.map((section) => {
          const entries = section.entries.filter((entry) => matchesDeckQuery(entry.cardNo, specByNo[entry.cardNo], normalizedQuery));
          return (
            <article className="deck-section" key={section.title} style={deckWireStyles.deckSection}>
              <header>
                <div>
                  <span style={deckWireStyles.eyebrow}>{section.title}</span>
                  <h2 style={deckWireStyles.h2}>{entries.reduce((total, entry) => total + entry.count, 0)} 张</h2>
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
                      style={deckWireStyles.deckRow}
                      type="button"
                    >
                      <strong>{spec?.cardName ?? "服务端图鉴未提供"}</strong>
                      <span style={deckWireStyles.deckRowMeta}>{entry.cardNo}</span>
                      <span style={deckWireStyles.deckRowMeta}>{entry.count} 张</span>
                      <span style={deckWireStyles.deckRowMeta}>{spec?.cardCategoryName ?? "未知类型"}</span>
                      <span style={deckWireStyles.deckRowMeta}>{spec ? conformanceLabel(spec.conformanceTier) : "缺少服务端证据"}</span>
                      <span style={deckWireStyles.deckRowMeta}>{spec ? statusLabel(spec.status) : "未知"}</span>
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

function matchesDeckQuery(cardNo: string, spec: { cardName?: string; cardCategoryName?: string } | undefined, query: string): boolean {
  if (!query) {
    return true;
  }

  return `${cardNo} ${spec?.cardName ?? ""} ${spec?.cardCategoryName ?? ""}`.toLowerCase().includes(query);
}

function persistStarterDeckOverride(deck: SubmitDeckCommand): void {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.setItem(starterDeckOverrideKey, serializeStarterDeck(deck));
  removeStarterDeckOverrideQuery();
}

function clearStarterDeckOverride(): void {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.removeItem(starterDeckOverrideKey);
  removeStarterDeckOverrideQuery();
}

function removeStarterDeckOverrideQuery(): void {
  if (typeof window === "undefined") {
    return;
  }

  const url = new URL(window.location.href);
  if (!url.searchParams.has(starterDeckOverrideQueryKey)) {
    return;
  }

  url.searchParams.delete(starterDeckOverrideQueryKey);
  window.history.replaceState(window.history.state, "", `${url.pathname}${url.search}${url.hash}`);
}

const deckWireStyles = {
  actionRow: {
    display: "flex",
    flexWrap: "wrap",
    gap: 8
  },
  authorityNote: {
    alignItems: "center",
    borderTop: "1px solid #111",
    display: "flex",
    flexWrap: "wrap",
    gap: 10,
    marginTop: "auto",
    paddingTop: 14
  },
  bodyCopy: {
    color: "#272727",
    lineHeight: 1.55,
    margin: 0,
    maxWidth: 760
  },
  commandCode: {
    background: "#fff",
    border: "1px solid #111",
    color: "#111",
    fontSize: 12,
    lineHeight: 1.45,
    margin: 0,
    maxHeight: 240,
    overflow: "auto",
    padding: 12,
    whiteSpace: "pre-wrap"
  },
  commandPreview: {
    background: "#fff",
    border: "1px solid #111",
    borderRadius: 0,
    display: "grid",
    gap: 12,
    padding: 16
  },
  deckRow: {
    background: "#fff",
    border: "1px solid #111",
    borderRadius: 0,
    color: "#111"
  },
  deckRowMeta: {
    color: "#272727",
    overflowWrap: "anywhere"
  },
  deckSection: {
    background: "#fff",
    border: "1px solid #111",
    borderRadius: 0,
    boxShadow: "none"
  },
  eyebrow: {
    color: "#111",
    display: "block",
    fontFamily: "ui-monospace, SFMono-Regular, Menlo, Consolas, monospace",
    fontSize: 11,
    fontWeight: 900,
    letterSpacing: 0,
    marginBottom: 6,
    textTransform: "uppercase"
  },
  feedbackPanel: {
    background: "#fff",
    border: "1px solid #111",
    color: "#111",
    display: "grid",
    gap: 14,
    minHeight: 360,
    padding: 16
  },
  filterBar: {
    background: "#fff",
    border: "1px solid #111",
    borderRadius: 0,
    boxShadow: "none",
    color: "#111",
    display: "grid",
    gridTemplateColumns: "minmax(240px, 1fr) minmax(240px, 1fr)",
    gap: 14
  },
  filterLabel: {
    alignItems: "center",
    color: "#111",
    display: "flex",
    gap: 6,
    fontWeight: 900
  },
  flow: {
    display: "grid",
    gap: 10,
    gridTemplateColumns: "repeat(auto-fit, minmax(190px, 1fr))"
  },
  flowStep: {
    alignItems: "center",
    background: "#fff",
    border: "1px solid #111",
    color: "#111",
    display: "flex",
    gap: 12,
    minHeight: 64,
    padding: "12px 14px"
  },
  h1: {
    color: "#111",
    fontSize: 34,
    letterSpacing: 0,
    lineHeight: 1.08,
    margin: 0
  },
  h2: {
    color: "#111",
    fontSize: 20,
    letterSpacing: 0,
    lineHeight: 1.15,
    margin: 0
  },
  header: {
    alignItems: "center",
    background: "#fff",
    border: "1px solid #111",
    color: "#111",
    display: "flex",
    flexWrap: "wrap",
    gap: 16,
    justifyContent: "space-between",
    padding: 18
  },
  importEditor: {
    background: "#fff",
    border: "1px solid #111",
    display: "grid",
    gap: 12,
    padding: 16
  },
  importShell: {
    display: "grid",
    gap: 12,
    gridTemplateColumns: "minmax(320px, 1.2fr) minmax(280px, 0.8fr)"
  },
  input: {
    background: "#fff",
    border: "1px solid #111",
    borderRadius: 0,
    color: "#111"
  },
  issueList: {
    color: "#111",
    display: "grid",
    gap: 8,
    lineHeight: 1.45,
    margin: 0,
    paddingLeft: 18
  },
  legalityNote: {
    color: "#111"
  },
  page: {
    background: "#fff",
    border: "1px solid #111",
    color: "#111",
    padding: 18
  },
  panelHeader: {
    alignItems: "center",
    display: "flex",
    gap: 12,
    justifyContent: "space-between"
  },
  previewGrid: {
    border: "1px solid #111",
    display: "grid",
    gap: 0,
    gridTemplateColumns: "repeat(2, minmax(0, 1fr))"
  },
  previewCell: {
    borderBottom: "1px solid #111",
    color: "#111",
    padding: 10
  },
  primaryButton: {
    background: "#111",
    border: "1px solid #111",
    borderRadius: 0,
    color: "#fff"
  },
  secondaryButton: {
    background: "#fff",
    border: "1px solid #111",
    borderRadius: 0,
    color: "#111"
  },
  summaryBox: {
    background: "#fff",
    border: "1px solid #111",
    borderRadius: 0,
    color: "#111",
    padding: 12
  },
  summaryText: {
    color: "#272727",
    display: "block",
    marginTop: 6,
    overflowWrap: "anywhere"
  },
  summaryGrid: {
    background: "#fff",
    border: "1px solid #111",
    borderRadius: 0,
    boxShadow: "none",
    color: "#111",
    display: "grid",
    gap: 10,
    gridTemplateColumns: "repeat(auto-fit, minmax(155px, 1fr))"
  },
  textarea: {
    background: "#fff",
    border: "1px solid #111",
    borderRadius: 0,
    color: "#111",
    fontFamily: "ui-monospace, SFMono-Regular, Menlo, Consolas, monospace",
    fontSize: 13,
    lineHeight: 1.5,
    minHeight: 320,
    padding: 12,
    resize: "vertical"
  }
} satisfies Record<string, CSSProperties>;

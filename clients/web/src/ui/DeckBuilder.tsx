import { useEffect, useMemo, useState } from "react";
import { cardInfo, cardName, COLOR_HEX, useCards } from "../game/cards";
import { copyLimit, deckProblems, deleteDeck, exportDeck, isBanned, FIELDS, fitsLegend, MAIN_CATEGORIES, MAIN_MIN, RUNES, saveDeck, type CustomDeck } from "../game/decks";
import type { CardInfo } from "../game/types";

type Tab = "legend" | "champion" | "fields" | "runes" | "main";
const TABS: [Tab, string][] = [
  ["legend", "传奇"],
  ["champion", "英雄"],
  ["fields", "战场"],
  ["runes", "符文"],
  ["main", "主牌"],
];
const KINDS: [string, (c: CardInfo) => boolean][] = [
  ["全部", () => true],
  ["单位", (c) => c.category.includes("单位")],
  ["法术", (c) => c.category.includes("法术")],
  ["装备", (c) => c.category.includes("装备")],
];

/** One printing per card: alternate arts share a name, and copy limits count names. */
function printings(cards: Record<string, CardInfo>, keep: (c: CardInfo) => boolean, key: (c: CardInfo) => string) {
  const best = new Map<string, string>();
  for (const [no, c] of Object.entries(cards)) {
    if (!c?.category || !keep(c)) continue;
    const k = key(c);
    const cur = best.get(k);
    if (!cur || (c.sort ?? 1e9) < (cards[cur].sort ?? 1e9)) best.set(k, no);
  }
  return [...best.values()].sort((a, b) => (cards[a].cost ?? 0) - (cards[b].cost ?? 0) || cards[a].name.localeCompare(cards[b].name, "zh"));
}

/** Full-screen editor for a custom deck; every change is kept in this browser as it is made. */
export function DeckBuilder({ initial, onClose }: { initial: CustomDeck; onClose: () => void }) {
  const cards = useCards();
  const [deck, setDeck] = useState(initial);
  const [tab, setTab] = useState<Tab>(initial.legendCardNo ? "main" : "legend");
  const [query, setQuery] = useState("");
  const [kind, setKind] = useState(0);
  const [peek, setPeek] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [copied, setCopied] = useState(false);

  // A copied deck is kept as soon as it opens, so closing without edits still leaves it in the list.
  useEffect(() => {
    if (initial.legendCardNo) saveDeck(initial);
  }, [initial]);

  const update = (next: CustomDeck) => {
    setDeck(next);
    saveDeck(next);
  };
  const legend = cardInfo(deck.legendCardNo);
  const problems = deckProblems(deck);

  const legends = useMemo(() => printings(cards, (c) => c.category === "传奇", (c) => `${c.name}|${c.hero}`), [cards]);
  const champions = useMemo(
    () => printings(cards, (c) => c.category === "英雄单位" && Boolean(legend) && c.hero === legend!.hero, (c) => `${c.name}|${c.sub ?? ""}`),
    [cards, legend],
  );
  const fields = useMemo(() => printings(cards, (c) => c.category === "战场", (c) => c.name).filter((no) => fitsLegend(no, deck.legendCardNo)), [cards, deck.legendCardNo]);
  const runes = useMemo(() => printings(cards, (c) => c.category === "符文", (c) => c.name).filter((no) => fitsLegend(no, deck.legendCardNo)), [cards, deck.legendCardNo]);
  const pool = useMemo(
    () =>
      printings(cards, (c) => MAIN_CATEGORIES.has(c.category), (c) => c.name).filter((no) => {
        const c = cards[no];
        if (!fitsLegend(no, deck.legendCardNo)) return false;
        if (c.category.startsWith("专属") && c.hero !== legend?.hero) return false;
        if (!KINDS[kind][1](c)) return false;
        const q = query.trim();
        return !q || c.name.includes(q) || (c.effect ?? "").includes(q) || (c.tag ?? "").includes(q);
      }),
    [cards, deck.legendCardNo, legend, kind, query],
  );

  // Counts go by card name, as the copy limit does.
  const mainByName = useMemo(() => {
    const m = new Map<string, number>();
    for (const no of deck.mainDeck) m.set(cardName(no), (m.get(cardName(no)) ?? 0) + 1);
    return m;
  }, [deck.mainDeck]);
  const countIn = (list: string[], no: string) => list.filter((x) => cardName(x) === cardName(no)).length;

  const addMain = (no: string) => {
    if ((mainByName.get(cardName(no)) ?? 0) >= copyLimit(no)) return;
    update({ ...deck, mainDeck: [...deck.mainDeck, no] });
  };
  const removeFrom = (field: "mainDeck" | "runeDeck", no: string) => {
    const list = [...deck[field]];
    // Remove the last copy of this name, but never the champion's required copy from the main deck.
    for (let i = list.length - 1; i >= 0; i--) {
      if (cardName(list[i]) !== cardName(no)) continue;
      if (field === "mainDeck" && list[i] === deck.championCardNo && list.filter((x) => x === deck.championCardNo).length === 1) continue;
      list.splice(i, 1);
      break;
    }
    update({ ...deck, [field]: list });
  };
  const chooseLegend = (no: string) => {
    const same = cardInfo(no)?.hero === legend?.hero;
    // A different legend brings different colours: keep only what still fits.
    const keep = (x: string) => fitsLegend(x, no);
    update({
      ...deck,
      legendCardNo: no,
      championCardNo: same ? deck.championCardNo : "",
      mainDeck: deck.mainDeck.filter((x) => keep(x) && (same || x !== deck.championCardNo)),
      runeDeck: deck.runeDeck.filter(keep),
      battlefields: deck.battlefields.filter(keep),
    });
    setTab(same ? "main" : "champion");
  };
  const chooseChampion = (no: string) => {
    const main = deck.mainDeck.filter((x) => x !== deck.championCardNo);
    update({ ...deck, championCardNo: no, mainDeck: [no, ...main] });
    setTab(deck.battlefields.length < FIELDS ? "fields" : "main");
  };
  const toggleField = (no: string) => {
    const has = deck.battlefields.includes(no);
    if (!has && deck.battlefields.length >= FIELDS) return;
    update({ ...deck, battlefields: has ? deck.battlefields.filter((x) => x !== no) : [...deck.battlefields, no] });
  };

  const tile = (no: string, opts: { on?: boolean; count?: number; onClick: () => void; onMinus?: () => void; dim?: boolean }) => {
    const c = cardInfo(no);
    const banned = isBanned(no);
    return (
      <div key={no} className={`db__tile${opts.on ? " is-on" : ""}${opts.dim || banned ? " is-dim" : ""}`} onMouseEnter={() => setPeek(no)} onMouseLeave={() => setPeek(null)}>
        <button className="db__art" onClick={banned && !opts.on ? undefined : opts.onClick} onContextMenu={(e) => (e.preventDefault(), opts.onMinus?.())} title={c?.name}>
          {c?.image ? <img src={c.image} alt={c?.name ?? no} loading="lazy" /> : <span>{c?.name ?? no}</span>}
          {opts.count ? <b className="db__count">×{opts.count}</b> : null}
          {banned && <em className="db__ban">禁卡</em>}
        </button>
        {opts.onMinus && opts.count ? (
          <button className="db__minus" onClick={opts.onMinus} aria-label={`移除一张 ${c?.name}`}>
            −
          </button>
        ) : null}
      </div>
    );
  };

  const counts = {
    legend: legend ? 1 : 0,
    champion: deck.championCardNo ? 1 : 0,
    fields: deck.battlefields.length,
    runes: deck.runeDeck.length,
    main: deck.mainDeck.length,
  };
  const need: Record<Tab, number> = { legend: 1, champion: 1, fields: FIELDS, runes: RUNES, main: MAIN_MIN };

  return (
    <main className="db">
      <header className="db__head">
        <button className="btn btn--ghost" onClick={onClose}>
          完成
        </button>
        <input className="input db__name" value={deck.name} maxLength={30} onChange={(e) => update({ ...deck, name: e.target.value })} aria-label="卡组名称" />
        <span className={`db__status${problems.length ? "" : " is-ok"}`}>{problems.length ? `还有 ${problems.length} 处未完成` : "卡组合法，可以使用"}</span>
        <button
          className="btn btn--ghost"
          title="把卡组列表复制为文本，可以发给别人后在「导入卡组」里粘贴"
          onClick={() => void navigator.clipboard?.writeText(exportDeck(deck)).then(() => setCopied(true))}
        >
          {copied ? "已复制" : "导出"}
        </button>
        <button
          className={`btn btn--ghost db__delete${deleting ? " is-armed" : ""}`}
          onBlur={() => setDeleting(false)}
          onClick={() => {
            // Deleting asks twice: the first press only arms the button.
            if (!deleting) return setDeleting(true);
            deleteDeck(deck.id);
            onClose();
          }}
        >
          {deleting ? "再按一次删除" : "删除"}
        </button>
      </header>

      <div className="db__body">
        <section className="db__pick">
          <nav className="db__tabs" role="tablist">
            {TABS.map(([id, label]) => (
              <button
                key={id}
                role="tab"
                aria-selected={tab === id}
                className={`db__tab${tab === id ? " is-on" : ""}`}
                disabled={id !== "legend" && !legend}
                onClick={() => setTab(id)}
              >
                {label}
                <small className={counts[id] >= need[id] && (id !== "runes" && id !== "fields" ? true : counts[id] === need[id]) ? "is-ok" : ""}>
                  {counts[id]}/{need[id]}
                </small>
              </button>
            ))}
          </nav>

          {tab === "legend" && <div className="db__grid db__grid--wide">{legends.map((no) => tile(no, { on: no === deck.legendCardNo, onClick: () => chooseLegend(no) }))}</div>}
          {tab === "champion" && (
            <div className="db__grid db__grid--wide">
              {champions.length ? champions.map((no) => tile(no, { on: no === deck.championCardNo, onClick: () => chooseChampion(no) })) : <p className="muted">先选择传奇。</p>}
            </div>
          )}
          {tab === "fields" && (
            <>
              <p className="muted small">选 {FIELDS} 张不同的战场，点击切换。</p>
              <div className="db__grid db__grid--wide">
                {fields.map((no) => tile(no, { on: deck.battlefields.includes(no), dim: !deck.battlefields.includes(no) && deck.battlefields.length >= FIELDS, onClick: () => toggleField(no) }))}
              </div>
            </>
          )}
          {tab === "runes" && (
            <>
              <p className="muted small">共 {RUNES} 张，点击添加，右键或点「−」移除。</p>
              <div className="db__grid">
                {runes.map((no) =>
                  tile(no, {
                    count: countIn(deck.runeDeck, no),
                    dim: deck.runeDeck.length >= RUNES,
                    onClick: () => deck.runeDeck.length < RUNES && update({ ...deck, runeDeck: [...deck.runeDeck, no] }),
                    onMinus: () => removeFrom("runeDeck", no),
                  }),
                )}
              </div>
            </>
          )}
          {tab === "main" && (
            <>
              <div className="db__filters">
                <input className="input" placeholder="搜索卡名、效果或标签" value={query} onChange={(e) => setQuery(e.target.value)} />
                {KINDS.map(([label], i) => (
                  <button key={label} className={`chip${kind === i ? " chip--on" : ""}`} onClick={() => setKind(i)}>
                    {label}
                  </button>
                ))}
                <span className="muted small">点击添加，右键或点「−」移除；同名最多 3 张。</span>
              </div>
              <div className="db__grid">
                {pool.map((no) => {
                  const n = mainByName.get(cardName(no)) ?? 0;
                  return tile(no, { count: n, dim: n >= copyLimit(no), onClick: () => addMain(no), onMinus: () => removeFrom("mainDeck", no) });
                })}
              </div>
            </>
          )}
        </section>

        <aside className="db__side">
          <DeckList deck={deck} onPeek={setPeek} onAdd={addMain} onRemove={(no) => removeFrom("mainDeck", no)} />
          {problems.length > 0 && (
            <ul className="db__problems">
              {problems.map((p) => (
                <li key={p}>{p}</li>
              ))}
            </ul>
          )}
        </aside>
      </div>
      {peek && cardInfo(peek)?.image && <img className="db__peek" src={cardInfo(peek)!.image} alt="" />}
    </main>
  );
}

function DeckList({ deck, onPeek, onAdd, onRemove }: { deck: CustomDeck; onPeek: (no: string | null) => void; onAdd: (no: string) => void; onRemove: (no: string) => void }) {
  const rows = useMemo(() => {
    const m = new Map<string, { no: string; n: number }>();
    for (const no of deck.mainDeck) {
      const key = cardName(no);
      const cur = m.get(key);
      m.set(key, { no: cur?.no ?? no, n: (cur?.n ?? 0) + 1 });
    }
    return [...m.values()].sort((a, b) => (cardInfo(a.no)?.cost ?? 0) - (cardInfo(b.no)?.cost ?? 0));
  }, [deck.mainDeck]);
  const runes = useMemo(() => {
    const m = new Map<string, number>();
    for (const no of deck.runeDeck) {
      const color = cardInfo(no)?.colors?.[0] ?? "colorless";
      m.set(color, (m.get(color) ?? 0) + 1);
    }
    return [...m];
  }, [deck.runeDeck]);
  return (
    <div className="db__list">
      <p className="small">
        <span className="muted">传奇</span> {cardName(deck.legendCardNo) || "—"} · <span className="muted">英雄</span> {deck.championCardNo ? cardName(deck.championCardNo) : "—"}
      </p>
      <p className="small">
        <span className="muted">战场</span> {deck.battlefields.map((no) => cardName(no)).join("、") || "—"}
      </p>
      <p className="small db__runes">
        <span className="muted">符文 {deck.runeDeck.length}</span>
        {runes.map(([color, n]) => (
          <span key={color}>
            <i style={{ background: COLOR_HEX[color] ?? COLOR_HEX.colorless }} />×{n}
          </span>
        ))}
      </p>
      <h4>
        主牌 <span className="muted">{deck.mainDeck.length}</span>
      </h4>
      <ul>
        {rows.map(({ no, n }) => (
          <li key={no} onMouseEnter={() => onPeek(no)} onMouseLeave={() => onPeek(null)}>
            <span className="dp__cost">{cardInfo(no)?.cost ?? "–"}</span>
            <span className="dp__name">
              {cardName(no)}
              {no === deck.championCardNo && <em className="muted"> 英雄</em>}
            </span>
            <button className="db__step" onClick={() => onRemove(no)} aria-label="减少">
              −
            </button>
            <span className="dp__count">{n}</span>
            <button className="db__step" onClick={() => onAdd(no)} aria-label="增加">
              +
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}

import { useEffect, useMemo, useState } from "react";
import { AnimatePresence, motion } from "motion/react";
import { client, type ClientState } from "../net/client";
import { enabledCandidate } from "../game/actions";
import { BLANK_BATTLEFIELD, cardInfo, cardName, COLOR_HEX, useCards } from "../game/cards";
import { MAIN_CATEGORIES, RUNES } from "../game/decks";
import {
  basicRunePrintings,
  breaksTraits,
  deckLock,
  deckTraits,
  evenRunes,
  fieldsNeeded,
  fieldUsesLeft,
  MAX_TRAITS,
  poolCounts,
  readDraft,
  sealedProblems,
  SEALED_MAIN_MIN,
  seriesScore,
  usedCounts,
  writeDraft,
  type DeckLock,
  type SealedDeck,
} from "../game/sealed";
import type { CardInfo, MatchSeries, Prompt, SealedPackCard, SealedPool, Snapshot } from "../game/types";
import { Seat } from "./Lobby";

/** Sealed rooms stay on this screen until a game is under way, including the gaps between games. */
export function sealedSetupOpen(snapshot: Snapshot) {
  if (snapshot.format !== "sealed") return false;
  const phase = snapshot.series?.phase;
  if (phase === "choosing_play_order" || phase === "submitting_decks") return true;
  return phase !== "finished" && snapshot.timing.roomStatus !== "IN_PROGRESS" && !snapshot.timing.winnerPlayerId;
}

export function SealedScreen({ state, snapshot, prompt }: { state: ClientState; snapshot: Snapshot; prompt: Prompt | null }) {
  useCards();
  const [draft, setDraft] = useState(() => readDraft(state.roomId, state.handle));
  const pool = snapshot.sealedPool;
  const series = snapshot.series ?? null;
  const me = snapshot.players[state.handle];
  const opponent = Object.values(snapshot.players).find((p) => p.id !== state.handle);
  const bestOf = snapshot.bestOf ?? series?.bestOf ?? 1;
  const lock = deckLock(series, state.handle);
  // The server's copy wins, so a reconnect on another device needs nothing from this browser.
  const lockedDeck = series?.lockedDeck;
  const deck = lockedDeck ? { ...lockedDeck, battlefields: lock === "all" || !draft.deck.battlefields.length ? lockedDeck.battlefields : draft.deck.battlefields } : draft.deck;

  const update = (next: typeof draft) => {
    setDraft(next);
    writeDraft(state.roomId, state.handle, next);
  };

  // A new game starts from the last deck, minus any battlefield that has now been used up.
  const gameNumber = series?.gameNumber ?? 1;
  useEffect(() => {
    if (!pool || draft.forGame === gameNumber) return;
    if (lock !== "none") return update({ ...draft, forGame: gameNumber, packsOpened: pool.packs.length, deck });
    const owned = poolCounts(pool);
    const left = new Map<string, number>();
    const battlefields = draft.deck.battlefields.filter((no) => {
      const uses = fieldUsesLeft(no, owned.get(no) ?? 0, series?.usedBattlefields[state.handle]) - (left.get(no) ?? 0);
      left.set(no, (left.get(no) ?? 0) + 1);
      return uses > 0;
    });
    update({ ...draft, forGame: gameNumber, deck: { ...draft.deck, battlefields } });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [gameNumber, pool]);

  let body;
  if (series?.phase === "choosing_play_order") body = <PlayOrder series={series} meId={state.handle} prompt={prompt} pending={state.pending} drawn={lock === "all"} />;
  else if (!pool) body = <p className="sealed__wait muted">{prompt?.reason ?? "等待服务器发放卡包…"}</p>;
  else if (lock === "none" && draft.packsOpened < pool.packs.length)
    body = <PackOpening pool={pool} opened={draft.packsOpened} onOpened={(n) => update({ ...draft, packsOpened: n })} />;
  else
    body = (
      <SealedBuilder
        pool={pool}
        bestOf={bestOf}
        deck={deck}
        lock={lock}
        onChange={(deck) => update({ ...draft, deck })}
        prompt={prompt}
        pending={state.pending}
        submitted={Boolean(me?.deckSubmitted)}
        usedBefore={series?.usedBattlefields[state.handle]}
        opponentSideboard={opponent ? snapshot.sideboardCounts?.[opponent.id] : undefined}
        gameNumber={gameNumber}
      />
    );

  const score = series && series.bestOf > 1 ? seriesScore(series, state.handle) : null;
  return (
    <main className="sealed">
      <header className="sealed__head">
        <div>
          <p className="muted small">
            房间号 · 现开{snapshot.sealedPacks?.length ? ` · ${snapshot.sealedPacks.map((p) => `${p.setCode}×${p.count}`).join(" + ")}` : ""}
            {bestOf === 3 ? " · 三局两胜" : " · 单局"}
          </p>
          <button className="setup__code" title="复制房间号" onClick={() => void navigator.clipboard?.writeText(state.roomId)}>
            {state.roomId}
          </button>
        </div>
        {score && (
          <p className="sealed__score" aria-label={`第 ${series!.gameNumber} 局，总比分 ${score.mine} 比 ${score.theirs}`}>
            <small>第 {series!.gameNumber} 局</small>
            <b>
              {score.mine} : {score.theirs}
            </b>
          </p>
        )}
        <div className="setup__seats">
          <Seat name={me?.name ?? state.handle} deck={me?.deckSubmitted} ready={me?.ready} self />
          <span className="setup__vs">对</span>
          <Seat name={opponent?.name} deck={opponent?.deckSubmitted} ready={opponent?.ready} />
        </div>
        <button className="btn btn--ghost" onClick={() => client.leaveRoom()}>
          返回大厅
        </button>
      </header>
      {body}
    </main>
  );
}

function PlayOrder({ series, meId, prompt, pending, drawn }: { series: MatchSeries; meId: string; prompt: Prompt | null; pending: boolean; drawn: boolean }) {
  const chooser = series.playOrderChooserPlayerId;
  const canChoose = Boolean(enabledCandidate(prompt, "CHOOSE_PLAY_ORDER"));
  const first = series.gameNumber === 1;
  return (
    <section className="sealed__order">
      <h2>{series.bestOf > 1 ? `第 ${series.gameNumber} 局 · 先后手` : "先后手"}</h2>
      {canChoose ? (
        <>
          <p className="muted">{first ? "你被随机选中来决定先后手。" : drawn ? "上一局打平，由你决定这一局的先后手。" : `第 ${series.gameNumber - 1} 局你落败，由你决定这一局的先后手。`}</p>
          <div className="lobby__row">
            <button className="btn btn--primary" disabled={pending} onClick={() => void client.submit({ cmdType: "CHOOSE_PLAY_ORDER", goFirst: true })}>
              我先手
            </button>
            <button className="btn" disabled={pending} onClick={() => void client.submit({ cmdType: "CHOOSE_PLAY_ORDER", goFirst: false })}>
              我后手
            </button>
          </div>
        </>
      ) : (
        <p className="muted">{chooser && chooser !== meId ? `${first ? "" : drawn ? "上一局打平。" : `第 ${series.gameNumber - 1} 局你获胜。`}等待 ${chooser} 决定先后手…` : (prompt?.reason ?? "等待服务器…")}</p>
      )}
    </section>
  );
}

const SLOT_LABEL: Record<string, string> = { common: "普通", uncommon: "不凡", rare_or_better: "稀有或更高", foil: "闪卡", rune_or_token: "符文 / 指示物" };

function PackOpening({ pool, opened, onOpened }: { pool: SealedPool; opened: number; onOpened: (n: number) => void }) {
  const [peek, setPeek] = useState<string | null>(null);
  const [showing, setShowing] = useState<number | null>(null);
  const total = pool.packs.length;
  const mixed = !pool.setCode && pool.packs.some((p) => p.setCode);
  const pack = showing === null ? null : pool.packs[showing];
  const openNext = () => setShowing(showing === null ? opened : showing + 1);
  const finished = showing !== null && showing + 1 >= total;
  // Cards count as opened once seen, so a reload does not deal the same pack out again.
  useEffect(() => {
    if (showing !== null && showing + 1 < total) onOpened(showing + 1);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [showing]);

  return (
    <section className="packs">
      <div className="packs__row" aria-label={`卡包，共 ${total} 包`}>
        {pool.packs.map((_, i) => (
          <span key={i} className={`packs__pack${showing === i ? " is-on" : ""}${i < (showing ?? opened) ? " is-done" : ""}`} title={mixed ? pool.packs[i].setCode : undefined}>
            {i + 1}
            {mixed && <small>{pool.packs[i].setCode}</small>}
          </span>
        ))}
        <span className="packs__set muted small">{total} 包 · 共 {pool.pool.reduce((a, p) => a + p.count, 0)} 张进卡池</span>
      </div>
      {pack ? (
        <div className="packs__cards" key={showing}>
          {pack.cards.map((card, i) => (
            <PackCard key={`${card.cardNo}-${i}`} card={card} index={i} onPeek={setPeek} />
          ))}
        </div>
      ) : (
        <div className="packs__intro">
          <h2>{opened > 0 ? "继续开包" : "开包"}</h2>
          {mixed && <p className="muted small">自由组合：每包按所属系列（包号下方）开出。</p>}
          <p className="muted">开出的牌组成你的卡池。稀有槽、闪卡槽和符文槽的牌带闪面；符文 / 指示物槽只用于展示，不进卡池。</p>
        </div>
      )}
      <div className="lobby__row packs__actions">
        {finished ? (
          <button className="btn btn--primary" onClick={() => onOpened(total)}>
            开始组牌
          </button>
        ) : (
          <button className="btn btn--primary" onClick={openNext}>
            打开第 {(showing === null ? opened : showing + 1) + 1} 包
          </button>
        )}
        {!finished && (
          <button className="btn btn--ghost" onClick={() => onOpened(total)}>
            跳过，直接组牌
          </button>
        )}
      </div>
      {peek && cardInfo(peek)?.image && <img className="db__peek" src={cardInfo(peek)!.image} alt="" />}
    </section>
  );
}

function PackCard({ card, index, onPeek }: { card: SealedPackCard; index: number; onPeek: (no: string | null) => void }) {
  const info = cardInfo(card.cardNo);
  return (
    <motion.figure
      className={`packs__card packs__card--${card.slot}${card.foil ? " is-foil" : ""}`}
      initial={{ opacity: 0, y: 18, rotateY: 70 }}
      animate={{ opacity: 1, y: 0, rotateY: 0 }}
      transition={{ delay: index * 0.05, duration: 0.35, ease: [0.2, 0.8, 0.2, 1] }}
      onMouseEnter={() => onPeek(card.cardNo)}
      onMouseLeave={() => onPeek(null)}
    >
      {info?.image ? <img src={info.image} alt={info.name} /> : <span className="packs__name">{info?.name ?? card.cardNo}</span>}
      <figcaption>{SLOT_LABEL[card.slot] ?? card.slot}</figcaption>
    </motion.figure>
  );
}

type Tab = "main" | "runes" | "fields" | "roles";
const KINDS: [string, (c: CardInfo) => boolean][] = [
  ["全部", () => true],
  ["单位", (c) => c.category.includes("单位")],
  ["法术", (c) => c.category.includes("法术")],
  ["装备", (c) => c.category.includes("装备")],
];
const COLOR_LABEL: Record<string, string> = { red: "红", green: "绿", blue: "蓝", orange: "橙", purple: "紫", yellow: "黄" };

type BuilderProps = {
  pool: SealedPool;
  bestOf: number;
  deck: SealedDeck;
  lock: DeckLock;
  onChange: (deck: SealedDeck) => void;
  prompt: Prompt | null;
  pending: boolean;
  submitted: boolean;
  usedBefore?: string[];
  opponentSideboard?: number;
  gameNumber: number;
};

function SealedBuilder({ pool, bestOf, deck, lock, onChange, prompt, pending, submitted, usedBefore, opponentSideboard, gameNumber }: BuilderProps) {
  const cards = useCards();
  const [chosenTab, setTab] = useState<Tab>("main");
  const tab = lock === "none" ? chosenTab : "fields";
  const [query, setQuery] = useState("");
  const [kind, setKind] = useState(0);
  const [color, setColor] = useState<string | null>(null);
  const [peek, setPeek] = useState<string | null>(null);

  const canSubmit = Boolean(enabledCandidate(prompt, "SUBMIT_DECK"));
  const canReady = Boolean(enabledCandidate(prompt, "READY"));
  // Build freely while the opponent has not sat down yet; only a submitted deck the server holds is frozen.
  const locked = submitted && !canSubmit;
  const owned = useMemo(() => poolCounts(pool), [pool]);
  const used = useMemo(() => usedCounts(deck), [deck]);
  const traits = deckTraits(deck);
  // A draw forces the deck the server already accepted, including a battlefield that now counts as used.
  const problems = lock === "all" ? [] : sealedProblems(deck, pool, bestOf, usedBefore);
  const fields = fieldsNeeded(bestOf);
  const set = (patch: Partial<SealedDeck>) => {
    if (locked || lock === "all" || (lock === "deck" && Object.keys(patch).some((k) => k !== "battlefields"))) return;
    onChange({ ...deck, ...patch });
  };

  const inCategory = (test: (c: CardInfo) => boolean) =>
    [...owned.keys()].filter((no) => cards[no] && test(cards[no])).sort((a, b) => (cards[a].cost ?? 0) - (cards[b].cost ?? 0) || cards[a].name.localeCompare(cards[b].name, "zh") || a.localeCompare(b));
  const mainPool = useMemo(
    () =>
      inCategory((c) => {
        if (!MAIN_CATEGORIES.has(c.category) || !KINDS[kind][1](c)) return false;
        if (color && !c.colors.includes(color)) return false;
        const q = query.trim();
        return !q || c.name.includes(q) || (c.effect ?? "").includes(q) || (c.tag ?? "").includes(q);
      }),
    [cards, owned, kind, color, query], // eslint-disable-line react-hooks/exhaustive-deps
  );
  const legends = useMemo(() => inCategory((c) => c.category === "传奇"), [cards, owned]); // eslint-disable-line react-hooks/exhaustive-deps
  const champions = useMemo(() => inCategory((c) => c.category === "英雄单位"), [cards, owned]); // eslint-disable-line react-hooks/exhaustive-deps
  const battlefields = useMemo(() => inCategory((c) => c.category === "战场"), [cards, owned]); // eslint-disable-line react-hooks/exhaustive-deps
  // A custom mix has no single set; prefer the first pack's set for the rune printings.
  const runeSet = pool.setCode ?? pool.packs[0]?.setCode ?? "";
  const runes = useMemo(() => basicRunePrintings(cards, runeSet), [cards, runeSet]);
  // Everything the deck leaves out is the sideboard; the server computes the same number for the opponent to see.
  const sideboard = pool.pool.reduce((sum, p) => sum + Math.max(0, p.count - (used.get(p.cardNo) ?? 0)), 0);

  const addMain = (no: string) => {
    if ((used.get(no) ?? 0) >= (owned.get(no) ?? 0)) return;
    set({ mainDeck: [...deck.mainDeck, no] });
  };
  const removeOne = (list: string[], no: string) => {
    const at = list.lastIndexOf(no);
    return at < 0 ? list : [...list.slice(0, at), ...list.slice(at + 1)];
  };
  const removeMain = (no: string) => {
    const next = removeOne(deck.mainDeck, no);
    // The chosen champion must be in the main deck, so taking out its last copy also unchooses it.
    set({ mainDeck: next, championCardNo: deck.championCardNo === no && !next.includes(no) ? "" : deck.championCardNo });
  };
  const chooseLegend = (no: string) => {
    if (deck.legendCardNo === no) return set({ legendCardNo: "" });
    if ((used.get(no) ?? 0) >= (owned.get(no) ?? 0)) return;
    set({ legendCardNo: no });
  };
  const chooseChampion = (no: string) => {
    if (deck.championCardNo === no) return set({ championCardNo: "" });
    if (deck.mainDeck.includes(no)) return set({ championCardNo: no });
    if ((used.get(no) ?? 0) >= (owned.get(no) ?? 0)) return;
    set({ championCardNo: no, mainDeck: [no, ...deck.mainDeck] });
  };
  const addField = (no: string) => {
    if (fields === 1) return set({ battlefields: deck.battlefields[0] === no ? [] : [no] });
    if (deck.battlefields.length >= fields) return;
    if (no !== BLANK_BATTLEFIELD && (used.get(no) ?? 0) >= (owned.get(no) ?? 0)) return;
    set({ battlefields: [...deck.battlefields, no] });
  };

  const tile = (no: string, opts: { on?: boolean; count?: number; dim?: boolean; note?: string; onClick: () => void; onMinus?: () => void }) => {
    const c = cardInfo(no);
    return (
      <div key={no} className={`db__tile${opts.on ? " is-on" : ""}${opts.dim ? " is-dim" : ""}`} onMouseEnter={() => setPeek(no)} onMouseLeave={() => setPeek(null)}>
        <button className="db__art" onClick={opts.onClick} onContextMenu={(e) => (e.preventDefault(), opts.onMinus?.())} title={c?.name}>
          {c?.image ? <img src={c.image} alt={c?.name ?? no} loading="lazy" /> : <span>{c?.name ?? no}</span>}
          {opts.note ? <b className="db__count">{opts.note}</b> : null}
          {!opts.note && opts.count ? <b className="db__count">×{opts.count}</b> : null}
        </button>
        {opts.onMinus && opts.count ? (
          <button className="db__minus" onClick={opts.onMinus} aria-label={`移除一张 ${c?.name ?? no}`}>
            −
          </button>
        ) : null}
      </div>
    );
  };

  const tabs: [Tab, string, string, boolean][] = [
    ["main", "主牌", `${deck.mainDeck.length}/${SEALED_MAIN_MIN}+`, deck.mainDeck.length >= SEALED_MAIN_MIN],
    ["runes", "符文", `${deck.runeDeck.length}/${RUNES}`, deck.runeDeck.length === RUNES],
    ["fields", "战场", `${deck.battlefields.length}/${fields}`, deck.battlefields.length === fields],
    ["roles", "传奇 · 英雄", `${(deck.legendCardNo ? 1 : 0) + (deck.championCardNo ? 1 : 0)}/2`, true], // both are optional in sealed
  ];
  const missing = (deck.legendCardNo ? 0 : 1) + (deck.championCardNo ? 0 : 1);
  const submit = () =>
    void client.submit({
      cmdType: "SUBMIT_DECK",
      legendCardNo: deck.legendCardNo || null,
      championCardNo: deck.championCardNo || null,
      mainDeck: deck.mainDeck,
      runeDeck: deck.runeDeck,
      battlefields: deck.battlefields,
    });

  return (
    <div className="db__body sealed__body">
      {lock === "all" ? (
        <section className="db__pick sealed__rematch">
          <h2>第 {gameNumber} 局 · 打平重赛</h2>
          <p className="muted">上一局打平，这一局要用同样的卡组和战场，不能换备牌。</p>
          <div className="sealed__rematch-fields">
            {deck.battlefields.map((no, i) => {
              const c = cardInfo(no);
              return <figure key={`${no}-${i}`}>{c?.image ? <img src={c.image} alt={c.name} /> : null}<figcaption>{c?.name ?? no}</figcaption></figure>;
            })}
          </div>
          {canSubmit && (
            <button className="btn btn--primary" disabled={pending} onClick={submit}>
              沿用这副牌开始第 {gameNumber} 局
            </button>
          )}
        </section>
      ) : (
      <section className="db__pick">
        {lock === "deck" && <p className="sealed__lock-note">第一局被跳过，卡组不能再改，这一局只能重选战场。</p>}
        <nav className="db__tabs" role="tablist">
          {tabs.filter(([id]) => lock === "none" || id === "fields").map(([id, label, count, ok]) => (
            <button key={id} role="tab" aria-selected={tab === id} className={`db__tab${tab === id ? " is-on" : ""}`} onClick={() => setTab(id)}>
              {label}
              <small className={ok ? "is-ok" : ""}>{count}</small>
            </button>
          ))}
        </nav>

        {tab === "main" && (
          <>
            <div className="db__filters">
              <input className="input" placeholder="搜索卡名、效果或标签" value={query} onChange={(e) => setQuery(e.target.value)} />
              {KINDS.map(([label], i) => (
                <button key={label} className={`chip${kind === i ? " chip--on" : ""}`} onClick={() => setKind(i)}>
                  {label}
                </button>
              ))}
              {Object.entries(COLOR_LABEL).map(([id, label]) => (
                <button key={id} className={`chip sealed__color${color === id ? " chip--on" : ""}`} aria-pressed={color === id} aria-label={`只看${label}色`} onClick={() => setColor(color === id ? null : id)}>
                  <i style={{ background: COLOR_HEX[id] }} />
                </button>
              ))}
            </div>
            <div className="db__grid">
              {mainPool.length === 0 && <p className="muted">卡池里没有符合条件的牌。</p>}
              {mainPool.map((no) => {
                const n = deck.mainDeck.filter((x) => x === no).length;
                const have = owned.get(no) ?? 0;
                const left = (used.get(no) ?? 0) >= have;
                return tile(no, { count: n, note: have > 1 ? `${n}/${have}` : undefined, on: no === deck.championCardNo, dim: left || breaksTraits(no, traits), onClick: () => addMain(no), onMinus: () => removeMain(no) });
              })}
            </div>
          </>
        )}

        {tab === "runes" && (
          <>
            <div className="db__filters">
              <span className="muted small">共 {RUNES} 张，不占卡池。点击添加，右键或点「−」移除。</span>
              <button className="chip" disabled={locked || traits.length === 0} onClick={() => set({ runeDeck: evenRunes(deck, runes) })}>
                按已用特性平均分配
              </button>
            </div>
            <div className="db__grid db__grid--wide">
              {runes.map((no) =>
                tile(no, {
                  count: deck.runeDeck.filter((x) => x === no).length,
                  dim: deck.runeDeck.length >= RUNES || breaksTraits(no, traits),
                  onClick: () => deck.runeDeck.length < RUNES && set({ runeDeck: [...deck.runeDeck, no] }),
                  onMinus: () => set({ runeDeck: removeOne(deck.runeDeck, no) }),
                }),
              )}
            </div>
          </>
        )}

        {tab === "fields" && (
          <>
            <p className="muted small">
              {bestOf === 3 ? "这一局用 1 张战场。每张实体战场一场比赛只能用一次，空白战场不限。" : "带 3 张战场，开局随机抽 1 张。空白战场不限数量。"}
            </p>
            <div className="db__grid db__grid--wide">
              {[...battlefields, BLANK_BATTLEFIELD].map((no) => {
                const n = deck.battlefields.filter((x) => x === no).length;
                const usesLeft = fieldUsesLeft(no, owned.get(no) ?? 0, bestOf === 3 ? usedBefore : undefined);
                const spent = usesLeft <= 0;
                return tile(no, {
                  count: n,
                  on: n > 0,
                  dim: spent || (!n && fields > 1 && deck.battlefields.length >= fields),
                  note: spent ? "已用过" : undefined,
                  onClick: () => !spent && addField(no),
                  onMinus: fields > 1 ? () => set({ battlefields: removeOne(deck.battlefields, no) }) : undefined,
                });
              })}
            </div>
          </>
        )}

        {tab === "roles" && (
          <div className="sealed__roles">
            <p className="muted small">传奇和英雄都可以不选，也不需要是同一位英雄。{missing > 0 ? `现在缺 ${missing} 个，你每局的首回合开始阶段会额外抽 1 张牌。` : ""}</p>
            <h3>传奇</h3>
            <div className="db__grid db__grid--wide sealed__grid">
              {legends.length === 0 && <p className="muted">这次没有开到传奇。</p>}
              {legends.map((no) => tile(no, { on: no === deck.legendCardNo, onClick: () => chooseLegend(no) }))}
            </div>
            <h3>英雄</h3>
            <div className="db__grid db__grid--wide sealed__grid">
              {champions.length === 0 && <p className="muted">这次没有开到英雄。</p>}
              {champions.map((no) => tile(no, { on: no === deck.championCardNo, onClick: () => chooseChampion(no) }))}
            </div>
          </div>
        )}
      </section>
      )}

      <aside className="db__side">
        <div className="sealed__traits" aria-label={`特性 ${traits.length} / ${MAX_TRAITS}`}>
          <span className="muted small">特性</span>
          {traits.map((t) => (
            <i key={t} title={COLOR_LABEL[t]} style={{ background: COLOR_HEX[t] }} />
          ))}
          <b className={traits.length > MAX_TRAITS ? "is-over" : ""}>
            {traits.length}/{MAX_TRAITS}
          </b>
        </div>
        <SealedList deck={deck} locked={locked || lock !== "none"} onPeek={setPeek} onAdd={addMain} onRemove={removeMain} />
        <p className="small sealed__side-counts">
          <span>备牌 {sideboard} 张</span>
          {opponentSideboard !== undefined && <span className="muted">对手备牌 {opponentSideboard} 张</span>}
        </p>
        {problems.length > 0 && !submitted && (
          <ul className="db__problems">
            {problems.map((p) => (
              <li key={p}>{p}</li>
            ))}
          </ul>
        )}
        {canSubmit && lock !== "all" && (
          <button className="btn btn--primary" disabled={pending || problems.length > 0} onClick={submit}>
            {gameNumber > 1 ? `用这副牌打第 ${gameNumber} 局` : "提交卡组"}
          </button>
        )}
        {canReady && (
          <button className="btn btn--primary" disabled={pending} onClick={() => void client.submit({ cmdType: "READY" })}>
            准备开战
          </button>
        )}
        <AnimatePresence>
          {!canSubmit && !canReady && (
            <motion.p className="muted small" initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
              {!submitted ? "可以先组牌，对手入座后就能提交。" : (prompt?.reason ?? "等待服务器…")}
            </motion.p>
          )}
        </AnimatePresence>
      </aside>
      {peek && cardInfo(peek)?.image && <img className="db__peek" src={cardInfo(peek)!.image} alt="" />}
    </div>
  );
}

function SealedList({ deck, locked, onPeek, onAdd, onRemove }: { deck: SealedDeck; locked: boolean; onPeek: (no: string | null) => void; onAdd: (no: string) => void; onRemove: (no: string) => void }) {
  const rows = useMemo(() => {
    const counts = new Map<string, number>();
    for (const no of deck.mainDeck) counts.set(no, (counts.get(no) ?? 0) + 1);
    return [...counts].sort((a, b) => (cardInfo(a[0])?.cost ?? 0) - (cardInfo(b[0])?.cost ?? 0));
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
        <span className="muted">传奇</span> {deck.legendCardNo ? cardName(deck.legendCardNo) : "不使用"} · <span className="muted">英雄</span> {deck.championCardNo ? cardName(deck.championCardNo) : "不使用"}
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
        {rows.map(([no, n]) => (
          <li key={no} onMouseEnter={() => onPeek(no)} onMouseLeave={() => onPeek(null)}>
            <span className="dp__cost">{cardInfo(no)?.cost ?? "–"}</span>
            <span className="dp__name">
              {cardName(no)}
              {no === deck.championCardNo && <em className="muted"> 英雄</em>}
            </span>
            <button className="db__step" disabled={locked} onClick={() => onRemove(no)} aria-label="减少">
              −
            </button>
            <span className="dp__count">{n}</span>
            <button className="db__step" disabled={locked} onClick={() => onAdd(no)} aria-label="增加">
              +
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}

import { useEffect, useMemo, useState } from "react";
import { client, type ClientState } from "../net/client";
import { cardInfo, cardName, COLOR_HEX, useCards } from "../game/cards";
import type { PreconstructedDeck, Snapshot, Prompt } from "../game/types";
import { deckProblems, newDeckFrom, parseDeck, saveDeck, useCustomDecks, type CustomDeck } from "../game/decks";
import { DeckBuilder } from "./DeckBuilder";
import { enabledCandidate } from "../game/actions";

export function ConnectScreen({ state }: { state: ClientState }) {
  const [handle, setHandle] = useState(state.handle);
  const [server, setServer] = useState(state.server);
  const busy = state.phase === "connecting";

  return (
    <main className="gate">
      <div className="gate__mark" aria-hidden />
      <h1 className="gate__title">符文战场</h1>
      <p className="gate__lede">先到 8 分者获胜。占领战场、守住它，每回合都能得分。</p>
      <form
        className="gate__form"
        onSubmit={(e) => {
          e.preventDefault();
          if (handle.trim()) void client.connect(server.trim(), handle);
        }}
      >
        <label className="formfield">
          <span>玩家名</span>
          <input
            value={handle}
            onChange={(e) => setHandle(e.target.value)}
            placeholder="字母、数字或下划线"
            autoFocus
            maxLength={24}
          />
        </label>
        <details className="gate__advanced">
          <summary>服务器地址</summary>
          <label className="formfield">
            <span>地址</span>
            <input value={server} onChange={(e) => setServer(e.target.value)} />
          </label>
        </details>
        <button className="btn btn--primary" type="submit" disabled={busy || !handle.trim()}>
          {busy ? "正在连接…" : "进入大厅"}
        </button>
        {state.connectionNote && <p className="gate__note">{state.connectionNote}</p>}
      </form>
    </main>
  );
}

type Room = { roomId: string; hostPlayerId: string; seatCount: number; capacity: number; status: string };

export function LobbyScreen({ state }: { state: ClientState }) {
  const [code, setCode] = useState("");
  const [rooms, setRooms] = useState<Room[]>([]);

  useEffect(() => {
    let alive = true;
    const refresh = () =>
      client
        .listRooms()
        .then((r) => alive && setRooms(r.filter((room) => room.hostPlayerId !== state.handle)))
        .catch(() => undefined);
    void refresh();
    const timer = setInterval(refresh, 4000);
    return () => {
      alive = false;
      clearInterval(timer);
    };
  }, [state.handle]);

  const matching = state.phase === "matchmaking";

  return (
    <main className="gate gate--lobby">
      <header className="lobby__head">
        <h1 className="gate__title gate__title--small">符文战场</h1>
        <p className="lobby__who">
          以 <strong>{state.handle}</strong> 的身份登录
        </p>
      </header>
      <section className="lobby__grid">
        <div className="lobby__block">
          <h2>快速匹配</h2>
          <p>与下一位排队的玩家自动配对。</p>
          {matching ? (
            <div className="lobby__matching">
              <span className="pulse" aria-hidden /> 正在寻找对手…
              <button className="btn btn--ghost" onClick={() => void client.cancelMatch()}>
                取消匹配
              </button>
            </div>
          ) : (
            <button className="btn btn--primary" onClick={() => void client.quickMatch()}>
              开始匹配
            </button>
          )}
        </div>
        <div className="lobby__block">
          <h2>好友对战</h2>
          <p>创建房间后把房间号发给对手，或输入对手的房间号加入。</p>
          <div className="lobby__row">
            <button className="btn" disabled={matching} onClick={() => void client.createRoom()}>
              创建房间
            </button>
          </div>
          <form
            className="lobby__row"
            onSubmit={(e) => {
              e.preventDefault();
              if (code.trim()) void client.joinRoom(code.trim().toUpperCase());
            }}
          >
            <input
              className="input"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              placeholder="房间号"
              aria-label="房间号"
            />
            <button className="btn" type="submit" disabled={matching || !code.trim()}>
              加入
            </button>
          </form>
        </div>
        <div className="lobby__block lobby__block--wide">
          <h2>等待中的房间</h2>
          {rooms.length === 0 ? (
            <p className="muted">现在没有公开房间。创建一个，或开始快速匹配。</p>
          ) : (
            <ul className="rooms">
              {rooms.map((r) => (
                <li key={r.roomId}>
                  <span className="rooms__code">{r.roomId}</span>
                  <span className="muted">
                    {r.hostPlayerId}，{r.seatCount}/{r.capacity} 人
                  </span>
                  <button className="btn btn--small" disabled={matching} onClick={() => void client.joinRoom(r.roomId)}>
                    加入
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </section>
      <Records server={state.server} handle={state.handle} />
    </main>
  );
}

type Profile = { handle: string; totalMatches: number; wins: number; losses: number; winRate: number };
type MatchRecord = { roomId: string; winnerPlayerId: string; finishedAt: string; players: { playerId: string; score: number; won: boolean }[] };
type Ranked = Profile & { rank: number };

/** This player's record, recent matches and the server's leaderboard. */
function Records({ server, handle }: { server: string; handle: string }) {
  const [profile, setProfile] = useState<Profile | null>(null);
  const [matches, setMatches] = useState<MatchRecord[]>([]);
  const [board, setBoard] = useState<Ranked[]>([]);
  useEffect(() => {
    const base = server.replace(/\/$/, "");
    const get = <T,>(path: string) => fetch(base + path).then((r) => (r.ok ? (r.json() as Promise<T>) : Promise.reject(r.status)));
    const who = encodeURIComponent(handle);
    let alive = true;
    void Promise.allSettled([get<Profile>(`/players/${who}`), get<MatchRecord[]>(`/players/${who}/matches?limit=8`), get<Ranked[]>("/leaderboard?limit=10")]).then(([p, m, b]) => {
      if (!alive) return;
      if (p.status === "fulfilled") setProfile(p.value);
      if (m.status === "fulfilled") setMatches(m.value);
      if (b.status === "fulfilled") setBoard(b.value);
    });
    return () => {
      alive = false;
    };
  }, [server, handle]);

  if (!profile && !board.length) return null;
  const when = (iso: string) => {
    const d = new Date(iso);
    return Number.isNaN(d.getTime()) ? "" : d.toLocaleString("zh-CN", { month: "numeric", day: "numeric", hour: "2-digit", minute: "2-digit" });
  };
  return (
    <section className="records">
      <div className="lobby__block">
        <h2>我的战绩</h2>
        {profile && profile.totalMatches > 0 ? (
          <p className="records__summary">
            <b>{profile.totalMatches}</b> 局 · <b className="records__win">{profile.wins}</b> 胜 <b className="records__loss">{profile.losses}</b> 负 · 胜率{" "}
            <b>{Math.round(profile.winRate * 100)}%</b>
          </p>
        ) : (
          <p className="muted">还没有完成的对局。</p>
        )}
        {matches.length > 0 && (
          <ul className="records__list">
            {matches.map((m) => {
              const me = m.players.find((p) => p.playerId === handle);
              const opp = m.players.find((p) => p.playerId !== handle);
              return (
                <li key={m.roomId + m.finishedAt}>
                  <span className={me?.won ? "records__win" : "records__loss"}>{me?.won ? "胜" : "负"}</span>
                  <span className="records__opp">对 {opp?.playerId ?? "—"}</span>
                  <span className="records__score">
                    {me?.score ?? 0} : {opp?.score ?? 0}
                  </span>
                  <span className="muted">{when(m.finishedAt)}</span>
                </li>
              );
            })}
          </ul>
        )}
      </div>
      <div className="lobby__block">
        <h2>排行榜</h2>
        {board.length === 0 ? (
          <p className="muted">还没有记录。</p>
        ) : (
          <ol className="records__board">
            {board.map((r) => (
              <li key={r.handle} className={r.handle === handle ? "is-me" : ""}>
                <span className="records__rank">{r.rank}</span>
                <span className="records__opp">{r.handle}</span>
                <span className="muted">
                  {r.wins} 胜 {r.losses} 负
                </span>
                <span>{Math.round(r.winRate * 100)}%</span>
              </li>
            ))}
          </ol>
        )}
      </div>
    </section>
  );
}

export function SetupScreen({ state, snapshot, prompt }: { state: ClientState; snapshot: Snapshot | null; prompt: Prompt | null }) {
  const [decks, setDecks] = useState<PreconstructedDeck[]>([]);
  const [deckId, setDeckId] = useState<string>("");
  const [copied, setCopied] = useState(false);
  const custom = useCustomDecks();
  const [editing, setEditing] = useState<CustomDeck | null>(null);
  const [importing, setImporting] = useState<string | null>(null);
  const [importErrors, setImportErrors] = useState<string[]>([]);

  useEffect(() => {
    fetch(`${state.server.replace(/\/$/, "")}/decks/preconstructed`)
      .then((r) => r.json())
      .then((d: PreconstructedDeck[]) => {
        setDecks(d);
        setDeckId((current) => current || d[0]?.id || "");
      })
      .catch(() => client.toast("无法读取预组卡组列表"));
  }, [state.server]);

  const me = snapshot?.players[state.handle];
  const opponent = snapshot ? Object.values(snapshot.players).find((p) => p.id !== state.handle) : undefined;
  const canSubmit = Boolean(enabledCandidate(prompt, "SUBMIT_DECK"));
  const canReady = Boolean(enabledCandidate(prompt, "READY"));
  const all: PreconstructedDeck[] = [...custom, ...decks];
  const deck = all.find((d) => d.id === deckId);
  const deckIssues = deck && "custom" in deck ? deckProblems(deck) : [];

  const submitDeck = () => {
    if (!deck) return;
    void client.submit({
      cmdType: "SUBMIT_DECK",
      legendCardNo: deck.legendCardNo,
      championCardNo: deck.championCardNo,
      mainDeck: deck.mainDeck,
      runeDeck: deck.runeDeck,
      battlefields: deck.battlefields,
    });
  };

  if (editing)
    return (
      <DeckBuilder
        initial={editing}
        onClose={() => {
          setDeckId(editing.id);
          setEditing(null);
        }}
      />
    );

  return (
    <main className="gate gate--setup">
      <header className="setup__head">
        <div>
          <p className="muted">房间号</p>
          <button
            className="setup__code"
            title="复制房间号"
            onClick={() => {
              void navigator.clipboard?.writeText(state.roomId).then(() => setCopied(true));
            }}
          >
            {state.roomId}
          </button>
          {copied && <span className="muted">已复制</span>}
        </div>
        <div className="setup__seats">
          <Seat name={me?.name ?? state.handle} deck={me?.deckSubmitted} ready={me?.ready} self />
          <span className="setup__vs">对</span>
          <Seat name={opponent?.name} deck={opponent?.deckSubmitted} ready={opponent?.ready} />
        </div>
        <button className="btn btn--ghost" onClick={() => client.leaveRoom()}>
          返回大厅
        </button>
      </header>

      <section className="setup__body">
        <div className="setup__pick">
        <h2>{me?.deckSubmitted ? "卡组已提交" : "选择卡组"}</h2>
        <div className="decks" role="radiogroup" aria-label="预组卡组">
          {all.map((d) => {
            const legend = cardInfo(d.legendCardNo);
            const champion = cardInfo(d.championCardNo);
            return (
              <button
                key={d.id}
                role="radio"
                aria-checked={d.id === deckId}
                className={`deck${d.id === deckId ? " deck--on" : ""}`}
                disabled={!canSubmit}
                onClick={() => setDeckId(d.id)}
              >
                {champion?.image && <img src={champion.image} alt="" loading="lazy" />}
                <span className="deck__name">
                  {"custom" in d && <em className="deck__tag">自定义</em>}
                  {d.name}
                </span>
                <span className="deck__legend">{legend?.name ?? d.legendCardNo}</span>
              </button>
            );
          })}
        </div>
        <div className="setup__actions">
          {canSubmit && (
            <button className="btn btn--primary" disabled={!deck || state.pending || deckIssues.length > 0} onClick={submitDeck}>
              使用此卡组
            </button>
          )}
          {canSubmit && deck && (
            <button className="btn btn--ghost" onClick={() => setEditing("custom" in deck ? (deck as CustomDeck) : newDeckFrom(deck))}>
              {"custom" in deck ? "编辑卡组" : "复制并编辑"}
            </button>
          )}
          {canSubmit && (
            <button className="btn btn--ghost" onClick={() => setEditing(newDeckFrom())}>
              新建卡组
            </button>
          )}
          {canSubmit && (
            <button className="btn btn--ghost" onClick={() => (setImporting(""), setImportErrors([]))}>
              导入卡组
            </button>
          )}
          {deckIssues.length > 0 && <p className="warn small">这套卡组还不完整：{deckIssues[0]}</p>}
          {canReady && (
            <button className="btn btn--primary" disabled={state.pending} onClick={() => void client.submit({ cmdType: "READY" })}>
              准备开战
            </button>
          )}
          {!canSubmit && !canReady && <p className="muted">{prompt?.reason ?? "等待服务器…"}</p>}
        </div>
        {importing !== null && (
          <div className="import">
            <p className="small">粘贴在卡组编辑器里「导出」得到的卡组列表：</p>
            <textarea className="input import__text" rows={8} value={importing} onChange={(e) => setImporting(e.target.value)} placeholder={"# 卡组名\n传奇 UNL-181/219 戏命师\n主牌 3 …"} />
            {importErrors.length > 0 && (
              <ul className="db__problems">
                {importErrors.slice(0, 5).map((e) => (
                  <li key={e}>{e}</li>
                ))}
              </ul>
            )}
            <div className="lobby__row">
              <button
                className="btn btn--primary"
                disabled={!importing.trim()}
                onClick={() => {
                  const { deck: parsed, errors } = parseDeck(importing);
                  if (errors.length || !parsed.legendCardNo) {
                    setImportErrors(errors.length ? errors : ["没有找到传奇。"]);
                    return;
                  }
                  saveDeck(parsed);
                  setImporting(null);
                  setEditing(parsed);
                }}
              >
                导入并打开
              </button>
              <button className="btn btn--ghost" onClick={() => setImporting(null)}>
                取消
              </button>
            </div>
          </div>
        )}
        </div>
        {deck && <DeckPreview deck={deck} />}
      </section>
    </main>
  );
}

/** Sections of the main deck, in the order a player reads a list. */
const SECTIONS: [string, (category: string) => boolean][] = [
  ["单位", (c) => c.includes("单位")],
  ["法术", (c) => c.includes("法术")],
  ["装备", (c) => c.includes("装备")],
];

/** The chosen deck at a glance: its key cards, the main deck by type and cost, the cost curve and runes. */
function DeckPreview({ deck }: { deck: PreconstructedDeck }) {
  useCards(); // re-render once card names and images arrive
  const [peek, setPeek] = useState<string | null>(null);
  const rows = useMemo(() => {
    const counts = new Map<string, number>();
    for (const no of deck.mainDeck) counts.set(no, (counts.get(no) ?? 0) + 1);
    return [...counts].map(([no, n]) => ({ no, n, info: cardInfo(no) })).sort((a, b) => (a.info?.cost ?? 99) - (b.info?.cost ?? 99) || (a.info?.name ?? a.no).localeCompare(b.info?.name ?? b.no, "zh"));
  }, [deck]);
  const curve = useMemo(() => {
    const bins = Array.from({ length: 8 }, () => 0);
    for (const no of deck.mainDeck) bins[Math.min(7, cardInfo(no)?.cost ?? 0)]++;
    return bins;
  }, [deck, rows]); // eslint-disable-line react-hooks/exhaustive-deps
  const peak = Math.max(1, ...curve);
  const runes = useMemo(() => {
    const byColor = new Map<string, number>();
    for (const no of deck.runeDeck) {
      const color = cardInfo(no)?.colors?.[0] ?? "colorless";
      byColor.set(color, (byColor.get(color) ?? 0) + 1);
    }
    return [...byColor];
  }, [deck, rows]); // eslint-disable-line react-hooks/exhaustive-deps
  const other = rows.filter((r) => !SECTIONS.some(([, test]) => test(r.info?.category ?? "")));

  const thumb = (no: string, label: string) => (
    <figure className="dp__key" key={label + no} onMouseEnter={() => setPeek(no)} onMouseLeave={() => setPeek(null)}>
      {cardInfo(no)?.image ? <img src={cardInfo(no)!.image} alt="" loading="lazy" /> : <span className="dp__keyname">{cardName(no)}</span>}
      <figcaption>
        <span className="muted">{label}</span> {cardName(no)}
      </figcaption>
    </figure>
  );
  const list = (title: string, items: typeof rows) =>
    items.length > 0 && (
      <div className="dp__section" key={title}>
        <h4>
          {title} <span className="muted">{items.reduce((a, r) => a + r.n, 0)}</span>
        </h4>
        <ul>
          {items.map((r) => (
            <li key={r.no} onMouseEnter={() => setPeek(r.no)} onMouseLeave={() => setPeek(null)}>
              <span className="dp__cost">{r.info?.cost ?? "–"}</span>
              <span className="dp__dots">
                {(r.info?.colors ?? []).map((c) => (
                  <i key={c} style={{ background: COLOR_HEX[c] ?? COLOR_HEX.colorless }} />
                ))}
              </span>
              <span className="dp__name">{r.info?.name ?? r.no}</span>
              <span className="dp__count">×{r.n}</span>
            </li>
          ))}
        </ul>
      </div>
    );

  return (
    <aside className="dp" aria-label={`卡组预览：${deck.name}`}>
      <h3 className="dp__title">{deck.name}</h3>
      {deck.description && <p className="dp__desc">{deck.description}</p>}
      <div className="dp__keys">
        {thumb(deck.legendCardNo, "传奇")}
        {thumb(deck.championCardNo, "英雄")}
      </div>
      <div className="dp__fields">
        <span className="muted">战场</span>
        {deck.battlefields.map((no) => (
          <span key={no} className="chip" onMouseEnter={() => setPeek(no)} onMouseLeave={() => setPeek(null)}>
            {cardName(no)}
          </span>
        ))}
      </div>
      <div className="dp__stats">
        <div className="dp__curve" aria-label="费用曲线">
          {curve.map((n, cost) => (
            <div key={cost} className="dp__bar" title={`${cost === 7 ? "7+" : cost} 费：${n} 张`}>
              <span style={{ height: `${(n / peak) * 100}%` }}>{n > 0 && <b>{n}</b>}</span>
              <small>{cost === 7 ? "7+" : cost}</small>
            </div>
          ))}
        </div>
        <div className="dp__runes" aria-label="符文">
          <span className="muted">符文 {deck.runeDeck.length}</span>
          {runes.map(([color, n]) => (
            <span key={color} className="dp__rune">
              <i style={{ background: COLOR_HEX[color] ?? COLOR_HEX.colorless }} />×{n}
            </span>
          ))}
        </div>
      </div>
      <div className="dp__list">
        {SECTIONS.map(([title, test]) => list(title, rows.filter((r) => test(r.info?.category ?? ""))))}
        {list("其他", other)}
      </div>
      {peek && cardInfo(peek)?.image && <img className="dp__peek" src={cardInfo(peek)!.image} alt={cardName(peek)} />}
    </aside>
  );
}

function Seat({ name, deck, ready, self }: { name?: string; deck?: boolean; ready?: boolean; self?: boolean }) {
  return (
    <div className={`seat${self ? " seat--self" : " seat--opp"}`}>
      <span className="seat__name">{name ?? "等待对手加入"}</span>
      <span className="seat__state">{!name ? "空位" : ready ? "已准备" : deck ? "已选卡组" : "选卡组中"}</span>
    </div>
  );
}

import { useEffect, useState } from "react";
import { client, type ClientState } from "../net/client";
import { cardInfo } from "../game/cards";
import type { PreconstructedDeck, Snapshot, Prompt } from "../game/types";
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
    </main>
  );
}

export function SetupScreen({ state, snapshot, prompt }: { state: ClientState; snapshot: Snapshot | null; prompt: Prompt | null }) {
  const [decks, setDecks] = useState<PreconstructedDeck[]>([]);
  const [deckId, setDeckId] = useState<string>("");
  const [copied, setCopied] = useState(false);

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
  const deck = decks.find((d) => d.id === deckId);

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
        <h2>{me?.deckSubmitted ? "卡组已提交" : "选择卡组"}</h2>
        <div className="decks" role="radiogroup" aria-label="预组卡组">
          {decks.map((d) => {
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
                <span className="deck__name">{d.name}</span>
                <span className="deck__legend">{legend?.name ?? d.legendCardNo}</span>
              </button>
            );
          })}
        </div>
        <div className="setup__actions">
          {canSubmit && (
            <button className="btn btn--primary" disabled={!deck || state.pending} onClick={submitDeck}>
              使用此卡组
            </button>
          )}
          {canReady && (
            <button className="btn btn--primary" disabled={state.pending} onClick={() => void client.submit({ cmdType: "READY" })}>
              准备开战
            </button>
          )}
          {!canSubmit && !canReady && <p className="muted">{prompt?.reason ?? "等待服务器…"}</p>}
        </div>
      </section>
    </main>
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

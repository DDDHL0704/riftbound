import { useLayoutEffect, useRef, type CSSProperties, type MouseEvent, type ReactNode } from "react";
import type { ObjectAction } from "../game/actions";
import { cardInfo, COLOR_HEX } from "../game/cards";
import type { GameObject, SnapshotPlayer, TableBattlefield } from "../game/types";
import type { HoverHandler } from "./Card";

export type RenderObject = (id: string, size?: "board" | "hand" | "hero" | "mini" | "rune", opts?: { peek?: boolean }) => ReactNode;

/** Which public pile is open; its cards are read from the live snapshot. */
export type ZoneView = { title: string; playerId: string; zone: "graveyard" | "banished" } | null;

/**
 * A row that never grows past its own width: once its cards no longer fit side by side, each card
 * tucks under its left neighbour just enough to fit (at most most of a card, so every card stays
 * clickable).
 */
export function Crowd({ className, style, children }: { className: string; style?: CSSProperties; children: ReactNode }) {
  const ref = useRef<HTMLDivElement>(null);
  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    const cards = () => [...el.children].filter((c): c is HTMLElement => c instanceof HTMLElement && c.classList.contains("obj"));
    const fit = () => {
      const all = cards();
      let shift = 0;
      if (all.length > 1) {
        // Exhausted cards lie sideways and are wider, so add up each card's own width.
        const widths = all.map((c) => c.offsetWidth);
        const gap = parseFloat(getComputedStyle(el).columnGap) || 0;
        const need = widths.reduce((a, w) => a + w, 0) + (all.length - 1) * gap;
        shift = Math.min(Math.min(...widths) * 0.8 + gap, Math.max(0, (need - el.clientWidth) / (all.length - 1)));
      }
      el.style.setProperty("--crowd-shift", `${shift}px`);
    };
    // Re-fit when the row resizes, a card comes or goes, or a card turns sideways.
    const ro = new ResizeObserver(fit);
    const watch = () => {
      ro.disconnect();
      ro.observe(el);
      cards().forEach((c) => ro.observe(c));
    };
    watch();
    const mo = new MutationObserver(watch);
    mo.observe(el, { childList: true });
    return () => {
      ro.disconnect();
      mo.disconnect();
    };
  }, []);
  return (
    <div ref={ref} className={`${className} crowd`} style={style}>
      {children}
    </div>
  );
}

export function ScoreSpine({ me, opp, goal }: { me: SnapshotPlayer; opp?: SnapshotPlayer; goal: number }) {
  const notches = Array.from({ length: goal }, (_, i) => i + 1);
  return (
    <div className="spine" aria-label={`比分：你 ${me.score}，对手 ${opp?.score ?? 0}，先到 ${goal} 分获胜`}>
      <div className="spine__score spine__score--opp" data-oid={`score:${opp?.id}`}>
        {opp?.score ?? 0}
      </div>
      <div className="spine__track">
        <div className="spine__half spine__half--opp">
          {notches.map((n) => (
            <span key={n} className={`spine__notch${(opp?.score ?? 0) >= n ? " is-on" : ""}`} />
          ))}
        </div>
        <div className="spine__goal" title={`${goal} 分获胜`}>
          {goal}
        </div>
        <div className="spine__half spine__half--me">
          {notches.map((n) => (
            <span key={n} className={`spine__notch${me.score >= n ? " is-on" : ""}`} />
          ))}
        </div>
      </div>
      <div className="spine__score spine__score--me" data-oid={`score:${me.id}`}>
        {me.score}
      </div>
    </div>
  );
}

export function OpponentHand({ player }: { player: SnapshotPlayer }) {
  const n = player.zones.handHidden + player.zones.hand.length;
  return (
    <div className="opp-hand" aria-label={`对手手牌 ${n} 张`}>
      {Array.from({ length: n }, (_, i) => (
        <span key={i} className="opp-hand__card" style={{ transform: `rotate(${(i - (n - 1) / 2) * -3}deg)` }} />
      ))}
      <span className="opp-hand__count">{n}</span>
    </div>
  );
}

type SideProps = {
  player: SnapshotPlayer;
  side: "me" | "opp";
  render: RenderObject;
  onZone: (ids: string[]) => (e: MouseEvent) => void;
  legal: Map<string, string>;
  onOpenZone: (zone: ZoneView) => void;
  heroActions: (id: string) => ObjectAction[];
  onHeroAction: (action: ObjectAction) => void;
  runeBar?: ReactNode;
};

export function Side({ player, side, render, onZone, legal, onOpenZone, heroActions, onHeroAction, runeBar }: SideProps) {
  const z = player.zones;
  const units = z.baseCards.filter((id) => !player.objects[id]?.attachedToObjectId);
  const pool = player.runePool;
  const baseLegal = legal.has(`BASE:${player.id}`) || (legal.has("BASE") && side === "me");
  const traits = Object.entries(pool.powerByTrait || {}).filter(([, v]) => v > 0);
  const heroes = [...z.legendZone, ...z.championZone];
  return (
    <section className={`side side--${side}`} aria-label={side === "me" ? "你的区域" : "对手区域"}>
      <div className="heroes">
        {heroes.map((id) => {
          const isLegend = z.legendZone.includes(id);
          const acts = side === "me" ? heroActions(id) : [];
          return (
            <div className="hero" key={id}>
              {render(id, "hero")}
              <span className="hero__label">{isLegend ? "传奇" : "英雄"}</span>
              {acts.length > 0 && (
                <div className="hero__acts">
                  {acts.map((a) => (
                    <button
                      key={a.key}
                      className="hero__act"
                      onClick={(e) => {
                        e.stopPropagation();
                        onHeroAction(a);
                      }}
                      title={a.label}
                    >
                      <span>{a.cmdType === "PLAY_CARD" ? "打出英雄" : a.label}</span>
                      {a.detail && <i>{a.detail}</i>}
                    </button>
                  ))}
                </div>
              )}
            </div>
          );
        })}
      </div>

      <div className={`base${baseLegal ? " is-legal" : ""}`} onClick={onZone(["BASE", `BASE:${player.id}`])} data-drop="BASE" data-drop-owner={player.id} aria-label="基地">
        <span className="zone-label">{side === "me" ? "我的基地" : "对手基地"}</span>
        <Crowd className="base__units">
          {units.map((id) => render(id))}
          {units.length === 0 && <span className="base__empty">{side === "me" ? "打出的单位会进入这里" : ""}</span>}
        </Crowd>
      </div>

      <div className="runezone" aria-label="符文">
        {runeBar}
        <Crowd className="runes" style={{ ["--n" as string]: z.baseRunes.length }}>{z.baseRunes.map((id) => render(id, "rune"))}</Crowd>
        <div className="pool" title="符文池：本回合可用的法力与符能">
          <span className="pool__mana">
            <b>{pool.mana}</b> 法力
          </span>
          <span className="pool__power">
            <b>{pool.power}</b> 符能
            {traits.map(([t, v]) => (
              <i key={t} className="pool__trait" style={{ background: COLOR_HEX[t] ?? COLOR_HEX.colorless }} title={`${t} ${v}`} />
            ))}
          </span>
        </div>
      </div>

      <div className="piles">
        <Pile label="牌库" count={z.mainDeckCount} />
        <Pile label="符文库" count={z.runeDeckCount} rune />
        <div className="piles__meta">
          <button
            className="piles__link"
            disabled={!z.graveyard.length}
            onClick={(e) => {
              e.stopPropagation();
              onOpenZone({ title: `${side === "me" ? "我的" : "对手的"}废牌堆`, playerId: player.id, zone: "graveyard" });
            }}
          >
            废牌 {z.graveyard.length}
          </button>
          {z.banished.length > 0 && (
            <button
              className="piles__link"
              onClick={(e) => {
                e.stopPropagation();
                onOpenZone({ title: `${side === "me" ? "我的" : "对手的"}放逐区`, playerId: player.id, zone: "banished" });
              }}
            >
              放逐 {z.banished.length}
            </button>
          )}
          {player.experience > 0 && <span>经验 {player.experience}</span>}
        </div>
      </div>
    </section>
  );
}

function Pile({ label, count, rune }: { label: string; count: number; rune?: boolean }) {
  return (
    <div className={`pile${rune ? " pile--rune" : ""}${count === 0 ? " pile--empty" : ""}`} title={`${label} ${count} 张`}>
      <span className="pile__stack" aria-hidden />
      <span className="pile__count">{count}</span>
      <span className="pile__label">{label}</span>
    </div>
  );
}

type BattlefieldProps = {
  bf: TableBattlefield;
  meId: string;
  oppId?: string;
  objects: Record<string, GameObject>;
  render: RenderObject;
  onZone: (ids: string[]) => (e: MouseEvent) => void;
  legal: Map<string, string>;
  hover: HoverHandler;
  related: boolean;
};

export function Battlefield({ bf, meId, oppId, objects, render, onZone, legal, hover, related }: BattlefieldProps) {
  const info = cardInfo(bf.cardNo);
  const mine = bf.unitsBySide[meId] ?? [];
  const theirs = oppId ? bf.unitsBySide[oppId] ?? [] : [];
  const owner = bf.controllerId === meId ? "me" : bf.controllerId ? "opp" : "none";
  const isLegal = legal.has(bf.battlefieldObjectId) || legal.has(`BATTLEFIELD:${bf.battlefieldObjectId}`);
  const status = bf.contested ? "争夺中" : owner === "me" ? "你控制" : owner === "opp" ? "对手控制" : "无人控制";
  // Standby slots: the owner sees which card is there, everyone else only a face-down card.
  const slots = bf.standbySlots ?? [];
  const mySlots = slots.filter((s) => s.sidePlayerId === meId || s.controllerId === meId);
  const theirSlots = slots.filter((s) => !mySlots.includes(s));
  const renderSlot = (s: (typeof slots)[number]) =>
    s.objectId && objects[s.objectId] ? (
      render(s.objectId, "board", { peek: s.sidePlayerId === meId || s.controllerId === meId })
    ) : (
      <div className="standby-hidden" key={s.slotId} title="对手的待命牌">
        <span className="card__back" />
        <span className="card__ribbon card__ribbon--opp">待命</span>
      </div>
    );
  return (
    <section
      className={`field field--${owner}${bf.contested ? " field--contested" : ""}${isLegal ? " is-legal" : ""}${related ? " field--focus" : ""}`}
      onClick={onZone([bf.battlefieldObjectId, `BATTLEFIELD:${bf.battlefieldObjectId}`])}
      data-oid={bf.battlefieldObjectId}
      data-drop={`BATTLEFIELD:${bf.battlefieldObjectId}`}
      aria-label={`战场 ${info?.name ?? ""}，${status}`}
    >
      <Crowd className="field__row field__row--opp">{theirs.map((id) => render(id))}</Crowd>
      <div className="field__mid">
        {theirSlots.length > 0 && <div className="field__standby field__standby--opp">{theirSlots.map(renderSlot)}</div>}
        <div className="field__cardwrap">
          <div
            className="field__card"
            onMouseEnter={(e) => hover(bf.cardNo ?? null, undefined, e.currentTarget.getBoundingClientRect())}
            onMouseLeave={() => hover(null)}
          >
            {info?.image ? <img src={info.image} alt="" className="field__art" /> : <span className="field__fallback">{info?.name ?? bf.cardNo}</span>}
          </div>
          <span className="field__status">
            {status}
            {bf.scoredThisTurn && " · 本回合已得分"}
          </span>
        </div>
        {mySlots.length > 0 && <div className="field__standby field__standby--me">{mySlots.map(renderSlot)}</div>}
      </div>
      <Crowd className="field__row field__row--me">{mine.map((id) => render(id))}</Crowd>
    </section>
  );
}

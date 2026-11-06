import { useEffect, useLayoutEffect, useState } from "react";
import { AnimatePresence, motion } from "motion/react";
import { cardInfo } from "../game/cards";
import { client } from "../net/client";
import { enabledCandidate } from "../game/actions";
import type { Prompt, Snapshot } from "../game/types";
import { cue, setSoundEnabled, useSoundEnabled } from "../game/sound";

const TEMPO: { action: string; label: string }[] = [
  { action: "PASS_PRIORITY", label: "让过" },
  { action: "PASS_FOCUS", label: "让过焦点" },
  { action: "PASS", label: "跳过" },
  { action: "END_TURN", label: "结束回合" },
];

export const TEMPO_ACTIONS = TEMPO.map((t) => t.action);

/** Tempo controls sit beside the board, where the eye already is, not up in the sidebar. */
type DockProps = {
  prompt: Prompt | null;
  pending: boolean;
  autoPass: boolean;
  onToggleAutoPass: () => void;
  /** Cards and units that could still act this turn; ending the turn with some left asks twice. */
  unused: number;
};

export function Dock({ prompt, pending, autoPass, onToggleAutoPass, unused }: DockProps) {
  const tempo = TEMPO.filter((t) => enabledCandidate(prompt, t.action));
  const [armed, setArmed] = useState(false);
  useEffect(() => {
    setArmed(false);
  }, [prompt?.promptId]);
  useEffect(() => {
    if (!armed) return;
    const t = setTimeout(() => setArmed(false), 3000);
    return () => clearTimeout(t);
  }, [armed]);
  const run = (action: string) => {
    if (action === "END_TURN" && unused > 0 && !armed) {
      setArmed(true);
      return;
    }
    void client.submit({ cmdType: action });
  };
  const tap = enabledCandidate(prompt, "TAP_RUNE");
  const runes = (tap?.sources ?? []).map((s) => s.id).slice(0, Number(tap?.metadata?.maxBatchSize ?? 12));
  return (
    <div className="dock" onClick={(e) => e.stopPropagation()}>
      <label className="dock__auto" title="对手行动后，如果你没有任何可以响应的牌或技能，自动让过">
        <input type="checkbox" checked={autoPass} onChange={onToggleAutoPass} />
        自动让过
      </label>
      <SoundToggle />
      {runes.length >= 2 && (
        <button
          className="btn dock__runes"
          disabled={pending}
          title="一次横置所有未横置的符文"
          // The engine resolves a batch atomically: either every rune taps or none do.
          onClick={() => void client.submit({ cmdType: "TAP_RUNE", sourceObjectId: runes[0], sourceObjectIds: runes })}
        >
          全部横置 <b>+{runes.length}</b>
          <kbd>T</kbd>
        </button>
      )}
      {armed && <p className="dock__warn">还有 {unused} 张牌或单位可以行动，再按一次结束回合</p>}
      {tempo.map((t, i) => {
        const primary = i === tempo.length - 1;
        return (
          <button
            key={t.action}
            className={`btn dock__tempo${primary ? " btn--primary" : ""}`}
            data-hotkey={primary ? "space" : undefined}
            disabled={pending}
            onClick={() => run(t.action)}
          >
            {t.label}
            {primary && <kbd>空格</kbd>}
          </button>
        );
      })}
    </div>
  );
}

type Arrow = { key: string; d: string; mine: boolean; x2: number; y2: number };

/** Draws a line from each chain entry in the sidebar to the objects it targets. */
export function ChainArrows({ snapshot, meId }: { snapshot: Snapshot; meId: string }) {
  const [arrows, setArrows] = useState<Arrow[]>([]);

  useLayoutEffect(() => {
    let frame = 0;
    const measure = () => {
      const next: Arrow[] = [];
      for (const item of snapshot.stack ?? []) {
        const id = String(item.stackItemId ?? "");
        const targets = (item.targetObjectIds as string[] | undefined) ?? [];
        const from = document.querySelector(`[data-stack="${CSS.escape(id)}"]`)?.getBoundingClientRect();
        if (!from || !targets.length) continue;
        for (const t of targets) {
          const to = document.querySelector(`[data-oid="${CSS.escape(t)}"]`)?.getBoundingClientRect();
          if (!to) continue;
          const x1 = from.left;
          const y1 = from.top + from.height / 2;
          const x2 = to.left + to.width / 2;
          const y2 = to.top + to.height / 2;
          const bend = Math.min(160, Math.abs(x1 - x2) / 2);
          next.push({
            key: `${id}->${t}`,
            d: `M ${x1} ${y1} C ${x1 - bend} ${y1}, ${x2} ${y2 - bend * 0.6}, ${x2} ${y2}`,
            mine: item.controllerId === meId,
            x2,
            y2,
          });
        }
      }
      setArrows(next);
    };
    measure();
    const onResize = () => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(measure);
    };
    window.addEventListener("resize", onResize);
    return () => {
      cancelAnimationFrame(frame);
      window.removeEventListener("resize", onResize);
    };
  }, [snapshot, meId]);

  if (!arrows.length) return null;
  return (
    <svg className="arrows" aria-hidden>
      {arrows.map((a) => (
        <g key={a.key} className={a.mine ? "arrow--me" : "arrow--opp"}>
          <path d={a.d} />
          <circle cx={a.x2} cy={a.y2} r={7} />
        </g>
      ))}
    </svg>
  );
}

type Fx = { id: number; x: number; y: number; text: string; tone: "damage" | "destroy" | "score-me" | "score-opp" };
type Burst = { id: number; x: number; y: number; tone: "hit" | "destroy" | "score-me" | "score-opp" | "play"; bits: { dx: number; dy: number; s: number; d: number }[] };

/** Scatter of sparks for a burst; directions are spread evenly with a little jitter. */
function sparks(count: number, reach: number) {
  return Array.from({ length: count }, (_, i) => {
    const a = (i / count) * Math.PI * 2 + (Math.random() - 0.5) * 0.5;
    const r = reach * (0.55 + Math.random() * 0.45);
    return { dx: Math.cos(a) * r, dy: Math.sin(a) * r, s: 0.6 + Math.random() * 0.8, d: Math.random() * 0.08 };
  });
}

/** Restarts a one-shot CSS animation class on an element (used for hit shakes and score pops). */
function flash(el: Element | null, cls: string, ms: number) {
  if (!el) return;
  el.classList.remove(cls);
  void (el as HTMLElement).offsetWidth;
  el.classList.add(cls);
  setTimeout(() => el.classList.remove(cls), ms);
}
function SoundToggle() {
  const on = useSoundEnabled();
  return (
    <label className="dock__auto" title="出牌、伤害、得分等时机的提示音">
      <input type="checkbox" checked={on} onChange={(e) => setSoundEnabled(e.target.checked)} />
      音效
    </label>
  );
}

let fxSeq = 0;

/** Short-lived marks where events land: damage numbers, destroyed units, points scored. */
export function EventFx({ meId }: { meId: string }) {
  const [items, setItems] = useState<Fx[]>([]);
  const [bursts, setBursts] = useState<Burst[]>([]);

  useEffect(() => {
    // Batches that existed before this table mounted are history, not fresh feedback.
    let lastSeq = client.state.eventBatches.at(-1)?.seq ?? 0;
    // Measure inside the store listener, before React renders: events often share a network
    // frame with the snapshot that removes destroyed units, and one render would apply both.
    const unsubscribe = client.subscribe(() => {
      const fresh = client.state.eventBatches.filter((b) => b.seq > lastSeq);
      if (!fresh.length) return;
      lastSeq = fresh[fresh.length - 1].seq;
      const at = (...ids: unknown[]) => {
        for (const id of ids) {
          if (typeof id !== "string") continue;
          const r = document.querySelector(`[data-oid="${CSS.escape(id)}"]`)?.getBoundingClientRect();
          if (r) return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
        }
        return null;
      };
      const next: Fx[] = [];
      const booms: Burst[] = [];
      const boom = (pos: { x: number; y: number } | null, tone: Burst["tone"], count: number, reach: number) => {
        if (pos) booms.push({ id: ++fxSeq, x: pos.x, y: pos.y, tone, bits: sparks(count, reach) });
      };
      const node = (id: unknown) => (typeof id === "string" ? document.querySelector(`[data-oid="${CSS.escape(id)}"]`) : null);
      const stacked = new Map<string, number>();
      const push = (key: string, pos: { x: number; y: number } | null, text: string, tone: Fx["tone"]) => {
        if (!pos) return;
        const n = stacked.get(key) ?? 0;
        stacked.set(key, n + 1);
        next.push({ id: ++fxSeq, x: pos.x, y: pos.y - n * 30, text, tone });
      };
      for (const e of fresh.flatMap((b) => b.events)) {
        const p = e.payload ?? {};
        if (e.kind === "CARD_PLAYED") cue(p.playerId === meId ? "play" : "play-opp");
        else if (e.kind === "DAMAGE_APPLIED" && Number(p.damage) > 0) cue("hit");
        else if (e.kind === "UNIT_DESTROYED") cue("destroy");
        else if (e.kind === "SCORE_GAINED" && Number(p.amount) > 0) cue(p.playerId === meId ? "score-me" : "score-opp");
        if (e.kind === "DAMAGE_APPLIED" && Number(p.damage) > 0) {
          const pos = at(p.targetObjectId, p.battlefieldId);
          push(String(p.targetObjectId), pos, `−${p.damage}`, "damage");
          boom(pos, "hit", 8, 42);
          flash(node(p.targetObjectId), "fx-hit", 520);
        } else if (e.kind === "UNIT_DESTROYED") {
          const pos = at(p.targetObjectId);
          push(String(p.targetObjectId), pos, "摧毁", "destroy");
          boom(pos, "destroy", 16, 78);
        } else if (e.kind === "SCORE_GAINED" && Number(p.amount) > 0) {
          const tone = p.playerId === meId ? "score-me" : "score-opp";
          const pos = at(p.battlefieldObjectId, p.sourceObjectId, `score:${p.playerId}`);
          push(String(p.battlefieldObjectId ?? p.playerId), pos, `+${p.amount} 分`, tone);
          boom(pos, tone, 14, 70);
          boom(at(`score:${p.playerId}`), tone, 10, 40);
          flash(node(`score:${p.playerId}`), "fx-pop", 700);
        } else if (e.kind === "CARD_PLAYED" && p.playerId === meId) {
          boom(at(p.sourceObjectId), "play", 10, 54);
        }
      }
      if (booms.length) {
        setBursts((cur) => [...cur, ...booms]);
        const done = new Set(booms.map((b) => b.id));
        setTimeout(() => setBursts((cur) => cur.filter((b) => !done.has(b.id))), 1100);
      }
      if (!next.length) return;
      setItems((cur) => [...cur, ...next]);
      // Each batch removes only its own marks, so a quick follow-up batch can't cancel the cleanup.
      const ids = new Set(next.map((f) => f.id));
      setTimeout(() => setItems((cur) => cur.filter((f) => !ids.has(f.id))), 1500);
    });
    return () => {
      unsubscribe();
    };
  }, [meId]);

  if (!items.length && !bursts.length) return null;
  return (
    <div className="fx" aria-hidden>
      {bursts.map((b) => (
        <span key={b.id} className={`fx__burst fx__burst--${b.tone}`} style={{ left: b.x, top: b.y }}>
          <i className="fx__ring" />
          {b.bits.map((s, i) => (
            <i
              key={i}
              className="fx__spark"
              style={{ ["--dx" as string]: `${s.dx.toFixed(1)}px`, ["--dy" as string]: `${s.dy.toFixed(1)}px`, ["--s" as string]: s.s, ["--d" as string]: `${s.d}s` }}
            />
          ))}
        </span>
      ))}
      {items.map((f) => (
        <span key={f.id} className={`fx__item fx__item--${f.tone}`} style={{ left: f.x, top: f.y }}>
          {f.text}
        </span>
      ))}
    </div>
  );
}

type Shown = { id: number; cardNo: string; verb: string };
let spotSeq = 0;

/** When the opponent plays a card, show it large for a moment so the play is never missed. */
export function Spotlight({ meId }: { meId: string }) {
  const [shown, setShown] = useState<Shown | null>(null);
  useEffect(() => {
    let lastSeq = client.state.eventBatches.at(-1)?.seq ?? 0;
    let timer: ReturnType<typeof setTimeout> | undefined;
    const unsubscribe = client.subscribe(() => {
      const fresh = client.state.eventBatches.filter((b) => b.seq > lastSeq);
      if (!fresh.length) return;
      lastSeq = fresh[fresh.length - 1].seq;
      for (const e of fresh.flatMap((b) => b.events)) {
        const p = e.payload ?? {};
        const cardNo = typeof p.cardNo === "string" ? p.cardNo : undefined;
        if (e.kind !== "CARD_PLAYED" || !cardNo || p.playerId === meId || !cardInfo(cardNo)) continue;
        setShown({ id: ++spotSeq, cardNo, verb: "对手打出" });
        clearTimeout(timer);
        timer = setTimeout(() => setShown(null), 1700);
      }
    });
    return () => {
      unsubscribe();
      clearTimeout(timer);
    };
  }, [meId]);
  const info = shown ? cardInfo(shown.cardNo) : undefined;
  return (
    <AnimatePresence>
      {shown && info?.image && (
        <motion.div
          key={shown.id}
          className="spotlight"
          initial={{ opacity: 0, x: -40, scale: 0.85, rotate: -4 }}
          animate={{ opacity: 1, x: 0, scale: 1, rotate: 0 }}
          exit={{ opacity: 0, x: 30, scale: 0.9 }}
          transition={{ type: "spring", stiffness: 300, damping: 24 }}
          aria-live="polite"
        >
          <span className="spotlight__verb">{shown.verb}</span>
          <img src={info.image} alt={info.name} />
        </motion.div>
      )}
    </AnimatePresence>
  );
}

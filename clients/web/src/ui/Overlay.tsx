import { useEffect, useLayoutEffect, useState } from "react";
import { client } from "../net/client";
import { enabledCandidate } from "../game/actions";
import type { Prompt, Snapshot } from "../game/types";

const TEMPO: { action: string; label: string }[] = [
  { action: "PASS_PRIORITY", label: "让过" },
  { action: "PASS_FOCUS", label: "让过焦点" },
  { action: "PASS", label: "跳过" },
  { action: "END_TURN", label: "结束回合" },
];

export const TEMPO_ACTIONS = TEMPO.map((t) => t.action);

/** Tempo controls sit beside the board, where the eye already is, not up in the sidebar. */
export function Dock({ prompt, pending }: { prompt: Prompt | null; pending: boolean }) {
  const tempo = TEMPO.filter((t) => enabledCandidate(prompt, t.action));
  const tap = enabledCandidate(prompt, "TAP_RUNE");
  const runes = (tap?.sources ?? []).map((s) => s.id).slice(0, Number(tap?.metadata?.maxBatchSize ?? 12));
  if (!tempo.length && runes.length < 2) return null;
  return (
    <div className="dock" onClick={(e) => e.stopPropagation()}>
      {runes.length >= 2 && (
        <button
          className="btn dock__runes"
          disabled={pending}
          title="一次横置所有未横置的符文"
          // The engine resolves a batch atomically: either every rune taps or none do.
          onClick={() => void client.submit({ cmdType: "TAP_RUNE", sourceObjectId: runes[0], sourceObjectIds: runes })}
        >
          横置全部符文 <b>+{runes.length}</b>
        </button>
      )}
      {tempo.map((t, i) => (
        <button
          key={t.action}
          className={`btn dock__tempo${i === tempo.length - 1 ? " btn--primary" : ""}`}
          disabled={pending}
          onClick={() => void client.submit({ cmdType: t.action })}
        >
          {t.label}
        </button>
      ))}
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
let fxSeq = 0;

/** Short-lived marks where events land: damage numbers, destroyed units, points scored. */
export function EventFx({ meId }: { meId: string }) {
  const [items, setItems] = useState<Fx[]>([]);

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
      const stacked = new Map<string, number>();
      const push = (key: string, pos: { x: number; y: number } | null, text: string, tone: Fx["tone"]) => {
        if (!pos) return;
        const n = stacked.get(key) ?? 0;
        stacked.set(key, n + 1);
        next.push({ id: ++fxSeq, x: pos.x, y: pos.y - n * 30, text, tone });
      };
      for (const e of fresh.flatMap((b) => b.events)) {
        const p = e.payload ?? {};
        if (e.kind === "DAMAGE_APPLIED" && Number(p.damage) > 0)
          push(String(p.targetObjectId), at(p.targetObjectId, p.battlefieldId), `−${p.damage}`, "damage");
        else if (e.kind === "UNIT_DESTROYED") push(String(p.targetObjectId), at(p.targetObjectId), "摧毁", "destroy");
        else if (e.kind === "SCORE_GAINED" && Number(p.amount) > 0)
          push(
            String(p.battlefieldObjectId ?? p.playerId),
            at(p.battlefieldObjectId, p.sourceObjectId, `score:${p.playerId}`),
            `+${p.amount} 分`,
            p.playerId === meId ? "score-me" : "score-opp",
          );
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

  if (!items.length) return null;
  return (
    <div className="fx" aria-hidden>
      {items.map((f) => (
        <span key={f.id} className={`fx__item fx__item--${f.tone}`} style={{ left: f.x, top: f.y }}>
          {f.text}
        </span>
      ))}
    </div>
  );
}

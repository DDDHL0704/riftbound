import { useEffect, useState, type CSSProperties } from "react";
import { AnimatePresence, motion } from "motion/react";
import { client } from "../net/client";
import { cardInfo } from "../game/cards";
import type { Snapshot } from "../game/types";

/*
 * Staging for the moments that decide a match: a battle being declared, spells and abilities
 * flying to their targets, and a battlefield changing hands or being held. Purely decorative and
 * driven by the server's events; nothing here reads or changes game state.
 */

type Pos = { x: number; y: number };
type Bolt = { id: number; from: Pos; to: Pos; mine: boolean; delay: number };
type Mark = { id: number; at: Pos; kind: "clash" | "conquer" | "hold"; mine: boolean; text: string };

let seq = 0;
const vars = (v: Record<string, string | number>) => v as CSSProperties;

function centre(el: Element | null | undefined): Pos | null {
  const r = el?.getBoundingClientRect();
  return r && r.width ? { x: r.left + r.width / 2, y: r.top + r.height / 2 } : null;
}
const byOid = (id: unknown) => (typeof id === "string" ? document.querySelector(`[data-oid="${CSS.escape(id)}"]`) : null);

/** Restarts a one-shot CSS animation class on an element. */
function flash(el: Element | null, cls: string, ms: number, style?: Record<string, string>) {
  if (!(el instanceof HTMLElement)) return;
  el.classList.remove(cls);
  for (const [k, v] of Object.entries(style ?? {})) el.style.setProperty(k, v);
  void el.offsetWidth;
  el.classList.add(cls);
  setTimeout(() => el.classList.remove(cls), ms);
}

export function Stage({ meId }: { meId: string }) {
  const [bolts, setBolts] = useState<Bolt[]>([]);
  const [marks, setMarks] = useState<Mark[]>([]);

  useEffect(() => {
    let lastSeq = client.state.eventBatches.at(-1)?.seq ?? 0;
    // Measured in the store listener, before React renders the snapshot that moves the cards.
    const unsubscribe = client.subscribe(() => {
      const fresh = client.state.eventBatches.filter((b) => b.seq > lastSeq);
      if (!fresh.length) return;
      lastSeq = fresh[fresh.length - 1].seq;
      const nextBolts: Bolt[] = [];
      const nextMarks: Mark[] = [];
      for (const e of fresh.flatMap((b) => b.events)) {
        const p = e.payload ?? {};
        if (e.kind === "STACK_ITEM_ADDED") {
          const targets = (Array.isArray(p.targetObjectIds) ? p.targetObjectIds : p.targetObjectId ? [p.targetObjectId] : []) as string[];
          if (!targets.length) continue;
          const mine = p.controllerId === meId;
          // A spell from the opponent's hidden hand starts from their side of the table.
          const from = centre(byOid(p.sourceObjectId)) ?? centre(document.querySelector(mine ? ".hand" : ".opp-hand"));
          if (!from) continue;
          targets.forEach((t, i) => {
            const to = centre(byOid(t));
            if (to && Math.hypot(to.x - from.x, to.y - from.y) > 30) nextBolts.push({ id: ++seq, from, to, mine, delay: i * 0.08 });
          });
        } else if (e.kind === "BATTLE_DECLARED") {
          const field = byOid(p.battlefieldId);
          const at = centre(field);
          const mine = p.playerId === meId;
          if (at) nextMarks.push({ id: ++seq, at, kind: "clash", mine, text: "交战" });
          flash(field, "stage-clash", 900);
          // Attackers lunge toward the rift: the player's from below, the opponent's from above.
          for (const id of (p.attackerObjectIds as string[] | undefined) ?? []) flash(byOid(id), "stage-lunge", 650, { "--lunge": mine ? "-16px" : "16px" });
          for (const id of (p.defenderObjectIds as string[] | undefined) ?? []) flash(byOid(id), "stage-brace", 650);
        } else if (e.kind === "BATTLEFIELD_CONQUERED" || e.kind === "BATTLEFIELD_HELD") {
          const field = byOid(p.battlefieldObjectId ?? p.battlefieldId);
          const at = centre(field);
          const mine = p.playerId === meId;
          const conquer = e.kind === "BATTLEFIELD_CONQUERED";
          if (at) nextMarks.push({ id: ++seq, at, kind: conquer ? "conquer" : "hold", mine, text: conquer ? "征服" : "据守" });
          flash(field?.querySelector(".field__card") ?? null, conquer ? "stage-flip" : "stage-hold", 1000, { "--side": mine ? "var(--brass)" : "var(--glacier)" });
        }
      }
      if (nextBolts.length) {
        setBolts((cur) => [...cur, ...nextBolts]);
        const ids = new Set(nextBolts.map((b) => b.id));
        setTimeout(() => setBolts((cur) => cur.filter((b) => !ids.has(b.id))), 1300);
      }
      if (nextMarks.length) {
        setMarks((cur) => [...cur, ...nextMarks]);
        const ids = new Set(nextMarks.map((m) => m.id));
        setTimeout(() => setMarks((cur) => cur.filter((m) => !ids.has(m.id))), 1600);
      }
    });
    return () => {
      unsubscribe();
    };
  }, [meId]);

  if (!bolts.length && !marks.length) return null;
  return (
    <div className="stage" aria-hidden>
      {bolts.map((b) => {
        const dx = b.to.x - b.from.x;
        const dy = b.to.y - b.from.y;
        return (
          <span
            key={b.id}
            className={`stage__bolt stage__bolt--${b.mine ? "me" : "opp"}`}
            style={vars({ left: `${b.from.x}px`, top: `${b.from.y}px`, "--dx": `${dx}px`, "--dy": `${dy}px`, "--angle": `${Math.atan2(dy, dx)}rad`, "--len": `${Math.hypot(dx, dy)}px`, "--delay": `${b.delay}s` })}
          >
            <i className="stage__trail" />
            <i className="stage__orb" />
            <i className="stage__hit" />
          </span>
        );
      })}
      {marks.map((m) => (
        <span key={m.id} className={`stage__mark stage__mark--${m.kind} stage__mark--${m.mine ? "me" : "opp"}`} style={{ left: m.at.x, top: m.at.y }}>
          {m.kind === "clash" && (
            <svg className="stage__blades" viewBox="0 0 100 100">
              <path d="M18 18 L70 70 M70 70 l8 -2 l-2 8 z M24 34 l10 -10" />
              <path d="M82 18 L30 70 M30 70 l-8 -2 l2 8 z M76 34 l-10 -10" />
            </svg>
          )}
          <b>{m.text}</b>
        </span>
      ))}
    </div>
  );
}

/** Art for a chain entry; hidden sources and abilities without a card show a card back. */
const art = (item: Snapshot["stack"][number]) =>
  item.sourceVisibility === "HIDDEN" || typeof item.cardNo !== "string" ? undefined : cardInfo(item.cardNo)?.image;

/** The chain shown on the table beside the tempo controls: what will resolve next, on top. */
export function StackShow({ snapshot, meId }: { snapshot: Snapshot; meId: string }) {
  const items = snapshot.stack ?? [];
  return (
    <div className="stackshow" aria-hidden>
      <AnimatePresence>
        {items.map((s, i) => {
          const depth = items.length - 1 - i;
          const mine = s.controllerId === meId;
          return (
            <motion.div
              key={String(s.stackItemId ?? i)}
              className={`stackshow__card stackshow__card--${mine ? "me" : "opp"}${depth === 0 ? " is-top" : ""}`}
              initial={{ opacity: 0, y: mine ? 80 : -80, scale: 0.6, rotate: mine ? -8 : 8 }}
              animate={{ opacity: depth > 3 ? 0 : 1 - depth * 0.15, y: depth * -16, x: depth * 16, scale: 1 - depth * 0.06, rotate: (i % 2 ? 1 : -1) * Math.min(depth, 3) * 3 }}
              exit={{ opacity: 0, y: -40, scale: 1.15, filter: "brightness(2)" }}
              transition={{ type: "spring", stiffness: 320, damping: 26 }}
              style={{ zIndex: i }}
            >
              {art(s) ? <img src={art(s)} alt="" /> : <span className="card__back" />}
            </motion.div>
          );
        })}
      </AnimatePresence>
      {items.length > 0 && <span className="stackshow__label">结算链 · {items.length}</span>}
    </div>
  );
}

import type { CSSProperties, PointerEvent } from "react";
import { cardInfo, useCards } from "../game/cards";

/** A small deterministic generator, so the motes sit in the same places on every render and reload. */
function seeded(seed: number) {
  let s = seed;
  return () => {
    s = (s * 16807) % 2147483647;
    return (s - 1) / 2147483646;
  };
}

const vars = (v: Record<string, string | number>) => v as CSSProperties;

function motes(count: number, seed: number) {
  const r = seeded(seed);
  return Array.from({ length: count }, (_, i) => ({
    key: i,
    style: vars({
      "--x": `${(r() * 100).toFixed(2)}%`,
      "--size": `${(1.5 + r() * 3).toFixed(1)}px`,
      "--dur": `${(14 + r() * 18).toFixed(1)}s`,
      "--delay": `${(-r() * 30).toFixed(1)}s`,
      "--drift": `${(r() * 60 - 30).toFixed(0)}px`,
      "--hue": r() < 0.55 ? "var(--mote-a)" : "var(--mote-b)",
    }),
  }));
}

const TABLE_MOTES = motes(22, 7);
const GATE_MOTES = motes(30, 11);

/** Slowly rising light motes and a drifting aurora behind the board or the lobby. Purely decorative. */
export function Ambient({ variant }: { variant: "table" | "gate" }) {
  const list = variant === "table" ? TABLE_MOTES : GATE_MOTES;
  return (
    <div className={`ambient ambient--${variant}`} aria-hidden>
      <span className="ambient__aurora" />
      {list.map((m) => (
        <span key={m.key} className="ambient__mote" style={m.style} />
      ))}
    </div>
  );
}

const CONFETTI = (() => {
  const r = seeded(23);
  const colors = ["#ffd27a", "#f0b54a", "#fff1b8", "#b78bff", "#6fd0ee", "#ff9e64"];
  return Array.from({ length: 64 }, (_, i) => ({
    key: i,
    style: vars({
      "--x": `${(r() * 100).toFixed(2)}%`,
      "--delay": `${(r() * 1.6).toFixed(2)}s`,
      "--dur": `${(2.6 + r() * 2.2).toFixed(2)}s`,
      "--spin": `${Math.round(360 + r() * 720) * (r() < 0.5 ? -1 : 1)}deg`,
      "--sway": `${(r() * 120 - 60).toFixed(0)}px`,
      "--w": `${(5 + r() * 6).toFixed(1)}px`,
      "--c": colors[i % colors.length],
    }),
  }));
})();

/** Victory: a burst of falling foil pieces over the result screen. */
export function Confetti() {
  return (
    <div className="confetti" aria-hidden>
      {CONFETTI.map((c) => (
        <span key={c.key} style={c.style} />
      ))}
    </div>
  );
}

/** Tilts a hand card toward the pointer and moves its glare; values live in CSS variables on the element. */
export function tiltHandlers() {
  return {
    onPointerMove: (e: PointerEvent<HTMLElement>) => {
      if (e.pointerType !== "mouse") return;
      const el = e.currentTarget;
      // Measure the card face, which lifts out of its fan slot on hover.
      const r = (el.querySelector(".card") ?? el).getBoundingClientRect();
      const px = (e.clientX - r.left) / r.width;
      const py = (e.clientY - r.top) / r.height;
      el.style.setProperty("--rx", `${((0.5 - py) * 14).toFixed(2)}deg`);
      el.style.setProperty("--ry", `${((px - 0.5) * 16).toFixed(2)}deg`);
      el.style.setProperty("--gx", `${(px * 100).toFixed(1)}%`);
      el.style.setProperty("--gy", `${(py * 100).toFixed(1)}%`);
    },
    onPointerLeave: (e: PointerEvent<HTMLElement>) => {
      const el = e.currentTarget;
      for (const k of ["--rx", "--ry", "--gx", "--gy"]) el.style.removeProperty(k);
    },
  };
}

/** Legends fanned out like a hand above the lobby; cards whose art is unavailable are skipped. */
const SHOWCASE = ["UNL-181/219", "UNL-191/219", "UNL-197/219", "UNL-199/219", "UNL-203/219"];

export function Showcase() {
  useCards();
  const list = SHOWCASE.flatMap((no) => {
    const info = cardInfo(no);
    return info?.image ? [{ no, image: info.image, name: info.name }] : [];
  });
  if (!list.length) return null;
  const mid = (list.length - 1) / 2;
  return (
    <div className="showcase" aria-hidden>
      {list.map((c, i) => (
        <span
          key={c.no}
          className="showcase__card"
          style={vars({ "--a": `${(i - mid) * 9}deg`, "--lift": `${Math.abs(i - mid) * 14}px`, "--i": i })}
        >
          <img src={c.image} alt="" draggable={false} />
        </span>
      ))}
    </div>
  );
}

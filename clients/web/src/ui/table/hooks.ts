import { useEffect, useRef, useState } from "react";
import { client } from "../../net/client";
import { enabledCandidate } from "../../game/actions";
import { cue, wantAmbience } from "../../game/sound";
import type { GameObject, Prompt, Snapshot } from "../../game/types";
import type { HoverHandler } from "../Card";

export const TABLE_MIN_WIDTH = 1100;

/**
 * The table needs about 1100 CSS px across. On a narrower touch screen (a tablet held upright, a
 * phone) lay the page out at that width and let the browser scale it to fit, instead of
 * reflowing the board; the lobby keeps the device width.
 */
export function useViewportFit() {
  useEffect(() => {
    const meta = document.querySelector<HTMLMetaElement>('meta[name="viewport"]');
    if (!meta || Math.min(screen.width, screen.height) >= TABLE_MIN_WIDTH || !matchMedia("(pointer: coarse)").matches) return;
    const before = meta.content;
    // Name the scale too: browsers keep the current zoom when only the layout width changes.
    const scale = Math.min(1, document.documentElement.clientWidth / TABLE_MIN_WIDTH).toFixed(3);
    meta.content = `width=${TABLE_MIN_WIDTH}, initial-scale=${scale}, minimum-scale=${scale}`;
    return () => {
      meta.content = before;
    };
  }, []);
}

/**
 * Table hotkeys. Esc calls `onEscape`; Space presses the dock's main button (pass / pass focus /
 * end turn); Enter confirms a complete pick; T taps every ready rune. They act through the DOM so
 * they always press exactly what the player could click.
 */
export function useTableHotkeys(onEscape: () => void) {
  const escape = useRef(onEscape);
  escape.current = onEscape;
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") escape.current();
      // Space presses the dock's main button (pass / pass focus / end turn), outside text fields
      // and never while a selection is half built.
      if (e.target instanceof HTMLInputElement || e.repeat) return;
      // Enter confirms a half-built selection once it is complete.
      if (e.key === "Enter") {
        const confirm = [...document.querySelectorAll<HTMLButtonElement>(".pickbar:not(.pickbar--leaving) button")].find((b) => b.textContent?.startsWith("确认") && !b.disabled);
        if (confirm) {
          e.preventDefault();
          confirm.click();
        }
      }
      // T taps every ready rune.
      if (e.key === "t" || e.key === "T") document.querySelector<HTMLButtonElement>(".dock__runes:not(:disabled)")?.click();
      if (e.code === "Space" && !document.querySelector(".pickbar, .menu, .runebar, .mulligan")) {
        const button = document.querySelector<HTMLButtonElement>('.dock [data-hotkey="space"]:not(:disabled)');
        if (button) {
          e.preventDefault();
          button.click();
        }
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);
}

/**
 * Where each of the opponent's board objects sits; when one arrives somewhere new (played,
 * moved, returned) it glows for a moment so the change is easy to spot. Returns the glowing ids.
 */
export function useOpponentArrivals(snapshot: Snapshot, meId: string) {
  const [recent, setRecent] = useState<Set<string>>(new Set());
  const oppPlaces = useRef<Map<string, string> | null>(null);
  useEffect(() => {
    const oppId = Object.keys(snapshot.players).find((id) => id !== meId);
    if (!oppId) return;
    const places = new Map<string, string>();
    for (const id of snapshot.players[oppId]?.zones.base ?? []) places.set(id, "base");
    for (const bf of snapshot.table?.battlefields ?? []) for (const id of bf.unitsBySide?.[oppId] ?? []) places.set(id, bf.battlefieldObjectId);
    const before = oppPlaces.current;
    oppPlaces.current = places;
    if (!before) return;
    const moved = [...places].filter(([id, at]) => before.get(id) !== at).map(([id]) => id);
    if (!moved.length) return;
    setRecent((cur) => new Set([...cur, ...moved]));
    // Not cancelled by the next snapshot: each arrival clears only its own glow.
    setTimeout(() => setRecent((cur) => new Set([...cur].filter((id) => !moved.includes(id)))), 2500);
  }, [snapshot, meId]);
  return recent;
}

/** The one orchestrated moment: announce whose turn just began. Returns the banner text, if showing. */
export function useTurnBanner(snapshot: Snapshot, meId: string) {
  const [banner, setBanner] = useState<string | null>(null);
  const lastTurn = useRef<string>("");
  useEffect(() => {
    const turnPlayer = snapshot.timing.turnPlayerId || snapshot.activePlayerId;
    const key = `${snapshot.turnNumber}:${turnPlayer}`;
    if (snapshot.timing.phase === "MULLIGAN" || lastTurn.current === key) return;
    const first = lastTurn.current === "";
    lastTurn.current = key;
    if (first && snapshot.turnNumber > 1) return;
    setBanner(turnPlayer === meId ? "你的回合" : "对手回合");
    if (turnPlayer === meId) cue("turn");
    // Short: a start-of-turn prompt often needs an answer right away and the strip sits over the fields.
    const t = setTimeout(() => setBanner(null), 1100);
    return () => clearTimeout(t);
  }, [snapshot.turnNumber, snapshot.activePlayerId, snapshot.timing.turnPlayerId, snapshot.timing.phase, meId]);
  return banner;
}

const AUTO_PASS_KEY = "riftbound.web.autoPass";

/** The player's auto-pass preference, remembered in this browser (on unless turned off). */
export function useAutoPassPreference() {
  const [autoPass, setAutoPass] = useState(() => {
    try {
      return localStorage.getItem(AUTO_PASS_KEY) !== "off";
    } catch {
      return true;
    }
  });
  const toggle = () => {
    const next = !autoPass;
    setAutoPass(next);
    try {
      localStorage.setItem(AUTO_PASS_KEY, next ? "on" : "off");
    } catch {
      /* preference simply isn't remembered */
    }
  };
  return [autoPass, toggle] as const;
}

/**
 * Auto-pass: in a priority or focus window where this player has nothing at all to respond with,
 * pass after a short beat. Any playable card or ability (`respondable`) or a half-built choice (`busy`) stops it.
 */
export function useAutoPass(enabled: boolean, prompt: Prompt | null, respondable: boolean, busy: boolean) {
  useEffect(() => {
    if (!enabled || !prompt?.actionable || busy) return;
    const type = prompt.view?.type;
    if (type !== "STACK_PRIORITY" && type !== "SPELL_DUEL_FOCUS") return;
    if (respondable) return;
    const passAction = enabledCandidate(prompt, "PASS_PRIORITY") ? "PASS_PRIORITY" : enabledCandidate(prompt, "PASS_FOCUS") ? "PASS_FOCUS" : null;
    if (!passAction) return;
    const t = setTimeout(() => {
      if (client.state.prompt?.promptId === prompt.promptId) void client.submit({ cmdType: passAction });
    }, 700);
    return () => clearTimeout(t);
  }, [enabled, prompt, respondable, busy]);
}

/**
 * The ambience bed plays only while the table is open and the game undecided (and only if the
 * player keeps it on); the closing cue plays once, when the result arrives during this visit.
 */
export function useResultSounds(winner: string | undefined | null, meId: string) {
  useEffect(() => {
    wantAmbience(!winner);
    return () => wantAmbience(false);
  }, [winner]);
  const seenWinner = useRef(winner);
  useEffect(() => {
    if (winner && !seenWinner.current) cue(winner === meId ? "win" : "loss");
    seenWinner.current = winner;
  }, [winner, meId]);
}

/** Anything that names a card (log lines, choice chips, panels) carries data-card-no and previews it on hover. */
export function useCardNameHover(hoverCard: HoverHandler, objects: Record<string, GameObject>) {
  const hoverRef = useRef(hoverCard);
  hoverRef.current = hoverCard;
  const objectsRef = useRef(objects);
  objectsRef.current = objects;
  useEffect(() => {
    const over = (e: globalThis.MouseEvent) => {
      const el = (e.target as Element | null)?.closest?.("[data-card-no]") as HTMLElement | null;
      if (!el?.dataset.cardNo) return;
      const object = el.dataset.objectId ? objectsRef.current[el.dataset.objectId] : undefined;
      hoverRef.current(el.dataset.cardNo, object, el.getBoundingClientRect());
    };
    document.addEventListener("mouseover", over);
    return () => document.removeEventListener("mouseover", over);
  }, []);
}

/** A hovered card can move or vanish without a mouseleave; drop the preview once nothing is under the pointer. */
export function useHoverAutoClear(hovering: boolean, clear: () => void) {
  const clearRef = useRef(clear);
  clearRef.current = clear;
  useEffect(() => {
    if (!hovering) return;
    let frame = 0;
    const check = (e: PointerEvent) => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => {
        const el = document.elementFromPoint(e.clientX, e.clientY);
        if (!el?.closest(".card, .field__card, .obj__attachment, .stack li, [data-card-no]")) clearRef.current();
      });
    };
    window.addEventListener("pointermove", check);
    window.addEventListener("pointerdown", check);
    return () => {
      cancelAnimationFrame(frame);
      window.removeEventListener("pointermove", check);
      window.removeEventListener("pointerdown", check);
    };
  }, [hovering]);
}

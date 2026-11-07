import { useCallback, useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from "react";

export type Drag = { id: string; kind: "hand" | "unit"; x: number; y: number; ox: number; oy: number; active: boolean };

const DRAG_THRESHOLD = 6;

/** Ids under a screen point that a drop can mean: drop zones first, then cards. */
export function dropIdsAt(x: number, y: number): string[] {
  const ids: string[] = [];
  for (const el of document.elementsFromPoint(x, y)) {
    const h = el as HTMLElement;
    if (h.dataset?.drop) {
      ids.push(h.dataset.drop);
      if (h.dataset.drop === "BASE" && h.dataset.dropOwner) ids.push(`BASE:${h.dataset.dropOwner}`);
      if (h.dataset.drop.startsWith("BATTLEFIELD:")) ids.push(h.dataset.drop.slice(12));
    }
    if (h.dataset?.oid) ids.push(h.dataset.oid);
  }
  return [...new Set(ids)];
}

/**
 * Dragging a hand card or a unit. A press only becomes a drag after the pointer travels a few
 * pixels, so plain clicks still work; the click that follows a real drag is swallowed
 * (`suppressClick`). `onDrop` gets the drag and the ids under the release point, and always runs
 * as of the latest render: auto-pay quotes and prompts change after the drag starts.
 */
export function useCardDrag(onDrop: (drag: Drag, ids: string[], x: number, y: number) => void, onActive: () => void) {
  const [drag, setDrag] = useState<Drag | null>(null);
  const dragRef = useRef<Drag | null>(null);
  dragRef.current = drag;
  const suppressClick = useRef(false);
  const latest = useRef({ onDrop, onActive });
  latest.current = { onDrop, onActive };

  const begin = (id: string, kind: Drag["kind"], e: ReactPointerEvent) => {
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    setDrag({ id, kind, x: e.clientX, y: e.clientY, ox: e.clientX - rect.left, oy: e.clientY - rect.top, active: false });
  };

  const finish = useCallback((x: number, y: number) => {
    const d = dragRef.current;
    setDrag(null);
    if (!d?.active) return;
    suppressClick.current = true;
    setTimeout(() => (suppressClick.current = false), 0);
    latest.current.onDrop(d, dropIdsAt(x, y), x, y);
  }, []);

  useEffect(() => {
    if (!drag) return;
    const move = (e: PointerEvent) => {
      setDrag((cur) => {
        if (!cur) return cur;
        const active = cur.active || Math.hypot(e.clientX - cur.x, e.clientY - cur.y) > DRAG_THRESHOLD;
        return { ...cur, x: active ? e.clientX : cur.x, y: active ? e.clientY : cur.y, active };
      });
      if (dragRef.current?.active) latest.current.onActive();
    };
    const up = (e: PointerEvent) => finish(e.clientX, e.clientY);
    window.addEventListener("pointermove", move);
    window.addEventListener("pointerup", up, { once: true });
    return () => {
      window.removeEventListener("pointermove", move);
      window.removeEventListener("pointerup", up);
    };
  }, [drag?.id, finish]); // eslint-disable-line react-hooks/exhaustive-deps

  return { drag, dragRef, suppressClick, begin };
}

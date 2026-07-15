import { type CSSProperties, type ReactNode, useLayoutEffect, useMemo, useRef, useState } from "react";
import type { ArenaPromptPresentationPlan } from "../../utils/arenaPromptPresentation";
import {
  chooseArenaActionPlacement,
  type ArenaActionPlacementName,
  type ArenaActionRect
} from "../../utils/arenaActionPlacement";

export type ArenaActionLayerProps = {
  children: ReactNode;
  label: string;
  plan: ArenaPromptPresentationPlan;
  protectedObjectIds?: string[];
};

type ArenaActionStyle = CSSProperties & {
  "--arena-action-x"?: string;
  "--arena-action-y"?: string;
  "--arena-action-translate-x"?: string;
  "--arena-action-translate-y"?: string;
};

type ArenaActionPosition = {
  anchorLeft: number;
  anchorTop: number;
  placement: ArenaActionPlacementName;
  protectedOverlapArea: number;
  protectedObjectCount: number;
  style: ArenaActionStyle;
};

export function ArenaActionLayer({ children, label, plan, protectedObjectIds = [] }: ArenaActionLayerProps) {
  const layerRef = useRef<HTMLElement>(null);
  const [position, setPosition] = useState<ArenaActionPosition>();
  const protectedObjectKey = useMemo(
    () => [...new Set(protectedObjectIds)].sort().join("\u0000"),
    [protectedObjectIds]
  );

  useLayoutEffect(() => {
    if (plan.mode !== "context" || !plan.anchorObjectId) {
      setPosition(undefined);
      return;
    }

    const anchorObjectId = plan.anchorObjectId;
    const protectedIdSet = new Set(protectedObjectKey.split("\u0000").filter(Boolean));
    let animationFrame = 0;
    const syncAnchor = () => {
      const layer = layerRef.current;
      const host = layer?.parentElement;
      const visibleObjects = [...document.querySelectorAll<HTMLElement>("[data-object-id]")]
        .filter(isVisible);
      const object = bestVisibleObject(visibleObjects, anchorObjectId, host);
      if (!host || !object) {
        setPosition(undefined);
        return;
      }

      const hostRect = host.getBoundingClientRect();
      const objectRect = object.getBoundingClientRect();
      const layerRect = layer.getBoundingClientRect();
      const protectedElements = visibleObjects.filter((candidate) => protectedIdSet.has(candidate.dataset.objectId ?? ""));
      const placement = chooseArenaActionPlacement({
        anchor: rectValue(objectRect),
        host: rectValue(hostRect),
        panelHeight: layerRect.height,
        panelWidth: Math.min(600, layerRect.width || 600),
        protectedRects: protectedElements.map((candidate) => rectValue(candidate.getBoundingClientRect()))
      });
      const nextPosition: ArenaActionPosition = {
        anchorLeft: objectRect.left - hostRect.left,
        anchorTop: objectRect.top - hostRect.top,
        placement: placement.placement,
        protectedObjectCount: protectedElements.length,
        protectedOverlapArea: placement.protectedOverlapArea,
        style: {
          "--arena-action-x": `${placement.left}px`,
          "--arena-action-y": `${placement.top}px`,
          "--arena-action-translate-x": "0%",
          "--arena-action-translate-y": "0%"
        }
      };
      setPosition((current) => samePosition(current, nextPosition) ? current : nextPosition);
    };
    const scheduleSync = () => {
      window.cancelAnimationFrame(animationFrame);
      animationFrame = window.requestAnimationFrame(syncAnchor);
    };

    syncAnchor();
    window.addEventListener("resize", scheduleSync);
    document.addEventListener("scroll", scheduleSync, true);
    window.visualViewport?.addEventListener("resize", scheduleSync);
    window.visualViewport?.addEventListener("scroll", scheduleSync);

    const resizeObserver = typeof ResizeObserver === "undefined" ? undefined : new ResizeObserver(scheduleSync);
    if (resizeObserver) {
      const host = layerRef.current?.parentElement;
      if (host) resizeObserver.observe(host);
      if (layerRef.current) resizeObserver.observe(layerRef.current);
      for (const object of document.querySelectorAll<HTMLElement>("[data-object-id]")) {
        if (object.dataset.objectId === anchorObjectId || protectedIdSet.has(object.dataset.objectId ?? "")) {
          resizeObserver.observe(object);
        }
      }
    }

    return () => {
      window.cancelAnimationFrame(animationFrame);
      window.removeEventListener("resize", scheduleSync);
      document.removeEventListener("scroll", scheduleSync, true);
      window.visualViewport?.removeEventListener("resize", scheduleSync);
      window.visualViewport?.removeEventListener("scroll", scheduleSync);
      resizeObserver?.disconnect();
    };
  }, [plan.anchorObjectId, plan.mode, protectedObjectKey]);

  if (plan.mode === "hidden") {
    return null;
  }

  return (
    <section
      aria-label={label}
      aria-modal={plan.mode === "modal" ? true : undefined}
      className={`arena-prompt-layer is-${plan.mode}`}
      data-arena-action-anchor={plan.anchorObjectId ?? "fallback"}
      data-arena-action-anchor-x={Math.round(position?.anchorLeft ?? 0)}
      data-arena-action-anchor-y={Math.round(position?.anchorTop ?? 0)}
      data-arena-action-mode={plan.mode}
      data-arena-action-placement={position?.placement ?? "fallback"}
      data-arena-action-protected-count={position?.protectedObjectCount ?? 0}
      data-arena-action-protected-overlap={Math.round(position?.protectedOverlapArea ?? 0)}
      ref={layerRef}
      role={plan.mode === "modal" ? "dialog" : "region"}
      style={position?.style}
    >
      {children}
    </section>
  );
}

function bestVisibleObject(objects: HTMLElement[], objectId: string, host?: HTMLElement | null): HTMLElement | undefined {
  const hostRect = host?.getBoundingClientRect();
  return objects
    .filter((candidate) => candidate.dataset.objectId === objectId)
    .map((candidate) => ({
      candidate,
      visibleArea: hostRect ? intersectingArea(candidate.getBoundingClientRect(), hostRect) : 1
    }))
    .sort((left, right) => right.visibleArea - left.visibleArea)[0]?.candidate;
}

function intersectingArea(first: DOMRect, second: DOMRect): number {
  return Math.max(0, Math.min(first.right, second.right) - Math.max(first.left, second.left))
    * Math.max(0, Math.min(first.bottom, second.bottom) - Math.max(first.top, second.top));
}

function isVisible(element: HTMLElement): boolean {
  const rect = element.getBoundingClientRect();
  const style = window.getComputedStyle(element);
  return rect.width > 0
    && rect.height > 0
    && style.display !== "none"
    && style.visibility !== "hidden";
}

function rectValue(rect: DOMRect): ArenaActionRect {
  return {
    bottom: rect.bottom,
    height: rect.height,
    left: rect.left,
    right: rect.right,
    top: rect.top,
    width: rect.width
  };
}

function samePosition(current: ArenaActionPosition | undefined, next: ArenaActionPosition): boolean {
  return Math.round(current?.anchorLeft ?? -1) === Math.round(next.anchorLeft)
    && Math.round(current?.anchorTop ?? -1) === Math.round(next.anchorTop)
    && current?.placement === next.placement
    && current.protectedObjectCount === next.protectedObjectCount
    && Math.round(current.protectedOverlapArea) === Math.round(next.protectedOverlapArea)
    && current.style["--arena-action-x"] === next.style["--arena-action-x"]
    && current.style["--arena-action-y"] === next.style["--arena-action-y"];
}

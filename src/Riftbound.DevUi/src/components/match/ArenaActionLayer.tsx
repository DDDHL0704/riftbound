import { type CSSProperties, type ReactNode, useLayoutEffect, useRef, useState } from "react";
import type { ArenaPromptPresentationPlan } from "../../utils/arenaPromptPresentation";

export type ArenaActionLayerProps = {
  children: ReactNode;
  label: string;
  plan: ArenaPromptPresentationPlan;
};

type ArenaActionStyle = CSSProperties & {
  "--arena-action-x"?: string;
  "--arena-action-y"?: string;
  "--arena-action-translate-y"?: string;
};

export function ArenaActionLayer({ children, label, plan }: ArenaActionLayerProps) {
  const layerRef = useRef<HTMLElement>(null);
  const [anchorStyle, setAnchorStyle] = useState<ArenaActionStyle>();

  useLayoutEffect(() => {
    if (plan.mode !== "context" || !plan.anchorObjectId) {
      setAnchorStyle(undefined);
      return;
    }

    const syncAnchor = () => {
      const layer = layerRef.current;
      const host = layer?.parentElement;
      const object = [...document.querySelectorAll<HTMLElement>("[data-object-id]")]
        .find((candidate) => candidate.dataset.objectId === plan.anchorObjectId && isVisible(candidate));
      if (!host || !object) {
        setAnchorStyle(undefined);
        return;
      }

      const hostRect = host.getBoundingClientRect();
      const objectRect = object.getBoundingClientRect();
      const openAbove = objectRect.top + objectRect.height / 2 > hostRect.top + hostRect.height / 2;
      const panelWidth = Math.min(600, Math.max(0, hostRect.width - 24));
      const desiredX = objectRect.left - hostRect.left + objectRect.width / 2;
      const minimumX = panelWidth / 2 + 12;
      const maximumX = hostRect.width - panelWidth / 2 - 12;
      const objectOnRight = desiredX >= hostRect.width / 2;
      // Keep the next battlefield target visible by docking the tray opposite the selected card.
      const clampedX = maximumX >= minimumX
        ? (objectOnRight ? minimumX : maximumX)
        : hostRect.width / 2;
      setAnchorStyle({
        "--arena-action-x": `${clampedX}px`,
        "--arena-action-y": `${(openAbove ? objectRect.top : objectRect.bottom) - hostRect.top + (openAbove ? -10 : 10)}px`,
        "--arena-action-translate-y": openAbove ? "-100%" : "0%"
      });
    };

    syncAnchor();
    window.addEventListener("resize", syncAnchor);
    return () => window.removeEventListener("resize", syncAnchor);
  }, [plan.anchorObjectId, plan.mode]);

  if (plan.mode === "hidden") {
    return null;
  }

  return (
    <section
      aria-label={label}
      aria-modal={plan.mode === "modal" ? true : undefined}
      className={`arena-prompt-layer is-${plan.mode}`}
      data-arena-action-anchor={plan.anchorObjectId ?? "fallback"}
      data-arena-action-mode={plan.mode}
      ref={layerRef}
      role={plan.mode === "modal" ? "dialog" : "region"}
      style={anchorStyle}
    >
      {children}
    </section>
  );
}

function isVisible(element: HTMLElement): boolean {
  return element.offsetParent !== null && element.getClientRects().length > 0;
}

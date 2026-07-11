import { useCallback, useRef, useState, type CSSProperties, type ReactNode } from "react";

export type ArenaTableProps = {
  battlefield: ReactNode;
  opponentBackdrop?: string;
  opponentEdge: ReactNode;
  opponentHand: ReactNode;
  selfBackdrop?: string;
  selfEdge: ReactNode;
  selfHand: ReactNode;
};

type ArenaBackdropStyle = CSSProperties & { "--arena-backdrop-image"?: string };

export function ArenaTable({
  battlefield,
  opponentBackdrop,
  opponentEdge,
  opponentHand,
  selfBackdrop,
  selfEdge,
  selfHand
}: ArenaTableProps) {
  const battlefieldRef = useRef<HTMLDivElement>(null);
  const [activeLane, setActiveLane] = useState<"left" | "right">("left");
  const selectLane = useCallback((lane: "left" | "right") => {
    const battlefieldRegion = battlefieldRef.current;
    if (!battlefieldRegion) {
      return;
    }

    const maxScroll = Math.max(0, battlefieldRegion.scrollWidth - battlefieldRegion.clientWidth);
    battlefieldRegion.scrollTo({
      behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth",
      left: lane === "left" ? 0 : maxScroll
    });
    setActiveLane(lane);
  }, []);
  const syncActiveLane = useCallback(() => {
    const battlefieldRegion = battlefieldRef.current;
    if (!battlefieldRegion) {
      return;
    }

    const maxScroll = Math.max(0, battlefieldRegion.scrollWidth - battlefieldRegion.clientWidth);
    setActiveLane(maxScroll > 0 && battlefieldRegion.scrollLeft > maxScroll / 2 ? "right" : "left");
  }, []);

  return (
    <section className="arena-table" data-arena-table>
      <div aria-hidden="true" className="arena-backdrop is-opponent" style={backdropStyle(opponentBackdrop)} />
      <div aria-hidden="true" className="arena-backdrop is-self" style={backdropStyle(selfBackdrop)} />
      <div className="arena-edge is-opponent" data-arena-slot="opponent-edge">{opponentEdge}</div>
      <div className="arena-hand is-opponent" data-arena-slot="opponent-hand">{opponentHand}</div>
      <div
        aria-label="公共战场"
        className="arena-battlefield"
        data-arena-battlefield-active-lane={activeLane}
        data-arena-battlefield-region
        data-arena-slot="battlefield"
        onScroll={syncActiveLane}
        ref={battlefieldRef}
        role="region"
        tabIndex={0}
      >
        <span className="arena-battlefield-accessible-label">公共战场</span>
        <div aria-label="选择战场" className="arena-battlefield-tabs" role="tablist">
          {(["left", "right"] as const).map((lane) => (
            <button
              aria-selected={activeLane === lane}
              data-arena-battlefield-lane-control={lane}
              key={lane}
              onClick={() => selectLane(lane)}
              role="tab"
              type="button"
            >
              {lane === "left" ? "左战场" : "右战场"}
            </button>
          ))}
        </div>
        {battlefield}
      </div>
      <div className="arena-edge is-self" data-arena-slot="self-edge">{selfEdge}</div>
      <div className="arena-hand is-self" data-arena-hand data-arena-slot="self-hand">{selfHand}</div>
    </section>
  );
}

function backdropStyle(image?: string): ArenaBackdropStyle | undefined {
  return image ? { "--arena-backdrop-image": `url("${image}")` } : undefined;
}

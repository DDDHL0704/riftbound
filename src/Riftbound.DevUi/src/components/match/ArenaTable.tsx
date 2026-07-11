import type { CSSProperties, ReactNode } from "react";

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
  return (
    <section className="arena-table" data-arena-table>
      <div aria-hidden="true" className="arena-backdrop is-opponent" style={backdropStyle(opponentBackdrop)} />
      <div aria-hidden="true" className="arena-backdrop is-self" style={backdropStyle(selfBackdrop)} />
      <div className="arena-edge is-opponent" data-arena-slot="opponent-edge">{opponentEdge}</div>
      <div className="arena-hand is-opponent" data-arena-slot="opponent-hand">{opponentHand}</div>
      <div className="arena-battlefield" data-arena-battlefield-region data-arena-slot="battlefield">{battlefield}</div>
      <div className="arena-edge is-self" data-arena-slot="self-edge">{selfEdge}</div>
      <div className="arena-hand is-self" data-arena-hand data-arena-slot="self-hand">{selfHand}</div>
    </section>
  );
}

function backdropStyle(image?: string): ArenaBackdropStyle | undefined {
  return image ? { "--arena-backdrop-image": `url("${image}")` } : undefined;
}

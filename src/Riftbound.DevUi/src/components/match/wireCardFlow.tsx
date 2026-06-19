import { type CSSProperties } from "react";
import { CardFace, type InspectedCard } from "../cards/CardFace";
import { BehaviorSpec } from "../../types/catalog";
import { CardObjectView } from "../../types/protocol";
import type { PromptObjectState } from "../../utils/promptInteraction";

export type WireCardFlowKind = "battlefield-unit" | "base" | "hand" | "signature";

export type WireCardFlowPlan = {
  cardHeight: number;
  cardWidth: number;
  density: "single" | "sparse" | "normal" | "dense" | "packed";
  gap: number;
  itemCount: number;
  kind: WireCardFlowKind;
  layout: "grid" | "rail";
  minSlots: number;
  slotCount: number;
};

type WireCardFlowProps = {
  className?: string;
  emptyLabel?: string;
  interactionByObjectId?: Record<string, PromptObjectState | undefined>;
  ids: string[];
  kind: WireCardFlowKind;
  minSlots?: number;
  objects: Record<string, CardObjectView | undefined>;
  onInspectCard: (card: InspectedCard) => void;
  onPreviewCard?: (card?: InspectedCard) => void;
  plan?: WireCardFlowPlan;
  renderEmptySlots?: boolean;
  selectedObjectId?: string;
  specs: Record<string, BehaviorSpec>;
};

type WireCssProperties = CSSProperties & Record<`--${string}`, string | number>;

const CARD_RATIO = 744 / 1039;

export function buildWireCardFlowPlan({
  itemCount,
  kind,
  minSlots = 0
}: {
  itemCount: number;
  kind: WireCardFlowKind;
  minSlots?: number;
}): WireCardFlowPlan {
  const effectiveCount = Math.max(itemCount, minSlots);
  const slotCount = Math.max(itemCount, minSlots);

  if (kind === "signature") {
    return plan(kind, itemCount, minSlots, slotCount, "single", 100, "grid", 4);
  }

  if (kind === "battlefield-unit") {
    if (effectiveCount <= 3) {
      return plan(kind, itemCount, minSlots, slotCount, "sparse", 74, "rail", 4);
    }
    if (effectiveCount <= 5) {
      return plan(kind, itemCount, minSlots, slotCount, "normal", 68, "rail", 4);
    }
    if (effectiveCount <= 8) {
      return plan(kind, itemCount, minSlots, slotCount, "dense", 58, "rail", 4);
    }
    if (effectiveCount <= 12) {
      return plan(kind, itemCount, minSlots, slotCount, "packed", 48, "rail", 3);
    }
    return plan(kind, itemCount, minSlots, slotCount, "packed", 42, "rail", 3);
  }

  if (kind === "base") {
    if (effectiveCount <= 3) {
      return plan(kind, itemCount, minSlots, slotCount, "sparse", 86, "rail", 4);
    }
    if (effectiveCount <= 6) {
      return plan(kind, itemCount, minSlots, slotCount, "normal", 74, "rail", 4);
    }
    if (effectiveCount <= 10) {
      return plan(kind, itemCount, minSlots, slotCount, "dense", 62, "rail", 3);
    }
    return plan(kind, itemCount, minSlots, slotCount, "packed", 52, "rail", 3);
  }

  if (effectiveCount <= 5) {
    return plan(kind, itemCount, minSlots, slotCount, "sparse", 86, "rail", 4);
  }
  if (effectiveCount <= 8) {
    return plan(kind, itemCount, minSlots, slotCount, "normal", 74, "rail", 4);
  }
  if (effectiveCount <= 12) {
    return plan(kind, itemCount, minSlots, slotCount, "dense", 62, "rail", 3);
  }
  return plan(kind, itemCount, minSlots, slotCount, "packed", 52, "rail", 3);
}

export function WireCardFlow({
  className = "",
  emptyLabel = "",
  interactionByObjectId,
  ids,
  kind,
  minSlots = 0,
  objects,
  onInspectCard,
  onPreviewCard,
  plan: providedPlan,
  renderEmptySlots = false,
  selectedObjectId,
  specs
}: WireCardFlowProps) {
  const flowPlan = providedPlan ?? buildWireCardFlowPlan({ itemCount: ids.length, kind, minSlots });
  const slotCount = renderEmptySlots ? Math.max(flowPlan.slotCount, ids.length, minSlots) : ids.length;
  const slots = renderEmptySlots ? Array.from({ length: slotCount }, (_, index) => ids[index]) : ids;

  return (
    <div
      className={`wire-card-flow wire-card-flow-${kind} wire-card-flow-${flowPlan.layout} wire-flow-${flowPlan.density} ${className}`.trim()}
      data-flow-count={ids.length}
      data-flow-kind={kind}
      data-flow-slots={slotCount}
      style={wireCardFlowStyle(flowPlan)}
    >
      {ids.length === 0 && !renderEmptySlots && <WireEmpty label={emptyLabel} />}
      {slots.map((id, index) => {
        if (!id) {
          return renderEmptySlots ? <WireCardSlot key={`empty-${kind}-${index}`} label={emptyLabel} /> : null;
        }

        const object = objects[id] ?? hiddenObject(id);
        return (
          <CardFace
            compact
            interactionState={interactionByObjectId?.[id]}
            key={id}
            object={object}
            objectId={id}
            onInspect={onInspectCard}
            onPreview={onPreviewCard}
            selected={selectedObjectId === id}
            spec={object.cardNo ? specs[object.cardNo] : undefined}
          />
        );
      })}
    </div>
  );
}

export function WireCardSlot({ label }: { label: string }) {
  return <div className="wire-card-slot" aria-hidden="true" data-empty-label={label || "卡牌"} />;
}

export function WirePublicPile({
  ids,
  interactionByObjectId,
  label,
  objects,
  onInspectCard,
  onPreviewCard,
  selectedObjectId,
  specs
}: {
  ids: string[];
  interactionByObjectId?: Record<string, PromptObjectState | undefined>;
  label: string;
  objects: Record<string, CardObjectView | undefined>;
  onInspectCard: (card: InspectedCard) => void;
  onPreviewCard?: (card?: InspectedCard) => void;
  selectedObjectId?: string;
  specs: Record<string, BehaviorSpec>;
}) {
  const topId = ids.at(-1);
  const topObject = topId ? objects[topId] : undefined;

  return (
    <div className="wire-stack-count wire-fixed-pile" role="group" aria-label={`${label} ${ids.length} 张`}>
      {topId ? (
        <CardFace
          compact
          interactionState={topId ? interactionByObjectId?.[topId] : undefined}
          object={topObject ?? hiddenObject(topId)}
          objectId={topId}
          onInspect={onInspectCard}
          onPreview={onPreviewCard}
          selected={selectedObjectId === topId}
          spec={topObject?.cardNo ? specs[topObject.cardNo] : undefined}
        />
      ) : (
        <div className="wire-stack-box" aria-hidden="true" />
      )}
    </div>
  );
}

export function WireStackCount({ count, label }: { count: number; label: string }) {
  return (
    <div className="wire-stack-count" role="group" aria-label={`${label} ${count} 张`}>
      <div className="wire-stack-box" aria-hidden="true" />
    </div>
  );
}

export function WireEmpty({ label }: { label: string }) {
  return <span className="wire-empty">{label}</span>;
}

function plan(
  kind: WireCardFlowKind,
  itemCount: number,
  minSlots: number,
  slotCount: number,
  density: WireCardFlowPlan["density"],
  cardWidth: number,
  layout: WireCardFlowPlan["layout"],
  gap: number
): WireCardFlowPlan {
  return {
    cardHeight: Math.round(cardWidth / CARD_RATIO),
    cardWidth,
    density,
    gap,
    itemCount,
    kind,
    layout,
    minSlots,
    slotCount
  };
}

function wireCardFlowStyle(flowPlan: WireCardFlowPlan): WireCssProperties {
  return {
    "--wire-card-h": `${flowPlan.cardHeight}px`,
    "--wire-card-w": `${flowPlan.cardWidth}px`,
    "--wire-flow-gap": `${flowPlan.gap}px`
  };
}

function hiddenObject(objectId: string): CardObjectView {
  return { objectId, isFaceDown: true };
}

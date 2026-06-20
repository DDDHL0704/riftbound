import { type CSSProperties } from "react";
import { CardFace, type InspectedCard } from "../cards/CardFace";
import { BehaviorSpec } from "../../types/catalog";
import { CardObjectView } from "../../types/protocol";
import type { PromptObjectState } from "../../utils/promptInteraction";
import {
  buildWireCardFlowPlan,
  type WireCardFlowKind,
  type WireCardFlowPlan
} from "./wireCardFlowPlan";
import { buildWirePilePlan, type WirePileKind, type WirePilePlan } from "./wirePilePlan";

export { buildWireCardFlowPlan, type WireCardFlowKind, type WireCardFlowPlan } from "./wireCardFlowPlan";
export { buildWirePilePlan, type WirePileKind, type WirePilePlan } from "./wirePilePlan";

export type WireTimelineObjectState = "event" | "rule";

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
  timelineByObjectId?: Record<string, WireTimelineObjectState | undefined>;
};

type WireCssProperties = CSSProperties & Record<`--${string}`, string | number>;

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
  specs,
  timelineByObjectId
}: WireCardFlowProps) {
  const sizingPlan = providedPlan ?? buildWireCardFlowPlan({ itemCount: ids.length, kind, minSlots });
  const slotCount = renderEmptySlots ? Math.max(sizingPlan.slotCount, ids.length, minSlots) : ids.length;
  const flowPlan = renderedFlowPlan(sizingPlan, ids.length, minSlots, slotCount);
  const slots = renderEmptySlots ? Array.from({ length: slotCount }, (_, index) => ids[index]) : ids;

  return (
    <div
      className={`wire-card-flow wire-card-flow-${kind} wire-card-flow-${flowPlan.layout} wire-flow-${flowPlan.density} ${className}`.trim()}
      data-flow-capacity={String(flowPlan.capacity)}
      data-flow-card-height={flowPlan.cardHeight}
      data-flow-card-width={flowPlan.cardWidth}
      data-flow-count={ids.length}
      data-flow-density={flowPlan.density}
      data-flow-fit={flowPlan.fit}
      data-flow-kind={kind}
      data-flow-layout={flowPlan.layout}
      data-flow-min-slots={flowPlan.minSlots}
      data-flow-overflow={flowPlan.overflow}
      data-flow-overflow-count={flowPlan.overflowCount}
      data-flow-scroll-after={flowPlan.scrollAfter}
      data-flow-slots={slotCount}
      data-flow-visible-slots={flowPlan.visibleSlotCount}
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
            timelineState={timelineByObjectId?.[id]}
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
  kind,
  label,
  objects,
  onInspectCard,
  onPreviewCard,
  selectedObjectId,
  specs,
  timelineByObjectId
}: {
  ids: string[];
  interactionByObjectId?: Record<string, PromptObjectState | undefined>;
  kind: Extract<WirePileKind, "banished" | "graveyard">;
  label: string;
  objects: Record<string, CardObjectView | undefined>;
  onInspectCard: (card: InspectedCard) => void;
  onPreviewCard?: (card?: InspectedCard) => void;
  selectedObjectId?: string;
  specs: Record<string, BehaviorSpec>;
  timelineByObjectId?: Record<string, WireTimelineObjectState | undefined>;
}) {
  const pilePlan = buildWirePilePlan({ ids, kind });
  const topId = pilePlan.topObjectId;
  const topObject = topId ? objects[topId] : undefined;

  return (
    <div className="wire-stack-count wire-fixed-pile" role="group" aria-label={`${label} ${pilePlan.count} 张`} {...wirePileDataAttrs(pilePlan)}>
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
          timelineState={topId ? timelineByObjectId?.[topId] : undefined}
        />
      ) : (
        <div className="wire-stack-box" aria-hidden="true" />
      )}
    </div>
  );
}

export function WireStackCount({ count, kind, label }: { count: number; kind: Extract<WirePileKind, "library" | "runeDeck">; label: string }) {
  const pilePlan = buildWirePilePlan({ count, kind });
  return (
    <div className="wire-stack-count" role="group" aria-label={`${label} ${pilePlan.count} 张`} {...wirePileDataAttrs(pilePlan)}>
      <div className="wire-stack-box" aria-hidden="true" />
    </div>
  );
}

export function WireEmpty({ label }: { label: string }) {
  return <span className="wire-empty">{label}</span>;
}

function wireCardFlowStyle(flowPlan: WireCardFlowPlan): WireCssProperties {
  return {
    "--wire-card-h": `${flowPlan.cardHeight}px`,
    "--wire-card-w": `${flowPlan.cardWidth}px`,
    "--wire-flow-gap": `${flowPlan.gap}px`,
    "--wire-flow-visible-slots": flowPlan.visibleSlotCount
  };
}

function renderedFlowPlan(
  sizingPlan: WireCardFlowPlan,
  itemCount: number,
  minSlots: number,
  slotCount: number
): WireCardFlowPlan {
  const visibleSlotCount = Math.min(slotCount, sizingPlan.scrollAfter);
  const overflowCount = Math.max(0, slotCount - visibleSlotCount);
  const overflow = overflowCount > 0 ? "scroll" : "none";
  return {
    ...sizingPlan,
    fit: overflow === "scroll" ? "overflow-rail" : sizingPlan.layout === "rail" ? "elastic-rail" : "fixed-slot",
    itemCount,
    minSlots,
    overflow,
    overflowCount,
    slotCount,
    visibleSlotCount
  };
}

function wirePileDataAttrs(pilePlan: WirePilePlan) {
  return {
    "data-wire-pile-capacity": pilePlan.capacity,
    "data-wire-pile-count": pilePlan.count,
    "data-wire-pile-face": pilePlan.face,
    "data-wire-pile-kind": pilePlan.kind,
    "data-wire-pile-overflow-count": pilePlan.overflowCount,
    "data-wire-pile-top-object-id": pilePlan.topObjectId ?? "",
    "data-wire-pile-visible-count": pilePlan.visibleCount
  };
}

function hiddenObject(objectId: string): CardObjectView {
  return { objectId, isFaceDown: true };
}

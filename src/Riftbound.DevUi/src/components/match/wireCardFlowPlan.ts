import {
  WIRE_CARD_IMAGE_RATIO,
  WIRE_RAIL_VISIBLE_SLOT_LIMITS,
  WIRE_SIGNATURE_CARD_CAPACITY,
  WIRE_UNBOUNDED_CAPACITY,
  type WireTableFlowKind,
  type WireTableRailFlowKind
} from "./wireTableContract";

export type WireCardFlowKind = WireTableFlowKind;
export type WireCardFlowDensity = "single" | "sparse" | "normal" | "dense" | "packed";
export type WireCardFlowFit = "fixed-slot" | "elastic-rail" | "overflow-rail";
export type WireCardFlowLayout = "grid" | "rail";
export type WireCardFlowOverflow = "none" | "scroll";

export type WireCardFlowPlan = {
  capacity: number | "unbounded";
  cardHeight: number;
  cardWidth: number;
  density: WireCardFlowDensity;
  fit: WireCardFlowFit;
  gap: number;
  itemCount: number;
  kind: WireCardFlowKind;
  layout: WireCardFlowLayout;
  minSlots: number;
  overflow: WireCardFlowOverflow;
  overflowCount: number;
  scrollAfter: number;
  slotCount: number;
  visibleSlotCount: number;
};

type WireCardFlowStep = {
  cardWidth: number;
  density: WireCardFlowDensity;
  gap: number;
  maxCount: number;
  scrollAfter: number;
};

const RAIL_FLOW_STRATEGIES: Record<WireTableRailFlowKind, WireCardFlowStep[]> = {
  "battlefield-unit": [
    step(3, "sparse", 74, 4, WIRE_RAIL_VISIBLE_SLOT_LIMITS["battlefield-unit"]),
    step(5, "normal", 68, 4, WIRE_RAIL_VISIBLE_SLOT_LIMITS["battlefield-unit"]),
    step(8, "dense", 58, 4, WIRE_RAIL_VISIBLE_SLOT_LIMITS["battlefield-unit"]),
    step(12, "packed", 48, 3, WIRE_RAIL_VISIBLE_SLOT_LIMITS["battlefield-unit"]),
    step(Number.POSITIVE_INFINITY, "packed", 42, 3, WIRE_RAIL_VISIBLE_SLOT_LIMITS["battlefield-unit"])
  ],
  base: [
    step(3, "sparse", 86, 4, WIRE_RAIL_VISIBLE_SLOT_LIMITS.base),
    step(6, "normal", 74, 4, WIRE_RAIL_VISIBLE_SLOT_LIMITS.base),
    step(10, "dense", 62, 3, WIRE_RAIL_VISIBLE_SLOT_LIMITS.base),
    step(Number.POSITIVE_INFINITY, "packed", 52, 3, WIRE_RAIL_VISIBLE_SLOT_LIMITS.base)
  ],
  hand: [
    step(5, "sparse", 86, 4, WIRE_RAIL_VISIBLE_SLOT_LIMITS.hand),
    step(8, "normal", 74, 4, WIRE_RAIL_VISIBLE_SLOT_LIMITS.hand),
    step(12, "dense", 62, 3, WIRE_RAIL_VISIBLE_SLOT_LIMITS.hand),
    step(Number.POSITIVE_INFINITY, "packed", 52, 3, WIRE_RAIL_VISIBLE_SLOT_LIMITS.hand)
  ],
  standby: [
    step(2, "sparse", 58, 4, WIRE_RAIL_VISIBLE_SLOT_LIMITS.standby),
    step(4, "normal", 52, 4, WIRE_RAIL_VISIBLE_SLOT_LIMITS.standby),
    step(8, "dense", 46, 3, WIRE_RAIL_VISIBLE_SLOT_LIMITS.standby),
    step(Number.POSITIVE_INFINITY, "packed", 40, 3, WIRE_RAIL_VISIBLE_SLOT_LIMITS.standby)
  ]
};

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
    return plan({
      capacity: WIRE_SIGNATURE_CARD_CAPACITY,
      cardWidth: 100,
      density: "single",
      gap: 4,
      itemCount,
      kind,
      layout: "grid",
      minSlots,
      scrollAfter: 1,
      slotCount
    });
  }

  const strategy = selectRailStrategy(RAIL_FLOW_STRATEGIES[kind], effectiveCount);
  return railPlan(kind, itemCount, minSlots, slotCount, strategy);
}

export function resolveWireCardFlowRenderPlan({
  itemCount,
  minSlots = 0,
  sizingPlan,
  slotCount = Math.max(itemCount, minSlots)
}: {
  itemCount: number;
  minSlots?: number;
  sizingPlan: WireCardFlowPlan;
  slotCount?: number;
}): WireCardFlowPlan {
  const visibleSlotCount = Math.min(slotCount, sizingPlan.scrollAfter);
  const overflowCount = Math.max(0, slotCount - visibleSlotCount);
  const overflow: WireCardFlowOverflow = overflowCount > 0 ? "scroll" : "none";
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

function railPlan(
  kind: WireTableRailFlowKind,
  itemCount: number,
  minSlots: number,
  slotCount: number,
  strategy: WireCardFlowStep
): WireCardFlowPlan {
  return plan({
    capacity: WIRE_UNBOUNDED_CAPACITY,
    cardWidth: strategy.cardWidth,
    density: strategy.density,
    gap: strategy.gap,
    itemCount,
    kind,
    layout: "rail",
    minSlots,
    scrollAfter: strategy.scrollAfter,
    slotCount
  });
}

function selectRailStrategy(steps: WireCardFlowStep[], effectiveCount: number): WireCardFlowStep {
  return steps.find((candidate) => effectiveCount <= candidate.maxCount) ?? steps[steps.length - 1];
}

function step(
  maxCount: number,
  density: WireCardFlowDensity,
  cardWidth: number,
  gap: number,
  scrollAfter: number
): WireCardFlowStep {
  return { cardWidth, density, gap, maxCount, scrollAfter };
}

function plan({
  capacity,
  cardWidth,
  density,
  gap,
  itemCount,
  kind,
  layout,
  minSlots,
  scrollAfter,
  slotCount
}: {
  capacity: WireCardFlowPlan["capacity"];
  cardWidth: number;
  density: WireCardFlowDensity;
  gap: number;
  itemCount: number;
  kind: WireCardFlowKind;
  layout: WireCardFlowLayout;
  minSlots: number;
  scrollAfter: number;
  slotCount: number;
}): WireCardFlowPlan {
  const visibleSlotCount = Math.min(slotCount, scrollAfter);
  const overflowCount = Math.max(0, slotCount - visibleSlotCount);
  const overflow: WireCardFlowOverflow = overflowCount > 0 ? "scroll" : "none";
  return {
    capacity,
    cardHeight: Math.round(cardWidth / WIRE_CARD_IMAGE_RATIO),
    cardWidth,
    density,
    fit: overflow === "scroll" ? "overflow-rail" : layout === "rail" ? "elastic-rail" : "fixed-slot",
    gap,
    itemCount,
    kind,
    layout,
    minSlots,
    overflow,
    overflowCount,
    scrollAfter,
    slotCount,
    visibleSlotCount
  };
}

export type WireCardFlowKind = "battlefield-unit" | "base" | "hand" | "signature" | "standby";
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

const CARD_RATIO = 744 / 1039;
const UNBOUNDED_CAPACITY = "unbounded" as const;

type RailFlowKind = Exclude<WireCardFlowKind, "signature">;
type WireCardFlowStep = {
  cardWidth: number;
  density: WireCardFlowDensity;
  gap: number;
  maxCount: number;
  scrollAfter: number;
};

const RAIL_FLOW_STRATEGIES: Record<RailFlowKind, WireCardFlowStep[]> = {
  "battlefield-unit": [
    step(3, "sparse", 74, 4, 12),
    step(5, "normal", 68, 4, 12),
    step(8, "dense", 58, 4, 12),
    step(12, "packed", 48, 3, 12),
    step(Number.POSITIVE_INFINITY, "packed", 42, 3, 12)
  ],
  base: [
    step(3, "sparse", 86, 4, 10),
    step(6, "normal", 74, 4, 10),
    step(10, "dense", 62, 3, 10),
    step(Number.POSITIVE_INFINITY, "packed", 52, 3, 10)
  ],
  hand: [
    step(5, "sparse", 86, 4, 12),
    step(8, "normal", 74, 4, 12),
    step(12, "dense", 62, 3, 12),
    step(Number.POSITIVE_INFINITY, "packed", 52, 3, 12)
  ],
  standby: [
    step(2, "sparse", 58, 4, 8),
    step(4, "normal", 52, 4, 8),
    step(8, "dense", 46, 3, 8),
    step(Number.POSITIVE_INFINITY, "packed", 40, 3, 8)
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
      capacity: 1,
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

function railPlan(
  kind: RailFlowKind,
  itemCount: number,
  minSlots: number,
  slotCount: number,
  strategy: WireCardFlowStep
): WireCardFlowPlan {
  return plan({
    capacity: UNBOUNDED_CAPACITY,
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
    cardHeight: Math.round(cardWidth / CARD_RATIO),
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

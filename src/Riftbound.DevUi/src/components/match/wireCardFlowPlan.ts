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

  if (kind === "battlefield-unit") {
    if (effectiveCount <= 3) {
      return railPlan(kind, itemCount, minSlots, slotCount, "sparse", 74, 4, 12);
    }
    if (effectiveCount <= 5) {
      return railPlan(kind, itemCount, minSlots, slotCount, "normal", 68, 4, 12);
    }
    if (effectiveCount <= 8) {
      return railPlan(kind, itemCount, minSlots, slotCount, "dense", 58, 4, 12);
    }
    if (effectiveCount <= 12) {
      return railPlan(kind, itemCount, minSlots, slotCount, "packed", 48, 3, 12);
    }
    return railPlan(kind, itemCount, minSlots, slotCount, "packed", 42, 3, 12);
  }

  if (kind === "standby") {
    if (effectiveCount <= 2) {
      return railPlan(kind, itemCount, minSlots, slotCount, "sparse", 58, 4, 8);
    }
    if (effectiveCount <= 4) {
      return railPlan(kind, itemCount, minSlots, slotCount, "normal", 52, 4, 8);
    }
    if (effectiveCount <= 8) {
      return railPlan(kind, itemCount, minSlots, slotCount, "dense", 46, 3, 8);
    }
    return railPlan(kind, itemCount, minSlots, slotCount, "packed", 40, 3, 8);
  }

  if (kind === "base") {
    if (effectiveCount <= 3) {
      return railPlan(kind, itemCount, minSlots, slotCount, "sparse", 86, 4, 10);
    }
    if (effectiveCount <= 6) {
      return railPlan(kind, itemCount, minSlots, slotCount, "normal", 74, 4, 10);
    }
    if (effectiveCount <= 10) {
      return railPlan(kind, itemCount, minSlots, slotCount, "dense", 62, 3, 10);
    }
    return railPlan(kind, itemCount, minSlots, slotCount, "packed", 52, 3, 10);
  }

  if (effectiveCount <= 5) {
    return railPlan(kind, itemCount, minSlots, slotCount, "sparse", 86, 4, 12);
  }
  if (effectiveCount <= 8) {
    return railPlan(kind, itemCount, minSlots, slotCount, "normal", 74, 4, 12);
  }
  if (effectiveCount <= 12) {
    return railPlan(kind, itemCount, minSlots, slotCount, "dense", 62, 3, 12);
  }
  return railPlan(kind, itemCount, minSlots, slotCount, "packed", 52, 3, 12);
}

function railPlan(
  kind: Exclude<WireCardFlowKind, "signature">,
  itemCount: number,
  minSlots: number,
  slotCount: number,
  density: WireCardFlowDensity,
  cardWidth: number,
  gap: number,
  scrollAfter: number
): WireCardFlowPlan {
  return plan({
    capacity: UNBOUNDED_CAPACITY,
    cardWidth,
    density,
    gap,
    itemCount,
    kind,
    layout: "rail",
    minSlots,
    scrollAfter,
    slotCount
  });
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

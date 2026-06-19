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

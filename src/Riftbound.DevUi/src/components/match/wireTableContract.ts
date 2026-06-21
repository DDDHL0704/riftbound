export const WIRE_RUNE_DECK_SIZE = 12;
export const WIRE_TABLE_PLAYER_COUNT = 2;
export const WIRE_TABLE_BATTLEFIELD_LANE_COUNT = 2;
export const WIRE_TABLE_ROW_COUNT = 5;
export const WIRE_TABLE_UNIT_ZONE_COUNT = WIRE_TABLE_PLAYER_COUNT * WIRE_TABLE_BATTLEFIELD_LANE_COUNT;
export const WIRE_TABLE_CAPACITY_ROW_COUNT =
  (WIRE_TABLE_PLAYER_COUNT * 2) + (WIRE_TABLE_BATTLEFIELD_LANE_COUNT * 3);

export const WIRE_SIGNATURE_CARD_CAPACITY = 1;
export const WIRE_UNBOUNDED_CAPACITY = "unbounded" as const;
export const WIRE_CARD_IMAGE_RATIO = 744 / 1039;

export const WIRE_TABLE_FLOW_KINDS = [
  "battlefield-unit",
  "base",
  "hand",
  "signature",
  "standby"
] as const;

export type WireTableFlowKind = typeof WIRE_TABLE_FLOW_KINDS[number];
export type WireTableRailFlowKind = Exclude<WireTableFlowKind, "signature">;

export const WIRE_RAIL_VISIBLE_SLOT_LIMITS: Record<WireTableRailFlowKind, number> = {
  "battlefield-unit": WIRE_RUNE_DECK_SIZE,
  base: 10,
  hand: WIRE_RUNE_DECK_SIZE,
  standby: 8
};

export function wireTableCapacityRowKey(
  sideOrLane: "opponent" | "self" | number,
  slot: "base" | "hand" | "opponent" | "self" | "standby"
): string {
  return typeof sideOrLane === "number"
    ? `battlefield:${sideOrLane}:${slot}`
    : `${sideOrLane}:${slot}`;
}

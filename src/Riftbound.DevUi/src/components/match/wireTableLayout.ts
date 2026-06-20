import { type CSSProperties } from "react";
import layoutData from "./wireTableLayoutData.json";

export type WirePlayerSide = "self" | "opponent";
export type WireHandBodySlot = "cards" | "piles";
export type WireHandSlot = "hand" | "runeDeck" | "runeTrack";
export type WirePileSlot = "library" | "played";
export type WireHomeSlot = "base" | "hero" | "legend";
export type WireBaseSlot = "banish" | "base";
export type WireBattlefieldSlot = "center" | "leftSite" | "rightSite";
export type WireBattlefieldUnitSide = "opponent" | "self";

export type WireTableRow =
  | { id: string; kind: "battlefield" }
  | { id: string; kind: "handRail"; side: WirePlayerSide }
  | { id: string; kind: "playerHome"; side: WirePlayerSide };

export type WireTableTokens = {
  battlefieldCardHeight: number;
  battlefieldCardWidth: number;
  cardGap: number;
  cardHeight: number;
  cardWidth: number;
  deckAreaWidth: number;
  fixedPileCardHeight: number;
  fixedPileCardWidth: number;
  fixedPileSlotHeight: number;
  fixedPileSlotWidth: number;
  handPilesWidth: number;
  heroAreaWidth: number;
  publicPileWidth: number;
  runeCardHeight: number;
  runeCardWidth: number;
  runeTrackWidth: number;
  signatureAreaWidth: number;
  signatureZoneHeight: number;
  tableMinHeight: number;
  tableMinWidth: number;
};

export type WireHandRailLayout = {
  columns: string[];
  handBodyColumns: string[];
  handBodySlots: WireHandBodySlot[];
  pileSlots: WirePileSlot[];
  runeReverse: boolean;
  slots: WireHandSlot[];
};

export type WirePlayerHomeLayout = {
  baseColumns: string[];
  baseSlots: WireBaseSlot[];
  columns: string[];
  slots: WireHomeSlot[];
};

export type WireBattlefieldUnitZoneLayout = {
  id: string;
  laneIndex: number;
  side: WireBattlefieldUnitSide;
};

export type WireBattlefieldStandbyZoneLayout = {
  id: string;
  laneIndex: number;
};

export type WireBattlefieldLayout = {
  centerColumns: string[];
  centerRows: string[];
  columns: string[];
  standbyZones: WireBattlefieldStandbyZoneLayout[];
  slots: WireBattlefieldSlot[];
  unitZones: WireBattlefieldUnitZoneLayout[];
};

export type WireTableLayout = {
  battlefield: WireBattlefieldLayout;
  handRails: Record<WirePlayerSide, WireHandRailLayout>;
  playerHomes: Record<WirePlayerSide, WirePlayerHomeLayout>;
  runeDeckSize: number;
  table: {
    rows: WireTableRow[];
    rowTemplates: string[];
  };
  tokens: WireTableTokens;
};

type WireCssProperties = CSSProperties & Record<`--${string}`, string | number>;

export const WIRE_TABLE_LAYOUT: WireTableLayout = layoutData as WireTableLayout;

export function wireMatchPageStyle(layout: WireTableLayout = WIRE_TABLE_LAYOUT): WireCssProperties {
  const tokens = layout.tokens;
  return {
    "--wire-battlefield-card-h": px(tokens.battlefieldCardHeight),
    "--wire-battlefield-card-w": px(tokens.battlefieldCardWidth),
    "--wire-card-gap": px(tokens.cardGap),
    "--wire-card-h": px(tokens.cardHeight),
    "--wire-card-w": px(tokens.cardWidth),
    "--wire-deck-area-w": px(tokens.deckAreaWidth),
    "--wire-fixed-pile-card-h": px(tokens.fixedPileCardHeight),
    "--wire-fixed-pile-card-w": px(tokens.fixedPileCardWidth),
    "--wire-fixed-pile-slot-h": px(tokens.fixedPileSlotHeight),
    "--wire-fixed-pile-slot-w": px(tokens.fixedPileSlotWidth),
    "--wire-hand-piles-w": px(tokens.handPilesWidth),
    "--wire-hero-area-w": px(tokens.heroAreaWidth),
    "--wire-public-pile-w": px(tokens.publicPileWidth),
    "--wire-rune-card-h": px(tokens.runeCardHeight),
    "--wire-rune-card-w": px(tokens.runeCardWidth),
    "--wire-rune-track-w": px(tokens.runeTrackWidth),
    "--wire-signature-area-w": px(tokens.signatureAreaWidth),
    "--wire-signature-zone-h": px(tokens.signatureZoneHeight),
    "--wire-table-min-h": px(tokens.tableMinHeight),
    "--wire-table-min-w": px(tokens.tableMinWidth)
  };
}

export function wireTableStyle(layout: WireTableLayout = WIRE_TABLE_LAYOUT): CSSProperties {
  return {
    gridTemplateRows: template(layout.table.rowTemplates),
    minHeight: px(layout.tokens.tableMinHeight),
    minWidth: px(layout.tokens.tableMinWidth)
  };
}

export function wireGridColumnsStyle(columns: string[]): CSSProperties {
  return { gridTemplateColumns: template(columns) };
}

export function wireGridTemplateStyle(columns: string[], rows: string[]): CSSProperties {
  return {
    gridTemplateColumns: template(columns),
    gridTemplateRows: template(rows)
  };
}

function template(parts: string[]): string {
  return parts.join(" ");
}

function px(value: number): string {
  return `${value}px`;
}

import layoutData from "./tabletopLayoutData.json";

export type TableSide = "self" | "opponent";

export type LayoutBox = {
  id: string;
  label: string;
  x: number;
  y: number;
  width: number;
  height: number;
};

export type PlayerTableLayout = {
  legend: LayoutBox;
  champion: LayoutBox;
  score: LayoutBox;
  piles: LayoutBox;
  base: LayoutBox;
  runeBank: LayoutBox;
  hand: LayoutBox;
};

export type TabletopLayoutData = {
  runeDeckSize: number;
  players: Record<TableSide, PlayerTableLayout>;
  battlefields: LayoutBox[];
};

const typedLayoutData: TabletopLayoutData = layoutData;

export const RUNE_DECK_SIZE = typedLayoutData.runeDeckSize;

export const PLAYER_TABLE_LAYOUT: Record<TableSide, PlayerTableLayout> = typedLayoutData.players;

export const BATTLEFIELD_TABLE_LAYOUT: readonly LayoutBox[] = typedLayoutData.battlefields;

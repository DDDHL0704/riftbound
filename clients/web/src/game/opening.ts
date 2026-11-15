import type { PreconstructedDeck } from "./types";

/*
 * Opening rules a constructed room is created with (src/Riftbound.Contracts/OpeningRules.cs).
 * The three city-challenge modes are the season-five test: the second player is compensated and the
 * opening runs in the tested order. The server applies all of it; the client only picks and shows.
 */

export type OpeningRule = "STANDARD" | "S5_CITY_MODE1" | "S5_CITY_MODE2" | "S5_CITY_MODE3";

export const OPENING_RULES: OpeningRule[] = ["STANDARD", "S5_CITY_MODE1", "S5_CITY_MODE2", "S5_CITY_MODE3"];

export const OPENING_LABEL: Record<OpeningRule, string> = {
  STANDARD: "现有规则",
  S5_CITY_MODE1: "后手多抽",
  S5_CITY_MODE2: "后手洞察",
  S5_CITY_MODE3: "后手 1 分",
};

export const OPENING_HINT: Record<OpeningRule, string> = {
  STANDARD: "开局流程不变",
  S5_CITY_MODE1: "第五赛季城市挑战赛测试：后手在第一次抽牌阶段额外抽一张",
  S5_CITY_MODE2: "第五赛季城市挑战赛测试：后手在第一次抽牌前查看牌库顶一张，可以回收",
  S5_CITY_MODE3: "第五赛季城市挑战赛测试：后手以 1 分开始",
};

export const isCityRule = (rule?: string | null): rule is Exclude<OpeningRule, "STANDARD"> =>
  !!rule && rule !== "STANDARD" && rule in OPENING_LABEL;

/** "城市赛 · 后手多抽" for a test room; nothing for the standard opening. */
export const openingTag = (rule?: string | null) => (isCityRule(rule) ? `城市赛 · ${OPENING_LABEL[rule]}` : null);

export const CITY_MAIN = 40;
export const CITY_SIDEBOARD_MAX = 10;

/** A city room registers exactly 40 main cards; whatever a deck holds beyond that goes to the sideboard. */
export function citySplit(deck: PreconstructedDeck) {
  return { mainDeck: deck.mainDeck.slice(0, CITY_MAIN), sideboard: deck.mainDeck.slice(CITY_MAIN) };
}

export function cityDeckProblem(deck: PreconstructedDeck) {
  const extra = deck.mainDeck.length - CITY_MAIN;
  if (extra < 0) return `主牌堆需要 ${CITY_MAIN} 张`;
  if (extra > CITY_SIDEBOARD_MAX) return `主牌堆 ${CITY_MAIN} 张，备牌至多 ${CITY_SIDEBOARD_MAX} 张`;
  return null;
}

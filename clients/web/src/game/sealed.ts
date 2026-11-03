import { BLANK_BATTLEFIELD, cardInfo } from "./cards";
import { MAIN_CATEGORIES, RUNES } from "./decks";
import type { CardInfo, MatchSeries, SealedPool } from "./types";

// Sealed deck rules as the server enforces them (src/Riftbound.Engine/SealedDeckRules.cs, tournament
// rules 602.3–602.4). The checks mirror the server's so the builder can say what is missing; the
// server still decides, against the pool it stored for this player.

export const SEALED_MAIN_MIN = 25;
export const MAX_TRAITS = 3;

export type SealedDeck = { legendCardNo: string; championCardNo: string; mainDeck: string[]; runeDeck: string[]; battlefields: string[] };

export const emptySealedDeck = (): SealedDeck => ({ legendCardNo: "", championCardNo: "", mainDeck: [], runeDeck: [], battlefields: [] });

/** Best-of-one brings three battlefields and one is drawn; best-of-three names one per game (602.3.e). */
export const fieldsNeeded = (bestOf: number) => (bestOf === 3 ? 1 : 3);

const BASIC_RUNES: Record<string, string> = { 炽烈符文: "red", 翠意符文: "green", 灵光符文: "blue", 摧破符文: "orange", 混沌符文: "purple", 序理符文: "yellow" };

export function isBasicRune(c?: CardInfo) {
  return Boolean(c && c.category === "符文" && !c.effect && BASIC_RUNES[c.name?.trim()] && c.colors.length === 1 && c.colors[0] === BASIC_RUNES[c.name.trim()]);
}

/** One printing of each basic rune, this set's own when it has one: any printing is legal and none is counted. */
export function basicRunePrintings(cards: Record<string, CardInfo>, setCode: string) {
  const best = new Map<string, string>();
  for (const [no, c] of Object.entries(cards)) {
    if (!isBasicRune(c)) continue;
    const color = c.colors[0];
    const cur = best.get(color);
    // Alternate arts carry a letter suffix, so the shortest number is the plain printing.
    const rank = (x: string) => (x.startsWith(setCode) ? 0 : 100) + x.length;
    if (!cur || rank(no) < rank(cur)) best.set(color, no);
  }
  return Object.values(BASIC_RUNES).flatMap((color) => (best.has(color) ? [best.get(color)!] : []));
}

export const poolCounts = (pool: SealedPool) => new Map(pool.pool.map((p) => [p.cardNo, p.count]));

/** Physical copies a deck takes from the pool; the chosen champion is one of the main deck's cards. */
export function usedCounts(deck: SealedDeck) {
  const used = new Map<string, number>();
  for (const no of [...deck.mainDeck, ...deck.battlefields, ...(deck.legendCardNo ? [deck.legendCardNo] : [])]) used.set(no, (used.get(no) ?? 0) + 1);
  return used;
}

const unlimited = (no: string) => no === BLANK_BATTLEFIELD || isBasicRune(cardInfo(no));

/** Traits the deck actually uses, legend, runes and battlefields included (602.4.a.2). */
export function deckTraits(deck: SealedDeck) {
  const traits = new Set<string>();
  for (const no of [...deck.mainDeck, ...deck.runeDeck, ...deck.battlefields, ...(deck.legendCardNo ? [deck.legendCardNo] : [])])
    for (const color of cardInfo(no)?.colors ?? []) if (color !== "colorless") traits.add(color);
  return [...traits];
}

/** Whether adding this card would take the deck past the trait limit. */
export function breaksTraits(no: string, traits: string[]) {
  const next = new Set(traits);
  for (const color of cardInfo(no)?.colors ?? []) if (color !== "colorless") next.add(color);
  return next.size > MAX_TRAITS;
}

/** How many more games this battlefield can be named this match: each physical copy once, blank without limit. */
export function fieldUsesLeft(no: string, owned: number, usedBefore: string[] | undefined) {
  if (no === BLANK_BATTLEFIELD) return Infinity;
  return owned - (usedBefore ?? []).filter((x) => x === no).length;
}

/** Problems that would make the server reject this deck; empty when it is ready to submit. */
export function sealedProblems(deck: SealedDeck, pool: SealedPool, bestOf: number, usedBefore?: string[]): string[] {
  const out: string[] = [];
  const owned = poolCounts(pool);
  if (deck.mainDeck.length < SEALED_MAIN_MIN) out.push(`主牌堆还差 ${SEALED_MAIN_MIN - deck.mainDeck.length} 张（至少 ${SEALED_MAIN_MIN} 张）。`);
  if (deck.championCardNo && !deck.mainDeck.includes(deck.championCardNo)) out.push("主牌堆必须包含 1 张所选英雄。");
  if (deck.runeDeck.length !== RUNES) out.push(`符文需要正好 ${RUNES} 张，现在 ${deck.runeDeck.length} 张。`);
  const fields = fieldsNeeded(bestOf);
  if (deck.battlefields.length !== fields) out.push(`战场需要正好 ${fields} 张，现在 ${deck.battlefields.length} 张。`);

  const traits = deckTraits(deck);
  if (traits.length > MAX_TRAITS) out.push(`卡组用到了 ${traits.length} 种特性，最多 ${MAX_TRAITS} 种（传奇、符文和战场都算）。`);

  const legend = cardInfo(deck.legendCardNo);
  for (const no of new Set(deck.mainDeck)) {
    const c = cardInfo(no);
    if (!c) continue;
    if (!MAIN_CATEGORIES.has(c.category)) out.push(`《${c.name}》不能放进主牌堆。`);
    // Signature spells are exempt from the hero tag (602.4.a.3.b); other signature cards are not (core 103.3.d.2).
    if (c.category.startsWith("专属") && c.category !== "专属法术" && c.hero !== legend?.hero) out.push(`专属牌《${c.name}》需要对应英雄的传奇。`);
  }
  for (const [no, n] of usedCounts(deck)) {
    if (!unlimited(no) && n > (owned.get(no) ?? 0)) out.push(`《${cardInfo(no)?.name ?? no}》用了 ${n} 张，卡池只有 ${owned.get(no) ?? 0} 张。`);
  }
  for (const no of deck.runeDeck) if (cardInfo(no) && !isBasicRune(cardInfo(no))) out.push(`《${cardInfo(no)?.name}》不是基础符文。`);
  if (bestOf === 3) {
    for (const no of new Set(deck.battlefields)) if (fieldUsesLeft(no, owned.get(no) ?? 0, usedBefore) <= 0) out.push(`《${cardInfo(no)?.name ?? no}》本场已经用过了。`);
  }
  return [...new Set(out)];
}

/** Twelve runes split as evenly as possible across the traits the deck uses. */
export function evenRunes(deck: SealedDeck, printings: string[]) {
  const traits = deckTraits({ ...deck, runeDeck: [] });
  const choices = printings.filter((no) => traits.includes(cardInfo(no)?.colors[0] ?? ""));
  if (!choices.length) return deck.runeDeck;
  return Array.from({ length: RUNES }, (_, i) => choices[i % choices.length]);
}

/** Games won by each side, from this viewer's seat. */
export function seriesScore(series: MatchSeries, meId: string) {
  const mine = series.wins[meId] ?? 0;
  const theirs = Object.entries(series.wins).find(([id]) => id !== meId)?.[1] ?? 0;
  return { mine, theirs };
}

export type DeckLock = "none" | "deck" | "all";

/**
 * What the next game lets this player change. After a draw nothing can change, the battlefield included;
 * after a skipped opening only the battlefield can. The snapshot does not name the case, but a draw follows
 * a game that recorded this player's battlefield and a skipped opening recorded none.
 */
export function deckLock(series: MatchSeries | null, meId: string): DeckLock {
  if (!series?.lockedDeck) return "none";
  return series.usedBattlefields[meId]?.length ? "all" : "deck";
}

// The draft stays in this browser, per room and player, so a reload or the next game of a match starts
// from the last deck rather than an empty one. It is never sent until the player submits it.
const key = (roomId: string, handle: string) => `riftbound.web.sealed.${roomId}.${handle}`;

export type SealedDraft = { deck: SealedDeck; packsOpened: number; forGame?: number };

export function readDraft(roomId: string, handle: string): SealedDraft {
  try {
    const raw = JSON.parse(localStorage.getItem(key(roomId, handle)) ?? "null");
    if (raw?.deck) return raw as SealedDraft;
  } catch {
    /* falls through to an empty draft */
  }
  return { deck: emptySealedDeck(), packsOpened: 0 };
}

export function writeDraft(roomId: string, handle: string, draft: SealedDraft) {
  try {
    localStorage.setItem(key(roomId, handle), JSON.stringify(draft));
  } catch {
    /* kept for this visit only */
  }
}

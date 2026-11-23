import { useSyncExternalStore } from "react";
import type { CardInfo, GameObject } from "./types";

/** The server's reserved number for sealed's blank battlefield (602.3.d.1); it has no catalog entry. */
export const BLANK_BATTLEFIELD = "SEALED·BLANK-BATTLEFIELD";

let cards: Record<string, CardInfo> = {};
let loaded = false;
const listeners = new Set<() => void>();

export function loadCards() {
  if (loaded) return;
  loaded = true;
  fetch(`${import.meta.env.BASE_URL}cards.json`)
    .then((r) => r.json())
    .then((data: Record<string, CardInfo>) => {
      cards = { ...data, [BLANK_BATTLEFIELD]: { name: "空白战场", category: "战场", colors: ["colorless"], effect: "没有规则文字的战场。" } };
      listeners.forEach((fn) => fn());
    })
    .catch(() => {
      /* names fall back to card numbers */
    });
}

const subscribe = (fn: () => void) => {
  listeners.add(fn);
  return () => listeners.delete(fn);
};

export function useCards() {
  return useSyncExternalStore(subscribe, () => cards);
}

export function cardInfo(cardNo: string | null | undefined): CardInfo | undefined {
  return cardNo ? cards[cardNo] : undefined;
}

export function cardName(cardNo: string | null | undefined) {
  return cardInfo(cardNo)?.name ?? cardNo ?? "未知卡牌";
}

// Server labels name cards by number ("横置友方单位：UNL-053/219", "OGN·042b/298"); read them as names.
const CARD_NO = /[A-Z]{2,4}[·-][A-Za-z0-9*]+(?:\/\d+)?(?:·[A-Z])?/g;

/** A label with every catalog card number in it replaced by that card's name. */
export function nameCardNumbers(text: string) {
  return text.replace(CARD_NO, (no) => cards[no]?.name ?? no);
}

// Legends, battlefields, runes and tokens never sit in a deck or hand, so a name never stands for them there.
const OFF_DECK = /^(传奇|战场|符文|指示物)/;
let printings: Map<string, string[]> | null = null;
let printingsFor: Record<string, CardInfo> | null = null;

function printingsByName() {
  if (printingsFor !== cards) {
    printings = new Map();
    const entries = Object.entries(cards)
      .filter(([, info]) => !OFF_DECK.test(info.category))
      .sort(([, a], [, b]) => (a.sort ?? 0) - (b.sort ?? 0));
    for (const [no, info] of entries) printings.set(info.name, [...(printings.get(info.name) ?? []), no]);
    printingsFor = cards;
  }
  return printings!;
}

/**
 * The card behind a choice that is not on the table, such as the top of the deck during insight. Choices
 * label such a card by number, or by name when it comes from a deck or hand. A name can belong to several
 * printings (蔚 has four different units), so the printing the object id names wins (the server builds deck
 * ids from the number, e.g. "p1-MAIN-UNL-053-219-35"), then the first printing of that name.
 */
export function choiceCardNo(label: string, objectId?: string): string | undefined {
  if (cards[label]) return label;
  const named = printingsByName().get(label);
  if (!named?.length) return undefined;
  const key = (no: string) => `-${no.replace(/[^A-Za-z0-9]/g, "-").replace(/^-+|-+$/g, "")}-`;
  return named.find((no) => objectId?.includes(key(no))) ?? named[0];
}

/** Colourless and neutral cards fit any legend and add no trait (server: MatchSession/Models.cs IsNeutral). */
export const isNeutralColor = (c: string) => c === "colorless" || c === "neutral";

export const COLOR_HEX: Record<string, string> = {
  red: "#d9503f",
  blue: "#4f8fdc",
  green: "#4fae6a",
  orange: "#e08a2e",
  purple: "#9a68d0",
  yellow: "#e2c044",
  colorless: "#9d97a6",
  neutral: "#9d97a6",
};

export const COLOR_ZH: Record<string, string> = {
  红色: "red",
  蓝色: "blue",
  绿色: "green",
  橙色: "orange",
  紫色: "purple",
  黄色: "yellow",
};

export type EffectToken =
  | { kind: "text"; text: string }
  | { kind: "mana"; value: string }
  | { kind: "might" }
  | { kind: "power"; color?: string }
  | { kind: "exhaust" }
  | { kind: "keyword"; text: string };

// Keyword explanations come from the cards themselves: the official text prints reminder text in
// parentheses after many keywords, e.g. "{{瞬息}}（在其控制者的开始阶段开始时，结算得分之前将其摧毁。）".
// The most common reminder per keyword wins; keywords the catalog never explains get none.
let glossary: Map<string, string> | null = null;
let glossaryFor: Record<string, CardInfo> | null = null;

function buildGlossary() {
  const seen = new Map<string, Map<string, number>>();
  const add = (kw: string, text: string, weight: number) => {
    const m = seen.get(kw) ?? new Map<string, number>();
    m.set(text, (m.get(text) ?? 0) + weight);
    seen.set(kw, m);
  };
  for (const card of Object.values(cards)) {
    const effect = card.effect ?? "";
    for (const m of effect.matchAll(/\{\{([^}]+)\}\}\s*（([^）]+)）/g)) add(m[1].replace(/>$/, ""), m[2], 1);
    // "{{绝念}}—effect.（reminder）": only trusted when many cards agree (see below).
    for (const m of effect.matchAll(/\{\{(绝念|鼓舞)>?\}\}—[^（{]*（([^）]+)）/g)) add(m[1], m[2], 0.01);
  }
  const out = new Map<string, string>();
  for (const [kw, texts] of seen) {
    const [text, score] = [...texts].sort((a, b) => b[1] - a[1])[0];
    if (score >= 0.03) out.set(kw, text);
  }
  return out;
}

/** The catalog's own reminder text for a keyword, if any card prints one. */
export function keywordReminder(keyword: string): string | undefined {
  if (glossaryFor !== cards) {
    glossary = buildGlossary();
    glossaryFor = cards;
  }
  // Colour-suffixed variants ("装配红色", "回响2红色") share the plain variant's reminder.
  return glossary!.get(keyword) ?? glossary!.get(keyword.replace(/(红色|蓝色|绿色|橙色|紫色|黄色)$/, ""));
}

/** Splits catalog effect text into plain runs and {{…}} symbols. */
export function tokenizeEffect(effect: string): EffectToken[] {
  const out: EffectToken[] = [];
  const re = /\{\{([^}]*)\}\}/g;
  let last = 0;
  for (let m = re.exec(effect); m; m = re.exec(effect)) {
    if (m.index > last) out.push({ kind: "text", text: effect.slice(last, m.index) });
    const raw = m[1].replace(/>$/, "");
    if (/^\d+$/.test(raw)) out.push({ kind: "mana", value: raw });
    else if (raw === "S") out.push({ kind: "might" });
    else if (raw === "A") out.push({ kind: "power" });
    else if (COLOR_ZH[raw]) out.push({ kind: "power", color: COLOR_ZH[raw] });
    else if (raw === "横置") out.push({ kind: "exhaust" });
    else out.push({ kind: "keyword", text: raw });
    last = m.index + m[0].length;
  }
  if (last < effect.length) out.push({ kind: "text", text: effect.slice(last) });
  return out;
}

export function isUnit(tags: string[] | undefined) {
  return Boolean(tags?.includes("CARD_TYPE:UNIT"));
}

export function colorOf(tags: string[] | undefined) {
  const tag = tags?.find((t) => t.startsWith("COLOR:") && !isNeutralColor(t.slice(6)));
  return tag ? tag.slice(6) : "colorless";
}

/** Rune payment choices arrive labelled by card number ("回收符文支付：OGN·089/298"); name the rune instead. */
export function paymentChoiceLabel(id: string, label: string, objects: Record<string, GameObject>) {
  const tail = id.split(":").pop() ?? id;
  const o = objects[id] ?? objects[tail];
  if (!o) return nameCardNumbers(label);
  // A label that already names the card ("让学徒铁匠变为休眠状态") reads as it is.
  const name = cardName(o.cardNo);
  return label.includes(name) ? label : `${label.split(/[：:]/)[0]}：${name}`;
}

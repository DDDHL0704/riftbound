import { useSyncExternalStore } from "react";
import type { CardInfo, GameObject } from "./types";

let cards: Record<string, CardInfo> = {};
let loaded = false;
const listeners = new Set<() => void>();

export function loadCards() {
  if (loaded) return;
  loaded = true;
  fetch(`${import.meta.env.BASE_URL}cards.json`)
    .then((r) => r.json())
    .then((data: Record<string, CardInfo>) => {
      cards = data;
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

export const COLOR_HEX: Record<string, string> = {
  red: "#d9503f",
  blue: "#4f8fdc",
  green: "#4fae6a",
  orange: "#e08a2e",
  purple: "#9a68d0",
  yellow: "#e2c044",
  colorless: "#9d97a6",
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
  const tag = tags?.find((t) => t.startsWith("COLOR:") && t !== "COLOR:colorless");
  return tag ? tag.slice(6) : "colorless";
}

/** Rune payment choices arrive labelled by card number ("回收符文支付：OGN·089/298"); name the rune instead. */
export function paymentChoiceLabel(id: string, label: string, objects: Record<string, GameObject>) {
  const tail = id.split(":").pop() ?? id;
  const o = objects[id] ?? objects[tail];
  return o ? `${label.split(/[：:]/)[0]}：${cardName(o.cardNo)}` : label;
}

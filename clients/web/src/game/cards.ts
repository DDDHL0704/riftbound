import { useSyncExternalStore } from "react";
import type { CardInfo } from "./types";

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

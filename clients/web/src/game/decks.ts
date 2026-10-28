import { useSyncExternalStore } from "react";
import { cardInfo } from "./cards";
import type { CardInfo, PreconstructedDeck } from "./types";

// Custom decks live in this browser only. The server validates every submitted deck against the
// official construction rules (src/Riftbound.Engine/OfficialDeckRules.cs); the checks here mirror
// those rules so the editor can say what is missing before anything is sent.

export type CustomDeck = PreconstructedDeck & { custom: true; updatedAt: number };

export const MAIN_MIN = 40;
export const RUNES = 12;
export const FIELDS = 3;
export const MAX_COPIES = 3;
export const MAX_EXCLUSIVE = 3;

const KEY = "riftbound.web.decks";
let decks: CustomDeck[] = read();
const listeners = new Set<() => void>();

function read(): CustomDeck[] {
  try {
    const raw = JSON.parse(localStorage.getItem(KEY) ?? "[]");
    return Array.isArray(raw) ? raw.filter((d) => d && typeof d.id === "string") : [];
  } catch {
    return [];
  }
}

function write(next: CustomDeck[]) {
  decks = next;
  try {
    localStorage.setItem(KEY, JSON.stringify(next));
  } catch {
    /* kept for this visit only */
  }
  listeners.forEach((fn) => fn());
}

export function useCustomDecks() {
  return useSyncExternalStore(
    (fn) => {
      listeners.add(fn);
      return () => listeners.delete(fn);
    },
    () => decks,
  );
}

export function saveDeck(deck: CustomDeck) {
  const rest = decks.filter((d) => d.id !== deck.id);
  write([{ ...deck, updatedAt: Date.now() }, ...rest]);
}

export function deleteDeck(id: string) {
  write(decks.filter((d) => d.id !== id));
}

export function newDeckFrom(base?: PreconstructedDeck): CustomDeck {
  return {
    id: `custom-${Date.now().toString(36)}`,
    name: base ? `${base.name}（自定义）` : "新卡组",
    description: "",
    legendCardNo: base?.legendCardNo ?? "",
    championCardNo: base?.championCardNo ?? "",
    mainDeck: [...(base?.mainDeck ?? [])],
    runeDeck: [...(base?.runeDeck ?? [])],
    battlefields: [...(base?.battlefields ?? [])],
    custom: true,
    updatedAt: Date.now(),
  };
}

const isExclusive = (c?: CardInfo) => Boolean(c?.category?.startsWith("专属"));
const isUnique = (c?: CardInfo) => c?.limit === 1 || Boolean(c?.effect?.includes("{{唯我}}"));
export const MAIN_CATEGORIES = new Set(["单位", "英雄单位", "装备", "法术", "专属单位", "专属装备", "专属法术"]);

/** How many copies of this card's name the main deck may hold. */
export function copyLimit(cardNo: string) {
  return isUnique(cardInfo(cardNo)) ? 1 : MAX_COPIES;
}

/** Whether a card's colours fit the legend's (colourless always fits). */
export function fitsLegend(cardNo: string, legendNo: string) {
  const legend = cardInfo(legendNo);
  if (!legend) return true;
  return (cardInfo(cardNo)?.colors ?? []).every((c) => c === "colorless" || legend.colors.includes(c));
}

/** Problems that would make the server reject this deck; empty when it is ready to submit. */
export function deckProblems(deck: PreconstructedDeck): string[] {
  const out: string[] = [];
  const legend = cardInfo(deck.legendCardNo);
  const champion = cardInfo(deck.championCardNo);
  if (!legend) out.push("还没有选择传奇。");
  if (!champion) out.push("还没有选择英雄。");
  else if (legend && champion.hero !== legend.hero) out.push("英雄必须与传奇是同一位英雄。");
  if (deck.mainDeck.length < MAIN_MIN) out.push(`主牌堆还差 ${MAIN_MIN - deck.mainDeck.length} 张（至少 ${MAIN_MIN} 张）。`);
  if (champion && !deck.mainDeck.includes(deck.championCardNo)) out.push("主牌堆必须包含 1 张所选英雄。");
  if (deck.runeDeck.length !== RUNES) out.push(`符文需要正好 ${RUNES} 张，现在 ${deck.runeDeck.length} 张。`);
  if (deck.battlefields.length !== FIELDS) out.push(`战场需要正好 ${FIELDS} 张，现在 ${deck.battlefields.length} 张。`);

  const byName = new Map<string, number>();
  let exclusive = 0;
  for (const no of deck.mainDeck) {
    const c = cardInfo(no);
    if (!c) continue;
    byName.set(c.name, (byName.get(c.name) ?? 0) + 1);
    if (isExclusive(c)) {
      exclusive++;
      if (legend && c.hero !== legend.hero) out.push(`专属牌《${c.name}》属于其他英雄。`);
    }
  }
  for (const [name, n] of byName) {
    const no = deck.mainDeck.find((x) => cardInfo(x)?.name === name)!;
    if (n > copyLimit(no)) out.push(`《${name}》有 ${n} 张，上限 ${copyLimit(no)} 张。`);
  }
  if (exclusive > MAX_EXCLUSIVE) out.push(`专属牌最多 ${MAX_EXCLUSIVE} 张，现在 ${exclusive} 张。`);
  if (legend) {
    const off = [...deck.mainDeck, ...deck.runeDeck, ...deck.battlefields].filter((no) => !fitsLegend(no, deck.legendCardNo));
    if (off.length) out.push(`有 ${new Set(off).size} 种牌的颜色不属于所选传奇。`);
  }
  const fieldNames = deck.battlefields.map((no) => cardInfo(no)?.name);
  if (new Set(fieldNames).size !== fieldNames.length) out.push("战场不能重名。");
  return out;
}

// Plain-text deck lists, one line per entry, so a deck can be pasted into chat and back:
//   传奇 UNL-181/219 戏命师
//   战场 OGN·275/298 团结圣坛
//   符文 6 OGN·007/298 红色符文
//   主牌 3 UNL-001/219 某张牌
// The card number decides; the trailing name is only for people reading the list.
const SECTIONS = { 传奇: "legend", 英雄: "champion", 战场: "battlefields", 符文: "runeDeck", 主牌: "mainDeck" } as const;

export function exportDeck(deck: PreconstructedDeck): string {
  const counted = (list: string[]) => {
    const m = new Map<string, number>();
    for (const no of list) m.set(no, (m.get(no) ?? 0) + 1);
    return [...m];
  };
  const name = (no: string) => cardInfo(no)?.name ?? "";
  return [
    `# ${deck.name}`,
    `传奇 ${deck.legendCardNo} ${name(deck.legendCardNo)}`,
    `英雄 ${deck.championCardNo} ${name(deck.championCardNo)}`,
    ...deck.battlefields.map((no) => `战场 ${no} ${name(no)}`),
    ...counted(deck.runeDeck).map(([no, n]) => `符文 ${n} ${no} ${name(no)}`),
    ...counted(deck.mainDeck).map(([no, n]) => `主牌 ${n} ${no} ${name(no)}`),
  ].join("\n");
}

/** Reads a list written by exportDeck; lines it cannot use come back as errors. */
export function parseDeck(text: string): { deck: CustomDeck; errors: string[] } {
  const deck = newDeckFrom();
  const errors: string[] = [];
  for (const [i, raw] of text.split(/\r?\n/).entries()) {
    const line = raw.trim();
    if (!line) continue;
    if (line.startsWith("#")) {
      deck.name = line.slice(1).trim().slice(0, 30) || deck.name;
      continue;
    }
    const m = /^(传奇|英雄|战场|符文|主牌)[\s:：]+(?:(\d+)\s*[x×]?\s+)?(\S+)/.exec(line);
    if (!m) {
      errors.push(`第 ${i + 1} 行看不懂：${line.slice(0, 30)}`);
      continue;
    }
    const [, section, count, no] = m;
    if (!cardInfo(no)) {
      errors.push(`第 ${i + 1} 行的牌号 ${no} 不存在。`);
      continue;
    }
    const field = SECTIONS[section as keyof typeof SECTIONS];
    const n = Math.min(Number(count ?? 1), 60);
    if (field === "legend") deck.legendCardNo = no;
    else if (field === "champion") deck.championCardNo = no;
    else for (let k = 0; k < n; k++) deck[field].push(no);
  }
  return { deck, errors };
}

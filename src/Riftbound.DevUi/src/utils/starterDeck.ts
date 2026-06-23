import { SubmitDeckCommand } from "../types/protocol";

export const starterDeckOverrideKey = "riftbound.dev.starterDeckOverride";
export const starterDeckOverrideQueryKey = "starterDeckOverride";

type DeckArrayField = "mainDeck" | "runeDeck" | "battlefields";
type DeckField = "cmdType" | "legendCardNo" | "championCardNo" | DeckArrayField | "format";

export type DeckImportIssue = {
  field: DeckField;
  message: string;
};

export type DeckImportResult =
  | { ok: true; deck: SubmitDeckCommand; format: "json" | "text" }
  | { ok: false; issues: DeckImportIssue[] };

export type DeckEntry = {
  cardNo: string;
  count: number;
};

export type DeckSubmissionSummary = {
  battlefields: number;
  championCardNo: string;
  distinctBattlefields: number;
  distinctMainDeck: number;
  distinctRuneDeck: number;
  legendCardNo: string;
  mainDeck: number;
  runeDeck: number;
};

export function defaultStarterDeck(): SubmitDeckCommand {
  return {
    cmdType: "SUBMIT_DECK",
    legendCardNo: "UNL-181/219",
    championCardNo: "UNL-022/219",
    mainDeck: [
      "UNL-022/219",
      "ARC-001/006",
      "ARC-001/006",
      "ARC-001/006",
      "ARC-003/006",
      "ARC-003/006",
      "ARC-003/006",
      "OGN·001/298",
      "OGN·001/298",
      "OGN·001/298",
      "OGN·002/298",
      "OGN·002/298",
      "OGN·002/298",
      "OGN·003/298",
      "OGN·003/298",
      "OGN·003/298",
      "OGN·004/298",
      "OGN·004/298",
      "OGN·004/298",
      "OGN·005/298",
      "OGN·005/298",
      "OGN·005/298",
      "OGN·006/298",
      "OGN·006/298",
      "OGN·006/298",
      "OGN·008/298",
      "OGN·008/298",
      "OGN·008/298",
      "OGN·009/298",
      "OGN·009/298",
      "OGN·009/298",
      "OGN·010/298",
      "OGN·010/298",
      "OGN·010/298",
      "OGN·011/298",
      "OGN·011/298",
      "OGN·011/298",
      "OGN·012/298",
      "OGN·012/298",
      "OGN·012/298"
    ],
    runeDeck: [
      "OGN·007/298",
      "OGN·007a/298",
      "OGN·007b/298",
      "OGN·089/298",
      "OGN·089a/298",
      "OGN·089b/298",
      "SFD·R01",
      "SFD·R01a",
      "SFD·R01b",
      "SFD·R03",
      "SFD·R03a",
      "SFD·R03b"
    ],
    battlefields: ["OGN·275/298", "OGN·276/298", "OGN·277/298"]
  };
}

export function parseStarterDeckOverride(raw: string): SubmitDeckCommand | undefined {
  const result = parseDeckJson(raw, true);
  return result.ok ? result.deck : undefined;
}

export function parseDeckImport(raw: string): DeckImportResult {
  const trimmed = raw.trim();
  if (!trimmed) {
    return invalid([{ field: "format", message: "粘贴内容为空。" }]);
  }

  if (trimmed.startsWith("{")) {
    return parseDeckJson(trimmed, false);
  }

  return parseDeckListText(trimmed);
}

export function serializeStarterDeck(deck: SubmitDeckCommand): string {
  return JSON.stringify(deck);
}

export function deckToImportText(deck: SubmitDeckCommand): string {
  return [
    `legend: ${deck.legendCardNo}`,
    `champion: ${deck.championCardNo}`,
    "main:",
    ...countDeckCards(deck.mainDeck).map(formatDeckEntry),
    "runes:",
    ...countDeckCards(deck.runeDeck).map(formatDeckEntry),
    "battlefields:",
    ...countDeckCards(deck.battlefields).map(formatDeckEntry)
  ].join("\n");
}

export function countDeckCards(cardNos: readonly string[]): DeckEntry[] {
  const counts = new Map<string, number>();
  for (const cardNo of cardNos) {
    counts.set(cardNo, (counts.get(cardNo) ?? 0) + 1);
  }

  return Array.from(counts, ([cardNo, count]) => ({ cardNo, count }));
}

export function summarizeStarterDeck(deck: SubmitDeckCommand): DeckSubmissionSummary {
  return {
    battlefields: deck.battlefields.length,
    championCardNo: deck.championCardNo,
    distinctBattlefields: countDeckCards(deck.battlefields).length,
    distinctMainDeck: countDeckCards(deck.mainDeck).length,
    distinctRuneDeck: countDeckCards(deck.runeDeck).length,
    legendCardNo: deck.legendCardNo,
    mainDeck: deck.mainDeck.length,
    runeDeck: deck.runeDeck.length
  };
}

function parseDeckJson(raw: string, requireCmdType: boolean): DeckImportResult {
  try {
    return toSubmitDeckCommand(JSON.parse(raw), requireCmdType, "json");
  } catch (error) {
    return invalid([
      {
        field: "format",
        message: `JSON 无法解析：${error instanceof Error ? error.message : String(error)}`
      }
    ]);
  }
}

function parseDeckListText(raw: string): DeckImportResult {
  let section: DeckArrayField | undefined;
  const issues: DeckImportIssue[] = [];
  const draft: {
    battlefields: string[];
    championCardNo?: string;
    legendCardNo?: string;
    mainDeck: string[];
    runeDeck: string[];
  } = {
    battlefields: [],
    mainDeck: [],
    runeDeck: []
  };

  raw.split(/\r?\n/).forEach((rawLine, index) => {
    const lineNo = index + 1;
    const line = stripListMarker(rawLine.trim());
    if (!line || line.startsWith("#") || line.startsWith("//")) {
      return;
    }

    const assignment = line.match(/^([^:：]+)[:：]\s*(.*)$/);
    if (assignment) {
      const label = normalizeLabel(assignment[1]);
      const value = assignment[2].trim();
      const field = fieldFromLabel(label);
      if (field === "legendCardNo" || field === "championCardNo") {
        const cardNo = firstCardToken(value);
        if (!cardNo) {
          issues.push({ field, message: `第 ${lineNo} 行缺少卡牌编号。` });
          return;
        }

        draft[field] = cardNo;
        section = undefined;
        return;
      }

      if (field === "mainDeck" || field === "runeDeck" || field === "battlefields") {
        section = field;
        if (value) {
          draft[field].push(...parseCardEntries(value, field, lineNo, issues));
        }
        return;
      }
    }

    const maybeSection = fieldFromLabel(normalizeLabel(line));
    if (maybeSection === "mainDeck" || maybeSection === "runeDeck" || maybeSection === "battlefields") {
      section = maybeSection;
      return;
    }

    if (!section) {
      issues.push({ field: "format", message: `第 ${lineNo} 行不在 main/runes/battlefields 区段内。` });
      return;
    }

    draft[section].push(...parseCardEntries(line, section, lineNo, issues));
  });

  if (issues.length > 0) {
    return invalid(issues);
  }

  return toSubmitDeckCommand(
    {
      cmdType: "SUBMIT_DECK",
      legendCardNo: draft.legendCardNo,
      championCardNo: draft.championCardNo,
      mainDeck: draft.mainDeck,
      runeDeck: draft.runeDeck,
      battlefields: draft.battlefields
    },
    true,
    "text"
  );
}

function toSubmitDeckCommand(value: unknown, requireCmdType: boolean, format: "json" | "text"): DeckImportResult {
  if (!isRecord(value)) {
    return invalid([{ field: "format", message: "导入内容必须是对象或分区文本。" }]);
  }

  const issues: DeckImportIssue[] = [];
  const cmdType = value.cmdType;
  if (requireCmdType || cmdType != null) {
    if (cmdType !== "SUBMIT_DECK") {
      issues.push({ field: "cmdType", message: "cmdType 必须是 SUBMIT_DECK。" });
    }
  }

  const legendCardNo = readRequiredString(value, "legendCardNo", "传奇", issues);
  const championCardNo = readRequiredString(value, "championCardNo", "英雄", issues);
  const mainDeck = readRequiredStringArray(value, "mainDeck", "主牌堆", issues);
  const runeDeck = readRequiredStringArray(value, "runeDeck", "符文牌堆", issues);
  const battlefields = readRequiredStringArray(value, "battlefields", "战场池", issues);

  if (issues.length > 0) {
    return invalid(issues);
  }

  return {
    ok: true,
    deck: {
      cmdType: "SUBMIT_DECK",
      legendCardNo: legendCardNo ?? "",
      championCardNo: championCardNo ?? "",
      mainDeck: mainDeck ?? [],
      runeDeck: runeDeck ?? [],
      battlefields: battlefields ?? []
    },
    format
  };
}

function readRequiredString(
  value: Record<string, unknown>,
  field: "legendCardNo" | "championCardNo",
  label: string,
  issues: DeckImportIssue[]
): string | undefined {
  const raw = value[field];
  if (typeof raw !== "string" || raw.trim().length === 0) {
    issues.push({ field, message: `${label}必须提供卡牌编号。` });
    return undefined;
  }

  return raw.trim();
}

function readRequiredStringArray(
  value: Record<string, unknown>,
  field: DeckArrayField,
  label: string,
  issues: DeckImportIssue[]
): string[] | undefined {
  const raw = value[field];
  if (!Array.isArray(raw)) {
    issues.push({ field, message: `${label}必须是卡牌编号数组。` });
    return undefined;
  }

  const cards = raw
    .filter((entry): entry is string => typeof entry === "string")
    .map((entry) => entry.trim())
    .filter(Boolean);
  if (cards.length !== raw.length || cards.length === 0) {
    issues.push({ field, message: `${label}包含空值或非字符串。` });
    return undefined;
  }

  return cards;
}

function parseCardEntries(raw: string, field: DeckArrayField, lineNo: number, issues: DeckImportIssue[]): string[] {
  return raw
    .split(/[，,]/)
    .map((entry) => parseCardEntry(entry.trim(), field, lineNo, issues))
    .flat();
}

function parseCardEntry(raw: string, field: DeckArrayField, lineNo: number, issues: DeckImportIssue[]): string[] {
  if (!raw) {
    return [];
  }

  const withoutMarker = stripListMarker(raw);
  const prefixCount = withoutMarker.match(/^(\d{1,3})\s*(?:[xX×*]\s*)?(.+)$/);
  const suffixCount = withoutMarker.match(/^(.+?)\s*(?:[xX×*])\s*(\d{1,3})$/);
  const count = Number(prefixCount?.[1] ?? suffixCount?.[2] ?? 1);
  const cardText = (prefixCount?.[2] ?? suffixCount?.[1] ?? withoutMarker).trim();
  const cardNo = firstCardToken(cardText);

  if (!Number.isInteger(count) || count < 1 || count > 120 || !cardNo) {
    issues.push({ field, message: `第 ${lineNo} 行卡牌数量或编号无效。` });
    return [];
  }

  return Array.from({ length: count }, () => cardNo);
}

function fieldFromLabel(label: string): DeckField | undefined {
  if (label === "legend" || label === "legendcardno" || label === "传奇") {
    return "legendCardNo";
  }
  if (label === "champion" || label === "championcardno" || label === "hero" || label === "英雄") {
    return "championCardNo";
  }
  if (label === "main" || label === "maindeck" || label === "deck" || label === "主牌" || label === "主牌堆") {
    return "mainDeck";
  }
  if (label === "rune" || label === "runes" || label === "runedeck" || label === "符文" || label === "符文牌堆") {
    return "runeDeck";
  }
  if (label === "battlefield" || label === "battlefields" || label === "战场" || label === "战场池") {
    return "battlefields";
  }

  return undefined;
}

function firstCardToken(raw: string): string {
  return raw.trim().split(/\s+/)[0]?.trim() ?? "";
}

function formatDeckEntry(entry: DeckEntry): string {
  return `${entry.count} ${entry.cardNo}`;
}

function invalid(issues: DeckImportIssue[]): DeckImportResult {
  return { ok: false, issues };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function normalizeLabel(raw: string): string {
  return raw.trim().replace(/\s+/g, "").toLowerCase();
}

function stripListMarker(raw: string): string {
  return raw.replace(/^[-*]\s+/, "").trim();
}

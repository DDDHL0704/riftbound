import { SubmitDeckCommand } from "../types/protocol";

const starterDeckOverrideKey = "riftbound.dev.starterDeckOverride";
const starterDeckOverrideQueryKey = "starterDeckOverride";

export function buildStarterDeck(): SubmitDeckCommand {
  return readStarterDeckOverrideFromQuery() ?? readStarterDeckOverrideFromStorage() ?? defaultStarterDeck();
}

function defaultStarterDeck(): SubmitDeckCommand {
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

function readStarterDeckOverrideFromQuery(): SubmitDeckCommand | undefined {
  if (typeof window === "undefined") {
    return undefined;
  }

  const raw = new URLSearchParams(window.location.search).get(starterDeckOverrideQueryKey);
  return raw ? parseStarterDeckOverride(raw) : undefined;
}

function readStarterDeckOverrideFromStorage(): SubmitDeckCommand | undefined {
  if (typeof window === "undefined") {
    return undefined;
  }

  const raw = window.localStorage.getItem(starterDeckOverrideKey);
  if (!raw) {
    return undefined;
  }

  return parseStarterDeckOverride(raw);
}

function parseStarterDeckOverride(raw: string): SubmitDeckCommand | undefined {
  try {
    const parsed = JSON.parse(raw) as Partial<SubmitDeckCommand>;
    if (
      parsed.cmdType !== "SUBMIT_DECK"
      || !isNonEmptyString(parsed.legendCardNo)
      || !isNonEmptyString(parsed.championCardNo)
      || !isStringArray(parsed.mainDeck)
      || !isStringArray(parsed.runeDeck)
      || !isStringArray(parsed.battlefields)
    ) {
      return undefined;
    }

    return {
      cmdType: "SUBMIT_DECK",
      legendCardNo: parsed.legendCardNo,
      championCardNo: parsed.championCardNo,
      mainDeck: parsed.mainDeck,
      runeDeck: parsed.runeDeck,
      battlefields: parsed.battlefields
    };
  } catch {
    return undefined;
  }
}

function isNonEmptyString(value: unknown): value is string {
  return typeof value === "string" && value.length > 0;
}

function isStringArray(value: unknown): value is string[] {
  return Array.isArray(value) && value.every(isNonEmptyString);
}

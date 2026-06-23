import { SubmitDeckCommand } from "../types/protocol";
import {
  defaultStarterDeck,
  parseStarterDeckOverride,
  starterDeckOverrideKey,
  starterDeckOverrideQueryKey
} from "../utils/starterDeck";

export function buildStarterDeck(): SubmitDeckCommand {
  return readStarterDeckOverrideFromQuery() ?? readStarterDeckOverrideFromStorage() ?? defaultStarterDeck();
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

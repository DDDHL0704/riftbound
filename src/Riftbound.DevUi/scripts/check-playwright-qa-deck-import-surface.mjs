import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const qaSource = readFileSync(resolve(scriptDir, "playwright-qa.mjs"), "utf8");
const deckPageSource = readFileSync(resolve(scriptDir, "../src/pages/DecksPage.tsx"), "utf8");

assert.match(
  qaSource,
  /await\s+assertDeckImportSurface\(page\)/,
  "Playwright QA must assert the deck import surface before accepting the decks appshot."
);

assert.match(
  qaSource,
  /async\s+function\s+assertDeckImportSurface\(page\)/,
  "Playwright QA must keep deck import surface checks in a named helper."
);

for (const requiredSelector of [
  "[data-deck-import-surface]",
  "[data-deck-import-editor]",
  "[data-deck-import-input]",
  "[data-deck-import-feedback]",
  "[data-deck-import-flow-state]",
  "[data-deck-import-flow-step]",
  "[data-deck-import-handoff]",
  "[data-deck-import-handoff-section]",
  "[data-deck-import-summary]",
  "[data-deck-import-summary-metric]",
  "[data-deck-import-command-preview]",
  "[data-deck-import-action]"
]) {
  assert.ok(
    qaSource.includes(requiredSelector),
    `Playwright QA deck import helper must inspect ${requiredSelector}.`
  );
}

for (const requiredAttribute of [
  "data-deck-import-state",
  "data-deck-import-command-length",
  "data-deck-import-handoff-active-section",
  "data-deck-import-handoff-summary",
  "data-deck-import-handoff-state",
  "data-deck-import-handoff-source",
  "data-deck-import-flow-step-state",
  "data-deck-import-summary-key",
  "data-deck-import-summary-value",
  "data-deck-import-action-state"
]) {
  assert.ok(
    qaSource.includes(requiredAttribute),
    `Playwright QA deck import helper must inspect ${requiredAttribute}.`
  );
  assert.ok(
    deckPageSource.includes(requiredAttribute),
    `DecksPage must expose ${requiredAttribute} for browser QA.`
  );
}

for (const requiredText of [
  "服务端权威",
  "SUBMIT_DECK",
  "主牌堆",
  "符文牌堆",
  "战场池"
]) {
  assert.ok(
    qaSource.includes(requiredText),
    `Playwright QA deck import helper must assert ${requiredText} copy.`
  );
}

console.log("Playwright QA deck import surface check passed.");

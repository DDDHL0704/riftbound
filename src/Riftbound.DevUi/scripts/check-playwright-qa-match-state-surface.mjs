import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(resolve(scriptDir, "playwright-qa.mjs"), "utf8");
const matchPageSource = readFileSync(resolve(scriptDir, "../src/pages/MatchPage.tsx"), "utf8");

assert.match(
  source,
  /await\s+assertMatchStateSurface\(page,\s*shot\)/,
  "Playwright QA must assert seeded match score, battlefield scoring, and rule-chain surfaces before appshots are accepted."
);

assert.match(
  source,
  /async\s+function\s+assertMatchStateSurface\(page,\s*shot\)/,
  "Playwright QA must keep match-state surface checks in a named helper."
);

for (const requiredSelector of [
  ".tabletop-score-token",
  "[data-wire-battlefield-score-state]",
  "[data-wire-side-panel-rule-chain-state]",
  "[data-wire-side-panel-rule-chain-lane]",
  "[data-wire-side-panel-rule-chain-metric]",
  "[data-wire-side-panel-rule-chain-route]"
]) {
  assert.ok(
    source.includes(requiredSelector),
    `Playwright QA match-state surface helper must inspect ${requiredSelector}.`
  );
}

assert.ok(
  source.includes("data-wire-battlefield-scored-player-count"),
  "Playwright QA match-state surface helper must inspect the battlefield scored-player count attribute."
);

assert.match(
  matchPageSource,
  /data-wire-battlefield-score-state=/,
  "The main wire tabletop must expose a battlefield score state data attribute from the server snapshot."
);

assert.match(
  matchPageSource,
  /data-wire-battlefield-scored-player-count=/,
  "The main wire tabletop must expose the server scored-this-turn player count for each battlefield."
);

for (const requiredText of [
  "本回合",
  "得分",
  "规则链"
]) {
  assert.ok(
    source.includes(requiredText),
    `Playwright QA match-state surface helper must assert ${requiredText} copy.`
  );
}

console.log("Playwright QA match-state surface check passed.");

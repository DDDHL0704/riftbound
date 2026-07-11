import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(resolve(scriptDir, "playwright-qa.mjs"), "utf8");
const chromeSmokeSource = readFileSync(resolve(scriptDir, "chrome-smoke.mjs"), "utf8");
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

for (const arenaSelector of [
  "[data-arena-table]",
  "[data-arena-battlefield-region]",
  "[data-arena-hand]",
  "[data-arena-action-mode]"
]) {
  assert.ok(source.includes(arenaSelector), `Playwright QA must inspect ${arenaSelector}.`);
}

for (const arenaMetric of [
  "battlefieldHeightRatio",
  "handViewportRatio",
  "homeCardMaxHeight",
  "pileBoxMaxHeight",
  "runeCardMaxHeight",
  "hasFixedDock",
  "legalTargetOcclusions",
  "opponentNeutralLabelCount"
]) {
  assert.ok(source.includes(arenaMetric), `Playwright QA must assert ${arenaMetric}.`);
}

for (const arenaShot of ["match-wide-playable", "match-midgame-showcase", "match-compact-playable", "match-mobile-playable"]) {
  assert.ok(source.includes(arenaShot), `Playwright QA must capture ${arenaShot}.`);
}

assert.match(
  source,
  /async\s+function\s+runArenaDirectSelectionInteraction\(page,\s*report\)/,
  "Playwright QA must exercise source, position, and target selection on the arena."
);

for (const requiredSelector of [
  ".tabletop-score-token",
  "[data-wire-battlefield-score-state]",
  "[data-wire-side-panel-state-rail]",
  "[data-wire-side-panel-state-metric]",
  "[data-wire-side-panel-rule-chain-state]",
  "[data-wire-side-panel-rule-chain-lane]",
  "[data-wire-side-panel-rule-chain-metric]",
  "[data-wire-side-panel-rule-chain-route]",
  "[data-wire-side-panel-operation-state]",
  "[data-wire-side-panel-operation-section]",
  "[data-wire-side-panel-operation-route]",
  "[data-wire-side-panel-receipt]",
  "[data-match-recovery-surface]",
  "[data-match-recovery-region]"
]) {
  assert.ok(
    source.includes(requiredSelector),
    `Playwright QA match-state surface helper must inspect ${requiredSelector}.`
  );
}

for (const requiredAttribute of [
  "data-wire-side-panel-state-key",
  "data-wire-side-panel-state-source",
  "data-wire-side-panel-operation-active",
  "data-wire-side-panel-operation-ready-count",
  "data-wire-side-panel-operation-section-state",
  "data-wire-side-panel-operation-section-primary",
  "data-wire-side-panel-operation-route-state",
  "data-wire-side-panel-operation-route-slot",
  "data-wire-side-panel-receipt-mode",
  "data-wire-side-panel-receipt-state",
  "data-wire-side-panel-receipt-bridge-state",
  "data-wire-side-panel-receipt-can-open-layer",
  "data-wire-side-panel-receipt-event-count",
  "data-wire-side-panel-receipt-hidden-count",
  "data-match-recovery-active-region",
  "data-match-recovery-state",
  "data-match-recovery-summary",
  "data-match-recovery-source"
]) {
  assert.ok(
    source.includes(requiredAttribute),
    `Playwright QA match-state surface helper must inspect ${requiredAttribute}.`
  );
}

assert.match(
  chromeSmokeSource,
  /await\s+runPlayableMatchSurfaceSmoke\(cdp,\s*viewport\.label\)/,
  "Chrome smoke must run player-facing match surface assertions at every desktop viewport."
);

assert.match(
  chromeSmokeSource,
  /async\s+function\s+runPlayableMatchSurfaceSmoke\(cdp,\s*viewportLabel\)/,
  "Chrome smoke must keep player-facing match surface checks in a named helper."
);

for (const arenaSmokeMetric of [
  "battlefieldHeightRatio",
  "handViewportRatio",
  "homeCardMaxHeight",
  "pileBoxMaxHeight",
  "runeCardMaxHeight",
  "hasFixedDock",
  "legalTargetOcclusions"
]) {
  assert.ok(chromeSmokeSource.includes(arenaSmokeMetric), `Chrome smoke must assert ${arenaSmokeMetric}.`);
}

assert.match(
  chromeSmokeSource,
  /async\s+function\s+runWireSidePanelBrowserAcceptanceSmoke\(cdp,\s*viewportLabel\)/,
  "Chrome smoke must keep wire side-panel state, operation, and receipt checks in a named helper."
);

for (const requiredChromeSelector of [
  "[data-wire-side-panel-operation-state]",
  "[data-wire-side-panel-operation-section]",
  "[data-wire-side-panel-operation-route]",
  "[data-wire-side-panel-receipt]",
  "[data-match-recovery-surface]",
  "[data-match-recovery-region]"
]) {
  assert.ok(
    chromeSmokeSource.includes(requiredChromeSelector),
    `Chrome smoke match-state browser checks must inspect ${requiredChromeSelector}.`
  );
}

for (const requiredChromeAttribute of [
  "data-wire-side-panel-operation-active",
  "data-wire-side-panel-operation-ready-count",
  "data-wire-side-panel-operation-section-state",
  "data-wire-side-panel-operation-section-primary",
  "data-wire-side-panel-operation-route-state",
  "data-wire-side-panel-operation-route-slot",
  "data-wire-side-panel-receipt-mode",
  "data-wire-side-panel-receipt-state",
  "data-wire-side-panel-receipt-bridge-state",
  "data-wire-side-panel-receipt-can-open-layer",
  "data-wire-side-panel-receipt-event-count",
  "data-wire-side-panel-receipt-hidden-count",
  "data-match-recovery-active-region",
  "data-match-recovery-state",
  "data-match-recovery-summary",
  "data-match-recovery-source"
]) {
  assert.ok(
    chromeSmokeSource.includes(requiredChromeAttribute),
    `Chrome smoke match-state browser checks must inspect ${requiredChromeAttribute}.`
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
  "候选",
  "快照",
  "规则链"
]) {
  assert.ok(
    source.includes(requiredText),
    `Playwright QA match-state surface helper must assert ${requiredText} copy.`
  );
}

console.log("Playwright QA match-state surface check passed.");

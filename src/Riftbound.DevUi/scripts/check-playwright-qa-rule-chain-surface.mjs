import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const qaSource = readFileSync(resolve(scriptDir, "playwright-qa.mjs"), "utf8");
const stripSource = readFileSync(resolve(scriptDir, "../src/components/match/WireSidePanelRuleChainStrip.tsx"), "utf8");

assert.match(
  qaSource,
  /await\s+assertRuleChainBrowserSurface\(surface,\s*shot\)/,
  "Playwright QA must assert the rule-chain browser surface with a named helper."
);

assert.match(
  qaSource,
  /async\s+function\s+assertRuleChainBrowserSurface\(surface,\s*shot\)/,
  "Playwright QA must keep rule-chain browser checks in a named helper."
);

for (const requiredSelector of [
  "[data-wire-side-panel-rule-chain-state]",
  "[data-wire-side-panel-rule-chain-lane]",
  "[data-wire-side-panel-rule-chain-metric]",
  "[data-wire-side-panel-rule-chain-route]"
]) {
  assert.ok(
    qaSource.includes(requiredSelector),
    `Playwright QA rule-chain helper must inspect ${requiredSelector}.`
  );
}

for (const requiredAttribute of [
  "data-wire-side-panel-rule-chain-active-lane",
  "data-wire-side-panel-rule-chain-lane-count",
  "data-wire-side-panel-rule-chain-lane-state",
  "data-wire-side-panel-rule-chain-route-state",
  "data-wire-side-panel-rule-chain-route-slot"
]) {
  assert.ok(
    qaSource.includes(requiredAttribute),
    `Playwright QA rule-chain helper must inspect ${requiredAttribute}.`
  );
  assert.ok(
    stripSource.includes(requiredAttribute),
    `WireSidePanelRuleChainStrip must expose ${requiredAttribute} for browser QA.`
  );
}

for (const laneKey of ["stack", "task", "trigger", "resolution"]) {
  assert.ok(
    qaSource.includes(laneKey),
    `Playwright QA rule-chain helper must assert ${laneKey} lane coverage.`
  );
}

for (const routeSlot of ["ruleQueue", "serverFlow", "timelineDetail", "log"]) {
  assert.ok(
    qaSource.includes(routeSlot),
    `Playwright QA rule-chain helper must assert ${routeSlot} route coverage.`
  );
}

for (const requiredCopy of ["结算链", "规则任务", "触发队列", "近期事件", "下一步"]) {
  assert.ok(
    qaSource.includes(requiredCopy),
    `Playwright QA rule-chain helper must assert ${requiredCopy} copy.`
  );
}

console.log("Playwright QA rule-chain surface check passed.");

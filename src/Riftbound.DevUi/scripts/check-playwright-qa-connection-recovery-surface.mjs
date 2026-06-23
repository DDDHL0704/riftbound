import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const qaSource = readFileSync(resolve(scriptDir, "playwright-qa.mjs"), "utf8");
const chromeSmokeSource = readFileSync(resolve(scriptDir, "chrome-smoke.mjs"), "utf8");
const panelSource = readFileSync(resolve(scriptDir, "../src/components/match/ConnectionRecoveryPanel.tsx"), "utf8");

assert.match(
  qaSource,
  /async\s+function\s+assertConnectionRecoveryPanelSurface\(page,\s*expectedSurface\)/,
  "Playwright QA must keep connection recovery panel checks in a named helper."
);

for (const expectedCall of [
  /await\s+assertConnectionRecoveryPanelSurface\(page,\s*"room"\)/,
  /await\s+assertConnectionRecoveryPanelSurface\(page,\s*"match"\)/
]) {
  assert.match(
    qaSource,
    expectedCall,
    "Playwright QA must assert connection recovery panels on both room and match surfaces."
  );
}

for (const requiredSelector of [
  "[data-connection-recovery-panel]",
  "[data-connection-recovery-action]"
]) {
  assert.ok(
    qaSource.includes(requiredSelector),
    `Playwright QA connection recovery helper must inspect ${requiredSelector}.`
  );
}

for (const requiredAttribute of [
  "data-connection-recovery-state",
  "data-connection-recovery-surface",
  "data-connection-recovery-tick-label",
  "data-connection-recovery-action",
  "data-connection-recovery-action-disabled",
  "data-connection-recovery-action-state"
]) {
  assert.ok(
    qaSource.includes(requiredAttribute),
    `Playwright QA connection recovery helper must inspect ${requiredAttribute}.`
  );
  assert.ok(
    chromeSmokeSource.includes(requiredAttribute),
    `Chrome smoke must inspect ${requiredAttribute}.`
  );
  assert.ok(
    panelSource.includes(requiredAttribute),
    `ConnectionRecoveryPanel must expose ${requiredAttribute} for browser QA.`
  );
}

for (const requiredAction of ["connect", "resync", "disconnect"]) {
  assert.ok(
    qaSource.includes(requiredAction),
    `Playwright QA connection recovery helper must assert ${requiredAction} action wiring.`
  );
}

for (const requiredCopy of ["连接恢复", "连接恢复操作"]) {
  assert.ok(
    qaSource.includes(requiredCopy),
    `Playwright QA connection recovery helper must assert ${requiredCopy} copy.`
  );
}

console.log("Playwright QA connection recovery surface check passed.");

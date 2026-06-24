import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const qaSource = readFileSync(resolve(scriptDir, "playwright-qa.mjs"), "utf8");
const chromeSmokeSource = readFileSync(resolve(scriptDir, "chrome-smoke.mjs"), "utf8");
const resultPageSource = readFileSync(resolve(scriptDir, "../src/pages/ResultPage.tsx"), "utf8");

assert.match(
  qaSource,
  /await\s+assertResultSurface\(page\)/,
  "Playwright QA must assert the result surface before accepting the result appshot."
);

assert.match(
  qaSource,
  /async\s+function\s+assertResultSurface\(page\)/,
  "Playwright QA must keep result surface checks in a named helper."
);

assert.match(
  chromeSmokeSource,
  /await\s+runResultPageSurfaceSmoke\(cdp\)/,
  "Chrome smoke must run result page browser assertions on the result route."
);

assert.match(
  chromeSmokeSource,
  /async\s+function\s+runResultPageSurfaceSmoke\(cdp\)/,
  "Chrome smoke must keep result page surface checks in a named helper."
);

for (const requiredSelector of [
  "[data-result-surface]",
  "[data-result-final-state]",
  "[data-result-action]",
  "[data-result-player-score]",
  "[data-result-event-summary]",
  "[data-result-error-summary]",
  "[data-result-log-entry]",
  "[data-result-return-path]"
]) {
  assert.ok(
    qaSource.includes(requiredSelector),
    `Playwright QA result helper must inspect ${requiredSelector}.`
  );
  assert.ok(
    chromeSmokeSource.includes(requiredSelector),
    `Chrome smoke result helper must inspect ${requiredSelector}.`
  );
}

for (const requiredAttribute of [
  "data-result-state",
  "data-result-authority",
  "data-result-room-status",
  "data-result-match-id",
  "data-result-player-id",
  "data-result-snapshot-tick",
  "data-result-winner-player-id",
  "data-result-has-snapshot",
  "data-result-action-route",
  "data-result-action-state",
  "data-result-player-id",
  "data-result-player-score",
  "data-result-player-winner",
  "data-result-event-count",
  "data-result-error-count",
  "data-result-log-kind",
  "data-result-return-action-count"
]) {
  assert.ok(
    qaSource.includes(requiredAttribute),
    `Playwright QA result helper must inspect ${requiredAttribute}.`
  );
  assert.ok(
    chromeSmokeSource.includes(requiredAttribute),
    `Chrome smoke result helper must inspect ${requiredAttribute}.`
  );
  assert.ok(
    resultPageSource.includes(requiredAttribute),
    `ResultPage must expose ${requiredAttribute} for browser QA.`
  );
}

for (const requiredCopy of [
  "服务端权威",
  "结果只读取服务端权威快照",
  "连接状态",
  "返回房间",
  "查看对战桌面",
  "事件 / 错误"
]) {
  assert.ok(
    qaSource.includes(requiredCopy),
    `Playwright QA result helper must assert ${requiredCopy} copy.`
  );
  assert.ok(
    chromeSmokeSource.includes(requiredCopy),
    `Chrome smoke result helper must assert ${requiredCopy} copy.`
  );
}

console.log("Playwright QA result surface check passed.");

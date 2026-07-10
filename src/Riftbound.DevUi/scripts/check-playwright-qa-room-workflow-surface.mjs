import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const qaSource = readFileSync(resolve(scriptDir, "playwright-qa.mjs"), "utf8");
const roomPageSource = readFileSync(resolve(scriptDir, "../src/pages/RoomPage.tsx"), "utf8");

assert.match(
  qaSource,
  /await\s+assertRoomWorkflowSurface\(page\)/,
  "Playwright QA must assert the room workflow surface before accepting the room appshot."
);

assert.match(
  qaSource,
  /async\s+function\s+assertRoomWorkflowSurface\(page\)/,
  "Playwright QA must keep room workflow surface checks in a named helper."
);

for (const requiredSelector of [
  "[data-room-workflow-surface]",
  "[data-room-workflow-region]",
  "[data-room-recovery-region]",
  "[data-room-actions-region]",
  "[data-room-quick-action]",
  "[data-room-setup-region]",
  "[data-room-setup-step]",
  "[data-room-submission-region]",
  "[data-room-errors-region]",
  "[data-error-resolution-action]",
  "[data-error-resolution-evidence-row]",
  "[data-error-resolution-next-step]",
  "[data-room-log-region]"
]) {
  assert.ok(
    qaSource.includes(requiredSelector),
    `Playwright QA room workflow helper must inspect ${requiredSelector}.`
  );
}

for (const requiredAttribute of [
  "data-room-workflow-active-region",
  "data-room-workflow-summary",
  "data-room-workflow-source",
  "data-room-workflow-state",
  "data-room-quick-action-state",
  "data-room-quick-action-command-source",
  "data-room-submission-state",
  "data-error-resolution-state",
  "data-error-resolution-action-state",
  "data-error-resolution-action-disabled",
  "data-error-resolution-evidence-label",
  "data-error-resolution-evidence-value"
]) {
  assert.ok(
    qaSource.includes(requiredAttribute),
    `Playwright QA room workflow helper must inspect ${requiredAttribute}.`
  );
  assert.ok(
    roomPageSource.includes(requiredAttribute),
    `RoomPage must expose ${requiredAttribute} for browser QA.`
  );
}

for (const requiredText of [
  "服务端连接",
  "卡组提交",
  "提交回执",
  "错误处理",
  "下一步",
  "连接状态",
  "错误来源",
  "服务端消息"
]) {
  assert.ok(
    qaSource.includes(requiredText),
    `Playwright QA room workflow helper must assert ${requiredText} copy.`
  );
}

console.log("Playwright QA room workflow surface check passed.");

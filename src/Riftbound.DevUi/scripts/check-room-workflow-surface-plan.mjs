import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const srcRoot = resolve(scriptDir, "../src");
const moduleCache = new Map();

const roomPageSource = readFileSync(resolve(srcRoot, "pages/RoomPage.tsx"), "utf8");
const packageJson = JSON.parse(readFileSync(resolve(scriptDir, "../package.json"), "utf8"));
const globalsCss = readFileSync(resolve(srcRoot, "styles/globals.css"), "utf8");
const { buildRoomWorkflowSurfacePlan } = loadTsModule(resolve(srcRoot, "utils/roomWorkflowSurfacePlan.ts")).exports;

const disconnected = buildRoomWorkflowSurfacePlan({
  connectionStatus: "disconnected",
  errorCount: 0,
  errorState: "clear",
  eventCount: 0,
  hasSnapshot: false,
  quickActions: [],
  roomStatus: "",
  setupGate: { label: "连接阻塞", nextStep: "连接/重连并入座。", reason: "连接状态：已断开，暂不提交行动。", tone: "bad" },
  submissionState: undefined
});
assert.deepEqual(disconnected.sections.map((section) => section.id), ["recovery", "setup", "actions", "submission", "errors", "log"]);
assert.equal(disconnected.activeRegionId, "recovery");
assert.equal(disconnected.sections.find((section) => section.id === "recovery")?.source, "server-connection");
assert.equal(disconnected.sections.find((section) => section.id === "recovery")?.state, "blocking");
assert.equal(disconnected.sections.find((section) => section.id === "actions")?.value, "0/0");
assert.equal(disconnected.summary, "连接：已断开 / 开局：连接阻塞 / 行动：0/0");

const readyRoom = buildRoomWorkflowSurfacePlan({
  connectionStatus: "connected",
  errorCount: 0,
  errorState: "clear",
  eventCount: 3,
  hasSnapshot: true,
  quickActions: [
    { id: "submitDeck", state: "ready" },
    { id: "ready", state: "blocked" }
  ],
  roomStatus: "IN_PROGRESS",
  setupGate: { label: "已开局", nextStep: "进入对战桌面。", reason: "服务端房间状态已进入对局进行中。", tone: "good" },
  submissionState: "sent"
});
assert.equal(readyRoom.activeRegionId, "actions");
assert.equal(readyRoom.sections.find((section) => section.id === "actions")?.state, "ready");
assert.equal(readyRoom.sections.find((section) => section.id === "actions")?.value, "1/2");
assert.equal(readyRoom.sections.find((section) => section.id === "submission")?.state, "ready");
assert.equal(readyRoom.sections.find((section) => section.id === "log")?.value, "3 事件 / 0 错误");
assert.ok(readyRoom.summary.includes("开局：已开局"));

const failedSubmission = buildRoomWorkflowSurfacePlan({
  connectionStatus: "connected",
  errorCount: 1,
  errorState: "input",
  eventCount: 2,
  hasSnapshot: true,
  quickActions: [{ id: "submitDeck", state: "ready" }],
  roomStatus: "",
  setupGate: { label: "等待卡组", nextStep: "缺卡组的玩家提交构筑。", reason: "服务端快照仅确认 1/2 份卡组。", tone: "warn" },
  submissionState: "failed"
});
assert.equal(failedSubmission.activeRegionId, "errors");
assert.equal(failedSubmission.sections.find((section) => section.id === "errors")?.state, "blocking");
assert.equal(failedSubmission.sections.find((section) => section.id === "submission")?.state, "blocking");

assert.ok(roomPageSource.includes("buildRoomWorkflowSurfacePlan"), "RoomPage must build the workflow surface from server-derived plans.");
assert.ok(roomPageSource.includes("data-room-workflow-surface"), "RoomPage must expose the workflow surface for browser smoke.");
assert.ok(roomPageSource.includes("data-room-workflow-region"), "RoomPage must expose each workflow region for browser smoke.");
assert.ok(roomPageSource.includes("data-room-workflow-source"), "RoomPage must expose server source labels.");
assert.ok(roomPageSource.includes("data-room-recovery-region"), "RoomPage must mark the connection recovery region.");
assert.ok(roomPageSource.includes("data-room-setup-region"), "RoomPage must mark the setup checklist region.");
assert.ok(roomPageSource.includes("data-room-actions-region"), "RoomPage must mark the quick action region.");
assert.ok(roomPageSource.includes("data-room-submission-region"), "RoomPage must mark the submission receipt region.");
assert.ok(roomPageSource.includes("data-room-errors-region"), "RoomPage must mark the error resolution region.");
assert.ok(roomPageSource.includes("data-room-log-region"), "RoomPage must mark the log region.");
assert.ok(globalsCss.includes(".room-workflow-surface"), "Room workflow surface styles must be present.");
assert.ok(globalsCss.includes(".room-workflow-region"), "Room workflow region styles must be present.");
assert.ok(packageJson.scripts["check:room-workflow-surface-plan"], "Package scripts must expose the room workflow surface check.");
assert.ok(packageJson.scripts.build.includes("check:room-workflow-surface-plan"), "Build must run the room workflow surface check.");

console.log("Room workflow surface plan check passed.");

function loadTsModule(filename) {
  const resolved = resolve(filename);
  const cached = moduleCache.get(resolved);
  if (cached) {
    return cached;
  }

  const source = readFileSync(resolved, "utf8");
  const output = ts.transpileModule(source, {
    compilerOptions: {
      esModuleInterop: true,
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022
    }
  }).outputText;
  const module = { exports: {} };
  moduleCache.set(resolved, module);

  const requireShim = (id) => {
    if (id.startsWith(".")) {
      const target = resolve(dirname(resolved), id);
      if (target.endsWith("/types/protocol") || target.endsWith("/types/catalog")) {
        return {};
      }

      return loadTsModule(`${target}.ts`).exports;
    }

    throw new Error(`Unexpected import in room workflow surface check: ${id}`);
  };

  new Function("exports", "module", "require", output)(module.exports, module, requireShim);
  return module;
}

import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const srcRoot = resolve(scriptDir, "../src");
const moduleCache = new Map();

const { buildRoomSetupFlowPlan } = loadTsModule(resolve(srcRoot, "utils/roomSetupFlowPlan.ts")).exports;

const disconnected = buildRoomSetupFlowPlan({
  connectionStatus: "disconnected",
  players: [],
  quickActions: [],
  roomStatus: "",
  submissionGateReason: "连接状态：已断开，暂不提交行动。"
});
assert.equal(disconnected.startGate.label, "连接阻塞");
assert.equal(disconnected.startGate.tone, "bad");
assert.equal(disconnected.steps[0].stateLabel, "已断开");
assert.equal(disconnected.steps[0].nextStep, "连接/重连并入座。");
assert.equal(disconnected.steps[1].detail, "阻塞：当前玩家尚未出现在服务端快照中。");

const oneSeated = buildRoomSetupFlowPlan({
  connectionStatus: "connected",
  currentPlayer: { deckSubmitted: false, ready: false, seat: "P1" },
  players: [{ deckSubmitted: false, ready: false, seat: "P1" }],
  quickActions: [{ id: "submitDeck", state: "ready", title: "可提交构筑" }, { id: "ready", state: "missing", title: "缺 READY" }],
  roomStatus: "",
  submissionGateReason: "可提交"
});
assert.equal(oneSeated.startGate.label, "等待入座");
assert.equal(oneSeated.steps[1].stateLabel, "1/2 人");
assert.equal(oneSeated.steps[2].nextStep, "点击导入构筑并等待服务端回执。");
assert.equal(oneSeated.steps[3].nextStep, "等待服务端提供 READY 候选。");

const waitingStart = buildRoomSetupFlowPlan({
  connectionStatus: "connected",
  currentPlayer: { deckSubmitted: true, ready: true, seat: "P1" },
  players: [
    { deckSubmitted: true, ready: true, seat: "P1" },
    { deckSubmitted: true, ready: true, seat: "P2" }
  ],
  quickActions: [],
  roomStatus: "",
  submissionGateReason: "可提交"
});
assert.equal(waitingStart.startGate.label, "等待开局");
assert.equal(waitingStart.startGate.tone, "info");
assert.equal(waitingStart.steps[4].detail, "状态：入座、卡组与准备均已满足，当前仍未收到 IN_PROGRESS 状态。");

const inProgress = buildRoomSetupFlowPlan({
  connectionStatus: "connected",
  currentPlayer: { deckSubmitted: true, ready: true, seat: "P1" },
  players: [
    { deckSubmitted: true, ready: true, seat: "P1" },
    { deckSubmitted: true, ready: true, seat: "P2" }
  ],
  quickActions: [],
  roomStatus: "IN_PROGRESS",
  submissionGateReason: "可提交"
});
assert.equal(inProgress.startGate.label, "已开局");
assert.equal(inProgress.startGate.nextStep, "进入对战桌面。");
assert.equal(inProgress.steps[4].stateLabel, "已开局");

console.log("Room setup flow plan check passed.");

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

    throw new Error(`Unexpected import in room setup flow plan check: ${id}`);
  };

  new Function("exports", "module", "require", output)(module.exports, module, requireShim);
  return module;
}

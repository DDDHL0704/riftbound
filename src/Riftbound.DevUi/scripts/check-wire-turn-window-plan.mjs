import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/wireTurnWindowPlan.ts");
const source = readFileSync(sourcePath, "utf8");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function("exports", "module", output)(moduleShim.exports, moduleShim);

const { buildWireTurnWindowPlan } = moduleShim.exports;

const actionable = buildWireTurnWindowPlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: {
    actionable: true,
    actions: ["PLAY_CARD"],
    candidates: [
      { action: "PLAY_CARD", enabled: true, label: "打出卡牌", reason: "可提交" },
      { action: "ACTIVATE_ABILITY", enabled: false, label: "激活技能", reason: "暂无窗口" }
    ],
    playerId: "P1",
    promptId: "prompt-1",
    reason: "可操作",
    view: {
      message: "请选择行动",
      title: "主行动窗口",
      type: "MAIN_ACTION"
    }
  },
  snapshot: {
    activePlayerId: "P1",
    lanes: {},
    players: {},
    stack: [],
    tick: 10,
    timing: {
      phase: "MAIN",
      roomStatus: "IN_PROGRESS",
      turnWindow: { actingPlayerId: "P1", state: "NEUTRAL_OPEN" }
    },
    turnNumber: 2,
    turnState: "MAIN"
  }
});

assert.equal(actionable.state, "you-action");
assert.equal(actionable.tone, "good");
assert.equal(actionable.enabledCandidateCount, 1);
assert.equal(actionable.metrics.find((metric) => metric.key === "prompt")?.mine, true);
assert.ok(actionable.nextStepLabel.includes("服务端候选"));

const actionableWithStack = buildWireTurnWindowPlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: {
    actionable: true,
    actions: ["PASS_PRIORITY"],
    candidates: [{ action: "PASS_PRIORITY", enabled: true, label: "让过优先行动权", reason: "可提交" }],
    playerId: "P1",
    reason: "等待响应"
  },
  snapshot: {
    activePlayerId: "P1",
    lanes: {},
    players: {},
    stack: [{ stackItemId: "stack-1", controllerId: "P2" }],
    tick: 10,
    timing: {
      turnWindow: { actingPlayerId: "P1", state: "NEUTRAL_OPEN" }
    },
    turnNumber: 2,
    turnState: "MAIN"
  }
});

assert.equal(actionableWithStack.state, "you-action");
assert.equal(actionableWithStack.stackCount, 1);
assert.ok(actionableWithStack.nextStepLabel.includes("服务端候选"));

const resolving = buildWireTurnWindowPlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: {
    actionable: false,
    actions: ["WAIT"],
    candidates: [],
    playerId: "P1",
    reason: "等待规则任务"
  },
  snapshot: {
    activePlayerId: "P2",
    lanes: {},
    players: {},
    stack: [{ stackItemId: "stack-1", controllerId: "P2" }],
    tick: 11,
    timing: {
      pendingTaskQueue: {
        isBlocking: true,
        tasks: [{ taskId: "task-1", kind: "START_BATTLE" }]
      },
      triggerQueue: [{ triggerId: "trigger-1" }],
      turnWindow: { actingPlayerId: "P2", state: "NEUTRAL_CLOSED" }
    },
    turnNumber: 2,
    turnState: "MAIN"
  }
});

assert.equal(resolving.state, "resolving");
assert.equal(resolving.tone, "warn");
assert.equal(resolving.blockingTaskCount, 1);
assert.equal(resolving.stackCount, 1);
assert.equal(resolving.triggerCount, 1);
assert.ok(resolving.nextStepLabel.includes("规则任务"));

const disconnected = buildWireTurnWindowPlan({
  connectionStatus: "reconnecting",
  playerId: "P1",
  snapshot: {
    activePlayerId: "P1",
    lanes: {},
    players: {},
    stack: [],
    tick: 12,
    timing: {},
    turnNumber: 2,
    turnState: "MAIN"
  }
});

assert.equal(disconnected.state, "disconnected");
assert.equal(disconnected.tone, "bad");
assert.ok(disconnected.nextStepLabel.includes("恢复连接"));

console.log("Wire turn window plan check passed.");

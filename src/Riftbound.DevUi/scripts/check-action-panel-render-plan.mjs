import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/actionPanelRenderPlan.ts");
const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function("exports", "module", output)(moduleShim.exports, moduleShim);

const { buildActionPanelRenderPlan } = moduleShim.exports;

const emptyPlan = buildActionPanelRenderPlan({
  canAct: false,
  connected: true
});

assert.equal(emptyPlan.state, "empty");
assert.equal(emptyPlan.promptType, "无");
assert.equal(emptyPlan.entries.length, 0);
assert.equal(emptyPlan.emptyLabel, "服务端暂未提供可提交候选。");

const mainPlan = buildActionPanelRenderPlan({
  canAct: true,
  connected: true,
  prompt: {
    actionable: true,
    candidates: [
      { action: "TAP_RUNE", enabled: true, label: "横置符文", presentation: { category: "resource", intent: "tap-rune", priority: 180, uiHint: "resource" } },
      { action: "ACTIVATE_ABILITY", enabled: false, label: "禁用能力", presentation: { category: "ability", intent: "activate-ability", priority: 10, uiHint: "card-action" } },
      { action: "ORDER_TRIGGERS", enabled: true, label: "排列触发", presentation: { category: "choice", intent: "order-triggers", priority: 60, uiHint: "choice" } },
      { action: "MULLIGAN", enabled: true, label: "起手调整", presentation: { category: "setup", intent: "mulligan", priority: 30, uiHint: "primary" } },
      { action: "PAY_COST", enabled: true, label: "支付费用", presentation: { category: "payment", intent: "pay-cost", priority: 35, uiHint: "payment" } },
      { action: "CHOOSE_HAND_CARDS", enabled: true, label: "选择手牌", presentation: { category: "choice", intent: "choose-hand", priority: 40, uiHint: "choice" } },
      { action: "ASSIGN_COMBAT_DAMAGE", enabled: true, label: "分配伤害", presentation: { category: "battle", intent: "assign-damage", priority: 50, uiHint: "battle" } },
      { action: "DECLARE_BATTLE", enabled: true, label: "声明战斗", presentation: { category: "battle", intent: "declare-battle", priority: 55, uiHint: "battle" } }
    ],
    playerId: "P1",
    view: { type: "MAIN_ACTION" }
  }
});

assert.equal(mainPlan.state, "ready");
assert.equal(mainPlan.promptType, "MAIN_ACTION");
assert.deepEqual(mainPlan.entries.map((entry) => entry.kind), [
  "mulligan",
  "pay-cost",
  "hand-choice",
  "damage-assignment",
  "battle-declaration",
  "order-triggers",
  "candidate-button",
  "candidate-button"
]);
assert.deepEqual(mainPlan.entries.map((entry) => entry.candidate?.action), [
  "MULLIGAN",
  "PAY_COST",
  "CHOOSE_HAND_CARDS",
  "ASSIGN_COMBAT_DAMAGE",
  "DECLARE_BATTLE",
  "ORDER_TRIGGERS",
  "TAP_RUNE",
  "ACTIVATE_ABILITY"
]);
assert.equal(mainPlan.entries.some((entry) => entry.candidate?.action === "ACTIVATE_ABILITY"), true);
assert.equal(mainPlan.entries.find((entry) => entry.candidate?.action === "ACTIVATE_ABILITY")?.canAct, false);
assert.equal(mainPlan.entries.find((entry) => entry.candidate?.action === "ACTIVATE_ABILITY")?.submitGate.state, "server-blocked");
assert.equal(mainPlan.entries.find((entry) => entry.candidate?.action === "ACTIVATE_ABILITY")?.submitGate.stateLabel, "服务端阻断");
assert.equal(mainPlan.entries.filter((entry) => entry.canAct).length, 7);
assert.equal(mainPlan.entries.filter((entry) => entry.submitGate.state === "ready").length, 7);

const readonlyHandPlan = buildActionPanelRenderPlan({
  canAct: false,
  connected: true,
  prompt: {
    actionable: false,
    candidates: [
      { action: "CHOOSE_HAND_CARDS", enabled: false, label: "选择手牌" }
    ],
    playerId: "P1",
    view: { type: "HAND_CHOICE" }
  }
});

assert.equal(readonlyHandPlan.state, "readonly");
assert.equal(readonlyHandPlan.entries.length, 1);
assert.equal(readonlyHandPlan.entries[0].kind, "hand-choice");
assert.equal(readonlyHandPlan.entries[0].readOnly, false);
assert.equal(readonlyHandPlan.entries[0].canAct, false);
assert.equal(readonlyHandPlan.entries[0].submitGate.state, "window-blocked");

const readonlyTriggerPlan = buildActionPanelRenderPlan({
  canAct: false,
  connected: true,
  prompt: {
    actionable: false,
    candidates: [],
    playerId: "P1",
    view: { type: "ORDER_TRIGGERS" }
  }
});

assert.equal(readonlyTriggerPlan.state, "readonly");
assert.equal(readonlyTriggerPlan.entries.length, 1);
assert.equal(readonlyTriggerPlan.entries[0].key, "readonly-order-triggers-prompt");
assert.equal(readonlyTriggerPlan.entries[0].kind, "order-triggers");
assert.equal(readonlyTriggerPlan.entries[0].submitGate.state, "readonly");

const disconnectedPlan = buildActionPanelRenderPlan({
  canAct: true,
  connected: false,
  prompt: {
    actionable: true,
    candidates: [
      { action: "PASS", enabled: true, label: "让过" }
    ],
    playerId: "P1",
    view: { type: "PRIORITY" }
  }
});

assert.equal(disconnectedPlan.state, "disabled");
assert.equal(disconnectedPlan.entries.length, 1);
assert.equal(disconnectedPlan.entries[0].kind, "candidate-button");
assert.equal(disconnectedPlan.entries[0].canAct, false);
assert.equal(disconnectedPlan.entries[0].submitGate.state, "submission-gate-blocked");
assert.equal(disconnectedPlan.entries[0].submitGate.stateLabel, "入口未就绪");

const blockedPlan = buildActionPanelRenderPlan({
  canAct: true,
  connected: true,
  prompt: {
    actionable: true,
    candidates: [
      { action: "ACTIVATE_ABILITY", enabled: false, label: "禁用能力", reason: "法力不足" },
      { action: "MOVE_UNIT", enabled: false, label: "禁用移动", reason: "时机不允许" }
    ],
    playerId: "P1",
    view: { type: "MAIN_ACTION" }
  }
});

assert.equal(blockedPlan.state, "blocked");
assert.equal(blockedPlan.entries.length, 2);
assert.equal(blockedPlan.entries.every((entry) => entry.kind === "candidate-button"), true);
assert.equal(blockedPlan.entries.every((entry) => entry.canAct === false), true);
assert.equal(blockedPlan.entries.every((entry) => entry.submitGate.state === "server-blocked"), true);
assert.equal(blockedPlan.entries[0].submitGate.reason, "法力不足");

const staleSnapshotPlan = buildActionPanelRenderPlan({
  canAct: true,
  prompt: {
    actionable: true,
    candidates: [
      { action: "PASS", enabled: true, label: "让过" }
    ],
    playerId: "P1",
    view: { type: "PRIORITY" }
  },
  submissionGate: {
    canSubmit: false,
    reason: "行动提示属于 tick 12，当前桌面快照是 tick 11。",
    stateLabel: "等待同步"
  }
});

assert.equal(staleSnapshotPlan.state, "disabled");
assert.equal(staleSnapshotPlan.entries[0].submitGate.state, "submission-gate-blocked");
assert.equal(staleSnapshotPlan.entries[0].submitGate.reason, "行动提示属于 tick 12，当前桌面快照是 tick 11。");

const windowBlockedPlan = buildActionPanelRenderPlan({
  canAct: false,
  connected: true,
  prompt: {
    actionable: false,
    candidates: [
      { action: "PASS", enabled: true, label: "让过", reason: "等待对手" }
    ],
    playerId: "P1",
    view: { type: "PRIORITY" }
  }
});

assert.equal(windowBlockedPlan.state, "readonly");
assert.equal(windowBlockedPlan.entries[0].submitGate.state, "window-blocked");
assert.equal(windowBlockedPlan.entries[0].submitGate.reason, "当前行动窗口不能提交该候选。");

console.log("Action panel render plan check passed.");

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
      { action: "MULLIGAN", enabled: true, label: "起手调整" },
      { action: "CHOOSE_HAND_CARDS", enabled: true, label: "选择手牌" },
      { action: "ASSIGN_COMBAT_DAMAGE", enabled: true, label: "分配伤害" },
      { action: "ORDER_TRIGGERS", enabled: true, label: "排列触发" },
      { action: "TAP_RUNE", enabled: true, label: "横置符文" },
      { action: "ACTIVATE_ABILITY", enabled: false, label: "禁用能力" }
    ],
    playerId: "P1",
    view: { type: "MAIN_ACTION" }
  }
});

assert.equal(mainPlan.state, "ready");
assert.equal(mainPlan.promptType, "MAIN_ACTION");
assert.deepEqual(mainPlan.entries.map((entry) => entry.kind), [
  "mulligan",
  "hand-choice",
  "damage-assignment",
  "order-triggers",
  "candidate-button",
  "candidate-button"
]);
assert.equal(mainPlan.entries.some((entry) => entry.candidate?.action === "ACTIVATE_ABILITY"), true);
assert.equal(mainPlan.entries.find((entry) => entry.candidate?.action === "ACTIVATE_ABILITY")?.canAct, false);
assert.equal(mainPlan.entries.filter((entry) => entry.canAct).length, 5);

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

console.log("Action panel render plan check passed.");

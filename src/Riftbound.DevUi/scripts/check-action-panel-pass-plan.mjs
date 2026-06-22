import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/actionPanelPassPlan.ts");
const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function("exports", "module", output)(moduleShim.exports, moduleShim);

const { buildActionPanelPassPlan } = moduleShim.exports;

const mainPass = buildActionPanelPassPlan({
  action: "PASS",
  commandTemplate: { bindings: [], cmdType: "PASS" },
  enabled: true,
  label: "让过",
  reason: "可让过"
}, {
  prompt: {
    actionable: true,
    candidates: [],
    playerId: "P1",
    view: { message: "普通行动窗口", type: "MAIN_ACTION" }
  }
});

assert.equal(mainPass.mode, "main-window");
assert.equal(mainPass.state, "ready");
assert.equal(mainPass.statusLabel, "可让过");
assert.equal(mainPass.windowLabel, "行动窗口");
assert.equal(mainPass.stackCount, 0);
assert.equal(mainPass.passedCount, 0);
assert.equal(mainPass.commandFieldCount, 0);

const stackPass = buildActionPanelPassPlan({
  action: "PASS_PRIORITY",
  commandTemplate: { bindings: [], cmdType: "PASS_PRIORITY" },
  enabled: true,
  label: "让过优先权",
  reason: "可让过"
}, {
  prompt: {
    actionable: true,
    candidates: [],
    playerId: "P2",
    view: {
      responsibility: {
        nextStep: "响应顶部项目或让过。",
        promptPlayerId: "P2",
        promptType: "STACK_PRIORITY",
        responsiblePlayerId: "P2"
      },
      type: "STACK_PRIORITY"
    }
  },
  snapshot: {
    players: {},
    stack: [{ stackItemId: "stack-1" }, { stackItemId: "stack-2" }],
    tick: 3,
    timing: {
      passedPriorityPlayerIds: ["P1"],
      turnWindow: { actingPlayerId: "P2" }
    }
  }
});

assert.equal(stackPass.mode, "stack-priority");
assert.equal(stackPass.statusLabel, "可让过优先权");
assert.equal(stackPass.windowLabel, "结算链优先权");
assert.equal(stackPass.stackCount, 2);
assert.equal(stackPass.passedCount, 1);
assert.equal(stackPass.metricRows.find((row) => row.key === "responsible")?.value, "P2");
assert.ok(stackPass.authorityLabel.includes("服务端"));

const focusPass = buildActionPanelPassPlan({
  action: "PASS_FOCUS",
  enabled: false,
  label: "让过焦点",
  reason: "等待窗口"
}, {
  prompt: {
    actionable: false,
    candidates: [],
    playerId: "P1",
    view: { type: "SPELL_DUEL_FOCUS" }
  },
  snapshot: {
    players: {},
    tick: 4,
    timing: {
      spellDuel: {
        focusPlayerId: "P1",
        passedFocusPlayerIds: ["P2"]
      }
    }
  }
});

assert.equal(focusPass.mode, "spell-duel");
assert.equal(focusPass.state, "blocked");
assert.equal(focusPass.statusLabel, "暂不可让过焦点");
assert.equal(focusPass.windowLabel, "法术对决焦点");
assert.equal(focusPass.passedCount, 1);
assert.equal(focusPass.metricRows.find((row) => row.key === "responsible")?.value, "P1");

console.log("Action panel pass plan check passed.");

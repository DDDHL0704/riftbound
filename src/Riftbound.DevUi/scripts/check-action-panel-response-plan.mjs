import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/actionPanelResponsePlan.ts");
const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function("exports", "module", output)(moduleShim.exports, moduleShim);

const { buildActionPanelResponsePlan } = moduleShim.exports;

const stackResponse = buildActionPanelResponsePlan({
  action: "RESPOND",
  enabled: true,
  label: "响应结算链",
  reason: "服务端候选可用",
  selectionSteps: [
    { choices: [{ id: "spell-1", label: "反应牌", objectIds: ["spell-1"] }], label: "来源", required: true, role: "source" },
    { choices: [{ id: "unit-1", label: "目标单位", objectIds: ["unit-1"] }], label: "目标", required: false, role: "target" }
  ],
  sources: [{ id: "spell-1", label: "反应牌", objectIds: ["spell-1"] }],
  targets: [{ id: "unit-1", label: "目标单位", objectIds: ["unit-1"] }]
}, {
  prompt: {
    actionable: true,
    candidates: [],
    playerId: "P1",
    view: {
      message: "响应顶部结算项目。",
      type: "STACK_PRIORITY"
    }
  },
  snapshot: {
    players: {},
    stack: [{ stackItemId: "stack-1" }],
    tick: 8
  }
});

assert.equal(stackResponse.mode, "stack-priority");
assert.equal(stackResponse.state, "ready");
assert.equal(stackResponse.statusLabel, "可响应");
assert.equal(stackResponse.windowLabel, "结算链响应");
assert.equal(stackResponse.stackCount, 1);
assert.equal(stackResponse.selectionStepCount, 2);
assert.equal(stackResponse.commandFieldCount, 0);
assert.equal(stackResponse.metricRows.find((row) => row.key === "choice")?.value, "2 项");
assert.ok(stackResponse.authorityLabel.includes("服务端"));

const spellDuelResponse = buildActionPanelResponsePlan({
  action: "RESPOND",
  commandTemplate: {
    bindings: [{ field: "sourceObjectId", required: true, source: "selectedSource" }],
    cmdType: "PLAY_CARD"
  },
  enabled: false,
  label: "响应法术对决",
  reason: "等待焦点"
}, {
  prompt: {
    actionable: false,
    candidates: [],
    playerId: "P2",
    view: {
      message: "法术对决响应。",
      type: "SPELL_DUEL_ACTION"
    }
  },
  snapshot: {
    players: {},
    tick: 9
  }
});

assert.equal(spellDuelResponse.mode, "spell-duel");
assert.equal(spellDuelResponse.state, "blocked");
assert.equal(spellDuelResponse.statusLabel, "暂不可响应法术对决");
assert.equal(spellDuelResponse.windowLabel, "法术对决响应");
assert.equal(spellDuelResponse.commandFieldCount, 1);
assert.equal(spellDuelResponse.metricRows.find((row) => row.key === "template")?.value, "1");

const waitResponse = buildActionPanelResponsePlan({
  action: "WAIT",
  enabled: false,
  label: "等待",
  reason: "等待服务端"
}, {
  prompt: {
    actionable: false,
    candidates: [],
    playerId: "P1",
    view: {
      message: "等待对手。",
      type: "WAIT"
    }
  }
});

assert.equal(waitResponse.mode, "wait");
assert.equal(waitResponse.state, "blocked");
assert.equal(waitResponse.statusLabel, "等待服务端");
assert.equal(waitResponse.windowLabel, "等待窗口");
assert.equal(waitResponse.metricRows.find((row) => row.key === "route")?.detail, "未提供直接命令，不在前端伪造响应命令");

console.log("Action panel response plan check passed.");

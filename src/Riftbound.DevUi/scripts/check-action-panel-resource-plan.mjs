import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/actionPanelResourcePlan.ts");
const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function("exports", "module", "runePoolText", output)(
  moduleShim.exports,
  moduleShim,
  (pool) => `法力 ${pool?.mana ?? 0} / 符能 ${pool?.power ?? pool?.totalPower ?? 0}`
);

const { buildActionPanelResourcePlan } = moduleShim.exports;

const plan = buildActionPanelResourcePlan({
  action: "TAP_RUNE",
  commandTemplate: {
    bindings: [{ field: "sourceObjectId", label: "来源", required: true, source: "selectedSource" }],
    cmdType: "TAP_RUNE"
  },
  composer: {
    commandFields: ["sourceObjectId"],
    reason: "needs source",
    requiredSelectionRoles: ["source"],
    selectionRoles: ["source"],
    supported: true
  },
  enabled: true,
  label: "横置符文",
  reason: "可横置",
  selectionSteps: [
    { choices: [{ id: "rune-1", label: "符文一", objectIds: ["rune-1"] }], label: "来源", required: true, role: "source" }
  ],
  sources: [{ id: "rune-1", label: "符文一" }]
}, {
  playerId: "P1",
  snapshot: {
    players: {
      P1: {
        runePool: {
          mana: 2,
          power: 3,
          powerByTrait: { blue: 2, red: 1 }
        }
      }
    },
    tick: 14
  }
});

assert.equal(plan.state, "ready");
assert.equal(plan.statusLabel, "可横置");
assert.equal(plan.sourceChoiceCount, 1);
assert.equal(plan.commandFieldCount, 1);
assert.equal(plan.selectionStepCount, 1);
assert.equal(plan.powerTraitCount, 2);
assert.equal(plan.poolLabel, "法力 2 / 符能 3");
assert.equal(plan.metricRows.find((row) => row.key === "mana")?.value, "2");
assert.equal(plan.metricRows.find((row) => row.key === "power")?.value, "3");
assert.equal(plan.metricRows.find((row) => row.key === "selection")?.value, "已公开");
assert.ok(plan.authorityLabel.includes("服务端"));

const blocked = buildActionPanelResourcePlan({
  action: "RECYCLE_RUNE",
  enabled: false,
  label: "回收符文",
  reason: "等待窗口"
});

assert.equal(blocked.state, "blocked");
assert.equal(blocked.statusLabel, "暂不可回收");
assert.equal(blocked.sourceChoiceCount, 0);
assert.equal(blocked.poolLabel, "服务端未公开资源池");
assert.equal(blocked.metricRows.find((row) => row.key === "selection")?.value, "未公开");

console.log("Action panel resource plan check passed.");

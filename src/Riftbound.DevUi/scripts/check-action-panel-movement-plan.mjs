import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/actionPanelMovementPlan.ts");
const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function("exports", "module", output)(moduleShim.exports, moduleShim);

const { buildActionPanelMovementPlan } = moduleShim.exports;

const plan = buildActionPanelMovementPlan({
  action: "MOVE_UNIT",
  commandTemplate: {
    bindings: [
      { field: "sourceObjectId", label: "来源", required: true, source: "selectedSource" },
      { field: "origin", label: "服务端", metadataKey: "origin", required: true, source: "requirementMetadata" },
      { field: "destination", label: "位置", source: "selectedDestination" },
      { asArray: true, field: "optionalCosts", label: "费用", source: "selectedOptionalCosts" }
    ],
    cmdType: "MOVE_UNIT"
  },
  composer: {
    commandFields: ["sourceObjectId", "origin", "destination", "optionalCosts"],
    reason: "needs selection",
    requiredSelectionRoles: ["source"],
    selectionRoles: ["source", "destination", "optionalCost"],
    supported: true
  },
  destinations: [{ id: "BATTLEFIELD:2", label: "右战场" }],
  enabled: true,
  label: "移动单位",
  metadata: {
    sourceRequirements: [
      {
        destinationChoices: [
          { id: "BATTLEFIELD:2", label: "右战场" },
          { id: "BASE", label: "基地" }
        ],
        optionalCostChoices: [{ id: "ROAM", label: "游走费用" }],
        origin: "BATTLEFIELD:1",
        requiredOptionalCosts: ["ROAM"],
        sourceObjectId: "unit-1"
      }
    ]
  },
  optionalCosts: [{ id: "ROAM", label: "游走费用" }],
  reason: "可移动",
  selectionSteps: [
    { choices: [{ id: "unit-1", label: "单位一", objectIds: ["unit-1"] }], label: "来源", required: true, role: "source" },
    { choices: [{ id: "BATTLEFIELD:2", label: "右战场", objectIds: ["BATTLEFIELD:2"] }], label: "位置", required: false, role: "destination" },
    { choices: [{ id: "ROAM", label: "游走费用", objectIds: ["ROAM"] }], label: "费用", required: false, role: "optionalCost" }
  ],
  sources: [{ id: "unit-1", label: "单位一" }]
});

assert.equal(plan.state, "ready");
assert.equal(plan.statusLabel, "可移动");
assert.equal(plan.sourceChoiceCount, 1);
assert.equal(plan.destinationChoiceCount, 2);
assert.equal(plan.optionalCostChoiceCount, 1);
assert.equal(plan.originCount, 1);
assert.equal(plan.requirementCount, 1);
assert.equal(plan.selectionStepCount, 3);
assert.equal(plan.commandFieldCount, 4);
assert.equal(plan.metricRows.find((row) => row.key === "destinations")?.value, "2");
assert.ok(plan.authorityLabel.includes("服务端"));

const blocked = buildActionPanelMovementPlan({
  action: "MOVE_UNIT",
  enabled: false,
  label: "移动单位",
  reason: "等待窗口"
});

assert.equal(blocked.state, "blocked");
assert.equal(blocked.statusLabel, "暂不可移动");
assert.equal(blocked.sourceChoiceCount, 0);
assert.equal(blocked.destinationChoiceCount, 0);
assert.equal(blocked.optionalCostChoiceCount, 0);

console.log("Action panel movement plan check passed.");

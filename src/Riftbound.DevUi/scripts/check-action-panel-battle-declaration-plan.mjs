import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/actionPanelBattleDeclarationPlan.ts");
const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function("exports", "module", output)(moduleShim.exports, moduleShim);

const { buildActionPanelBattleDeclarationPlan } = moduleShim.exports;

const plan = buildActionPanelBattleDeclarationPlan({
  action: "DECLARE_BATTLE",
  commandTemplate: {
    bindings: [
      { asArray: true, field: "attackerObjectIds", required: true, source: "selectedSource" },
      { field: "battlefieldId", required: true, source: "selectedDestination" },
      { asArray: true, field: "defenderObjectIds", required: true, source: "selectedTargets" },
      { asArray: true, field: "optionalCosts", source: "selectedOptionalCosts" }
    ],
    cmdType: "DECLARE_BATTLE"
  },
  composer: {
    commandFields: ["attackerObjectIds", "battlefieldId", "defenderObjectIds", "optionalCosts"],
    reason: "needs selection",
    requiredSelectionRoles: ["source", "destination", "target"],
    selectionRoles: ["source", "destination", "target", "optionalCost"],
    supported: true
  },
  destinations: [{ id: "bf-1", label: "战场一" }],
  enabled: true,
  label: "声明战斗",
  metadata: {
    sourceRequirements: [
      {
        battlefieldChoices: [{ id: "bf-1", label: "战场一" }],
        optionalCostChoices: [{ id: "cost-1", label: "战斗符能" }],
        paymentResourceChoices: [{ id: "rune-1", label: "符文一" }],
        sourceObjectId: "attacker-1",
        targetChoicesByIndex: {
          "0": [
            { id: "defender-1", label: "防守一" },
            { id: "defender-2", label: "防守二" }
          ],
          "1": [
            { id: "defender-2", label: "防守二" },
            { id: "defender-3", label: "防守三" }
          ]
        }
      }
    ]
  },
  optionalCosts: [{ id: "cost-1", label: "战斗符能" }],
  reason: "可声明",
  sources: [{ id: "attacker-1", label: "攻击者" }]
});

assert.equal(plan.state, "ready");
assert.equal(plan.statusLabel, "可声明");
assert.equal(plan.sourceChoiceCount, 1);
assert.equal(plan.battlefieldChoiceCount, 1);
assert.equal(plan.defenderChoiceCount, 3);
assert.equal(plan.optionalCostChoiceCount, 1);
assert.equal(plan.paymentResourceChoiceCount, 1);
assert.equal(plan.commandFieldCount, 4);
assert.equal(plan.selectionStepCount, 4);
assert.equal(plan.metricRows.length, 5);
assert.equal(plan.metricRows.find((row) => row.key === "defenders")?.value, 3);
assert.ok(plan.authorityLabel.includes("服务端"));

const stepFallback = buildActionPanelBattleDeclarationPlan({
  action: "DECLARE_BATTLE",
  enabled: false,
  label: "声明战斗",
  reason: "等待窗口",
  selectionSteps: [
    { choices: [{ id: "attacker-1", label: "攻击者", objectIds: ["attacker-1"] }], label: "攻击", required: true, role: "source" },
    { choices: [{ id: "bf-1", label: "战场", objectIds: ["bf-1"] }], label: "战场", required: true, role: "destination" },
    { choices: [{ id: "defender-1", label: "防守", objectIds: ["defender-1"] }], label: "防守", required: true, role: "target" }
  ]
});

assert.equal(stepFallback.state, "blocked");
assert.equal(stepFallback.sourceChoiceCount, 1);
assert.equal(stepFallback.battlefieldChoiceCount, 1);
assert.equal(stepFallback.defenderChoiceCount, 1);
assert.equal(stepFallback.selectionStepCount, 3);

console.log("Action panel battle declaration plan check passed.");

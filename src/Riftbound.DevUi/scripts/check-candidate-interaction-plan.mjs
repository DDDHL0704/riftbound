import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/candidateInteractionPlan.ts");
const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function("exports", "module", "promptChoiceRoleLabel", output)(
  moduleShim.exports,
  moduleShim,
  promptChoiceRoleLabel
);

const { buildCandidateInteractionPlans } = moduleShim.exports;

const plans = buildCandidateInteractionPlans([
  {
    action: "ACTIVATE_ABILITY",
    command: {
      bindings: [
        { field: "sourceObjectId", required: true, source: "selectedSource" },
        { field: "abilityId", required: true, source: "requirementMetadata" }
      ],
      cmdType: "ACTIVATE_ABILITY"
    },
    enabled: false,
    label: "激活技能",
    reason: "缺少来源",
    choices: [],
    steps: [
      {
        count: 0,
        label: "来源",
        required: true,
        role: "source",
        sampleLabels: []
      }
    ]
  },
  {
    action: "PLAY_CARD",
    command: {
      bindings: [
        { field: "sourceObjectId", required: true, role: "source", roleLabel: "来源", source: "selectedSource" },
        { field: "targetObjectIds", required: false, role: "target", roleLabel: "目标", source: "selectedTargets" },
        { field: "optionalCosts", required: false, role: "optionalCost", roleLabel: "费用", source: "selectedOptionalCosts" }
      ],
      cmdType: "PLAY_CARD"
    },
    enabled: true,
    label: "打出手牌",
    reason: "可提交",
    choices: [],
    steps: [
      {
        count: 1,
        label: "来源",
        required: true,
        role: "source",
        sampleLabels: ["手牌法术"]
      },
      {
        count: 2,
        label: "目标",
        required: false,
        role: "target",
        sampleLabels: ["敌方单位", "敌方基地"]
      }
    ]
  }
]);

assert.equal(plans.length, 2);
assert.equal(plans[0].action, "PLAY_CARD");
assert.equal(plans[0].enabled, true);
assert.equal(plans[0].commandFieldCount, 3);
assert.equal(plans[0].missingRequiredStepCount, 0);
assert.equal(plans[0].requiredStepCount, 1);
assert.equal(plans[0].optionalStepCount, 1);
assert.equal(plans[0].nextRequiredStep?.label, "来源");
assert.equal(plans[0].stepRows[0].state, "available");
assert.equal(plans[0].stepRows[1].state, "optional");
assert.ok(plans[0].summary.includes("可提交"));
assert.ok(plans[0].summary.includes("命令字段 3"));

assert.equal(plans[1].action, "ACTIVATE_ABILITY");
assert.equal(plans[1].enabled, false);
assert.equal(plans[1].missingRequiredStepCount, 1);
assert.equal(plans[1].nextRequiredStep?.state, "missing-required");
assert.equal(plans[1].nextRequiredStep?.stateLabel, "缺少必需项");
assert.ok(plans[1].summary.includes("缺口 1"));

console.log("Candidate interaction plan check passed.");

function promptChoiceRoleLabel(role) {
  return {
    destination: "位置",
    mode: "模式",
    optionalCost: "费用",
    source: "来源",
    target: "目标"
  }[role] ?? role;
}

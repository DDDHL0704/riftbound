import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/focusedInteractionGrammarPlan.ts");
const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function("exports", "module", "promptChoiceRoleLabel", "promptChoiceRoleOrder", output)(
  moduleShim.exports,
  moduleShim,
  promptChoiceRoleLabel,
  ["source", "mode", "destination", "target", "optionalCost"]
);

const { buildFocusedInteractionGrammarPlan, candidateGrammarKey } = moduleShim.exports;

const playCardCandidate = {
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
    },
    {
      count: 1,
      label: "追加费用",
      required: false,
      role: "optionalCost",
      sampleLabels: ["回响"]
    }
  ]
};

const readyPlan = buildFocusedInteractionGrammarPlan({
  candidates: [playCardCandidate],
  disabledByConnection: false,
  selectionDraft: {
    candidateKey: candidateGrammarKey(playCardCandidate),
    optionalCostIds: ["ECHO"],
    sourceObjectId: "P1-HAND-1",
    targetChoiceIds: ["P2-UNIT-1"]
  },
  sourceObjectId: "P1-HAND-1"
});

assert.equal(readyPlan.state, "ready");
assert.equal(readyPlan.commandType, "PLAY_CARD");
assert.equal(readyPlan.commandFieldCount, 3);
assert.equal(readyPlan.missingRequiredCount, 0);
assert.equal(readyPlan.nextStepLabel, "提交服务端候选");
assert.equal(readyPlan.steps.find((step) => step.role === "source")?.state, "locked");
assert.equal(readyPlan.steps.find((step) => step.role === "target")?.state, "selected");
assert.equal(readyPlan.steps.find((step) => step.role === "optionalCost")?.state, "selected");
assert.equal(readyPlan.steps.find((step) => step.role === "submit")?.state, "ready");

const blockedByConnection = buildFocusedInteractionGrammarPlan({
  candidates: [playCardCandidate],
  disabledByConnection: true,
  selectionDraft: {
    candidateKey: candidateGrammarKey(playCardCandidate),
    optionalCostIds: [],
    sourceObjectId: "P1-HAND-1",
    targetChoiceIds: []
  },
  sourceObjectId: "P1-HAND-1"
});

assert.equal(blockedByConnection.state, "blocked");
assert.equal(blockedByConnection.steps.find((step) => step.role === "submit")?.stateLabel, "等待连接恢复");

const missingSourcePlan = buildFocusedInteractionGrammarPlan({
  candidates: [{
    action: "ACTIVATE_ABILITY",
    enabled: true,
    label: "激活技能",
    reason: "等待来源",
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
  }],
  disabledByConnection: false
});

assert.equal(missingSourcePlan.state, "incomplete");
assert.equal(missingSourcePlan.missingRequiredCount, 1);
assert.equal(missingSourcePlan.nextStepLabel, "等待服务端提供来源");
assert.equal(missingSourcePlan.steps.find((step) => step.role === "source")?.state, "missing");
assert.equal(missingSourcePlan.steps.find((step) => step.role === "submit")?.state, "blocked");

const requiredTargetPlan = buildFocusedInteractionGrammarPlan({
  candidates: [{
    ...playCardCandidate,
    steps: playCardCandidate.steps.map((step) => step.role === "target" ? { ...step, required: true } : step)
  }],
  disabledByConnection: false,
  sourceObjectId: "P1-HAND-1"
});

assert.equal(requiredTargetPlan.state, "incomplete");
assert.equal(requiredTargetPlan.missingRequiredCount, 1);
assert.equal(requiredTargetPlan.nextStepLabel, "选择目标");
assert.equal(requiredTargetPlan.steps.find((step) => step.role === "source")?.state, "locked");
assert.equal(requiredTargetPlan.steps.find((step) => step.role === "target")?.state, "available");
assert.equal(requiredTargetPlan.steps.find((step) => step.role === "submit")?.state, "blocked");

const emptyPlan = buildFocusedInteractionGrammarPlan({
  candidates: [],
  disabledByConnection: false
});

assert.equal(emptyPlan.state, "empty");
assert.equal(emptyPlan.steps.length, 0);
assert.equal(emptyPlan.nextStepLabel, "点击含服务端候选的卡牌");

console.log("Focused interaction grammar plan check passed.");

function promptChoiceRoleLabel(role) {
  return {
    destination: "位置",
    mode: "模式",
    optionalCost: "费用",
    source: "来源",
    target: "目标"
  }[role] ?? role;
}

import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/wirePromptCandidatePlan.ts");
const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function(
  "exports",
  "module",
  "promptChoiceRoleLabel",
  "promptChoiceSummaryObjectIds",
  output
)(moduleShim.exports, moduleShim, promptChoiceRoleLabel, promptChoiceSummaryObjectIds);

const { buildWirePromptCandidateListPlan } = moduleShim.exports;

const model = {
  candidates: [
    candidate({
      action: "PLAY_CARD",
      enabled: true,
      label: "打出手牌",
      reason: "可提交",
      choices: [
        choice("source", "source-card", ["source-1"]),
        choice("target", "target-card", ["target-1"]),
        choice("target", "hidden-target", ["HIDDEN"]),
        choice("destination", "battlefield", ["battlefield-1"])
      ]
    }),
    candidate({
      action: "TAP_RUNE",
      enabled: false,
      label: "横置符文",
      reason: "窗口不允许",
      choices: [
        choice("source", "disabled-rune", ["rune-1"])
      ]
    })
  ],
  disabledObjectIds: new Set(["rune-1"]),
  enabledObjectIds: new Set(["source-1"])
};

const objects = {
  "battlefield-1": { cardNo: "OGN-275/298", objectId: "battlefield-1" },
  "source-1": { cardNo: "OGN-001/298", objectId: "source-1" },
  "target-1": { cardNo: "OGN-002/298", objectId: "target-1" }
};

const plan = buildWirePromptCandidateListPlan({
  model,
  objects,
  promptId: "room:1:P1:PLAY_CARD",
  promptMessage: "选择一张手牌。",
  promptReason: "fallback reason",
  promptTitle: "主要行动",
  promptType: "MAIN_ACTION",
  snapshotTick: 12
});

assert.equal(plan.promptTitle, "主要行动");
assert.equal(plan.promptType, "MAIN_ACTION");
assert.equal(plan.message, "选择一张手牌。");
assert.equal(plan.versionLabel, "版本：room:1:P1:PLAY_CARD / tick 12");
assert.equal(plan.emptyLabel, undefined);
assert.equal(plan.enabledRows.length, 1);
assert.equal(plan.disabledRows.length, 1);
assert.equal(plan.enabledRows[0].key, "enabled-PLAY_CARD-打出手牌");
assert.equal(plan.enabledRows[0].choiceGroups[0].summary, "来源：source-card");
assert.equal(plan.enabledRows[0].choiceGroups[1].summary, "目标：target-card、hidden-target");
assert.deepEqual(
  plan.enabledRows[0].objectRefs.map((ref) => `${ref.role}:${ref.id}`),
  ["来源:source-1", "目标:target-1", "位置:battlefield-1"],
  "object refs must ignore hidden placeholders and unknown object ids"
);
assert.equal(plan.disabledRows[0].label, "横置符文");
assert.equal(plan.disabledRows[0].objectRefs.length, 0, "disabled row must not invent refs for objects absent from snapshot");

const emptyPlan = buildWirePromptCandidateListPlan({
  model: {
    candidates: [],
    disabledObjectIds: new Set(),
    enabledObjectIds: new Set(),
    objectById: new Map()
  },
  objects: {},
  promptReason: "等待服务端"
});

assert.equal(emptyPlan.emptyLabel, "服务端暂未提供候选行动。");
assert.equal(emptyPlan.promptTitle, "当前行动窗口");
assert.equal(emptyPlan.promptType, "无");
assert.equal(emptyPlan.message, "等待服务端");

console.log("Wire prompt candidate plan check passed.");

function candidate({ action, choices, enabled, label, reason }) {
  return {
    action,
    choices,
    enabled,
    label,
    reason,
    steps: []
  };
}

function choice(role, label, objectIds) {
  return {
    id: label,
    label,
    objectIds,
    role
  };
}

function promptChoiceRoleLabel(role) {
  switch (role) {
    case "source":
      return "来源";
    case "target":
      return "目标";
    case "destination":
      return "位置";
    case "optionalCost":
      return "费用";
    case "mode":
      return "模式";
    default:
      return role;
  }
}

function promptChoiceSummaryObjectIds(choice) {
  return choice.objectIds ?? [choice.id];
}

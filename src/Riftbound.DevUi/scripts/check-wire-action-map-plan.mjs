import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));

const candidatePlanExports = loadTsModule(resolve(scriptDir, "../src/utils/candidateInteractionPlan.ts"), {
  promptChoiceRoleLabel
});
const commandFieldDisplayExports = loadTsModule(resolve(scriptDir, "../src/utils/commandFieldDisplay.ts"));
const promptInteractionExports = loadTsModule(resolve(scriptDir, "../src/utils/promptInteraction.ts"), {
  promptActionLabel,
  promptReasonLabel,
  redactInternalText: (value) => String(value).trim(),
  sourceRequirementRecords: () => []
});
const actionMapExports = loadTsModule(resolve(scriptDir, "../src/utils/wireActionMapPlan.ts"), {
  buildCandidateInteractionPlans: candidatePlanExports.buildCandidateInteractionPlans,
  buildPromptInteractionModel: promptInteractionExports.buildPromptInteractionModel,
  commandBindingDisplayLabel: commandFieldDisplayExports.commandBindingDisplayLabel,
  commandBindingFieldKey: commandFieldDisplayExports.commandBindingFieldKey,
  promptActionLabel,
  promptChoiceSummaryObjectIds: promptInteractionExports.promptChoiceSummaryObjectIds,
  promptChoiceRoleOrder: promptInteractionExports.promptChoiceRoleOrder,
  promptChoiceRoleLabel: promptInteractionExports.promptChoiceRoleLabel,
  promptCommandBindingLabel: promptInteractionExports.promptCommandBindingLabel,
  promptCommandBindingSourceLabel: promptInteractionExports.promptCommandBindingSourceLabel,
  promptReasonLabel
});

const { buildWireActionMapPlan } = actionMapExports;

const prompt = {
  actionable: true,
  candidates: [
    {
      action: "PLAY_CARD",
      commandTemplate: {
        bindings: [
          { field: "sourceObjectId", label: "来源", required: true, source: "selectedSource" },
          { field: "targetObjectIds", label: "目标", required: false, source: "selectedTargets" },
          { field: "serverPaymentState", label: "内部费用", required: false, source: "requirementMetadata" }
        ],
        cmdType: "PLAY_CARD"
      },
      enabled: true,
      label: "打出手牌",
      reason: "可提交",
      sources: [{ id: "p1-hand-1", label: "手牌法术", objectIds: ["p1-hand-1"] }],
      targets: [{ id: "p2-unit-1", label: "敌方单位", objectIds: ["p2-unit-1"] }]
    },
    {
      action: "ACTIVATE_ABILITY",
      enabled: false,
      label: "启动能力",
      reason: "窗口不允许",
      sources: [{ id: "p1-hand-1", label: "手牌法术", objectIds: ["p1-hand-1"] }]
    },
    {
      action: "MOVE_UNIT",
      enabled: false,
      label: "移动单位",
      reason: "单位不能移动",
      sources: [{ id: "blocked-unit", label: "疲劳单位", objectIds: ["blocked-unit"] }]
    }
  ],
  contract: {
    candidateAction: "PLAY_CARD",
    hiddenMetadata: ["serverPaymentState"],
    legalChoices: ["candidate.sources", "candidate.targets"],
    promptKind: "MAIN_ACTION",
    requiredPayload: ["sourceObjectId", "targetObjectIds"],
    validationErrors: [],
    visibleMetadata: ["sourceRequirements"]
  },
  playerId: "P1"
};

const snapshot = {
  players: {
    P1: {
      objects: {
        "p1-hand-1": { cardNo: "OGN-001/298", objectId: "p1-hand-1", ownerId: "P1" },
        "blocked-unit": { cardNo: "OGN-002/298", objectId: "blocked-unit", ownerId: "P1" }
      },
      zones: {}
    },
    P2: {
      objects: {
        "p2-unit-1": { cardNo: "OGN-003/298", objectId: "p2-unit-1", ownerId: "P2" }
      },
      zones: {}
    }
  }
};

const plan = buildWireActionMapPlan({
  playerId: "P1",
  prompt,
  selectedObjectId: "p1-hand-1",
  snapshot
});

assert.equal(plan.canAct, true);
assert.deepEqual(plan.metrics.map((metric) => metric.value), ["1", "3", "2", "1"]);
assert.equal(plan.objectEntries.length, 2);
assert.equal(plan.objectEntries[0].label, "OGN-001/298");
assert.equal(plan.objectEntries[0].selected, true);
assert.equal(plan.objectEntries[0].enabledCandidateCount, 1);
assert.equal(plan.objectEntries[0].disabledCandidateCount, 1);
assert.equal(plan.blockedObjectEntries.length, 1);
assert.equal(plan.blockedObjectEntries[0].objectId, "blocked-unit");
assert.equal(plan.blockedObjectEntries[0].label, "OGN-002/298");
assert.equal(plan.blockedObjectEntries[0].disabledCandidateCount, 1);
assert.equal(plan.blockedObjectEntries[0].selected, false);
assert.equal(plan.focus.objectId, "p1-hand-1");
assert.equal(plan.focus.label, "OGN-001/298");
assert.equal(plan.focus.stateLabel, "1 个可提交候选");
assert.deepEqual(plan.focus.roleLabels, ["来源"]);
assert.equal(plan.focus.enabledCandidateCount, 1);
assert.equal(plan.focus.disabledCandidateCount, 1);
assert.equal(plan.focus.relatedCandidates.length, 2);
assert.equal(plan.focus.relatedCandidates[0].label, "打出手牌");
assert.equal(plan.focus.relatedCandidates[0].commandType, "PLAY_CARD");
assert.deepEqual(plan.focus.relatedCandidates[0].roleLabels, ["来源"]);
assert.equal(plan.focus.relatedCandidates[0].nextStepLabel, "可选目标");
assert.deepEqual(plan.focus.relatedCandidates[0].nextObjectRefs, [{
  key: "PLAY_CARD:target:p2-unit-1:p2-unit-1",
  label: "敌方单位",
  objectId: "p2-unit-1",
  roleLabel: "目标"
}]);
assert.equal(plan.disabledOnlyObjectCount, 1);
assert.equal(plan.groups[0].label, "打出手牌");
assert.equal(plan.groups[0].enabledCount, 1);
assert.equal(plan.groups[0].roleCounts.find((role) => role.role === "source")?.count, 1);
assert.equal(plan.groups[0].roleCounts.find((role) => role.role === "target")?.count, 1);
assert.equal(plan.candidatePlanTotalCount, 3);
assert.equal(plan.candidatePlans[0].action, "PLAY_CARD");
assert.equal(plan.candidatePlans[0].draftActive, false);
assert.equal(plan.candidatePlans[0].stepRows[0].selectionState, "inactive");
assert.equal(plan.candidatePlans[0].stepRows[0].progressLabel, "未进入当前草稿");
assert.equal(plan.route, undefined);
assert.deepEqual(plan.candidatePlans[0].stepRows[0].objectRefs, [{
  key: "PLAY_CARD:source:p1-hand-1:p1-hand-1",
  label: "手牌法术",
  objectId: "p1-hand-1",
  roleLabel: "来源"
}]);
assert.deepEqual(plan.candidatePlans[0].stepRows[1].objectRefs, [{
  key: "PLAY_CARD:target:p2-unit-1:p2-unit-1",
  label: "敌方单位",
  objectId: "p2-unit-1",
  roleLabel: "目标"
}]);
assert.equal(plan.grammarCandidateTotalCount, 1);
assert.equal(plan.grammarCandidates[0].commandType, "PLAY_CARD");
assert.equal(plan.grammarCandidates[0].commandFieldCount, 3);
assert.equal(plan.grammarCandidates[0].commandFields[0].label, "来源:sourceObjectId*");
assert.equal(plan.grammarCandidates[0].commandFields[2].field, "server-metadata-2");
assert.equal(plan.grammarCandidates[0].commandFields[2].label, "服务端字段");
assert.equal(plan.grammarCandidates[0].commandFields[2].sourceLabel, "服务端注入");
assert.equal(plan.contract.hiddenMetadataCount, 1);
assert.equal(JSON.stringify(plan).includes("serverPaymentState"), false);

const draftPlan = buildWireActionMapPlan({
  playerId: "P1",
  prompt,
  selectedObjectId: "p1-hand-1",
  selectionDraft: {
    candidateKey: "PLAY_CARD::打出手牌",
    optionalCostIds: [],
    sourceObjectId: "p1-hand-1",
    targetChoiceIds: ["p2-unit-1"]
  },
  snapshot
});
const draftCandidate = draftPlan.candidatePlans.find((candidate) => candidate.action === "PLAY_CARD");
assert.equal(draftCandidate.draftActive, true);
const draftSourceStep = draftCandidate.stepRows.find((step) => step.role === "source");
const draftTargetStep = draftCandidate.stepRows.find((step) => step.role === "target");
assert.equal(draftSourceStep.selectionState, "selected");
assert.equal(draftSourceStep.selectedCount, 1);
assert.deepEqual(draftSourceStep.selectedLabels, ["手牌法术"]);
assert.equal(draftTargetStep.selectionState, "selected");
assert.equal(draftTargetStep.selectedCount, 1);
assert.deepEqual(draftTargetStep.selectedLabels, ["敌方单位"]);
assert.equal(draftPlan.route.candidateLabel, "打出手牌");
assert.equal(draftPlan.route.state, "ready");
assert.equal(draftPlan.route.selectedStepCount, 2);
assert.equal(draftPlan.route.missingRequiredSelectionCount, 0);
assert.equal(draftPlan.route.steps.find((step) => step.role === "source").state, "selected");
assert.equal(draftPlan.route.steps.find((step) => step.role === "target").state, "selected");
assert.equal(draftPlan.candidatePlans.find((candidate) => candidate.action === "ACTIVATE_ABILITY").draftActive, false);

const readOnlyPlan = buildWireActionMapPlan({
  playerId: "P2",
  prompt,
  snapshot
});
assert.equal(readOnlyPlan.canAct, false);
assert.equal(readOnlyPlan.focus, undefined);

const blockedFocusPlan = buildWireActionMapPlan({
  playerId: "P1",
  prompt,
  selectedObjectId: "blocked-unit",
  snapshot
});
assert.equal(blockedFocusPlan.blockedObjectEntries[0].selected, true);
assert.equal(blockedFocusPlan.focus.objectId, "blocked-unit");
assert.equal(blockedFocusPlan.focus.stateLabel, "仅有关联但当前阻断");
assert.equal(blockedFocusPlan.focus.enabledCandidateCount, 0);
assert.equal(blockedFocusPlan.focus.disabledCandidateCount, 1);
assert.deepEqual(blockedFocusPlan.focus.roleLabels, ["来源"]);
assert.equal(blockedFocusPlan.focus.relatedCandidates[0].commandType, "MOVE_UNIT");
assert.equal(blockedFocusPlan.focus.relatedCandidates[0].stateLabel, "暂不可提交");

const limitedPlan = buildWireActionMapPlan({
  maxObjectEntries: 1,
  playerId: "P1",
  prompt,
  snapshot
});
assert.equal(limitedPlan.objectEntries.length, 1);
assert.equal(limitedPlan.objectEntryOverflowCount, 1);
assert.equal(limitedPlan.blockedObjectEntries.length, 1);
assert.equal(limitedPlan.blockedObjectEntryOverflowCount, 0);

console.log("Wire action map plan check passed.");

function promptActionLabel(candidate) {
  return candidate.label || candidate.action;
}

function promptReasonLabel(reason, fallback = "服务端候选") {
  return reason || fallback;
}

function promptChoiceRoleLabel(role) {
  return {
    destination: "位置",
    mode: "模式",
    optionalCost: "费用",
    source: "来源",
    target: "目标"
  }[role] ?? role;
}

function loadTsModule(sourcePath, injectedValues = {}) {
  const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
  const output = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022
    }
  }).outputText;
  const moduleShim = { exports: {} };
  const names = Object.keys(injectedValues);
  const values = Object.values(injectedValues);

  new Function("exports", "module", ...names, output)(moduleShim.exports, moduleShim, ...values);
  return moduleShim.exports;
}

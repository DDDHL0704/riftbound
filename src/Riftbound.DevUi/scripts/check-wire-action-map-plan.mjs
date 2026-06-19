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
assert.equal(plan.disabledOnlyObjectCount, 1);
assert.equal(plan.groups[0].label, "打出手牌");
assert.equal(plan.groups[0].enabledCount, 1);
assert.equal(plan.groups[0].roleCounts.find((role) => role.role === "source")?.count, 1);
assert.equal(plan.groups[0].roleCounts.find((role) => role.role === "target")?.count, 1);
assert.equal(plan.candidatePlanTotalCount, 3);
assert.equal(plan.candidatePlans[0].action, "PLAY_CARD");
assert.equal(plan.grammarCandidateTotalCount, 1);
assert.equal(plan.grammarCandidates[0].commandType, "PLAY_CARD");
assert.equal(plan.grammarCandidates[0].commandFieldCount, 3);
assert.equal(plan.grammarCandidates[0].commandFields[0].label, "来源:sourceObjectId*");
assert.equal(plan.grammarCandidates[0].commandFields[2].field, "server-metadata-2");
assert.equal(plan.grammarCandidates[0].commandFields[2].label, "服务端字段");
assert.equal(plan.grammarCandidates[0].commandFields[2].sourceLabel, "服务端注入");
assert.equal(plan.contract.hiddenMetadataCount, 1);
assert.equal(JSON.stringify(plan).includes("serverPaymentState"), false);

const readOnlyPlan = buildWireActionMapPlan({
  playerId: "P2",
  prompt,
  snapshot
});
assert.equal(readOnlyPlan.canAct, false);

const limitedPlan = buildWireActionMapPlan({
  maxObjectEntries: 1,
  playerId: "P1",
  prompt,
  snapshot
});
assert.equal(limitedPlan.objectEntries.length, 1);
assert.equal(limitedPlan.objectEntryOverflowCount, 1);

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

import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/candidateComposerModel.ts");
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
  "commandFromActionPromptTemplate",
  "redactInternalText",
  output
)(
  moduleShim.exports,
  moduleShim,
  commandFromActionPromptTemplate,
  redactInternalText
);

const {
  booleanFromRecord,
  buildCandidateComposerModel,
  buildCandidateComposerSubmissionPlan,
  buildCandidateCommandPreviewPlan,
  candidateComposerKey,
  choiceLabel,
  choiceLabelById,
  composerCommand,
  composerControls,
  initialComposerState,
  safePromptSummary,
  selectedRequirement,
  uniqueStrings
} = moduleShim.exports;

const playCandidate = {
  action: "PLAY_CARD",
  enabled: true,
  label: "打出法术",
  metadata: {
    sourceRequirements: [
      {
        cardNo: "OGN-001/298",
        destinationChoices: [
          { id: "bf-a", label: "左战场" },
          { id: "bf-b", label: "右战场" }
        ],
        minTargetCount: 2,
        mode: "burst",
        modeLabel: "爆发",
        optionalCostChoices: [
          { id: "rune-extra", label: "额外符文" }
        ],
        requiredOptionalCosts: ["rune-main"],
        sourceObjectId: "hand-1",
        targetChoicesByIndex: {
          0: [{ id: "target-a", label: "目标 A", objectIds: ["target-a"] }],
          1: [{ id: "target-b", label: "目标 B", objectIds: ["target-b"] }]
        }
      },
      {
        cardNo: "OGN-002/298",
        sourceObjectId: "hand-2"
      }
    ]
  },
  reason: "可打出",
  sources: [
    { id: "hand-1", label: "手牌 1", objectIds: ["hand-1"] },
    { id: "hand-2", label: "手牌 2", objectIds: ["hand-2"] }
  ]
};

const model = buildCandidateComposerModel(playCandidate);
assert.equal(model.sourceRequirements.length, 2);
assert.equal(model.sourceRequirementById.get("hand-1")?.cardNo, "OGN-001/298");
assert.ok(model.resetKey.includes("PLAY_CARD::打出法术"));

const requirement = selectedRequirement(model, "hand-1");
const controls = composerControls(playCandidate, model, requirement, undefined);
assert.deepEqual(controls.sources.map((choice) => choice.id), ["hand-1", "hand-2"]);
assert.deepEqual(controls.modeChoices.map((choice) => choice.label), ["爆发"]);
assert.deepEqual(controls.destinationChoices.map((choice) => choice.id), ["bf-a", "bf-b"]);
assert.deepEqual(controls.targetGroups.map((group) => `${group.key}:${group.label}:${group.required}`), [
  "目标-0:目标 1:true",
  "目标-1:目标 2:true"
]);
assert.deepEqual(controls.requiredOptionalCostIds, ["rune-main"]);
assert.deepEqual(controls.optionalCostChoices.map((choice) => choice.id), ["rune-extra", "rune-main"]);

const draft = {
  candidateKey: candidateComposerKey(playCandidate),
  destinationId: "bf-b",
  mode: "burst",
  optionalCostIds: ["rune-extra", "not-legal"],
  sourceObjectId: "hand-1",
  targetChoiceIds: ["target-b", "target-a"]
};
const state = initialComposerState(playCandidate, model, undefined, draft);
assert.equal(state.sourceId, "hand-1");
assert.equal(state.destinationId, "bf-b");
assert.equal(state.mode, "burst");
assert.deepEqual(state.optionalCostIds, ["rune-extra"]);
assert.deepEqual(state.targetIdsByGroup, {
  "目标-0": "target-a",
  "目标-1": "target-b"
});

const selectedTargetIds = controls.targetGroups.map((group) => state.targetIdsByGroup[group.key]);
const optionalCostIds = uniqueStrings([...controls.requiredOptionalCostIds, ...state.optionalCostIds]);
const submission = buildCandidateComposerSubmissionPlan({
  candidate: playCandidate,
  controls,
  disabledByConnection: false,
  requirement,
  snapshot: undefined,
  state
});
assert.equal(submission.canSubmit, true);
assert.equal(submission.gateCanSubmit, true);
assert.equal(submission.gateStateLabel, "可提交");
assert.equal(submission.stateLabel, "待服务端校验");
assert.equal(submission.blockReason, undefined);
assert.equal(submission.checkSummary, "7 通过 / 0 阻断 / 0 等待");
assert.deepEqual(submission.checkRows.map((check) => check.key), [
  "server-candidate",
  "submission-gate",
  "source",
  "destination",
  "target",
  "command",
  "backend-support"
]);
assert.deepEqual(submission.checkRows.map((check) => check.state), ["ready", "ready", "ready", "ready", "ready", "ready", "ready"]);
assert.deepEqual(submission.selectedTargetIds, ["target-a", "target-b"]);
assert.deepEqual(submission.optionalCostIds, ["rune-main", "rune-extra"]);
assert.equal(submission.unsupportedReason, undefined);
assert.deepEqual(buildCandidateCommandPreviewPlan(controls, state), {
  costLabels: ["rune-main", "额外符文"],
  destinationLabel: "右战场",
  modeLabel: "爆发",
  sourceLabel: "手牌 1",
  targetLabels: ["目标 A", "目标 B"]
});
assert.deepEqual(optionalCostIds, ["rune-main", "rune-extra"]);
assert.deepEqual(composerCommand(playCandidate, undefined, state, requirement, selectedTargetIds, optionalCostIds), {
  cardNo: "OGN-001/298",
  cmdType: "PLAY_CARD",
  destination: "bf-b",
  mode: "burst",
  optionalCosts: ["rune-main", "rune-extra"],
  sourceObjectId: "hand-1",
  targetObjectIds: ["target-a", "target-b"]
});

const forcedControls = composerControls(playCandidate, model, requirement, "hand-1");
assert.deepEqual(forcedControls.sources.map((choice) => choice.id), ["hand-1"]);

const blockedRequirement = {
  ...requirement,
  composable: false,
  unsupportedReason: { summary: "需要后端补齐窗口" }
};
const blockedSubmission = buildCandidateComposerSubmissionPlan({
  candidate: playCandidate,
  controls,
  disabledByConnection: false,
  requirement: blockedRequirement,
  snapshot: undefined,
  state
});
assert.equal(blockedSubmission.canSubmit, false);
assert.equal(blockedSubmission.unsupportedReason, "需要后端补齐窗口");
assert.equal(blockedSubmission.blockReason, "需要后端补齐窗口");
assert.equal(blockedSubmission.checkSummary, "6 通过 / 1 阻断 / 0 等待");
assert.equal(blockedSubmission.checkRows.find((check) => check.key === "backend-support")?.state, "blocked");

const disconnectedSubmission = buildCandidateComposerSubmissionPlan({
  candidate: playCandidate,
  controls,
  disabledByConnection: true,
  requirement,
  snapshot: undefined,
  state
});
assert.equal(disconnectedSubmission.canSubmit, false);
assert.equal(disconnectedSubmission.gateCanSubmit, false);
assert.equal(disconnectedSubmission.stateLabel, "连接未就绪");
assert.equal(disconnectedSubmission.blockReason, "当前入口不可提交，等待服务端窗口或连接恢复。");
assert.equal(disconnectedSubmission.checkSummary, "6 通过 / 1 阻断 / 0 等待");
assert.equal(disconnectedSubmission.checkRows.find((check) => check.key === "submission-gate")?.state, "blocked");

const staleGateSubmission = buildCandidateComposerSubmissionPlan({
  candidate: playCandidate,
  controls,
  disabledByConnection: true,
  requirement,
  snapshot: undefined,
  state,
  submissionGate: {
    canSubmit: false,
    reason: "行动提示属于 tick 7，当前桌面快照是 tick 8。",
    state: "stale-snapshot",
    stateLabel: "等待同步"
  }
});
assert.equal(staleGateSubmission.canSubmit, false);
assert.equal(staleGateSubmission.gateCanSubmit, false);
assert.equal(staleGateSubmission.gateStateLabel, "等待同步");
assert.equal(staleGateSubmission.stateLabel, "等待同步");
assert.equal(staleGateSubmission.blockReason, "行动提示属于 tick 7，当前桌面快照是 tick 8。");
assert.equal(staleGateSubmission.checkSummary, "6 通过 / 1 阻断 / 0 等待");
assert.equal(staleGateSubmission.checkRows.find((check) => check.key === "submission-gate")?.stateLabel, "等待同步");

const battleCandidate = {
  action: "DECLARE_BATTLE",
  enabled: true,
  label: "声明战斗",
  metadata: {
    sourceRequirements: [
      {
        battlefieldChoices: [{ id: "bf-a", label: "左战场" }],
        minDefenderCount: 1,
        sourceObjectId: "unit-1",
        targetChoices: [{ id: "defender-1", label: "防守单位" }]
      }
    ]
  },
  reason: "可宣战"
};
const battleModel = buildCandidateComposerModel(battleCandidate);
const battleRequirement = selectedRequirement(battleModel, "unit-1");
const battleControls = composerControls(battleCandidate, battleModel, battleRequirement, undefined);
const battleState = initialComposerState(battleCandidate, battleModel);
assert.equal(battleControls.targetGroups[0].label, "防守方 1");
assert.equal(battleControls.targetGroups[0].required, true);
assert.deepEqual(composerCommand(
  battleCandidate,
  undefined,
  battleState,
  battleRequirement,
  [battleControls.targetGroups[0].choices[0].id],
  []
), {
  attackerObjectIds: ["unit-1"],
  battlefieldId: "bf-a",
  battlefieldTargetObjectIds: ["bf-a"],
  cmdType: "DECLARE_BATTLE",
  defenderObjectIds: ["defender-1"],
  optionalCosts: undefined
});

const templatedCandidate = {
  action: "ACTIVATE_ABILITY",
  commandTemplate: {
    bindings: [
      { field: "sourceObjectId", required: true, source: "selectedSource" },
      { field: "abilityId", metadataKey: "abilityId", required: true, source: "requirementMetadata" }
    ],
    cmdType: "ACTIVATE_ABILITY"
  },
  enabled: true,
  label: "启动能力",
  metadata: {
    sourceRequirements: [
      { abilityId: "ability-1", sourceObjectId: "unit-1" }
    ]
  },
  reason: "可启动"
};
const templatedModel = buildCandidateComposerModel(templatedCandidate);
const templatedState = initialComposerState(templatedCandidate, templatedModel);
assert.deepEqual(composerCommand(
  templatedCandidate,
  undefined,
  templatedState,
  selectedRequirement(templatedModel, "unit-1"),
  [],
  []
), {
  abilityId: "ability-1",
  cmdType: "ACTIVATE_ABILITY",
  sourceObjectId: "unit-1"
});

assert.equal(booleanFromRecord({ composable: false }, "composable", true), false);
assert.equal(safePromptSummary(["one", "two", "three", "four"]), "one、two、three");
assert.equal(choiceLabel({ id: "secret", label: "serverPaymentState" }), "服务端字段");
assert.equal(choiceLabelById(controls.sources, "hand-2"), "手牌 2");

console.log("Candidate composer model check passed.");

function commandFromActionPromptTemplate(template, selection, requirement) {
  if (!template?.cmdType) {
    return undefined;
  }

  const command = { cmdType: template.cmdType };
  for (const binding of template.bindings ?? []) {
    let value;
    if (binding.source === "selectedSource") {
      value = selection.sourceId;
    } else if (binding.source === "selectedTarget") {
      value = selection.targetObjectIds?.[0];
    } else if (binding.source === "selectedTargets") {
      value = selection.targetObjectIds ?? [];
    } else if (binding.source === "selectedDestination") {
      value = selection.destinationId;
    } else if (binding.source === "selectedMode") {
      value = selection.mode;
    } else if (binding.source === "selectedOptionalCosts") {
      value = selection.optionalCostIds ?? [];
    } else if (binding.source === "requirementMetadata") {
      value = requirement?.[binding.metadataKey];
    }
    if (binding.required && (value == null || value === "" || (Array.isArray(value) && value.length === 0))) {
      return undefined;
    }
    if (value != null && value !== "") {
      command[binding.field] = value;
    }
  }
  return command;
}

function redactInternalText(value) {
  return String(value).replace(/serverPaymentState|privateChoiceGraph/g, "服务端字段");
}

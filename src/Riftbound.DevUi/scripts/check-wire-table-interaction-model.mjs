import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const interactionExports = loadTsModule(
  resolve(scriptDir, "../src/components/match/wireTableInteractionModel.ts"),
  {
    candidateComposerKey: (candidate) => `${candidate.action}:${candidate.label}`,
    promptChoiceRoleLabel: (role) => ({
      destination: "位置",
      mode: "模式",
      optionalCost: "费用",
      source: "来源",
      target: "目标"
    })[role],
    promptChoiceRoleOrder: ["source", "mode", "destination", "target", "optionalCost"],
    promptChoiceSummaryObjectIds: (choice) => choice.objectIds ?? []
  }
);

const {
  buildWireInteractionMap,
  buildWireObjectHintMap,
  buildWireTimelineMap,
  candidateChoiceForObject,
  emptySelectionDraft,
  focusedCandidateSummaries,
  mergeWireTimelineMaps,
  sourceCandidateForObject,
  updateSelectionDraft
} = interactionExports;

const enabledCandidate = {
  action: "PLAY_CARD",
  choices: [
    choice("source", "source-card", ["source-1"]),
    choice("target", "target-card", ["target-1"]),
    choice("target", "backup-target", ["target-2"]),
    choice("destination", "battlefield-left", ["battlefield-1"]),
    choice("optionalCost", "echo-cost", ["rune-1"]),
    choice("mode", "fast-mode", ["mode-object-ignored"])
  ],
  enabled: true,
  label: "打出卡牌",
  reason: "可提交",
  steps: []
};
const disabledCandidate = {
  action: "TAP_RUNE",
  choices: [
    choice("source", "disabled-rune", ["disabled-1"])
  ],
  enabled: false,
  label: "横置符文",
  reason: "暂无服务端可执行候选",
  steps: []
};
const model = {
  candidates: [enabledCandidate, disabledCandidate],
  disabledObjectIds: new Set(["disabled-1"]),
  enabledObjectIds: new Set(["source-1"]),
  objectById: new Map([
    objectSummary("source-1", [enabledCandidate.choices[0]], "enabled", 1, 0),
    objectSummary("target-1", [enabledCandidate.choices[1]], "enabled", 1, 0),
    objectSummary("target-2", [enabledCandidate.choices[2]], "enabled", 1, 0),
    objectSummary("battlefield-1", [enabledCandidate.choices[3]], "enabled", 1, 0),
    objectSummary("rune-1", [enabledCandidate.choices[4]], "enabled", 1, 0),
    objectSummary("mode-object-ignored", [enabledCandidate.choices[5]], "enabled", 1, 0),
    objectSummary("disabled-1", [disabledCandidate.choices[0]], "disabled", 0, 1)
  ])
};

const focused = focusedCandidateSummaries(model.candidates, "source-1");
assert.deepEqual(focused, [enabledCandidate], "focused source should expose only enabled source candidates");
assert.equal(sourceCandidateForObject(model.candidates, "source-1"), enabledCandidate);
assert.equal(sourceCandidateForObject(model.candidates, "disabled-1"), undefined, "disabled candidates cannot seed a tabletop draft");

const targetChoice = candidateChoiceForObject(focused, "target-1");
assert.equal(targetChoice?.candidate, enabledCandidate);
assert.equal(targetChoice?.choice.id, "target-card");
assert.equal(candidateChoiceForObject(focused, "mode-object-ignored"), undefined, "mode choices must not be selected from board object clicks");

let draft = emptySelectionDraft("source-1", enabledCandidate);
assert.deepEqual(draft, {
  candidateKey: "PLAY_CARD:打出卡牌",
  optionalCostIds: [],
  sourceObjectId: "source-1",
  targetChoiceIds: []
});

draft = updateSelectionDraft(draft, "source-1", enabledCandidate, targetChoice.choice);
draft = updateSelectionDraft(draft, "source-1", enabledCandidate, enabledCandidate.choices[2]);
draft = updateSelectionDraft(draft, "source-1", enabledCandidate, enabledCandidate.choices[3]);
draft = updateSelectionDraft(draft, "source-1", enabledCandidate, enabledCandidate.choices[4]);
assert.deepEqual(draft.targetChoiceIds, ["backup-target", "target-card"], "latest target click should move to the front without duplicates");
assert.equal(draft.destinationId, "battlefield-left");
assert.deepEqual(draft.optionalCostIds, ["echo-cost"]);

let interaction = buildWireInteractionMap(model, focused, "source-1", draft);
assert.equal(interaction["source-1"], "source");
assert.equal(interaction["target-1"], "chosen");
assert.equal(interaction["target-2"], "chosen");
assert.equal(interaction["battlefield-1"], "chosen");
assert.equal(interaction["rune-1"], "chosen");
assert.equal(interaction["disabled-1"], "disabled");
assert.equal(interaction["mode-object-ignored"], undefined);

const hints = buildWireObjectHintMap(model, focused, "source-1", draft);
assert.equal(hints["source-1"]?.state, "source");
assert.deepEqual(hints["source-1"]?.roleLabels, ["来源"]);
assert.equal(hints["source-1"]?.nextClickLabel, "已选来源");
assert.equal(hints["source-1"]?.semanticSummary.includes("打出卡牌"), true);
assert.equal(hints["target-1"]?.state, "chosen");
assert.deepEqual(hints["target-1"]?.roleLabels, ["目标"]);
assert.equal(hints["target-1"]?.dataLabel, "chosen target");
assert.equal(hints["battlefield-1"]?.nextClickLabel, "已选择");
assert.deepEqual(hints["battlefield-1"]?.roleLabels, ["位置"]);
assert.equal(hints["rune-1"]?.dataLabel, "chosen optionalCost");
assert.equal(hints["disabled-1"]?.nextClickLabel, "暂不可用");
assert.equal(hints["disabled-1"]?.enabledCandidateCount, 0);
assert.equal(hints["disabled-1"]?.disabledCandidateCount, 1);
assert.equal(hints["mode-object-ignored"], undefined, "mode choices should stay out of tabletop object hints");

draft = updateSelectionDraft(draft, "source-1", enabledCandidate, enabledCandidate.choices[4]);
interaction = buildWireInteractionMap(model, focused, "source-1", draft);
assert.equal(interaction["rune-1"], "optionalCost", "clicking an optional cost twice should remove chosen state but keep role highlight");

const timelineMap = buildWireTimelineMap({
  id: "detail-1",
  lines: [],
  refs: [
    { id: "target-1", label: "目标", role: "目标" },
    { id: "target-1", label: "重复", role: "目标" },
    { id: "HIDDEN", label: "隐藏", role: "隐藏" },
    { id: "   ", label: "空", role: "空" }
  ],
  source: "rule",
  title: "规则详情"
});
assert.deepEqual(timelineMap, { "target-1": "rule" }, "timeline map must dedupe visible refs and ignore hidden placeholders");
assert.deepEqual(
  mergeWireTimelineMaps({ "target-1": "rule", "event-1": "event" }, { "event-1": "rule" }, { "ignored-1": undefined }),
  { "target-1": "rule", "event-1": "rule" },
  "timeline maps should merge in order while ignoring empty states"
);

console.log("Wire table interaction model check passed.");

function choice(role, id, objectIds) {
  return {
    id,
    label: id,
    objectIds,
    role
  };
}

function objectSummary(objectId, choices, state, enabledCandidateCount, disabledCandidateCount) {
  return [
    objectId,
    {
      choices,
      disabledCandidateCount,
      enabledCandidateCount,
      objectId,
      state
    }
  ];
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

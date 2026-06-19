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
    promptChoiceSummaryObjectIds: (choice) => choice.objectIds ?? []
  }
);

const {
  buildWireInteractionMap,
  buildWireTimelineMap,
  candidateChoiceForObject,
  emptySelectionDraft,
  focusedCandidateSummaries,
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
  enabledObjectIds: new Set(["source-1"])
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

console.log("Wire table interaction model check passed.");

function choice(role, id, objectIds) {
  return {
    id,
    label: id,
    objectIds,
    role
  };
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

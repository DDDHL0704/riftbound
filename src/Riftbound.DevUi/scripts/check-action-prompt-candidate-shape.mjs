import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/actionPromptCandidateShape.ts");
const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function("exports", "module", output)(moduleShim.exports, moduleShim);

const { candidateRequiresFurtherChoice, singlePromptChoiceId } = moduleShim.exports;

assert.equal(candidateRequiresFurtherChoice({ action: "PASS", enabled: true }), false);
assert.equal(candidateRequiresFurtherChoice({ action: "PLAY_CARD", enabled: true, targets: [{ id: "target-1" }] }), true);
assert.equal(candidateRequiresFurtherChoice({ action: "MOVE_UNIT", enabled: true, destinations: [{ id: "battlefield-left" }] }), true);
assert.equal(candidateRequiresFurtherChoice({ action: "LEGEND_ACT", enabled: true, modes: [{ id: "mode-1" }] }), true);
assert.equal(candidateRequiresFurtherChoice({ action: "PLAY_CARD", enabled: true, optionalCosts: [{ id: "cost-1" }] }), true);
assert.equal(candidateRequiresFurtherChoice({
  action: "TAP_RUNE",
  enabled: true,
  selectionSteps: [
    { choices: [{ id: "rune-1", label: "符文", objectIds: ["rune-1"] }], label: "来源", required: true, role: "source" }
  ],
  sources: [{ id: "rune-1" }, { id: "rune-2" }]
}), false);
assert.equal(candidateRequiresFurtherChoice({
  action: "PLAY_CARD",
  enabled: true,
  selectionSteps: [
    { choices: [{ id: "hand-1", label: "手牌", objectIds: ["hand-1"] }], label: "来源", required: true, role: "source" },
    { choices: [], label: "目标", required: true, role: "target" }
  ],
  targets: null
}), true);
assert.equal(candidateRequiresFurtherChoice({
  action: "PLAY_CARD",
  enabled: true,
  selectionSteps: [
    { choices: [{ id: "echo", label: "ECHO", objectIds: [] }], label: "费用", required: false, role: "optionalCost" }
  ]
}), true);

assert.equal(singlePromptChoiceId(undefined), undefined);
assert.equal(singlePromptChoiceId([]), undefined);
assert.equal(singlePromptChoiceId([{ id: "only" }]), "only");
assert.equal(singlePromptChoiceId([{ id: "" }]), undefined);
assert.equal(singlePromptChoiceId([{ id: "a" }, { id: "b" }]), undefined);

console.log("Action prompt candidate shape check passed.");

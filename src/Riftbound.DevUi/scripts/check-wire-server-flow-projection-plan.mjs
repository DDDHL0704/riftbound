import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const { buildWireServerFlowProjectionPlan } = loadTsModule(
  resolve(scriptDir, "../src/utils/wireServerFlowProjectionPlan.ts")
).exports;

const emptyPlan = buildWireServerFlowProjectionPlan();
assert.equal(emptyPlan.state, "empty");
assert.equal(emptyPlan.relatedObjectCount, 0);
assert.deepEqual(emptyPlan.objectIds, []);
assert.deepEqual(emptyPlan.timelineByObjectId, {});

const noFlowPlan = buildWireServerFlowProjectionPlan({
  actionable: false,
  actions: [],
  candidates: [],
  playerId: "P1",
  promptId: "prompt-1",
  reason: "等待",
  snapshotTick: 7
});
assert.equal(noFlowPlan.state, "empty");
assert.deepEqual(noFlowPlan.timelineByObjectId, {});

const linkedPlan = buildWireServerFlowProjectionPlan({
  actionable: true,
  actions: ["PLAY_CARD"],
  candidates: [],
  playerId: "P1",
  promptId: "prompt-2",
  reason: "可行动",
  serverFlow: {
    actionableForPromptPlayer: true,
    isResponsiblePlayer: true,
    lanes: [],
    nextStep: "选择服务端公开对象。",
    primaryLabel: "服务端窗口",
    promptPlayerId: "P1",
    promptType: "MAIN_ACTION",
    queueCounts: {},
    reason: "服务端公开关联对象。",
    relatedObjectIds: [" unit-1 ", "unit-1", "HIDDEN", "hidden", "   ", "battlefield-1"],
    responsiblePlayerId: "P1",
    state: "ready",
    stateLabel: "可提交",
    steps: [],
    summary: "服务端窗口",
    tone: "good"
  },
  snapshotTick: 7
});
assert.equal(linkedPlan.state, "linked");
assert.equal(linkedPlan.relatedObjectCount, 2);
assert.deepEqual(linkedPlan.objectIds, ["unit-1", "battlefield-1"]);
assert.deepEqual(linkedPlan.timelineByObjectId, {
  "battlefield-1": "rule",
  "unit-1": "rule"
});

console.log("Wire server flow projection plan check passed.");

function loadTsModule(sourcePath) {
  const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
  const output = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022
    }
  }).outputText;
  const moduleShim = { exports: {} };
  new Function("exports", "module", output)(moduleShim.exports, moduleShim);
  return moduleShim;
}

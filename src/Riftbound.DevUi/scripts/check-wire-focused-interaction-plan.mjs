import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const srcRoot = resolve(scriptDir, "../src");
const moduleCache = new Map();

const { buildWireFocusedInteractionPlan } = loadTsModule(resolve(srcRoot, "utils/wireFocusedInteractionPlan.ts")).exports;

const sourceObjectId = "p1-hand-spell";
const playCandidate = {
  action: "PLAY_CARD",
  commandTemplate: {
    bindings: [
      { field: "sourceObjectId", required: true, roleLabel: "来源", source: "selectedSource" },
      { asArray: true, field: "targetObjectIds", required: false, roleLabel: "目标", source: "selectedTargets" }
    ],
    cmdType: "PLAY_CARD"
  },
  enabled: true,
  label: "打出手牌",
  reason: "可提交",
  selectionSteps: [
    {
      choices: [{ id: sourceObjectId, label: "手牌法术", objectIds: [sourceObjectId] }],
      label: "来源",
      required: true,
      role: "source"
    },
    {
      choices: [{ id: "p2-unit-1", label: "敌方单位", objectIds: ["p2-unit-1"] }],
      label: "目标",
      required: false,
      role: "target"
    }
  ],
  sources: [{ id: sourceObjectId, label: "手牌法术", objectIds: [sourceObjectId] }],
  targets: [{ id: "p2-unit-1", label: "敌方单位", objectIds: ["p2-unit-1"] }]
};
const tapCandidate = {
  action: "TAP_RUNE",
  commandTemplate: {
    bindings: [{ field: "sourceObjectId", required: true, roleLabel: "来源", source: "selectedSource" }],
    cmdType: "TAP_RUNE"
  },
  enabled: true,
  label: "横置符文",
  reason: "可提交",
  sources: [{ id: sourceObjectId, label: "手牌法术", objectIds: [sourceObjectId] }]
};

const plan = buildWireFocusedInteractionPlan({
  canSubmitCommands: true,
  disabledByConnection: false,
  prompt: {
    actionable: true,
    actions: ["PLAY_CARD", "TAP_RUNE"],
    candidates: [playCandidate, tapCandidate],
    playerId: "P1",
    promptId: "prompt-1",
    reason: "等待行动",
    snapshotTick: 42,
    view: {
      message: "选择一个服务端候选行动。",
      title: "主行动",
      type: "MAIN_ACTION"
    }
  },
  selectionDraft: {
    candidateKey: "PLAY_CARD::打出手牌",
    optionalCostIds: [],
    sourceObjectId,
    targetChoiceIds: ["p2-unit-1"]
  },
  snapshot: {
    activePlayerId: "P1",
    lanes: {},
    players: {
      P1: {
        objects: {
          [sourceObjectId]: {
            cardNo: "OGN-001/298",
            controllerId: "P1",
            objectId: sourceObjectId,
            ownerId: "P1",
            tags: ["CARD_TYPE:SPELL"]
          }
        }
      },
      P2: {
        objects: {
          "p2-unit-1": {
            cardNo: "OGN-002/298",
            controllerId: "P2",
            objectId: "p2-unit-1",
            ownerId: "P2",
            tags: ["CARD_TYPE:UNIT"]
          }
        }
      }
    },
    stack: [],
    tick: 42,
    timing: {},
    turnNumber: 1,
    turnState: "MAIN"
  },
  sourceControllerId: "P1",
  sourceObjectId
});

assert.equal(plan.sourceObject.serverCandidateLabel, "2 可用 / 0 禁用");
assert.equal(plan.relatedCandidateRows.length, 2);
assert.equal(plan.sourceCandidates.length, 2);
assert.equal(plan.actionEntries.length, 2);
assert.equal(plan.actionEntries.find((entry) => entry.candidate.action === "PLAY_CARD")?.mode, "composer");
assert.equal(plan.actionEntries.find((entry) => entry.candidate.action === "TAP_RUNE")?.mode, "button");
assert.equal(plan.actionEntries.find((entry) => entry.candidate.action === "TAP_RUNE")?.actionPlan.command?.cmdType, "TAP_RUNE");
assert.equal(plan.grammarPlan.commandType, "PLAY_CARD");
assert.equal(plan.grammarPlan.steps.find((step) => step.role === "source")?.state, "locked");
assert.equal(plan.grammarPlan.steps.find((step) => step.role === "target")?.state, "selected");
assert.equal(plan.draft?.targetCount, 1);
assert.equal(plan.promptCandidateList.versionLabel, "版本：prompt-1 / tick 42");
assert.ok(plan.sourceCandidatePaths[0]?.steps.some((step) => step.label === "目标"));

console.log("Wire focused interaction plan check passed.");

function loadTsModule(filename) {
  const resolved = resolve(filename);
  const cached = moduleCache.get(resolved);
  if (cached) {
    return cached;
  }

  const source = readFileSync(resolved, "utf8");
  const output = ts.transpileModule(source, {
    compilerOptions: {
      esModuleInterop: true,
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022
    }
  }).outputText;
  const module = { exports: {} };
  moduleCache.set(resolved, module);

  const requireShim = (id) => {
    if (id.startsWith(".")) {
      const target = resolve(dirname(resolved), id);
      if (target.endsWith("/types/protocol") || target.endsWith("/types/catalog")) {
        return {};
      }

      return loadTsModule(`${target}.ts`).exports;
    }

    throw new Error(`Unexpected import in focused interaction check: ${id}`);
  };

  new Function("exports", "module", "require", output)(module.exports, module, requireShim);
  return module;
}

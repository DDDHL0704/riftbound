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
  playerId: "P1",
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
assert.equal(plan.submissionGate.state, "connected");
assert.equal(plan.windowGate.state, "ready");
assert.equal(plan.readiness.state, "ready");
assert.equal(plan.readiness.canSubmit, true);
assert.equal(plan.readiness.commandType, "PLAY_CARD");
assert.equal(plan.readiness.enabledCount, 2);
assert.equal(plan.readiness.missingRequiredCount, 0);
assert.deepEqual(plan.legalActionRows.map((row) => `${row.label}:${row.state}:${row.stateLabel}`), [
  "打出手牌:ready:可提交",
  "横置符文:ready:可提交"
]);
assert.equal(plan.legalActionRows[0].commandType, "PLAY_CARD");
assert.deepEqual(plan.legalActionRows[0].roleLabels, ["来源"]);
assert.deepEqual(plan.legalActionRows[0].missingRequiredLabels, []);

const noFocusPlan = buildWireFocusedInteractionPlan({
  canSubmitCommands: true,
  disabledByConnection: false,
  playerId: "P1",
  prompt: promptFor([playCandidate]),
  snapshot: emptySnapshot()
});
assert.equal(noFocusPlan.readiness.state, "no-focus");
assert.equal(noFocusPlan.readiness.canSubmit, false);

const notCandidatePlan = buildWireFocusedInteractionPlan({
  canSubmitCommands: true,
  disabledByConnection: false,
  playerId: "P1",
  prompt: promptFor([playCandidate]),
  snapshot: emptySnapshot(),
  sourceObjectId: "object-not-in-server-candidates"
});
assert.equal(notCandidatePlan.readiness.state, "not-candidate");
assert.equal(notCandidatePlan.readiness.canSubmit, false);

const disconnectedPlan = buildWireFocusedInteractionPlan({
  canSubmitCommands: true,
  disabledByConnection: true,
  playerId: "P1",
  prompt: promptFor([playCandidate]),
  snapshot: emptySnapshot(),
  sourceObjectId
});
assert.equal(disconnectedPlan.readiness.state, "submission-gate-blocked");
assert.equal(disconnectedPlan.readiness.canSubmit, false);
assert.equal(disconnectedPlan.readiness.nextStepLabel, "行动入口未就绪，等待服务端窗口、连接或快照同步。");

const staleSubmissionPlan = buildWireFocusedInteractionPlan({
  canSubmitCommands: true,
  disabledByConnection: true,
  playerId: "P1",
  prompt: promptFor([playCandidate]),
  snapshot: emptySnapshot(),
  sourceObjectId,
  submissionGate: {
    canSubmit: false,
    reason: "行动提示属于 tick 7，当前桌面快照是 tick 8。",
    state: "stale-snapshot",
    stateLabel: "等待同步"
  }
});
assert.equal(staleSubmissionPlan.readiness.state, "submission-gate-blocked");
assert.equal(staleSubmissionPlan.readiness.nextStepLabel, "行动提示属于 tick 7，当前桌面快照是 tick 8。");
assert.equal(staleSubmissionPlan.grammarPlan.steps.find((step) => step.role === "submit")?.stateLabel, "等待同步");

const wrongPlayerPlan = buildWireFocusedInteractionPlan({
  canSubmitCommands: true,
  disabledByConnection: false,
  playerId: "P2",
  prompt: promptFor([playCandidate]),
  snapshot: emptySnapshot(),
  sourceObjectId
});
assert.equal(wrongPlayerPlan.readiness.state, "window-blocked");
assert.equal(wrongPlayerPlan.windowGate.state, "wrong-player");
assert.equal(wrongPlayerPlan.actionEntries[0].disabledByActionGate, true);
assert.equal(wrongPlayerPlan.actionEntries[0].actionPlan.disabled, true);
assert.ok(wrongPlayerPlan.actionEntries[0].actionPlan.title.includes("只读观察"));
assert.equal(wrongPlayerPlan.legalActionRows[0].state, "blocked");

const blockedPlan = buildWireFocusedInteractionPlan({
  canSubmitCommands: true,
  disabledByConnection: false,
  playerId: "P1",
  prompt: promptFor([{ ...playCandidate, enabled: false, reason: "法力不足" }]),
  snapshot: emptySnapshot(),
  sourceObjectId
});
assert.equal(blockedPlan.readiness.state, "server-blocked");
assert.equal(blockedPlan.readiness.canSubmit, false);
assert.equal(blockedPlan.readiness.nextStepLabel, "法力不足");
assert.equal(blockedPlan.sourceCandidates.length, 1);
assert.equal(blockedPlan.actionEntries.length, 1);
assert.equal(blockedPlan.actionEntries[0].candidate.enabled, false);
assert.equal(blockedPlan.actionEntries[0].actionPlan.disabled, true);
assert.equal(blockedPlan.actionEntries[0].actionPlan.title, "法力不足");
assert.equal(blockedPlan.grammarPlan.state, "blocked");
assert.equal(blockedPlan.grammarPlan.candidateLabel, "打出手牌");
assert.equal(blockedPlan.sourceCandidatePaths.length, 1);
assert.equal(blockedPlan.legalActionRows[0].state, "blocked");
assert.equal(blockedPlan.legalActionRows[0].nextStepLabel, "法力不足");

const requiredTargetCandidate = {
  ...playCandidate,
  selectionSteps: playCandidate.selectionSteps.map((step) =>
    step.role === "target" ? { ...step, required: true } : step)
};
const needsSelectionPlan = buildWireFocusedInteractionPlan({
  canSubmitCommands: true,
  disabledByConnection: false,
  playerId: "P1",
  prompt: promptFor([requiredTargetCandidate]),
  snapshot: emptySnapshot(),
  sourceObjectId
});
assert.equal(needsSelectionPlan.readiness.state, "needs-selection");
assert.equal(needsSelectionPlan.readiness.canSubmit, false);
assert.equal(needsSelectionPlan.readiness.missingRequiredCount, 1);
assert.ok(needsSelectionPlan.readiness.nextStepLabel.includes("目标"));
assert.equal(needsSelectionPlan.legalActionRows[0].state, "needs-selection");
assert.deepEqual(needsSelectionPlan.legalActionRows[0].missingRequiredLabels, ["目标"]);
assert.equal(needsSelectionPlan.legalActionRows[0].nextStepLabel, "选择目标");

const targetOnlyPlan = buildWireFocusedInteractionPlan({
  canSubmitCommands: true,
  disabledByConnection: false,
  playerId: "P1",
  prompt: promptFor([requiredTargetCandidate]),
  snapshot: emptySnapshot(),
  sourceObjectId: "p2-unit-1"
});
assert.equal(targetOnlyPlan.readiness.state, "not-candidate");
assert.equal(targetOnlyPlan.legalActionRows.length, 1);
assert.equal(targetOnlyPlan.legalActionRows[0].state, "informational");
assert.deepEqual(targetOnlyPlan.legalActionRows[0].roleLabels, ["目标"]);
assert.ok(targetOnlyPlan.legalActionRows[0].nextStepLabel.includes("作为目标"));

console.log("Wire focused interaction plan check passed.");

function promptFor(candidates) {
  return {
    actionable: true,
    actions: candidates.map((candidate) => candidate.action),
    candidates,
    playerId: "P1",
    promptId: "prompt-check",
    reason: "等待行动",
    snapshotTick: 1,
    view: {
      message: "检查焦点状态。",
      title: "检查",
      type: "MAIN_ACTION"
    }
  };
}

function emptySnapshot() {
  return {
    activePlayerId: "P1",
    lanes: {},
    players: {},
    stack: [],
    tick: 1,
    timing: {},
    turnNumber: 1,
    turnState: "MAIN"
  };
}

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

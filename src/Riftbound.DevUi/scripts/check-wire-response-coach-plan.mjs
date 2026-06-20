import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const srcRoot = resolve(scriptDir, "../src");
const moduleCache = new Map();

const { buildWireResponseCoachPlan } = loadTsModule(resolve(srcRoot, "utils/wireResponseCoachPlan.ts")).exports;

const basePrompt = promptFor("P1", [
  {
    action: "PLAY_CARD",
    commandTemplate: {
      bindings: [
        { field: "sourceObjectId", label: "来源", required: true, source: "selectedSource" },
        { asArray: true, field: "targetObjectIds", label: "目标", required: true, source: "selectedTargets" },
        { field: "serverPaymentState", label: "费用状态", required: false, source: "requirementMetadata" }
      ],
      cmdType: "PLAY_CARD"
    },
    enabled: true,
    label: "打出手牌",
    reason: "可提交",
    selectionSteps: [
      {
        choices: [{ id: "p1-hand-1", label: "手牌法术", objectIds: ["p1-hand-1"] }],
        label: "来源",
        required: true,
        role: "source"
      },
      {
        choices: [{ id: "p2-unit-1", label: "敌方单位", objectIds: ["p2-unit-1"] }],
        label: "目标",
        required: true,
        role: "target"
      }
    ],
    sources: [{ id: "p1-hand-1", label: "手牌法术", objectIds: ["p1-hand-1"] }],
    targets: [{ id: "p2-unit-1", label: "敌方单位", objectIds: ["p2-unit-1"] }]
  }
]);

const selectingSource = buildWireResponseCoachPlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: basePrompt,
  snapshot: snapshot()
});
assert.equal(selectingSource.state, "selecting");
assert.equal(selectingSource.stepRole, "source");
assert.equal(selectingSource.primaryLabel, "选择来源");
assert.equal(selectingSource.rows.find((row) => row.key === "prompt")?.state, "server");
assert.equal(selectingSource.rows.find((row) => row.key === "route")?.state, "waiting");

const selectingTarget = buildWireResponseCoachPlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: basePrompt,
  selectionDraft: {
    candidateKey: "PLAY_CARD::打出手牌",
    optionalCostIds: [],
    sourceObjectId: "p1-hand-1",
    targetChoiceIds: []
  },
  snapshot: snapshot()
});
assert.equal(selectingTarget.state, "selecting");
assert.equal(selectingTarget.stepRole, "target");
assert.equal(selectingTarget.rows.find((row) => row.key === "draft")?.state, "selecting");
assert.equal(selectingTarget.rows.find((row) => row.key === "route")?.state, "selecting");

const ready = buildWireResponseCoachPlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: basePrompt,
  selectionDraft: {
    candidateKey: "PLAY_CARD::打出手牌",
    optionalCostIds: [],
    sourceObjectId: "p1-hand-1",
    targetChoiceIds: ["p2-unit-1"]
  },
  snapshot: snapshot()
});
assert.equal(ready.state, "ready");
assert.equal(ready.stepRole, "submit");
assert.equal(ready.rows.find((row) => row.key === "submit")?.state, "ready");
assert.equal(ready.metrics.find((metric) => metric.key === "route")?.value, "可送服务端校验");

const stale = buildWireResponseCoachPlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: basePrompt,
  snapshot: snapshot(),
  submissionGate: {
    canSubmit: false,
    reason: "行动提示属于 tick 7，当前桌面快照是 tick 8。",
    state: "stale-snapshot",
    stateLabel: "等待同步"
  }
});
assert.equal(stale.state, "blocked");
assert.equal(stale.stepRole, "sync");
assert.equal(stale.rows.find((row) => row.key === "gate")?.state, "blocked");

const opponent = buildWireResponseCoachPlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: promptFor("P2", basePrompt.candidates),
  snapshot: { ...snapshot(), activePlayerId: "P2" }
});
assert.equal(opponent.state, "opponent");
assert.equal(opponent.stepRole, "wait");

const resolving = buildWireResponseCoachPlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: { ...basePrompt, actionable: false },
  snapshot: {
    ...snapshot(),
    stack: [{ effectKind: "SPELL", sourceObjectId: "p1-hand-1", stackItemId: "stack-1" }]
  }
});
assert.equal(resolving.state, "resolving");
assert.equal(resolving.stepRole, "window");

console.log("Wire response coach plan check passed.");

function promptFor(playerId, candidates) {
  return {
    actionable: true,
    actions: candidates.map((candidate) => candidate.action),
    candidates,
    playerId,
    promptId: `prompt-${playerId}`,
    reason: "等待行动",
    snapshotTick: 8,
    view: {
      message: "选择服务端候选行动。",
      title: "主行动",
      type: "MAIN_ACTION"
    }
  };
}

function snapshot() {
  return {
    activePlayerId: "P1",
    lanes: {},
    players: {
      P1: {
        objects: {
          "p1-hand-1": { cardNo: "OGN-001/298", controllerId: "P1", objectId: "p1-hand-1", ownerId: "P1" }
        }
      },
      P2: {
        objects: {
          "p2-unit-1": { cardNo: "OGN-002/298", controllerId: "P2", objectId: "p2-unit-1", ownerId: "P2" }
        }
      }
    },
    stack: [],
    tick: 8,
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

    throw new Error(`Unexpected import in response coach check: ${id}`);
  };

  new Function("exports", "module", "require", output)(module.exports, module, requireShim);
  return module;
}

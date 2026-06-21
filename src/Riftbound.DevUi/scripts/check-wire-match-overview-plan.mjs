import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const srcRoot = resolve(scriptDir, "../src");
const moduleCache = new Map();

const { buildWireMatchOverviewPlan } = loadTsModule(resolve(srcRoot, "utils/wireMatchOverviewPlan.ts")).exports;

const gate = {
  canSubmit: true,
  reason: "行动提示和桌面快照同属 tick 7。",
  state: "connected",
  stateLabel: "可提交"
};

const readyPlan = buildWireMatchOverviewPlan({
  connectionStatus: "connected",
  events: [],
  playerId: "P1",
  prompt: prompt(),
  selectedObjectContext: objectContext(),
  selectedObjectId: "unit-1",
  snapshot: snapshot(),
  submissionGate: gate
});
assert.equal(readyPlan.state, "ready");
assert.equal(readyPlan.stateLabel, "可行动");
assert.equal(readyPlan.tone, "good");
assert.equal(readyPlan.rows.length, 5);
assert.equal(row(readyPlan, "window").state, "ready");
assert.equal(row(readyPlan, "candidates").state, "ready");
assert.equal(row(readyPlan, "candidates").sourceLabel, "服务端候选列表");
assert.equal(row(readyPlan, "rules").state, "empty");
assert.equal(row(readyPlan, "focus").state, "server");
assert.equal(row(readyPlan, "focus").count, 2);
assert.ok(row(readyPlan, "focus").summary.includes("结算链 1"));
assert.equal(row(readyPlan, "timeline").state, "empty");
assert.equal(readyPlan.metrics.find((metric) => metric.key === "candidates")?.value, "1/1");

const resolvingPlan = buildWireMatchOverviewPlan({
  connectionStatus: "connected",
  events: [],
  playerId: "P1",
  prompt: prompt({ title: "结算链响应", type: "STACK_PRIORITY" }),
  snapshot: snapshot({
    stack: [{
      cardNo: "OGN-001",
      controllerId: "P1",
      effectKind: "DAMAGE",
      sourceObjectId: "unit-1",
      stackItemId: "stack-1",
      targetObjectIds: ["unit-2"]
    }]
  }),
  submissionGate: gate
});
assert.equal(resolvingPlan.state, "resolving");
assert.equal(resolvingPlan.tone, "warn");
assert.equal(row(resolvingPlan, "rules").state, "warning");
assert.equal(row(resolvingPlan, "rules").value, "stack");
assert.equal(resolvingPlan.metrics.find((metric) => metric.key === "stack")?.value, "1 项");

const blockedPlan = buildWireMatchOverviewPlan({
  connectionStatus: "connected",
  events: [],
  playerId: "P1",
  prompt: prompt(),
  snapshot: snapshot({
    timing: {
      pendingTaskQueue: {
        isBlocking: true,
        tasks: [{ actingPlayerId: "P1", kind: "PAY_COST", status: "PENDING", taskId: "task-1" }]
      }
    }
  }),
  submissionGate: gate
});
assert.equal(blockedPlan.state, "blocked");
assert.equal(blockedPlan.stateLabel, "阻塞");
assert.equal(row(blockedPlan, "rules").state, "blocked");

const reviewPlan = buildWireMatchOverviewPlan({
  connectionStatus: "connected",
  events: [],
  playerId: "P1",
  prompt: prompt({ actionable: false, title: "等待" }),
  snapshot: snapshot(),
  submissionGate: gate,
  timelineDetail: {
    id: "event:STACK_ITEM_ADDED:0",
    refs: [{ visibility: "visible" }, { visibility: "missing" }, { visibility: "hidden" }],
    source: "event",
    title: "加入结算链"
  }
});
assert.equal(reviewPlan.state, "review");
assert.equal(row(reviewPlan, "timeline").count, 2);
assert.equal(row(reviewPlan, "timeline").sourceLabel, "事件详情");

const disconnectedPlan = buildWireMatchOverviewPlan({
  connectionStatus: "disconnected",
  events: [],
  playerId: "P1",
  prompt: prompt(),
  snapshot: snapshot(),
  submissionGate: { ...gate, canSubmit: false, state: "disconnected", stateLabel: "未连接" }
});
assert.equal(disconnectedPlan.state, "disconnected");
assert.equal(disconnectedPlan.tone, "bad");
assert.equal(row(disconnectedPlan, "window").state, "blocked");

console.log("Wire match overview plan check passed.");

function row(plan, key) {
  return plan.rows.find((item) => item.key === key);
}

function prompt({
  actionable = true,
  title = "主行动",
  type = "MAIN_ACTION"
} = {}) {
  return {
    actionable,
    actions: actionable ? ["END_TURN"] : [],
    candidates: actionable ? [{ action: "END_TURN", enabled: true, label: "结束回合", reason: "可结束回合" }] : [],
    playerId: "P1",
    promptId: "prompt-1",
    reason: actionable ? "可行动" : "等待服务端窗口",
    snapshotTick: 7,
    view: {
      message: title,
      responsibility: {
        actionableForPromptPlayer: actionable,
        isResponsiblePlayer: actionable,
        nextStep: actionable ? "根据服务端候选行动。" : "等待服务端。",
        promptPlayerId: "P1",
        promptType: type,
        relatedObjectIds: [],
        responsiblePlayerId: "P1",
        state: actionable ? "PLAYER_ACTION" : "WAITING"
      },
      title,
      type
    }
  };
}

function snapshot(overrides = {}) {
  const timingOverride = overrides.timing ?? {};
  return {
    activePlayerId: "P1",
    lanes: {},
    players: {
      P1: { id: "P1", objects: {}, zones: {} },
      P2: { id: "P2", objects: {}, zones: {} }
    },
    stack: overrides.stack ?? [],
    tick: 7,
    timing: {
      battleResolutions: [],
      battlefieldResolutions: [],
      pendingTaskQueue: {
        isBlocking: false,
        tasks: []
      },
      phase: "MAIN",
      roomStatus: "IN_PROGRESS",
      triggerQueue: [],
      turnWindow: {
        actingPlayerId: "P1",
        state: "NEUTRAL_OPEN"
      },
      ...timingOverride
    },
    turnNumber: 1,
    turnState: "MAIN"
  };
}

function objectContext() {
  return {
    candidateLinks: [],
    candidateSource: "server",
    contextBoundary: "服务端对象上下文只公开当前行动提示中的对象候选。",
    contextSource: "server-action-prompt",
    eventLinks: [],
    objectId: "unit-1",
    promptDisabledCount: 1,
    promptEnabledCount: 1,
    serverRelations: [],
    stackRoles: ["结算来源"],
    stateLabels: ["4 战力"],
    zone: { kind: "battlefield", label: "左侧战场", playerId: "P1" }
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

    throw new Error(`Unexpected import in wire match overview plan check: ${id}`);
  };

  new Function("exports", "module", "require", output)(module.exports, module, requireShim);
  return module;
}

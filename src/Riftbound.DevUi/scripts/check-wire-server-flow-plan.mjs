import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const srcRoot = resolve(scriptDir, "../src");
const moduleCache = new Map();

const { buildWireServerFlowPlan } = loadTsModule(resolve(srcRoot, "utils/wireServerFlowPlan.ts")).exports;

const connectedGate = {
  canSubmit: true,
  reason: "行动提示和桌面快照同属 tick 7。",
  state: "connected",
  stateLabel: "可提交"
};

const readyPlan = buildWireServerFlowPlan({
  connectionStatus: "connected",
  events: [],
  playerId: "P1",
  prompt: prompt({ title: "主行动" }),
  snapshot: snapshot(),
  submissionGate: connectedGate
});
assert.equal(readyPlan.state, "ready");
assert.equal(readyPlan.primaryLabel, "提交给服务端");
assert.equal(readyPlan.metrics.find((metric) => metric.key === "prompt").value, "主行动");
assert.equal(readyPlan.lanes.find((lane) => lane.key === "stack").count, 0);
assert.equal(readyPlan.steps.length, 4);

const stackPlan = buildWireServerFlowPlan({
  connectionStatus: "connected",
  events: [],
  playerId: "P1",
  prompt: prompt({ title: "结算链响应", type: "STACK_PRIORITY" }),
  snapshot: snapshot({
    stack: [
      {
        cardNo: "OGN-001",
        controllerId: "P1",
        effectKind: "DAMAGE",
        sourceObjectId: "unit-1",
        stackItemId: "stack-1",
        targetObjectIds: ["unit-2"]
      }
    ]
  }),
  submissionGate: connectedGate
});
assert.equal(stackPlan.state, "respond");
assert.equal(stackPlan.primaryLabel, "响应结算链");
assert.equal(stackPlan.detail?.id, "rule:stack:stack-1");
assert.equal(stackPlan.lanes.find((lane) => lane.key === "stack").state, "active");
assert.equal(stackPlan.steps[0].state, "respond");

const blockedPlan = buildWireServerFlowPlan({
  connectionStatus: "connected",
  events: [],
  playerId: "P1",
  prompt: prompt({ title: "规则任务" }),
  snapshot: snapshot({
    timing: {
      pendingTaskQueue: {
        isBlocking: true,
        tasks: [
          {
            actingPlayerId: "P1",
            kind: "PAY_COST",
            status: "PENDING",
            taskId: "task-1"
          }
        ]
      }
    }
  }),
  submissionGate: connectedGate
});
assert.equal(blockedPlan.state, "blocked");
assert.equal(blockedPlan.primaryLabel, "规则任务阻塞");
assert.equal(blockedPlan.detail?.id, "rule:task:task-1");
assert.equal(blockedPlan.lanes.find((lane) => lane.key === "task").state, "blocked");

const historyPlan = buildWireServerFlowPlan({
  connectionStatus: "connected",
  events: [
    {
      description: "战场控制结算完成",
      kind: "BATTLEFIELD_CONTROL_RESOLVED",
      objectRefs: [{ objectId: "battlefield-1", role: "战场" }],
      payload: {}
    }
  ],
  playerId: "P1",
  prompt: prompt({ actionable: false, title: "等待" }),
  snapshot: snapshot(),
  submissionGate: connectedGate
});
assert.equal(historyPlan.state, "history");
assert.equal(historyPlan.primaryLabel, "规则事件回看");
assert.equal(historyPlan.detail?.id, "rule:event:BATTLEFIELD_CONTROL_RESOLVED:0");
assert.equal(historyPlan.lanes.find((lane) => lane.key === "resolution").count, 1);

console.log("Wire server flow plan check passed.");

function prompt({
  actionable = true,
  title = "主行动",
  type = "MAIN_ACTION"
} = {}) {
  return {
    actionable,
    actions: actionable ? ["END_TURN"] : [],
    candidates: actionable
      ? [
          {
            action: "END_TURN",
            enabled: true,
            label: "结束回合",
            reason: "可结束回合"
          }
        ]
      : [],
    playerId: "P1",
    promptId: "prompt-1",
    reason: actionable ? "可行动" : "等待服务端窗口",
    snapshotTick: 7,
    view: {
      message: title,
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
      P1: { id: "P1", zones: {}, objects: {} },
      P2: { id: "P2", zones: {}, objects: {} }
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

    throw new Error(`Unexpected import in wire server flow plan check: ${id}`);
  };

  new Function("exports", "module", "require", output)(module.exports, module, requireShim);
  return module;
}

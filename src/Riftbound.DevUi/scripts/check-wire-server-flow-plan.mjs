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

const serverFlowFixture = {
  actionableForPromptPlayer: true,
  candidateCount: 2,
  disabledCandidateCount: 1,
  enabledCandidateCount: 1,
  isResponsiblePlayer: true,
  lanes: [
    { count: 1, headline: "DAMAGE / OGN-001", key: "stack", label: "结算链", state: "active" },
    { count: 0, headline: "无任务", key: "task", label: "任务", state: "empty" },
    { count: 0, headline: "无触发", key: "trigger", label: "触发", state: "empty" },
    { count: 0, headline: "无结算记录", key: "resolution", label: "结算", state: "empty" }
  ],
  nextStep: "按服务端 prompt 选择响应或让过。",
  primaryLabel: "响应结算链",
  promptPlayerId: "P1",
  promptType: "STACK_PRIORITY",
  queueCounts: { stack: 1 },
  reason: "服务端声明结算链存在。",
  relatedBattlefieldId: "bf-from-server-flow",
  relatedObjectIds: ["unit-1"],
  relatedObjects: [{ objectId: "unit-1", role: "结算来源" }],
  relatedSpellDuelId: "spell-duel-from-server-flow",
  responsiblePlayerId: "P1",
  state: "respond",
  stateLabel: "响应",
  steps: [
    {
      detail: "服务端候选裁定。",
      key: "stack",
      label: "结算链",
      role: "stack",
      state: "respond",
      stateLabel: "响应",
      value: "1 项"
    }
  ],
  summary: "优先行动 / 响应 / 按服务端 prompt 选择响应或让过。",
  topStackItemId: "stack-1",
  tone: "info"
};

const serverBackedPlan = buildWireServerFlowPlan({
  connectionStatus: "connected",
  events: [],
  playerId: "P1",
  prompt: prompt({
    serverFlow: serverFlowFixture,
    title: "优先行动",
    type: "STACK_PRIORITY"
  }),
  snapshot: snapshot(),
  submissionGate: connectedGate
});
assert.equal(serverBackedPlan.state, "respond");
assert.equal(serverBackedPlan.primaryLabel, "响应结算链");
assert.equal(serverBackedPlan.metrics.find((metric) => metric.key === "source").value, "服务端");
assert.equal(serverBackedPlan.metrics.find((metric) => metric.key === "prompt").value, "优先行动");
assert.equal(serverBackedPlan.metrics.find((metric) => metric.key === "top-stack").value, "stack-1");
assert.equal(serverBackedPlan.metrics.find((metric) => metric.key === "battlefield").value, "bf-from-server-flow");
assert.equal(serverBackedPlan.metrics.find((metric) => metric.key === "spell-duel").value, "spell-duel-from-server-flow");
assert.equal(serverBackedPlan.metrics.find((metric) => metric.key === "related").value, "1");
assert.deepEqual(serverBackedPlan.relatedObjectIds, ["unit-1"]);
assert.equal(serverBackedPlan.relatedObjectCount, 1);
assert.equal(serverBackedPlan.relatedActionRows[0].state, "unknown");
assert.equal(serverBackedPlan.relatedActionRows[0].nextStepLabel, "服务端声明相关，但当前 prompt 未把它列为可选择对象。");
assert.equal(serverBackedPlan.detail?.id, "server-flow:STACK_PRIORITY:P1:related");
assert.equal(serverBackedPlan.detail?.title, "服务端关联对象");
assert.deepEqual(serverBackedPlan.detail?.refs, [{ id: "unit-1", role: "结算来源" }]);
assert.equal(serverBackedPlan.detailButtonLabel, "打开关联对象");
assert.equal(serverBackedPlan.lanes.find((lane) => lane.key === "stack").headline, "DAMAGE / OGN-001");
assert.equal(serverBackedPlan.steps[0].role, "stack");
assert.equal(serverBackedPlan.steps[0].timelineDetail?.id, "server-flow:STACK_PRIORITY:P1:step:stack:0");
assert.equal(serverBackedPlan.steps[0].timelineDetail?.title, "服务端流程：结算链");
assert.equal(serverBackedPlan.steps[0].timelineDetail?.lines.find((line) => line.label === "角色").value, "stack");
assert.equal(serverBackedPlan.steps[0].timelineDetail?.lines.find((line) => line.label === "说明").value, "服务端候选裁定。");
assert.deepEqual(serverBackedPlan.steps[0].timelineDetail?.refs, [{ id: "unit-1", role: "结算来源" }]);

const crowdedServerFlowFixture = {
  ...serverFlowFixture,
  reason: "服务端声明结算链、规则任务与触发队列同时存在。",
  steps: [
    {
      detail: "当前窗口由服务端裁定。",
      key: "prompt",
      label: "行动窗口",
      role: "window",
      state: "waiting",
      stateLabel: "等待",
      value: "结算链响应"
    },
    {
      detail: "责任方仍由服务端指定。",
      key: "responsibility",
      label: "责任方",
      role: "responsibility",
      state: "ready",
      stateLabel: "负责",
      value: "P1"
    },
    {
      detail: "已有项目等待响应。",
      key: "stack",
      label: "结算链",
      role: "stack",
      state: "respond",
      stateLabel: "响应",
      value: "1 项"
    },
    {
      detail: "规则任务阻塞主阶段行动。",
      key: "task",
      label: "规则任务",
      role: "task",
      state: "blocked",
      stateLabel: "阻塞",
      value: "1 项"
    },
    {
      detail: "触发等待服务端结算。",
      key: "trigger",
      label: "触发队列",
      role: "trigger",
      state: "server",
      stateLabel: "服务端",
      value: "1 项"
    },
    {
      detail: "前端只提交服务端候选，不重算合法性。",
      key: "candidate",
      label: "候选",
      role: "candidate",
      state: "ready",
      stateLabel: "2 可提交",
      value: "WAIT / SURRENDER"
    }
  ],
  summary: "拥挤流程 / 响应 / 按服务端 prompt 选择响应或让过。"
};

const crowdedServerBackedPlan = buildWireServerFlowPlan({
  connectionStatus: "connected",
  events: [],
  playerId: "P1",
  prompt: prompt({
    serverFlow: crowdedServerFlowFixture,
    title: "结算链响应",
    type: "STACK_PRIORITY"
  }),
  snapshot: snapshot(),
  submissionGate: connectedGate
});
assert.equal(crowdedServerBackedPlan.steps.length, 6);
assert.deepEqual(
  crowdedServerBackedPlan.steps.map((step) => step.key),
  ["server:prompt", "server:responsibility", "server:stack", "server:task", "server:trigger", "server:candidate"]
);
assert.deepEqual(
  crowdedServerBackedPlan.steps.map((step) => step.role),
  ["window", "responsibility", "stack", "task", "trigger", "candidate"]
);
assert.equal(crowdedServerBackedPlan.steps.at(-1).value, "WAIT / SURRENDER");
assert.equal(crowdedServerBackedPlan.steps.at(-1).detail, "前端只提交服务端候选，不重算合法性。");
assert.equal(crowdedServerBackedPlan.steps.at(-1).timelineDetail?.id, "server-flow:STACK_PRIORITY:P1:step:candidate:5");
assert.equal(crowdedServerBackedPlan.steps.at(-1).timelineDetail?.lines.find((line) => line.label === "值").value, "WAIT / SURRENDER");
assert.equal(crowdedServerBackedPlan.steps.at(-1).timelineDetail?.lines.find((line) => line.label === "责任玩家").mine, true);

const serverFlowActionBridgePlan = buildWireServerFlowPlan({
  connectionStatus: "connected",
  events: [],
  playerId: "P1",
  prompt: prompt({
    candidates: [
      {
        action: "PLAY_CARD",
        enabled: true,
        label: "打出手牌",
        reason: "可提交",
        sources: [{ id: "unit-1", label: "来源单位", objectIds: ["unit-1"] }]
      }
    ],
    serverFlow: serverFlowFixture,
    title: "优先行动",
    type: "STACK_PRIORITY"
  }),
  snapshot: snapshot(),
  submissionGate: connectedGate
});
assert.equal(serverFlowActionBridgePlan.state, "selecting");
assert.deepEqual(serverFlowActionBridgePlan.relatedActionRows.map((row) => ({
  actionRoleLabels: row.actionRoleLabels,
  candidateActionLabels: row.candidateActionLabels,
  disabledCandidateCount: row.disabledCandidateCount,
  enabledCandidateCount: row.enabledCandidateCount,
  objectId: row.objectId,
  serverRoleLabel: row.serverRoleLabel,
  state: row.state,
  stateLabel: row.stateLabel
})), [{
  actionRoleLabels: ["来源"],
  candidateActionLabels: ["PLAY_CARD"],
  disabledCandidateCount: 0,
  enabledCandidateCount: 1,
  objectId: "unit-1",
  serverRoleLabel: "结算来源",
  state: "ready",
  stateLabel: "可进入候选"
}]);
assert.equal(serverFlowActionBridgePlan.relatedActionRows[0].nextStepLabel, "可作为 来源 进入 1 个候选。");

const serverFlowActionBridgeServerSummaryPlan = buildWireServerFlowPlan({
  connectionStatus: "connected",
  events: [],
  playerId: "P1",
  prompt: prompt({
    serverFlow: {
      ...serverFlowFixture,
      relatedObjects: [{
        candidateActions: ["PLAY_CARD", "TAP_RUNE"],
        candidateBoundary: "服务端对象上下文边界",
        candidateRoles: ["目标"],
        candidateSteps: [
          { choiceCount: 1, index: 0, label: "来源", objectChoiceCount: 0, required: true, role: "source" },
          { choiceCount: 2, index: 1, label: "目标", objectChoiceCount: 1, required: false, role: "target" }
        ],
        candidateSource: "server-action-prompt",
        disabledCandidateCount: 1,
        enabledCandidateCount: 2,
        objectId: "unit-1",
        role: "结算来源"
      }]
    },
    title: "优先行动",
    type: "STACK_PRIORITY"
  }),
  snapshot: snapshot(),
  submissionGate: connectedGate
});
assert.deepEqual(serverFlowActionBridgeServerSummaryPlan.relatedActionRows.map((row) => ({
  actionRoleLabels: row.actionRoleLabels,
  candidateActionLabels: row.candidateActionLabels,
  disabledCandidateCount: row.disabledCandidateCount,
  enabledCandidateCount: row.enabledCandidateCount,
  objectId: row.objectId,
  serverRoleLabel: row.serverRoleLabel,
  state: row.state,
  stateLabel: row.stateLabel,
  stepSummary: row.stepSummary
})), [{
  actionRoleLabels: ["目标"],
  candidateActionLabels: ["PLAY_CARD", "TAP_RUNE"],
  disabledCandidateCount: 1,
  enabledCandidateCount: 2,
  objectId: "unit-1",
  serverRoleLabel: "结算来源",
  state: "ready",
  stateLabel: "可进入候选",
  stepSummary: "来源* 0/1 / 目标 1/2"
}]);
assert.equal(serverFlowActionBridgeServerSummaryPlan.relatedActionRows[0].nextStepLabel, "可作为 目标 进入 2 个候选。");
assert.deepEqual(serverFlowActionBridgeServerSummaryPlan.detail?.refs[0].candidateActions, ["PLAY_CARD", "TAP_RUNE"]);
assert.equal(serverFlowActionBridgeServerSummaryPlan.detail?.refs[0].candidateSource, "server-action-prompt");
assert.equal(serverFlowActionBridgeServerSummaryPlan.detail?.refs[0].candidateStepSummary, "来源* 0/1 / 目标 1/2");

console.log("Wire server flow plan check passed.");

function prompt({
  actionable = true,
  candidates,
  serverFlow,
  title = "主行动",
  type = "MAIN_ACTION"
} = {}) {
  return {
    actionable,
    actions: actionable ? ["END_TURN"] : [],
    candidates: candidates ?? (actionable
      ? [
          {
            action: "END_TURN",
            enabled: true,
            label: "结束回合",
            reason: "可结束回合"
          }
        ]
      : []),
    playerId: "P1",
    promptId: "prompt-1",
    reason: actionable ? "可行动" : "等待服务端窗口",
    serverFlow,
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

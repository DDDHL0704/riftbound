import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/wireRuleQueuePlan.ts");
const source = readFileSync(sourcePath, "utf8");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };
const helperModules = {
  "./collections": {
    asArray(value) {
      return Array.isArray(value) ? value : [];
    },
    asRecord(value) {
      return value && typeof value === "object" && !Array.isArray(value) ? value : {};
    },
    asString(value, fallback = "未提供") {
      return typeof value === "string" && value.trim().length > 0 ? value : fallback;
    }
  },
  "./formatters": {
    matchPhaseLabel(value) {
      return {
        MAIN: "主阶段",
        MULLIGAN: "起手调整",
        ROOM: "房间阶段",
        TURN_END: "回合结束",
        TURN_START: "回合开始"
      }[value] ?? (value ? "服务端阶段" : "等待开局");
    },
    timingStateLabel(value) {
      return {
        MULLIGAN: "起手调整",
        NEUTRAL_CLOSED: "普通闭环",
        NEUTRAL_OPEN: "普通开环",
        ROOM: "房间窗口",
        SPELL_DUEL_CLOSED: "法术对决闭环",
        SPELL_DUEL_OPEN: "法术对决开环"
      }[value] ?? (value ? "服务端窗口" : "未知窗口");
    }
  },
  "./redaction": {
    redactInternalText(value) {
      return value;
    }
  },
  "./snapshotObjectIndex": {
    buildCardObjectIndex(snapshot) {
      const indexed = {};
      for (const player of Object.values(snapshot?.players ?? {})) {
        for (const [objectId, object] of Object.entries(player.objects ?? {})) {
          indexed[object.objectId ?? objectId] = { ...object, objectId: object.objectId ?? objectId };
        }
      }

      return indexed;
    }
  }
};

function requireShim(id) {
  const module = helperModules[id];
  if (!module) {
    throw new Error(`Unexpected check helper import: ${id}`);
  }

  return module;
}

new Function("exports", "module", "require", output)(moduleShim.exports, moduleShim, requireShim);

const { buildWireRuleQueuePlan } = moduleShim.exports;

const baseSnapshot = {
  activePlayerId: "P1",
  lanes: {},
  players: {},
  stack: [],
  tick: 8,
  timing: {
    phase: "MAIN",
    roomStatus: "IN_PROGRESS",
    turnWindow: { actingPlayerId: "P1", state: "NEUTRAL_OPEN" }
  },
  turnNumber: 3,
  turnState: "MAIN"
};

const taskBlocked = buildWireRuleQueuePlan({
  playerId: "P1",
  prompt: { actionable: false, actions: ["WAIT"], candidates: [], playerId: "P1", reason: "等待任务" },
  snapshot: {
    ...baseSnapshot,
    timing: {
      ...baseSnapshot.timing,
      pendingTaskQueue: {
        activeTaskId: "task-1",
        isBlocking: true,
        phase: "BATTLEFIELD_TASKS",
        tasks: [
          {
            actingPlayerId: "P2",
            battlefieldObjectId: "battlefield-a",
            kind: "BATTLEFIELD_CONTESTED",
            participantObjectIds: ["unit-a", "unit-b"],
            reason: "BATTLEFIELD_CONTESTED",
            status: "OPEN",
            taskId: "task-1"
          }
        ]
      }
    }
  }
});

assert.equal(taskBlocked.state, "task-blocked");
assert.equal(taskBlocked.activeLaneKey, "task");
assert.equal(taskBlocked.header.statusLabel, "规则阻塞");
assert.equal(taskBlocked.header.statusTone, "warn");
assert.equal(taskBlocked.header.subtitle, "tick 8 / prompt 无");
assert.equal(taskBlocked.focus.laneKey, "task");
assert.equal(taskBlocked.focus.laneLabel, "规则任务");
assert.ok(taskBlocked.focus.reasonLabel.includes("阻塞普通行动"));
assert.ok(taskBlocked.focus.detail?.id.includes("rule:task:task-1"));
assert.equal(taskBlocked.inspector.activeLaneLabel, "规则任务");
assert.equal(taskBlocked.inspector.lanes.find((lane) => lane.key === "task")?.stateLabel, "阻塞");
assert.ok(taskBlocked.inspector.summary.includes("规则阻塞"));
assert.equal(taskBlocked.lanes.find((lane) => lane.key === "task")?.state, "blocked");
assert.ok(taskBlocked.nextStepLabel.includes("阻塞规则任务"));
assert.ok(taskBlocked.sequence[0].detailLabel.includes("战场控制检查"));
assert.equal(taskBlocked.metrics.find((metric) => metric.key === "task")?.value, "1 项");
assert.equal(taskBlocked.sections.find((section) => section.key === "task")?.items.length, 1);
assert.ok(taskBlocked.sections.find((section) => section.key === "task")?.notes.some((note) => note.includes("阻塞普通行动")));
assert.ok(taskBlocked.sections.find((section) => section.key === "task")?.items[0]?.detail.id.includes("rule:task:task-1"));

const stackResponse = buildWireRuleQueuePlan({
  playerId: "P1",
  snapshot: {
    ...baseSnapshot,
    stack: [
      {
        cardNo: "OGN-001/298",
        controllerId: "P2",
        effectKind: "SPELL",
        sourceObjectId: "spell-1",
        stackItemId: "stack-1",
        targetObjectIds: ["unit-1"]
      }
    ]
  }
});

assert.equal(stackResponse.state, "stack-response");
assert.equal(stackResponse.activeLaneKey, "stack");
assert.equal(stackResponse.header.statusLabel, "等待响应");
assert.equal(stackResponse.header.statusTone, "info");
assert.equal(stackResponse.focus.laneKey, "stack");
assert.equal(stackResponse.focus.laneLabel, "结算链");
assert.ok(stackResponse.focus.reasonLabel.includes("结算链顶部"));
assert.equal(stackResponse.focus.detail?.title, "结算链项目");
assert.equal(stackResponse.inspector.activeLaneLabel, "结算链");
assert.equal(stackResponse.inspector.sequence[0].laneLabel, "结算链");
assert.equal(stackResponse.inspector.sequence[0].objectCount, 2);
assert.deepEqual(
  stackResponse.sequence[0].refs.map((ref) => `${ref.role}:${ref.id}`),
  ["来源:spell-1", "目标:unit-1"]
);
assert.deepEqual(
  stackResponse.inspector.sequence[0].refs.map((ref) => `${ref.role}:${ref.id}`),
  ["来源:spell-1", "目标:unit-1"]
);
assert.equal(stackResponse.lanes.find((lane) => lane.key === "stack")?.state, "active");
assert.equal(stackResponse.sequence[0].lane, "stack");
assert.equal(stackResponse.sequence[0].objectCount, 2);
assert.equal(stackResponse.sections.find((section) => section.key === "stack")?.items[0]?.title, "项目 1");
assert.ok(stackResponse.sections.find((section) => section.key === "stack")?.items[0]?.refs.some((ref) => ref.role === "目标" && ref.id === "unit-1"));
assert.equal(
  stackResponse.sections.find((section) => section.key === "stack")?.items[0]?.lines.find((line) => line.label === "顺序")?.value,
  "顶部；下一个结算"
);
assert.equal(
  stackResponse.sections.find((section) => section.key === "stack")?.items[0]?.lines.find((line) => line.label === "响应")?.value,
  "响应窗口由服务端 prompt 裁定"
);
assert.ok(
  stackResponse.sections.find((section) => section.key === "stack")?.items[0]?.detail.lines.some((line) =>
    line.label === "权威" && line.value.includes("前端不重算优先权"))
);
assert.ok(
  stackResponse.sections.find((section) => section.key === "stack")?.items[0]?.detail.lines.some((line) =>
    line.label === "边界" && line.value.includes("公开结算链项"))
);

const multiStackResponse = buildWireRuleQueuePlan({
  playerId: "P1",
  snapshot: {
    ...baseSnapshot,
    stack: [
      {
        controllerId: "P2",
        effectKind: "SPELL",
        sourceObjectId: "spell-top",
        stackItemId: "stack-top",
        targetObjectIds: []
      },
      {
        controllerId: "P1",
        effectKind: "ABILITY",
        sourceObjectId: "ability-bottom",
        stackItemId: "stack-bottom",
        targetObjectIds: ["unit-2"]
      }
    ]
  }
});

const bottomStackItem = multiStackResponse.sections.find((section) => section.key === "stack")?.items[1];
assert.equal(bottomStackItem?.title, "项目 1");
assert.equal(bottomStackItem?.lines.find((line) => line.label === "顺序")?.value, "等待上方 1 项");
assert.equal(bottomStackItem?.lines.find((line) => line.label === "响应")?.value, "先等待上方结算链项目");

const sameObjectMultiRole = buildWireRuleQueuePlan({
  playerId: "P1",
  snapshot: {
    ...baseSnapshot,
    stack: [
      {
        controllerId: "P1",
        effectKind: "ABILITY",
        sourceObjectId: "shared-object",
        stackItemId: "stack-shared",
        targetObjectIds: ["shared-object"]
      }
    ]
  }
});

assert.deepEqual(
  sameObjectMultiRole.sequence[0].refs.map((ref) => `${ref.role}:${ref.id}`),
  ["来源:shared-object", "目标:shared-object"],
  "sequence refs must preserve distinct roles for the same object"
);

const triggerPending = buildWireRuleQueuePlan({
  playerId: "P1",
  snapshot: {
    ...baseSnapshot,
    timing: {
      ...baseSnapshot.timing,
      triggerQueue: [
        {
          controllerId: "P1",
          effectKind: "TRIGGER",
          sourceObjectId: "jinx-1",
          triggeredByEventKind: "CARD_DISCARDED",
          triggerId: "trigger-1"
        }
      ]
    }
  }
});

assert.equal(triggerPending.state, "trigger-pending");
assert.equal(triggerPending.activeLaneKey, "trigger");
assert.equal(triggerPending.header.statusLabel, "触发待处理");
assert.equal(triggerPending.header.statusTone, "info");
assert.equal(triggerPending.focus.laneKey, "trigger");
assert.equal(triggerPending.focus.detail?.title, "触发 1");
assert.ok(triggerPending.lanes.find((lane) => lane.key === "trigger")?.headline.includes("触发"));
assert.equal(triggerPending.sequence[0].stateLabel, "P1");
assert.deepEqual(triggerPending.sequence[0].refs.map((ref) => `${ref.role}:${ref.id}`), ["来源:jinx-1"]);
assert.ok(triggerPending.sections.find((section) => section.key === "trigger")?.items[0]?.detail.lines.some((line) => line.label === "来源事件"));

const resolutionHistory = buildWireRuleQueuePlan({
  playerId: "P1",
  snapshot: {
    ...baseSnapshot,
    timing: {
      ...baseSnapshot.timing,
      battleResolutions: [
        {
          battlefieldId: "battlefield-b",
          destroyedObjectIds: ["unit-a"],
          kind: "NO_RESULT",
          resolutionId: "battle-resolution-1",
          tick: 9
        }
      ],
      battlefieldResolutions: [
        {
          battlefieldObjectId: "battlefield-a",
          kind: "HELD",
          participantObjectIds: ["unit-b"],
          playerId: "P2",
          resolutionId: "battlefield-resolution-1",
          tick: 8
        }
      ]
    }
  }
});

assert.equal(resolutionHistory.state, "resolution-history");
assert.equal(resolutionHistory.activeLaneKey, "resolution");
assert.equal(resolutionHistory.header.statusLabel, "近期规则事件");
assert.equal(resolutionHistory.focus.laneKey, "resolution");
assert.equal(resolutionHistory.focus.detail?.title, "据守");
assert.equal(resolutionHistory.sequence.length, 2);
assert.equal(resolutionHistory.sequence[0].tickLabel, "tick 8");
assert.deepEqual(
  resolutionHistory.sequence[0].refs.map((ref) => `${ref.role}:${ref.id}`),
  ["战场:battlefield-a", "参与:unit-b"]
);
assert.equal(resolutionHistory.sequence[1].detailLabel, "战斗无结果");
assert.deepEqual(
  resolutionHistory.sequence[1].refs.map((ref) => `${ref.role}:${ref.id}`),
  ["战场:battlefield-b", "被摧毁:unit-a"]
);
assert.equal(resolutionHistory.sections.find((section) => section.key === "resolution")?.items.length, 2);
assert.ok(resolutionHistory.sections.find((section) => section.key === "resolution")?.items.some((item) => item.key.startsWith("battle-resolution:")));

const idle = buildWireRuleQueuePlan({ playerId: "P1", snapshot: baseSnapshot });
assert.equal(idle.state, "idle");
assert.equal(idle.activeLaneKey, "none");
assert.equal(idle.header.statusLabel, "空闲");
assert.equal(idle.header.statusTone, "neutral");
assert.equal(idle.focus.laneKey, "none");
assert.equal(idle.focus.detail, undefined);
assert.equal(idle.focus.laneLabel, "无活动通道");
assert.equal(idle.inspector.activeLaneLabel, "无活动通道");
assert.equal(idle.sequence.length, 0);
assert.ok(idle.lanes.every((lane) => lane.state === "empty"));
assert.ok(idle.sections.every((section) => section.items.length === 0));

console.log("Wire rule queue plan check passed.");

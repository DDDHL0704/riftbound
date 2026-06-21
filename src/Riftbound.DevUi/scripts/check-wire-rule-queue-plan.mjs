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
    },
    asNumber(value, fallback = 0) {
      return typeof value === "number" && Number.isFinite(value) ? value : fallback;
    }
  },
  "./eventLogPlan": {
    eventDescriptionLabel(event) {
      return event.description || helperModules["./eventLogPlan"].eventKindLabel(event.kind);
    },
    eventKindLabel(kind) {
      return {
        BATTLEFIELD_CONQUERED: "征服战场",
        CARD_DRAWN: "抽牌",
        STACK_ITEM_RESOLVED: "结算链项目结算",
        TRIGGER_QUEUED: "触发排队"
      }[kind] ?? "服务端事件";
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
  "./gameEventObjectRefs": {
    gameEventObjectRefPlan(event) {
      const serverRefs = Array.isArray(event.objectRefs) ? event.objectRefs.filter((ref) => ref?.objectId) : [];
      if (serverRefs.length > 0) {
        return { refs: serverRefs, source: "server" };
      }

      const refs = [];
      const payload = event.payload ?? {};
      if (typeof payload.battlefieldObjectId === "string") {
        refs.push({ objectId: payload.battlefieldObjectId, role: "战场" });
      }
      if (Array.isArray(payload.participantObjectIds)) {
        refs.push(...payload.participantObjectIds.map((objectId) => ({ objectId, role: "参与" })));
      }
      return refs.length > 0 ? { refs, source: "payload" } : { refs: [], source: "none" };
    },
    gameEventObjectRefSourceLabel(source) {
      return {
        none: "无对象引用",
        payload: "事件字段",
        server: "服务端摘要"
      }[source];
    }
  },
  "./promptCandidateCounts": {
    promptCandidateCounts(prompt) {
      const candidates = prompt?.candidates ?? [];
      const enabledFallback = candidates.filter((candidate) => candidate.enabled).length;
      const total = finiteCount(prompt?.serverFlow?.candidateCount) ?? candidates.length;
      const enabled = finiteCount(prompt?.serverFlow?.enabledCandidateCount) ?? enabledFallback;
      const disabled = finiteCount(prompt?.serverFlow?.disabledCandidateCount)
        ?? (prompt?.serverFlow ? Math.max(0, total - enabled) : candidates.filter((candidate) => !candidate.enabled).length);
      return {
        candidateCount: total,
        disabledCandidateCount: disabled,
        enabledCandidateCount: enabled,
        source: prompt?.serverFlow ? "server-flow" : "candidates"
      };
    }
  },
  "./promptInteraction": {
    buildPromptInteractionModel(prompt) {
      const objectById = new Map();
      const candidates = (prompt?.candidates ?? []).map((candidate) => {
        const choices = promptCandidateChoicesForCheck(candidate);
        for (const choice of choices) {
          for (const objectId of helperModules["./promptInteraction"].promptChoiceSummaryObjectIds(choice)) {
            const existing = objectById.get(objectId) ?? {
              choices: [],
              disabledCandidateCount: 0,
              enabledCandidateCount: 0,
              objectId,
              state: "disabled"
            };
            existing.choices.push(choice);
            if (candidate.enabled) {
              existing.enabledCandidateCount += 1;
              existing.state = "enabled";
            } else {
              existing.disabledCandidateCount += 1;
            }
            objectById.set(objectId, existing);
          }
        }

        return {
          action: candidate.action,
          enabled: Boolean(candidate.enabled),
          label: candidate.label || candidate.action,
          presentation: {
            category: normalizedPresentationForCheck(candidate.presentation?.category, "custom"),
            intent: normalizedPresentationForCheck(candidate.presentation?.intent, String(candidate.action ?? "action").toLowerCase().replaceAll("_", "-")),
            priority: typeof candidate.presentation?.priority === "number" && Number.isFinite(candidate.presentation.priority)
              ? candidate.presentation.priority
              : 700,
            uiHint: normalizedPresentationForCheck(candidate.presentation?.uiHint, "card-action")
          },
          reason: candidate.reason || (candidate.enabled ? "可提交" : "暂不可提交"),
          choices,
          steps: []
        };
      });
      const enabledObjectIds = new Set();
      const disabledObjectIds = new Set();
      for (const [objectId, summary] of objectById) {
        if (summary.enabledCandidateCount > 0) {
          enabledObjectIds.add(objectId);
        } else {
          disabledObjectIds.add(objectId);
        }
      }

      return { candidates, disabledObjectIds, enabledObjectIds, objectById };
    },
    promptChoiceRoleLabel(role) {
      return {
        destination: "位置",
        mode: "模式",
        optionalCost: "费用",
        source: "来源",
        target: "目标"
      }[role] ?? role;
    },
    promptChoiceSummaryObjectIds(choice) {
      return choice.objectIds?.length ? choice.objectIds : promptChoiceObjectIdsForCheck(choice.id);
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

function finiteCount(value) {
  return typeof value === "number" && Number.isFinite(value) ? Math.max(0, Math.floor(value)) : undefined;
}

const promptChoiceGroupsForCheck = [
  { key: "sources", role: "source" },
  { key: "targets", role: "target" },
  { key: "destinations", role: "destination" },
  { key: "modes", role: "mode" },
  { key: "optionalCosts", role: "optionalCost" }
];

function promptCandidateChoicesForCheck(candidate) {
  return [
    ...(candidate.selectionSteps ?? []).flatMap((step) =>
      (step.choices ?? []).map((choice) => ({
        id: choice.id,
        label: choice.label || choice.id,
        objectIds: normalizedObjectIdsForCheck(choice.objectIds),
        role: step.role
      }))),
    ...promptChoiceGroupsForCheck.flatMap(({ key, role }) =>
      (candidate[key] ?? []).map((choice) => ({
        id: choice.id,
        label: choice.label || choice.id,
        objectIds: normalizedObjectIdsForCheck(choice.objectIds),
        role
      })))
  ].filter((choice) => choice.id);
}

function promptChoiceObjectIdsForCheck(choiceId) {
  const cleaned = String(choiceId ?? "").trim();
  if (!cleaned) {
    return [];
  }

  const ids = new Set([cleaned]);
  const lastSegment = cleaned.split(":").filter(Boolean).at(-1);
  if (lastSegment && lastSegment !== cleaned) {
    ids.add(lastSegment);
  }
  return [...ids];
}

function normalizedObjectIdsForCheck(objectIds) {
  if (!Array.isArray(objectIds)) {
    return undefined;
  }

  const normalized = objectIds.map((objectId) => String(objectId).trim()).filter(Boolean);
  return normalized.length > 0 ? [...new Set(normalized)] : undefined;
}

function normalizedPresentationForCheck(value, fallback) {
  const normalized = typeof value === "string" ? value.trim() : "";
  return normalized || fallback;
}

function requireShim(id) {
  const module = helperModules[id];
  if (!module) {
    throw new Error(`Unexpected check helper import: ${id}`);
  }

  return module;
}

new Function("exports", "module", "require", output)(moduleShim.exports, moduleShim, requireShim);

const { buildWireRuleQueuePlan } = moduleShim.exports;

function coverageRow(plan, key) {
  const row = plan.coverage.find((item) => item.key === key);
  assert.ok(row, `Missing coverage row: ${key}`);
  return row;
}

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
assert.equal(taskBlocked.responsibility.stateLabel, "规则阻塞");
assert.equal(taskBlocked.responsibility.activeCount, 1);
assert.ok(taskBlocked.responsibility.summary.includes("阻塞规则任务"));
assert.equal(taskBlocked.responsibility.items[0].state, "blocked");
assert.equal(taskBlocked.responsibility.items[0].stateLabel, "阻塞");
assert.equal(taskBlocked.responsibility.items[0].actionLabel, "等待规则任务");
assert.ok(taskBlocked.responsibility.items[0].reason.includes("阻塞普通行动"));
assert.equal(taskBlocked.inspector.activeLaneLabel, "规则任务");
assert.equal(taskBlocked.inspector.lanes.find((lane) => lane.key === "task")?.stateLabel, "阻塞");
assert.ok(taskBlocked.inspector.summary.includes("规则阻塞"));
assert.equal(taskBlocked.lanes.find((lane) => lane.key === "task")?.state, "blocked");
assert.ok(taskBlocked.nextStepLabel.includes("阻塞规则任务"));
assert.ok(taskBlocked.sequence[0].detailLabel.includes("战场控制检查"));
assert.equal(taskBlocked.metrics.find((metric) => metric.key === "task")?.value, "1 项");
assert.equal(taskBlocked.metrics.find((metric) => metric.key === "coverage")?.value, "2 类");
assert.equal(coverageRow(taskBlocked, "battle").state, "live");
assert.equal(coverageRow(taskBlocked, "battle").liveCount, 1);
assert.equal(taskBlocked.sections.find((section) => section.key === "task")?.items.length, 1);
assert.ok(taskBlocked.sections.find((section) => section.key === "task")?.notes.some((note) => note.includes("阻塞普通行动")));
assert.ok(taskBlocked.sections.find((section) => section.key === "task")?.items[0]?.detail.id.includes("rule:task:task-1"));

const stackPrioritySnapshot = {
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
};

const stackResponse = buildWireRuleQueuePlan({
  playerId: "P1",
  snapshot: stackPrioritySnapshot
});

assert.equal(stackResponse.state, "stack-response");
assert.equal(stackResponse.activeLaneKey, "stack");
assert.equal(stackResponse.header.statusLabel, "等待响应");
assert.equal(stackResponse.header.statusTone, "info");
assert.equal(stackResponse.focus.laneKey, "stack");
assert.equal(stackResponse.focus.laneLabel, "结算链");
assert.ok(stackResponse.focus.reasonLabel.includes("结算链顶部"));
assert.equal(stackResponse.focus.detail?.title, "结算链项目");
assert.deepEqual(
  stackResponse.focus.actionRows.map((row) => `${row.objectId}:${row.serverRoleLabel}:${row.state}:${row.stateLabel}:${row.semanticSummary}`),
  [
    "spell-1:来源:referenced:仅规则引用:无候选语义",
    "unit-1:目标:referenced:仅规则引用:无候选语义"
  ]
);
assert.equal(stackResponse.responsibility.stateLabel, "响应窗口");
assert.equal(stackResponse.responsibility.activeCount, 1);
assert.ok(stackResponse.responsibility.summary.includes("当前响应入口"));
assert.equal(stackResponse.responsibility.items[0].state, "respond");
assert.equal(stackResponse.responsibility.items[0].stateLabel, "可响应");
assert.equal(stackResponse.responsibility.items[0].actionLabel, "响应结算链");
assert.equal(stackResponse.responsibility.items[0].actorLabel, "控制者 P2");
assert.ok(stackResponse.responsibility.items[0].reason.includes("服务端 prompt"));
assert.equal(stackResponse.responsibility.submitReadyCount, 0);
assert.equal(stackResponse.responsibility.items[0].submit.state, "waiting-prompt");
assert.equal(stackResponse.responsibility.items[0].submit.canSubmit, false);
assert.equal(stackResponse.responsibility.items[0].submit.semanticSummary, "无动作语义");
assert.deepEqual(stackResponse.responsibility.items[0].submit.semanticRows, []);
assert.ok(stackResponse.responsibility.items[0].submit.reason.includes("等待服务端 prompt"));
assert.equal(stackResponse.inspector.activeLaneLabel, "结算链");
assert.equal(stackResponse.inspector.sequence[0].laneLabel, "结算链");
assert.equal(stackResponse.inspector.sequence[0].objectCount, 2);
assert.equal(coverageRow(stackResponse, "stack").state, "live");
assert.equal(coverageRow(stackResponse, "stack").liveCount, 1);
assert.equal(coverageRow(stackResponse, "stack").eventCount, 0);
assert.equal(stackResponse.metrics.find((metric) => metric.key === "coverage")?.value, "2 类");
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

const stackReadyPrompt = buildWireRuleQueuePlan({
  playerId: "P1",
  prompt: {
    actionable: true,
    actions: ["RESPOND", "WAIT"],
    candidates: [
      {
        action: "PLAY_CARD",
        enabled: true,
        label: "打出反应",
        presentation: { category: "play", intent: "play-card", priority: 100, uiHint: "card-action" },
        reason: "服务端候选可用",
        sources: [{ id: "spell-1", label: "反应牌", objectIds: ["spell-1"] }],
        targets: [{ id: "unit-1", label: "目标单位", objectIds: ["unit-1"] }]
      },
      {
        action: "TAP_RUNE",
        enabled: true,
        label: "横置符文",
        presentation: { category: "resource", intent: "tap-rune", priority: 180, uiHint: "resource" },
        reason: "服务端候选可用"
      },
      {
        action: "RESPOND",
        enabled: true,
        label: "响应结算链",
        presentation: { category: "tempo", intent: "respond", priority: 500, uiHint: "secondary" },
        reason: "服务端候选可用"
      },
      {
        action: "WAIT",
        enabled: false,
        label: "不响应",
        presentation: { category: "tempo", intent: "pass-priority", priority: 510, uiHint: "secondary" },
        reason: "等待确认"
      }
    ],
    playerId: "P1",
    reason: "P1 可以响应",
    serverFlow: { candidateCount: 9, disabledCandidateCount: 5, enabledCandidateCount: 4, promptType: "STACK_PRIORITY" },
    view: { message: "响应窗口", title: "响应", type: "STACK_PRIORITY" }
  },
  snapshot: stackPrioritySnapshot
});

assert.equal(stackReadyPrompt.responsibility.submitReadyCount, 1);
assert.equal(stackReadyPrompt.responsibility.items[0].submit.state, "ready");
assert.equal(stackReadyPrompt.responsibility.items[0].submit.canSubmit, true);
assert.equal(stackReadyPrompt.responsibility.items[0].submit.promptType, "STACK_PRIORITY");
assert.equal(stackReadyPrompt.responsibility.items[0].submit.candidateCount, 9);
assert.equal(stackReadyPrompt.responsibility.items[0].submit.enabledCandidateCount, 4);
assert.equal(stackReadyPrompt.responsibility.items[0].submit.semanticSummary, "play/play-card / resource/tap-rune +1");
assert.deepEqual(
  stackReadyPrompt.focus.actionRows.map((row) => `${row.objectId}:${row.serverRoleLabel}:${row.state}:${row.actionRoleLabels.join("/")}:${row.semanticSummary}:${row.enabledCandidateCount}/${row.candidateCount}`),
  [
    "spell-1:来源:ready:来源:play/play-card:1/1",
    "unit-1:目标:ready:目标:play/play-card:1/1"
  ]
);
assert.deepEqual(stackReadyPrompt.responsibility.items[0].submit.semanticRows.map((row) => `${row.category}:${row.intent}:${row.enabledCount}/${row.count}:${row.priority}:${row.uiHint}`), [
  "play:play-card:1/1:100:card-action",
  "resource:tap-rune:1/1:180:resource",
  "tempo:respond:1/1:500:secondary",
  "tempo:pass-priority:0/1:510:secondary"
]);
assert.ok(stackReadyPrompt.responsibility.items[0].submit.reason.includes("4/9"));
assert.ok(stackReadyPrompt.responsibility.summary.includes("1 个可提交入口"));

const stackOpponentPrompt = buildWireRuleQueuePlan({
  playerId: "P1",
  prompt: {
    actionable: true,
    actions: ["RESPOND"],
    candidates: [{ action: "RESPOND", enabled: true, label: "响应结算链", reason: "服务端候选可用" }],
    playerId: "P2",
    reason: "P2 可以响应",
    view: { message: "响应窗口", title: "响应", type: "STACK_PRIORITY" }
  },
  snapshot: stackPrioritySnapshot
});

assert.equal(stackOpponentPrompt.responsibility.submitReadyCount, 0);
assert.equal(stackOpponentPrompt.responsibility.items[0].submit.state, "wrong-player");
assert.equal(stackOpponentPrompt.responsibility.items[0].submit.canSubmit, false);
assert.equal(stackOpponentPrompt.responsibility.items[0].submit.semanticSummary, "custom/respond");
assert.ok(stackOpponentPrompt.responsibility.items[0].submit.reason.includes("P2"));

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
assert.deepEqual(multiStackResponse.responsibility.items.map((item) => `${item.label}:${item.state}:${item.actionLabel}`), [
  "结算链 2:respond:响应结算链",
  "结算链 1:waiting:等待前序"
]);

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
assert.equal(triggerPending.responsibility.stateLabel, "触发观察");
assert.equal(triggerPending.responsibility.activeCount, 1);
assert.equal(triggerPending.responsibility.items[0].state, "watch");
assert.equal(triggerPending.responsibility.items[0].actionLabel, "查看触发");
assert.ok(triggerPending.responsibility.items[0].reason.includes("触发队列"));
assert.ok(triggerPending.lanes.find((lane) => lane.key === "trigger")?.headline.includes("触发"));
assert.equal(triggerPending.sequence[0].stateLabel, "P1");
assert.deepEqual(triggerPending.sequence[0].refs.map((ref) => `${ref.role}:${ref.id}`), ["来源:jinx-1"]);
assert.equal(coverageRow(triggerPending, "trigger").state, "live");
assert.equal(coverageRow(triggerPending, "trigger").liveCount, 1);
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
assert.equal(resolutionHistory.responsibility.stateLabel, "可回看");
assert.equal(resolutionHistory.responsibility.activeCount, 0);
assert.deepEqual(resolutionHistory.responsibility.items.map((item) => item.state), ["history", "history"]);
assert.ok(resolutionHistory.responsibility.items.every((item) => item.actionLabel === "查看事件"));
assert.equal(resolutionHistory.sequence.length, 2);
assert.equal(resolutionHistory.sequence[0].tickLabel, "tick 8");
assert.equal(coverageRow(resolutionHistory, "battle").state, "live");
assert.equal(coverageRow(resolutionHistory, "battle").liveCount, 2);
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

const eventResolutionHistory = buildWireRuleQueuePlan({
  events: [
    {
      description: "玩家抽一张牌",
      kind: "CARD_DRAWN",
      objectRefs: [{ cardNo: "OGN-001/298", objectId: "hand-card", role: "卡牌" }],
      payload: {}
    },
    {
      description: "P1 征服左战场",
      kind: "BATTLEFIELD_CONQUERED",
      objectRefs: [
        { objectId: "battlefield-a", role: "战场" },
        { objectId: "unit-a", role: "参与" }
      ],
      payload: {}
    },
    {
      description: "隐藏来源触发排队",
      kind: "TRIGGER_QUEUED",
      objectRefs: [{ isHidden: true, objectId: "secret-trigger", role: "来源" }],
      payload: {}
    }
  ],
  playerId: "P1",
  snapshot: {
    ...baseSnapshot,
    players: {
      P1: {
        objects: {
          "battlefield-a": { cardNo: "BF-001", objectId: "battlefield-a" },
          "unit-a": { cardNo: "UNL-001/219", objectId: "unit-a" }
        }
      }
    }
  }
});

assert.equal(eventResolutionHistory.state, "resolution-history");
assert.equal(eventResolutionHistory.activeLaneKey, "resolution");
assert.equal(eventResolutionHistory.lanes.find((lane) => lane.key === "resolution")?.count, 2);
assert.equal(eventResolutionHistory.lanes.find((lane) => lane.key === "resolution")?.headline, "触发排队");
assert.equal(eventResolutionHistory.metrics.find((metric) => metric.key === "resolution")?.value, "2 项");
assert.equal(eventResolutionHistory.metrics.find((metric) => metric.key === "coverage")?.value, "3 类");
assert.equal(coverageRow(eventResolutionHistory, "trigger").state, "history");
assert.equal(coverageRow(eventResolutionHistory, "trigger").eventCount, 1);
assert.equal(coverageRow(eventResolutionHistory, "trigger").objectRefCount, 0);
assert.equal(coverageRow(eventResolutionHistory, "battle").state, "history");
assert.equal(coverageRow(eventResolutionHistory, "battle").eventCount, 1);
assert.equal(coverageRow(eventResolutionHistory, "battle").objectRefCount, 2);
assert.equal(coverageRow(eventResolutionHistory, "window").state, "live");
assert.equal(eventResolutionHistory.sequence.length, 2);
assert.equal(eventResolutionHistory.responsibility.items.length, 2);
assert.equal(eventResolutionHistory.responsibility.items[0].state, "history");
assert.equal(eventResolutionHistory.responsibility.items[0].actorLabel, "隐藏来源触发排队");
assert.equal(eventResolutionHistory.sequence[0].detailLabel, "触发排队");
assert.deepEqual(eventResolutionHistory.sequence[0].refs.map((ref) => `${ref.role}:${ref.id}:${ref.visibility ?? "auto"}`), ["来源:HIDDEN:hidden"]);
assert.equal(eventResolutionHistory.sequence[1].detailLabel, "征服战场");
assert.deepEqual(
  eventResolutionHistory.sequence[1].refs.map((ref) => `${ref.role}:${ref.id}`),
  ["战场:battlefield-a", "参与:unit-a"]
);
const eventResolutionSection = eventResolutionHistory.sections.find((section) => section.key === "resolution");
assert.equal(eventResolutionSection?.items.length, 2);
assert.equal(eventResolutionSection?.items[0]?.title, "触发排队");
assert.equal(eventResolutionSection?.items[0]?.detail.lines.find((line) => line.label === "引用")?.value, "服务端摘要");
assert.equal(eventResolutionSection?.items[0]?.detail.lines.find((line) => line.label === "对象可见性")?.value, "可见 0 / 隐藏 1 / 缺失 0");
assert.equal(eventResolutionSection?.items[1]?.detail.lines.find((line) => line.label === "对象可见性")?.value, "可见 2 / 隐藏 0 / 缺失 0");

const mixedCoverage = buildWireRuleQueuePlan({
  events: [
    {
      description: "结算链顶部项目结算",
      kind: "STACK_ITEM_RESOLVED",
      objectRefs: [{ objectId: "spell-1", role: "来源" }],
      payload: {}
    }
  ],
  playerId: "P1",
  snapshot: {
    ...baseSnapshot,
    stack: [
      {
        controllerId: "P1",
        effectKind: "SPELL",
        sourceObjectId: "spell-2",
        stackItemId: "stack-live",
        targetObjectIds: []
      }
    ]
  }
});

assert.equal(coverageRow(mixedCoverage, "stack").state, "mixed");
assert.equal(coverageRow(mixedCoverage, "stack").liveCount, 1);
assert.equal(coverageRow(mixedCoverage, "stack").eventCount, 1);
assert.equal(coverageRow(mixedCoverage, "stack").objectRefCount, 1);
assert.equal(mixedCoverage.metrics.find((metric) => metric.key === "coverage")?.value, "2 类");

const serverCoverage = buildWireRuleQueuePlan({
  events: [
    {
      description: "费用支付完成",
      kind: "COST_PAID",
      objectRefs: [{ objectId: "rune-1", role: "费用" }],
      payload: {}
    }
  ],
  playerId: "P1",
  snapshot: {
    ...baseSnapshot,
    timing: {
      ...baseSnapshot.timing,
      pendingPayment: {
        paymentId: "pay-1",
        paymentWindow: "PLAY_CARD",
        playerId: "P1"
      },
      ruleQueueCoverage: [
        { evidenceKeys: ["pendingPayment"], key: "payment", liveCount: 1 },
        { evidenceKeys: ["battle"], key: "battle", liveCount: 1 },
        { evidenceKeys: ["ignored"], key: "unknown", liveCount: 99 }
      ]
    }
  }
});

assert.equal(coverageRow(serverCoverage, "payment").state, "mixed");
assert.equal(coverageRow(serverCoverage, "payment").liveCount, 1);
assert.equal(coverageRow(serverCoverage, "payment").eventCount, 1);
assert.equal(coverageRow(serverCoverage, "payment").objectRefCount, 1);
assert.ok(coverageRow(serverCoverage, "payment").hint.includes("pendingPayment"));
assert.equal(coverageRow(serverCoverage, "battle").state, "live");
assert.equal(coverageRow(serverCoverage, "battle").liveCount, 1);
assert.equal(serverCoverage.metrics.find((metric) => metric.key === "coverage")?.value, "3 类");

const idle = buildWireRuleQueuePlan({ playerId: "P1", snapshot: baseSnapshot });
assert.equal(idle.state, "idle");
assert.equal(idle.activeLaneKey, "none");
assert.equal(idle.header.statusLabel, "空闲");
assert.equal(idle.header.statusTone, "neutral");
assert.equal(idle.focus.laneKey, "none");
assert.equal(idle.focus.detail, undefined);
assert.equal(idle.focus.laneLabel, "无活动通道");
assert.equal(idle.responsibility.stateLabel, "无待响应");
assert.equal(idle.responsibility.activeCount, 0);
assert.equal(idle.responsibility.items.length, 0);
assert.equal(idle.responsibility.summary, "当前没有服务端结算链、规则任务、触发或近期规则事件。");
assert.equal(idle.inspector.activeLaneLabel, "无活动通道");
assert.equal(idle.sequence.length, 0);
assert.ok(idle.lanes.every((lane) => lane.state === "empty"));
assert.ok(idle.sections.every((section) => section.items.length === 0));
assert.equal(coverageRow(idle, "window").state, "live");
assert.equal(idle.metrics.find((metric) => metric.key === "coverage")?.value, "1 类");

console.log("Wire rule queue plan check passed.");

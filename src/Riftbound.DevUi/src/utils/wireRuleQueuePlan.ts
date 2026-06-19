import type { ActionPromptDto, SnapshotDto } from "../types/protocol";
import { asArray, asRecord, asString } from "./collections";
import { matchPhaseLabel, timingStateLabel } from "./formatters";
import { redactInternalText } from "./redaction";
import { buildCardObjectIndex, type SnapshotObjectIndex } from "./snapshotObjectIndex";

export type WireRuleQueueLaneKey = "resolution" | "stack" | "task" | "trigger";

export type WireRuleQueueState =
  | "idle"
  | "resolution-history"
  | "stack-response"
  | "task-blocked"
  | "task-open"
  | "trigger-pending";

export type WireRuleQueueLaneState = "active" | "blocked" | "empty" | "waiting";

export type WireRuleQueueMetric = {
  key: string;
  label: string;
  mine?: boolean;
  value: string;
};

export type WireRuleQueueLane = {
  count: number;
  headline: string;
  hint: string;
  key: WireRuleQueueLaneKey;
  label: string;
  state: WireRuleQueueLaneState;
};

export type WireRuleQueueSequenceItem = {
  detailLabel: string;
  key: string;
  lane: WireRuleQueueLaneKey;
  label: string;
  objectCount: number;
  stateLabel: string;
  tickLabel?: string;
};

export type WireRuleQueueInspectorLane = {
  count: number;
  headline: string;
  hint: string;
  key: WireRuleQueueLaneKey;
  label: string;
  state: WireRuleQueueLaneState;
  stateLabel: string;
};

export type WireRuleQueueInspectorSequence = {
  detailLabel: string;
  key: string;
  laneLabel: string;
  label: string;
  objectCount: number;
  stateLabel: string;
  tickLabel?: string;
};

export type WireRuleQueueInspectorPlan = {
  activeLaneKey: WireRuleQueueLaneKey | "none";
  activeLaneLabel: string;
  lanes: WireRuleQueueInspectorLane[];
  nextStepLabel: string;
  sequence: WireRuleQueueInspectorSequence[];
  state: WireRuleQueueState;
  stateLabel: string;
  summary: string;
};

export type WireRuleQueueDetailLine = {
  label: string;
  mine?: boolean;
  value: string;
};

export type WireRuleQueueObjectRef = {
  id: string;
  label?: string;
  role: string;
};

export type WireRuleQueueDetailPlan = {
  id: string;
  lines: WireRuleQueueDetailLine[];
  refs: WireRuleQueueObjectRef[];
  source: "rule";
  subtitle?: string;
  title: string;
};

export type WireRuleQueueItemPlan = {
  detail: WireRuleQueueDetailPlan;
  key: string;
  lines: WireRuleQueueDetailLine[];
  refs: WireRuleQueueObjectRef[];
  subtitle: string;
  title: string;
};

export type WireRuleQueueSectionPlan = {
  emptyLabel: string;
  items: WireRuleQueueItemPlan[];
  key: WireRuleQueueLaneKey;
  notes: string[];
  title: string;
};

export type WireRuleQueuePlan = {
  activeLaneKey: WireRuleQueueLaneKey | "none";
  inspector: WireRuleQueueInspectorPlan;
  lanes: WireRuleQueueLane[];
  metrics: WireRuleQueueMetric[];
  nextStepLabel: string;
  sections: WireRuleQueueSectionPlan[];
  sequence: WireRuleQueueSequenceItem[];
  state: WireRuleQueueState;
  stateLabel: string;
};

type BuildWireRuleQueuePlanInput = {
  playerId: string;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
};

type RuleQueueCounts = {
  battlefieldResolutionCount: number;
  battleResolutionCount: number;
  pendingTaskCount: number;
  stackCount: number;
  taskCount: number;
  triggerCount: number;
};

export function buildWireRuleQueuePlan({
  playerId,
  prompt,
  snapshot
}: BuildWireRuleQueuePlanInput): WireRuleQueuePlan {
  const timing = asRecord(snapshot?.timing);
  const queue = asRecord(timing.pendingTaskQueue);
  const turnWindow = asRecord(timing.turnWindow);
  const stack = asArray<Record<string, unknown>>(snapshot?.stack);
  const pendingTasks = asArray<Record<string, unknown>>(queue.tasks);
  const battlefieldTasks = asArray<Record<string, unknown>>(timing.battlefieldTasks);
  const tasks = [...pendingTasks, ...battlefieldTasks];
  const triggers = asArray<Record<string, unknown>>(timing.triggerQueue);
  const battlefieldResolutions = asArray<Record<string, unknown>>(timing.battlefieldResolutions);
  const battleResolutions = asArray<Record<string, unknown>>(timing.battleResolutions);
  const counts: RuleQueueCounts = {
    battlefieldResolutionCount: battlefieldResolutions.length,
    battleResolutionCount: battleResolutions.length,
    pendingTaskCount: pendingTasks.length,
    stackCount: stack.length,
    taskCount: tasks.length,
    triggerCount: triggers.length
  };
  const isBlocking = Boolean(queue.isBlocking);
  const state = queueState({ counts, isBlocking });
  const activeLaneKey = activeLaneForState(state);
  const phase = asString(timing.phase, snapshot?.turnState ?? "");
  const windowState = asString(asRecord(timing.turnWindow).state, asString(timing.timingState, ""));
  const actingPlayerId = asString(turnWindow.actingPlayerId, asString(timing.priorityPlayerId, ""));
  const promptOwner = prompt?.playerId ?? asString(timing.promptPlayerId, "");

  const lanes = [
    lane({
      count: stack.length,
      headline: stack.length > 0 ? topStackHeadline(stack[0], stack.length) : "空",
      hint: stack.length > 0 ? "等待响应或继续按 LIFO 结算" : "当前无待响应结算项目",
      key: "stack",
      label: "结算链",
      state: laneState(activeLaneKey, "stack", stack.length, false)
    }),
    lane({
      count: tasks.length,
      headline: tasks.length > 0 ? topTaskHeadline(tasks[0], isBlocking) : "空",
      hint: tasks.length > 0 ? taskQueueHint(queue, isBlocking) : "当前无服务端规则任务",
      key: "task",
      label: "规则任务",
      state: laneState(activeLaneKey, "task", tasks.length, isBlocking)
    }),
    lane({
      count: triggers.length,
      headline: triggers.length > 0 ? topTriggerHeadline(triggers[0]) : "空",
      hint: triggers.length > 0 ? "等待触发排序或触发结算" : "当前无待排序触发",
      key: "trigger",
      label: "触发队列",
      state: laneState(activeLaneKey, "trigger", triggers.length, false)
    }),
    lane({
      count: battlefieldResolutions.length + battleResolutions.length,
      headline: resolutionHeadline(battlefieldResolutions, battleResolutions),
      hint: battlefieldResolutions.length + battleResolutions.length > 0 ? "可选择近期规则事件查看桌面投影" : "当前无近期战场或战斗结算",
      key: "resolution",
      label: "近期事件",
      state: laneState(activeLaneKey, "resolution", battlefieldResolutions.length + battleResolutions.length, false)
    })
  ];
  const metrics = [
    { key: "phase", label: "阶段", value: matchPhaseLabel(phase) },
    { key: "window", label: "窗口", value: timingStateLabel(windowState) },
    { key: "acting-player", label: "行动权", mine: actingPlayerId === playerId, value: actingPlayerId || "无" },
    { key: "prompt-owner", label: "提示归属", mine: promptOwner === playerId, value: promptOwner || "无" },
    { key: "stack", label: "结算链", value: `${counts.stackCount} 项` },
    { key: "task", label: "任务", value: `${counts.taskCount} 项` },
    { key: "trigger", label: "触发", value: `${counts.triggerCount} 项` },
    { key: "resolution", label: "近期事件", value: `${counts.battlefieldResolutionCount + counts.battleResolutionCount} 项` }
  ];
  const sequence = queueSequence({ battleResolutions, battlefieldResolutions, stack, tasks, triggers });
  const nextStep = nextStepLabel(state);
  const objectIndex = buildCardObjectIndex(snapshot);
  const sections = ruleQueueSections({
    battleResolutions,
    battlefieldResolutions,
    objects: objectIndex,
    playerId,
    queue,
    stack,
    tasks,
    triggers
  });

  return {
    activeLaneKey,
    inspector: inspectorPlan({ activeLaneKey, lanes, nextStepLabel: nextStep, sequence, state }),
    lanes,
    metrics,
    nextStepLabel: nextStep,
    sections,
    sequence,
    state,
    stateLabel: queueStateLabel(state)
  };
}

function queueState({ counts, isBlocking }: { counts: RuleQueueCounts; isBlocking: boolean }): WireRuleQueueState {
  if (counts.taskCount > 0) {
    return isBlocking ? "task-blocked" : "task-open";
  }

  if (counts.stackCount > 0) {
    return "stack-response";
  }

  if (counts.triggerCount > 0) {
    return "trigger-pending";
  }

  if (counts.battlefieldResolutionCount + counts.battleResolutionCount > 0) {
    return "resolution-history";
  }

  return "idle";
}

function activeLaneForState(state: WireRuleQueueState): WireRuleQueueLaneKey | "none" {
  switch (state) {
    case "resolution-history":
      return "resolution";
    case "stack-response":
      return "stack";
    case "task-blocked":
    case "task-open":
      return "task";
    case "trigger-pending":
      return "trigger";
    case "idle":
      return "none";
  }
}

function queueStateLabel(state: WireRuleQueueState): string {
  switch (state) {
    case "idle":
      return "空闲";
    case "resolution-history":
      return "近期规则事件";
    case "stack-response":
      return "等待响应";
    case "task-blocked":
      return "规则阻塞";
    case "task-open":
      return "规则任务";
    case "trigger-pending":
      return "触发待处理";
  }
}

function nextStepLabel(state: WireRuleQueueState): string {
  switch (state) {
    case "idle":
      return "等待服务端下一条规则事件。";
    case "resolution-history":
      return "选择近期规则事件，查看对象投影与关联候选。";
    case "stack-response":
      return "等待双方响应，或由服务端继续结算链。";
    case "task-blocked":
      return "等待服务端处理阻塞规则任务，普通行动保持只读。";
    case "task-open":
      return "查看规则任务详情，等待服务端推进任务队列。";
    case "trigger-pending":
      return "等待触发排序或触发结算。";
  }
}

function lane({
  count,
  headline,
  hint,
  key,
  label,
  state
}: WireRuleQueueLane): WireRuleQueueLane {
  return { count, headline, hint, key, label, state };
}

function laneState(
  activeLaneKey: WireRuleQueueLaneKey | "none",
  key: WireRuleQueueLaneKey,
  count: number,
  blocked: boolean
): WireRuleQueueLaneState {
  if (count <= 0) {
    return "empty";
  }

  if (blocked) {
    return "blocked";
  }

  return activeLaneKey === key ? "active" : "waiting";
}

function inspectorPlan({
  activeLaneKey,
  lanes,
  nextStepLabel,
  sequence,
  state
}: {
  activeLaneKey: WireRuleQueueLaneKey | "none";
  lanes: WireRuleQueueLane[];
  nextStepLabel: string;
  sequence: WireRuleQueueSequenceItem[];
  state: WireRuleQueueState;
}): WireRuleQueueInspectorPlan {
  const activeLane = lanes.find((lane) => lane.key === activeLaneKey);
  const stateLabel = queueStateLabel(state);
  const activeLaneLabel = activeLane?.label ?? "无活动通道";

  return {
    activeLaneKey,
    activeLaneLabel,
    lanes: lanes.map((laneItem) => ({
      ...laneItem,
      stateLabel: laneStateLabel(laneItem.state)
    })),
    nextStepLabel,
    sequence: sequence.map((item) => ({
      detailLabel: item.detailLabel,
      key: item.key,
      label: item.label,
      laneLabel: laneLabel(item.lane),
      objectCount: item.objectCount,
      stateLabel: item.stateLabel,
      tickLabel: item.tickLabel
    })),
    state,
    stateLabel,
    summary: `${stateLabel} / ${activeLaneLabel} / ${nextStepLabel}`
  };
}

function laneLabel(key: WireRuleQueueLaneKey): string {
  switch (key) {
    case "resolution":
      return "近期事件";
    case "stack":
      return "结算链";
    case "task":
      return "规则任务";
    case "trigger":
      return "触发队列";
  }
}

function laneStateLabel(state: WireRuleQueueLaneState): string {
  switch (state) {
    case "active":
      return "活动";
    case "blocked":
      return "阻塞";
    case "empty":
      return "空";
    case "waiting":
      return "等待";
  }
}

function queueSequence({
  battleResolutions,
  battlefieldResolutions,
  stack,
  tasks,
  triggers
}: {
  battleResolutions: Array<Record<string, unknown>>;
  battlefieldResolutions: Array<Record<string, unknown>>;
  stack: Array<Record<string, unknown>>;
  tasks: Array<Record<string, unknown>>;
  triggers: Array<Record<string, unknown>>;
}): WireRuleQueueSequenceItem[] {
  return [
    ...stack.map((item, index) => ({
      detailLabel: stackEffectLabel(asString(item.effectKind, "")),
      key: `stack:${asString(item.stackItemId, String(index))}`,
      label: `结算链 ${stack.length - index}`,
      lane: "stack" as const,
      objectCount: objectCount([item.sourceObjectId, ...asArray(item.targetObjectIds)]),
      stateLabel: asString(item.controllerId, "未知控制者")
    })),
    ...tasks.map((task, index) => ({
      detailLabel: taskKindLabel(asString(task.kind, "")),
      key: `task:${asString(task.taskId, String(index))}`,
      label: `任务 ${index + 1}`,
      lane: "task" as const,
      objectCount: objectCount([task.battlefieldObjectId, ...asArray(task.participantObjectIds)]),
      stateLabel: asString(task.status, "状态未提供")
    })),
    ...triggers.map((trigger, index) => ({
      detailLabel: stackEffectLabel(asString(trigger.effectKind, "")),
      key: `trigger:${asString(trigger.triggerId, String(index))}`,
      label: `触发 ${index + 1}`,
      lane: "trigger" as const,
      objectCount: objectCount([trigger.sourceObjectId]),
      stateLabel: asString(trigger.controllerId, "无控制者")
    })),
    ...battlefieldResolutions.map((resolution, index) => ({
      detailLabel: battlefieldResolutionLabel(asString(resolution.kind, "")),
      key: `battlefield-resolution:${asString(resolution.resolutionId, String(index))}`,
      label: `战场事件 ${index + 1}`,
      lane: "resolution" as const,
      objectCount: objectCount([resolution.battlefieldObjectId, resolution.sourceObjectId, ...asArray(resolution.participantObjectIds)]),
      stateLabel: asString(resolution.playerId, asString(resolution.controllerId, "无控制者")),
      tickLabel: tickLabel(resolution.tick)
    })),
    ...battleResolutions.map((resolution, index) => ({
      detailLabel: battleResolutionLabel(asString(resolution.kind, "")),
      key: `battle-resolution:${asString(resolution.resolutionId, String(index))}`,
      label: `战斗事件 ${index + 1}`,
      lane: "resolution" as const,
      objectCount: objectCount([
        resolution.battlefieldId,
        ...asArray(resolution.attackerObjectIds),
        ...asArray(resolution.defenderObjectIds),
        ...asArray(resolution.destroyedObjectIds)
      ]),
      stateLabel: asString(resolution.winnerPlayerId, "无胜者"),
      tickLabel: tickLabel(resolution.tick)
    }))
  ].slice(0, 8);
}

function ruleQueueSections({
  battleResolutions,
  battlefieldResolutions,
  objects,
  playerId,
  queue,
  stack,
  tasks,
  triggers
}: {
  battleResolutions: Array<Record<string, unknown>>;
  battlefieldResolutions: Array<Record<string, unknown>>;
  objects: SnapshotObjectIndex;
  playerId: string;
  queue: Record<string, unknown>;
  stack: Array<Record<string, unknown>>;
  tasks: Array<Record<string, unknown>>;
  triggers: Array<Record<string, unknown>>;
}): WireRuleQueueSectionPlan[] {
  return [
    {
      emptyLabel: "当前无结算链项目。",
      items: stack.map((item, index) => stackItemPlan(item, index, stack.length, playerId, objects)),
      key: "stack",
      notes: [],
      title: "结算链"
    },
    {
      emptyLabel: "当前无待处理规则任务。",
      items: tasks.map((task, index) => taskItemPlan(task, index, playerId, objects)),
      key: "task",
      notes: tasks.length > 0 ? taskQueueNotes(queue) : [],
      title: "规则任务"
    },
    {
      emptyLabel: "当前无待排序或待结算触发。",
      items: triggers.map((trigger, index) => triggerItemPlan(trigger, index, playerId, objects)),
      key: "trigger",
      notes: [],
      title: "触发队列"
    },
    {
      emptyLabel: "暂无近期战场或战斗结算。",
      items: [
        ...battlefieldResolutions.map((resolution, index) => battlefieldResolutionItemPlan(resolution, index, playerId, objects)),
        ...battleResolutions.map((resolution, index) => battleResolutionItemPlan(resolution, index, playerId, objects))
      ],
      key: "resolution",
      notes: [],
      title: "近期规则事件"
    }
  ];
}

function stackItemPlan(
  item: Record<string, unknown>,
  index: number,
  stackLength: number,
  playerId: string,
  objects: SnapshotObjectIndex
): WireRuleQueueItemPlan {
  const stackItemId = asString(item.stackItemId, String(index));
  const controllerId = optionalString(item.controllerId);
  const sourceObjectId = optionalString(item.sourceObjectId);
  const cardNo = nullableString(item.cardNo);
  const targetObjectIds = stringArray(item.targetObjectIds);
  const effectKind = asString(item.effectKind, "");
  const damageAmount = numberValue(item.damageAmount);
  const destination = optionalString(item.destination);
  const lines = compactLines([
    detailLine("控制者", controllerId ?? "未知", controllerId === playerId),
    detailLine("来源", sourceLabel(sourceObjectId, cardNo, objects)),
    detailLine("目标", objectListLabel(targetObjectIds, objects)),
    damageAmount != null && damageAmount > 0 ? detailLine("伤害", String(damageAmount)) : undefined,
    destination ? detailLine("去向", zoneLabel(destination)) : undefined
  ]);
  const refs = [
    objectRef("来源", sourceObjectId),
    ...objectRefs("目标", targetObjectIds)
  ];
  const detail = detailPlan({
    id: ruleDetailId("stack", stackItemId),
    lines: compactLines([...lines, detailLine("服务端编号", idLabel(stackItemId))]),
    refs,
    subtitle: `${stackEffectLabel(effectKind)} / 项目 ${stackLength - index}`,
    title: "结算链项目"
  });

  return {
    detail,
    key: `stack:${stackItemId}`,
    lines,
    refs,
    subtitle: stackEffectLabel(effectKind),
    title: `项目 ${stackLength - index}`
  };
}

function taskItemPlan(
  task: Record<string, unknown>,
  index: number,
  playerId: string,
  objects: SnapshotObjectIndex
): WireRuleQueueItemPlan {
  const taskId = asString(task.taskId, String(index));
  const actingPlayerId = nullableString(task.actingPlayerId);
  const battlefieldObjectId = optionalString(task.battlefieldObjectId);
  const participantObjectIds = stringArray(task.participantObjectIds);
  const stackItemIds = stringArray(task.stackItemIds);
  const spellDuelId = optionalString(task.spellDuelId);
  const battleId = optionalString(task.battleId);
  const status = optionalString(task.status);
  const lines = compactLines([
    detailLine("原因", taskReasonLabel(optionalString(task.reason))),
    detailLine("战场", battlefieldObjectId ? objectLabel(battlefieldObjectId, objects) : "无"),
    detailLine("行动玩家", actingPlayerId ?? "无", actingPlayerId === playerId),
    detailLine("参与对象", objectListLabel(participantObjectIds, objects)),
    stackItemIds.length > 0 ? detailLine("关联结算链", `${stackItemIds.length} 项`) : undefined,
    spellDuelId ? detailLine("法术对决", "服务端已创建") : undefined,
    battleId ? detailLine("战斗", "服务端已创建") : undefined
  ]);
  const refs = [
    objectRef("战场", battlefieldObjectId),
    ...objectRefs("参与", participantObjectIds)
  ];
  const detail = detailPlan({
    id: ruleDetailId("task", taskId),
    lines: compactLines([detailLine("状态", status ? protocolValue(status, "服务端状态") : "状态未提供"), ...lines]),
    refs,
    subtitle: status ? protocolValue(status, "服务端状态") : "状态未提供",
    title: taskKindLabel(optionalString(task.kind))
  });

  return {
    detail,
    key: `task:${taskId}`,
    lines: compactLines([detailLine("状态", status ? protocolValue(status, "服务端状态") : "状态未提供"), ...lines]),
    refs,
    subtitle: status ? protocolValue(status, "服务端状态") : "状态未提供",
    title: taskKindLabel(optionalString(task.kind))
  };
}

function triggerItemPlan(
  trigger: Record<string, unknown>,
  index: number,
  playerId: string,
  objects: SnapshotObjectIndex
): WireRuleQueueItemPlan {
  const triggerId = asString(trigger.triggerId, String(index));
  const controllerId = optionalString(trigger.controllerId);
  const sourceObjectId = optionalString(trigger.sourceObjectId);
  const sourceVisibility = optionalString(trigger.sourceVisibility);
  const refs = sourceVisibility === "HIDDEN" ? [objectRef("来源", "HIDDEN")] : [objectRef("来源", sourceObjectId)];
  const lines = compactLines([
    detailLine("控制者", controllerId ?? "无", controllerId === playerId),
    detailLine("来源", sourceVisibility === "HIDDEN" ? "隐藏来源" : objectLabel(sourceObjectId, objects)),
    detailLine("来源事件", eventLabel(optionalString(trigger.triggeredByEventKind)))
  ]);
  const detail = detailPlan({
    id: ruleDetailId("trigger", triggerId),
    lines: compactLines([...lines, detailLine("服务端编号", idLabel(triggerId))]),
    refs,
    subtitle: stackEffectLabel(optionalString(trigger.effectKind)),
    title: `触发 ${index + 1}`
  });

  return {
    detail,
    key: `trigger:${triggerId}`,
    lines,
    refs,
    subtitle: stackEffectLabel(optionalString(trigger.effectKind)),
    title: `触发 ${index + 1}`
  };
}

function battlefieldResolutionItemPlan(
  resolution: Record<string, unknown>,
  index: number,
  playerId: string,
  objects: SnapshotObjectIndex
): WireRuleQueueItemPlan {
  const resolutionId = asString(resolution.resolutionId, String(index));
  const battlefieldObjectId = optionalString(resolution.battlefieldObjectId);
  const playerOrController = nullableString(resolution.playerId) ?? nullableString(resolution.controllerId);
  const participantObjectIds = stringArray(resolution.participantObjectIds);
  const relatedEventKinds = stringArray(resolution.relatedEventKinds);
  const tick = numberValue(resolution.tick);
  const lines = compactLines([
    detailLine("战场", objectLabel(battlefieldObjectId, objects)),
    detailLine("控制者", playerOrController ?? "无", playerOrController === playerId),
    detailLine("参与对象", objectListLabel(participantObjectIds, objects)),
    detailLine("事件", eventListLabel(relatedEventKinds))
  ]);
  const refs = [
    objectRef("战场", battlefieldObjectId),
    objectRef("来源", nullableString(resolution.sourceObjectId)),
    ...objectRefs("参与", participantObjectIds)
  ];
  const detail = detailPlan({
    id: ruleDetailId("battlefield-resolution", resolutionId),
    lines: compactLines([
      ...lines,
      detailLine("之前控制者", nullableString(resolution.previousControllerId) ?? "无", nullableString(resolution.previousControllerId) === playerId),
      detailLine("tick", tick == null ? "无" : String(tick))
    ]),
    refs,
    subtitle: `tick ${tick ?? "无"}`,
    title: battlefieldResolutionLabel(optionalString(resolution.kind))
  });

  return {
    detail,
    key: `battlefield-resolution:${resolutionId}`,
    lines: compactLines([...lines, detailLine("tick", tick == null ? "无" : String(tick))]),
    refs,
    subtitle: `tick ${tick ?? "无"}`,
    title: battlefieldResolutionLabel(optionalString(resolution.kind))
  };
}

function battleResolutionItemPlan(
  resolution: Record<string, unknown>,
  index: number,
  playerId: string,
  objects: SnapshotObjectIndex
): WireRuleQueueItemPlan {
  const resolutionId = asString(resolution.resolutionId, String(index));
  const battlefieldId = optionalString(resolution.battlefieldId);
  const destroyedObjectIds = stringArray(resolution.destroyedObjectIds);
  const relatedEventKinds = stringArray(resolution.relatedEventKinds);
  const winnerPlayerId = nullableString(resolution.winnerPlayerId);
  const tick = numberValue(resolution.tick);
  const lines = compactLines([
    detailLine("战场", objectLabel(battlefieldId, objects)),
    detailLine("胜者", winnerPlayerId ?? "无", winnerPlayerId === playerId),
    detailLine("被摧毁", objectListLabel(destroyedObjectIds, objects)),
    detailLine("事件", eventListLabel(relatedEventKinds))
  ]);
  const refs = [
    objectRef("战场", battlefieldId),
    ...objectRefs("攻击", stringArray(resolution.attackerObjectIds)),
    ...objectRefs("防守", stringArray(resolution.defenderObjectIds)),
    ...objectRefs("被摧毁", destroyedObjectIds)
  ];
  const detail = detailPlan({
    id: ruleDetailId("battle-resolution", resolutionId),
    lines: compactLines([
      detailLine("攻击方", nullableString(resolution.attackingPlayerId) ?? "无", nullableString(resolution.attackingPlayerId) === playerId),
      detailLine("防守方", nullableString(resolution.defendingPlayerId) ?? "无", nullableString(resolution.defendingPlayerId) === playerId),
      ...lines,
      detailLine("tick", tick == null ? "无" : String(tick))
    ]),
    refs,
    subtitle: `tick ${tick ?? "无"}`,
    title: battleResolutionLabel(optionalString(resolution.kind))
  });

  return {
    detail,
    key: `battle-resolution:${resolutionId}`,
    lines: compactLines([...lines, detailLine("tick", tick == null ? "无" : String(tick))]),
    refs,
    subtitle: `tick ${tick ?? "无"}`,
    title: battleResolutionLabel(optionalString(resolution.kind))
  };
}

function taskQueueNotes(queue: Record<string, unknown>): string[] {
  const phase = taskPhaseLabel(asString(queue.phase, ""));
  const active = asString(queue.activeTaskId, "");
  const blocking = Boolean(queue.isBlocking);
  return [
    `队列阶段：${phase}`,
    `活动任务：${active ? "处理中" : "无"}`,
    blocking ? "阻塞普通行动" : "不阻塞普通行动"
  ];
}

function detailPlan({
  id,
  lines,
  refs,
  subtitle,
  title
}: {
  id: string;
  lines: WireRuleQueueDetailLine[];
  refs: WireRuleQueueObjectRef[];
  subtitle?: string;
  title: string;
}): WireRuleQueueDetailPlan {
  return {
    id,
    lines,
    refs,
    source: "rule",
    subtitle,
    title
  };
}

function detailLine(label: string, value: string | null | undefined, mine?: boolean): WireRuleQueueDetailLine {
  return { label, mine, value: value || "无" };
}

function compactLines(lines: Array<WireRuleQueueDetailLine | undefined>): WireRuleQueueDetailLine[] {
  return lines.filter((item): item is WireRuleQueueDetailLine => Boolean(item));
}

function objectRef(role: string, id: string | null | undefined): WireRuleQueueObjectRef {
  return { id: id?.trim() ?? "", role };
}

function objectRefs(role: string, ids: string[] | undefined): WireRuleQueueObjectRef[] {
  return (ids ?? []).map((id) => objectRef(role, id));
}

function ruleDetailId(kind: string, id: string): string {
  return `rule:${kind}:${id}`;
}

function objectLabel(objectId: string | null | undefined, objects: SnapshotObjectIndex): string {
  if (!objectId) {
    return "无";
  }

  if (objectId === "HIDDEN") {
    return "隐藏对象";
  }

  const object = objects[objectId];
  return object?.cardNo ? `${object.cardNo}` : idLabel(objectId);
}

function sourceLabel(sourceObjectId: string | null | undefined, cardNo: string | null | undefined, objects: SnapshotObjectIndex): string {
  if (cardNo) {
    return sourceObjectId ? `${cardNo} / ${objectLabel(sourceObjectId, objects)}` : cardNo;
  }

  return objectLabel(sourceObjectId, objects);
}

function objectListLabel(objectIds: string[] | undefined, objects: SnapshotObjectIndex): string {
  const ids = objectIds ?? [];
  if (ids.length === 0) {
    return "无";
  }

  if (ids.length <= 3) {
    return ids.map((id) => objectLabel(id, objects)).join(" / ");
  }

  return `${ids.length} 个对象`;
}

function eventLabel(kind: string | null | undefined): string {
  return kind && kind.trim().length > 0 ? labelFor({}, kind, "服务端事件", "无") : "无";
}

function eventListLabel(kinds: string[] | undefined): string {
  const values = kinds ?? [];
  if (values.length === 0) {
    return "无";
  }

  if (values.length <= 2) {
    return values.map(eventLabel).join(" / ");
  }

  return `${values.length} 个事件`;
}

function zoneLabel(value: string): string {
  const labels: Record<string, string> = {
    BASE: "基地",
    GRAVEYARD: "已打出",
    HAND: "手牌",
    STACK: "结算链"
  };
  return labelFor(labels, value, "服务端区域", "无");
}

function protocolValue(value: string | null | undefined, fallback: string): string {
  const raw = value?.trim() ?? "";
  if (!raw) {
    return "无";
  }

  return isProtocolToken(raw) ? fallback : raw;
}

function idLabel(value: string): string {
  return isProtocolToken(value) ? "服务端对象" : redactInternalText(value);
}

function stringArray(value: unknown): string[] {
  return asArray(value).filter((item): item is string => typeof item === "string" && item.trim().length > 0);
}

function optionalString(value: unknown): string {
  return typeof value === "string" && value.trim().length > 0 ? value : "";
}

function nullableString(value: unknown): string | null {
  return typeof value === "string" && value.trim().length > 0 ? value : null;
}

function numberValue(value: unknown): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function topStackHeadline(item: Record<string, unknown>, stackLength: number): string {
  return `${stackEffectLabel(asString(item.effectKind, ""))} / 顶部 ${stackLength}`;
}

function topTaskHeadline(task: Record<string, unknown>, isBlocking: boolean): string {
  return `${isBlocking ? "阻塞" : "待处理"} / ${taskKindLabel(asString(task.kind, ""))}`;
}

function topTriggerHeadline(trigger: Record<string, unknown>): string {
  return `${stackEffectLabel(asString(trigger.effectKind, ""))} / ${asString(trigger.controllerId, "无控制者")}`;
}

function taskQueueHint(queue: Record<string, unknown>, isBlocking: boolean): string {
  const phase = taskPhaseLabel(asString(queue.phase, ""));
  const active = asString(queue.activeTaskId, "");
  return `${phase} / ${active ? "活动任务处理中" : "无活动任务"} / ${isBlocking ? "阻塞行动" : "不阻塞普通行动"}`;
}

function resolutionHeadline(
  battlefieldResolutions: Array<Record<string, unknown>>,
  battleResolutions: Array<Record<string, unknown>>
): string {
  const firstBattlefield = battlefieldResolutions[0];
  if (firstBattlefield) {
    return battlefieldResolutionLabel(asString(firstBattlefield.kind, ""));
  }

  const firstBattle = battleResolutions[0];
  if (firstBattle) {
    return battleResolutionLabel(asString(firstBattle.kind, ""));
  }

  return "空";
}

function objectCount(values: unknown[]): number {
  return new Set(values.filter((value): value is string => typeof value === "string" && value.trim().length > 0 && value !== "HIDDEN")).size;
}

function tickLabel(value: unknown): string | undefined {
  return typeof value === "number" && Number.isFinite(value) ? `tick ${value}` : undefined;
}

function stackEffectLabel(value: string): string {
  const labels: Record<string, string> = {
    ABILITY: "技能",
    HEXTECH_RAY_DAMAGE_3: "海克斯射线伤害",
    LEGEND_ABILITY: "传奇技能",
    REVEAL_CARD: "翻开待命",
    SPELL: "法术",
    TRIGGER: "触发"
  };
  return labelFor(labels, value, "服务端效果", "效果");
}

function taskKindLabel(value: string): string {
  const labels: Record<string, string> = {
    BATTLEFIELD_CONTESTED: "战场控制检查",
    DESTROY_LETHAL_UNIT: "致命伤害清理",
    DESTROY_ZERO_POWER_UNIT: "0 战力清理",
    REMOVE_ILLEGAL_STANDBY: "待命清理",
    RECALL_UNATTACHED_EQUIPMENT: "装备清理",
    START_BATTLE: "开始战斗",
    START_SPELL_DUEL: "开始法术对决"
  };
  return labelFor(labels, value, "服务端任务", "任务");
}

function taskPhaseLabel(value: string): string {
  const labels: Record<string, string> = {
    BATTLE_TASKS: "战斗任务",
    BATTLEFIELD_TASKS: "战场任务",
    IDLE: "空闲",
    SPELL_DUEL_TASKS: "法术对决任务",
    STATE_BASED_CLEANUP: "状态清理"
  };
  return labelFor(labels, value, "服务端阶段", "无");
}

function taskReasonLabel(value: string): string {
  const labels: Record<string, string> = {
    ADDITIONAL_COST: "额外费用",
    BATTLE_CLEANUP: "战斗清理",
    BATTLE_CLEANUP_ATTACKER_RECALL: "战斗后召回攻击者",
    BATTLEFIELD_CONTROL_CLEANUP: "战场控制清理",
    BATTLEFIELD_CONTESTED: "战场争夺",
    LETHAL_DAMAGE: "致命伤害",
    UNATTACHED_EQUIPMENT_CLEANUP: "装备脱离清理",
    ZERO_POWER: "0 战力"
  };
  return labelFor(labels, value, "服务端原因", "无");
}

function battlefieldResolutionLabel(value: string): string {
  const labels: Record<string, string> = {
    CONQUERED: "征服",
    CONTROL_RESOLVED: "控制结算",
    HELD: "据守"
  };
  return labelFor(labels, value, "战场结果", "战场结果");
}

function battleResolutionLabel(value: string): string {
  const labels: Record<string, string> = {
    CLOSED: "战斗结束",
    NO_RESULT: "战斗无结果"
  };
  return labelFor(labels, value, "战斗结果", "战斗结果");
}

function labelFor(map: Record<string, string>, value: string, protocolFallback: string, emptyFallback: string): string {
  const raw = value.trim();
  if (!raw) {
    return emptyFallback;
  }

  return map[raw] ?? (isProtocolToken(raw) ? protocolFallback : raw);
}

function isProtocolToken(value: string): boolean {
  return /^[A-Z0-9_:-]+$/.test(value) || /^[a-z0-9]+(?:[-_:][a-z0-9]+)+$/.test(value);
}

import type { ActionPromptDto, SnapshotDto } from "../types/protocol";
import { asArray, asRecord, asString } from "./collections";
import { matchPhaseLabel, timingStateLabel } from "./formatters";

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

export type WireRuleQueuePlan = {
  activeLaneKey: WireRuleQueueLaneKey | "none";
  lanes: WireRuleQueueLane[];
  metrics: WireRuleQueueMetric[];
  nextStepLabel: string;
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

  return {
    activeLaneKey,
    lanes: [
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
    ],
    metrics: [
      { key: "phase", label: "阶段", value: matchPhaseLabel(phase) },
      { key: "window", label: "窗口", value: timingStateLabel(windowState) },
      { key: "acting-player", label: "行动权", mine: actingPlayerId === playerId, value: actingPlayerId || "无" },
      { key: "prompt-owner", label: "提示归属", mine: promptOwner === playerId, value: promptOwner || "无" },
      { key: "stack", label: "结算链", value: `${counts.stackCount} 项` },
      { key: "task", label: "任务", value: `${counts.taskCount} 项` },
      { key: "trigger", label: "触发", value: `${counts.triggerCount} 项` },
      { key: "resolution", label: "近期事件", value: `${counts.battlefieldResolutionCount + counts.battleResolutionCount} 项` }
    ],
    nextStepLabel: nextStepLabel(state),
    sequence: queueSequence({ battleResolutions, battlefieldResolutions, stack, tasks, triggers }),
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

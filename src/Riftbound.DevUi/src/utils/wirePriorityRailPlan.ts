import type { ActionPromptDto, ConnectionStatus, SnapshotDto } from "../types/protocol";

export type WirePriorityRailMode =
  | "battle"
  | "battlefield-task"
  | "disconnected"
  | "main-action"
  | "spell-duel"
  | "stack-response"
  | "task"
  | "waiting";

export type WirePriorityRailStepState = "active" | "blocked" | "done" | "waiting";

export type WirePriorityRailStep = {
  hint: string;
  key: string;
  label: string;
  mine?: boolean;
  state: WirePriorityRailStepState;
  value: string;
};

export type WirePriorityRailPlan = {
  activeStepKey: string;
  blockingReasonLabel: string;
  headline: string;
  mode: WirePriorityRailMode;
  modeLabel: string;
  nextInteractionLabel: string;
  steps: WirePriorityRailStep[];
};

type BuildWirePriorityRailPlanInput = {
  connectionStatus: ConnectionStatus;
  playerId: string;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
};

export function buildWirePriorityRailPlan({
  connectionStatus,
  playerId,
  prompt,
  snapshot
}: BuildWirePriorityRailPlanInput): WirePriorityRailPlan {
  const timing = record(snapshot?.timing);
  const queue = record(timing.pendingTaskQueue);
  const turnWindow = record(timing.turnWindow);
  const spellDuel = record(timing.spellDuel);
  const battle = record(timing.battle);
  const tasks = [
    ...array<Record<string, unknown>>(queue.tasks),
    ...array<Record<string, unknown>>(timing.battlefieldTasks)
  ];
  const triggerCount = array(timing.triggerQueue).length;
  const stackCount = array(snapshot?.stack).length;
  const promptType = prompt?.view?.type ?? "WAIT";
  const promptOwnerId = prompt?.playerId ?? stringValue(timing.promptPlayerId);
  const activePlayerId = snapshot?.activePlayerId || stringValue(turnWindow.actingPlayerId) || stringValue(timing.priorityPlayerId);
  const focusPlayerId = stringValue(spellDuel.focusPlayerId) || stringValue(timing.focusPlayerId);
  const enabledCandidateCount = (prompt?.candidates ?? []).filter((candidate) => candidate.enabled).length;
  const isConnected = connectionStatus === "connected" || connectionStatus === "resyncing";
  const isBlocking = Boolean(queue.isBlocking);
  const taskPhase = stringValue(queue.phase);
  const hasBattleTask = tasks.some((task) => Boolean(stringValue(task.battleId))) || promptType === "BATTLE_DECLARATION" || promptType === "ASSIGN_COMBAT_DAMAGE" || Boolean(stringValue(battle.battleId));
  const hasSpellDuelTask = tasks.some((task) => Boolean(stringValue(task.spellDuelId))) || promptType.startsWith("SPELL_DUEL") || Boolean(stringValue(spellDuel.spellDuelId)) || Boolean(focusPlayerId);
  const hasBattlefieldTask = taskPhase === "BATTLEFIELD_TASKS" || tasks.some((task) => stringValue(task.kind) === "BATTLEFIELD_CONTESTED");
  const mine = Boolean(prompt?.actionable && promptOwnerId === playerId);
  const mode = railMode({
    connectionStatus,
    hasBattleTask,
    hasBattlefieldTask,
    hasSpellDuelTask,
    isBlocking,
    isConnected,
    promptActionable: Boolean(prompt?.actionable),
    stackCount,
    taskCount: tasks.length
  });
  const activeStepKey = railActiveStepKey(mode);
  const blockingReasonLabel = topBlockingReason({ isBlocking, tasks, triggerCount });
  const nextInteractionLabel = nextInteraction({
    activePlayerId,
    enabledCandidateCount,
    focusPlayerId,
    isBlocking,
    isConnected,
    mine,
    promptActionable: Boolean(prompt?.actionable),
    promptOwnerId,
    stackCount,
    tasks,
    triggerCount
  });

  return {
    activeStepKey,
    blockingReasonLabel,
    headline: railHeadline(mode, nextInteractionLabel),
    mode,
    modeLabel: railModeLabel(mode),
    nextInteractionLabel,
    steps: [
      {
        hint: `第 ${snapshot?.turnNumber ?? 0} 回合 / 当前玩家 ${activePlayerId || "无"}`,
        key: "phase",
        label: "回合阶段",
        mine: activePlayerId === playerId,
        state: stepState(activeStepKey, "phase"),
        value: phaseLabel(stringValue(timing.phase) || snapshot?.turnState || "")
      },
      {
        hint: prompt?.view?.title?.trim() || "服务端未提供可操作提示",
        key: "window",
        label: "行动窗口",
        mine: promptOwnerId === playerId,
        state: stepState(activeStepKey, "window"),
        value: windowLabel(stringValue(turnWindow.state) || stringValue(timing.timingState))
      },
      {
        hint: focusHint({ focusPlayerId, playerId, stackCount }),
        key: "focus",
        label: "响应/焦点",
        mine: focusPlayerId === playerId,
        state: stepState(activeStepKey, "focus"),
        value: focusLabel({ focusPlayerId, hasBattleTask, hasSpellDuelTask, stackCount })
      },
      {
        hint: blockingReasonLabel,
        key: "tasks",
        label: "规则任务",
        state: isBlocking ? "blocked" : stepState(activeStepKey, "tasks"),
        value: `${tasks.length} 任务 / ${triggerCount} 触发`
      },
      {
        hint: nextInteractionLabel,
        key: "entry",
        label: "操作入口",
        mine,
        state: stepState(activeStepKey, "entry"),
        value: entryLabel({ enabledCandidateCount, mine, promptActionable: Boolean(prompt?.actionable), promptOwnerId })
      }
    ]
  };
}

function railMode({
  connectionStatus,
  hasBattleTask,
  hasBattlefieldTask,
  hasSpellDuelTask,
  isBlocking,
  isConnected,
  promptActionable,
  stackCount,
  taskCount
}: {
  connectionStatus: ConnectionStatus;
  hasBattleTask: boolean;
  hasBattlefieldTask: boolean;
  hasSpellDuelTask: boolean;
  isBlocking: boolean;
  isConnected: boolean;
  promptActionable: boolean;
  stackCount: number;
  taskCount: number;
}): WirePriorityRailMode {
  if (!isConnected || connectionStatus === "reconnecting" || connectionStatus === "disconnected" || connectionStatus === "error") {
    return "disconnected";
  }

  if (hasBattleTask) {
    return "battle";
  }

  if (hasSpellDuelTask) {
    return "spell-duel";
  }

  if (hasBattlefieldTask) {
    return "battlefield-task";
  }

  if (isBlocking || taskCount > 0) {
    return "task";
  }

  if (stackCount > 0) {
    return "stack-response";
  }

  if (promptActionable) {
    return "main-action";
  }

  return "waiting";
}

function railActiveStepKey(mode: WirePriorityRailMode): string {
  switch (mode) {
    case "battle":
    case "spell-duel":
    case "stack-response":
      return "focus";
    case "battlefield-task":
    case "task":
      return "tasks";
    case "main-action":
      return "entry";
    case "disconnected":
      return "window";
    case "waiting":
      return "phase";
  }
}

function railModeLabel(mode: WirePriorityRailMode): string {
  switch (mode) {
    case "battle":
      return "战斗窗口";
    case "battlefield-task":
      return "战场任务";
    case "disconnected":
      return "连接恢复";
    case "main-action":
      return "普通行动";
    case "spell-duel":
      return "法术对决";
    case "stack-response":
      return "响应窗口";
    case "task":
      return "规则任务";
    case "waiting":
      return "等待服务端";
  }
}

function railHeadline(mode: WirePriorityRailMode, nextInteractionLabel: string): string {
  return `${railModeLabel(mode)}：${nextInteractionLabel}`;
}

function stepState(activeStepKey: string, key: string): WirePriorityRailStepState {
  if (activeStepKey === key) {
    return "active";
  }

  const order = ["phase", "window", "focus", "tasks", "entry"];
  return order.indexOf(key) < order.indexOf(activeStepKey) ? "done" : "waiting";
}

function nextInteraction({
  activePlayerId,
  enabledCandidateCount,
  focusPlayerId,
  isBlocking,
  isConnected,
  mine,
  promptActionable,
  promptOwnerId,
  stackCount,
  tasks,
  triggerCount
}: {
  activePlayerId?: string;
  enabledCandidateCount: number;
  focusPlayerId: string;
  isBlocking: boolean;
  isConnected: boolean;
  mine: boolean;
  promptActionable: boolean;
  promptOwnerId?: string;
  stackCount: number;
  tasks: Array<Record<string, unknown>>;
  triggerCount: number;
}): string {
  if (!isConnected) {
    return "先恢复连接或重新同步服务端快照";
  }

  if (isBlocking || tasks.length > 0) {
    return "等待服务端处理规则任务队列";
  }

  if (mine) {
    return `从服务端候选提交 ${enabledCandidateCount} 项可操作`;
  }

  if (promptActionable) {
    return `等待 ${promptOwnerId || "对手"} 提交候选`;
  }

  if (focusPlayerId) {
    return `等待 ${focusPlayerId} 的焦点响应`;
  }

  if (triggerCount > 0) {
    return "等待触发排序或触发结算";
  }

  if (stackCount > 0) {
    return "等待响应或继续结算链";
  }

  return `等待 ${activePlayerId || "服务端"} 的下一窗口`;
}

function topBlockingReason({
  isBlocking,
  tasks,
  triggerCount
}: {
  isBlocking: boolean;
  tasks: Array<Record<string, unknown>>;
  triggerCount: number;
}): string {
  const first = tasks[0];
  if (first) {
    const kind = taskKindLabel(stringValue(first.kind));
    const reason = taskReasonLabel(stringValue(first.reason));
    return `${isBlocking ? "阻塞" : "待处理"}：${kind} / ${reason}`;
  }

  if (triggerCount > 0) {
    return `待处理：${triggerCount} 个触发`;
  }

  return "无阻塞规则任务";
}

function focusLabel({
  focusPlayerId,
  hasBattleTask,
  hasSpellDuelTask,
  stackCount
}: {
  focusPlayerId: string;
  hasBattleTask: boolean;
  hasSpellDuelTask: boolean;
  stackCount: number;
}): string {
  if (hasBattleTask) {
    return "战斗响应";
  }

  if (hasSpellDuelTask) {
    return focusPlayerId ? `法术对决 ${focusPlayerId}` : "法术对决";
  }

  if (stackCount > 0) {
    return `结算链 ${stackCount} 项`;
  }

  return "无响应焦点";
}

function focusHint({
  focusPlayerId,
  playerId,
  stackCount
}: {
  focusPlayerId: string;
  playerId: string;
  stackCount: number;
}): string {
  if (focusPlayerId) {
    return focusPlayerId === playerId ? "当前焦点在我方" : `当前焦点在 ${focusPlayerId}`;
  }

  if (stackCount > 0) {
    return "服务端结算链存在待响应项目";
  }

  return "当前无服务端焦点对象";
}

function entryLabel({
  enabledCandidateCount,
  mine,
  promptActionable,
  promptOwnerId
}: {
  enabledCandidateCount: number;
  mine: boolean;
  promptActionable: boolean;
  promptOwnerId?: string;
}): string {
  if (mine) {
    return `我方 ${enabledCandidateCount} 项`;
  }

  if (promptActionable) {
    return `${promptOwnerId || "对手"} ${enabledCandidateCount} 项`;
  }

  return "只读";
}

function phaseLabel(value: string): string {
  const labels: Record<string, string> = {
    MAIN: "主阶段",
    MULLIGAN: "起手调整",
    ROOM: "房间阶段",
    TURN_END: "回合结束",
    TURN_START: "回合开始"
  };
  return labelFor(labels, value, "服务端阶段", "等待开局");
}

function windowLabel(value: string): string {
  const labels: Record<string, string> = {
    MAIN_ACTION: "主行动窗口",
    MULLIGAN: "起手调整",
    NEUTRAL_CLOSED: "普通闭环",
    NEUTRAL_OPEN: "普通开环",
    ROOM: "房间窗口",
    SPELL_DUEL_CLOSED: "法术对决闭环",
    SPELL_DUEL_OPEN: "法术对决开环"
  };
  return labelFor(labels, value, "服务端窗口", "未知窗口");
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
  return labelFor(labels, value, "服务端任务", "规则任务");
}

function taskReasonLabel(value: string): string {
  const labels: Record<string, string> = {
    ADDITIONAL_COST: "额外费用",
    BATTLE_CLEANUP: "战斗清理",
    BATTLEFIELD_CONTESTED: "战场争夺",
    LETHAL_DAMAGE: "致命伤害",
    UNATTACHED_EQUIPMENT_CLEANUP: "装备脱离清理",
    ZERO_POWER: "0 战力"
  };
  return labelFor(labels, value, "服务端规则", "服务端规则");
}

function labelFor(map: Record<string, string>, value: string, protocolFallback: string, emptyFallback: string): string {
  if (!value) {
    return emptyFallback;
  }

  return map[value] ?? (isProtocolToken(value) ? protocolFallback : value);
}

function isProtocolToken(value: string): boolean {
  return /^[A-Z0-9_:-]+$/.test(value) || /^[a-z0-9]+(?:[-_:][a-z0-9]+)+$/.test(value);
}

function record(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" && !Array.isArray(value)
    ? value as Record<string, unknown>
    : {};
}

function array<T>(value: unknown): T[] {
  return Array.isArray(value) ? value as T[] : [];
}

function stringValue(value: unknown): string {
  return typeof value === "string" ? value : "";
}

import type { ActionPromptDto, ConnectionStatus, SnapshotDto } from "../types/protocol";

export type WireWindowPlanTone = "bad" | "good" | "info" | "neutral" | "warn";

export type WireWindowPlanMetric = {
  key: string;
  label: string;
  mine?: boolean;
  value: string;
};

export type WireTurnWindowPlan = {
  activePlayerId?: string;
  blockingTaskCount: number;
  enabledCandidateCount: number;
  nextStepLabel: string;
  phase: string;
  promptActionable: boolean;
  promptOwnerId?: string;
  promptTitle: string;
  promptType: string;
  queueStateLabel: string;
  roomStatus: string;
  stackCount: number;
  state: "disconnected" | "opponent-action" | "resolving" | "server-wait" | "you-action";
  stateLabel: string;
  tone: WireWindowPlanTone;
  triggerCount: number;
  windowState: string;
  metrics: WireWindowPlanMetric[];
};

export function buildWireTurnWindowPlan({
  connectionStatus,
  playerId,
  prompt,
  snapshot
}: {
  connectionStatus: ConnectionStatus;
  playerId: string;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
}): WireTurnWindowPlan {
  const timing = record(snapshot?.timing);
  const turnWindow = record(timing.turnWindow);
  const queue = record(timing.pendingTaskQueue);
  const stackCount = arrayLength(snapshot?.stack);
  const taskCount = arrayLength(queue.tasks) + arrayLength(timing.battlefieldTasks);
  const triggerCount = arrayLength(timing.triggerQueue);
  const promptOwnerId = prompt?.playerId ?? stringValue(timing.promptPlayerId);
  const activePlayerId = snapshot?.activePlayerId || stringValue(turnWindow.actingPlayerId) || stringValue(timing.priorityPlayerId);
  const enabledCandidateCount = (prompt?.candidates ?? []).filter((candidate) => candidate.enabled).length;
  const isConnected = connectionStatus === "connected" || connectionStatus === "resyncing";
  const isBlocking = Boolean(queue.isBlocking);
  const promptActionable = Boolean(prompt?.actionable);
  const mine = promptActionable && promptOwnerId === playerId;
  const state = windowState({
    connectionStatus,
    isBlocking,
    isConnected,
    mine,
    promptActionable,
    stackCount,
    triggerCount
  });

  return {
    activePlayerId,
    blockingTaskCount: taskCount,
    enabledCandidateCount,
    nextStepLabel: nextStepLabel({
      activePlayerId,
      isBlocking,
      isConnected,
      mine,
      promptActionable,
      promptOwnerId,
      stackCount,
      triggerCount
    }),
    phase: stringValue(timing.phase) || snapshot?.turnState || "",
    promptActionable,
    promptOwnerId,
    promptTitle: prompt?.view?.title?.trim() || "无行动窗口",
    promptType: prompt?.view?.type || "WAIT",
    queueStateLabel: isBlocking ? "规则任务阻塞" : taskCount > 0 ? "规则任务待处理" : "无阻塞任务",
    roomStatus: stringValue(timing.roomStatus),
    stackCount,
    state,
    stateLabel: stateLabel(state),
    tone: stateTone(state),
    triggerCount,
    windowState: stringValue(turnWindow.state) || stringValue(timing.timingState),
    metrics: [
      { key: "active", label: "当前玩家", mine: activePlayerId === playerId, value: activePlayerId || "无" },
      { key: "prompt", label: "提示归属", mine: promptOwnerId === playerId, value: promptOwnerId || "无" },
      { key: "candidates", label: "可提交", value: String(enabledCandidateCount) },
      { key: "stack", label: "结算链", value: `${stackCount} 项` },
      { key: "tasks", label: "任务", value: `${taskCount} 项` },
      { key: "triggers", label: "触发", value: `${triggerCount} 项` }
    ]
  };
}

function windowState({
  connectionStatus,
  isBlocking,
  isConnected,
  mine,
  promptActionable,
  stackCount,
  triggerCount
}: {
  connectionStatus: ConnectionStatus;
  isBlocking: boolean;
  isConnected: boolean;
  mine: boolean;
  promptActionable: boolean;
  stackCount: number;
  triggerCount: number;
}): WireTurnWindowPlan["state"] {
  if (!isConnected || connectionStatus === "reconnecting" || connectionStatus === "disconnected" || connectionStatus === "error") {
    return "disconnected";
  }

  if (isBlocking) {
    return "resolving";
  }

  if (mine) {
    return "you-action";
  }

  if (promptActionable) {
    return "opponent-action";
  }

  if (stackCount > 0 || triggerCount > 0) {
    return "resolving";
  }

  return "server-wait";
}

function stateLabel(state: WireTurnWindowPlan["state"]): string {
  switch (state) {
    case "disconnected":
      return "连接未稳定";
    case "opponent-action":
      return "等待对手行动";
    case "resolving":
      return "规则正在结算";
    case "server-wait":
      return "等待服务端窗口";
    case "you-action":
      return "轮到你操作";
  }
}

function stateTone(state: WireTurnWindowPlan["state"]): WireWindowPlanTone {
  switch (state) {
    case "disconnected":
      return "bad";
    case "opponent-action":
      return "neutral";
    case "resolving":
      return "warn";
    case "server-wait":
      return "info";
    case "you-action":
      return "good";
  }
}

function nextStepLabel({
  activePlayerId,
  isBlocking,
  isConnected,
  mine,
  promptActionable,
  promptOwnerId,
  stackCount,
  triggerCount
}: {
  activePlayerId?: string;
  isBlocking: boolean;
  isConnected: boolean;
  mine: boolean;
  promptActionable: boolean;
  promptOwnerId?: string;
  stackCount: number;
  triggerCount: number;
}): string {
  if (!isConnected) {
    return "先恢复连接或重新同步快照";
  }

  if (isBlocking) {
    return "等待服务端处理规则任务";
  }

  if (mine) {
    return "从服务端候选中选择并提交";
  }

  if (promptActionable) {
    return `等待 ${promptOwnerId || "对手"} 提交服务端候选`;
  }

  if (triggerCount > 0) {
    return "等待触发排序或触发结算";
  }

  if (stackCount > 0) {
    return "等待响应、让过或结算链继续结算";
  }

  if (activePlayerId) {
    return `等待 ${activePlayerId} 的下一个服务端窗口`;
  }

  return "等待服务端推进对局";
}

function record(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" && !Array.isArray(value)
    ? value as Record<string, unknown>
    : {};
}

function arrayLength(value: unknown): number {
  return Array.isArray(value) ? value.length : 0;
}

function stringValue(value: unknown): string {
  return typeof value === "string" ? value : "";
}

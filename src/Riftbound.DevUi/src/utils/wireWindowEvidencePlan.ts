import type { ActionPromptDto, ConnectionStatus, SnapshotDto, StackItemView } from "../types/protocol";

export type WireWindowEvidenceState = "active" | "empty" | "mine" | "waiting";

export type WireWindowEvidenceRow = {
  key: string;
  label: string;
  mine?: boolean;
  source: string;
  state: WireWindowEvidenceState;
  value: string;
};

export type WireWindowEvidencePlan = {
  headline: string;
  rows: WireWindowEvidenceRow[];
};

export function buildWireWindowEvidencePlan({
  connectionStatus,
  playerId,
  prompt,
  snapshot
}: {
  connectionStatus: ConnectionStatus;
  playerId: string;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
}): WireWindowEvidencePlan {
  const timing = record(snapshot?.timing);
  const queue = record(timing.pendingTaskQueue);
  const spellDuel = record(timing.spellDuel);
  const battle = record(timing.battle);
  const stack = array<StackItemView>(snapshot?.stack);
  const tasks = [
    ...array<Record<string, unknown>>(queue.tasks),
    ...array<Record<string, unknown>>(timing.battlefieldTasks)
  ];
  const triggers = array<Record<string, unknown>>(timing.triggerQueue);
  const promptOwnerId = prompt?.playerId ?? stringValue(timing.promptPlayerId);
  const focusPlayerId = stringValue(spellDuel.focusPlayerId) || stringValue(timing.focusPlayerId);
  const activePlayerId = snapshot?.activePlayerId || stringValue(record(timing.turnWindow).actingPlayerId) || stringValue(timing.priorityPlayerId);
  const spellDuelId = stringValue(spellDuel.spellDuelId);
  const battleId = stringValue(battle.battleId);
  const topStack = stack[0];
  const mine = promptOwnerId === playerId;

  return {
    headline: evidenceHeadline({
      activePlayerId,
      battleId,
      connectionStatus,
      focusPlayerId,
      mine,
      promptActionable: Boolean(prompt?.actionable),
      promptOwnerId,
      spellDuelId,
      stackCount: stack.length,
      taskCount: tasks.length
    }),
    rows: [
      {
        key: "prompt",
        label: "提示",
        mine,
        source: "服务端行动提示",
        state: prompt?.actionable ? mine ? "mine" : "active" : "waiting",
        value: prompt?.view?.title?.trim() || prompt?.reason || "无行动提示"
      },
      {
        key: "priority",
        label: "优先/焦点",
        mine: (focusPlayerId || promptOwnerId || activePlayerId) === playerId,
        source: "服务端时机状态",
        state: focusPlayerId || promptOwnerId ? (focusPlayerId || promptOwnerId) === playerId ? "mine" : "active" : "waiting",
        value: focusPlayerId ? `焦点 ${focusPlayerId}` : promptOwnerId ? `提示 ${promptOwnerId}` : activePlayerId ? `当前 ${activePlayerId}` : "无"
      },
      {
        key: "stack",
        label: "结算链",
        source: "服务端结算链",
        state: stack.length > 0 ? "active" : "empty",
        value: stack.length > 0 ? `${stack.length} 项 / 顶部 ${stackItemLabel(topStack)}` : "空"
      },
      {
        key: "tasks",
        label: "规则任务",
        source: "服务端规则任务",
        state: tasks.length > 0 ? "active" : "empty",
        value: tasks.length > 0 ? `${tasks.length} 项 / ${taskLabel(tasks[0])}` : "空"
      },
      {
        key: "triggers",
        label: "触发队列",
        source: "服务端触发队列",
        state: triggers.length > 0 ? "active" : "empty",
        value: triggers.length > 0 ? `${triggers.length} 项 / ${triggerLabel(triggers[0])}` : "空"
      },
      {
        key: "spell-duel",
        label: "法术对决",
        mine: focusPlayerId === playerId,
        source: "服务端法术对决",
        state: spellDuelId || focusPlayerId ? focusPlayerId === playerId ? "mine" : "active" : "empty",
        value: spellDuelId || focusPlayerId ? `${spellDuelId || "无 ID"} / 焦点 ${focusPlayerId || "无"}` : "空"
      },
      {
        key: "battle",
        label: "战斗",
        source: "服务端战斗",
        state: battleId ? "active" : "empty",
        value: battleId ? `${battleId} / 战场 ${stringValue(battle.battlefieldObjectId) || "无"}` : "空"
      }
    ]
  };
}

function evidenceHeadline({
  activePlayerId,
  battleId,
  connectionStatus,
  focusPlayerId,
  mine,
  promptActionable,
  promptOwnerId,
  spellDuelId,
  stackCount,
  taskCount
}: {
  activePlayerId?: string;
  battleId: string;
  connectionStatus: ConnectionStatus;
  focusPlayerId: string;
  mine: boolean;
  promptActionable: boolean;
  promptOwnerId?: string;
  spellDuelId: string;
  stackCount: number;
  taskCount: number;
}): string {
  if (connectionStatus === "reconnecting" || connectionStatus === "disconnected" || connectionStatus === "error") {
    return "连接证据不稳定，先同步服务端快照";
  }

  if (taskCount > 0) {
    return "服务端规则任务正在决定下一步";
  }

  if (battleId) {
    return "战斗证据来自服务端 battle 快照";
  }

  if (spellDuelId || focusPlayerId) {
    return `法术对决焦点：${focusPlayerId || "服务端未公开"}`;
  }

  if (stackCount > 0) {
    return "结算链存在，等待响应或继续结算";
  }

  if (promptActionable) {
    return mine ? "你的服务端候选窗口" : `等待 ${promptOwnerId || "对手"} 的服务端候选`;
  }

  return `等待 ${activePlayerId || "服务端"} 的下一窗口`;
}

function stackItemLabel(item?: StackItemView): string {
  return item?.effectKind || item?.cardNo || item?.sourceObjectId || item?.stackItemId || "无";
}

function taskLabel(task?: Record<string, unknown>): string {
  return stringValue(task?.kind) || stringValue(task?.reason) || stringValue(task?.taskId) || "服务端任务";
}

function triggerLabel(trigger?: Record<string, unknown>): string {
  return stringValue(trigger?.effectKind) || stringValue(trigger?.triggeredByEventKind) || stringValue(trigger?.triggerId) || "服务端触发";
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

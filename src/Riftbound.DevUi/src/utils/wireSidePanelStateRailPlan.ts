import type { ActionPromptDto, ConnectionStatus, GameEvent, SnapshotDto } from "../types/protocol";
import type { CommandSubmissionFeedback } from "../stores/useMatchController";
import type { ServerSubmissionGatePlan } from "./serverSubmissionGatePlan";
import type { WireSidePanelSlot } from "../components/match/wireTableLayout";
import type { WireSidePanelOrchestrationPlan, WireSidePanelOrchestrationState } from "./wireSidePanelOrchestrationPlan";
import type { WireSidePanelRuleChainPlan } from "./wireSidePanelRuleChainPlan";

export type WireSidePanelStateRailMetricKey =
  | "candidates"
  | "connection"
  | "events"
  | "prompt"
  | "receipt"
  | "snapshot"
  | "stack"
  | "submission"
  | "tasks"
  | "triggers";

export type WireSidePanelStateRailMetricState =
  | "active"
  | "blocked"
  | "empty"
  | "offline"
  | "ready"
  | "waiting";

export type WireSidePanelStateRailMetricSource =
  | "connection"
  | "event-log"
  | "prompt"
  | "receipt"
  | "rule-chain"
  | "snapshot"
  | "submission-gate";

export type WireSidePanelStateRailMetric = {
  detail: string;
  key: WireSidePanelStateRailMetricKey;
  label: string;
  source: WireSidePanelStateRailMetricSource;
  state: WireSidePanelStateRailMetricState;
  value: string;
};

export type WireSidePanelStateRailPlan = {
  activeSlot: WireSidePanelSlot;
  byKey: Record<WireSidePanelStateRailMetricKey, WireSidePanelStateRailMetric>;
  entries: WireSidePanelStateRailMetric[];
  state: WireSidePanelOrchestrationState;
  summary: string;
};

export function buildWireSidePanelStateRailPlan({
  activeSlot,
  connectionStatus,
  events = [],
  orchestration,
  prompt,
  ruleChainPlan,
  snapshot,
  submissionFeedback,
  submissionGate
}: {
  activeSlot: WireSidePanelSlot;
  connectionStatus: ConnectionStatus;
  events?: GameEvent[];
  orchestration: WireSidePanelOrchestrationPlan;
  prompt?: ActionPromptDto;
  ruleChainPlan: WireSidePanelRuleChainPlan;
  snapshot?: SnapshotDto;
  submissionFeedback?: CommandSubmissionFeedback;
  submissionGate?: ServerSubmissionGatePlan;
}): WireSidePanelStateRailPlan {
  const candidateCount = prompt?.serverFlow?.candidateCount ?? prompt?.candidates?.length ?? 0;
  const enabledCandidateCount = prompt?.serverFlow?.enabledCandidateCount
    ?? prompt?.candidates?.filter((candidate) => candidate.enabled).length
    ?? 0;
  const stackCount = Array.isArray(snapshot?.stack) ? snapshot.stack.length : 0;
  const timing = asRecord(snapshot?.timing);
  const pendingTaskQueue = asRecord(timing.pendingTaskQueue);
  const taskCount = arrayLength(pendingTaskQueue.tasks) + arrayLength(timing.battlefieldTasks);
  const triggerCount = arrayLength(timing.triggerQueue);
  const queueCount = stackCount + taskCount + triggerCount;
  const promptType = prompt?.serverFlow?.promptType ?? prompt?.view?.type ?? prompt?.contract?.promptKind;
  const entries: WireSidePanelStateRailMetric[] = [
    metric({
      detail: connectionDetail(connectionStatus),
      key: "connection",
      label: "连接",
      source: "connection",
      state: connectionMetricState(connectionStatus),
      value: connectionLabel(connectionStatus)
    }),
    metric({
      detail: snapshot?.tick == null ? "尚未收到服务端桌面快照。" : "服务端桌面快照 tick。",
      key: "snapshot",
      label: "快照",
      source: "snapshot",
      state: snapshot?.tick == null ? "waiting" : "ready",
      value: snapshot?.tick == null ? "无" : String(snapshot.tick)
    }),
    metric({
      detail: prompt
        ? `prompt tick ${prompt.snapshotTick ?? "无"} / ${prompt.actionable ? "当前可操作" : "只读"}`
        : "服务端尚未公开行动提示。",
      key: "prompt",
      label: "提示",
      source: "prompt",
      state: prompt ? prompt.actionable ? "ready" : "waiting" : "empty",
      value: promptType ?? "无"
    }),
    metric({
      detail: candidateCount > 0
        ? `${enabledCandidateCount} 个可提交，${Math.max(0, candidateCount - enabledCandidateCount)} 个受限。`
        : "当前没有服务端候选行动。",
      key: "candidates",
      label: "候选",
      source: "prompt",
      state: candidateCount > 0 ? enabledCandidateCount > 0 ? "ready" : "blocked" : "empty",
      value: `${enabledCandidateCount}/${candidateCount}`
    }),
    metric({
      detail: ruleChainPlan.stateLabel,
      key: "stack",
      label: "栈",
      source: "rule-chain",
      state: stackCount > 0 ? "active" : "empty",
      value: String(stackCount)
    }),
    metric({
      detail: "pendingTaskQueue.tasks + battlefieldTasks。",
      key: "tasks",
      label: "任务",
      source: "snapshot",
      state: taskCount > 0 ? "active" : "empty",
      value: String(taskCount)
    }),
    metric({
      detail: "timing.triggerQueue。",
      key: "triggers",
      label: "触发",
      source: "snapshot",
      state: triggerCount > 0 ? "active" : "empty",
      value: String(triggerCount)
    }),
    metric({
      detail: events.length > 0 ? "服务端公开事件日志数量。" : "暂无公开事件。",
      key: "events",
      label: "日志",
      source: "event-log",
      state: events.length > 0 ? "active" : "empty",
      value: String(events.length)
    }),
    metric({
      detail: submissionGate?.reason ?? "尚未计算提交门。",
      key: "submission",
      label: "提交",
      source: "submission-gate",
      state: submissionGate?.canSubmit ? "ready" : "blocked",
      value: submissionGate?.stateLabel ?? "未知"
    }),
    metric({
      detail: submissionFeedback?.message ?? "尚未提交命令。",
      key: "receipt",
      label: "回执",
      source: "receipt",
      state: receiptMetricState(submissionFeedback),
      value: submissionFeedback?.stateLabel ?? "无"
    })
  ];
  const byKey = Object.fromEntries(entries.map((entry) => [entry.key, entry])) as Record<
    WireSidePanelStateRailMetricKey,
    WireSidePanelStateRailMetric
  >;

  return {
    activeSlot,
    byKey,
    entries,
    state: orchestration.state,
    summary: `${orchestration.stateLabel} / tick ${snapshot?.tick ?? "无"} / 候选 ${enabledCandidateCount}/${candidateCount} / 队列 ${queueCount}`
  };
}

function metric(entry: WireSidePanelStateRailMetric): WireSidePanelStateRailMetric {
  return entry;
}

function connectionLabel(status: ConnectionStatus): string {
  switch (status) {
    case "idle":
      return "未连接";
    case "connecting":
      return "连接中";
    case "connected":
      return "已连接";
    case "reconnecting":
      return "重连中";
    case "resyncing":
      return "同步中";
    case "disconnected":
      return "已断开";
    case "error":
      return "错误";
  }
}

function connectionDetail(status: ConnectionStatus): string {
  return status === "connected" || status === "resyncing"
    ? "连接可接收服务端投影。"
    : "连接未稳定，行动提交受限。";
}

function connectionMetricState(status: ConnectionStatus): WireSidePanelStateRailMetricState {
  switch (status) {
    case "connected":
      return "ready";
    case "resyncing":
    case "connecting":
    case "reconnecting":
      return "waiting";
    case "idle":
      return "empty";
    case "disconnected":
    case "error":
      return "offline";
  }
}

function receiptMetricState(feedback?: CommandSubmissionFeedback): WireSidePanelStateRailMetricState {
  if (!feedback) {
    return "empty";
  }

  if (feedback.state === "failed") {
    return "blocked";
  }

  return feedback.state === "submitting" ? "waiting" : "ready";
}

function arrayLength(value: unknown): number {
  return Array.isArray(value) ? value.length : 0;
}

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" && !Array.isArray(value) ? value as Record<string, unknown> : {};
}

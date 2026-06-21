import type { ActionPromptDto, ConnectionStatus, GameEvent, SnapshotDto } from "../types/protocol";
import type { TableObjectContext } from "./tableObjectContext";
import type { ServerSubmissionGatePlan } from "./serverSubmissionGatePlan";
import { buildWireRuleQueuePlan } from "./wireRuleQueuePlan";
import { buildWireServerFlowPlan } from "./wireServerFlowPlan";
import { buildWireTurnWindowPlan, type WireWindowPlanTone } from "./wireTurnWindowPlan";

export type WireMatchOverviewState =
  | "blocked"
  | "disconnected"
  | "ready"
  | "review"
  | "resolving"
  | "waiting";

export type WireMatchOverviewRowState =
  | "blocked"
  | "empty"
  | "ready"
  | "review"
  | "server"
  | "waiting"
  | "warning";

export type WireMatchOverviewRow = {
  count: number;
  key: string;
  label: string;
  sourceLabel: string;
  state: WireMatchOverviewRowState;
  stateLabel: string;
  summary: string;
  value: string;
};

export type WireMatchOverviewMetric = {
  key: string;
  label: string;
  value: string;
};

export type WireMatchOverviewTimelineDetail = {
  id?: string;
  refs?: readonly { visibility?: "hidden" | "missing" | "visible" }[];
  source?: "event" | "rule";
  title?: string;
};

export type WireMatchOverviewPlan = {
  headline: string;
  metrics: WireMatchOverviewMetric[];
  nextStepLabel: string;
  rows: WireMatchOverviewRow[];
  state: WireMatchOverviewState;
  stateLabel: string;
  tone: WireWindowPlanTone;
};

export function buildWireMatchOverviewPlan({
  connectionStatus,
  events = [],
  playerId,
  prompt,
  selectedObjectContext,
  selectedObjectId,
  snapshot,
  submissionGate,
  timelineDetail
}: {
  connectionStatus: ConnectionStatus;
  events?: GameEvent[];
  playerId: string;
  prompt?: ActionPromptDto;
  selectedObjectContext?: TableObjectContext;
  selectedObjectId?: string;
  snapshot?: SnapshotDto;
  submissionGate?: ServerSubmissionGatePlan;
  timelineDetail?: WireMatchOverviewTimelineDetail;
}): WireMatchOverviewPlan {
  const windowPlan = buildWireTurnWindowPlan({ connectionStatus, playerId, prompt, snapshot });
  const rulePlan = buildWireRuleQueuePlan({ events, playerId, prompt, selectedObjectId, snapshot });
  const serverFlowPlan = buildWireServerFlowPlan({
    connectionStatus,
    events,
    playerId,
    prompt,
    snapshot,
    submissionGate
  });
  const state = overviewState({ connectionStatus, ruleState: rulePlan.state, serverFlowState: serverFlowPlan.state, submissionGate, timelineDetail, windowState: windowPlan.state });
  const actionCount = windowPlan.candidateCount;
  const enabledActionCount = windowPlan.enabledCandidateCount;
  const objectCandidateCount = (selectedObjectContext?.promptEnabledCount ?? 0) + (selectedObjectContext?.promptDisabledCount ?? 0);
  const timelineRefCount = timelineDetail?.refs?.filter((ref) => ref.visibility !== "missing").length ?? 0;

  return {
    headline: overviewHeadline(state, windowPlan.stateLabel, serverFlowPlan.primaryLabel),
    metrics: [
      { key: "tick", label: "快照", value: snapshot?.tick == null ? "无" : String(snapshot.tick) },
      { key: "candidates", label: "候选", value: `${enabledActionCount}/${actionCount}` },
      { key: "stack", label: "结算链", value: `${windowPlan.stackCount} 项` },
      { key: "events", label: "事件", value: `${events.length}` }
    ],
    nextStepLabel: overviewNextStep({ ruleNextStep: rulePlan.nextStepLabel, serverFlowNextStep: serverFlowPlan.nextStepLabel, state, windowNextStep: windowPlan.nextStepLabel }),
    rows: [
      {
        count: windowPlan.promptOwnerId ? 1 : 0,
        key: "window",
        label: "行动窗口",
        sourceLabel: windowPlan.responsibilitySource === "server" ? "服务端责任窗口" : "快照兜底",
        state: windowRowState(windowPlan.state),
        stateLabel: windowPlan.stateLabel,
        summary: windowPlan.nextStepLabel,
        value: windowPlan.promptType
      },
      {
        count: actionCount,
        key: "candidates",
        label: "合法候选",
        sourceLabel: windowPlan.candidateCountSource === "server-flow" ? "服务端流程计数" : "服务端候选列表",
        state: candidateRowState({ enabledActionCount, submissionGate, totalCount: actionCount }),
        stateLabel: `${enabledActionCount} 可提交 / ${windowPlan.disabledCandidateCount} 阻断`,
        summary: actionCount > 0 ? "只展示服务端当前行动候选，不在前端推导规则。" : "当前服务端没有公开候选行动。",
        value: `${enabledActionCount}/${actionCount}`
      },
      {
        count: rulePlan.sequence.length,
        key: "rules",
        label: "规则队列",
        sourceLabel: rulePlan.activeLaneKey === "none" ? "服务端空队列" : `服务端 ${rulePlan.activeLaneKey}`,
        state: ruleRowState(rulePlan.state),
        stateLabel: rulePlan.stateLabel,
        summary: rulePlan.nextStepLabel,
        value: rulePlan.activeLaneKey === "none" ? "无活动" : rulePlan.activeLaneKey
      },
      {
        count: objectCandidateCount,
        key: "focus",
        label: "对象焦点",
        sourceLabel: selectedObjectContext ? objectSourceLabel(selectedObjectContext) : "未选择对象",
        state: focusRowState(selectedObjectContext),
        stateLabel: selectedObjectContext ? `${selectedObjectContext.promptEnabledCount} 可用 / ${selectedObjectContext.promptDisabledCount} 阻断` : "未选择",
        summary: selectedObjectContext ? focusSummary(selectedObjectContext) : "点击桌面对象查看区域、候选、事件和结算链关联。",
        value: selectedObjectContext?.zone.label ?? "无"
      },
      {
        count: timelineRefCount,
        key: "timeline",
        label: "详情追踪",
        sourceLabel: timelineDetail?.source === "event" ? "事件详情" : timelineDetail?.source === "rule" ? "规则详情" : "未选择详情",
        state: timelineDetail ? "review" : "empty",
        stateLabel: timelineDetail ? "已选中" : "未选中",
        summary: timelineDetail?.title ?? "从规则队列或日志选择一条事件查看关联对象。",
        value: timelineDetail?.id ?? "无"
      }
    ],
    state,
    stateLabel: overviewStateLabel(state),
    tone: overviewTone(state)
  };
}

function overviewState({
  connectionStatus,
  ruleState,
  serverFlowState,
  submissionGate,
  timelineDetail,
  windowState
}: {
  connectionStatus: ConnectionStatus;
  ruleState: string;
  serverFlowState: string;
  submissionGate?: ServerSubmissionGatePlan;
  timelineDetail?: WireMatchOverviewTimelineDetail;
  windowState: string;
}): WireMatchOverviewState {
  if (!["connected", "resyncing"].includes(connectionStatus)) {
    return "disconnected";
  }

  if (serverFlowState === "blocked" || ruleState === "task-blocked") {
    return "blocked";
  }

  if (serverFlowState === "respond" || serverFlowState === "resolving" || windowState === "resolving") {
    return "resolving";
  }

  if (windowState === "you-action" && submissionGate?.canSubmit) {
    return "ready";
  }

  if (timelineDetail) {
    return "review";
  }

  return "waiting";
}

function overviewHeadline(state: WireMatchOverviewState, windowLabel: string, serverFlowLabel: string): string {
  switch (state) {
    case "blocked":
      return "规则任务阻塞行动";
    case "disconnected":
      return "等待连接恢复";
    case "ready":
      return "可以按服务端候选行动";
    case "review":
      return "正在查看规则或事件详情";
    case "resolving":
      return serverFlowLabel;
    case "waiting":
      return windowLabel;
  }
}

function overviewNextStep({
  ruleNextStep,
  serverFlowNextStep,
  state,
  windowNextStep
}: {
  ruleNextStep: string;
  serverFlowNextStep: string;
  state: WireMatchOverviewState;
  windowNextStep: string;
}): string {
  if (state === "blocked" || state === "resolving") {
    return serverFlowNextStep || ruleNextStep;
  }

  if (state === "review") {
    return "查看详情中的对象引用，必要时回到桌面选择关联对象。";
  }

  return windowNextStep;
}

function overviewStateLabel(state: WireMatchOverviewState): string {
  switch (state) {
    case "blocked":
      return "阻塞";
    case "disconnected":
      return "未连接";
    case "ready":
      return "可行动";
    case "review":
      return "回看";
    case "resolving":
      return "结算中";
    case "waiting":
      return "等待";
  }
}

function overviewTone(state: WireMatchOverviewState): WireWindowPlanTone {
  switch (state) {
    case "blocked":
    case "disconnected":
      return "bad";
    case "ready":
      return "good";
    case "resolving":
      return "warn";
    case "review":
      return "info";
    case "waiting":
      return "neutral";
  }
}

function windowRowState(state: string): WireMatchOverviewRowState {
  if (state === "you-action") {
    return "ready";
  }

  if (state === "resolving") {
    return "warning";
  }

  if (state === "disconnected") {
    return "blocked";
  }

  return "waiting";
}

function candidateRowState({
  enabledActionCount,
  submissionGate,
  totalCount
}: {
  enabledActionCount: number;
  submissionGate?: ServerSubmissionGatePlan;
  totalCount: number;
}): WireMatchOverviewRowState {
  if (totalCount <= 0) {
    return "empty";
  }

  if (!submissionGate?.canSubmit) {
    return "blocked";
  }

  return enabledActionCount > 0 ? "ready" : "warning";
}

function ruleRowState(state: string): WireMatchOverviewRowState {
  if (state === "idle") {
    return "empty";
  }

  if (state === "task-blocked") {
    return "blocked";
  }

  return state === "resolution-history" ? "review" : "warning";
}

function focusRowState(context: TableObjectContext | undefined): WireMatchOverviewRowState {
  if (!context) {
    return "empty";
  }

  if (context.candidateSource === "server") {
    return "server";
  }

  if (context.promptEnabledCount > 0) {
    return "ready";
  }

  return context.promptDisabledCount > 0 || context.eventLinks.length > 0 || context.stackRoles.length > 0 ? "warning" : "waiting";
}

function objectSourceLabel(context: TableObjectContext): string {
  if (context.candidateSource === "server") {
    return "服务端对象上下文";
  }

  if (context.candidateSource === "derived") {
    return "公开候选派生";
  }

  return "快照对象索引";
}

function focusSummary(context: TableObjectContext): string {
  const parts = [
    context.zone.label,
    context.stackRoles.length > 0 ? `结算链 ${context.stackRoles.length}` : "",
    context.eventLinks.length > 0 ? `事件 ${context.eventLinks.length}` : "",
    context.serverRelations.length > 0 ? `服务端关联 ${context.serverRelations.length}` : ""
  ].filter(Boolean);
  return parts.join(" / ") || context.contextBoundary;
}

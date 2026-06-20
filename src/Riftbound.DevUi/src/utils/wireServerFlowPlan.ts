import type { ActionPromptDto, ActionPromptServerFlowDto, ConnectionStatus, GameEvent, SnapshotDto } from "../types/protocol";
import type { CandidateSelectionDraft } from "./candidateSelectionDraft";
import type { ServerSubmissionGatePlan } from "./serverSubmissionGatePlan";
import { buildWireResponseCoachPlan, type WireResponseCoachPlan } from "./wireResponseCoachPlan";
import {
  buildWireRuleQueuePlan,
  type WireRuleQueueLane,
  type WireRuleQueuePlan,
  type WireRuleQueueResponsibilityItem
} from "./wireRuleQueuePlan";

export type WireServerFlowState =
  | "blocked"
  | "history"
  | "ready"
  | "respond"
  | "selecting"
  | "waiting";

export type WireServerFlowTone = "bad" | "good" | "info" | "neutral" | "warn";

export type WireServerFlowMetric = {
  key: string;
  label: string;
  value: string;
};

export type WireServerFlowLane = {
  count: number;
  headline: string;
  key: string;
  label: string;
  state: string;
};

export type WireServerFlowStep = {
  detail: string;
  key: string;
  label: string;
  state: string;
  stateLabel: string;
  value: string;
};

export type WireServerFlowDetail = {
  id: string;
  lines: Array<{ label: string; mine?: boolean; value: string }>;
  refs: Array<{ id: string; label?: string; role: string; visibility?: "hidden" | "missing" | "visible" }>;
  source: "rule";
  subtitle?: string;
  title: string;
};

export type WireServerFlowPlan = {
  detail?: WireServerFlowDetail;
  detailButtonLabel: string;
  lanes: WireServerFlowLane[];
  metrics: WireServerFlowMetric[];
  nextStepLabel: string;
  primaryLabel: string;
  reason: string;
  relatedObjectCount: number;
  relatedObjectIds: string[];
  state: WireServerFlowState;
  stateLabel: string;
  steps: WireServerFlowStep[];
  summary: string;
  tone: WireServerFlowTone;
};

export function buildWireServerFlowPlan({
  connectionStatus,
  events,
  playerId,
  prompt,
  selectionDraft,
  snapshot,
  submissionGate
}: {
  connectionStatus: ConnectionStatus;
  events?: GameEvent[];
  playerId: string;
  prompt?: ActionPromptDto;
  selectionDraft?: CandidateSelectionDraft;
  snapshot?: SnapshotDto;
  submissionGate?: ServerSubmissionGatePlan;
}): WireServerFlowPlan {
  const rulePlan = buildWireRuleQueuePlan({ events, playerId, prompt, snapshot });
  const responsePlan = buildWireResponseCoachPlan({
    connectionStatus,
    playerId,
    prompt,
    selectionDraft,
    snapshot,
    submissionGate
  });
  const detail = rulePlan.focus.detail ? { ...rulePlan.focus.detail, source: "rule" as const } : undefined;
  if (prompt?.serverFlow) {
    return serverBackedFlowPlan(prompt.serverFlow, responsePlan, rulePlan, prompt, snapshot, detail);
  }

  const state = serverFlowState(rulePlan, responsePlan);
  const relatedObjectIds = detailRelatedObjectIds(detail);

  return {
    detail,
    detailButtonLabel: detail ? "打开规则焦点" : "暂无焦点",
    lanes: rulePlan.lanes.map(flowLane),
    metrics: [
      { key: "tick", label: "快照", value: snapshot?.tick == null ? "无" : String(snapshot.tick) },
      { key: "rule", label: "规则", value: rulePlan.stateLabel },
      { key: "action", label: "行动", value: responsePlan.stateLabel },
      { key: "prompt", label: "提示", value: prompt?.view?.title ?? prompt?.view?.type ?? "无" },
      { key: "related", label: "关联", value: String(relatedObjectIds.length) }
    ],
    nextStepLabel: flowNextStep(state, rulePlan, responsePlan),
    primaryLabel: flowPrimaryLabel(state, rulePlan, responsePlan),
    reason: flowReason(state, rulePlan, responsePlan),
    relatedObjectCount: relatedObjectIds.length,
    relatedObjectIds,
    state,
    stateLabel: flowStateLabel(state),
    steps: flowSteps(rulePlan, responsePlan),
    summary: `${rulePlan.stateLabel} / ${responsePlan.stateLabel} / ${flowNextStep(state, rulePlan, responsePlan)}`,
    tone: flowTone(state)
  };
}

function serverBackedFlowPlan(
  serverFlow: ActionPromptServerFlowDto,
  responsePlan: WireResponseCoachPlan,
  rulePlan: WireRuleQueuePlan,
  prompt: ActionPromptDto,
  snapshot: SnapshotDto | undefined,
  detail: WireServerFlowDetail | undefined
): WireServerFlowPlan {
  const state = responsePlan.state === "selecting" ? "selecting" : serverFlowStateFromDto(serverFlow);
  const relatedObjectIds = visibleServerFlowObjectIds(serverFlow.relatedObjectIds);
  const serverFlowDetail = detail ?? serverFlowRelatedObjectsDetail(serverFlow, relatedObjectIds);
  return {
    detail: serverFlowDetail,
    detailButtonLabel: detail ? "打开规则焦点" : serverFlowDetail ? "打开关联对象" : "暂无焦点",
    lanes: serverFlow.lanes.length > 0 ? serverFlow.lanes.map((lane) => ({
      count: lane.count,
      headline: lane.headline,
      key: lane.key,
      label: lane.label,
      state: lane.state
    })) : rulePlan.lanes.map(flowLane),
    metrics: [
      { key: "tick", label: "快照", value: snapshot?.tick == null ? "无" : String(snapshot.tick) },
      { key: "source", label: "来源", value: "服务端" },
      { key: "action", label: "行动", value: responsePlan.stateLabel },
      { key: "prompt", label: "提示", value: prompt.view?.title ?? serverFlow.promptType ?? "无" },
      { key: "related", label: "关联", value: String(relatedObjectIds.length) }
    ],
    nextStepLabel: state === "selecting" ? responsePlan.nextStepLabel : serverFlow.nextStep,
    primaryLabel: state === "selecting" ? responsePlan.primaryLabel : serverFlow.primaryLabel,
    reason: state === "selecting" ? responsePlan.reason : serverFlow.reason,
    relatedObjectCount: relatedObjectIds.length,
    relatedObjectIds,
    state,
    stateLabel: state === "selecting" ? "选择中" : serverFlow.stateLabel,
    steps: serverFlow.steps.map((step) => ({
      detail: step.detail,
      key: `server:${step.key}`,
      label: step.label,
      state: step.state,
      stateLabel: step.stateLabel,
      value: step.value
    })),
    summary: state === "selecting" ? `${serverFlow.summary} / 本地选择中` : serverFlow.summary,
    tone: state === "selecting" ? "info" : serverFlowToneFromDto(serverFlow)
  };
}

function serverFlowRelatedObjectsDetail(
  serverFlow: ActionPromptServerFlowDto,
  relatedObjectIds: string[]
): WireServerFlowDetail | undefined {
  if (relatedObjectIds.length === 0) {
    return undefined;
  }

  return {
    id: `server-flow:${serverFlow.promptType || "UNKNOWN"}:${serverFlow.promptPlayerId || "UNKNOWN"}:related`,
    lines: [
      { label: "提示类型", value: serverFlow.promptType || "无" },
      { label: "窗口状态", value: serverFlow.stateLabel || serverFlow.state || "无" },
      { label: "下一步", value: serverFlow.nextStep || "无" },
      { label: "原因", value: serverFlow.reason || "无" },
      { label: "责任玩家", mine: serverFlow.isResponsiblePlayer, value: serverFlow.responsiblePlayerId || "无" }
    ],
    refs: relatedObjectIds.map((id) => ({ id, role: "服务端关联" })),
    source: "rule",
    subtitle: serverFlow.summary || serverFlow.primaryLabel,
    title: "服务端关联对象"
  };
}

function serverFlowStateFromDto(serverFlow: ActionPromptServerFlowDto): WireServerFlowState {
  switch (serverFlow.state) {
    case "blocked":
      return "blocked";
    case "history":
      return "history";
    case "ready":
      return "ready";
    case "respond":
      return "respond";
    case "waiting":
      return "waiting";
    default:
      return "waiting";
  }
}

function serverFlowToneFromDto(serverFlow: ActionPromptServerFlowDto): WireServerFlowTone {
  switch (serverFlow.tone) {
    case "bad":
      return "bad";
    case "good":
      return "good";
    case "info":
      return "info";
    case "neutral":
      return "neutral";
    case "warn":
      return "warn";
    default:
      return flowTone(serverFlowStateFromDto(serverFlow));
  }
}

function detailRelatedObjectIds(detail?: WireServerFlowDetail): string[] {
  return visibleServerFlowObjectIds(detail?.refs.map((ref) => ref.id) ?? []);
}

function visibleServerFlowObjectIds(ids: readonly string[]): string[] {
  const objectIds: string[] = [];
  const seen = new Set<string>();
  for (const rawId of ids) {
    const objectId = rawId.trim();
    if (!objectId || objectId.toUpperCase() === "HIDDEN" || seen.has(objectId)) {
      continue;
    }

    seen.add(objectId);
    objectIds.push(objectId);
  }

  return objectIds;
}

function serverFlowState(rulePlan: WireRuleQueuePlan, responsePlan: WireResponseCoachPlan): WireServerFlowState {
  if (rulePlan.state === "task-blocked") {
    return "blocked";
  }

  if (rulePlan.state === "stack-response") {
    return "respond";
  }

  if (rulePlan.state === "trigger-pending" || rulePlan.state === "task-open") {
    return "waiting";
  }

  if (responsePlan.state === "ready") {
    return "ready";
  }

  if (responsePlan.state === "selecting") {
    return "selecting";
  }

  if (responsePlan.state === "blocked" || responsePlan.state === "resolving") {
    return "blocked";
  }

  if (rulePlan.state === "resolution-history") {
    return "history";
  }

  return "waiting";
}

function flowLane(lane: WireRuleQueueLane): WireServerFlowLane {
  return {
    count: lane.count,
    headline: lane.headline,
    key: lane.key,
    label: lane.label,
    state: lane.state
  };
}

function flowSteps(rulePlan: WireRuleQueuePlan, responsePlan: WireResponseCoachPlan): WireServerFlowStep[] {
  const responsibilitySteps = rulePlan.responsibility.items.slice(0, 3).map(responsibilityStep);
  if (responsibilitySteps.length > 0) {
    return [
      ...responsibilitySteps,
      responseStep(responsePlan)
    ].slice(0, 4);
  }

  return responsePlan.rows.slice(0, 4).map((row) => ({
    detail: row.detail,
    key: `response:${row.key}`,
    label: row.label,
    state: row.state,
    stateLabel: row.stateLabel,
    value: row.value
  }));
}

function responsibilityStep(item: WireRuleQueueResponsibilityItem): WireServerFlowStep {
  return {
    detail: item.reason,
    key: item.key,
    label: item.label,
    state: item.state,
    stateLabel: item.stateLabel,
    value: `${item.actorLabel} / ${item.actionLabel}`
  };
}

function responseStep(responsePlan: WireResponseCoachPlan): WireServerFlowStep {
  return {
    detail: responsePlan.reason,
    key: "response:next",
    label: "服务端行动",
    state: responsePlan.state,
    stateLabel: responsePlan.stateLabel,
    value: responsePlan.primaryLabel
  };
}

function flowPrimaryLabel(
  state: WireServerFlowState,
  rulePlan: WireRuleQueuePlan,
  responsePlan: WireResponseCoachPlan
): string {
  switch (state) {
    case "blocked":
      return rulePlan.state === "task-blocked" ? "规则任务阻塞" : responsePlan.primaryLabel;
    case "history":
      return "规则事件回看";
    case "ready":
      return responsePlan.primaryLabel;
    case "respond":
      return "响应结算链";
    case "selecting":
      return responsePlan.primaryLabel;
    case "waiting":
      return rulePlan.activeLaneKey === "none" ? responsePlan.primaryLabel : rulePlan.nextStepLabel;
  }
}

function flowNextStep(
  state: WireServerFlowState,
  rulePlan: WireRuleQueuePlan,
  responsePlan: WireResponseCoachPlan
): string {
  switch (state) {
    case "blocked":
      return rulePlan.state === "task-blocked" ? rulePlan.nextStepLabel : responsePlan.nextStepLabel;
    case "history":
      return "选择近期规则事件查看详情";
    case "ready":
    case "selecting":
      return responsePlan.nextStepLabel;
    case "respond":
      return "按服务端 prompt 选择响应或让过";
    case "waiting":
      return rulePlan.activeLaneKey === "none" ? responsePlan.nextStepLabel : rulePlan.nextStepLabel;
  }
}

function flowReason(
  state: WireServerFlowState,
  rulePlan: WireRuleQueuePlan,
  responsePlan: WireResponseCoachPlan
): string {
  switch (state) {
    case "blocked":
      return rulePlan.state === "task-blocked"
        ? rulePlan.responsibility.summary
        : responsePlan.reason;
    case "history":
      return "近期战场、战斗或规则事件可用于核对桌面状态。";
    case "ready":
    case "selecting":
      return responsePlan.reason;
    case "respond":
      return "结算链项目存在时，合法响应和提交字段仍由服务端候选决定。";
    case "waiting":
      return rulePlan.responsibility.summary || responsePlan.reason;
  }
}

function flowStateLabel(state: WireServerFlowState): string {
  switch (state) {
    case "blocked":
      return "阻塞";
    case "history":
      return "回看";
    case "ready":
      return "可提交";
    case "respond":
      return "响应";
    case "selecting":
      return "选择中";
    case "waiting":
      return "等待";
  }
}

function flowTone(state: WireServerFlowState): WireServerFlowTone {
  switch (state) {
    case "blocked":
      return "warn";
    case "history":
      return "info";
    case "ready":
      return "good";
    case "respond":
      return "info";
    case "selecting":
      return "info";
    case "waiting":
      return "neutral";
  }
}

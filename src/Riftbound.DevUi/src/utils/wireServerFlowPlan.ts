import type {
  ActionPromptDto,
  ActionPromptObjectCandidateStepDto,
  ActionPromptServerFlowDto,
  ConnectionStatus,
  GameEvent,
  SnapshotDto
} from "../types/protocol";
import type { CandidateSelectionDraft } from "./candidateSelectionDraft";
import {
  buildPromptInteractionModel,
  promptChoiceRoleLabel,
  type PromptInteractionModel,
  type PromptObjectSummary
} from "./promptInteraction";
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

export type WireServerFlowObjectRef = {
  candidateBoundary?: string;
  candidateRoles?: string[];
  candidateSource?: string;
  candidateStepSummary?: string;
  candidateSteps?: WireServerFlowObjectCandidateStep[];
  disabledCandidateCount?: number;
  enabledCandidateCount?: number;
  id: string;
  label?: string;
  role: string;
  visibility?: "hidden" | "missing" | "visible";
};

export type WireServerFlowObjectCandidateStep = {
  choiceCount: number;
  index: number;
  label: string;
  objectChoiceCount: number;
  required: boolean;
  role: string;
};

export type WireServerFlowDetail = {
  id: string;
  lines: Array<{ label: string; mine?: boolean; value: string }>;
  refs: WireServerFlowObjectRef[];
  source: "rule";
  subtitle?: string;
  title: string;
};

export type WireServerFlowRelatedActionRow = {
  actionRoleLabels: string[];
  disabledCandidateCount: number;
  enabledCandidateCount: number;
  key: string;
  nextStepLabel: string;
  objectId: string;
  serverRoleLabel: string;
  state: "blocked" | "ready" | "unknown";
  stateLabel: string;
  stepSummary: string;
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
  relatedObjectRefs: WireServerFlowDetail["refs"];
  relatedActionRows: WireServerFlowRelatedActionRow[];
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
  const interactionModel = buildPromptInteractionModel(prompt);
  if (prompt?.serverFlow) {
    return serverBackedFlowPlan(prompt.serverFlow, responsePlan, rulePlan, prompt, snapshot, detail, interactionModel);
  }

  const state = serverFlowState(rulePlan, responsePlan);
  const relatedObjectRefs = detailRelatedObjectRefs(detail);
  const relatedObjectIds = visibleServerFlowObjectIds(relatedObjectRefs.map((ref) => ref.id));
  const relatedActionRows = serverFlowRelatedActionRows(relatedObjectRefs, interactionModel);

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
    relatedObjectRefs,
    relatedActionRows,
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
  detail: WireServerFlowDetail | undefined,
  interactionModel: PromptInteractionModel
): WireServerFlowPlan {
  const state = responsePlan.state === "selecting" ? "selecting" : serverFlowStateFromDto(serverFlow);
  const relatedObjectRefs = serverFlowRelatedObjectRefs(serverFlow);
  const relatedObjectIds = visibleServerFlowObjectIds(relatedObjectRefs.map((ref) => ref.id));
  const relatedActionRows = serverFlowRelatedActionRows(relatedObjectRefs, interactionModel);
  const serverFlowDetail = detail ?? serverFlowRelatedObjectsDetail(serverFlow, relatedObjectRefs);
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
    relatedObjectRefs,
    relatedActionRows,
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

function serverFlowRelatedActionRows(
  refs: WireServerFlowDetail["refs"],
  interactionModel: PromptInteractionModel
): WireServerFlowRelatedActionRow[] {
  const groupedRefs = new Map<string, WireServerFlowDetail["refs"]>();
  for (const ref of refs) {
    const objectId = ref.id.trim();
    if (!objectId || objectId.toUpperCase() === "HIDDEN") {
      continue;
    }

    groupedRefs.set(objectId, [...(groupedRefs.get(objectId) ?? []), ref]);
  }

  return [...groupedRefs.entries()].map(([objectId, objectRefs]) => {
    const summary = interactionModel.objectById.get(objectId);
    const actionRoleLabels = serverFlowRelatedActionRoleLabels(objectRefs, summary);
    const enabledCandidateCount = firstFiniteNumber(objectRefs.map((ref) => ref.enabledCandidateCount))
      ?? summary?.enabledCandidateCount
      ?? 0;
    const disabledCandidateCount = firstFiniteNumber(objectRefs.map((ref) => ref.disabledCandidateCount))
      ?? summary?.disabledCandidateCount
      ?? 0;
    const serverRoleLabel = uniqueStrings(objectRefs.map((ref) => ref.role)).join(" / ") || "服务端关联";
    const stepSummary = serverFlowCandidateStepSummary(objectRefs);
    const state: WireServerFlowRelatedActionRow["state"] = enabledCandidateCount > 0
      ? "ready"
      : disabledCandidateCount > 0
        ? "blocked"
        : "unknown";

    return {
      actionRoleLabels,
      disabledCandidateCount,
      enabledCandidateCount,
      key: `server-flow-action:${objectId}`,
      nextStepLabel: serverFlowRelatedActionNextStep(state, actionRoleLabels, enabledCandidateCount, disabledCandidateCount),
      objectId,
      serverRoleLabel,
      state,
      stateLabel: serverFlowRelatedActionStateLabel(state),
      stepSummary
    };
  });
}

function serverFlowCandidateStepSummary(objectRefs: WireServerFlowDetail["refs"]): string {
  const stepsByRole = new Map<string, WireServerFlowObjectCandidateStep>();
  for (const step of objectRefs.flatMap((ref) => ref.candidateSteps ?? [])) {
    const roleKey = step.role.trim() || step.label.trim();
    if (!roleKey) {
      continue;
    }

    const existing = stepsByRole.get(roleKey);
    if (!existing
      || step.required && !existing.required
      || step.objectChoiceCount > existing.objectChoiceCount
      || step.choiceCount > existing.choiceCount) {
      stepsByRole.set(roleKey, step);
    }
  }

  return [...stepsByRole.values()]
    .sort((left, right) => left.index - right.index || left.role.localeCompare(right.role))
    .slice(0, 4)
    .map((step) => `${step.label}${step.required ? "*" : ""} ${step.objectChoiceCount}/${step.choiceCount}`)
    .join(" / ");
}

function serverFlowRelatedActionRoleLabels(
  objectRefs: WireServerFlowDetail["refs"],
  summary: PromptObjectSummary | undefined
): string[] {
  const serverRoles = uniqueStrings(objectRefs.flatMap((ref) => ref.candidateRoles ?? []));
  if (serverRoles.length > 0) {
    return serverRoles;
  }

  return uniqueStrings(summary?.choices.map((choice) => promptChoiceRoleLabel(choice.role)) ?? []);
}

function firstFiniteNumber(values: Array<number | undefined>): number | undefined {
  return values.find((value) => Number.isFinite(value));
}

function serverFlowRelatedActionNextStep(
  state: WireServerFlowRelatedActionRow["state"],
  actionRoleLabels: string[],
  enabledCandidateCount: number,
  disabledCandidateCount: number
): string {
  switch (state) {
    case "ready":
      return `可作为 ${actionRoleLabels.join(" / ") || "候选对象"} 进入 ${enabledCandidateCount} 个候选。`;
    case "blocked":
      return `当前只关联 ${disabledCandidateCount} 个阻断候选，等待服务端开放或换对象。`;
    case "unknown":
      return "服务端声明相关，但当前 prompt 未把它列为可选择对象。";
  }
}

function serverFlowRelatedActionStateLabel(state: WireServerFlowRelatedActionRow["state"]): string {
  switch (state) {
    case "blocked":
      return "仅阻断";
    case "ready":
      return "可进入候选";
    case "unknown":
      return "无候选";
  }
}

function serverFlowRelatedObjectsDetail(
  serverFlow: ActionPromptServerFlowDto,
  relatedObjectRefs: WireServerFlowDetail["refs"]
): WireServerFlowDetail | undefined {
  if (relatedObjectRefs.length === 0) {
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
    refs: relatedObjectRefs,
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

function detailRelatedObjectRefs(detail?: WireServerFlowDetail): WireServerFlowDetail["refs"] {
  return visibleServerFlowObjectRefs(detail?.refs ?? []);
}

function serverFlowRelatedObjectRefs(serverFlow: ActionPromptServerFlowDto): WireServerFlowDetail["refs"] {
  const semanticRefs = visibleServerFlowObjectRefs(
    (serverFlow.relatedObjects ?? []).map((ref) => ({
      candidateBoundary: ref.candidateBoundary ?? undefined,
      candidateRoles: ref.candidateRoles ?? undefined,
      candidateSteps: normalizedServerFlowCandidateSteps(ref.candidateSteps),
      candidateSource: ref.candidateSource ?? undefined,
      disabledCandidateCount: normalizedOptionalCount(ref.disabledCandidateCount),
      enabledCandidateCount: normalizedOptionalCount(ref.enabledCandidateCount),
      id: ref.objectId,
      role: ref.role || "服务端关联"
    }))
  );
  if (semanticRefs.length > 0) {
    return semanticRefs;
  }

  return visibleServerFlowObjectIds(serverFlow.relatedObjectIds)
    .map((id) => ({ id, role: "服务端关联" }));
}

function visibleServerFlowObjectRefs(refs: WireServerFlowDetail["refs"]): WireServerFlowDetail["refs"] {
  const objectRefs: WireServerFlowDetail["refs"] = [];
  const seen = new Set<string>();
  for (const ref of refs) {
    const objectId = ref.id.trim();
    const role = (ref.role || "服务端关联").trim();
    const key = `${role}\u001f${objectId}`;
    if (!objectId || objectId.toUpperCase() === "HIDDEN" || seen.has(key)) {
      continue;
    }

    seen.add(key);
    objectRefs.push(compactServerFlowObjectRef({ ...ref, id: objectId, role }));
  }

  return objectRefs;
}

function compactServerFlowObjectRef(ref: WireServerFlowObjectRef): WireServerFlowObjectRef {
  const compactRef: WireServerFlowObjectRef = {
    id: ref.id,
    role: ref.role
  };
  if (ref.label) {
    compactRef.label = ref.label;
  }

  if (ref.visibility) {
    compactRef.visibility = ref.visibility;
  }

  if (ref.candidateRoles?.length) {
    compactRef.candidateRoles = ref.candidateRoles;
  }

  if (ref.candidateSource) {
    compactRef.candidateSource = ref.candidateSource;
  }

  if (ref.candidateBoundary) {
    compactRef.candidateBoundary = ref.candidateBoundary;
  }

  if (ref.candidateSteps?.length) {
    compactRef.candidateSteps = ref.candidateSteps;
    compactRef.candidateStepSummary = serverFlowCandidateStepSummary([ref]);
  }

  if (Number.isFinite(ref.enabledCandidateCount)) {
    compactRef.enabledCandidateCount = ref.enabledCandidateCount;
  }

  if (Number.isFinite(ref.disabledCandidateCount)) {
    compactRef.disabledCandidateCount = ref.disabledCandidateCount;
  }

  return compactRef;
}

function normalizedOptionalCount(value: number | null | undefined): number | undefined {
  return Number.isFinite(value) ? Number(value) : undefined;
}

function normalizedServerFlowCandidateSteps(
  steps: ActionPromptObjectCandidateStepDto[] | null | undefined
): WireServerFlowObjectCandidateStep[] | undefined {
  const normalized = (steps ?? [])
    .map((step) => ({
      choiceCount: Number.isFinite(step.choiceCount) ? Number(step.choiceCount) : 0,
      index: Number.isFinite(step.index) ? Number(step.index) : 0,
      label: step.label?.trim() || step.role?.trim() || "步骤",
      objectChoiceCount: Number.isFinite(step.objectChoiceCount) ? Number(step.objectChoiceCount) : 0,
      required: Boolean(step.required),
      role: step.role?.trim() || step.label?.trim() || "step"
    }))
    .filter((step) => step.required || step.choiceCount > 0 || step.objectChoiceCount > 0)
    .sort((left, right) => left.index - right.index || left.role.localeCompare(right.role));

  return normalized.length > 0 ? normalized : undefined;
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

function uniqueStrings(values: string[]): string[] {
  return [...new Set(values.map((value) => value.trim()).filter(Boolean))];
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

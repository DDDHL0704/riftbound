import type { ActionPromptDto, ConnectionStatus, SnapshotDto } from "../types/protocol";
import type { CandidateSelectionDraft } from "./candidateSelectionDraft";
import { buildWireActionMapPlan, type WireActionCandidatePlan, type WireActionRoutePlan, type WireActionRouteStepPlan } from "./wireActionMapPlan";
import type { ServerSubmissionGatePlan } from "./serverSubmissionGatePlan";
import { buildWireTurnWindowPlan, type WireWindowPlanTone } from "./wireTurnWindowPlan";

export type WireResponseCoachState =
  | "blocked"
  | "opponent"
  | "ready"
  | "resolving"
  | "selecting"
  | "waiting";

export type WireResponseCoachStepRole =
  | "destination"
  | "mode"
  | "optionalCost"
  | "source"
  | "submit"
  | "sync"
  | "target"
  | "wait"
  | "window";

export type WireResponseCoachRowState =
  | "blocked"
  | "done"
  | "ready"
  | "selecting"
  | "server"
  | "waiting";

export type WireResponseCoachMetric = {
  key: string;
  label: string;
  value: string;
};

export type WireResponseCoachRow = {
  detail: string;
  key: string;
  label: string;
  state: WireResponseCoachRowState;
  stateLabel: string;
  value: string;
};

export type WireResponseCoachPlan = {
  candidateLabel?: string;
  metrics: WireResponseCoachMetric[];
  nextStepLabel: string;
  primaryLabel: string;
  reason: string;
  rows: WireResponseCoachRow[];
  state: WireResponseCoachState;
  stateLabel: string;
  stepRole: WireResponseCoachStepRole;
  summary: string;
  tone: WireWindowPlanTone;
};

export function buildWireResponseCoachPlan({
  connectionStatus,
  playerId,
  prompt,
  selectionDraft,
  snapshot,
  submissionGate
}: {
  connectionStatus: ConnectionStatus;
  playerId: string;
  prompt?: ActionPromptDto;
  selectionDraft?: CandidateSelectionDraft;
  snapshot?: SnapshotDto;
  submissionGate?: ServerSubmissionGatePlan;
}): WireResponseCoachPlan {
  const turnPlan = buildWireTurnWindowPlan({ connectionStatus, playerId, prompt, snapshot });
  const actionPlan = buildWireActionMapPlan({
    playerId,
    prompt,
    selectedObjectId: selectionDraft?.sourceObjectId,
    selectionDraft,
    snapshot,
    submissionGate
  });
  const gate = actionPlan.submissionGate;
  const route = actionPlan.route;
  const activeCandidate = route
    ? undefined
    : actionPlan.candidatePlans.find((candidate) => candidate.enabled)
      ?? actionPlan.candidatePlans[0];
  const decision = coachDecision({
    activeCandidate,
    gate,
    prompt,
    playerId,
    route,
    selectionDraft,
    turnPlan
  });

  return {
    candidateLabel: route?.candidateLabel ?? activeCandidate?.candidateLabel,
    metrics: [
      { key: "window", label: "窗口", value: turnPlan.stateLabel },
      { key: "enabled", label: "可提交", value: String(actionPlan.metrics.find((metric) => metric.key === "enabled")?.value ?? "0") },
      { key: "route", label: "路径", value: route?.stateLabel ?? (activeCandidate ? "未开始" : "无") },
      { key: "draft", label: "草稿", value: selectionDraft ? "已开始" : "无" }
    ],
    nextStepLabel: decision.nextStepLabel,
    primaryLabel: decision.primaryLabel,
    reason: decision.reason,
    rows: [
      row("gate", "提交门禁", gate.canSubmit ? "ready" : "blocked", gate.stateLabel, gate.state, gate.reason),
      row("window", "行动窗口", turnRowState(turnPlan.state), turnPlan.stateLabel, turnPlan.promptType, turnPlan.nextStepLabel),
      row(
        "prompt",
        "服务端提示",
        prompt?.actionable ? "server" : "waiting",
        prompt?.actionable ? "有行动窗口" : "无行动窗口",
        prompt?.view?.title?.trim() || "无",
        prompt?.reason ?? turnPlan.promptTitle
      ),
      row("draft", "当前草稿", selectionDraft ? "selecting" : "waiting", selectionDraft ? "选择中" : "未开始", draftValue(selectionDraft), draftDetail(selectionDraft)),
      row("route", "候选路径", routeRowState(route), route?.stateLabel ?? "未进入路径", route?.candidateLabel ?? activeCandidate?.candidateLabel ?? "无", route?.summary ?? activeCandidateSummary(activeCandidate)),
      row("submit", "下一步", decision.state === "ready" ? "ready" : decision.state === "blocked" ? "blocked" : "selecting", decision.stateLabel, decision.primaryLabel, decision.nextStepLabel)
    ],
    state: decision.state,
    stateLabel: decision.stateLabel,
    stepRole: decision.stepRole,
    summary: decision.summary,
    tone: decision.tone
  };
}

type CoachDecisionOptions = {
  activeCandidate?: WireActionCandidatePlan;
  gate: { canSubmit: boolean; reason: string };
  playerId: string;
  prompt?: ActionPromptDto;
  route?: WireActionRoutePlan;
  selectionDraft?: CandidateSelectionDraft;
  turnPlan: ReturnType<typeof buildWireTurnWindowPlan>;
};

function coachDecision({
  activeCandidate,
  gate,
  playerId,
  prompt,
  route,
  selectionDraft,
  turnPlan
}: CoachDecisionOptions): Pick<WireResponseCoachPlan, "nextStepLabel" | "primaryLabel" | "reason" | "state" | "stateLabel" | "stepRole" | "summary" | "tone"> {
  if (!gate.canSubmit || turnPlan.state === "disconnected") {
    const reason = !gate.canSubmit ? gate.reason : turnPlan.nextStepLabel;
    return decision("blocked", "提交阻断", "等待同步", "sync", reason, reason, "bad");
  }

  if (turnPlan.state === "resolving") {
    return decision("resolving", "规则处理中", "等待结算", "window", turnPlan.nextStepLabel, turnPlan.queueStateLabel, "info");
  }

  if (turnPlan.state === "opponent-action" || prompt?.playerId && prompt.playerId !== playerId) {
    return decision("opponent", "对手窗口", "等待对手", "wait", turnPlan.nextStepLabel, "当前 prompt 不属于本玩家。", "neutral");
  }

  if (!prompt?.actionable) {
    return decision("waiting", "等待窗口", "等待服务端提示", "wait", turnPlan.nextStepLabel, "服务端尚未开放可提交行动。", "neutral");
  }

  if (route) {
    if (route.state === "ready") {
      return decision("ready", "可提交", "提交给服务端", "submit", "可送服务端校验。", route.summary, "good");
    }

    if (route.state === "blocked") {
      return decision("blocked", "路径阻断", "等待服务端", "sync", route.nextStepLabel, route.summary, "warn");
    }

    const missingStep = nextOpenRouteStep(route);
    if (missingStep) {
      return decision(
        "selecting",
        "继续选择",
        `选择${missingStep.label}`,
        missingStep.role,
        route.nextStepLabel,
        route.summary,
        "info"
      );
    }

    return decision("blocked", "路径缺字段", "等待服务端字段", "submit", route.nextStepLabel, route.summary, "warn");
  }

  if (activeCandidate) {
    if (!activeCandidate.enabled) {
      return decision("blocked", "候选阻断", "等待合法窗口", "window", activeCandidate.summary, activeCandidate.summary, "warn");
    }

    const firstStep = nextOpenCandidateStep(activeCandidate);
    if (firstStep) {
      return decision(
        "selecting",
        "选择候选",
        `选择${firstStep.label}`,
        firstStep.role,
        `从服务端候选中选择${firstStep.label}。`,
        activeCandidate.summary,
        "info"
      );
    }

    return decision("ready", "可直接提交", "提交给服务端", "submit", "当前候选不需要额外选择。", activeCandidate.summary, "good");
  }

  if (selectionDraft) {
    return decision("blocked", "草稿失效", "重新选择来源", "source", "当前草稿未匹配服务端候选。", "服务端候选变化后需要重新选择。", "warn");
  }

  return decision("waiting", "无候选", "等待服务端候选", "wait", "当前窗口没有服务端候选。", "等待服务端开放合法操作。", "neutral");
}

function decision(
  state: WireResponseCoachState,
  stateLabel: string,
  primaryLabel: string,
  stepRole: WireResponseCoachStepRole,
  nextStepLabel: string,
  reason: string,
  tone: WireWindowPlanTone
): Pick<WireResponseCoachPlan, "nextStepLabel" | "primaryLabel" | "reason" | "state" | "stateLabel" | "stepRole" | "summary" | "tone"> {
  return {
    nextStepLabel,
    primaryLabel,
    reason,
    state,
    stateLabel,
    stepRole,
    summary: `${stateLabel} / ${primaryLabel} / ${nextStepLabel}`,
    tone
  };
}

function nextOpenRouteStep(route: WireActionRoutePlan): WireActionRouteStepPlan | undefined {
  return route.steps.find((step) => step.required && step.selectedCount <= 0)
    ?? route.steps.find((step) => !step.required && step.totalCount > 0 && step.selectedCount <= 0);
}

function nextOpenCandidateStep(candidate: WireActionCandidatePlan): WireActionCandidatePlan["stepRows"][number] | undefined {
  return candidate.stepRows.find((step) => step.required && step.selectedCount <= 0)
    ?? candidate.stepRows.find((step) => !step.required && step.count > 0 && step.selectedCount <= 0);
}

function row(
  key: string,
  label: string,
  state: WireResponseCoachRowState,
  stateLabel: string,
  value: string,
  detail: string
): WireResponseCoachRow {
  return { detail, key, label, state, stateLabel, value };
}

function turnRowState(state: ReturnType<typeof buildWireTurnWindowPlan>["state"]): WireResponseCoachRowState {
  switch (state) {
    case "you-action":
      return "ready";
    case "opponent-action":
    case "server-wait":
      return "waiting";
    case "resolving":
      return "server";
    case "disconnected":
      return "blocked";
  }
}

function routeRowState(route: WireActionRoutePlan | undefined): WireResponseCoachRowState {
  if (!route) {
    return "waiting";
  }

  switch (route.state) {
    case "ready":
      return "ready";
    case "blocked":
      return "blocked";
    case "incomplete":
      return "selecting";
  }
}

function draftValue(selectionDraft: CandidateSelectionDraft | undefined): string {
  if (!selectionDraft) {
    return "无";
  }

  return [
    selectionDraft.sourceObjectId ? "来源 1" : "来源 0",
    selectionDraft.targetChoiceIds.length ? `目标 ${selectionDraft.targetChoiceIds.length}` : "",
    selectionDraft.destinationId ? "位置 1" : "",
    selectionDraft.mode ? "模式 1" : "",
    selectionDraft.optionalCostIds.length ? `费用 ${selectionDraft.optionalCostIds.length}` : ""
  ].filter(Boolean).join(" / ") || "空草稿";
}

function draftDetail(selectionDraft: CandidateSelectionDraft | undefined): string {
  if (!selectionDraft) {
    return "点击服务端候选对象后开始选择。";
  }

  return selectionDraft.candidateKey;
}

function activeCandidateSummary(candidate: WireActionCandidatePlan | undefined): string {
  if (!candidate) {
    return "服务端没有公开候选。";
  }

  return `${candidate.summary} / ${candidate.enabled ? "可用" : "阻断"}`;
}

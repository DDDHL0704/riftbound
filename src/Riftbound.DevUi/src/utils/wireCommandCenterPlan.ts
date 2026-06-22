import type { CommandSubmissionFollowupPlan, CommandSubmissionFollowupState } from "./commandSubmissionFollowupPlan";
import type { TableObjectContext } from "./tableObjectContext";
import type { WireResponseCoachPlan, WireResponseCoachStepRole } from "./wireResponseCoachPlan";
import type {
  WireFocusedInteractionPlan,
  WireFocusedLegalActionState,
  WireFocusedReadinessTone
} from "./wireFocusedInteractionPlan";

export type WireCommandCenterState =
  | "blocked"
  | "no-focus"
  | "observe"
  | "ready"
  | "selecting";

export type WireCommandCenterRowState =
  | "blocked"
  | "empty"
  | "ready"
  | "server"
  | "selecting"
  | "waiting";

export type WireCommandCenterRow = {
  detail: string;
  key: string;
  label: string;
  state: WireCommandCenterRowState;
  value: string;
};

export type WireCommandCenterLayoutMode =
  | "inspect"
  | "resolve"
  | "select"
  | "submit";

export type WireCommandCenterRowGroupKey =
  | "authority"
  | "focus"
  | "submission";

export type WireCommandCenterRowGroup = {
  key: WireCommandCenterRowGroupKey;
  label: string;
  rows: WireCommandCenterRow[];
  state: WireCommandCenterRowState;
  summary: string;
};

export type WireCommandCenterWorkflowStep = WireCommandCenterRow & {
  active: boolean;
  order: number;
};

export type WireCommandCenterActionRow = {
  action: string;
  commandType?: string;
  key: string;
  label: string;
  nextStepLabel: string;
  roleLabel: string;
  state: WireFocusedLegalActionState;
  stateLabel: string;
};

export type WireCommandCenterPlan = {
  actionRows: WireCommandCenterActionRow[];
  activeStepKey: string;
  canShowFocusedActions: boolean;
  headline: string;
  layoutMode: WireCommandCenterLayoutMode;
  nextStepLabel: string;
  reason: string;
  rowGroups: WireCommandCenterRowGroup[];
  rows: WireCommandCenterRow[];
  state: WireCommandCenterState;
  stateLabel: string;
  stepRole: WireResponseCoachStepRole;
  submissionFollowup: CommandSubmissionFollowupPlan;
  tone: WireFocusedReadinessTone;
  workflowSteps: WireCommandCenterWorkflowStep[];
};

export function buildWireCommandCenterPlan({
  coachPlan,
  focusedPlan,
  objectContext,
  submissionFollowup
}: {
  coachPlan: WireResponseCoachPlan;
  focusedPlan: WireFocusedInteractionPlan;
  objectContext?: TableObjectContext;
  submissionFollowup?: CommandSubmissionFollowupPlan;
}): WireCommandCenterPlan {
  const state = commandCenterState(focusedPlan, coachPlan);
  const followup = submissionFollowup ?? emptySubmissionFollowupPlan();
  const rows = [
    row("window", "窗口", coachPlan.primaryLabel, coachPlan.reason, coachRowState(coachPlan.state)),
    row("focus", "焦点", focusValue(focusedPlan), focusDetail(focusedPlan, objectContext), focusedPlan.sourceObjectId ? "server" : "empty"),
    row("candidate", "候选", `${focusedPlan.readiness.enabledCount} 可用 / ${focusedPlan.readiness.blockedCount} 阻断`, focusedPlan.readiness.stateLabel, candidateRowState(focusedPlan)),
    row("command", "命令", focusedPlan.readiness.commandType ?? coachPlan.candidateLabel ?? "无", focusedPlan.readiness.nextStepLabel, commandRowState(state)),
    row("submit", "提交", focusedPlan.submissionGate.stateLabel, focusedPlan.submissionGate.reason, focusedPlan.submissionGate.canSubmit ? "ready" : "blocked"),
    row("feedback", "回执", submissionFollowupStateLabel(followup.state), followup.summary, submissionFollowupRowState(followup.state))
  ];
  const activeStepKey = activeStepFor(state, rows);
  const actionRows = focusedPlan.legalActionRows.slice(0, 4).map((row): WireCommandCenterActionRow => ({
    action: row.action,
    commandType: row.commandType,
    key: row.key,
    label: row.label,
    nextStepLabel: row.nextStepLabel,
    roleLabel: row.roleLabels.length > 0 ? row.roleLabels.join(" / ") : "无角色",
    state: row.state,
    stateLabel: row.stateLabel
  }));

  return {
    activeStepKey,
    actionRows,
    canShowFocusedActions: Boolean(focusedPlan.sourceObjectId) && focusedPlan.actionEntries.length > 0,
    headline: headlineFor(state, focusedPlan, coachPlan),
    layoutMode: layoutModeFor(state),
    nextStepLabel: nextStepFor(state, focusedPlan, coachPlan),
    reason: reasonFor(state, focusedPlan, coachPlan),
    rowGroups: rowGroupsFor(rows),
    rows,
    state,
    stateLabel: stateLabelFor(state),
    stepRole: coachPlan.stepRole,
    submissionFollowup: followup,
    tone: toneFor(state),
    workflowSteps: workflowStepsFor(rows, activeStepKey)
  };
}

function emptySubmissionFollowupPlan(): CommandSubmissionFollowupPlan {
  return {
    bridge: {
      headline: "等待提交",
      nextStepLabel: "先提交服务端候选路线。",
      rows: [],
      serverStateLabel: "无",
      state: "empty",
      stateLabel: "未提交",
      summary: "尚未提交命令。"
    },
    events: [],
    hiddenEventCount: 0,
    metrics: [],
    serverEventKinds: [],
    serverFollowupState: "none",
    serverFollowupStateLabel: "无",
    sourceRows: [],
    state: "empty",
    summary: "尚未提交命令。"
  };
}

function submissionFollowupStateLabel(state: CommandSubmissionFollowupState | undefined): string {
  switch (state) {
    case "accepted-awaiting":
      return "等待事件/快照";
    case "accepted-events":
      return "已有后续事件";
    case "accepted-silent":
      return "静默接受";
    case "accepted-snapshot":
      return "快照已追上";
    case "failed":
      return "提交失败";
    case "pending":
      return "提交中";
    case "unknown-tick":
      return "回执缺 tick";
    case "empty":
    case undefined:
      return "尚未提交";
  }
}

function submissionFollowupRowState(state: CommandSubmissionFollowupState | undefined): WireCommandCenterRowState {
  switch (state) {
    case "accepted-events":
    case "accepted-silent":
    case "accepted-snapshot":
      return "ready";
    case "failed":
      return "blocked";
    case "accepted-awaiting":
    case "pending":
    case "unknown-tick":
      return "waiting";
    case "empty":
    case undefined:
      return "empty";
  }
}

function commandCenterState(
  focusedPlan: WireFocusedInteractionPlan,
  coachPlan: WireResponseCoachPlan
): WireCommandCenterState {
  if (!focusedPlan.submissionGate.canSubmit || coachPlan.state === "blocked") {
    return "blocked";
  }

  if (focusedPlan.readiness.state === "ready") {
    return "ready";
  }

  if (focusedPlan.readiness.state === "needs-selection") {
    return "selecting";
  }

  if (!focusedPlan.sourceObjectId) {
    return "no-focus";
  }

  if (focusedPlan.readiness.state === "server-blocked" || !focusedPlan.windowGate.canAct) {
    return focusedPlan.actionEntries.length > 0 ? "blocked" : "observe";
  }

  return "observe";
}

function headlineFor(
  state: WireCommandCenterState,
  focusedPlan: WireFocusedInteractionPlan,
  coachPlan: WireResponseCoachPlan
): string {
  if (state === "no-focus") {
    return coachPlan.primaryLabel;
  }

  const objectLabel = focusedPlan.sourceObject.objectIdLabel;
  return `${objectLabel} / ${focusedPlan.readiness.stateLabel}`;
}

function nextStepFor(
  state: WireCommandCenterState,
  focusedPlan: WireFocusedInteractionPlan,
  coachPlan: WireResponseCoachPlan
): string {
  if (state === "no-focus") {
    return coachPlan.nextStepLabel;
  }

  return focusedPlan.readiness.nextStepLabel;
}

function reasonFor(
  state: WireCommandCenterState,
  focusedPlan: WireFocusedInteractionPlan,
  coachPlan: WireResponseCoachPlan
): string {
  if (state === "no-focus") {
    return coachPlan.reason;
  }

  if (!focusedPlan.windowGate.canAct) {
    return focusedPlan.windowGate.reason;
  }

  return focusedPlan.submissionGate.canSubmit ? coachPlan.summary : focusedPlan.submissionGate.reason;
}

function focusValue(focusedPlan: WireFocusedInteractionPlan): string {
  return focusedPlan.sourceObjectId ? focusedPlan.sourceObject.objectIdLabel : "未选择";
}

function focusDetail(
  focusedPlan: WireFocusedInteractionPlan,
  objectContext: TableObjectContext | undefined
): string {
  if (!focusedPlan.sourceObjectId) {
    return "点击桌面卡牌建立焦点。";
  }

  const zone = objectContext?.zone.label ?? "未定位区域";
  return `${zone} / ${focusedPlan.sourceObject.serverCandidateLabel}`;
}

function coachRowState(state: WireResponseCoachPlan["state"]): WireCommandCenterRowState {
  switch (state) {
    case "blocked":
      return "blocked";
    case "ready":
      return "ready";
    case "selecting":
      return "selecting";
    case "opponent":
    case "resolving":
    case "waiting":
      return "waiting";
  }
}

function candidateRowState(focusedPlan: WireFocusedInteractionPlan): WireCommandCenterRowState {
  switch (focusedPlan.readiness.state) {
    case "ready":
      return "ready";
    case "needs-selection":
      return "selecting";
    case "server-blocked":
    case "submission-gate-blocked":
    case "window-blocked":
      return "blocked";
    case "no-focus":
      return "empty";
    case "not-candidate":
      return "waiting";
  }
}

function commandRowState(state: WireCommandCenterState): WireCommandCenterRowState {
  switch (state) {
    case "ready":
      return "ready";
    case "selecting":
      return "selecting";
    case "blocked":
      return "blocked";
    case "no-focus":
      return "empty";
    case "observe":
      return "waiting";
  }
}

function stateLabelFor(state: WireCommandCenterState): string {
  switch (state) {
    case "blocked":
      return "阻断";
    case "no-focus":
      return "待选焦点";
    case "observe":
      return "观察";
    case "ready":
      return "可提交";
    case "selecting":
      return "待选择";
  }
}

function toneFor(state: WireCommandCenterState): WireFocusedReadinessTone {
  switch (state) {
    case "ready":
      return "good";
    case "blocked":
    case "selecting":
      return "warn";
    case "no-focus":
    case "observe":
      return "neutral";
  }
}

function row(
  key: string,
  label: string,
  value: string,
  detail: string,
  state: WireCommandCenterRowState
): WireCommandCenterRow {
  return { detail, key, label, state, value };
}

function activeStepFor(
  state: WireCommandCenterState,
  rows: readonly WireCommandCenterRow[]
): string {
  if (state === "blocked") {
    return rows.find((item) => item.state === "blocked")?.key ?? "submit";
  }

  switch (state) {
    case "no-focus":
      return "focus";
    case "observe":
      return "command";
    case "ready":
      return "submit";
    case "selecting":
      return "candidate";
  }
}

function layoutModeFor(state: WireCommandCenterState): WireCommandCenterLayoutMode {
  switch (state) {
    case "blocked":
      return "resolve";
    case "no-focus":
    case "observe":
      return "inspect";
    case "ready":
      return "submit";
    case "selecting":
      return "select";
  }
}

function workflowStepsFor(
  rows: readonly WireCommandCenterRow[],
  activeStepKey: string
): WireCommandCenterWorkflowStep[] {
  return rows.map((item, index) => ({
    ...item,
    active: item.key === activeStepKey,
    order: index + 1
  }));
}

function rowGroupsFor(rows: readonly WireCommandCenterRow[]): WireCommandCenterRowGroup[] {
  return [
    rowGroup("authority", "窗口与权威", rows, ["window", "submit"]),
    rowGroup("focus", "焦点与候选", rows, ["focus", "candidate", "command"]),
    rowGroup("submission", "提交与回执", rows, ["feedback"])
  ];
}

function rowGroup(
  key: WireCommandCenterRowGroupKey,
  label: string,
  rows: readonly WireCommandCenterRow[],
  rowKeys: readonly string[]
): WireCommandCenterRowGroup {
  const groupRows = rowKeys
    .map((rowKey) => rows.find((item) => item.key === rowKey))
    .filter((item): item is WireCommandCenterRow => Boolean(item));
  const state = groupState(groupRows);
  return {
    key,
    label,
    rows: groupRows,
    state,
    summary: groupRows.map((item) => `${item.label} ${item.value}`).join(" / ")
  };
}

function groupState(rows: readonly WireCommandCenterRow[]): WireCommandCenterRowState {
  return rows.find((item) => item.state === "blocked")?.state
    ?? rows.find((item) => item.state === "selecting")?.state
    ?? rows.find((item) => item.state === "ready")?.state
    ?? rows.find((item) => item.state === "server")?.state
    ?? rows.find((item) => item.state === "waiting")?.state
    ?? "empty";
}

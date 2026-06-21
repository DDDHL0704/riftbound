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
  canShowFocusedActions: boolean;
  headline: string;
  nextStepLabel: string;
  reason: string;
  rows: WireCommandCenterRow[];
  state: WireCommandCenterState;
  stateLabel: string;
  stepRole: WireResponseCoachStepRole;
  tone: WireFocusedReadinessTone;
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
    actionRows,
    canShowFocusedActions: Boolean(focusedPlan.sourceObjectId) && focusedPlan.actionEntries.length > 0,
    headline: headlineFor(state, focusedPlan, coachPlan),
    nextStepLabel: nextStepFor(state, focusedPlan, coachPlan),
    reason: reasonFor(state, focusedPlan, coachPlan),
    rows: [
      row("window", "窗口", coachPlan.primaryLabel, coachPlan.reason, coachRowState(coachPlan.state)),
      row("focus", "焦点", focusValue(focusedPlan), focusDetail(focusedPlan, objectContext), focusedPlan.sourceObjectId ? "server" : "empty"),
      row("candidate", "候选", `${focusedPlan.readiness.enabledCount} 可用 / ${focusedPlan.readiness.blockedCount} 阻断`, focusedPlan.readiness.stateLabel, candidateRowState(focusedPlan)),
      row("command", "命令", focusedPlan.readiness.commandType ?? coachPlan.candidateLabel ?? "无", focusedPlan.readiness.nextStepLabel, commandRowState(state)),
      row("submit", "提交", focusedPlan.submissionGate.stateLabel, focusedPlan.submissionGate.reason, focusedPlan.submissionGate.canSubmit ? "ready" : "blocked"),
      row("feedback", "回执", submissionFollowupStateLabel(submissionFollowup?.state), submissionFollowup?.summary ?? "尚未提交命令。", submissionFollowupRowState(submissionFollowup?.state))
    ],
    state,
    stateLabel: stateLabelFor(state),
    stepRole: coachPlan.stepRole,
    tone: toneFor(state)
  };
}

function submissionFollowupStateLabel(state: CommandSubmissionFollowupState | undefined): string {
  switch (state) {
    case "accepted-awaiting":
      return "等待事件/快照";
    case "accepted-events":
      return "已有后续事件";
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

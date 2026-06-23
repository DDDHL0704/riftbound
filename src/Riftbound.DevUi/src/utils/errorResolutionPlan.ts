import type { ConnectionStatus, ErrorDto } from "../types/protocol";
import { errorCodeLabel, errorMessageLabel } from "./errors";

export type ErrorResolutionState = "authority" | "clear" | "connection" | "input" | "server" | "sync";

export type ErrorResolutionActionId = "connect" | "openDecks" | "resync" | "reviewPrompt" | "waitServer";

export type ErrorResolutionActionState = "disabled" | "primary" | "secondary";

export type ErrorResolutionAction = {
  disabled: boolean;
  id: ErrorResolutionActionId;
  label: string;
  state: ErrorResolutionActionState;
  title: string;
};

export type ErrorResolutionEvidenceRow = {
  label: string;
  value: string;
};

export type ErrorResolutionPlan = {
  actions: ErrorResolutionAction[];
  detail: string;
  evidenceRows: ErrorResolutionEvidenceRow[];
  headline: string;
  nextStep: string;
  state: ErrorResolutionState;
  statusLabel: string;
  surface: "match" | "room";
  tone: "bad" | "good" | "info" | "neutral" | "warn";
};

export type LastCommandSubmissionLike = {
  cmdType?: string | null;
  errorCode?: string | null;
  message?: string | null;
  receiptState?: string | null;
  serverTick?: number | null;
  snapshotTick?: number | null;
  state?: string | null;
  stateLabel?: string | null;
};

type ErrorResolutionIssue = {
  code?: string;
  cmdType?: string;
  message: string;
  receiptState?: string;
  serverTick?: number;
  snapshotTick?: number;
  source: "command" | "server" | "none";
};

export function buildErrorResolutionPlan({
  connectionStatus,
  errors,
  hasSnapshot,
  lastCommandSubmission,
  surface
}: {
  connectionStatus: ConnectionStatus;
  errors: readonly ErrorDto[];
  hasSnapshot: boolean;
  lastCommandSubmission?: LastCommandSubmissionLike;
  surface: "match" | "room";
}): ErrorResolutionPlan {
  const issue = latestIssue(errors, lastCommandSubmission);
  const state = classifyState(issue, connectionStatus);
  const copy = copyForState(state, issue);

  return {
    actions: actionsForState(state, connectionStatus, hasSnapshot),
    detail: copy.detail,
    evidenceRows: evidenceRows(issue, connectionStatus),
    headline: copy.headline,
    nextStep: copy.nextStep,
    state,
    statusLabel: statusLabel(state),
    surface,
    tone: toneForState(state)
  };
}

function latestIssue(
  errors: readonly ErrorDto[],
  lastCommandSubmission?: LastCommandSubmissionLike
): ErrorResolutionIssue {
  if (lastCommandSubmission?.state === "failed") {
    return {
      code: lastCommandSubmission.errorCode ?? undefined,
      cmdType: lastCommandSubmission.cmdType ?? undefined,
      message: lastCommandSubmission.message?.trim() || "服务端拒绝了这次提交。",
      receiptState: lastCommandSubmission.receiptState ?? undefined,
      serverTick: lastCommandSubmission.serverTick ?? undefined,
      snapshotTick: lastCommandSubmission.snapshotTick ?? undefined,
      source: "command"
    };
  }

  const latestError = errors[0];
  if (latestError) {
    return {
      code: latestError.code,
      message: errorMessageLabel(latestError),
      source: "server"
    };
  }

  return {
    message: "当前没有服务端错误或失败回执。",
    source: "none"
  };
}

function classifyState(issue: ErrorResolutionIssue, connectionStatus: ConnectionStatus): ErrorResolutionState {
  if (issue.source === "none") {
    return "clear";
  }

  if (connectionStatus === "disconnected" || connectionStatus === "error" || issue.code === "INVALID_RECONNECT_TOKEN") {
    return "connection";
  }

  switch (issue.code) {
    case "INVALID_DECK":
    case "INVALID_PAYLOAD":
    case "PLAYER_ID_REQUIRED":
      return "input";
    case "PROMPT_EXPIRED":
    case "CLIENT_INTENT_CONFLICT":
    case "RECOVERY_INCONSISTENT":
      return "sync";
    case "CARD_NOT_IN_HAND":
    case "INSUFFICIENT_COST":
    case "INVALID_TARGET":
    case "MATCH_NOT_STARTED":
    case "PHASE_NOT_ALLOWED":
    case "PLAYER_NOT_IN_ROOM":
    case "UNSUPPORTED_COMMAND":
      return "authority";
    default:
      return "server";
  }
}

function copyForState(
  state: ErrorResolutionState,
  issue: ErrorResolutionIssue
): Pick<ErrorResolutionPlan, "detail" | "headline" | "nextStep"> {
  switch (state) {
    case "authority":
      return {
        detail: issue.message,
        headline: issue.code ? errorCodeLabel(issue.code) : "服务端拒绝行动",
        nextStep: nextStepForAuthority(issue.code)
      };
    case "clear":
      return {
        detail: issue.message,
        headline: "无阻断错误",
        nextStep: "继续按服务端提示、候选行动和房间流程操作。"
      };
    case "connection":
      return {
        detail: issue.message,
        headline: issue.code ? errorCodeLabel(issue.code) : "连接需要处理",
        nextStep: "重新连接并入座，服务端会发布新的房间会话和快照。"
      };
    case "input":
      return {
        detail: issue.message,
        headline: issue.code ? errorCodeLabel(issue.code) : "提交内容需要修正",
        nextStep: issue.code === "INVALID_DECK"
          ? "回到构筑/导入页修正卡组，然后重新提交。"
          : "按服务端要求补齐输入，再重新提交。"
      };
    case "server":
      return {
        detail: issue.message,
        headline: issue.code ? errorCodeLabel(issue.code) : "服务端错误",
        nextStep: "保留当前状态，重新同步快照；如果仍失败，再重新连接。"
      };
    case "sync":
      return {
        detail: issue.message,
        headline: issue.code ? errorCodeLabel(issue.code) : "快照需要同步",
        nextStep: "同步服务端权威快照，放弃旧 promptId/snapshotTick 后重新选择行动。"
      };
  }
}

function nextStepForAuthority(code?: string): string {
  switch (code) {
    case "INVALID_TARGET":
      return "按最新服务端候选重新选择目标，不沿用本地推断。";
    case "INSUFFICIENT_COST":
      return "查看服务端资源候选，先横置或回收符文后再提交费用行动。";
    case "MATCH_NOT_STARTED":
    case "PLAYER_NOT_IN_ROOM":
      return "返回房间完成入座、构筑提交与准备流程。";
    case "PHASE_NOT_ALLOWED":
      return "等待当前服务端窗口、结算链或响应流程推进后再提交。";
    default:
      return "按最新服务端行动提示重新选择，不从前端自行推断合法性。";
  }
}

function actionsForState(
  state: ErrorResolutionState,
  connectionStatus: ConnectionStatus,
  hasSnapshot: boolean
): ErrorResolutionAction[] {
  return [
    connectAction(state, connectionStatus),
    resyncAction(state, connectionStatus, hasSnapshot),
    openDecksAction(state),
    reviewPromptAction(state),
    waitServerAction(state)
  ];
}

function connectAction(state: ErrorResolutionState, connectionStatus: ConnectionStatus): ErrorResolutionAction {
  const enabled = state === "connection" || connectionStatus === "idle" || connectionStatus === "disconnected" || connectionStatus === "error";

  return {
    disabled: !enabled,
    id: "connect",
    label: "连接",
    state: state === "connection" ? "primary" : enabled ? "secondary" : "disabled",
    title: enabled ? "重新连接服务端并入座" : "当前连接状态不需要重新入座"
  };
}

function resyncAction(
  state: ErrorResolutionState,
  connectionStatus: ConnectionStatus,
  hasSnapshot: boolean
): ErrorResolutionAction {
  const enabled = connectionStatus === "connected" && hasSnapshot;

  return {
    disabled: !enabled,
    id: "resync",
    label: "同步",
    state: state === "sync" || state === "server" ? "primary" : enabled ? "secondary" : "disabled",
    title: enabled ? "请求服务端重新发送权威快照" : "连接并收到首个快照后才能同步"
  };
}

function openDecksAction(state: ErrorResolutionState): ErrorResolutionAction {
  const enabled = state === "input";

  return {
    disabled: !enabled,
    id: "openDecks",
    label: "构筑",
    state: enabled ? "primary" : "disabled",
    title: enabled ? "打开构筑/导入页修正提交内容" : "当前错误不需要打开构筑页"
  };
}

function reviewPromptAction(state: ErrorResolutionState): ErrorResolutionAction {
  const enabled = state === "authority" || state === "clear";

  return {
    disabled: !enabled,
    id: "reviewPrompt",
    label: "提示",
    state: state === "authority" ? "primary" : enabled ? "secondary" : "disabled",
    title: enabled ? "查看当前服务端行动提示与候选" : "同步或修正输入后再查看提示"
  };
}

function waitServerAction(state: ErrorResolutionState): ErrorResolutionAction {
  const enabled = state === "server";

  return {
    disabled: !enabled,
    id: "waitServer",
    label: "等待",
    state: enabled ? "primary" : "disabled",
    title: enabled ? "保留当前页面，等待服务端或下一条事件" : "当前不需要等待服务端错误恢复"
  };
}

function evidenceRows(issue: ErrorResolutionIssue, connectionStatus: ConnectionStatus): ErrorResolutionEvidenceRow[] {
  const rows: ErrorResolutionEvidenceRow[] = [
    { label: "连接状态", value: connectionStatus },
    { label: "错误来源", value: issue.source === "none" ? "无" : issue.source === "command" ? "提交回执" : "服务端错误" }
  ];

  if (issue.code) {
    rows.push({ label: "错误码", value: issue.code });
  }

  if (issue.cmdType) {
    rows.push({ label: "提交命令", value: issue.cmdType });
  }

  if (issue.snapshotTick != null) {
    rows.push({ label: "提交快照", value: String(issue.snapshotTick) });
  }

  if (issue.serverTick != null) {
    rows.push({ label: "服务端 tick", value: String(issue.serverTick) });
  }

  if (issue.receiptState) {
    rows.push({ label: "回执状态", value: issue.receiptState });
  }

  return rows;
}

function statusLabel(state: ErrorResolutionState): string {
  switch (state) {
    case "authority":
      return "需按提示重选";
    case "clear":
      return "无错误";
    case "connection":
      return "需重连";
    case "input":
      return "需修正输入";
    case "server":
      return "需同步确认";
    case "sync":
      return "需同步";
  }
}

function toneForState(state: ErrorResolutionState): ErrorResolutionPlan["tone"] {
  switch (state) {
    case "clear":
      return "good";
    case "authority":
    case "input":
    case "sync":
      return "warn";
    case "connection":
    case "server":
      return "bad";
  }
}

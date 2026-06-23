import type { ConnectionStatus } from "../types/protocol";
import type { ErrorResolutionState } from "./errorResolutionPlan";
import { connectionStatusLabel } from "./formatters";
import type { RoomSetupGate } from "./roomSetupFlowPlan";
import type { ServerQuickActionState } from "./serverQuickActionPlan";

export type RoomWorkflowRegionId = "actions" | "errors" | "log" | "recovery" | "setup" | "submission";

export type RoomWorkflowRegionState = "blocking" | "clear" | "ready" | "waiting";

export type RoomWorkflowSource =
  | "server-connection"
  | "server-events"
  | "server-prompt"
  | "server-receipt"
  | "server-snapshot";

export type RoomWorkflowQuickActionLike = {
  id: string;
  state?: ServerQuickActionState | string;
};

export type RoomWorkflowRegion = {
  detail: string;
  id: RoomWorkflowRegionId;
  label: string;
  nextStep: string;
  source: RoomWorkflowSource;
  state: RoomWorkflowRegionState;
  value: string;
};

export type RoomWorkflowSurfacePlan = {
  activeRegionId: RoomWorkflowRegionId;
  sections: RoomWorkflowRegion[];
  summary: string;
};

export function buildRoomWorkflowSurfacePlan({
  connectionStatus,
  errorCount,
  errorState,
  eventCount,
  hasSnapshot,
  quickActions,
  roomStatus,
  setupGate,
  submissionState
}: {
  connectionStatus: ConnectionStatus;
  errorCount: number;
  errorState: ErrorResolutionState;
  eventCount: number;
  hasSnapshot: boolean;
  quickActions: readonly RoomWorkflowQuickActionLike[];
  roomStatus: string;
  setupGate: RoomSetupGate;
  submissionState?: string;
}): RoomWorkflowSurfacePlan {
  const readyQuickActions = quickActions.filter((action) => action.state === "ready").length;
  const sections: RoomWorkflowRegion[] = [
    recoveryRegion(connectionStatus, hasSnapshot),
    setupRegion(setupGate, roomStatus),
    actionsRegion(readyQuickActions, quickActions.length),
    submissionRegion(submissionState),
    errorsRegion(errorState, errorCount),
    logRegion(eventCount, errorCount)
  ];

  return {
    activeRegionId: activeRegion(sections),
    sections,
    summary: `连接：${connectionStatusLabel(connectionStatus)} / 开局：${setupGate.label} / 行动：${readyQuickActions}/${quickActions.length}`
  };
}

function recoveryRegion(connectionStatus: ConnectionStatus, hasSnapshot: boolean): RoomWorkflowRegion {
  const connected = connectionStatus === "connected";
  return {
    detail: hasSnapshot ? "已收到服务端房间快照。" : "尚无服务端房间快照。",
    id: "recovery",
    label: "恢复",
    nextStep: connected ? "必要时重新同步快照。" : "连接并入座，等待服务端发布快照。",
    source: "server-connection",
    state: connected ? "ready" : "blocking",
    value: connectionStatusLabel(connectionStatus)
  };
}

function setupRegion(setupGate: RoomSetupGate, roomStatus: string): RoomWorkflowRegion {
  return {
    detail: setupGate.reason,
    id: "setup",
    label: "开局",
    nextStep: setupGate.nextStep,
    source: "server-snapshot",
    state: stateForGate(setupGate),
    value: roomStatus || setupGate.label
  };
}

function actionsRegion(readyQuickActions: number, quickActionCount: number): RoomWorkflowRegion {
  const hasActions = quickActionCount > 0;
  return {
    detail: hasActions ? "快捷行动只来自当前服务端 prompt 候选。" : "当前没有服务端候选行动。",
    id: "actions",
    label: "行动",
    nextStep: readyQuickActions > 0 ? "可提交的快捷行动会携带 prompt/tick 身份。" : "等待服务端开放房间行动候选。",
    source: "server-prompt",
    state: readyQuickActions > 0 ? "ready" : "waiting",
    value: `${readyQuickActions}/${quickActionCount}`
  };
}

function submissionRegion(submissionState?: string): RoomWorkflowRegion {
  const state = submissionState === "failed" ? "blocking" : submissionState === "sent" ? "ready" : "waiting";
  const value = submissionState ? submissionStateLabel(submissionState) : "未提交";
  return {
    detail: submissionState ? "最近一次提交已有服务端回执。" : "尚无房间页提交回执。",
    id: "submission",
    label: "回执",
    nextStep: submissionState === "failed" ? "按服务端拒绝原因修正后重试。" : "查看服务端接受、拒绝或失败原因。",
    source: "server-receipt",
    state,
    value
  };
}

function errorsRegion(errorState: ErrorResolutionState, errorCount: number): RoomWorkflowRegion {
  const clear = errorState === "clear" && errorCount === 0;
  return {
    detail: clear ? "当前没有服务端错误或失败回执。" : "错误处理面板会给出下一步操作。",
    id: "errors",
    label: "错误",
    nextStep: clear ? "继续按服务端提示操作。" : "先处理错误，再继续提交行动。",
    source: "server-events",
    state: clear ? "clear" : "blocking",
    value: clear ? "无阻断" : `${errorCount} 个`
  };
}

function logRegion(eventCount: number, errorCount: number): RoomWorkflowRegion {
  return {
    detail: "只展示服务端公开事件、错误和系统消息。",
    id: "log",
    label: "日志",
    nextStep: "用日志核对服务端事件顺序，不从前端推断规则。",
    source: "server-events",
    state: errorCount > 0 ? "blocking" : eventCount > 0 ? "ready" : "clear",
    value: `${eventCount} 事件 / ${errorCount} 错误`
  };
}

function activeRegion(sections: readonly RoomWorkflowRegion[]): RoomWorkflowRegionId {
  const recovery = sections.find((section) => section.id === "recovery");
  if (recovery?.state === "blocking") {
    return "recovery";
  }

  const errors = sections.find((section) => section.id === "errors");
  if (errors?.state === "blocking") {
    return "errors";
  }

  const submission = sections.find((section) => section.id === "submission");
  if (submission?.state === "blocking") {
    return "submission";
  }

  const actions = sections.find((section) => section.id === "actions");
  if (actions?.state === "ready") {
    return "actions";
  }

  const setup = sections.find((section) => section.id === "setup");
  if (setup?.state !== "ready") {
    return "setup";
  }

  return "log";
}

function stateForGate(setupGate: RoomSetupGate): RoomWorkflowRegionState {
  switch (setupGate.tone) {
    case "bad":
      return "blocking";
    case "good":
      return "ready";
    case "info":
    case "neutral":
    case "warn":
      return "waiting";
  }
}

function submissionStateLabel(state: string): string {
  switch (state) {
    case "failed":
      return "失败";
    case "sent":
      return "已接受";
    case "submitting":
      return "提交中";
    default:
      return state;
  }
}

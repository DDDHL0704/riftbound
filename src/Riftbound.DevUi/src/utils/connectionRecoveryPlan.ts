import type { ConnectionStatus } from "../types/protocol";
import { connectionStatusLabel } from "./formatters";

export type ConnectionRecoveryState = "connecting" | "error" | "offline" | "online" | "recovering" | "resyncing" | "stale";

export type ConnectionRecoveryActionId = "connect" | "disconnect" | "resync";

export type ConnectionRecoveryActionState = "disabled" | "primary" | "secondary";

export type ConnectionRecoveryAction = {
  disabled: boolean;
  id: ConnectionRecoveryActionId;
  label: string;
  state: ConnectionRecoveryActionState;
  title: string;
};

export type ConnectionRecoveryPlan = {
  actions: ConnectionRecoveryAction[];
  detail: string;
  headline: string;
  nextStep: string;
  state: ConnectionRecoveryState;
  statusLabel: string;
  tickLabel: string;
};

export function buildConnectionRecoveryPlan({
  connectionStatus,
  hasSnapshot,
  lastSystemMessage,
  promptSnapshotTick,
  snapshotTick
}: {
  connectionStatus: ConnectionStatus;
  hasSnapshot: boolean;
  lastSystemMessage?: string | null;
  promptSnapshotTick?: number | null;
  snapshotTick?: number | null;
}): ConnectionRecoveryPlan {
  const state = recoveryState(connectionStatus, promptSnapshotTick, snapshotTick);
  const copy = copyForState(state, lastSystemMessage);

  return {
    actions: actionsForState(state, hasSnapshot),
    detail: copy.detail,
    headline: copy.headline,
    nextStep: copy.nextStep,
    state,
    statusLabel: connectionStatusLabel(connectionStatus),
    tickLabel: tickLabel(snapshotTick, promptSnapshotTick)
  };
}

function recoveryState(
  connectionStatus: ConnectionStatus,
  promptSnapshotTick?: number | null,
  snapshotTick?: number | null
): ConnectionRecoveryState {
  if (connectionStatus === "connected" && promptSnapshotTick != null && snapshotTick != null && promptSnapshotTick !== snapshotTick) {
    return "stale";
  }

  switch (connectionStatus) {
    case "connected":
      return "online";
    case "connecting":
      return "connecting";
    case "error":
      return "error";
    case "reconnecting":
      return "recovering";
    case "resyncing":
      return "resyncing";
    case "disconnected":
    case "idle":
      return "offline";
  }
}

function copyForState(state: ConnectionRecoveryState, lastSystemMessage?: string | null): Pick<ConnectionRecoveryPlan, "detail" | "headline" | "nextStep"> {
  const message = lastSystemMessage?.trim();
  const detail = message && message.length > 0 ? message : fallbackDetail(state);

  switch (state) {
    case "connecting":
      return {
        detail,
        headline: "正在连接",
        nextStep: "等待连接完成，或断开后重新连接。"
      };
    case "error":
      return {
        detail,
        headline: "连接需要处理",
        nextStep: "重新连接；如果仍失败，检查服务端地址和房间。"
      };
    case "offline":
      return {
        detail,
        headline: "未连接服务端",
        nextStep: "连接并入座，等待服务端发布快照。"
      };
    case "recovering":
      return {
        detail,
        headline: "连接恢复中",
        nextStep: "等待自动重连；必要时断开后重新连接。"
      };
    case "resyncing":
      return {
        detail,
        headline: "正在同步快照",
        nextStep: "等待服务端返回最新快照。"
      };
    case "stale":
      return {
        detail,
        headline: "快照需要同步",
        nextStep: "重新同步快照，再提交行动。"
      };
    case "online":
      return {
        detail,
        headline: "连接正常",
        nextStep: "保持服务端快照同步，按候选行动提交。"
      };
  }
}

function fallbackDetail(state: ConnectionRecoveryState): string {
  switch (state) {
    case "connecting":
      return "正在建立服务端实时连接。";
    case "error":
      return "最近一次连接或同步操作失败。";
    case "offline":
      return "尚未进入服务端房间。";
    case "online":
      return "服务端连接、快照和提示入口可用。";
    case "recovering":
      return "实时连接正在恢复。";
    case "resyncing":
      return "正在请求服务端权威快照。";
    case "stale":
      return "当前提示 tick 与桌面快照 tick 不一致。";
  }
}

function actionsForState(state: ConnectionRecoveryState, hasSnapshot: boolean): ConnectionRecoveryAction[] {
  return [
    connectAction(state),
    resyncAction(state, hasSnapshot),
    disconnectAction(state)
  ];
}

function connectAction(state: ConnectionRecoveryState): ConnectionRecoveryAction {
  const primary = state === "error" || state === "offline";

  return {
    disabled: !primary,
    id: "connect",
    label: "连接",
    state: primary ? "primary" : "disabled",
    title: primary ? "连接服务端并进入当前房间" : "当前状态不需要重复连接"
  };
}

function resyncAction(state: ConnectionRecoveryState, hasSnapshot: boolean): ConnectionRecoveryAction {
  const enabled = state === "online" || state === "stale";

  return {
    disabled: !enabled,
    id: "resync",
    label: "同步",
    state: state === "stale" ? "primary" : enabled ? "secondary" : "disabled",
    title: enabled
      ? hasSnapshot
        ? "请求服务端重新发送权威快照"
        : "请求服务端发送首个权威快照"
      : "连接稳定后才能同步快照"
  };
}

function disconnectAction(state: ConnectionRecoveryState): ConnectionRecoveryAction {
  const enabled = state === "connecting" || state === "online" || state === "recovering" || state === "resyncing" || state === "stale";

  return {
    disabled: !enabled,
    id: "disconnect",
    label: "断开",
    state: enabled ? "secondary" : "disabled",
    title: enabled ? "断开当前实时连接" : "当前没有可断开的实时连接"
  };
}

function tickLabel(snapshotTick?: number | null, promptSnapshotTick?: number | null): string {
  if (snapshotTick != null && promptSnapshotTick != null) {
    return `快照 tick ${snapshotTick} / prompt tick ${promptSnapshotTick}`;
  }

  if (snapshotTick != null) {
    return `快照 tick ${snapshotTick}`;
  }

  if (promptSnapshotTick != null) {
    return `prompt tick ${promptSnapshotTick}`;
  }

  return "尚无服务端快照";
}

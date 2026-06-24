import type { ActionPromptDto, ConnectionStatus, SnapshotDto } from "../types/protocol";
import { connectionStatusLabel } from "./formatters";

export type ServerSubmissionGateState =
  | "connected"
  | "disconnected"
  | "missing-snapshot"
  | "read-only-prompt"
  | "resyncing"
  | "stale-snapshot";

export type ServerSubmissionGatePlan = {
  canSubmit: boolean;
  reason: string;
  state: ServerSubmissionGateState;
  stateLabel: string;
};

export function buildServerSubmissionGatePlan({
  connectionStatus,
  prompt,
  snapshot
}: {
  connectionStatus: ConnectionStatus;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
}): ServerSubmissionGatePlan {
  if (connectionStatus === "resyncing") {
    return blockedGate("resyncing", "同步中", "正在重新同步服务端快照，暂不提交行动。");
  }

  if (connectionStatus !== "connected") {
    return blockedGate("disconnected", "连接未就绪", `连接状态：${connectionStatusLabel(connectionStatus)}，暂不提交行动。`);
  }

  if (prompt && !prompt.actionable) {
    return blockedGate("read-only-prompt", "只读提示", "当前服务端提示为只读状态，暂不提交行动。");
  }

  if (prompt?.snapshotTick == null) {
    return {
      canSubmit: true,
      reason: "服务端未要求特定快照 tick。",
      state: "connected",
      stateLabel: "可提交"
    };
  }

  if (!snapshot) {
    return blockedGate("missing-snapshot", "等待快照", `行动提示属于 tick ${prompt.snapshotTick}，但本地尚未收到服务端快照。`);
  }

  if (snapshot.tick !== prompt.snapshotTick) {
    return blockedGate("stale-snapshot", "等待同步", `行动提示属于 tick ${prompt.snapshotTick}，当前桌面快照是 tick ${snapshot.tick}。`);
  }

  return {
    canSubmit: true,
    reason: `行动提示和桌面快照同属 tick ${snapshot.tick}。`,
    state: "connected",
    stateLabel: "可提交"
  };
}

function blockedGate(state: Exclude<ServerSubmissionGateState, "connected">, stateLabel: string, reason: string): ServerSubmissionGatePlan {
  return {
    canSubmit: false,
    reason,
    state,
    stateLabel
  };
}

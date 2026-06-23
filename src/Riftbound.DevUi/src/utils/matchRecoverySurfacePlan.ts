export type MatchRecoveryConnectionState =
  | "connecting"
  | "error"
  | "offline"
  | "online"
  | "recovering"
  | "resyncing"
  | "stale";

export type MatchRecoveryRegionId = "connection" | "errors" | "snapshot" | "submission";

export type MatchRecoveryRegionState = "blocked" | "ready" | "waiting";

export type MatchRecoverySource =
  | "server-connection"
  | "server-errors"
  | "server-prompt"
  | "server-receipt"
  | "server-snapshot";

export type MatchRecoverySubmissionGateLike = {
  canSubmit: boolean;
  reason: string;
  state: string;
  stateLabel: string;
};

export type MatchRecoveryRegion = {
  detail: string;
  id: MatchRecoveryRegionId;
  label: string;
  nextStep: string;
  source: MatchRecoverySource;
  state: MatchRecoveryRegionState;
  value: string;
};

export type MatchRecoverySurfacePlan = {
  activeRegionId: MatchRecoveryRegionId;
  sections: MatchRecoveryRegion[];
  state: "blocked" | "ready" | "waiting";
  summary: string;
};

export function buildMatchRecoverySurfacePlan({
  connectionState,
  connectionStatusLabel,
  errorCount,
  hasSnapshot,
  promptSnapshotTick,
  snapshotTick,
  submissionGate,
  submissionState
}: {
  connectionState: MatchRecoveryConnectionState;
  connectionStatusLabel: string;
  errorCount: number;
  hasSnapshot: boolean;
  promptSnapshotTick?: number | null;
  snapshotTick?: number | null;
  submissionGate: MatchRecoverySubmissionGateLike;
  submissionState?: string;
}): MatchRecoverySurfacePlan {
  const sections: MatchRecoveryRegion[] = [
    connectionRegion(connectionState, connectionStatusLabel),
    snapshotRegion(hasSnapshot, promptSnapshotTick, snapshotTick),
    submissionRegion(submissionGate, submissionState),
    errorsRegion(errorCount)
  ];
  const activeRegionId = activeRegion(sections);

  return {
    activeRegionId,
    sections,
    state: surfaceState(sections),
    summary: `连接：${connectionStatusLabel} / 快照：${hasSnapshot ? "有" : "无"} / 提交：${submissionGate.stateLabel} / 错误：${errorCount}`
  };
}

function connectionRegion(
  connectionState: MatchRecoveryConnectionState,
  connectionStatusLabel: string
): MatchRecoveryRegion {
  const ready = connectionState === "online" || connectionState === "stale";
  const waiting = connectionState === "connecting" || connectionState === "recovering" || connectionState === "resyncing";

  return {
    detail: ready ? "服务端实时连接可用。" : waiting ? "服务端连接正在建立或恢复。" : "当前无法向服务端提交行动。",
    id: "connection",
    label: "连接",
    nextStep: ready ? "保持连接，继续以服务端快照为准。" : waiting ? "等待连接恢复完成。" : "重新连接并请求权威快照。",
    source: "server-connection",
    state: ready ? "ready" : waiting ? "waiting" : "blocked",
    value: connectionStatusLabel
  };
}

function snapshotRegion(
  hasSnapshot: boolean,
  promptSnapshotTick?: number | null,
  snapshotTick?: number | null
): MatchRecoveryRegion {
  const stale = snapshotTick != null && promptSnapshotTick != null && snapshotTick !== promptSnapshotTick;
  const value = snapshotTick != null && promptSnapshotTick != null
    ? `${snapshotTick} / ${promptSnapshotTick}`
    : snapshotTick != null
      ? `快照 ${snapshotTick}`
      : promptSnapshotTick != null
        ? `prompt ${promptSnapshotTick}`
        : "无";

  return {
    detail: stale ? "桌面快照与行动提示 tick 不一致。" : hasSnapshot ? "桌面来自服务端权威快照。" : "尚未收到服务端权威快照。",
    id: "snapshot",
    label: "快照",
    nextStep: stale ? "重新同步服务端快照，再提交行动。" : hasSnapshot ? "继续使用当前服务端投影。" : "等待或主动同步快照。",
    source: stale || promptSnapshotTick != null ? "server-prompt" : "server-snapshot",
    state: stale || !hasSnapshot ? "blocked" : "ready",
    value
  };
}

function submissionRegion(
  submissionGate: MatchRecoverySubmissionGateLike,
  submissionState?: string
): MatchRecoveryRegion {
  const failed = submissionState === "failed";
  const value = submissionState ? submissionStateLabel(submissionState) : submissionGate.stateLabel;

  return {
    detail: submissionGate.reason,
    id: "submission",
    label: "提交",
    nextStep: failed ? "按服务端拒绝或失败原因修正后重试。" : submissionGate.canSubmit ? "提交时携带 prompt/tick 身份。" : "等待提交入口恢复。",
    source: submissionState ? "server-receipt" : "server-prompt",
    state: failed || !submissionGate.canSubmit ? "blocked" : submissionState === "submitting" ? "waiting" : "ready",
    value
  };
}

function errorsRegion(errorCount: number): MatchRecoveryRegion {
  const hasErrors = errorCount > 0;
  return {
    detail: hasErrors ? "存在服务端错误或失败回执。" : "当前没有服务端错误。",
    id: "errors",
    label: "错误",
    nextStep: hasErrors ? "先处理错误，再继续提交行动。" : "继续按服务端候选行动操作。",
    source: "server-errors",
    state: hasErrors ? "blocked" : "ready",
    value: hasErrors ? `${errorCount} 个` : "0"
  };
}

function activeRegion(sections: readonly MatchRecoveryRegion[]): MatchRecoveryRegionId {
  const connection = sections.find((section) => section.id === "connection");
  if (connection?.state === "blocked") {
    return "connection";
  }

  const snapshot = sections.find((section) => section.id === "snapshot");
  if (snapshot?.state === "blocked") {
    return "snapshot";
  }

  const errors = sections.find((section) => section.id === "errors");
  if (errors?.state === "blocked") {
    return "errors";
  }

  const submission = sections.find((section) => section.id === "submission");
  if (submission?.state === "blocked" || submission?.state === "ready") {
    return "submission";
  }

  return "connection";
}

function surfaceState(sections: readonly MatchRecoveryRegion[]): MatchRecoverySurfacePlan["state"] {
  if (sections.some((section) => section.state === "blocked")) {
    return "blocked";
  }

  if (sections.every((section) => section.state === "ready")) {
    return "ready";
  }

  return "waiting";
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

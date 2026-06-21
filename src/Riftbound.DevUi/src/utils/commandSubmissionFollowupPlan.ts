import type { CommandReceiptFollowupDto, GameEvent, GameEventObjectRef, SnapshotDto } from "../types/protocol";
import { eventDescriptionLabel, eventKindLabel } from "./eventLogPlan";

export type ObservedGameEvent = GameEvent & {
  receivedAt?: number;
  receivedBatchIndex?: number;
  receivedMessageType?: string;
  receivedServerTick?: number;
};

export type CommandSubmissionFollowupState =
  | "accepted-awaiting"
  | "accepted-events"
  | "accepted-snapshot"
  | "empty"
  | "failed"
  | "pending"
  | "unknown-tick";

export type CommandSubmissionFollowupMetric = {
  key: "events" | "prompt" | "serverState" | "snapshot" | "tick";
  label: string;
  state: "empty" | "ready" | "waiting";
  value: string;
};

export type CommandSubmissionFollowupBridgeState = "empty" | "failed" | "ready" | "unknown" | "waiting";

export type CommandSubmissionFollowupBridgeRow = {
  key: CommandSubmissionFollowupMetric["key"];
  label: string;
  state: "blocked" | "empty" | "ready" | "waiting";
  stateLabel: string;
  value: string;
};

export type CommandSubmissionFollowupBridgePlan = {
  headline: string;
  nextStepLabel: string;
  rows: CommandSubmissionFollowupBridgeRow[];
  serverStateLabel: string;
  state: CommandSubmissionFollowupBridgeState;
  stateLabel: string;
  summary: string;
};

export type CommandSubmissionFollowupFeedback = {
  followup?: CommandReceiptFollowupDto | null;
  serverTick?: number | null;
  state?: string;
  uiSource?: CommandSubmissionUiSource;
};

export type CommandSubmissionUiSource = {
  detailId?: string;
  label: string;
  objectId?: string;
  surface: string;
};

export type CommandSubmissionFollowupEventRow = {
  description: string;
  kind: string;
  key: string;
  messageType?: string;
  refCount: number;
  refs: CommandSubmissionFollowupEventRef[];
  title: string;
};

export type CommandSubmissionFollowupEventRef = {
  hidden: boolean;
  key: string;
  label: string;
  objectId?: string;
  role: string;
};

export type CommandSubmissionFollowupPlan = {
  bridge: CommandSubmissionFollowupBridgePlan;
  events: CommandSubmissionFollowupEventRow[];
  hiddenEventCount: number;
  metrics: CommandSubmissionFollowupMetric[];
  serverFollowupState: string;
  serverFollowupStateLabel: string;
  state: CommandSubmissionFollowupState;
  summary: string;
  uiSource?: CommandSubmissionUiSource;
};

export function buildCommandSubmissionFollowupPlan({
  events,
  feedback,
  limit = 4,
  snapshot
}: {
  events?: readonly ObservedGameEvent[];
  feedback?: CommandSubmissionFollowupFeedback;
  limit?: number;
  snapshot?: SnapshotDto;
}): CommandSubmissionFollowupPlan {
  if (!feedback) {
    return emptyPlan("empty", "尚未提交命令，等待服务端回执。");
  }

  if (feedback.state === "submitting") {
    return emptyPlan("pending", "命令已发出，等待服务端回执和后续事件。", feedback, snapshot);
  }

  if (feedback.state === "failed") {
    return emptyPlan("failed", feedback.followup?.summary ?? "命令未被服务端接受，不展示后续事件。", feedback, snapshot);
  }

  const receiptFollowup = feedback.followup;
  const serverTick = numberOrUndefined(feedback.serverTick) ?? numberOrUndefined(receiptFollowup?.serverTick);
  if (serverTick == null) {
    return emptyPlan("unknown-tick", "回执未携带服务端 tick，无法关联后续事件。", feedback, snapshot);
  }

  const allMatchingEvents = (events ?? []).filter((event) => event.receivedServerTick === serverTick);
  const reportedEventCount = nonNegativeIntegerOrUndefined(receiptFollowup?.eventCount);
  const reportedSnapshotCount = nonNegativeIntegerOrUndefined(receiptFollowup?.snapshotCount);
  const reportedPromptCount = nonNegativeIntegerOrUndefined(receiptFollowup?.promptCount);
  const authoritativeEventCount = Math.max(allMatchingEvents.length, reportedEventCount ?? 0);
  const visibleEvents = allMatchingEvents.slice(0, limit).map((event, index) => ({
    description: eventDescriptionLabel(event),
    kind: event.kind,
    key: `${serverTick}:${event.kind}:${event.receivedBatchIndex ?? index}`,
    messageType: event.receivedMessageType,
    refCount: event.objectRefs?.length ?? 0,
    refs: followupEventRefs(event.objectRefs),
    title: eventKindLabel(event.kind)
  }));
  const snapshotTick = numberOrUndefined(snapshot?.tick);
  const snapshotCaughtUp = snapshotTick != null && snapshotTick >= serverTick;

  if (visibleEvents.length > 0) {
    return attachBridge({
      events: visibleEvents,
      hiddenEventCount: Math.max(0, authoritativeEventCount - visibleEvents.length),
      metrics: followupMetrics({
        eventCount: authoritativeEventCount,
        feedback,
        promptCount: reportedPromptCount,
        snapshot,
        snapshotBroadcastCount: reportedSnapshotCount,
        serverTick
      }),
      ...serverFollowupFields(feedback),
      uiSource: feedback.uiSource,
      state: "accepted-events",
      summary: receiptFollowup?.summary ?? `服务端 tick ${serverTick} 广播 ${authoritativeEventCount} 条后续事件。`
    });
  }

  if (reportedEventCount != null && reportedEventCount > 0) {
    return attachBridge({
      events: [],
      hiddenEventCount: reportedEventCount,
      metrics: followupMetrics({
        eventCount: reportedEventCount,
        feedback,
        promptCount: reportedPromptCount,
        snapshot,
        snapshotBroadcastCount: reportedSnapshotCount,
        serverTick
      }),
      ...serverFollowupFields(feedback),
      uiSource: feedback.uiSource,
      state: "accepted-awaiting",
      summary: `服务端回执声明 tick ${serverTick} 有 ${reportedEventCount} 条公开事件，等待事件流抵达。`
    });
  }

  if (snapshotCaughtUp || (reportedSnapshotCount ?? 0) > 0 || (reportedPromptCount ?? 0) > 0) {
    return attachBridge({
      events: [],
      hiddenEventCount: 0,
      metrics: followupMetrics({
        eventCount: 0,
        feedback,
        promptCount: reportedPromptCount,
        snapshot,
        snapshotBroadcastCount: reportedSnapshotCount,
        serverTick
      }),
      ...serverFollowupFields(feedback),
      uiSource: feedback.uiSource,
      state: "accepted-snapshot",
      summary: receiptFollowup?.summary ?? `当前快照已追上 tick ${serverTick}；该命令没有公开事件，后续以当前快照/提示为准。`
    });
  }

  return attachBridge({
    events: [],
    hiddenEventCount: 0,
    metrics: followupMetrics({
      eventCount: 0,
      feedback,
      promptCount: reportedPromptCount,
      snapshot,
      snapshotBroadcastCount: reportedSnapshotCount,
      serverTick
    }),
    ...serverFollowupFields(feedback),
    uiSource: feedback.uiSource,
    state: "accepted-awaiting",
    summary: `等待 tick ${serverTick} 的事件或快照广播。`
  });
}

function emptyPlan(
  state: CommandSubmissionFollowupState,
  summary: string,
  feedback?: CommandSubmissionFollowupFeedback,
  snapshot?: SnapshotDto
): CommandSubmissionFollowupPlan {
  const serverTick = numberOrUndefined(feedback?.serverTick);
  return attachBridge({
    events: [],
    hiddenEventCount: 0,
    metrics: followupMetrics({
      eventCount: 0,
      feedback,
      promptCount: nonNegativeIntegerOrUndefined(feedback?.followup?.promptCount),
      snapshot,
      snapshotBroadcastCount: nonNegativeIntegerOrUndefined(feedback?.followup?.snapshotCount),
      serverTick: serverTick ?? numberOrUndefined(feedback?.followup?.serverTick)
    }),
    ...serverFollowupFields(feedback),
    uiSource: feedback?.uiSource,
    state,
    summary
  });
}

function attachBridge(plan: Omit<CommandSubmissionFollowupPlan, "bridge">): CommandSubmissionFollowupPlan {
  return {
    ...plan,
    bridge: bridgePlanFor(plan)
  };
}

function bridgePlanFor(plan: Omit<CommandSubmissionFollowupPlan, "bridge">): CommandSubmissionFollowupBridgePlan {
  const state = bridgeStateFor(plan.state);
  return {
    headline: bridgeHeadlineFor(plan.state),
    nextStepLabel: bridgeNextStepFor(plan.state),
    rows: plan.metrics.map((metric) => {
      const rowState = bridgeRowStateFor(metric, plan.state);
      return {
        key: metric.key,
        label: metric.label,
        state: rowState,
        stateLabel: bridgeRowStateLabel(rowState),
        value: metric.value
      };
    }),
    serverStateLabel: plan.serverFollowupStateLabel,
    state,
    stateLabel: bridgeStateLabel(state),
    summary: plan.summary
  };
}

function bridgeStateFor(state: CommandSubmissionFollowupState): CommandSubmissionFollowupBridgeState {
  switch (state) {
    case "accepted-events":
    case "accepted-snapshot":
      return "ready";
    case "accepted-awaiting":
    case "pending":
      return "waiting";
    case "failed":
      return "failed";
    case "unknown-tick":
      return "unknown";
    case "empty":
      return "empty";
  }
}

function bridgeHeadlineFor(state: CommandSubmissionFollowupState): string {
  switch (state) {
    case "accepted-awaiting":
      return "等待同 tick 广播";
    case "accepted-events":
      return "已收到同 tick 事件";
    case "accepted-snapshot":
      return "快照/提示已同步";
    case "empty":
      return "等待提交";
    case "failed":
      return "提交未成立";
    case "pending":
      return "等待服务端回执";
    case "unknown-tick":
      return "缺少回执 tick";
  }
}

function bridgeNextStepFor(state: CommandSubmissionFollowupState): string {
  switch (state) {
    case "accepted-awaiting":
      return "等待事件流、快照或提示广播。";
    case "accepted-events":
      return "查看事件引用，必要时选择对象检查规则上下文。";
    case "accepted-snapshot":
      return "查看当前快照和提示；无公开事件时以服务端快照为准。";
    case "empty":
      return "先提交服务端候选路线。";
    case "failed":
      return "查看错误和服务端回执，重新选择合法候选。";
    case "pending":
      return "保持当前路线，不重复提交同一意图。";
    case "unknown-tick":
      return "等待携带 tick 的回执或重新同步快照。";
  }
}

function bridgeStateLabel(state: CommandSubmissionFollowupBridgeState): string {
  switch (state) {
    case "empty":
      return "未提交";
    case "failed":
      return "失败";
    case "ready":
      return "已同步";
    case "unknown":
      return "未知";
    case "waiting":
      return "等待";
  }
}

function bridgeRowStateFor(
  metric: CommandSubmissionFollowupMetric,
  state: CommandSubmissionFollowupState
): CommandSubmissionFollowupBridgeRow["state"] {
  if (state === "failed" && metric.key === "serverState") {
    return "blocked";
  }

  return metric.state;
}

function bridgeRowStateLabel(state: CommandSubmissionFollowupBridgeRow["state"]): string {
  switch (state) {
    case "blocked":
      return "阻断";
    case "empty":
      return "无";
    case "ready":
      return "就绪";
    case "waiting":
      return "等待";
  }
}

function followupMetrics({
  eventCount,
  feedback,
  promptCount,
  serverTick,
  snapshot,
  snapshotBroadcastCount
}: {
  eventCount: number;
  feedback?: CommandSubmissionFollowupFeedback;
  promptCount?: number;
  serverTick?: number;
  snapshot?: SnapshotDto;
  snapshotBroadcastCount?: number;
}): CommandSubmissionFollowupMetric[] {
  const snapshotTick = numberOrUndefined(snapshot?.tick);
  const snapshotCaughtUp = serverTick != null && snapshotTick != null && snapshotTick >= serverTick;
  const effectiveSnapshotCount = snapshotBroadcastCount ?? 0;
  const effectivePromptCount = promptCount ?? 0;

  return [
    {
      key: "serverState",
      label: "服务端后续",
      state: serverFollowupMetricState(feedback),
      value: serverFollowupStateLabel(serverFollowupState(feedback))
    },
    {
      key: "tick",
      label: "回执 tick",
      state: serverTick == null ? "waiting" : "ready",
      value: serverTick == null ? "无" : String(serverTick)
    },
    {
      key: "events",
      label: "后续事件",
      state: eventCount > 0 ? "ready" : feedback?.followup ? "empty" : feedback?.state === "sent" ? "waiting" : "empty",
      value: String(eventCount)
    },
    {
      key: "snapshot",
      label: "当前快照",
      state: snapshotCaughtUp || effectiveSnapshotCount > 0 ? "ready" : snapshotTick == null ? "empty" : "waiting",
      value: effectiveSnapshotCount > 0 && snapshotTick != null
        ? `${snapshotTick} / ${effectiveSnapshotCount}`
        : snapshotTick == null
          ? String(effectiveSnapshotCount)
          : String(snapshotTick)
    },
    {
      key: "prompt",
      label: "提示广播",
      state: effectivePromptCount > 0 ? "ready" : feedback?.followup ? "empty" : feedback?.state === "sent" ? "waiting" : "empty",
      value: String(effectivePromptCount)
    }
  ];
}

function serverFollowupFields(feedback?: CommandSubmissionFollowupFeedback): {
  serverFollowupState: string;
  serverFollowupStateLabel: string;
} {
  const state = serverFollowupState(feedback);
  return {
    serverFollowupState: state,
    serverFollowupStateLabel: serverFollowupStateLabel(state)
  };
}

function serverFollowupState(feedback?: CommandSubmissionFollowupFeedback): string {
  if (feedback?.followup?.state) {
    return feedback.followup.state;
  }

  if (!feedback) {
    return "none";
  }

  if (feedback.state === "submitting") {
    return "pending";
  }

  if (feedback.state === "failed") {
    return "client-failed";
  }

  if (feedback.state === "sent") {
    return "receipt-only";
  }

  return "none";
}

function serverFollowupMetricState(feedback?: CommandSubmissionFollowupFeedback): CommandSubmissionFollowupMetric["state"] {
  const state = serverFollowupState(feedback);
  if (state === "none" || state === "client-failed") {
    return "empty";
  }

  return state === "pending" ? "waiting" : "ready";
}

function serverFollowupStateLabel(state: string): string {
  switch (state) {
    case "events":
      return "事件";
    case "snapshot-prompt":
      return "快照/提示";
    case "silent":
      return "静默";
    case "rejected":
      return "拒绝";
    case "failed":
      return "失败";
    case "pending":
      return "等待";
    case "receipt-only":
      return "仅回执";
    case "client-failed":
      return "本地失败";
    case "none":
      return "无";
    default:
      return state || "未知";
  }
}

function numberOrUndefined(value: unknown): number | undefined {
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

function nonNegativeIntegerOrUndefined(value: unknown): number | undefined {
  return typeof value === "number" && Number.isInteger(value) && value >= 0 ? value : undefined;
}

function followupEventRefs(refs: readonly GameEventObjectRef[] | null | undefined): CommandSubmissionFollowupEventRef[] {
  return (refs ?? []).map((ref, index) => {
    const hidden = ref.isHidden === true || ref.isFaceDown === true || ref.objectId === "HIDDEN";
    return {
      hidden,
      key: `${index}:${ref.role}:${hidden ? "hidden" : ref.objectId}`,
      label: hidden ? `${ref.role}：隐藏对象` : `${ref.role}：${ref.objectId}`,
      objectId: hidden ? undefined : ref.objectId,
      role: ref.role
    };
  });
}

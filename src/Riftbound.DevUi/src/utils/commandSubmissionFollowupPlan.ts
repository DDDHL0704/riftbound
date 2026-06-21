import type { CommandReceiptFollowupDto, GameEvent, SnapshotDto } from "../types/protocol";
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
  key: "events" | "prompt" | "snapshot" | "tick";
  label: string;
  state: "empty" | "ready" | "waiting";
  value: string;
};

export type CommandSubmissionFollowupFeedback = {
  followup?: CommandReceiptFollowupDto | null;
  serverTick?: number | null;
  state?: string;
};

export type CommandSubmissionFollowupEventRow = {
  description: string;
  kind: string;
  key: string;
  messageType?: string;
  refCount: number;
  title: string;
};

export type CommandSubmissionFollowupPlan = {
  events: CommandSubmissionFollowupEventRow[];
  hiddenEventCount: number;
  metrics: CommandSubmissionFollowupMetric[];
  state: CommandSubmissionFollowupState;
  summary: string;
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
    title: eventKindLabel(event.kind)
  }));
  const snapshotTick = numberOrUndefined(snapshot?.tick);
  const snapshotCaughtUp = snapshotTick != null && snapshotTick >= serverTick;

  if (visibleEvents.length > 0) {
    return {
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
      state: "accepted-events",
      summary: receiptFollowup?.summary ?? `服务端 tick ${serverTick} 广播 ${authoritativeEventCount} 条后续事件。`
    };
  }

  if (reportedEventCount != null && reportedEventCount > 0) {
    return {
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
      state: "accepted-awaiting",
      summary: `服务端回执声明 tick ${serverTick} 有 ${reportedEventCount} 条公开事件，等待事件流抵达。`
    };
  }

  if (snapshotCaughtUp || (reportedSnapshotCount ?? 0) > 0 || (reportedPromptCount ?? 0) > 0) {
    return {
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
      state: "accepted-snapshot",
      summary: receiptFollowup?.summary ?? `当前快照已追上 tick ${serverTick}；该命令没有公开事件，后续以当前快照/提示为准。`
    };
  }

  return {
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
    state: "accepted-awaiting",
    summary: `等待 tick ${serverTick} 的事件或快照广播。`
  };
}

function emptyPlan(
  state: CommandSubmissionFollowupState,
  summary: string,
  feedback?: CommandSubmissionFollowupFeedback,
  snapshot?: SnapshotDto
): CommandSubmissionFollowupPlan {
  const serverTick = numberOrUndefined(feedback?.serverTick);
  return {
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
    state,
    summary
  };
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

function numberOrUndefined(value: unknown): number | undefined {
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

function nonNegativeIntegerOrUndefined(value: unknown): number | undefined {
  return typeof value === "number" && Number.isInteger(value) && value >= 0 ? value : undefined;
}

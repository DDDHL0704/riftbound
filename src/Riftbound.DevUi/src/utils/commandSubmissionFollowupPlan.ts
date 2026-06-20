import type { GameEvent, SnapshotDto } from "../types/protocol";
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
  key: "events" | "snapshot" | "tick";
  label: string;
  state: "empty" | "ready" | "waiting";
  value: string;
};

export type CommandSubmissionFollowupFeedback = {
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
    return emptyPlan("failed", "命令未被服务端接受，不展示后续事件。", feedback, snapshot);
  }

  const serverTick = numberOrUndefined(feedback.serverTick);
  if (serverTick == null) {
    return emptyPlan("unknown-tick", "回执未携带服务端 tick，无法关联后续事件。", feedback, snapshot);
  }

  const allMatchingEvents = (events ?? []).filter((event) => event.receivedServerTick === serverTick);
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
      hiddenEventCount: Math.max(0, allMatchingEvents.length - visibleEvents.length),
      metrics: followupMetrics({ eventCount: allMatchingEvents.length, feedback, snapshot, serverTick }),
      state: "accepted-events",
      summary: `服务端 tick ${serverTick} 广播 ${allMatchingEvents.length} 条后续事件。`
    };
  }

  if (snapshotCaughtUp) {
    return {
      events: [],
      hiddenEventCount: 0,
      metrics: followupMetrics({ eventCount: 0, feedback, snapshot, serverTick }),
      state: "accepted-snapshot",
      summary: `当前快照已追上 tick ${serverTick}；该命令没有公开事件，后续以当前快照/提示为准。`
    };
  }

  return {
    events: [],
    hiddenEventCount: 0,
    metrics: followupMetrics({ eventCount: 0, feedback, snapshot, serverTick }),
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
    metrics: followupMetrics({ eventCount: 0, feedback, snapshot, serverTick }),
    state,
    summary
  };
}

function followupMetrics({
  eventCount,
  feedback,
  serverTick,
  snapshot
}: {
  eventCount: number;
  feedback?: CommandSubmissionFollowupFeedback;
  serverTick?: number;
  snapshot?: SnapshotDto;
}): CommandSubmissionFollowupMetric[] {
  const snapshotTick = numberOrUndefined(snapshot?.tick);
  const snapshotCaughtUp = serverTick != null && snapshotTick != null && snapshotTick >= serverTick;

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
      state: eventCount > 0 ? "ready" : feedback?.state === "sent" ? "waiting" : "empty",
      value: String(eventCount)
    },
    {
      key: "snapshot",
      label: "当前快照",
      state: snapshotCaughtUp ? "ready" : snapshotTick == null ? "empty" : "waiting",
      value: snapshotTick == null ? "无" : String(snapshotTick)
    }
  ];
}

function numberOrUndefined(value: unknown): number | undefined {
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

import type {
  CommandSubmissionFollowupBridgeRow,
  CommandSubmissionFollowupEventRow,
  CommandSubmissionFollowupMetric,
  CommandSubmissionFollowupPlan,
  CommandSubmissionUiSource
} from "./commandSubmissionFollowupPlan";

export type WireSidePanelReceiptFeedback = {
  clientIntentId?: string;
  cmdType?: string;
  errorCode?: string | null;
  followup?: {
    serverTick?: number | null;
    state?: string | null;
  } | null;
  message?: string;
  promptId?: string | null;
  receiptState?: string | null;
  serverTick?: number | null;
  snapshotTick?: number | null;
  state?: string;
  stateLabel?: string;
  uiSource?: CommandSubmissionUiSource;
};

export type WireSidePanelReceiptMetric = {
  key: "command" | "events" | "followup" | "receipt" | "tick";
  label: string;
  state: "empty" | "failed" | "ready" | "waiting";
  value: string;
};

export type WireSidePanelReceiptPlan = {
  bridge: {
    headline: string;
    hiddenRowCount: number;
    nextStepLabel: string;
    rows: CommandSubmissionFollowupBridgeRow[];
    serverStateLabel: string;
    state: CommandSubmissionFollowupPlan["bridge"]["state"];
    stateLabel: string;
    summary: string;
  };
  canOpenLayer: boolean;
  detailButtonLabel: string;
  eventRows: CommandSubmissionFollowupEventRow[];
  hiddenEventCount: number;
  metrics: WireSidePanelReceiptMetric[];
  mode: "accepted" | "empty" | "failed" | "pending" | "unknown";
  sourceRows: CommandSubmissionFollowupPlan["sourceRows"];
  state: string;
  stateLabel: string;
  subtitle: string;
  title: string;
};

export function buildWireSidePanelReceiptPlan({
  eventLimit = 2,
  feedback,
  followup,
  metricLimit = 3
}: {
  eventLimit?: number;
  feedback?: WireSidePanelReceiptFeedback;
  followup: CommandSubmissionFollowupPlan;
  metricLimit?: number;
}): WireSidePanelReceiptPlan {
  const mode = receiptMode(feedback, followup);
  const state = feedback?.state ?? "empty";
  const eventRows = followup.events.slice(0, Math.max(0, eventLimit));
  const bridgeRows = followup.bridge.rows.slice(0, Math.max(0, metricLimit));

  return {
    bridge: {
      headline: followup.bridge.headline,
      hiddenRowCount: Math.max(0, followup.bridge.rows.length - bridgeRows.length),
      nextStepLabel: followup.bridge.nextStepLabel,
      rows: bridgeRows,
      serverStateLabel: followup.bridge.serverStateLabel,
      state: followup.bridge.state,
      stateLabel: followup.bridge.stateLabel,
      summary: followup.bridge.summary
    },
    canOpenLayer: Boolean(feedback),
    detailButtonLabel: "打开回执检查层",
    eventRows,
    hiddenEventCount: Math.max(0, followup.hiddenEventCount + followup.events.length - eventRows.length),
    metrics: receiptMetrics({ feedback, followup }).slice(0, Math.max(0, metricLimit + 1)),
    mode,
    sourceRows: followup.sourceRows,
    state,
    stateLabel: feedback?.stateLabel ?? "尚未提交",
    subtitle: receiptSubtitle(feedback, followup),
    title: "提交反馈"
  };
}

function receiptMetrics({
  feedback,
  followup
}: {
  feedback?: WireSidePanelReceiptFeedback;
  followup: CommandSubmissionFollowupPlan;
}): WireSidePanelReceiptMetric[] {
  const command = feedback?.cmdType ?? "无";
  const receipt = feedback?.receiptState ?? feedback?.state ?? "无";
  const tick = feedback?.serverTick ?? feedback?.followup?.serverTick;
  const eventMetric = followup.metrics.find((metric) => metric.key === "events");

  return [
    {
      key: "command",
      label: "命令",
      state: command === "无" ? "empty" : "ready",
      value: command
    },
    {
      key: "receipt",
      label: "回执",
      state: receiptMetricState(feedback),
      value: receipt
    },
    {
      key: "followup",
      label: "后续",
      state: bridgeMetricState(followup.bridge.state),
      value: followup.serverFollowupStateLabel
    },
    {
      key: "events",
      label: "事件",
      state: followupMetricState(eventMetric),
      value: eventMetric?.value ?? String(followup.events.length)
    },
    {
      key: "tick",
      label: "tick",
      state: tick == null ? "waiting" : "ready",
      value: tick == null ? "无" : String(tick)
    }
  ];
}

function receiptMode(
  feedback: WireSidePanelReceiptFeedback | undefined,
  followup: CommandSubmissionFollowupPlan
): WireSidePanelReceiptPlan["mode"] {
  if (!feedback) {
    return "empty";
  }

  if (feedback.state === "failed" || followup.bridge.state === "failed") {
    return "failed";
  }

  if (feedback.state === "submitting" || followup.bridge.state === "waiting") {
    return "pending";
  }

  if (feedback.state === "sent") {
    return "accepted";
  }

  return "unknown";
}

function receiptSubtitle(
  feedback: WireSidePanelReceiptFeedback | undefined,
  followup: CommandSubmissionFollowupPlan
): string {
  if (!feedback) {
    return "等待右侧路线或候选操作提交给服务端。";
  }

  return feedback.message || followup.summary;
}

function receiptMetricState(feedback?: WireSidePanelReceiptFeedback): WireSidePanelReceiptMetric["state"] {
  if (!feedback) {
    return "empty";
  }

  if (feedback.state === "failed") {
    return "failed";
  }

  if (feedback.state === "submitting") {
    return "waiting";
  }

  return "ready";
}

function bridgeMetricState(state: CommandSubmissionFollowupPlan["bridge"]["state"]): WireSidePanelReceiptMetric["state"] {
  switch (state) {
    case "empty":
      return "empty";
    case "failed":
      return "failed";
    case "ready":
      return "ready";
    case "unknown":
    case "waiting":
      return "waiting";
  }
}

function followupMetricState(metric?: CommandSubmissionFollowupMetric): WireSidePanelReceiptMetric["state"] {
  if (!metric) {
    return "empty";
  }

  return metric.state;
}

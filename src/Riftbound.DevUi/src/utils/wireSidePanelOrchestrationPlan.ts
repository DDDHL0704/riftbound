import type { WireSidePanelSlot } from "../components/match/wireTableLayout";
import type { ActionPromptDto, ConnectionStatus, GameEvent, SnapshotDto } from "../types/protocol";
import type { CandidateSelectionDraft } from "./candidateSelectionDraft";
import type { ServerSubmissionGatePlan } from "./serverSubmissionGatePlan";
import type { WireSidePanelDirectoryPlan } from "./wireSidePanelDirectoryPlan";

export type WireSidePanelOrchestrationState =
  | "active"
  | "audit"
  | "blocked"
  | "empty"
  | "history"
  | "offline"
  | "ready"
  | "review"
  | "waiting";

export type WireSidePanelOrchestrationTone = "bad" | "good" | "info" | "neutral" | "warn";

export type WireSidePanelOrchestrationEntry = {
  count: number;
  detail: string;
  groupLabel: string;
  href: string;
  label: string;
  order: number;
  slot: WireSidePanelSlot;
  state: WireSidePanelOrchestrationState;
  stateLabel: string;
  tone: WireSidePanelOrchestrationTone;
};

export type WireSidePanelOrchestrationPlan = {
  activeCount: number;
  entries: WireSidePanelOrchestrationEntry[];
  nextStepLabel: string;
  primarySlot: WireSidePanelSlot;
  state: WireSidePanelOrchestrationState;
  stateLabel: string;
  summary: string;
  urgentCount: number;
};

export type WireSidePanelTimelineDetailContext = {
  id?: string;
  source?: "event" | "rule" | string;
};

export function buildWireSidePanelOrchestrationPlan({
  connectionStatus,
  directory,
  events = [],
  prompt,
  selectedObjectId,
  selectionDraft,
  snapshot,
  submissionGate,
  timelineDetail
}: {
  connectionStatus: ConnectionStatus;
  directory: WireSidePanelDirectoryPlan;
  events?: GameEvent[];
  prompt?: ActionPromptDto;
  selectedObjectId?: string;
  selectionDraft?: CandidateSelectionDraft;
  snapshot?: SnapshotDto;
  submissionGate?: ServerSubmissionGatePlan;
  timelineDetail?: WireSidePanelTimelineDetailContext;
}): WireSidePanelOrchestrationPlan {
  const context = orchestrationContext({
    connectionStatus,
    events,
    prompt,
    selectedObjectId,
    selectionDraft,
    snapshot,
    submissionGate,
    timelineDetail
  });
  const entries = directory.entries.map((entry) => ({
    ...entry,
    ...slotState(entry.slot, context),
    href: `#${entry.anchorId}`
  }));
  const primary = primaryEntry(entries);
  const activeCount = entries.filter((entry) => isActiveState(entry.state)).length;
  const urgentCount = entries.filter((entry) => entry.state === "blocked" || entry.state === "offline" || entry.state === "ready").length;

  return {
    activeCount,
    entries,
    nextStepLabel: primary.detail,
    primarySlot: primary.slot,
    state: primary.state,
    stateLabel: primary.stateLabel,
    summary: `${primary.label} / ${primary.stateLabel}`,
    urgentCount
  };
}

type OrchestrationContext = {
  candidateCount: number;
  canSubmit: boolean;
  connectionStatus: ConnectionStatus;
  disabledCandidateCount: number;
  enabledCandidateCount: number;
  eventCount: number;
  hasDraft: boolean;
  hasPromptAuthority: boolean;
  hasSelectedObject: boolean;
  hasServerFlow: boolean;
  hasTableProjection: boolean;
  hasTimelineDetail: boolean;
  promptActionable: boolean;
  stackCount: number;
  submissionGateReason?: string;
  submissionGateStateLabel?: string;
  taskCount: number;
  triggerCount: number;
};

function orchestrationContext({
  connectionStatus,
  events = [],
  prompt,
  selectedObjectId,
  selectionDraft,
  snapshot,
  submissionGate,
  timelineDetail
}: {
  connectionStatus: ConnectionStatus;
  events?: GameEvent[];
  prompt?: ActionPromptDto;
  selectedObjectId?: string;
  selectionDraft?: CandidateSelectionDraft;
  snapshot?: SnapshotDto;
  submissionGate?: ServerSubmissionGatePlan;
  timelineDetail?: WireSidePanelTimelineDetailContext;
}): OrchestrationContext {
  const candidateCount = prompt?.serverFlow?.candidateCount ?? prompt?.candidates?.length ?? 0;
  const enabledCandidateCount = prompt?.serverFlow?.enabledCandidateCount
    ?? prompt?.candidates?.filter((candidate) => candidate.enabled).length
    ?? 0;
  const disabledCandidateCount = prompt?.serverFlow?.disabledCandidateCount
    ?? Math.max(0, candidateCount - enabledCandidateCount);
  const timing = asRecord(snapshot?.timing);
  const pendingTaskQueue = asRecord(timing.pendingTaskQueue);

  return {
    candidateCount,
    canSubmit: Boolean(submissionGate?.canSubmit),
    connectionStatus,
    disabledCandidateCount,
    enabledCandidateCount,
    eventCount: events.length,
    hasDraft: Boolean(selectionDraft?.candidateKey),
    hasPromptAuthority: Boolean(prompt?.contract || prompt?.serverFlow || prompt?.view?.responsibility),
    hasSelectedObject: Boolean(selectedObjectId),
    hasServerFlow: Boolean(prompt?.serverFlow),
    hasTableProjection: Boolean(snapshot?.table?.source),
    hasTimelineDetail: Boolean(timelineDetail?.id),
    promptActionable: Boolean(prompt?.actionable),
    stackCount: Array.isArray(snapshot?.stack) ? snapshot.stack.length : 0,
    submissionGateReason: submissionGate?.reason,
    submissionGateStateLabel: submissionGate?.stateLabel,
    taskCount: arrayLength(pendingTaskQueue.tasks) + arrayLength(timing.battlefieldTasks),
    triggerCount: arrayLength(timing.triggerQueue)
  };
}

function slotState(
  slot: WireSidePanelSlot,
  context: OrchestrationContext
): Omit<WireSidePanelOrchestrationEntry, "groupLabel" | "href" | "label" | "order" | "slot"> {
  if (!connectionStable(context.connectionStatus)) {
    return disconnectedSlotState(slot, context);
  }

  switch (slot) {
    case "overview":
      return entryState("active", "态势", context.eventCount, "总览当前窗口、候选、规则队列和选中对象。");
    case "turnWindow":
      return context.promptActionable
        ? entryState("active", "窗口", context.candidateCount, "服务端已公开当前行动窗口。")
        : context.stackCount + context.taskCount + context.triggerCount > 0
          ? entryState("active", "结算", context.stackCount + context.taskCount + context.triggerCount, "服务端存在规则队列或结算链。")
          : entryState("waiting", "等待", 0, "等待服务端公开下一行动窗口。");
    case "commandCenter":
      return commandSlotState(context, "指挥中心汇总当前可提交命令、对象焦点和提交反馈。");
    case "serverFlow":
      return context.hasServerFlow
        ? entryState("active", "服务端", context.candidateCount, "使用服务端 serverFlow 作为结算与行动流程来源。")
        : context.stackCount + context.taskCount + context.triggerCount > 0
          ? entryState("active", "队列", context.stackCount + context.taskCount + context.triggerCount, "从快照规则队列投影当前流程。")
          : entryState("waiting", "无流程", 0, "暂无服务端流程或规则队列。");
    case "responseCoach":
      return context.canSubmit
        ? entryState("ready", "可响应", context.enabledCandidateCount, "响应导航可提交服务端候选。")
        : context.promptActionable
          ? entryState("blocked", "受限", context.disabledCandidateCount, "行动窗口存在，但提交门未开放。")
          : entryState("waiting", "观察", 0, "当前没有需要本地响应的候选。");
    case "tableAuthority":
      return context.hasTableProjection
        ? entryState("audit", "服务端", 1, "桌面区域来自服务端 table 投影。")
        : entryState("audit", "兼容", 0, "桌面区域使用旧快照字段兜底。");
    case "informationBoundary":
      return entryState("audit", "边界", context.eventCount, "检查隐藏区、手牌、备战位和对象引用边界。");
    case "promptAuthority":
      return context.hasPromptAuthority
        ? entryState("audit", "服务端", context.candidateCount, "行动窗口包含服务端责任、契约或流程字段。")
        : entryState("waiting", "缺口", 0, "当前行动窗口缺少服务端契约字段。");
    case "actionMap":
      return commandSlotState(context, "合法操作地图只展示服务端当前候选。");
    case "interaction":
      return context.hasSelectedObject
        ? entryState("review", "焦点", 1, "已选中桌面对象，可查看候选、事件和规则关联。")
        : entryState("waiting", "无焦点", 0, "点击桌面对象后显示焦点行动。");
    case "ruleQueue":
      return context.stackCount + context.taskCount + context.triggerCount > 0
        ? entryState("active", "规则", context.stackCount + context.taskCount + context.triggerCount, "存在结算链、任务或触发队列。")
        : entryState("waiting", "空队列", 0, "当前没有公开规则队列。");
    case "timelineDetail":
      return context.hasTimelineDetail
        ? entryState("review", "详情", 1, "正在查看规则或事件详情。")
        : entryState("empty", "未选", 0, "从规则队列或日志选择事件查看详情。");
    case "actionPrompt":
      return commandSlotState(context, "服务端行动提示保留原始候选与提交入口。");
    case "log":
      return context.eventCount > 0
        ? entryState("history", "日志", context.eventCount, "显示服务端事件历史。")
        : entryState("empty", "空", 0, "暂无服务端事件。");
  }
}

function commandSlotState(
  context: OrchestrationContext,
  detail: string
): Omit<WireSidePanelOrchestrationEntry, "groupLabel" | "href" | "label" | "order" | "slot"> {
  if (context.canSubmit && context.enabledCandidateCount > 0) {
    return entryState("ready", "可提交", context.enabledCandidateCount, detail);
  }

  if (context.hasDraft) {
    return entryState("review", "选择中", context.candidateCount, "已有本地选择草稿，等待补齐服务端要求字段。");
  }

  if (context.candidateCount > 0) {
    return entryState(
      "blocked",
      context.submissionGateStateLabel ?? "阻断",
      context.disabledCandidateCount,
      context.submissionGateReason ?? "服务端公开候选，但当前提交门或候选状态阻断。"
    );
  }

  return entryState("waiting", "无候选", 0, "当前服务端没有公开可提交候选。");
}

function disconnectedSlotState(
  slot: WireSidePanelSlot,
  context: OrchestrationContext
): Omit<WireSidePanelOrchestrationEntry, "groupLabel" | "href" | "label" | "order" | "slot"> {
  if (slot === "log" && context.eventCount > 0) {
    return entryState("history", "离线日志", context.eventCount, "连接未稳定，仅能查看已收到事件。");
  }

  if (slot === "timelineDetail" && context.hasTimelineDetail) {
    return entryState("review", "离线详情", 1, "连接未稳定，仅能查看当前详情。");
  }

  return entryState("offline", "离线", 0, "等待连接恢复后再提交行动。");
}

function entryState(
  state: WireSidePanelOrchestrationState,
  stateLabel: string,
  count: number,
  detail: string
): Omit<WireSidePanelOrchestrationEntry, "groupLabel" | "href" | "label" | "order" | "slot"> {
  return {
    count,
    detail,
    state,
    stateLabel,
    tone: toneForState(state)
  };
}

function primaryEntry(entries: WireSidePanelOrchestrationEntry[]): WireSidePanelOrchestrationEntry {
  const mainPaneEntries = entries.filter((entry) => entry.slot !== "serverFlow");
  // The server-flow pane is always visible in the right rail, so it should not steal the main work pane.
  return mainPaneEntries
    .slice()
    .sort((left, right) => stateWeight(right.state) - stateWeight(left.state) || left.order - right.order)[0]
    ?? entries
      .slice()
      .sort((left, right) => stateWeight(right.state) - stateWeight(left.state) || left.order - right.order)[0]
    ?? {
      count: 0,
      detail: "暂无右侧面板。",
      groupLabel: "窗口",
      href: "#",
      label: "无",
      order: 0,
      slot: "overview",
      state: "empty",
      stateLabel: "空",
      tone: "neutral"
    };
}

function stateWeight(state: WireSidePanelOrchestrationState): number {
  switch (state) {
    case "offline":
      return 90;
    case "blocked":
      return 80;
    case "ready":
      return 70;
    case "active":
      return 60;
    case "review":
      return 50;
    case "audit":
      return 35;
    case "history":
      return 30;
    case "waiting":
      return 20;
    case "empty":
      return 10;
  }
}

function toneForState(state: WireSidePanelOrchestrationState): WireSidePanelOrchestrationTone {
  switch (state) {
    case "blocked":
    case "offline":
      return "bad";
    case "ready":
      return "good";
    case "active":
    case "review":
      return "warn";
    case "audit":
    case "history":
      return "info";
    case "empty":
    case "waiting":
      return "neutral";
  }
}

function isActiveState(state: WireSidePanelOrchestrationState): boolean {
  return state === "active" || state === "blocked" || state === "offline" || state === "ready" || state === "review";
}

function connectionStable(status: ConnectionStatus): boolean {
  return status === "connected" || status === "resyncing";
}

function arrayLength(value: unknown): number {
  return Array.isArray(value) ? value.length : 0;
}

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" && !Array.isArray(value) ? value as Record<string, unknown> : {};
}

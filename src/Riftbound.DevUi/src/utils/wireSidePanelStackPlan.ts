import type { WireSidePanelSlot } from "../components/match/wireTableLayout";
import type { CommandSubmissionFeedback } from "../stores/useMatchController";
import type { WireSidePanelFocusPlan } from "./wireSidePanelFocusPlan";
import type {
  WireSidePanelOrchestrationEntry,
  WireSidePanelOrchestrationPlan,
  WireSidePanelOrchestrationState
} from "./wireSidePanelOrchestrationPlan";
import type { WireSidePanelRuleChainPlan } from "./wireSidePanelRuleChainPlan";

export type WireSidePanelStackRailKey = "focus" | "main" | "receipt" | "rules" | "status";

export type WireSidePanelStackRailMode = "expanded" | "hidden" | "summary";

export type WireSidePanelStackRailState = "empty" | "normal" | "primary" | "urgent";

export type WireSidePanelStackRailEntry = {
  actionLabel: string;
  actionSlot?: WireSidePanelSlot;
  key: WireSidePanelStackRailKey;
  label: string;
  mode: WireSidePanelStackRailMode;
  order: number;
  reason: string;
  slot?: WireSidePanelSlot;
  state: WireSidePanelStackRailState;
};

export type WireSidePanelStackPlan = {
  activeSlot: WireSidePanelSlot;
  activeSlotLabel: string;
  byRail: Record<WireSidePanelStackRailKey, WireSidePanelStackRailEntry>;
  entries: WireSidePanelStackRailEntry[];
  expandedCount: number;
  hiddenCount: number;
  state: WireSidePanelOrchestrationState;
  summary: string;
  summaryCount: number;
  visibleEntries: WireSidePanelStackRailEntry[];
};

export function buildWireSidePanelStackPlan({
  activeSlot,
  focusPlan,
  orchestration,
  ruleChainPlan,
  submissionFeedback
}: {
  activeSlot: WireSidePanelSlot;
  focusPlan: WireSidePanelFocusPlan;
  orchestration: WireSidePanelOrchestrationPlan;
  ruleChainPlan: WireSidePanelRuleChainPlan;
  submissionFeedback?: CommandSubmissionFeedback;
}): WireSidePanelStackPlan {
  const activeEntry = entryBySlot(orchestration.entries, activeSlot) ?? orchestration.entries[0];
  const entries: WireSidePanelStackRailEntry[] = [
    statusRail(orchestration),
    focusRail({ activeSlot, focusPlan }),
    rulesRail({ activeSlot, ruleChainPlan }),
    receiptRail({ activeSlot, submissionFeedback }),
    mainRail(activeEntry)
  ];
  const visibleEntries = entries.filter((entry) => entry.mode !== "hidden");
  const expandedCount = visibleEntries.filter((entry) => entry.mode === "expanded").length;
  const summaryCount = visibleEntries.filter((entry) => entry.mode === "summary").length;
  const hiddenCount = entries.length - visibleEntries.length;
  const byRail = Object.fromEntries(entries.map((entry) => [entry.key, entry])) as Record<
    WireSidePanelStackRailKey,
    WireSidePanelStackRailEntry
  >;

  return {
    activeSlot,
    activeSlotLabel: activeEntry?.label ?? activeSlot,
    byRail,
    entries,
    expandedCount,
    hiddenCount,
    state: orchestration.state,
    summary: `${activeEntry?.label ?? activeSlot} 展开；${summaryCount} 个摘要；${hiddenCount} 个隐藏。`,
    summaryCount,
    visibleEntries
  };
}

function statusRail(orchestration: WireSidePanelOrchestrationPlan): WireSidePanelStackRailEntry {
  return {
    actionLabel: "总览",
    actionSlot: "overview",
    key: "status",
    label: "状态",
    mode: "summary",
    order: 1,
    reason: orchestration.summary,
    state: urgentOrchestrationState(orchestration.state) ? "urgent" : "normal"
  };
}

function focusRail({
  activeSlot,
  focusPlan
}: {
  activeSlot: WireSidePanelSlot;
  focusPlan: WireSidePanelFocusPlan;
}): WireSidePanelStackRailEntry {
  if (!focusPlan.visible) {
    return {
      actionLabel: "选择",
      key: "focus",
      label: "焦点",
      mode: "hidden",
      order: 2,
      reason: "没有选中对象。",
      state: "empty"
    };
  }

  const expanded = activeSlot === "interaction";
  return {
    actionLabel: expanded ? "已展开" : "焦点",
    actionSlot: "interaction",
    key: "focus",
    label: "焦点",
    mode: expanded ? "expanded" : "summary",
    order: 2,
    reason: focusPlan.nextStepLabel,
    slot: "interaction",
    state: expanded ? "primary" : "normal"
  };
}

function rulesRail({
  activeSlot,
  ruleChainPlan
}: {
  activeSlot: WireSidePanelSlot;
  ruleChainPlan: WireSidePanelRuleChainPlan;
}): WireSidePanelStackRailEntry {
  const expanded = activeSlot === "ruleQueue" || activeSlot === "timelineDetail" || activeSlot === "serverFlow";
  const idle = ruleChainPlan.state === "idle";
  if (idle && !expanded) {
    return {
      actionLabel: "队列",
      key: "rules",
      label: "规则链",
      mode: "hidden",
      order: 3,
      reason: ruleChainPlan.nextStepLabel,
      state: "empty"
    };
  }

  return {
    actionLabel: expanded ? "已展开" : "队列",
    actionSlot: "ruleQueue",
    key: "rules",
    label: "规则链",
    mode: expanded ? "expanded" : "summary",
    order: 3,
    reason: ruleChainPlan.nextStepLabel,
    slot: "ruleQueue",
    state: expanded ? "primary" : urgentRuleState(ruleChainPlan.state) ? "urgent" : "normal"
  };
}

function receiptRail({
  activeSlot,
  submissionFeedback
}: {
  activeSlot: WireSidePanelSlot;
  submissionFeedback?: CommandSubmissionFeedback;
}): WireSidePanelStackRailEntry {
  if (!submissionFeedback) {
    return {
      actionLabel: "回执",
      key: "receipt",
      label: "回执",
      mode: "hidden",
      order: 4,
      reason: "尚未提交命令。",
      state: "empty"
    };
  }

  const urgent = submissionFeedback.state === "failed" || submissionFeedback.state === "submitting";
  const expanded = urgent || activeSlot === "commandCenter" || activeSlot === "actionMap" || activeSlot === "actionPrompt";
  return {
    actionLabel: expanded ? "已展开" : "回执",
    actionSlot: "commandCenter",
    key: "receipt",
    label: "回执",
    mode: expanded ? "expanded" : "summary",
    order: 4,
    reason: submissionFeedback.message,
    slot: "commandCenter",
    state: urgent ? "urgent" : expanded ? "primary" : "normal"
  };
}

function mainRail(activeEntry?: WireSidePanelOrchestrationEntry): WireSidePanelStackRailEntry {
  return {
    actionLabel: "当前",
    actionSlot: activeEntry?.slot,
    key: "main",
    label: "主面板",
    mode: "expanded",
    order: 5,
    reason: activeEntry?.detail ?? "显示当前右侧主入口。",
    slot: activeEntry?.slot,
    state: "primary"
  };
}

function entryBySlot(
  entries: readonly WireSidePanelOrchestrationEntry[],
  slot: WireSidePanelSlot
): WireSidePanelOrchestrationEntry | undefined {
  return entries.find((entry) => entry.slot === slot);
}

function urgentOrchestrationState(state: WireSidePanelOrchestrationState): boolean {
  return state === "blocked" || state === "offline" || state === "ready";
}

function urgentRuleState(state: WireSidePanelRuleChainPlan["state"]): boolean {
  return state === "stack-response" || state === "task-blocked" || state === "task-open" || state === "trigger-pending";
}

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

export type WireSidePanelStackDensity = "balanced" | "crowded" | "quiet" | "urgent";

export type WireSidePanelStackRailBodyMode = "collapsed" | "compact" | "full";

export type WireSidePanelStackRailMode = "expanded" | "hidden" | "summary";

export type WireSidePanelStackRailPriority = "background" | "context" | "primary" | "urgent";

export type WireSidePanelStackRailState = "empty" | "normal" | "primary" | "urgent";

export type WireSidePanelStackRailEntry = {
  actionLabel: string;
  actionSlot?: WireSidePanelSlot;
  bodyPreference?: WireSidePanelStackRailBodyMode;
  bodyMode: WireSidePanelStackRailBodyMode;
  capacityWeight: number;
  key: WireSidePanelStackRailKey;
  label: string;
  mode: WireSidePanelStackRailMode;
  order: number;
  priority: WireSidePanelStackRailPriority;
  reason: string;
  slot?: WireSidePanelSlot;
  state: WireSidePanelStackRailState;
};

export type WireSidePanelStackPlan = {
  activeSlot: WireSidePanelSlot;
  activeSlotLabel: string;
  byRail: Record<WireSidePanelStackRailKey, WireSidePanelStackRailEntry>;
  capacityMaxWeight: number;
  capacityOverflow: boolean;
  capacityWeight: number;
  density: WireSidePanelStackDensity;
  entries: WireSidePanelStackRailEntry[];
  expandedCount: number;
  hiddenCount: number;
  renderedBodyCount: number;
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
  const rawEntries: WireSidePanelStackRailEntry[] = [
    statusRail(orchestration),
    focusRail({ activeSlot, focusPlan }),
    rulesRail({ activeSlot, ruleChainPlan }),
    receiptRail({ activeSlot, submissionFeedback }),
    mainRail(activeEntry)
  ];
  const capacity = applyRailCapacity(rawEntries);
  const entries = capacity.entries;
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
    capacityMaxWeight: capacity.maxWeight,
    capacityOverflow: capacity.overflow,
    capacityWeight: capacity.weight,
    density: capacity.density,
    entries,
    expandedCount,
    hiddenCount,
    renderedBodyCount: capacity.renderedBodyCount,
    state: orchestration.state,
    summary: `${activeEntry?.label ?? activeSlot} 展开；${summaryCount} 个摘要；${hiddenCount} 个隐藏；${capacity.density} 密度。`,
    summaryCount,
    visibleEntries
  };
}

function statusRail(orchestration: WireSidePanelOrchestrationPlan): WireSidePanelStackRailEntry {
  return {
    actionLabel: "总览",
    actionSlot: "overview",
    bodyMode: "collapsed",
    capacityWeight: 0,
    key: "status",
    label: "状态",
    mode: "summary",
    order: 1,
    priority: "background",
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
      bodyMode: "collapsed",
      capacityWeight: 0,
      key: "focus",
      label: "焦点",
      mode: "hidden",
      order: 2,
      priority: "context",
      reason: "没有选中对象。",
      state: "empty"
    };
  }

  const expanded = activeSlot === "interaction";
  const commandSurfaceCompact = activeSlot === "actionMap" || activeSlot === "actionPrompt";
  return {
    actionLabel: expanded ? "已展开" : "焦点",
    actionSlot: "interaction",
    bodyPreference: commandSurfaceCompact && !expanded ? "compact" : undefined,
    bodyMode: "collapsed",
    capacityWeight: 0,
    key: "focus",
    label: "焦点",
    mode: expanded ? "expanded" : "summary",
    order: 2,
    priority: expanded ? "primary" : "context",
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
  const expanded = activeSlot === "ruleQueue" || activeSlot === "serverFlow";
  const idle = ruleChainPlan.state === "idle";
  if (idle && !expanded) {
    return {
      actionLabel: "队列",
      bodyMode: "collapsed",
      capacityWeight: 0,
      key: "rules",
      label: "规则链",
      mode: "hidden",
      order: 3,
      priority: "context",
      reason: ruleChainPlan.nextStepLabel,
      state: "empty"
    };
  }

  return {
    actionLabel: expanded ? "已展开" : "队列",
    actionSlot: "ruleQueue",
    bodyMode: "collapsed",
    capacityWeight: 0,
    key: "rules",
    label: "规则链",
    mode: expanded ? "expanded" : "summary",
    order: 3,
    priority: expanded ? "primary" : urgentRuleState(ruleChainPlan.state) ? "urgent" : "context",
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
  const commandReceiptSurface = isReceiptSurface(activeSlot);
  if (!submissionFeedback && !commandReceiptSurface) {
    return {
      actionLabel: "回执",
      bodyMode: "collapsed",
      capacityWeight: 0,
      key: "receipt",
      label: "回执",
      mode: "hidden",
      order: 4,
      priority: "context",
      reason: "尚未提交命令。",
      state: "empty"
    };
  }

  const urgent = submissionFeedback?.state === "failed" || submissionFeedback?.state === "submitting";
  const expanded = Boolean(submissionFeedback) && (urgent || commandReceiptSurface);
  return {
    actionLabel: expanded ? "已展开" : "回执",
    actionSlot: "commandCenter",
    bodyPreference: !submissionFeedback && commandReceiptSurface ? "compact" : undefined,
    bodyMode: "collapsed",
    capacityWeight: 0,
    key: "receipt",
    label: "回执",
    mode: expanded ? "expanded" : "summary",
    order: 4,
    priority: urgent ? "urgent" : expanded ? "primary" : "context",
    reason: submissionFeedback?.message ?? "尚未提交命令。",
    slot: "commandCenter",
    state: urgent ? "urgent" : submissionFeedback ? expanded ? "primary" : "normal" : "empty"
  };
}

function mainRail(activeEntry?: WireSidePanelOrchestrationEntry): WireSidePanelStackRailEntry {
  return {
    actionLabel: "当前",
    actionSlot: activeEntry?.slot,
    bodyMode: "collapsed",
    capacityWeight: 0,
    key: "main",
    label: "主面板",
    mode: "expanded",
    order: 5,
    priority: "primary",
    reason: activeEntry?.detail ?? "显示当前右侧主入口。",
    slot: activeEntry?.slot,
    state: "primary"
  };
}

function applyRailCapacity(entries: WireSidePanelStackRailEntry[]): {
  density: WireSidePanelStackDensity;
  entries: WireSidePanelStackRailEntry[];
  maxWeight: number;
  overflow: boolean;
  renderedBodyCount: number;
  weight: number;
} {
  const visible = entries.filter((entry) => entry.mode !== "hidden");
  const density = railDensity(visible);
  const normalized = entries.map((entry) => {
    const bodyMode = bodyModeForEntry(entry, density, visible.length);
    return {
      ...entry,
      bodyMode,
      capacityWeight: capacityWeightFor(bodyMode, entry),
      priority: priorityForEntry(entry)
    };
  });
  const maxWeight = 9;
  const weight = normalized.reduce((total, entry) => total + entry.capacityWeight, 0);
  const renderedBodyCount = normalized.filter((entry) => entry.mode !== "hidden" && entry.bodyMode !== "collapsed").length;
  return {
    density,
    entries: normalized,
    maxWeight,
    overflow: weight > maxWeight,
    renderedBodyCount,
    weight
  };
}

function railDensity(visible: readonly WireSidePanelStackRailEntry[]): WireSidePanelStackDensity {
  if (visible.some((entry) => entry.key !== "status" && entry.state === "urgent")) {
    return "urgent";
  }

  const expandedCount = visible.filter((entry) => entry.mode === "expanded").length;
  const summaryCount = visible.filter((entry) => entry.mode === "summary").length;
  if (visible.length >= 4 || expandedCount >= 2) {
    return "crowded";
  }

  if (visible.length >= 3 || summaryCount >= 2) {
    return "balanced";
  }

  return "quiet";
}

function bodyModeForEntry(
  entry: WireSidePanelStackRailEntry,
  density: WireSidePanelStackDensity,
  visibleCount: number
): WireSidePanelStackRailBodyMode {
  if (entry.mode === "hidden") {
    return "collapsed";
  }

  if (entry.key === "main") {
    return "full";
  }

  if (entry.bodyPreference) {
    return entry.bodyPreference;
  }

  if (entry.mode === "expanded") {
    return "full";
  }

  if (entry.key === "status") {
    return density === "quiet" || density === "balanced" && visibleCount <= 3 ? "compact" : "collapsed";
  }

  if (entry.state === "urgent") {
    return "compact";
  }

  return "collapsed";
}

function capacityWeightFor(
  bodyMode: WireSidePanelStackRailBodyMode,
  entry: WireSidePanelStackRailEntry
): number {
  if (entry.mode === "hidden") {
    return 0;
  }

  switch (bodyMode) {
    case "collapsed":
      return 1;
    case "compact":
      return 2;
    case "full":
      return entry.key === "main" ? 4 : 3;
  }
}

function priorityForEntry(entry: WireSidePanelStackRailEntry): WireSidePanelStackRailPriority {
  if (entry.state === "urgent") {
    return "urgent";
  }

  if (entry.mode === "expanded" || entry.state === "primary") {
    return "primary";
  }

  if (entry.key === "status") {
    return "background";
  }

  return "context";
}

function entryBySlot(
  entries: readonly WireSidePanelOrchestrationEntry[],
  slot: WireSidePanelSlot
): WireSidePanelOrchestrationEntry | undefined {
  return entries.find((entry) => entry.slot === slot);
}

function isReceiptSurface(activeSlot: WireSidePanelSlot): boolean {
  return activeSlot === "commandCenter" || activeSlot === "actionMap" || activeSlot === "actionPrompt" || activeSlot === "timelineDetail";
}

function urgentOrchestrationState(state: WireSidePanelOrchestrationState): boolean {
  return state === "blocked" || state === "offline" || state === "ready";
}

function urgentRuleState(state: WireSidePanelRuleChainPlan["state"]): boolean {
  return state === "stack-response" || state === "task-blocked" || state === "task-open" || state === "trigger-pending";
}

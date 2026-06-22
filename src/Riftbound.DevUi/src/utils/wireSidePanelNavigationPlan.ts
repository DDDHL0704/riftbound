import type { WireSidePanelSlot } from "../components/match/wireTableLayout";
import type { WireSidePanelDirectoryViewTabSpec } from "./wireSidePanelDirectoryViewPlan";
import type {
  WireSidePanelOrchestrationEntry,
  WireSidePanelOrchestrationState
} from "./wireSidePanelOrchestrationPlan";

export type WireSidePanelNavigationSource = "auto" | "control-route" | "directory" | "rail" | "restore" | "tab";

export type WireSidePanelTransitionPlan<TTab extends string = string> = {
  actionLabel: string;
  alreadyActive: boolean;
  fromSlot: WireSidePanelSlot;
  fromSlotLabel: string;
  fromTab: TTab;
  fromTabLabel: string;
  reason: string;
  selectable: boolean;
  source: WireSidePanelNavigationSource;
  tabChanges: boolean;
  targetSlot: WireSidePanelSlot;
  targetSlotLabel: string;
  targetTab: TTab;
  targetTabLabel: string;
};

export function buildWireSidePanelTransitionPlan<TTab extends string>({
  activeSlot,
  entries,
  primarySlot,
  source,
  tabs,
  targetSlot,
  targetTab
}: {
  activeSlot: WireSidePanelSlot;
  entries: readonly WireSidePanelOrchestrationEntry[];
  primarySlot: WireSidePanelSlot;
  source: WireSidePanelNavigationSource;
  tabs: readonly WireSidePanelDirectoryViewTabSpec<TTab>[];
  targetSlot?: WireSidePanelSlot;
  targetTab?: TTab;
}): WireSidePanelTransitionPlan<TTab> {
  const entryBySlot = new Map(entries.map((entry) => [entry.slot, entry]));
  const { slotToTab, tabById } = indexTabs(tabs, entryBySlot);
  const activeEntry = requiredEntry(entryBySlot, activeSlot);
  const currentTab = requiredTabForSlot(slotToTab, activeSlot);
  const currentTabSpec = requiredTab(tabById, currentTab);
  const resolvedTargetSlot = targetSlot ?? preferredWireSidePanelSlotForTab(
    requiredTargetTab(tabById, targetTab),
    Object.fromEntries(entryBySlot) as Record<WireSidePanelSlot, WireSidePanelOrchestrationEntry>,
    primarySlot
  );
  const resolvedTargetTab = requiredTabForSlot(slotToTab, resolvedTargetSlot);

  if (targetTab && resolvedTargetTab !== targetTab) {
    throw new Error(`Wire side panel target slot ${resolvedTargetSlot} does not belong to requested tab ${targetTab}`);
  }

  const targetEntry = requiredEntry(entryBySlot, resolvedTargetSlot);
  const targetTabSpec = requiredTab(tabById, resolvedTargetTab);
  const alreadyActive = activeSlot === resolvedTargetSlot;
  const tabChanges = currentTab !== resolvedTargetTab;

  return {
    actionLabel: actionLabelFor(source, alreadyActive, tabChanges),
    alreadyActive,
    fromSlot: activeSlot,
    fromSlotLabel: activeEntry.label,
    fromTab: currentTab,
    fromTabLabel: currentTabSpec.label,
    reason: reasonFor({
      alreadyActive,
      currentTabLabel: currentTabSpec.label,
      source,
      tabChanges,
      targetSlotLabel: targetEntry.label,
      targetTabLabel: targetTabSpec.label
    }),
    selectable: !alreadyActive,
    source,
    tabChanges,
    targetSlot: resolvedTargetSlot,
    targetSlotLabel: targetEntry.label,
    targetTab: resolvedTargetTab,
    targetTabLabel: targetTabSpec.label
  };
}

export function preferredWireSidePanelSlotForTab(
  tab: WireSidePanelDirectoryViewTabSpec<string>,
  entryBySlot: Record<WireSidePanelSlot, WireSidePanelOrchestrationEntry | undefined>,
  primarySlot: WireSidePanelSlot
): WireSidePanelSlot {
  if (tab.slots.includes(primarySlot)) {
    return primarySlot;
  }

  return tab.slots.find((slot) => isStickyWireSidePanelState(entryBySlot[slot]?.state))
    ?? tab.primarySlot;
}

export function isStickyWireSidePanelState(state: WireSidePanelOrchestrationState | string | undefined): boolean {
  return state === "active" || state === "blocked" || state === "ready" || state === "review";
}

function indexTabs<TTab extends string>(
  tabs: readonly WireSidePanelDirectoryViewTabSpec<TTab>[],
  entryBySlot: ReadonlyMap<WireSidePanelSlot, WireSidePanelOrchestrationEntry>
): {
  slotToTab: Map<WireSidePanelSlot, TTab>;
  tabById: Map<TTab, WireSidePanelDirectoryViewTabSpec<TTab>>;
} {
  const slotToTab = new Map<WireSidePanelSlot, TTab>();
  const tabById = new Map<TTab, WireSidePanelDirectoryViewTabSpec<TTab>>();

  for (const tab of tabs) {
    if (tabById.has(tab.id)) {
      throw new Error(`Duplicate wire side panel navigation tab: ${tab.id}`);
    }
    tabById.set(tab.id, tab);

    for (const slot of tab.slots) {
      if (!entryBySlot.has(slot)) {
        throw new Error(`Wire side panel navigation tab references missing slot: ${slot}`);
      }
      if (slotToTab.has(slot)) {
        throw new Error(`Wire side panel navigation slot appears in multiple tabs: ${slot}`);
      }
      slotToTab.set(slot, tab.id);
    }
  }

  return { slotToTab, tabById };
}

function requiredEntry(
  entryBySlot: ReadonlyMap<WireSidePanelSlot, WireSidePanelOrchestrationEntry>,
  slot: WireSidePanelSlot
): WireSidePanelOrchestrationEntry {
  const entry = entryBySlot.get(slot);
  if (!entry) {
    throw new Error(`Wire side panel navigation slot is not registered: ${slot}`);
  }
  return entry;
}

function requiredTab<TTab extends string>(
  tabById: ReadonlyMap<TTab, WireSidePanelDirectoryViewTabSpec<TTab>>,
  tabId: TTab
): WireSidePanelDirectoryViewTabSpec<TTab> {
  const tab = tabById.get(tabId);
  if (!tab) {
    throw new Error(`Wire side panel navigation tab is not registered: ${tabId}`);
  }
  return tab;
}

function requiredTargetTab<TTab extends string>(
  tabById: ReadonlyMap<TTab, WireSidePanelDirectoryViewTabSpec<TTab>>,
  targetTab: TTab | undefined
): WireSidePanelDirectoryViewTabSpec<TTab> {
  if (!targetTab) {
    throw new Error("Wire side panel navigation requires targetSlot or targetTab");
  }
  return requiredTab(tabById, targetTab);
}

function requiredTabForSlot<TTab extends string>(
  slotToTab: ReadonlyMap<WireSidePanelSlot, TTab>,
  slot: WireSidePanelSlot
): TTab {
  const tab = slotToTab.get(slot);
  if (!tab) {
    throw new Error(`Wire side panel navigation slot is not assigned to a tab: ${slot}`);
  }
  return tab;
}

function actionLabelFor(
  source: WireSidePanelNavigationSource,
  alreadyActive: boolean,
  tabChanges: boolean
): string {
  if (alreadyActive) {
    return "当前";
  }
  if (source === "tab") {
    return tabChanges ? "切换" : "定位";
  }
  return "转到";
}

function reasonFor({
  alreadyActive,
  currentTabLabel,
  source,
  tabChanges,
  targetSlotLabel,
  targetTabLabel
}: {
  alreadyActive: boolean;
  currentTabLabel: string;
  source: WireSidePanelNavigationSource;
  tabChanges: boolean;
  targetSlotLabel: string;
  targetTabLabel: string;
}): string {
  if (alreadyActive) {
    return `已在 ${targetSlotLabel}。`;
  }

  if (tabChanges) {
    return `${sourceLabel(source)}：${currentTabLabel} -> ${targetTabLabel} / ${targetSlotLabel}。`;
  }

  return `${sourceLabel(source)}：定位到 ${targetSlotLabel}。`;
}

function sourceLabel(source: WireSidePanelNavigationSource): string {
  switch (source) {
    case "auto":
      return "自动导航";
    case "control-route":
      return "控制路由";
    case "directory":
      return "目录入口";
    case "rail":
      return "摘要栏";
    case "restore":
      return "返回入口";
    case "tab":
      return "页签入口";
  }
}

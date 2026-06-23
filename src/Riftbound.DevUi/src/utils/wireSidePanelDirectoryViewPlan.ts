import type { WireSidePanelSlot } from "../components/match/wireTableLayout";
import type {
  WireSidePanelDirectoryGroup,
  WireSidePanelDirectoryPlan
} from "./wireSidePanelDirectoryPlan";
import type {
  WireSidePanelOrchestrationEntry,
  WireSidePanelOrchestrationState
} from "./wireSidePanelOrchestrationPlan";

export type WireSidePanelDirectoryViewTabSpec<TTab extends string = string> = {
  id: TTab;
  label: string;
  primarySlot: WireSidePanelSlot;
  slots: readonly WireSidePanelSlot[];
};

export type WireSidePanelDirectoryDensity = "balanced" | "dense" | "quiet";

export type WireSidePanelDirectoryIndexMode = "compact" | "full";

export type WireSidePanelDirectoryViewEntry<TTab extends string = string> =
  WireSidePanelOrchestrationEntry & {
    active: boolean;
    primary: boolean;
    tabId: TTab;
  };

export type WireSidePanelDirectoryViewTab<TTab extends string = string> = {
  active: boolean;
  count: number;
  id: TTab;
  label: string;
  primarySlot: WireSidePanelSlot;
  state: WireSidePanelOrchestrationState;
  stateLabel: string;
  urgent: boolean;
};

export type WireSidePanelDirectoryViewPlan<TTab extends string = string> = {
  activeEntry: WireSidePanelDirectoryViewEntry<TTab>;
  currentTab: WireSidePanelDirectoryViewTab<TTab>;
  density: WireSidePanelDirectoryDensity;
  hiddenCount: number;
  hiddenEntries: WireSidePanelDirectoryViewEntry<TTab>[];
  indexMode: WireSidePanelDirectoryIndexMode;
  primaryEntry: WireSidePanelDirectoryViewEntry<TTab>;
  tabs: WireSidePanelDirectoryViewTab<TTab>[];
  visibleEntries: WireSidePanelDirectoryViewEntry<TTab>[];
};

export type WireSidePanelDirectoryLayer<TTab extends string = string> = {
  count: number;
  entries: WireSidePanelDirectoryViewEntry<TTab>[];
  group: WireSidePanelDirectoryGroup;
  key: WireSidePanelDirectoryGroup;
  label: string;
};

export function buildWireSidePanelDirectoryViewPlan<TTab extends string>({
  activeSlot,
  activeTab,
  entries,
  tabs
}: {
  activeSlot: WireSidePanelSlot;
  activeTab: TTab;
  entries: readonly WireSidePanelOrchestrationEntry[];
  tabs: readonly WireSidePanelDirectoryViewTabSpec<TTab>[];
}): WireSidePanelDirectoryViewPlan<TTab> {
  const entryBySlot = new Map(entries.map((entry) => [entry.slot, entry]));
  const slotToTab = new Map<WireSidePanelSlot, TTab>();
  const tabById = new Map<TTab, WireSidePanelDirectoryViewTabSpec<TTab>>();

  for (const tab of tabs) {
    if (tabById.has(tab.id)) {
      throw new Error(`Duplicate wire side panel tab: ${tab.id}`);
    }
    tabById.set(tab.id, tab);

    for (const slot of tab.slots) {
      if (!entryBySlot.has(slot)) {
        throw new Error(`Wire side panel tab references missing slot: ${slot}`);
      }
      if (slotToTab.has(slot)) {
        throw new Error(`Wire side panel slot appears in multiple tabs: ${slot}`);
      }
      slotToTab.set(slot, tab.id);
    }
  }

  const currentTabSpec = tabById.get(activeTab);
  if (!currentTabSpec) {
    throw new Error(`Active wire side panel tab is not registered: ${activeTab}`);
  }

  const activeBaseEntry = entryBySlot.get(activeSlot);
  if (!activeBaseEntry) {
    throw new Error(`Active wire side panel slot is not in orchestration entries: ${activeSlot}`);
  }

  const tabEntries = currentTabSpec.slots.map((slot) => viewEntry({
    activeSlot,
    entry: requiredEntry(entryBySlot, slot),
    primarySlot: currentTabSpec.primarySlot,
    tabId: activeTab
  }));
  const tabsView = tabs.map((tab) => tabView(tab, activeTab, entryBySlot));
  const density = directoryDensity({
    tabCount: tabsView.length,
    visibleCount: tabEntries.length
  });
  const visibleEntries = density === "dense"
    ? compactVisibleEntries(tabEntries)
    : tabEntries;
  const visibleSlotSet = new Set(visibleEntries.map((entry) => entry.slot));
  const hiddenEntries = entries
    .filter((entry) => !visibleSlotSet.has(entry.slot))
    .map((entry) => viewEntry({
      activeSlot,
      entry,
      primarySlot: currentTabSpec.primarySlot,
      tabId: requiredTabId(slotToTab, entry.slot)
    }));
  const primaryEntry = tabEntries.find((entry) => entry.slot === currentTabSpec.primarySlot)
    ?? tabEntries[0]
    ?? viewEntry({
      activeSlot,
      entry: activeBaseEntry,
      primarySlot: activeBaseEntry.slot,
      tabId: activeTab
    });

  return {
    activeEntry: viewEntry({
      activeSlot,
      entry: activeBaseEntry,
      primarySlot: currentTabSpec.primarySlot,
      tabId: activeTab
    }),
    currentTab: requiredTabView(tabsView, activeTab),
    density,
    hiddenCount: hiddenEntries.length,
    hiddenEntries,
    indexMode: density === "dense" ? "compact" : "full",
    primaryEntry,
    tabs: tabsView,
    visibleEntries
  };
}

export function buildWireSidePanelDirectoryLayerPlan<TTab extends string>({
  directory,
  view
}: {
  directory: WireSidePanelDirectoryPlan;
  view: WireSidePanelDirectoryViewPlan<TTab>;
}): WireSidePanelDirectoryLayer<TTab>[] {
  const layers = new Map<WireSidePanelDirectoryGroup, WireSidePanelDirectoryLayer<TTab>>();

  for (const entry of view.visibleEntries) {
    const directoryEntry = directory.bySlot[entry.slot];
    let layer = layers.get(directoryEntry.group);
    if (!layer) {
      layer = {
        count: 0,
        entries: [],
        group: directoryEntry.group,
        key: directoryEntry.group,
        label: directoryEntry.groupLabel
      };
      layers.set(directoryEntry.group, layer);
    }

    layer.entries.push(entry);
    layer.count += 1;
  }

  return Array.from(layers.values());
}

function viewEntry<TTab extends string>({
  activeSlot,
  entry,
  primarySlot,
  tabId
}: {
  activeSlot: WireSidePanelSlot;
  entry: WireSidePanelOrchestrationEntry;
  primarySlot: WireSidePanelSlot;
  tabId: TTab;
}): WireSidePanelDirectoryViewEntry<TTab> {
  return {
    ...entry,
    active: entry.slot === activeSlot,
    primary: entry.slot === primarySlot,
    tabId
  };
}

function compactVisibleEntries<TTab extends string>(
  entries: readonly WireSidePanelDirectoryViewEntry<TTab>[]
): WireSidePanelDirectoryViewEntry<TTab>[] {
  const maxVisibleEntries = 3;
  if (entries.length <= maxVisibleEntries) {
    return [...entries];
  }

  const keep = new Set<WireSidePanelSlot>();
  const addEntry = (entry: WireSidePanelDirectoryViewEntry<TTab> | undefined) => {
    if (entry && keep.size < maxVisibleEntries) {
      keep.add(entry.slot);
    }
  };

  entries.filter((entry) => entry.active || entry.primary).forEach(addEntry);
  entries
    .filter((entry) => urgentDirectoryEntry(entry))
    .slice()
    .sort((left, right) => directoryUrgencyWeight(right.state) - directoryUrgencyWeight(left.state) || left.order - right.order)
    .forEach(addEntry);
  entries.forEach(addEntry);

  return entries.filter((entry) => keep.has(entry.slot));
}

function urgentDirectoryEntry(entry: WireSidePanelDirectoryViewEntry<string>): boolean {
  return entry.state === "blocked" || entry.state === "offline" || entry.state === "ready";
}

function directoryUrgencyWeight(state: WireSidePanelOrchestrationState): number {
  switch (state) {
    case "offline":
      return 4;
    case "blocked":
      return 3;
    case "ready":
      return 2;
    default:
      return 1;
  }
}

function tabView<TTab extends string>(
  tab: WireSidePanelDirectoryViewTabSpec<TTab>,
  activeTab: TTab,
  entryBySlot: Map<WireSidePanelSlot, WireSidePanelOrchestrationEntry>
): WireSidePanelDirectoryViewTab<TTab> {
  const tabEntries = tab.slots.map((slot) => requiredEntry(entryBySlot, slot));
  const primaryEntry = entryBySlot.get(tab.primarySlot) ?? tabEntries[0];
  const urgentEntry = tabEntries.find((entry) => entry.state === "blocked" || entry.state === "offline" || entry.state === "ready");

  return {
    active: tab.id === activeTab,
    count: tabEntries.reduce((total, entry) => total + entry.count, 0),
    id: tab.id,
    label: tab.label,
    primarySlot: tab.primarySlot,
    state: urgentEntry?.state ?? primaryEntry.state,
    stateLabel: urgentEntry?.stateLabel ?? primaryEntry.stateLabel,
    urgent: Boolean(urgentEntry)
  };
}

function requiredEntry(
  entryBySlot: Map<WireSidePanelSlot, WireSidePanelOrchestrationEntry>,
  slot: WireSidePanelSlot
): WireSidePanelOrchestrationEntry {
  const entry = entryBySlot.get(slot);
  if (!entry) {
    throw new Error(`Wire side panel slot is not in orchestration entries: ${slot}`);
  }
  return entry;
}

function requiredTabId<TTab extends string>(
  slotToTab: Map<WireSidePanelSlot, TTab>,
  slot: WireSidePanelSlot
): TTab {
  const tabId = slotToTab.get(slot);
  if (!tabId) {
    throw new Error(`Wire side panel slot is not assigned to a tab: ${slot}`);
  }
  return tabId;
}

function requiredTabView<TTab extends string>(
  tabs: readonly WireSidePanelDirectoryViewTab<TTab>[],
  activeTab: TTab
): WireSidePanelDirectoryViewTab<TTab> {
  const tab = tabs.find((item) => item.id === activeTab);
  if (!tab) {
    throw new Error(`Active wire side panel tab view is not registered: ${activeTab}`);
  }
  return tab;
}

function directoryDensity({
  tabCount,
  visibleCount
}: {
  tabCount: number;
  visibleCount: number;
}): WireSidePanelDirectoryDensity {
  if (visibleCount >= 4 || tabCount >= 5) {
    return "dense";
  }

  if (visibleCount >= 3 || tabCount >= 4) {
    return "balanced";
  }

  return "quiet";
}

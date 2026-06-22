import type { WireSidePanelSlot } from "../components/match/wireTableLayout";
import type {
  WireSidePanelDirectoryViewEntry,
  WireSidePanelDirectoryViewPlan
} from "./wireSidePanelDirectoryViewPlan";
import type {
  WireSidePanelOrchestrationEntry,
  WireSidePanelOrchestrationPlan,
  WireSidePanelOrchestrationState,
  WireSidePanelOrchestrationTone
} from "./wireSidePanelOrchestrationPlan";

export type WireSidePanelControlRouteKey = "active" | "hidden" | "primary" | "urgent";

export type WireSidePanelControlRoute = {
  actionLabel: string;
  count: number;
  detail: string;
  key: WireSidePanelControlRouteKey;
  label: string;
  selectable: boolean;
  slot?: WireSidePanelSlot;
  slotLabel: string;
  state: WireSidePanelOrchestrationState;
  stateLabel: string;
  tone: WireSidePanelOrchestrationTone;
};

export type WireSidePanelControlPlan = {
  activeRoute: WireSidePanelControlRoute;
  hiddenRoute?: WireSidePanelControlRoute;
  primaryRoute: WireSidePanelControlRoute;
  routeCount: number;
  routes: WireSidePanelControlRoute[];
  state: WireSidePanelOrchestrationState;
  stateLabel: string;
  summary: string;
  urgentRoute?: WireSidePanelControlRoute;
};

export function buildWireSidePanelControlPlan<TTab extends string>({
  orchestration,
  view
}: {
  orchestration: WireSidePanelOrchestrationPlan;
  view: WireSidePanelDirectoryViewPlan<TTab>;
}): WireSidePanelControlPlan {
  const primaryEntry = entryBySlot(view, orchestration.primarySlot) ?? view.primaryEntry;
  const urgentEntry = urgencyEntry(orchestration.entries);
  const hiddenEntry = hiddenEntryFor(view.hiddenEntries, new Set([
    view.activeEntry.slot,
    primaryEntry.slot,
    urgentEntry?.slot
  ].filter(Boolean) as WireSidePanelSlot[]));
  const activeRoute = routeFromEntry("active", view.activeEntry, {
    actionLabel: "当前",
    selectable: false
  });
  const primaryRoute = routeFromEntry("primary", primaryEntry, {
    actionLabel: "转到主入口",
    selectable: primaryEntry.slot !== view.activeEntry.slot
  });
  const urgentRoute = urgentEntry
    ? routeFromEntry("urgent", urgentEntry, {
      actionLabel: urgentEntry.slot === view.activeEntry.slot ? "正在处理" : "处理紧急",
      selectable: urgentEntry.slot !== view.activeEntry.slot
    })
    : undefined;
  const hiddenRoute = hiddenEntry
    ? routeFromEntry("hidden", hiddenEntry, {
      actionLabel: hiddenEntry.slot === view.activeEntry.slot ? "已展开" : "查看隐藏",
      selectable: hiddenEntry.slot !== view.activeEntry.slot
    })
    : undefined;
  const routes = uniqueRoutes([
    activeRoute,
    primaryRoute,
    urgentRoute,
    hiddenRoute
  ]);

  return {
    activeRoute,
    hiddenRoute,
    primaryRoute,
    routeCount: routes.length,
    routes,
    state: orchestration.state,
    stateLabel: orchestration.stateLabel,
    summary: summaryFor({ hiddenRoute, primaryRoute, urgentRoute }),
    urgentRoute
  };
}

function routeFromEntry(
  key: WireSidePanelControlRouteKey,
  entry: WireSidePanelOrchestrationEntry,
  {
    actionLabel,
    selectable
  }: {
    actionLabel: string;
    selectable: boolean;
  }
): WireSidePanelControlRoute {
  return {
    actionLabel,
    count: entry.count,
    detail: entry.detail,
    key,
    label: routeLabel(key),
    selectable,
    slot: entry.slot,
    slotLabel: entry.label,
    state: entry.state,
    stateLabel: entry.stateLabel,
    tone: entry.tone
  };
}

function routeLabel(key: WireSidePanelControlRouteKey): string {
  switch (key) {
    case "active":
      return "当前页";
    case "hidden":
      return "隐藏入口";
    case "primary":
      return "主入口";
    case "urgent":
      return "紧急入口";
  }
}

function summaryFor({
  hiddenRoute,
  primaryRoute,
  urgentRoute
}: {
  hiddenRoute?: WireSidePanelControlRoute;
  primaryRoute: WireSidePanelControlRoute;
  urgentRoute?: WireSidePanelControlRoute;
}): string {
  if (urgentRoute && urgentRoute.slot !== primaryRoute.slot) {
    return `${urgentRoute.slotLabel} / ${urgentRoute.stateLabel}；主入口 ${primaryRoute.slotLabel}。`;
  }

  if (hiddenRoute) {
    return `${primaryRoute.slotLabel} / ${primaryRoute.stateLabel}；另有隐藏入口 ${hiddenRoute.slotLabel}。`;
  }

  return `${primaryRoute.slotLabel} / ${primaryRoute.stateLabel}。`;
}

function entryBySlot<TTab extends string>(
  view: WireSidePanelDirectoryViewPlan<TTab>,
  slot: WireSidePanelSlot
): WireSidePanelDirectoryViewEntry<TTab> | undefined {
  return [...view.visibleEntries, ...view.hiddenEntries].find((entry) => entry.slot === slot);
}

function urgencyEntry<TEntry extends WireSidePanelOrchestrationEntry>(
  entries: readonly TEntry[]
): TEntry | undefined {
  return entries
    .filter((entry) => urgentState(entry.state))
    .slice()
    .sort((left, right) => urgencyWeight(right.state) - urgencyWeight(left.state) || left.order - right.order)[0];
}

function hiddenEntryFor<TEntry extends WireSidePanelOrchestrationEntry>(
  entries: readonly TEntry[],
  excludedSlots: ReadonlySet<WireSidePanelSlot>
): TEntry | undefined {
  const candidates = entries.filter((entry) => !excludedSlots.has(entry.slot));
  return urgencyEntry(candidates) ?? candidates[0];
}

function urgentState(state: WireSidePanelOrchestrationState): boolean {
  return state === "blocked" || state === "offline" || state === "ready";
}

function urgencyWeight(state: WireSidePanelOrchestrationState): number {
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

function uniqueRoutes(routes: Array<WireSidePanelControlRoute | undefined>): WireSidePanelControlRoute[] {
  const seen = new Set<string>();
  const result: WireSidePanelControlRoute[] = [];
  for (const route of routes) {
    if (!route) {
      continue;
    }

    const identity = route.slot ?? route.key;
    if (seen.has(identity)) {
      continue;
    }
    seen.add(identity);
    result.push(route);
  }
  return result;
}

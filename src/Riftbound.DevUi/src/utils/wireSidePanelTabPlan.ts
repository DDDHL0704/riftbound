import type { WireSidePanelSlot } from "../components/match/wireTableLayout";

export type WireSidePanelTab = "action" | "response" | "rules" | "log" | "detail";

export type WireSidePanelTabSpec = {
  id: WireSidePanelTab;
  label: string;
  primarySlot: WireSidePanelSlot;
  slots: readonly WireSidePanelSlot[];
};

export const WIRE_SIDE_PANEL_TABS: readonly WireSidePanelTabSpec[] = [
  { id: "action", label: "行动", primarySlot: "commandCenter", slots: ["commandCenter", "actionMap", "interaction", "actionPrompt"] },
  { id: "response", label: "响应", primarySlot: "responseCoach", slots: ["responseCoach", "turnWindow"] },
  { id: "rules", label: "规则", primarySlot: "ruleQueue", slots: ["ruleQueue", "serverFlow"] },
  { id: "log", label: "日志", primarySlot: "log", slots: ["log"] },
  {
    id: "detail",
    label: "详情",
    primarySlot: "timelineDetail",
    slots: ["timelineDetail", "overview", "tableAuthority", "informationBoundary", "promptAuthority"]
  }
] as const;

export const WIRE_SIDE_PANEL_TAB_BY_SLOT = WIRE_SIDE_PANEL_TABS.reduce((map, tab) => {
  for (const slot of tab.slots) {
    map[slot] = tab.id;
  }
  return map;
}, {} as Record<WireSidePanelSlot, WireSidePanelTab>);

export const WIRE_SIDE_PANEL_SHORT_LABELS: Record<WireSidePanelSlot, string> = {
  actionMap: "地图",
  actionPrompt: "提示",
  commandCenter: "指挥",
  informationBoundary: "边界",
  interaction: "焦点",
  log: "日志",
  overview: "总览",
  promptAuthority: "契约",
  responseCoach: "响应",
  ruleQueue: "队列",
  serverFlow: "流程",
  tableAuthority: "桌面",
  timelineDetail: "事件",
  turnWindow: "窗口"
};

export function wireSidePanelTabPanelIdForSlot(slot: WireSidePanelSlot): string | undefined {
  const tab = WIRE_SIDE_PANEL_TABS.find((item) => item.primarySlot === slot);
  return tab ? `wire-side-panel-tab-${tab.id}` : undefined;
}

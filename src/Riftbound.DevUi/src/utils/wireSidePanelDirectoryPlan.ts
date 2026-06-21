import type { WireSidePanelSlot } from "../components/match/wireTableLayout";

export type WireSidePanelDirectoryGroup = "window" | "command" | "authority" | "rules" | "history";

export type WireSidePanelDirectoryEntry = {
  anchorId: string;
  group: WireSidePanelDirectoryGroup;
  groupLabel: string;
  label: string;
  order: number;
  slot: WireSidePanelSlot;
};

export type WireSidePanelDirectoryPlan = {
  entries: WireSidePanelDirectoryEntry[];
  groups: {
    entries: WireSidePanelDirectoryEntry[];
    group: WireSidePanelDirectoryGroup;
    label: string;
  }[];
  bySlot: Record<WireSidePanelSlot, WireSidePanelDirectoryEntry>;
};

const GROUP_LABELS: Record<WireSidePanelDirectoryGroup, string> = {
  authority: "权威",
  command: "指挥",
  history: "记录",
  rules: "规则",
  window: "窗口"
};

const GROUP_ORDER: WireSidePanelDirectoryGroup[] = ["window", "command", "authority", "rules", "history"];

const SLOT_META: Record<WireSidePanelSlot, { group: WireSidePanelDirectoryGroup; label: string }> = {
  actionMap: { group: "command", label: "操作地图" },
  actionPrompt: { group: "command", label: "行动提示" },
  commandCenter: { group: "command", label: "指挥中心" },
  informationBoundary: { group: "authority", label: "信息边界" },
  interaction: { group: "command", label: "焦点行动" },
  log: { group: "history", label: "日志" },
  promptAuthority: { group: "authority", label: "窗口契约" },
  responseCoach: { group: "command", label: "响应导航" },
  ruleQueue: { group: "rules", label: "规则队列" },
  serverFlow: { group: "rules", label: "服务端流" },
  tableAuthority: { group: "authority", label: "桌面契约" },
  timelineDetail: { group: "rules", label: "事件详情" },
  turnWindow: { group: "window", label: "窗口总览" }
};

export function buildWireSidePanelDirectoryPlan(slots: readonly WireSidePanelSlot[]): WireSidePanelDirectoryPlan {
  const seen = new Set<WireSidePanelSlot>();
  const entries = slots.map((slot, index) => {
    if (seen.has(slot)) {
      throw new Error(`Duplicate wire side panel slot: ${slot}`);
    }
    seen.add(slot);

    const meta = SLOT_META[slot];
    return {
      anchorId: wireSidePanelAnchorId(slot),
      group: meta.group,
      groupLabel: GROUP_LABELS[meta.group],
      label: meta.label,
      order: index + 1,
      slot
    };
  });

  const bySlot = {} as Record<WireSidePanelSlot, WireSidePanelDirectoryEntry>;
  for (const entry of entries) {
    bySlot[entry.slot] = entry;
  }

  const groups = GROUP_ORDER.map((group) => ({
    entries: entries.filter((entry) => entry.group === group),
    group,
    label: GROUP_LABELS[group]
  })).filter((group) => group.entries.length > 0);

  return { bySlot, entries, groups };
}

export function wireSidePanelAnchorId(slot: WireSidePanelSlot): string {
  return `wire-side-panel-${slot}`;
}

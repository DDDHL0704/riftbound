import type {
  CommandSubmissionFollowupEventRef,
  CommandSubmissionFollowupPlan
} from "../../utils/commandSubmissionFollowupPlan";
import {
  buildWireTableSelectedLayoutPlan,
  type WireTableSelectedLayoutPlan
} from "./wireTableAuthorityPlan";
import type { WireTableViewModel } from "./wireTableViewModel";

export type WireCommandFollowupLayoutProjectionState = "empty" | "hidden-only" | "linked" | "unknown";

export type WireCommandFollowupLayoutProjectionRow = {
  capacityRowKey?: string;
  eventKind: string;
  eventTitle: string;
  key: string;
  layoutKind: WireTableSelectedLayoutPlan["kind"];
  layoutState: WireTableSelectedLayoutPlan["state"];
  objectId: string;
  objectLabel: string;
  refRole: string;
  state: Exclude<WireCommandFollowupLayoutProjectionState, "empty" | "hidden-only">;
  stateLabel: string;
  zoneKey?: string;
  zoneLabel: string;
};

export type WireCommandFollowupLayoutProjectionPlan = {
  hiddenRefCount: number;
  hiddenVisibleEventCount: number;
  locatedCount: number;
  overflowCount: number;
  publicRefCount: number;
  rows: WireCommandFollowupLayoutProjectionRow[];
  state: WireCommandFollowupLayoutProjectionState;
  stateLabel: string;
  summary: string;
  totalRefCount: number;
  unknownCount: number;
};

export function buildWireCommandFollowupLayoutProjectionPlan({
  maxRows = 8,
  plan,
  table
}: {
  maxRows?: number;
  plan: CommandSubmissionFollowupPlan;
  table: WireTableViewModel;
}): WireCommandFollowupLayoutProjectionPlan {
  const hiddenRefCount = plan.events.reduce(
    (total, event) => total + event.refs.filter((ref) => ref.hidden || !ref.objectId).length,
    0
  );
  const rows = plan.events.flatMap((event) =>
    event.refs.flatMap((ref) => projectionRowsForRef({
      eventKind: event.kind,
      eventKey: event.key,
      eventTitle: event.title,
      ref,
      table
    })));
  const publicRefCount = rows.length;
  const visibleRows = rows.slice(0, Math.max(0, maxRows));
  const locatedCount = rows.filter((row) => row.layoutState === "located").length;
  const unknownCount = rows.filter((row) => row.layoutState === "unknown").length;
  const state = projectionState({ hiddenRefCount, locatedCount, publicRefCount, unknownCount });

  return {
    hiddenRefCount,
    hiddenVisibleEventCount: plan.hiddenEventCount,
    locatedCount,
    overflowCount: Math.max(0, rows.length - visibleRows.length),
    publicRefCount,
    rows: visibleRows,
    state,
    stateLabel: projectionStateLabel(state),
    summary: projectionSummary({
      hiddenRefCount,
      hiddenVisibleEventCount: plan.hiddenEventCount,
      locatedCount,
      publicRefCount,
      state,
      totalRefCount: publicRefCount + hiddenRefCount,
      unknownCount
    }),
    totalRefCount: publicRefCount + hiddenRefCount,
    unknownCount
  };
}

function projectionRowsForRef({
  eventKind,
  eventKey,
  eventTitle,
  ref,
  table
}: {
  eventKind: string;
  eventKey: string;
  eventTitle: string;
  ref: CommandSubmissionFollowupEventRef;
  table: WireTableViewModel;
}): WireCommandFollowupLayoutProjectionRow[] {
  if (ref.hidden || !ref.objectId) {
    return [];
  }

  const layout = buildWireTableSelectedLayoutPlan(table, ref.objectId);
  const state = layout.state === "located" ? "linked" : "unknown";
  return [
    {
      capacityRowKey: layout.capacityRowKey,
      eventKind,
      eventTitle,
      key: `${eventKey}:${ref.key}:${ref.objectId}`,
      layoutKind: layout.kind,
      layoutState: layout.state,
      objectId: ref.objectId,
      objectLabel: ref.label,
      refRole: ref.role,
      state,
      stateLabel: projectionStateLabel(state),
      zoneKey: layout.zoneKey,
      zoneLabel: layout.zoneLabel
    }
  ];
}

function projectionState({
  hiddenRefCount,
  locatedCount,
  publicRefCount,
  unknownCount
}: {
  hiddenRefCount: number;
  locatedCount: number;
  publicRefCount: number;
  unknownCount: number;
}): WireCommandFollowupLayoutProjectionState {
  if (locatedCount > 0) {
    return "linked";
  }

  if (publicRefCount > 0 || unknownCount > 0) {
    return "unknown";
  }

  if (hiddenRefCount > 0) {
    return "hidden-only";
  }

  return "empty";
}

function projectionStateLabel(state: WireCommandFollowupLayoutProjectionState): string {
  switch (state) {
    case "empty":
      return "无投影";
    case "hidden-only":
      return "仅隐藏";
    case "linked":
      return "已落位";
    case "unknown":
      return "未定位";
  }
}

function projectionSummary({
  hiddenRefCount,
  hiddenVisibleEventCount,
  locatedCount,
  publicRefCount,
  state,
  totalRefCount,
  unknownCount
}: {
  hiddenRefCount: number;
  hiddenVisibleEventCount: number;
  locatedCount: number;
  publicRefCount: number;
  state: WireCommandFollowupLayoutProjectionState;
  totalRefCount: number;
  unknownCount: number;
}): string {
  if (state === "empty") {
    return hiddenVisibleEventCount > 0
      ? `当前公开事件窗口没有可投影对象；另有 ${hiddenVisibleEventCount} 条同 tick 事件未展开。`
      : "当前回执没有公开对象引用可投影到桌面。";
  }

  if (state === "hidden-only") {
    return `${hiddenRefCount} 个对象引用被服务端标记为隐藏，前端不投影身份或区域。`;
  }

  return `${locatedCount}/${publicRefCount} 个公开引用已落到桌面区域；${unknownCount} 个引用未进入当前桌面索引，隐藏引用 ${hiddenRefCount}/${totalRefCount}。`;
}

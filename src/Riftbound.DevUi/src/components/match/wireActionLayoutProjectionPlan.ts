import type {
  WireActionCandidatePlan,
  WireActionFocusCandidatePlan,
  WireActionFocusObjectRef,
  WireActionMapPlan,
  WireActionObjectEntry
} from "../../utils/wireActionMapPlan";
import {
  buildWireTableSelectedLayoutPlan,
  type WireTableSelectedLayoutPlan
} from "./wireTableAuthorityPlan";
import type { WireTableViewModel } from "./wireTableViewModel";

export type WireActionLayoutProjectionState = "blocked" | "empty" | "ready" | "unknown";
export type WireActionLayoutProjectionSource = "blocked-entry" | "candidate-step" | "enabled-entry" | "focused-next";

export type WireActionLayoutProjectionRow = {
  actionLabel: string;
  actionState: Exclude<WireActionLayoutProjectionState, "empty">;
  actionStateLabel: string;
  capacityRowKey?: string;
  key: string;
  layoutKind: WireTableSelectedLayoutPlan["kind"];
  layoutState: WireTableSelectedLayoutPlan["state"];
  objectId: string;
  objectLabel: string;
  roleLabel: string;
  selected: boolean;
  source: WireActionLayoutProjectionSource;
  sourceLabel: string;
  zoneKey?: string;
  zoneLabel: string;
};

export type WireActionLayoutProjectionPlan = {
  blockedCount: number;
  locatedCount: number;
  overflowCount: number;
  readyCount: number;
  rows: WireActionLayoutProjectionRow[];
  selectedCount: number;
  state: WireActionLayoutProjectionState;
  stateLabel: string;
  summary: string;
  totalCount: number;
};

export function buildWireActionLayoutProjectionPlan({
  actionMap,
  maxRows = 10,
  selectedObjectId,
  table
}: {
  actionMap: WireActionMapPlan;
  maxRows?: number;
  selectedObjectId?: string;
  table: WireTableViewModel;
}): WireActionLayoutProjectionPlan {
  const rows = compactRows([
    ...actionMap.objectEntries.map((entry) => entryProjectionRow(entry, "enabled-entry", table)),
    ...actionMap.blockedObjectEntries.map((entry) => entryProjectionRow(entry, "blocked-entry", table)),
    ...actionMap.candidatePlans.flatMap((candidate) => candidateStepProjectionRows(candidate, table, selectedObjectId)),
    ...focusNextProjectionRows(actionMap.focus?.relatedCandidates ?? [], table, selectedObjectId)
  ], selectedObjectId);
  const totalCount = rows.length;
  const visibleRows = rows.slice(0, Math.max(0, maxRows));
  const locatedCount = rows.filter((row) => row.layoutState === "located").length;
  const readyCount = rows.filter((row) => row.actionState === "ready").length;
  const blockedCount = rows.filter((row) => row.actionState === "blocked").length;
  const selectedCount = rows.filter((row) => row.selected).length;
  const state = projectionState(rows);

  return {
    blockedCount,
    locatedCount,
    overflowCount: Math.max(0, rows.length - visibleRows.length),
    readyCount,
    rows: visibleRows,
    selectedCount,
    state,
    stateLabel: projectionStateLabel(state),
    summary: projectionSummary({ blockedCount, locatedCount, readyCount, state, totalCount }),
    totalCount
  };
}

function entryProjectionRow(
  entry: WireActionObjectEntry,
  source: Extract<WireActionLayoutProjectionSource, "blocked-entry" | "enabled-entry">,
  table: WireTableViewModel
): WireActionLayoutProjectionRow {
  return rowFromLayout({
    actionLabel: source === "enabled-entry" ? `${entry.enabledCandidateCount} 个可提交候选` : `${entry.disabledCandidateCount} 个阻断候选`,
    actionState: source === "enabled-entry" ? "ready" : "blocked",
    layout: buildWireTableSelectedLayoutPlan(table, entry.objectId),
    objectId: entry.objectId,
    objectLabel: entry.label,
    roleLabel: source === "enabled-entry" ? "可操作对象" : "阻断对象",
    selected: entry.selected,
    source
  });
}

function candidateStepProjectionRows(
  candidate: WireActionCandidatePlan,
  table: WireTableViewModel,
  selectedObjectId?: string
): WireActionLayoutProjectionRow[] {
  return candidate.stepRows.flatMap((step) =>
    step.objectRefs.map((ref) => refProjectionRow({
      actionLabel: candidate.candidateLabel,
      actionState: candidate.enabled ? "ready" : "blocked",
      ref,
      selectedObjectId,
      source: "candidate-step",
      table
    })));
}

function focusNextProjectionRows(
  candidates: WireActionFocusCandidatePlan[],
  table: WireTableViewModel,
  selectedObjectId?: string
): WireActionLayoutProjectionRow[] {
  return candidates.flatMap((candidate) =>
    candidate.nextObjectRefs.map((ref) => refProjectionRow({
      actionLabel: candidate.label,
      actionState: candidate.enabled ? "ready" : "blocked",
      ref,
      selectedObjectId,
      source: "focused-next",
      table
    })));
}

function refProjectionRow({
  actionLabel,
  actionState,
  ref,
  selectedObjectId,
  source,
  table
}: {
  actionLabel: string;
  actionState: Exclude<WireActionLayoutProjectionState, "empty">;
  ref: WireActionFocusObjectRef;
  selectedObjectId?: string;
  source: Extract<WireActionLayoutProjectionSource, "candidate-step" | "focused-next">;
  table: WireTableViewModel;
}): WireActionLayoutProjectionRow {
  return rowFromLayout({
    actionLabel,
    actionState,
    layout: buildWireTableSelectedLayoutPlan(table, ref.objectId),
    objectId: ref.objectId,
    objectLabel: ref.label,
    roleLabel: ref.roleLabel,
    selected: selectedObjectId === ref.objectId,
    source
  });
}

function rowFromLayout({
  actionLabel,
  actionState,
  layout,
  objectId,
  objectLabel,
  roleLabel,
  selected,
  source
}: {
  actionLabel: string;
  actionState: Exclude<WireActionLayoutProjectionState, "empty">;
  layout: WireTableSelectedLayoutPlan;
  objectId: string;
  objectLabel: string;
  roleLabel: string;
  selected: boolean;
  source: WireActionLayoutProjectionSource;
}): WireActionLayoutProjectionRow {
  const state = layout.state === "unknown" && actionState === "ready" ? "unknown" : actionState;
  return {
    actionLabel,
    actionState: state,
    actionStateLabel: projectionStateLabel(state),
    capacityRowKey: layout.capacityRowKey,
    key: `${source}:${roleLabel}:${objectId}:${actionLabel}`,
    layoutKind: layout.kind,
    layoutState: layout.state,
    objectId,
    objectLabel,
    roleLabel,
    selected,
    source,
    sourceLabel: projectionSourceLabel(source),
    zoneKey: layout.zoneKey,
    zoneLabel: layout.zoneLabel
  };
}

function compactRows(
  rows: WireActionLayoutProjectionRow[],
  selectedObjectId?: string
): WireActionLayoutProjectionRow[] {
  const byKey = new Map<string, WireActionLayoutProjectionRow>();
  for (const row of rows) {
    const key = `${row.objectId}:${row.roleLabel}:${row.source}`;
    const current = byKey.get(key);
    if (!current || rowRank(row, selectedObjectId) < rowRank(current, selectedObjectId)) {
      byKey.set(key, row);
    }
  }

  return [...byKey.values()].sort((left, right) =>
    rowRank(left, selectedObjectId) - rowRank(right, selectedObjectId)
    || left.zoneLabel.localeCompare(right.zoneLabel)
    || left.roleLabel.localeCompare(right.roleLabel)
    || left.objectLabel.localeCompare(right.objectLabel));
}

function rowRank(row: WireActionLayoutProjectionRow, selectedObjectId?: string): number {
  if (selectedObjectId && row.objectId === selectedObjectId) {
    return 0;
  }

  if (row.actionState === "ready" && row.layoutState === "located") {
    return 1;
  }

  if (row.actionState === "ready") {
    return 2;
  }

  if (row.layoutState === "located") {
    return 3;
  }

  return 4;
}

function projectionState(rows: WireActionLayoutProjectionRow[]): WireActionLayoutProjectionState {
  if (rows.length === 0) {
    return "empty";
  }

  if (rows.some((row) => row.actionState === "ready" && row.layoutState === "located")) {
    return "ready";
  }

  if (rows.some((row) => row.layoutState === "unknown")) {
    return "unknown";
  }

  return "blocked";
}

function projectionStateLabel(state: WireActionLayoutProjectionState): string {
  switch (state) {
    case "blocked":
      return "阻断";
    case "empty":
      return "无投影";
    case "ready":
      return "可定位";
    case "unknown":
      return "未定位";
  }
}

function projectionSourceLabel(source: WireActionLayoutProjectionSource): string {
  switch (source) {
    case "blocked-entry":
      return "阻断入口";
    case "candidate-step":
      return "候选步骤";
    case "enabled-entry":
      return "可操作入口";
    case "focused-next":
      return "焦点下一步";
  }
}

function projectionSummary({
  blockedCount,
  locatedCount,
  readyCount,
  state,
  totalCount
}: {
  blockedCount: number;
  locatedCount: number;
  readyCount: number;
  state: WireActionLayoutProjectionState;
  totalCount: number;
}): string {
  if (state === "empty") {
    return "当前服务端候选没有公开可投影到桌面的对象。";
  }

  return `${locatedCount}/${totalCount} 个候选对象已定位到桌面区域；${readyCount} 个可提交关联，${blockedCount} 个阻断关联。`;
}

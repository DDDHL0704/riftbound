import type { CardObjectView } from "../types/protocol";
import { summarizePromptCandidateSemantics } from "./promptCandidateSemantics";
import type { TableObjectContext } from "./tableObjectContext";

export type WireTimelineDetailLineLike = {
  label: string;
  mine?: boolean;
  value: string;
};

export type WireTimelineDetailRefLike = {
  id: string;
  label?: string;
  role: string;
};

export type WireTimelineDetailLike = {
  id: string;
  lines: WireTimelineDetailLineLike[];
  refs: WireTimelineDetailRefLike[];
  source: "event" | "rule";
  subtitle?: string;
  title: string;
};

export type WireTimelineProjectionState = "hidden" | "missing" | "selected" | "visible";

export type WireTimelineStatusCard = {
  label: string;
  value: string;
};

export type WireTimelineProjectionRow = {
  id: string;
  key: string;
  label: string;
  role: string;
  state: WireTimelineProjectionState;
  stateLabel: string;
};

export type WireTimelineActionHintRow = {
  commandFieldLabels: string[];
  commandTypes: string[];
  disabledCount: number;
  enabledCount: number;
  key: string;
  label: string;
  objectId: string;
  reasonLabels: string[];
  requiredCommandFieldLabels: string[];
  role: string;
  selectionRoleLabels: string[];
  stateLabel: string;
  zoneLabel: string;
};

export type WireTimelineInspectorProjection = {
  count: number;
  key: WireTimelineProjectionState;
  label: string;
};

export type WireTimelineInspectorCandidate = {
  disabledCount: number;
  enabledCount: number;
  key: string;
  label: string;
  role: string;
  stateLabel: string;
  zoneLabel: string;
};

export type WireTimelineDetailInspectorPlan = {
  actionCandidateCount: number;
  hiddenRefCount: number;
  missingRefCount: number;
  projectionRows: WireTimelineInspectorProjection[];
  candidateRows: WireTimelineInspectorCandidate[];
  selectedProjectionCount: number;
  sourceLabel: string;
  summary: string;
  visibleRefCount: number;
};

export type WireTimelineDetailPlan = {
  actionHintRows: WireTimelineActionHintRow[];
  headerSubtitle: string;
  headerTitle: string;
  inspector: WireTimelineDetailInspectorPlan;
  projectionRows: WireTimelineProjectionRow[];
  statusCards: WireTimelineStatusCard[];
};

export function buildWireTimelineDetailPlan({
  detail,
  objectContextById = {},
  objectIndex,
  selectedObjectContext,
  selectedObjectId
}: {
  detail?: WireTimelineDetailLike;
  objectContextById?: Record<string, TableObjectContext>;
  objectIndex: Record<string, CardObjectView>;
  selectedObjectContext?: TableObjectContext;
  selectedObjectId?: string;
}): WireTimelineDetailPlan {
  const projectionRows = detail ? projectionRowsForDetail(detail, objectIndex, selectedObjectId) : [];
  const actionHintRows = detail ? actionHintRowsForDetail(detail, objectIndex, objectContextById) : [];
  const selectedProjection = projectionRows.some((row) => row.state === "selected");
  const visibleProjectionCount = projectionRows.filter((row) => row.state === "selected" || row.state === "visible").length;
  const enabledActionHintCount = actionHintRows.reduce((sum, row) => sum + row.enabledCount, 0);
  const disabledActionHintCount = actionHintRows.reduce((sum, row) => sum + row.disabledCount, 0);
  const focusValue = selectedObjectContext
    ? selectedObjectContext.zone.label
    : selectedObjectId
      ? "未定位焦点"
      : "无";

  return {
    actionHintRows,
    headerSubtitle: detail?.subtitle
      ?? (selectedObjectContext
        ? "来自服务端快照、行动窗口、结算链和事件索引。"
        : "从结算链、规则任务、触发队列或日志中选择一项。"),
    headerTitle: detail ? detail.title : selectedObjectContext ? "焦点对象规则上下文" : "未选择规则事件",
    inspector: inspectorPlan({
      actionHintRows,
      detail,
      projectionRows,
      visibleProjectionCount
    }),
    projectionRows,
    statusCards: [
      { label: "详情来源", value: detail ? detailSourceLabel(detail.source) : "无" },
      { label: "桌面投影", value: projectionRows.length > 0 ? `${visibleProjectionCount} / ${projectionRows.length} 可定位` : "无对象" },
      { label: "当前焦点", value: focusValue },
      { label: "焦点关联", value: selectedProjection ? "已命中详情对象" : detail ? "未命中详情对象" : "无详情" },
      { label: "关联候选", value: actionHintRows.length > 0 ? `${enabledActionHintCount} 可用 / ${disabledActionHintCount} 阻断` : "无候选" }
    ]
  };
}

function inspectorPlan({
  actionHintRows,
  detail,
  projectionRows,
  visibleProjectionCount
}: {
  actionHintRows: WireTimelineActionHintRow[];
  detail?: WireTimelineDetailLike;
  projectionRows: WireTimelineProjectionRow[];
  visibleProjectionCount: number;
}): WireTimelineDetailInspectorPlan {
  const hiddenRefCount = projectionRows.filter((row) => row.state === "hidden").length;
  const missingRefCount = projectionRows.filter((row) => row.state === "missing").length;
  const selectedProjectionCount = projectionRows.filter((row) => row.state === "selected").length;
  const actionCandidateCount = actionHintRows.reduce((sum, row) => sum + row.enabledCount + row.disabledCount, 0);
  const sourceLabel = detail ? detailSourceLabel(detail.source) : "无";

  return {
    actionCandidateCount,
    hiddenRefCount,
    missingRefCount,
    projectionRows: projectionStateRows(projectionRows),
    candidateRows: actionHintRows.map((row) => ({
      disabledCount: row.disabledCount,
      enabledCount: row.enabledCount,
      key: row.key,
      label: row.label,
      role: row.role,
      stateLabel: row.stateLabel,
      zoneLabel: row.zoneLabel
    })),
    selectedProjectionCount,
    sourceLabel,
    summary: detail
      ? `${sourceLabel} / 可定位 ${visibleProjectionCount} / 隐藏 ${hiddenRefCount} / 未公开 ${missingRefCount} / 候选 ${actionCandidateCount}`
      : "未选择详情",
    visibleRefCount: visibleProjectionCount
  };
}

function projectionStateRows(projectionRows: WireTimelineProjectionRow[]): WireTimelineInspectorProjection[] {
  return (["selected", "visible", "hidden", "missing"] satisfies WireTimelineProjectionState[])
    .map((state) => ({
      count: projectionRows.filter((row) => row.state === state).length,
      key: state,
      label: projectionStateLabel(state)
    }));
}

function actionHintRowsForDetail(
  detail: WireTimelineDetailLike,
  objectIndex: Record<string, CardObjectView>,
  objectContextById: Record<string, TableObjectContext>
): WireTimelineActionHintRow[] {
  const seen = new Set<string>();
  const rows: WireTimelineActionHintRow[] = [];
  for (const ref of detail.refs) {
    const id = ref.id.trim();
    if (!id || id === "HIDDEN" || seen.has(id)) {
      continue;
    }
    seen.add(id);

    const context = objectContextById[id];
    if (!context?.candidateLinks.length) {
      continue;
    }
    const candidateSummary = summarizePromptCandidateSemantics(context.candidateLinks, { disabledReasonsOnly: true });

    rows.push({
      commandFieldLabels: candidateSummary.commandFieldLabels,
      commandTypes: candidateSummary.commandTypes,
      disabledCount: context.promptDisabledCount,
      enabledCount: context.promptEnabledCount,
      key: `${ref.role || "对象"}:${id}`,
      label: projectionLabel(ref, objectIndex),
      objectId: id,
      reasonLabels: candidateSummary.reasonLabels,
      requiredCommandFieldLabels: candidateSummary.requiredCommandFieldLabels,
      role: ref.role || "对象",
      selectionRoleLabels: candidateSummary.selectionRoleLabels,
      stateLabel: `${context.promptEnabledCount} 可用 / ${context.promptDisabledCount} 阻断`,
      zoneLabel: context.zone.label
    });
  }

  return rows.sort((left, right) =>
    (right.enabledCount - left.enabledCount)
    || (left.disabledCount - right.disabledCount)
    || left.role.localeCompare(right.role, "zh-Hans-CN")
  );
}

function projectionRowsForDetail(
  detail: WireTimelineDetailLike,
  objectIndex: Record<string, CardObjectView>,
  selectedObjectId?: string
): WireTimelineProjectionRow[] {
  const seen = new Set<string>();
  const rows: WireTimelineProjectionRow[] = [];
  for (const ref of detail.refs) {
    const id = ref.id.trim();
    if (!id) {
      continue;
    }

    const key = `${ref.role}:${id}`;
    if (seen.has(key)) {
      continue;
    }
    seen.add(key);

    const state = projectionState(id, objectIndex, selectedObjectId);
    rows.push({
      id,
      key,
      label: projectionLabel(ref, objectIndex),
      role: ref.role || "对象",
      state,
      stateLabel: projectionStateLabel(state)
    });
  }

  return rows;
}

function projectionState(
  id: string,
  objectIndex: Record<string, CardObjectView>,
  selectedObjectId?: string
): WireTimelineProjectionState {
  if (id === "HIDDEN") {
    return "hidden";
  }

  if (selectedObjectId === id) {
    return "selected";
  }

  return objectIndex[id] ? "visible" : "missing";
}

function projectionLabel(ref: WireTimelineDetailRefLike, objectIndex: Record<string, CardObjectView>): string {
  if (ref.id === "HIDDEN") {
    return "隐藏对象";
  }

  const object = objectIndex[ref.id];
  return ref.label?.trim() || object?.cardNo || "未公开对象";
}

function projectionStateLabel(state: WireTimelineProjectionState): string {
  switch (state) {
    case "hidden":
      return "隐藏";
    case "missing":
      return "未公开";
    case "selected":
      return "已选中";
    case "visible":
      return "可定位";
  }
}

function detailSourceLabel(source: WireTimelineDetailLike["source"]): string {
  return source === "event" ? "日志事件" : "规则队列";
}

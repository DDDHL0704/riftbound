import type { ActionPromptDto, CardObjectView } from "../types/protocol";
import { summarizePromptCandidateSemantics } from "./promptCandidateSemantics";
import {
  buildPromptInteractionModel,
  promptChoiceRoleLabel,
  promptChoiceSummaryObjectIds,
  type PromptCandidateSummary,
  type PromptChoiceRole
} from "./promptInteraction";
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

export type WireTimelineNavigationActionState = "available" | "blocked" | "none";

export type WireTimelineNavigationFocusState = "focusable" | "hidden" | "missing" | "selected";

export type WireTimelineNavigationRow = {
  actionLabel: string;
  actionState: WireTimelineNavigationActionState;
  canFocus: boolean;
  focusLabel: string;
  focusState: WireTimelineNavigationFocusState;
  key: string;
  label: string;
  objectId?: string;
  projectionState: WireTimelineProjectionState;
  role: string;
  selected: boolean;
  zoneLabel: string;
};

export type WireTimelineCommandBridgeObjectRef = {
  key: string;
  label: string;
  objectId: string;
  roleLabel: string;
};

export type WireTimelineCommandBridgeRow = {
  commandType?: string;
  detailObjectId: string;
  enabled: boolean;
  key: string;
  label: string;
  nextObjectRefs: WireTimelineCommandBridgeObjectRef[];
  nextStepLabel: string;
  reasonLabel: string;
  roleLabels: string[];
  stateLabel: string;
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
  commandBridgeCount: number;
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
  commandBridgeRows: WireTimelineCommandBridgeRow[];
  headerSubtitle: string;
  headerTitle: string;
  inspector: WireTimelineDetailInspectorPlan;
  navigationRows: WireTimelineNavigationRow[];
  projectionRows: WireTimelineProjectionRow[];
  statusCards: WireTimelineStatusCard[];
};

export function buildWireTimelineDetailPlan({
  detail,
  objectContextById = {},
  objectIndex,
  prompt,
  selectedObjectContext,
  selectedObjectId
}: {
  detail?: WireTimelineDetailLike;
  objectContextById?: Record<string, TableObjectContext>;
  objectIndex: Record<string, CardObjectView>;
  prompt?: ActionPromptDto;
  selectedObjectContext?: TableObjectContext;
  selectedObjectId?: string;
}): WireTimelineDetailPlan {
  const projectionRows = detail ? projectionRowsForDetail(detail, objectIndex, selectedObjectId) : [];
  const actionHintRows = detail ? actionHintRowsForDetail(detail, objectIndex, objectContextById) : [];
  const commandBridgeRows = detail ? commandBridgeRowsForDetail(detail, objectIndex, prompt) : [];
  const navigationRows = navigationRowsForDetail(projectionRows, objectContextById);
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
      commandBridgeRows,
      detail,
      projectionRows,
      visibleProjectionCount
    }),
    navigationRows,
    commandBridgeRows,
    projectionRows,
    statusCards: [
      { label: "详情来源", value: detail ? detailSourceLabel(detail.source) : "无" },
      { label: "桌面投影", value: projectionRows.length > 0 ? `${visibleProjectionCount} / ${projectionRows.length} 可定位` : "无对象" },
      { label: "当前焦点", value: focusValue },
      { label: "焦点关联", value: selectedProjection ? "已命中详情对象" : detail ? "未命中详情对象" : "无详情" },
      { label: "关联候选", value: actionHintRows.length > 0 ? `${enabledActionHintCount} 可用 / ${disabledActionHintCount} 阻断` : "无候选" },
      { label: "候选路径", value: commandBridgeRows.length > 0 ? `${commandBridgeRows.length} 条` : "无路径" }
    ]
  };
}

function commandBridgeRowsForDetail(
  detail: WireTimelineDetailLike,
  objectIndex: Record<string, CardObjectView>,
  prompt?: ActionPromptDto
): WireTimelineCommandBridgeRow[] {
  const promptModel = buildPromptInteractionModel(prompt);
  const rows: WireTimelineCommandBridgeRow[] = [];
  const seen = new Set<string>();
  const visibleRefIds = detail.refs
    .map((ref) => ref.id.trim())
    .filter((id) => id && id !== "HIDDEN" && objectIndex[id]);

  for (const objectId of visibleRefIds) {
    for (const candidate of promptModel.candidates) {
      const roleLabels = roleLabelsForObject(candidate, objectId);
      if (roleLabels.length === 0) {
        continue;
      }

      const key = `${candidate.action}:${candidate.label}:${objectId}`;
      if (seen.has(key)) {
        continue;
      }
      seen.add(key);

      const nextStep = nextStepForCommandBridge(candidate, roleLabels);
      rows.push({
        commandType: candidate.command?.cmdType ?? candidate.action,
        detailObjectId: objectId,
        enabled: candidate.enabled,
        key,
        label: candidate.label,
        nextObjectRefs: nextStep ? objectRefsForCommandBridge(candidate, nextStep.role, objectIndex) : [],
        nextStepLabel: nextStepLabelForCommandBridge(candidate, nextStep),
        reasonLabel: candidate.reason,
        roleLabels,
        stateLabel: candidate.enabled ? "可提交" : "暂不可提交"
      });
    }
  }

  return rows.sort((left, right) =>
    Number(right.enabled) - Number(left.enabled)
    || right.nextObjectRefs.length - left.nextObjectRefs.length
    || left.label.localeCompare(right.label, "zh-Hans-CN")
  ).slice(0, 6);
}

function navigationRowsForDetail(
  projectionRows: WireTimelineProjectionRow[],
  objectContextById: Record<string, TableObjectContext>
): WireTimelineNavigationRow[] {
  return projectionRows.map((row) => {
    const context = objectContextById[row.id];
    const selected = row.state === "selected";
    const canFocus = row.state === "selected" || row.state === "visible";
    const actionState = navigationActionState(context);
    return {
      actionLabel: navigationActionLabel(actionState, context),
      actionState,
      canFocus,
      focusLabel: navigationFocusLabel(row.state),
      focusState: navigationFocusState(row.state),
      key: row.key,
      label: row.label,
      objectId: canFocus ? row.id : undefined,
      projectionState: row.state,
      role: row.role,
      selected,
      zoneLabel: context?.zone.label ?? projectionStateLabel(row.state)
    };
  });
}

function inspectorPlan({
  actionHintRows,
  commandBridgeRows,
  detail,
  projectionRows,
  visibleProjectionCount
}: {
  actionHintRows: WireTimelineActionHintRow[];
  commandBridgeRows: WireTimelineCommandBridgeRow[];
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
    commandBridgeCount: commandBridgeRows.length,
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
      ? `${sourceLabel} / 可定位 ${visibleProjectionCount} / 隐藏 ${hiddenRefCount} / 未公开 ${missingRefCount} / 候选 ${actionCandidateCount} / 路径 ${commandBridgeRows.length}`
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

function roleLabelsForObject(candidate: PromptCandidateSummary, objectId: string): string[] {
  return uniqueStrings(candidate.choices
    .filter((choice) => promptChoiceSummaryObjectIds(choice).includes(objectId))
    .map((choice) => promptChoiceRoleLabel(choice.role)));
}

function nextStepForCommandBridge(
  candidate: PromptCandidateSummary,
  selectedRoleLabels: string[]
): PromptCandidateSummary["steps"][number] | undefined {
  return candidate.steps.find((step) =>
    step.required && !selectedRoleLabels.includes(promptChoiceRoleLabel(step.role)))
    ?? candidate.steps.find((step) =>
      step.count > 0 && !selectedRoleLabels.includes(promptChoiceRoleLabel(step.role)));
}

function nextStepLabelForCommandBridge(
  candidate: PromptCandidateSummary,
  nextStep: PromptCandidateSummary["steps"][number] | undefined
): string {
  if (nextStep) {
    return nextStep.required ? `需要${nextStep.label}` : `可选${nextStep.label}`;
  }

  return candidate.enabled ? "可提交给服务端" : "等待服务端窗口";
}

function objectRefsForCommandBridge(
  candidate: PromptCandidateSummary,
  role: PromptChoiceRole,
  objectIndex: Record<string, CardObjectView>
): WireTimelineCommandBridgeObjectRef[] {
  const refs: WireTimelineCommandBridgeObjectRef[] = [];
  const seen = new Set<string>();
  for (const choice of candidate.choices.filter((item) => item.role === role)) {
    const objectId = promptChoiceSummaryObjectIds(choice).find((id) => objectIndex[id]);
    if (!objectId || seen.has(objectId)) {
      continue;
    }

    seen.add(objectId);
    refs.push({
      key: `${candidate.action}:${role}:${choice.id}:${objectId}`,
      label: choice.label || objectIndex[objectId]?.cardNo || "服务端对象",
      objectId,
      roleLabel: promptChoiceRoleLabel(role)
    });
  }

  return refs.slice(0, 4);
}

function navigationActionState(context?: TableObjectContext): WireTimelineNavigationActionState {
  if (!context) {
    return "none";
  }

  if (context.promptEnabledCount > 0) {
    return "available";
  }

  if (context.promptDisabledCount > 0 || context.candidateLinks.length > 0) {
    return "blocked";
  }

  return "none";
}

function navigationActionLabel(actionState: WireTimelineNavigationActionState, context?: TableObjectContext): string {
  switch (actionState) {
    case "available":
      return `${context?.promptEnabledCount ?? 0} 可用`;
    case "blocked":
      return `${context?.promptDisabledCount ?? 0} 阻断`;
    case "none":
      return "无候选";
  }
}

function navigationFocusState(state: WireTimelineProjectionState): WireTimelineNavigationFocusState {
  switch (state) {
    case "hidden":
      return "hidden";
    case "missing":
      return "missing";
    case "selected":
      return "selected";
    case "visible":
      return "focusable";
  }
}

function navigationFocusLabel(state: WireTimelineProjectionState): string {
  switch (state) {
    case "hidden":
      return "隐藏";
    case "missing":
      return "未公开";
    case "selected":
      return "当前焦点";
    case "visible":
      return "可聚焦";
  }
}

function detailSourceLabel(source: WireTimelineDetailLike["source"]): string {
  return source === "event" ? "日志事件" : "规则队列";
}

function uniqueStrings(values: string[]): string[] {
  return [...new Set(values.map((value) => value.trim()).filter(Boolean))];
}

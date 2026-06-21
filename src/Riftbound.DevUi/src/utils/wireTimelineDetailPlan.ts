import type { ActionPromptDto, CardObjectView } from "../types/protocol";
import type { CandidateSelectionDraft } from "./candidateSelectionDraft";
import { candidateComposerKey } from "./candidateComposerModel";
import {
  buildFocusedInteractionGrammarPlan,
  type FocusedInteractionGrammarState,
  type FocusedInteractionGrammarStepState
} from "./focusedInteractionGrammarPlan";
import { summarizePromptCandidateSemantics } from "./promptCandidateSemantics";
import {
  buildPromptInteractionModel,
  promptCommandBindingLabel,
  promptCommandBindingSourceLabel,
  promptChoiceRoleLabel,
  promptChoiceSummaryObjectIds,
  type PromptCandidateSummary,
  type PromptCommandBindingSummary,
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
  visibility?: WireTimelineDetailRefVisibility;
};

export type WireTimelineDetailRefVisibility = "hidden" | "missing" | "visible";

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

export type WireTimelineEvidenceRowState = "blocked" | "empty" | "ready" | "warn";

export type WireTimelineEvidenceRow = {
  key: string;
  label: string;
  state: WireTimelineEvidenceRowState;
  stateLabel: string;
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

export type WireTimelineCommandBridgeRouteState = "blocked" | "inactive" | "ready" | "selecting";

export type WireTimelineCommandBridgeFieldState = "covered" | "missing" | "optional" | "server";

export type WireTimelineCommandBridgeFieldRow = {
  field: string;
  key: string;
  label: string;
  required: boolean;
  roleLabel?: string;
  sourceLabel: string;
  state: WireTimelineCommandBridgeFieldState;
  stateLabel: string;
};

export type WireTimelineCommandBridgeGrammarStep = {
  availableCount: number;
  key: string;
  label: string;
  required: boolean;
  role: string;
  selectedCount: number;
  state: FocusedInteractionGrammarStepState;
  stateLabel: string;
};

export type WireTimelineCommandBridgeGateState = "blocked" | "ready" | "waiting";

export type WireTimelineCommandBridgeGateRow = {
  key: string;
  label: string;
  reason: string;
  state: WireTimelineCommandBridgeGateState;
  stateLabel: string;
};

export type WireTimelineCommandBridgeSubmitState = "blocked" | "inactive" | "ready" | "selecting";

export type WireTimelineCommandBridgeSubmitField = {
  field: string;
  key: string;
  label: string;
  state: WireTimelineCommandBridgeFieldState;
  stateLabel: string;
};

export type WireTimelineCommandBridgeSubmitPlan = {
  canSubmit: boolean;
  commandType?: string;
  fieldCount: number;
  fieldSummary: string;
  fields: WireTimelineCommandBridgeSubmitField[];
  firstBlockingGate?: WireTimelineCommandBridgeGateRow;
  key: string;
  nextStepLabel: string;
  reason: string;
  state: WireTimelineCommandBridgeSubmitState;
  stateLabel: string;
  submitLabel: string;
};

export type WireTimelineCommandBridgeRow = {
  commandFieldSummary: string;
  commandFields: WireTimelineCommandBridgeFieldRow[];
  commandType?: string;
  detailLinkLabel: string;
  detailObjectId: string;
  detailRoleLabel: string;
  draftActive: boolean;
  enabled: boolean;
  grammarState: FocusedInteractionGrammarState;
  grammarStateLabel: string;
  grammarSteps: WireTimelineCommandBridgeGrammarStep[];
  grammarSummary: string;
  gateRows: WireTimelineCommandBridgeGateRow[];
  gateSummary: string;
  key: string;
  label: string;
  missingRequiredCount: number;
  nextObjectRefs: WireTimelineCommandBridgeObjectRef[];
  nextStepLabel: string;
  reasonLabel: string;
  roleLabels: string[];
  routeState: WireTimelineCommandBridgeRouteState;
  routeStateLabel: string;
  selectedRoleLabels: string[];
  serverRoleSummary: string;
  selectedStepCount: number;
  selectionLabel: string;
  stateLabel: string;
  submitPlan: WireTimelineCommandBridgeSubmitPlan;
  totalStepCount: number;
};

export type WireTimelineNextStepState = "blocked" | "empty" | "observe" | "ready" | "selecting";

export type WireTimelineNextStepCheckRow = {
  detail: string;
  key: string;
  label: string;
  state: WireTimelineCommandBridgeGateState;
  stateLabel: string;
};

export type WireTimelineNextStepGrammarRow = {
  availableCount: number;
  key: string;
  label: string;
  required: boolean;
  role: string;
  selectedCount: number;
  state: FocusedInteractionGrammarStepState;
  stateLabel: string;
};

export type WireTimelineNextStepPlan = {
  body: string;
  checks: WireTimelineNextStepCheckRow[];
  commandType?: string;
  detail: string;
  headline: string;
  key: string;
  refs: WireTimelineCommandBridgeObjectRef[];
  state: WireTimelineNextStepState;
  steps: WireTimelineNextStepGrammarRow[];
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

export type WireTimelineRouteSummaryState = "blocked" | "empty" | "inactive" | "ready" | "selecting";

export type WireTimelineRouteSummaryCount = {
  key: WireTimelineCommandBridgeRouteState | "draft";
  label: string;
  state: WireTimelineRouteSummaryState;
  value: number;
};

export type WireTimelineRouteSummaryPlan = {
  blockedCount: number;
  body: string;
  draftCount: number;
  headline: string;
  inactiveCount: number;
  nextStepLabel: string;
  readyCount: number;
  rows: WireTimelineRouteSummaryCount[];
  selectingCount: number;
  state: WireTimelineRouteSummaryState;
  stateLabel: string;
  totalCount: number;
};

export type WireTimelineDetailPlan = {
  actionHintRows: WireTimelineActionHintRow[];
  commandBridgeRows: WireTimelineCommandBridgeRow[];
  evidenceRows: WireTimelineEvidenceRow[];
  headerSubtitle: string;
  headerTitle: string;
  inspector: WireTimelineDetailInspectorPlan;
  navigationRows: WireTimelineNavigationRow[];
  nextStep: WireTimelineNextStepPlan;
  projectionRows: WireTimelineProjectionRow[];
  routeSummary: WireTimelineRouteSummaryPlan;
  statusCards: WireTimelineStatusCard[];
};

export function buildWireTimelineDetailPlan({
  detail,
  disabledByConnection = false,
  objectContextById = {},
  objectIndex,
  prompt,
  selectionDraft,
  selectedObjectContext,
  selectedObjectId
}: {
  detail?: WireTimelineDetailLike;
  disabledByConnection?: boolean;
  objectContextById?: Record<string, TableObjectContext>;
  objectIndex: Record<string, CardObjectView>;
  prompt?: ActionPromptDto;
  selectionDraft?: CandidateSelectionDraft;
  selectedObjectContext?: TableObjectContext;
  selectedObjectId?: string;
}): WireTimelineDetailPlan {
  const projectionRows = detail ? projectionRowsForDetail(detail, objectIndex, selectedObjectId) : [];
  const actionHintRows = detail ? actionHintRowsForDetail(detail, objectIndex, objectContextById) : [];
  const commandBridgeRows = detail ? commandBridgeRowsForDetail(detail, objectIndex, prompt, selectionDraft, disabledByConnection) : [];
  const navigationRows = navigationRowsForDetail(projectionRows, objectContextById);
  const selectedProjection = projectionRows.some((row) => row.state === "selected");
  const visibleProjectionCount = projectionRows.filter((row) => row.state === "selected" || row.state === "visible").length;
  const enabledActionHintCount = actionHintRows.reduce((sum, row) => sum + row.enabledCount, 0);
  const disabledActionHintCount = actionHintRows.reduce((sum, row) => sum + row.disabledCount, 0);
  const hiddenRefCount = projectionRows.filter((row) => row.state === "hidden").length;
  const missingRefCount = projectionRows.filter((row) => row.state === "missing").length;
  const focusValue = selectedObjectContext
    ? selectedObjectContext.zone.label
    : selectedObjectId
      ? "未定位焦点"
      : "无";

  return {
    actionHintRows,
    evidenceRows: evidenceRowsForDetail({
      commandBridgeRows,
      detail,
      disabledActionHintCount,
      enabledActionHintCount,
      hiddenRefCount,
      missingRefCount,
      projectionRows,
      visibleProjectionCount
    }),
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
    routeSummary: routeSummaryPlan(commandBridgeRows),
    nextStep: nextStepPlanForDetail({
      actionHintRows,
      commandBridgeRows,
      detail,
      projectionRows
    }),
    projectionRows,
    statusCards: [
      { label: "详情来源", value: detail ? detailSourceLabel(detail.source) : "无" },
      { label: "桌面投影", value: projectionRows.length > 0 ? `${visibleProjectionCount} / ${projectionRows.length} 可定位` : "无对象" },
      { label: "当前焦点", value: focusValue },
      { label: "焦点关联", value: selectedProjection ? "已命中详情对象" : detail ? "未命中详情对象" : "无详情" },
      { label: "关联候选", value: actionHintRows.length > 0 ? `${enabledActionHintCount} 可用 / ${disabledActionHintCount} 阻断` : "无候选" },
      { label: "候选路径", value: commandBridgeRows.length > 0 ? commandBridgeStatusLabel(commandBridgeRows) : "无路径" }
    ]
  };
}

function routeSummaryPlan(rows: WireTimelineCommandBridgeRow[]): WireTimelineRouteSummaryPlan {
  const readyCount = rows.filter((row) => row.routeState === "ready").length;
  const selectingCount = rows.filter((row) => row.routeState === "selecting").length;
  const blockedCount = rows.filter((row) => row.routeState === "blocked").length;
  const inactiveCount = rows.filter((row) => row.routeState === "inactive").length;
  const draftCount = rows.filter((row) => row.draftActive).length;
  const primaryRow = rows.find((row) => row.routeState === "ready")
    ?? rows.find((row) => row.routeState === "selecting")
    ?? rows.find((row) => row.enabled && row.routeState === "inactive")
    ?? rows.find((row) => row.routeState === "blocked")
    ?? rows[0];
  const state: WireTimelineRouteSummaryState = readyCount > 0
    ? "ready"
    : selectingCount > 0
      ? "selecting"
      : blockedCount > 0 && inactiveCount === 0
        ? "blocked"
        : rows.length > 0
          ? "inactive"
          : "empty";

  return {
    blockedCount,
    body: routeSummaryBody(state, primaryRow),
    draftCount,
    headline: routeSummaryHeadline(state),
    inactiveCount,
    nextStepLabel: routeSummaryNextStep(state, primaryRow),
    readyCount,
    rows: [
      { key: "ready", label: "可送", state: readyCount > 0 ? "ready" : "empty", value: readyCount },
      { key: "selecting", label: "待选", state: selectingCount > 0 ? "selecting" : "empty", value: selectingCount },
      { key: "blocked", label: "阻断", state: blockedCount > 0 ? "blocked" : "empty", value: blockedCount },
      { key: "inactive", label: "未进", state: inactiveCount > 0 ? "inactive" : "empty", value: inactiveCount },
      { key: "draft", label: "草稿", state: draftCount > 0 ? "selecting" : "empty", value: draftCount }
    ],
    selectingCount,
    state,
    stateLabel: routeSummaryStateLabel(state),
    totalCount: rows.length
  };
}

function routeSummaryHeadline(state: WireTimelineRouteSummaryState): string {
  switch (state) {
    case "blocked":
      return "提交路线阻断";
    case "empty":
      return "无候选路线";
    case "inactive":
      return "等待选择候选对象";
    case "ready":
      return "存在可提交路线";
    case "selecting":
      return "继续补齐候选选择";
  }
}

function routeSummaryBody(
  state: WireTimelineRouteSummaryState,
  primaryRow?: WireTimelineCommandBridgeRow
): string {
  if (!primaryRow) {
    return "当前详情没有可由服务端候选解释的提交路线。";
  }

  switch (state) {
    case "blocked":
      return `${primaryRow.label} / ${primaryRow.gateSummary || primaryRow.reasonLabel}`;
    case "empty":
      return "当前详情没有可由服务端候选解释的提交路线。";
    case "inactive":
      return `${primaryRow.detailRoleLabel} -> ${primaryRow.label}`;
    case "ready":
      return `${primaryRow.label} / ${primaryRow.commandType ?? "服务端命令"}`;
    case "selecting":
      return `${primaryRow.label} / ${primaryRow.selectionLabel}`;
  }
}

function routeSummaryNextStep(
  state: WireTimelineRouteSummaryState,
  primaryRow?: WireTimelineCommandBridgeRow
): string {
  if (!primaryRow) {
    return "只查看规则事件。";
  }

  switch (state) {
    case "blocked":
      return primaryRow.gateSummary || primaryRow.reasonLabel || "等待服务端开放。";
    case "empty":
      return "只查看规则事件。";
    case "inactive":
      return primaryRow.nextStepLabel || "先聚焦候选对象。";
    case "ready":
      return "可从卡牌详情或操作区送服务端校验。";
    case "selecting":
      return primaryRow.nextStepLabel || "补齐服务端要求的选择。";
  }
}

function routeSummaryStateLabel(state: WireTimelineRouteSummaryState): string {
  switch (state) {
    case "blocked":
      return "阻断";
    case "empty":
      return "无路径";
    case "inactive":
      return "未进入";
    case "ready":
      return "可提交";
    case "selecting":
      return "待选择";
  }
}

function nextStepPlanForDetail({
  actionHintRows,
  commandBridgeRows,
  detail,
  projectionRows
}: {
  actionHintRows: WireTimelineActionHintRow[];
  commandBridgeRows: WireTimelineCommandBridgeRow[];
  detail?: WireTimelineDetailLike;
  projectionRows: WireTimelineProjectionRow[];
}): WireTimelineNextStepPlan {
  if (!detail) {
    return {
      body: "从结算链、规则任务或日志中选择一项。",
      checks: [],
      detail: "尚无服务端详情材料。",
      headline: "等待选择规则事件",
      key: "empty",
      refs: [],
      state: "empty",
      steps: []
    };
  }

  const readyRoute = commandBridgeRows.find((row) => row.routeState === "ready");
  if (readyRoute) {
    return nextStepFromBridgeRow(readyRoute, {
      body: `${readyRoute.label} / ${readyRoute.commandType ?? "服务端命令"}`,
      detail: `${readyRoute.routeStateLabel} / ${readyRoute.nextStepLabel} / ${readyRoute.gateSummary}`,
      headline: "可送服务端校验",
      key: "ready",
      state: "ready"
    });
  }

  const selectingRoute = commandBridgeRows.find((row) => row.routeState === "selecting");
  if (selectingRoute) {
    return nextStepFromBridgeRow(selectingRoute, {
      body: `${selectingRoute.label} / ${selectingRoute.commandType ?? "服务端命令"}`,
      detail: `${selectingRoute.selectionLabel} / ${selectingRoute.nextStepLabel} / ${selectingRoute.gateSummary}`,
      headline: "继续补齐选择",
      key: "selecting",
      state: "selecting"
    });
  }

  const inactiveRoute = commandBridgeRows.find((row) => row.enabled && row.routeState === "inactive")
    ?? commandBridgeRows.find((row) => row.routeState === "inactive");
  if (inactiveRoute) {
    return nextStepFromBridgeRow(inactiveRoute, {
      body: `${inactiveRoute.detailRoleLabel} -> ${inactiveRoute.label}`,
      detail: `${inactiveRoute.nextStepLabel} / ${inactiveRoute.commandFieldSummary}`,
      headline: "从详情对象开始选择",
      key: "inactive",
      state: "selecting"
    });
  }

  const blockedRoute = commandBridgeRows.find((row) => row.routeState === "blocked") ?? commandBridgeRows.find((row) => !row.enabled);
  if (blockedRoute) {
    return nextStepFromBridgeRow(blockedRoute, {
      body: `${blockedRoute.label} / ${blockedRoute.commandType ?? "服务端命令"}`,
      detail: `${blockedRoute.reasonLabel} / ${blockedRoute.gateSummary}`,
      headline: "服务端暂不允许",
      key: "blocked",
      state: "blocked"
    });
  }

  const enabledHint = actionHintRows.find((row) => row.enabledCount > 0);
  if (enabledHint) {
    return {
      body: `${enabledHint.role} / ${enabledHint.label}`,
      checks: [{
        detail: enabledHint.zoneLabel,
        key: "server-candidate",
        label: "服务端候选",
        state: "ready",
        stateLabel: enabledHint.stateLabel
      }],
      commandType: enabledHint.commandTypes[0],
      detail: `${enabledHint.stateLabel} / ${enabledHint.zoneLabel}`,
      headline: "关联对象有服务端候选",
      key: "hint-ready",
      refs: [{
        key: `hint:${enabledHint.objectId}`,
        label: enabledHint.label,
        objectId: enabledHint.objectId,
        roleLabel: enabledHint.role
      }],
      state: "ready",
      steps: []
    };
  }

  const blockedHint = actionHintRows.find((row) => row.disabledCount > 0);
  if (blockedHint) {
    return {
      body: `${blockedHint.role} / ${blockedHint.label}`,
      checks: [{
        detail: blockedHint.reasonLabels[0] || blockedHint.zoneLabel,
        key: "server-candidate",
        label: "服务端候选",
        state: "blocked",
        stateLabel: blockedHint.stateLabel
      }],
      commandType: blockedHint.commandTypes[0],
      detail: blockedHint.reasonLabels[0] || blockedHint.stateLabel,
      headline: "关联候选被服务端阻断",
      key: "hint-blocked",
      refs: [{
        key: `hint:${blockedHint.objectId}`,
        label: blockedHint.label,
        objectId: blockedHint.objectId,
        roleLabel: blockedHint.role
      }],
      state: "blocked",
      steps: []
    };
  }

  const hiddenCount = projectionRows.filter((row) => row.state === "hidden").length;
  const missingCount = projectionRows.filter((row) => row.state === "missing").length;
  return {
    body: projectionRows.length > 0 ? `${projectionRows.length} 个详情对象` : "当前详情没有对象引用",
    checks: [],
    detail: hiddenCount > 0 || missingCount > 0
      ? `${hiddenCount} 隐藏 / ${missingCount} 未公开，等待服务端公开后再操作。`
      : "当前服务端详情没有公开可提交候选。",
    headline: "仅查看公开证据",
    key: "observe",
    refs: [],
    state: "observe",
    steps: []
  };
}

function nextStepFromBridgeRow(
  row: WireTimelineCommandBridgeRow,
  copy: {
    body: string;
    detail: string;
    headline: string;
    key: string;
    state: WireTimelineNextStepState;
  }
): WireTimelineNextStepPlan {
  return {
    ...copy,
    checks: row.gateRows.map((gate) => ({
      detail: gate.reason,
      key: gate.key,
      label: gate.label,
      state: gate.state,
      stateLabel: gate.stateLabel
    })),
    commandType: row.commandType,
    refs: row.nextObjectRefs,
    steps: row.grammarSteps.map((step) => ({
      availableCount: step.availableCount,
      key: step.key,
      label: step.label,
      required: step.required,
      role: step.role,
      selectedCount: step.selectedCount,
      state: step.state,
      stateLabel: step.stateLabel
    }))
  };
}

function evidenceRowsForDetail({
  commandBridgeRows,
  detail,
  disabledActionHintCount,
  enabledActionHintCount,
  hiddenRefCount,
  missingRefCount,
  projectionRows,
  visibleProjectionCount
}: {
  commandBridgeRows: WireTimelineCommandBridgeRow[];
  detail?: WireTimelineDetailLike;
  disabledActionHintCount: number;
  enabledActionHintCount: number;
  hiddenRefCount: number;
  missingRefCount: number;
  projectionRows: WireTimelineProjectionRow[];
  visibleProjectionCount: number;
}): WireTimelineEvidenceRow[] {
  if (!detail) {
    return [];
  }

  const readyPathCount = commandBridgeRows.filter((row) => row.routeState === "ready").length;
  const selectingPathCount = commandBridgeRows.filter((row) => row.routeState === "selecting").length;
  const blockedPathCount = commandBridgeRows.filter((row) => row.routeState === "blocked").length;
  const inactivePathCount = commandBridgeRows.filter((row) => row.routeState === "inactive").length;
  const totalActionCandidateCount = enabledActionHintCount + disabledActionHintCount;
  const totalProjectionCount = projectionRows.length;

  return [
    {
      key: "source",
      label: "材料",
      state: "ready",
      stateLabel: detail.source === "event" ? "服务端日志" : "服务端规则",
      value: detailSourceLabel(detail.source)
    },
    {
      key: "projection",
      label: "投影",
      state: totalProjectionCount === 0 ? "empty" : missingRefCount > 0 ? "warn" : "ready",
      stateLabel: totalProjectionCount === 0
        ? "无对象"
        : missingRefCount > 0
          ? "存在未公开对象"
          : hiddenRefCount > 0
            ? "隐藏边界内"
            : "可定位",
      value: totalProjectionCount > 0 ? `${visibleProjectionCount}/${totalProjectionCount} 可定位` : "无对象"
    },
    {
      key: "candidate",
      label: "候选",
      state: enabledActionHintCount > 0
        ? "ready"
        : disabledActionHintCount > 0
          ? "blocked"
          : "empty",
      stateLabel: totalActionCandidateCount > 0
        ? enabledActionHintCount > 0
          ? "服务端开放"
          : "服务端阻断"
        : "无候选",
      value: totalActionCandidateCount > 0 ? `${enabledActionHintCount} 可用 / ${disabledActionHintCount} 阻断` : "无候选"
    },
    {
      key: "path",
      label: "路径",
      state: commandBridgeRows.length === 0
        ? "empty"
        : readyPathCount > 0
          ? "ready"
          : selectingPathCount > 0
            ? "warn"
            : blockedPathCount > 0
              ? "blocked"
              : "empty",
      stateLabel: commandBridgeRows.length === 0
        ? "无提交路径"
        : readyPathCount > 0
          ? "可送服务端"
          : selectingPathCount > 0
            ? "等待选择"
            : blockedPathCount > 0
              ? "路径阻断"
              : "未进入草稿",
      value: commandBridgeRows.length > 0
        ? `${commandBridgeRows.length} 路径 / ${readyPathCount} 可送 / ${selectingPathCount} 待选 / ${blockedPathCount} 阻断 / ${inactivePathCount} 未进`
        : "无路径"
    },
    {
      key: "boundary",
      label: "边界",
      state: hiddenRefCount > 0 || missingRefCount > 0 ? "warn" : "ready",
      stateLabel: hiddenRefCount > 0
        ? "隐藏保护"
        : missingRefCount > 0
          ? "未公开对象"
          : "可公开",
      value: `${hiddenRefCount} 隐藏 / ${missingRefCount} 未公开`
    }
  ];
}

function commandBridgeRowsForDetail(
  detail: WireTimelineDetailLike,
  objectIndex: Record<string, CardObjectView>,
  prompt?: ActionPromptDto,
  selectionDraft?: CandidateSelectionDraft,
  disabledByConnection = false
): WireTimelineCommandBridgeRow[] {
  const promptModel = buildPromptInteractionModel(prompt);
  const rows: WireTimelineCommandBridgeRow[] = [];
  const seen = new Set<string>();
  const visibleRefs = detail.refs
    .map((ref) => ({ ref, objectId: ref.id.trim() }))
    .filter(({ objectId }) => objectId && objectId !== "HIDDEN" && objectIndex[objectId]);

  for (const { ref, objectId } of visibleRefs) {
    for (const candidate of promptModel.candidates) {
      const roleLabels = roleLabelsForObject(candidate, objectId);
      if (roleLabels.length === 0) {
        continue;
      }

      const detailRoleLabel = ref.role?.trim() || "详情对象";
      const key = `${candidate.action}:${candidate.label}:${detailRoleLabel}:${objectId}`;
      if (seen.has(key)) {
        continue;
      }
      seen.add(key);

      const draftState = commandBridgeDraftState(candidate, selectionDraft);
      const progressRoleLabels = draftState.draftActive ? draftState.selectedRoleLabels : roleLabels;
      const nextStep = nextStepForCommandBridge(candidate, progressRoleLabels);
      const commandFields = commandBridgeFieldRows(candidate, draftState);
      const grammar = buildFocusedInteractionGrammarPlan({
        candidates: [candidate],
        disabledByConnection,
        selectionDraft,
        sourceObjectId: draftState.draftActive ? selectionDraft?.sourceObjectId : undefined
      });
      const gateRows = commandBridgeGateRows({
        candidate,
        commandFields,
        disabledByConnection,
        draftState,
        grammarState: grammar.state,
        grammarSteps: grammar.steps
      });
      const routeState: WireTimelineCommandBridgeRouteState = disabledByConnection && draftState.draftActive
        ? "blocked"
        : draftState.routeState;
      const serverRoleSummary = roleLabels.join(" / ");
      const commandType = candidate.command?.cmdType ?? candidate.action;
      const nextStepLabel = nextStepLabelForCommandBridge(candidate, nextStep, draftState.draftActive);
      rows.push({
        commandFieldSummary: commandFieldSummary(commandFields),
        commandFields,
        commandType,
        detailLinkLabel: `详情${detailRoleLabel} / 候选${serverRoleSummary}`,
        detailObjectId: objectId,
        detailRoleLabel,
        draftActive: draftState.draftActive,
        enabled: candidate.enabled,
        grammarState: grammar.state,
        grammarStateLabel: grammar.stateLabel,
        grammarSteps: grammar.steps.map((step) => ({
          availableCount: step.availableCount,
          key: `${key}:${step.key}`,
          label: step.label,
          required: step.required,
          role: step.role,
          selectedCount: step.selectedCount,
          state: step.state,
          stateLabel: step.stateLabel
        })),
        grammarSummary: grammar.summary,
        gateRows,
        gateSummary: commandBridgeGateSummary(gateRows),
        key,
        label: candidate.label,
        missingRequiredCount: draftState.missingRequiredCount,
        nextObjectRefs: nextStep ? objectRefsForCommandBridge(candidate, nextStep.role, objectIndex) : [],
        nextStepLabel,
        reasonLabel: candidate.reason,
        roleLabels,
        routeState,
        routeStateLabel: routeStateLabel(routeState),
        selectedRoleLabels: draftState.selectedRoleLabels,
        serverRoleSummary,
        selectedStepCount: draftState.selectedStepCount,
        selectionLabel: selectionLabel(draftState.draftActive, draftState.selectedRoleLabels),
        stateLabel: candidate.enabled ? "可提交" : "暂不可提交",
        submitPlan: commandBridgeSubmitPlan({
          commandFields,
          commandType,
          gateRows,
          nextStepLabel,
          routeState
        }),
        totalStepCount: candidate.steps.length
      });
    }
  }

  return rows.sort((left, right) =>
    Number(right.enabled) - Number(left.enabled)
    || Number(right.draftActive) - Number(left.draftActive)
    || right.nextObjectRefs.length - left.nextObjectRefs.length
    || left.label.localeCompare(right.label, "zh-Hans-CN")
  ).slice(0, 6);
}

function commandBridgeGateRows({
  candidate,
  commandFields,
  disabledByConnection,
  draftState,
  grammarState,
  grammarSteps
}: {
  candidate: PromptCandidateSummary;
  commandFields: WireTimelineCommandBridgeFieldRow[];
  disabledByConnection: boolean;
  draftState: CommandBridgeDraftState;
  grammarState: FocusedInteractionGrammarState;
  grammarSteps: Array<{ role: string; state: FocusedInteractionGrammarStepState; stateLabel: string }>;
}): WireTimelineCommandBridgeGateRow[] {
  const missingRequiredFields = commandFields.filter((field) => field.state === "missing").length;
  const submitStep = grammarSteps.find((step) => step.role === "submit");
  return [
    {
      key: "server-candidate",
      label: "服务端候选",
      reason: candidate.reason,
      state: candidate.enabled ? "ready" : "blocked",
      stateLabel: candidate.enabled ? "开放" : "阻断"
    },
    {
      key: "connection",
      label: "连接状态",
      reason: disabledByConnection ? "连接恢复前不能提交命令" : "提交通道可用",
      state: disabledByConnection ? "blocked" : "ready",
      stateLabel: disabledByConnection ? "断开" : "可用"
    },
    {
      key: "player-draft",
      label: "玩家草稿",
      reason: draftState.draftActive ? selectionLabel(true, draftState.selectedRoleLabels) : "从候选对象开始选择",
      state: draftState.draftActive ? "ready" : "waiting",
      stateLabel: draftState.draftActive ? "已进入" : "等待"
    },
    {
      key: "required-fields",
      label: "必需字段",
      reason: commandFieldSummary(commandFields),
      state: missingRequiredFields > 0 ? "blocked" : "ready",
      stateLabel: missingRequiredFields > 0 ? `缺少 ${missingRequiredFields}` : "齐备"
    },
    {
      key: "submit-step",
      label: "提交步骤",
      reason: submitStep?.stateLabel ?? grammarStateLabel(grammarState),
      state: submitStep?.state === "ready" ? "ready" : "blocked",
      stateLabel: submitStep?.state === "ready" ? "可提交" : "未就绪"
    }
  ];
}

function commandBridgeGateSummary(rows: WireTimelineCommandBridgeGateRow[]): string {
  const readyCount = rows.filter((row) => row.state === "ready").length;
  const blockedCount = rows.filter((row) => row.state === "blocked").length;
  const waitingCount = rows.filter((row) => row.state === "waiting").length;
  return `${readyCount} 通过 / ${blockedCount} 阻断 / ${waitingCount} 等待`;
}

function commandBridgeSubmitPlan({
  commandFields,
  commandType,
  gateRows,
  nextStepLabel,
  routeState
}: {
  commandFields: WireTimelineCommandBridgeFieldRow[];
  commandType?: string;
  gateRows: WireTimelineCommandBridgeGateRow[];
  nextStepLabel: string;
  routeState: WireTimelineCommandBridgeRouteState;
}): WireTimelineCommandBridgeSubmitPlan {
  const firstBlockingGate = gateRows.find((row) => row.state === "blocked")
    ?? gateRows.find((row) => row.state === "waiting");
  const canSubmit = routeState === "ready" && !firstBlockingGate;
  const state: WireTimelineCommandBridgeSubmitState = canSubmit
    ? "ready"
    : routeState === "inactive"
      ? "inactive"
      : routeState === "selecting"
        ? "selecting"
        : "blocked";
  return {
    canSubmit,
    commandType,
    fieldCount: commandFields.length,
    fieldSummary: commandFieldSummary(commandFields),
    fields: commandFields.map((field) => ({
      field: field.field,
      key: `submit:${field.key}`,
      label: field.label,
      state: field.state,
      stateLabel: field.stateLabel
    })),
    firstBlockingGate,
    key: `submit:${commandType ?? "unknown"}:${state}`,
    nextStepLabel,
    reason: canSubmit
      ? "当前草稿已覆盖服务端模板所需字段，提交后仍由服务端规则校验。"
      : firstBlockingGate
        ? `${firstBlockingGate.label}：${firstBlockingGate.reason}`
        : nextStepLabel,
    state,
    stateLabel: commandBridgeSubmitStateLabel(state),
    submitLabel: canSubmit
      ? `提交 ${commandType ?? "服务端命令"}`
      : nextStepLabel
  };
}

function commandBridgeSubmitStateLabel(state: WireTimelineCommandBridgeSubmitState): string {
  switch (state) {
    case "blocked":
      return "提交阻断";
    case "inactive":
      return "未进入草稿";
    case "ready":
      return "可送服务端";
    case "selecting":
      return "草稿未齐";
  }
}

function commandBridgeFieldRows(
  candidate: PromptCandidateSummary,
  draftState: CommandBridgeDraftState
): WireTimelineCommandBridgeFieldRow[] {
  return (candidate.command?.bindings ?? []).map((binding, index) => {
    const state = commandBridgeFieldState(binding, draftState);
    return {
      field: binding.field,
      key: `${candidate.action}:${candidate.label}:${binding.field}:${index}`,
      label: promptCommandBindingLabel(binding),
      required: binding.required,
      roleLabel: binding.roleLabel,
      sourceLabel: promptCommandBindingSourceLabel(binding),
      state,
      stateLabel: commandBridgeFieldStateLabel(state)
    };
  });
}

function commandBridgeFieldState(
  binding: PromptCommandBindingSummary,
  draftState: CommandBridgeDraftState
): WireTimelineCommandBridgeFieldState {
  if (binding.source === "requirementMetadata") {
    return "server";
  }

  if (binding.role && draftState.selectedRoles.has(binding.role)) {
    return "covered";
  }

  return binding.required ? "missing" : "optional";
}

function commandBridgeFieldStateLabel(state: WireTimelineCommandBridgeFieldState): string {
  switch (state) {
    case "covered":
      return "已覆盖";
    case "missing":
      return "缺少选择";
    case "optional":
      return "可选";
    case "server":
      return "服务端注入";
  }
}

function commandFieldSummary(fields: WireTimelineCommandBridgeFieldRow[]): string {
  if (fields.length === 0) {
    return "命令字段未公开";
  }

  const coveredCount = fields.filter((field) => field.state === "covered").length;
  const missingCount = fields.filter((field) => field.state === "missing").length;
  const serverCount = fields.filter((field) => field.state === "server").length;
  return `${coveredCount} 覆盖 / ${missingCount} 缺少 / ${serverCount} 服务端`;
}

function commandBridgeStatusLabel(rows: WireTimelineCommandBridgeRow[]): string {
  const draftCount = rows.filter((row) => row.draftActive).length;
  return draftCount > 0 ? `${rows.length} 条 / ${draftCount} 草稿` : `${rows.length} 条`;
}

function grammarStateLabel(state: FocusedInteractionGrammarState): string {
  switch (state) {
    case "blocked":
      return "阻断";
    case "empty":
      return "无候选";
    case "incomplete":
      return "待选择";
    case "ready":
      return "可提交";
  }
}

type CommandBridgeDraftState = {
  draftActive: boolean;
  missingRequiredCount: number;
  routeState: WireTimelineCommandBridgeRouteState;
  selectedRoles: Set<PromptChoiceRole>;
  selectedRoleLabels: string[];
  selectedStepCount: number;
};

function commandBridgeDraftState(
  candidate: PromptCandidateSummary,
  selectionDraft?: CandidateSelectionDraft
): CommandBridgeDraftState {
  const draftActive = selectionDraft?.candidateKey === candidateComposerKey(candidate);
  const selectedRoles = draftActive ? selectedRolesForDraft(candidate, selectionDraft) : new Set<PromptChoiceRole>();
  const missingRequiredCount = draftActive
    ? candidate.steps.filter((step) => step.required && !selectedRoles.has(step.role)).length
    : 0;
  const selectedStepCount = draftActive
    ? candidate.steps.filter((step) => selectedRoles.has(step.role)).length
    : 0;
  const routeState: WireTimelineCommandBridgeRouteState = !draftActive
    ? "inactive"
    : !candidate.enabled
      ? "blocked"
      : missingRequiredCount > 0
        ? "selecting"
        : "ready";

  return {
    draftActive,
    missingRequiredCount,
    routeState,
    selectedRoles,
    selectedRoleLabels: uniqueStrings([...selectedRoles].map(promptChoiceRoleLabel)),
    selectedStepCount
  };
}

function selectedRolesForDraft(
  candidate: PromptCandidateSummary,
  selectionDraft?: CandidateSelectionDraft
): Set<PromptChoiceRole> {
  const selectedRoles = new Set<PromptChoiceRole>();
  if (!selectionDraft) {
    return selectedRoles;
  }

  for (const choice of candidate.choices) {
    if (choiceSelectedForDraft(choice, selectionDraft)) {
      selectedRoles.add(choice.role);
    }
  }

  return selectedRoles;
}

function choiceSelectedForDraft(choice: PromptCandidateSummary["choices"][number], selectionDraft: CandidateSelectionDraft): boolean {
  switch (choice.role) {
    case "source":
      return choiceMatchesDraftValue(choice, selectionDraft.sourceObjectId);
    case "target":
      return selectionDraft.targetChoiceIds.some((id) => choiceMatchesDraftValue(choice, id));
    case "destination":
      return choiceMatchesDraftValue(choice, selectionDraft.destinationId);
    case "mode":
      return choiceMatchesDraftValue(choice, selectionDraft.mode);
    case "optionalCost":
      return selectionDraft.optionalCostIds.some((id) => choiceMatchesDraftValue(choice, id));
  }
}

function choiceMatchesDraftValue(choice: PromptCandidateSummary["choices"][number], value?: string): boolean {
  if (!value) {
    return false;
  }

  return choice.id === value || promptChoiceSummaryObjectIds(choice).includes(value);
}

function routeStateLabel(state: WireTimelineCommandBridgeRouteState): string {
  switch (state) {
    case "blocked":
      return "草稿阻断";
    case "inactive":
      return "未进入草稿";
    case "ready":
      return "可送服务端校验";
    case "selecting":
      return "缺少必需选择";
  }
}

function selectionLabel(draftActive: boolean, selectedRoleLabels: string[]): string {
  if (!draftActive) {
    return "未进入草稿";
  }

  return selectedRoleLabels.length > 0 ? `已选 ${selectedRoleLabels.join(" / ")}` : "草稿未选步骤";
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
    const key = `${ref.role || "对象"}:${id}`;
    if (!id || id === "HIDDEN" || seen.has(key)) {
      continue;
    }
    seen.add(key);

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
      key,
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

    const state = projectionState(ref, objectIndex, selectedObjectId);
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
  ref: WireTimelineDetailRefLike,
  objectIndex: Record<string, CardObjectView>,
  selectedObjectId?: string
): WireTimelineProjectionState {
  const id = ref.id.trim();
  if (ref.visibility === "hidden") {
    return "hidden";
  }

  if (ref.visibility === "missing") {
    return "missing";
  }

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
  nextStep: PromptCandidateSummary["steps"][number] | undefined,
  draftActive: boolean
): string {
  if (nextStep) {
    return nextStep.required ? `需要${nextStep.label}` : `可选${nextStep.label}`;
  }

  return draftActive && candidate.enabled ? "草稿可送服务端校验" : candidate.enabled ? "可提交给服务端" : "等待服务端窗口";
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

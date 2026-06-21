import type { BehaviorSpec } from "../types/catalog";
import type { CardObjectView } from "../types/protocol";
import { isHiddenObject } from "./hiddenInfo";
import {
  tableObjectContextSourceLabel,
  type TableObjectContext
} from "./tableObjectContext";
import type { WireFocusedInteractionPlan } from "./wireFocusedInteractionPlan";

export type WireObjectCommandTrayState = "blocked" | "empty" | "readonly" | "ready" | "selecting";
export type WireObjectCommandTrayTone = "good" | "neutral" | "warn";

export type WireObjectCommandTrayCard = {
  object?: CardObjectView;
  objectId?: string;
  spec?: Pick<BehaviorSpec, "cardCategoryName" | "cardName" | "cardNo">;
};

export type WireObjectCommandTrayMetric = {
  key: string;
  label: string;
  value: string;
};

export type WireObjectCommandTrayContextRow = {
  key: string;
  label: string;
  tone: "neutral" | "warn";
  value: string;
};

export type WireObjectCommandTraySemanticRow = {
  category: string;
  count: number;
  intent: string;
  key: string;
  priority: number;
  uiHint: string;
};

export type WireObjectCommandTrayPlan = {
  actionLimit: number;
  canShowActions: boolean;
  contextRows: WireObjectCommandTrayContextRow[];
  metrics: WireObjectCommandTrayMetric[];
  nextStepLabel: string;
  objectId?: string;
  primaryLabel: string;
  semanticRows: WireObjectCommandTraySemanticRow[];
  semanticSummary: string;
  state: WireObjectCommandTrayState;
  stateLabel: string;
  subtitle: string;
  title: string;
  tone: WireObjectCommandTrayTone;
  visible: boolean;
};

export function buildWireObjectCommandTrayPlan({
  card,
  focusedPlan,
  objectContext
}: {
  card?: WireObjectCommandTrayCard;
  focusedPlan: WireFocusedInteractionPlan;
  objectContext?: TableObjectContext;
}): WireObjectCommandTrayPlan {
  const objectId = card?.objectId ?? card?.object?.objectId;
  if (!card || !objectId) {
    return emptyPlan;
  }

  const hidden = isHiddenObject(card.object) && !card.spec;
  const state = hidden ? "readonly" : stateFromReadiness(focusedPlan);
  const enabledCount = hidden ? 0 : focusedPlan.readiness.enabledCount;
  const blockedCount = hidden ? 0 : focusedPlan.readiness.blockedCount;
  const roleLabel = hidden ? "隐藏" : roleSummary(focusedPlan);
  const commandLabel = hidden ? "不公开" : focusedPlan.readiness.commandType ?? "无";
  const sourceLabel = objectContext ? tableObjectContextSourceLabel(objectContext) : "公开快照索引";
  const semanticRows = hidden ? [] : semanticRowsFor(focusedPlan, objectContext);
  const semanticSummary = hidden ? "不公开" : semanticSummaryFor(semanticRows);
  const canShowActions = !hidden
    && focusedPlan.actionEntries.length > 0
    && state !== "readonly"
    && state !== "empty";

  return {
    actionLimit: 3,
    canShowActions,
    contextRows: hidden ? [] : objectCommandContextRows(objectContext, sourceLabel),
    metrics: [
      { key: "candidate", label: "候选", value: `${enabledCount} 可用 / ${blockedCount} 阻断` },
      { key: "semantic", label: "动作", value: semanticSummary },
      { key: "role", label: "角色", value: roleLabel },
      { key: "command", label: "命令", value: commandLabel },
      { key: "gate", label: "门禁", value: focusedPlan.submissionGate.stateLabel },
      { key: "window", label: "窗口", value: focusedPlan.windowGate.stateLabel }
    ],
    nextStepLabel: hidden
      ? "隐藏对象只可检查服务端公开外壳，不展示或提交前端推断操作。"
      : focusedPlan.readiness.nextStepLabel,
    objectId,
    primaryLabel: primaryLabelFor(state, focusedPlan),
    semanticRows,
    semanticSummary,
    state,
    stateLabel: stateLabelFor(state),
    subtitle: subtitleFor(card, objectContext, sourceLabel),
    title: titleFor(card, hidden, objectId),
    tone: toneFor(state),
    visible: true
  };
}

const emptyPlan: WireObjectCommandTrayPlan = {
  actionLimit: 0,
  canShowActions: false,
  contextRows: [],
  metrics: [],
  nextStepLabel: "点击桌面上的公开卡牌查看服务端候选和下一步。",
  primaryLabel: "等待焦点",
  semanticRows: [],
  semanticSummary: "无",
  state: "empty",
  stateLabel: "无焦点",
  subtitle: "未选择对象",
  title: "未选择卡牌",
  tone: "neutral",
  visible: false
};

function stateFromReadiness(focusedPlan: WireFocusedInteractionPlan): WireObjectCommandTrayState {
  switch (focusedPlan.readiness.state) {
    case "ready":
      return "ready";
    case "needs-selection":
      return "selecting";
    case "no-focus":
      return "empty";
    case "not-candidate":
      return "readonly";
    case "server-blocked":
    case "submission-gate-blocked":
    case "window-blocked":
      return "blocked";
  }
}

function stateLabelFor(state: WireObjectCommandTrayState): string {
  switch (state) {
    case "blocked":
      return "阻断";
    case "empty":
      return "无焦点";
    case "readonly":
      return "只读";
    case "ready":
      return "可提交";
    case "selecting":
      return "待选择";
  }
}

function toneFor(state: WireObjectCommandTrayState): WireObjectCommandTrayTone {
  switch (state) {
    case "ready":
      return "good";
    case "blocked":
    case "selecting":
      return "warn";
    case "empty":
    case "readonly":
      return "neutral";
  }
}

function primaryLabelFor(state: WireObjectCommandTrayState, focusedPlan: WireFocusedInteractionPlan): string {
  switch (state) {
    case "ready":
      return focusedPlan.readiness.commandType ? `提交 ${focusedPlan.readiness.commandType}` : "提交候选";
    case "selecting":
      return focusedPlan.readiness.nextStepLabel;
    case "blocked":
      return "等待服务端窗口";
    case "readonly":
      return "查看对象";
    case "empty":
      return "选择对象";
  }
}

function titleFor(card: WireObjectCommandTrayCard, hidden: boolean, objectId: string): string {
  if (hidden) {
    return "未公开卡牌";
  }

  return card.spec?.cardName?.trim()
    || card.object?.cardNo?.trim()
    || card.spec?.cardNo?.trim()
    || objectId;
}

function subtitleFor(
  card: WireObjectCommandTrayCard,
  objectContext: TableObjectContext | undefined,
  sourceLabel: string
): string {
  const zoneLabel = objectContext?.zone.label ?? "未定位区域";
  const typeLabel = card.spec?.cardCategoryName?.trim() || "公开对象";
  return `${zoneLabel} / ${typeLabel} / ${sourceLabel}`;
}

function roleSummary(focusedPlan: WireFocusedInteractionPlan): string {
  const roles = uniqueStrings(focusedPlan.legalActionRows.flatMap((row) => row.roleLabels));
  return roles.length > 0 ? compactList(roles, 3) : "无";
}

function semanticRowsFor(
  focusedPlan: WireFocusedInteractionPlan,
  objectContext: TableObjectContext | undefined
): WireObjectCommandTraySemanticRow[] {
  const byKey = new Map<string, WireObjectCommandTraySemanticRow>();
  if (objectContext?.candidateLinks.length) {
    for (const candidate of objectContext.candidateLinks) {
      addSemanticRow(byKey, {
        action: candidate.commandType ?? candidate.label,
        category: candidate.category,
        intent: candidate.intent,
        priority: candidate.priority,
        uiHint: candidate.uiHint
      });
    }

    return sortedSemanticRows(byKey);
  }

  for (const row of focusedPlan.legalActionRows) {
    addSemanticRow(byKey, {
      action: row.action,
      category: row.category,
      intent: row.intent,
      priority: row.priority,
      uiHint: row.uiHint
    });
  }

  return sortedSemanticRows(byKey);
}

function addSemanticRow(
  byKey: Map<string, WireObjectCommandTraySemanticRow>,
  candidate: {
    action: string;
    category?: string;
    intent?: string;
    priority?: number;
    uiHint?: string;
  }
): void {
  const category = candidate.category?.trim() || "custom";
  const intent = candidate.intent?.trim() || candidate.action.toLowerCase().replaceAll("_", "-");
  const uiHint = candidate.uiHint?.trim() || "card-action";
  const priority = typeof candidate.priority === "number" && Number.isFinite(candidate.priority) ? candidate.priority : 700;
  const key = `${category}:${intent}:${uiHint}`;
  const existing = byKey.get(key);
  if (existing) {
    existing.count += 1;
    existing.priority = Math.min(existing.priority, priority);
    return;
  }

  byKey.set(key, {
    category,
    count: 1,
    intent,
    key,
    priority,
    uiHint
  });
}

function sortedSemanticRows(byKey: Map<string, WireObjectCommandTraySemanticRow>): WireObjectCommandTraySemanticRow[] {
  return [...byKey.values()].sort((left, right) =>
    left.priority - right.priority
    || left.category.localeCompare(right.category)
    || left.intent.localeCompare(right.intent));
}

function semanticSummaryFor(rows: WireObjectCommandTraySemanticRow[]): string {
  if (rows.length === 0) {
    return "无服务端动作语义";
  }

  return compactList(rows.map((row) => `${row.category}/${row.intent}${row.count > 1 ? ` x${row.count}` : ""}`), 2);
}

function objectCommandContextRows(
  objectContext: TableObjectContext | undefined,
  sourceLabel: string
): WireObjectCommandTrayContextRow[] {
  if (!objectContext) {
    return [
      { key: "source", label: "来源", tone: "neutral", value: sourceLabel },
      { key: "fields", label: "字段", tone: "neutral", value: "无提交字段" }
    ];
  }

  const requiredFields = uniqueStrings(objectContext.candidateLinks.flatMap((candidate) => candidate.requiredCommandFields));
  const commandFields = uniqueStrings(objectContext.candidateLinks.flatMap((candidate) => candidate.commandFields));
  const blockedReasons = uniqueStrings(objectContext.candidateLinks
    .filter((candidate) => !candidate.enabled)
    .map((candidate) => candidate.reason));
  const rows: WireObjectCommandTrayContextRow[] = [
    { key: "source", label: "来源", tone: "neutral", value: sourceLabel },
    {
      key: "fields",
      label: "字段",
      tone: requiredFields.length > 0 ? "warn" : "neutral",
      value: requiredFields.length > 0 || commandFields.length > 0
        ? `${requiredFields.length} 必填 / ${commandFields.length} 公开`
        : "无提交字段"
    }
  ];

  if (blockedReasons.length > 0) {
    rows.push({
      key: "blocked",
      label: "阻断",
      tone: "warn",
      value: compactList(blockedReasons, 2)
    });
  }

  rows.push({
    key: "boundary",
    label: "边界",
    tone: "neutral",
    value: boundarySummary(objectContext.contextBoundary)
  });

  return rows;
}

function boundarySummary(boundary: string | undefined): string {
  const text = boundary?.trim();
  if (!text) {
    return "只读公开对象";
  }

  if (text.includes("隐藏 metadata")) {
    return "不公开隐藏 metadata";
  }

  return text.length > 24 ? `${text.slice(0, 24)}...` : text;
}

function uniqueStrings(values: string[]): string[] {
  return [...new Set(values.map((value) => value.trim()).filter(Boolean))];
}

function compactList(values: string[], limit: number): string {
  const visible = values.slice(0, limit);
  return values.length > limit
    ? `${visible.join(" / ")} +${values.length - limit}`
    : visible.join(" / ");
}

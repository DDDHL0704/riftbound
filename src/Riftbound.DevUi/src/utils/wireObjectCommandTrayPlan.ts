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

export type WireObjectCommandTrayPlan = {
  actionLimit: number;
  canShowActions: boolean;
  metrics: WireObjectCommandTrayMetric[];
  nextStepLabel: string;
  objectId?: string;
  primaryLabel: string;
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

  return {
    actionLimit: 3,
    canShowActions: !hidden && focusedPlan.actionEntries.length > 0,
    metrics: [
      { key: "candidate", label: "候选", value: `${enabledCount} 可用 / ${blockedCount} 阻断` },
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
  metrics: [],
  nextStepLabel: "点击桌面上的公开卡牌查看服务端候选和下一步。",
  primaryLabel: "等待焦点",
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

function uniqueStrings(values: string[]): string[] {
  return [...new Set(values.map((value) => value.trim()).filter(Boolean))];
}

function compactList(values: string[], limit: number): string {
  const visible = values.slice(0, limit);
  return values.length > limit
    ? `${visible.join(" / ")} +${values.length - limit}`
    : visible.join(" / ");
}

import type { WireSidePanelSlot } from "../components/match/wireTableLayout";
import type { WireTimelineDetail } from "../components/match/WireTimelineDetailPanel";
import type { TableObjectContext } from "./tableObjectContext";
import type {
  WireObjectCommandTrayPlan,
  WireObjectCommandTrayState,
  WireObjectCommandTrayTone
} from "./wireObjectCommandTrayPlan";
import type {
  WireRuleQueueSelectedObjectPlan,
  WireRuleQueueSelectedObjectRelation,
  WireRuleQueueSelectedObjectRelationState
} from "./wireRuleQueuePlan";

export type WireSidePanelFocusRoute = {
  actionLabel: string;
  key: "actions" | "detail" | "map" | "rules";
  label: string;
  reason: string;
  slot?: WireSidePanelSlot;
  state: "available" | "disabled";
  stateLabel: string;
  targetKind: "drawer" | "side-panel";
  targetLabel: string;
};

export type WireSidePanelFocusMetric = {
  key: "candidate" | "command" | "gate" | "window";
  label: string;
  value: string;
};

export type WireSidePanelFocusContextMetric = {
  key: "event" | "relation" | "source" | "syntax";
  label: string;
  value: string;
};

export type WireSidePanelFocusRelation = {
  detail?: WireTimelineDetail;
  key: string;
  label: string;
  sourceLabel: string;
  state: WireRuleQueueSelectedObjectRelationState;
  stateLabel: string;
  value: string;
};

export type WireSidePanelFocusPlan = {
  contextMetrics: WireSidePanelFocusContextMetric[];
  eventCount: number;
  metrics: WireSidePanelFocusMetric[];
  nextStepLabel: string;
  objectId?: string;
  relationCount: number;
  relations: WireSidePanelFocusRelation[];
  routes: WireSidePanelFocusRoute[];
  state: WireObjectCommandTrayState;
  stateLabel: string;
  subtitle: string;
  title: string;
  tone: WireObjectCommandTrayTone;
  visible: boolean;
};

export function buildWireSidePanelFocusPlan({
  objectContext,
  selectedObjectPlan,
  trayPlan
}: {
  objectContext?: TableObjectContext;
  selectedObjectPlan?: WireRuleQueueSelectedObjectPlan;
  trayPlan: WireObjectCommandTrayPlan;
}): WireSidePanelFocusPlan {
  if (!trayPlan.visible) {
    return {
      contextMetrics: [],
      eventCount: 0,
      metrics: [],
      nextStepLabel: trayPlan.nextStepLabel,
      relationCount: 0,
      relations: [],
      routes: focusRoutes({ canShowActions: false, hasRuleProjection: false, visible: false }),
      state: "empty",
      stateLabel: trayPlan.stateLabel,
      subtitle: trayPlan.subtitle,
      title: trayPlan.title,
      tone: trayPlan.tone,
      visible: false
    };
  }

  const relations = focusRelations(selectedObjectPlan);
  return {
    contextMetrics: focusContextMetrics({ objectContext, selectedObjectPlan }),
    eventCount: objectContext?.eventLinks.length ?? 0,
    metrics: focusMetrics(trayPlan),
    nextStepLabel: trayPlan.nextStepLabel,
    objectId: trayPlan.objectId,
    relationCount: selectedObjectPlan?.relationCount ?? 0,
    relations,
    routes: focusRoutes({
      canShowActions: trayPlan.canShowActions,
      hasRuleProjection: Boolean(selectedObjectPlan?.objectId),
      visible: true
    }),
    state: trayPlan.state,
    stateLabel: trayPlan.stateLabel,
    subtitle: trayPlan.subtitle,
    title: trayPlan.title,
    tone: trayPlan.tone,
    visible: true
  };
}

function focusContextMetrics({
  objectContext,
  selectedObjectPlan
}: {
  objectContext?: TableObjectContext;
  selectedObjectPlan?: WireRuleQueueSelectedObjectPlan;
}): WireSidePanelFocusContextMetric[] {
  return [
    { key: "relation", label: "关联", value: `${selectedObjectPlan?.relationCount ?? 0}` },
    { key: "event", label: "事件", value: `${objectContext?.eventLinks.length ?? 0}` },
    { key: "syntax", label: "语法", value: selectedObjectPlan?.syntaxSummary ?? "无" },
    { key: "source", label: "来源", value: contextSourceLabel(objectContext) }
  ];
}

function focusRelations(selectedObjectPlan?: WireRuleQueueSelectedObjectPlan): WireSidePanelFocusRelation[] {
  return (selectedObjectPlan?.relations ?? []).slice(0, 2).map((relation) => ({
    detail: relation.detail ? ruleDetailAsTimelineDetail(relation) : undefined,
    key: relation.key,
    label: relation.detailLabel,
    sourceLabel: relation.sourceLabel,
    state: relation.state,
    stateLabel: relation.stateLabel,
    value: relationValue(relation)
  }));
}

function ruleDetailAsTimelineDetail(relation: WireRuleQueueSelectedObjectRelation): WireTimelineDetail | undefined {
  const detail = relation.detail;
  if (!detail) {
    return undefined;
  }

  return {
    id: detail.id,
    lines: detail.lines,
    refs: detail.refs,
    source: detail.source,
    subtitle: detail.subtitle,
    title: detail.title
  };
}

function relationValue(relation: WireRuleQueueSelectedObjectRelation): string {
  const parts = [
    relation.laneLabel,
    relation.roleLabel,
    relation.candidateActions.length > 0 ? relation.candidateActions.join("/") : undefined
  ].filter((part): part is string => Boolean(part && part.trim()));
  return parts.join(" / ") || "服务端关联";
}

function focusMetrics(plan: WireObjectCommandTrayPlan): WireSidePanelFocusMetric[] {
  return [
    metric(plan, "candidate", "候选"),
    metric(plan, "command", "命令"),
    metric(plan, "gate", "门禁"),
    metric(plan, "window", "窗口")
  ];
}

function metric(
  plan: WireObjectCommandTrayPlan,
  key: WireSidePanelFocusMetric["key"],
  label: string
): WireSidePanelFocusMetric {
  return {
    key,
    label,
    value: plan.metrics.find((item) => item.key === key)?.value ?? "无"
  };
}

function focusRoutes({
  canShowActions,
  hasRuleProjection,
  visible
}: {
  canShowActions: boolean;
  hasRuleProjection: boolean;
  visible: boolean;
}): WireSidePanelFocusRoute[] {
  const actionState = canShowActions ? "available" : "disabled";
  const detailState = visible ? "available" : "disabled";
  const ruleState = visible && hasRuleProjection ? "available" : "disabled";
  const noFocusReason = "未选择公开对象。";
  return [
    {
      actionLabel: actionState === "available" ? "查看操作" : "无操作",
      key: "actions",
      label: "操作",
      reason: !visible ? noFocusReason : canShowActions ? "进入焦点候选面板，查看服务端公开的候选与提交路线。" : "该对象当前没有服务端可展示候选。",
      slot: "interaction",
      state: actionState,
      stateLabel: actionState === "available" ? "可进入" : "不可用",
      targetKind: "side-panel",
      targetLabel: "焦点候选"
    },
    {
      actionLabel: actionState === "available" ? "查看地图" : "无地图",
      key: "map",
      label: "地图",
      reason: !visible ? noFocusReason : canShowActions ? "进入合法操作地图，查看对象在当前服务端候选中的位置。" : "该对象当前没有可映射到服务端候选的操作。",
      slot: "actionMap",
      state: actionState,
      stateLabel: actionState === "available" ? "可进入" : "不可用",
      targetKind: "side-panel",
      targetLabel: "合法操作"
    },
    {
      actionLabel: ruleState === "available" ? "查看规则" : "无规则",
      key: "rules",
      label: "规则",
      reason: !visible ? noFocusReason : hasRuleProjection ? "进入规则队列，查看该对象的结算链、事件和服务端流程关联。" : "当前规则队列没有投影到该对象。",
      slot: "ruleQueue",
      state: ruleState,
      stateLabel: ruleState === "available" ? "可进入" : "不可用",
      targetKind: "side-panel",
      targetLabel: "规则队列"
    },
    {
      actionLabel: detailState === "available" ? "打开详情" : "无详情",
      key: "detail",
      label: "详情",
      reason: visible ? "打开卡牌详情抽屉，查看对象上下文、服务端检查摘要和卡牌文本。" : noFocusReason,
      state: detailState,
      stateLabel: detailState === "available" ? "可打开" : "不可用",
      targetKind: "drawer",
      targetLabel: "卡牌详情"
    }
  ];
}

function contextSourceLabel(context: TableObjectContext | undefined): string {
  switch (context?.contextSource) {
    case "server-action-prompt":
      return "服务端对象";
    case "server-flow-related-object":
      return "服务端流程";
    case "prompt-public-derived":
      return "公开派生";
    case "snapshot-public-index":
      return "公开快照";
    default:
      return "无";
  }
}

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
  key: "actions" | "detail" | "map" | "rules";
  label: string;
  slot?: WireSidePanelSlot;
  state: "available" | "disabled";
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
  return [
    { key: "actions", label: "操作", slot: "interaction", state: actionState },
    { key: "map", label: "地图", slot: "actionMap", state: actionState },
    { key: "rules", label: "规则", slot: "ruleQueue", state: ruleState },
    { key: "detail", label: "详情", state: detailState }
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

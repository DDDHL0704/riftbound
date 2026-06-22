import type { WireSidePanelSlot } from "../components/match/wireTableLayout";
import type {
  WireObjectCommandTrayPlan,
  WireObjectCommandTrayState,
  WireObjectCommandTrayTone
} from "./wireObjectCommandTrayPlan";

export type WireSidePanelFocusRoute = {
  key: "actions" | "detail" | "map";
  label: string;
  slot?: WireSidePanelSlot;
  state: "available" | "disabled";
};

export type WireSidePanelFocusMetric = {
  key: "candidate" | "command" | "gate" | "window";
  label: string;
  value: string;
};

export type WireSidePanelFocusPlan = {
  metrics: WireSidePanelFocusMetric[];
  nextStepLabel: string;
  objectId?: string;
  routes: WireSidePanelFocusRoute[];
  state: WireObjectCommandTrayState;
  stateLabel: string;
  subtitle: string;
  title: string;
  tone: WireObjectCommandTrayTone;
  visible: boolean;
};

export function buildWireSidePanelFocusPlan({
  trayPlan
}: {
  trayPlan: WireObjectCommandTrayPlan;
}): WireSidePanelFocusPlan {
  if (!trayPlan.visible) {
    return {
      metrics: [],
      nextStepLabel: trayPlan.nextStepLabel,
      routes: focusRoutes({ canShowActions: false, visible: false }),
      state: "empty",
      stateLabel: trayPlan.stateLabel,
      subtitle: trayPlan.subtitle,
      title: trayPlan.title,
      tone: trayPlan.tone,
      visible: false
    };
  }

  return {
    metrics: focusMetrics(trayPlan),
    nextStepLabel: trayPlan.nextStepLabel,
    objectId: trayPlan.objectId,
    routes: focusRoutes({ canShowActions: trayPlan.canShowActions, visible: true }),
    state: trayPlan.state,
    stateLabel: trayPlan.stateLabel,
    subtitle: trayPlan.subtitle,
    title: trayPlan.title,
    tone: trayPlan.tone,
    visible: true
  };
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
  visible
}: {
  canShowActions: boolean;
  visible: boolean;
}): WireSidePanelFocusRoute[] {
  const actionState = canShowActions ? "available" : "disabled";
  const detailState = visible ? "available" : "disabled";
  return [
    { key: "actions", label: "操作", slot: "interaction", state: actionState },
    { key: "map", label: "地图", slot: "actionMap", state: actionState },
    { key: "detail", label: "详情", state: detailState }
  ];
}

import type { WireSidePanelSlot } from "../components/match/wireTableLayout";
import type {
  WireRuleQueueDetailPlan,
  WireRuleQueueLaneKey,
  WireRuleQueueLaneState,
  WireRuleQueuePlan,
  WireRuleQueueState
} from "./wireRuleQueuePlan";

export type WireSidePanelRuleChainMetric = {
  key: "detail" | "event" | "lane" | "responsibility";
  label: string;
  value: string;
};

export type WireSidePanelRuleChainLane = {
  count: number;
  detailId?: string;
  key: WireRuleQueueLaneKey;
  label: string;
  state: WireRuleQueueLaneState;
  stateLabel: string;
};

export type WireSidePanelRuleChainRoute = {
  key: "detail" | "flow" | "log" | "queue";
  label: string;
  slot: WireSidePanelSlot;
  state: "available" | "disabled";
};

export type WireSidePanelRuleChainPlan = {
  activeLaneKey: WireRuleQueueLaneKey | "none";
  activeLaneLabel: string;
  detail?: WireRuleQueueDetailPlan;
  lanes: WireSidePanelRuleChainLane[];
  metrics: WireSidePanelRuleChainMetric[];
  nextStepLabel: string;
  routes: WireSidePanelRuleChainRoute[];
  state: WireRuleQueueState;
  stateLabel: string;
  subtitle: string;
  title: string;
};

export function buildWireSidePanelRuleChainPlan({
  ruleQueuePlan
}: {
  ruleQueuePlan: WireRuleQueuePlan;
}): WireSidePanelRuleChainPlan {
  const detail = focusDetail(ruleQueuePlan);
  const activeLane = ruleQueuePlan.lanes.find((lane) => lane.key === ruleQueuePlan.activeLaneKey);

  return {
    activeLaneKey: ruleQueuePlan.activeLaneKey,
    activeLaneLabel: activeLane?.label ?? "无",
    detail,
    lanes: ruleQueuePlan.lanes.map((lane) => ({
      count: lane.count,
      detailId: lane.detail?.id,
      key: lane.key,
      label: lane.label,
      state: lane.state,
      stateLabel: laneStateLabel(lane.state)
    })),
    metrics: [
      { key: "lane", label: "通道", value: activeLane?.label ?? "无" },
      {
        key: "responsibility",
        label: "责任",
        value: `${ruleQueuePlan.responsibility.activeCount} 活动 / ${ruleQueuePlan.responsibility.submitReadyCount} 可提交`
      },
      {
        key: "event",
        label: "事件",
        value: `${ruleQueuePlan.eventSummary.totalEventCount} 件 / ${ruleQueuePlan.eventSummary.activeCount} 类`
      },
      { key: "detail", label: "详情", value: detail ? detail.title : "无" }
    ],
    nextStepLabel: ruleQueuePlan.nextStepLabel,
    routes: [
      { key: "queue", label: "队列", slot: "ruleQueue", state: "available" },
      { key: "flow", label: "流程", slot: "serverFlow", state: "available" },
      { key: "detail", label: "详情", slot: "timelineDetail", state: detail ? "available" : "disabled" },
      {
        key: "log",
        label: "日志",
        slot: "log",
        state: ruleQueuePlan.eventSummary.totalEventCount > 0 ? "available" : "disabled"
      }
    ],
    state: ruleQueuePlan.state,
    stateLabel: ruleQueuePlan.stateLabel,
    subtitle: ruleQueuePlan.responsibility.summary,
    title: "规则链"
  };
}

function focusDetail(plan: WireRuleQueuePlan): WireRuleQueueDetailPlan | undefined {
  return plan.focus.detail
    ?? plan.sequence.find((item) => item.detail)?.detail
    ?? plan.eventSummary.rows.find((row) => row.detail)?.detail;
}

function laneStateLabel(state: WireRuleQueueLaneState): string {
  switch (state) {
    case "active":
      return "当前";
    case "blocked":
      return "阻塞";
    case "empty":
      return "空";
    case "waiting":
      return "等待";
  }
}

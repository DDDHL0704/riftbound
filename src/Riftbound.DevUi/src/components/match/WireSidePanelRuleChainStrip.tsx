import { FileText } from "lucide-react";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";
import type { WireTimelineDetail } from "./WireTimelineDetailPanel";
import type { WireSidePanelSlot } from "./wireTableLayout";
import type { WireSidePanelRuleChainPlan } from "../../utils/wireSidePanelRuleChainPlan";

type WireSidePanelRuleChainStripProps = {
  onSelectDetail: (detail: WireTimelineDetail) => void;
  onSelectSlot: (slot: WireSidePanelSlot) => void;
  plan: WireSidePanelRuleChainPlan;
};

export function WireSidePanelRuleChainStrip({
  onSelectDetail,
  onSelectSlot,
  plan
}: WireSidePanelRuleChainStripProps) {
  const openDetail = () => {
    if (!plan.detail) {
      return;
    }

    onSelectDetail(plan.detail);
    onSelectSlot("timelineDetail");
  };

  return (
    <section
      aria-label="右侧规则链摘要"
      className="wire-side-panel-rule-chain-strip"
      data-wire-side-panel-rule-chain-active-lane={plan.activeLaneKey}
      data-wire-side-panel-rule-chain-detail-id={plan.detail?.id ?? ""}
      data-wire-side-panel-rule-chain-state={plan.state}
    >
      <header>
        <div>
          <small>规则</small>
          <strong>{plan.title}</strong>
          <span>{plan.subtitle}</span>
        </div>
        <StatusPill tone={ruleChainTone(plan.state)}>{plan.stateLabel}</StatusPill>
      </header>

      <ol className="wire-side-panel-rule-chain-lanes" aria-label="规则链通道">
        {plan.lanes.map((lane) => (
          <li
            data-wire-side-panel-rule-chain-lane={lane.key}
            data-wire-side-panel-rule-chain-lane-count={lane.count}
            data-wire-side-panel-rule-chain-lane-detail-id={lane.detailId ?? ""}
            data-wire-side-panel-rule-chain-lane-state={lane.state}
            key={lane.key}
          >
            <span>{lane.label}</span>
            <strong>{lane.count}</strong>
            <small>{lane.stateLabel}</small>
          </li>
        ))}
      </ol>

      <dl className="wire-side-panel-rule-chain-metrics" aria-label="规则链摘要指标">
        {plan.metrics.map((metric) => (
          <div data-wire-side-panel-rule-chain-metric={metric.key} key={metric.key}>
            <dt>{metric.label}</dt>
            <dd>{metric.value}</dd>
          </div>
        ))}
      </dl>

      <div className="wire-side-panel-rule-chain-next">
        <small>下一步</small>
        <strong>{plan.nextStepLabel}</strong>
      </div>

      <div className="wire-side-panel-rule-chain-routes" aria-label="规则链导航">
        {plan.routes.map((route) => (
          <button
            data-wire-side-panel-rule-chain-route={route.key}
            data-wire-side-panel-rule-chain-route-state={route.state}
            disabled={route.state === "disabled"}
            key={route.key}
            onClick={() => {
              if (route.key === "detail") {
                openDetail();
                return;
              }

              onSelectSlot(route.slot);
            }}
            type="button"
          >
            {route.label}
          </button>
        ))}
        <Button disabled={!plan.detail} icon={<FileText size={14} />} onClick={openDetail} variant="secondary">事件</Button>
      </div>
    </section>
  );
}

function ruleChainTone(state: WireSidePanelRuleChainPlan["state"]): "good" | "info" | "neutral" | "warn" {
  switch (state) {
    case "task-blocked":
    case "trigger-pending":
      return "warn";
    case "stack-response":
    case "task-open":
      return "info";
    case "resolution-history":
      return "good";
    case "idle":
      return "neutral";
  }
}

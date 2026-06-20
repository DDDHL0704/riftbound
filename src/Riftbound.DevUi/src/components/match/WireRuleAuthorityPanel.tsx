import type { GameEvent, SnapshotDto } from "../../types/protocol";
import { StatusPill } from "../ui/StatusPill";
import { buildWireRuleAuthorityPlan, type WireRuleAuthorityState } from "./wireRuleAuthorityPlan";

export function WireRuleAuthorityPanel({
  events,
  snapshot
}: {
  events?: GameEvent[];
  snapshot?: SnapshotDto;
}) {
  const plan = buildWireRuleAuthorityPlan({ events, snapshot });

  return (
    <section
      aria-label="服务端规则材料契约"
      className="wire-rule-authority"
      data-wire-rule-authority-state={plan.state}
    >
      <header className="wire-rule-authority-header">
        <div>
          <strong>规则材料契约</strong>
          <span>{plan.summary}</span>
        </div>
        <StatusPill tone={stateTone(plan.state)}>{plan.stateLabel}</StatusPill>
      </header>

      <div className="wire-rule-authority-metrics">
        {plan.metrics.map((metric) => (
          <span
            data-wire-rule-authority-metric={metric.key}
            data-wire-rule-authority-metric-state={metric.state}
            key={metric.key}
          >
            <small>{metric.label}</small>
            <strong>{metric.value}</strong>
          </span>
        ))}
      </div>

      <ol className="wire-rule-authority-rows">
        {plan.rows.map((row) => (
          <li
            data-wire-rule-authority-row={row.key}
            data-wire-rule-authority-row-state={row.state}
            key={row.key}
          >
            <span>{row.label}</span>
            <strong>{row.stateLabel}</strong>
            <small>{row.value}</small>
          </li>
        ))}
      </ol>
    </section>
  );
}

function stateTone(state: WireRuleAuthorityState): "good" | "info" | "neutral" | "warn" {
  if (state === "server") {
    return "good";
  }

  if (state === "mixed") {
    return "warn";
  }

  if (state === "fallback") {
    return "neutral";
  }

  return "info";
}

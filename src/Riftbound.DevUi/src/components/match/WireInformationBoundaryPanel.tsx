import type { GameEvent } from "../../types/protocol";
import { StatusPill } from "../ui/StatusPill";
import { buildWireInformationBoundaryPlan, type WireInformationBoundaryState } from "./wireInformationBoundaryPlan";
import type { WireTableViewModel } from "./wireTableViewModel";

export function WireInformationBoundaryPanel({
  events,
  table
}: {
  events?: GameEvent[];
  table: WireTableViewModel;
}) {
  const plan = buildWireInformationBoundaryPlan({ events, table });

  return (
    <section
      aria-label="隐藏信息边界契约"
      className="wire-information-boundary"
      data-wire-information-boundary-state={plan.state}
    >
      <header className="wire-information-boundary-header">
        <div>
          <strong>信息边界契约</strong>
          <span>{plan.summary}</span>
        </div>
        <StatusPill tone={stateTone(plan.state)}>{plan.stateLabel}</StatusPill>
      </header>

      <div className="wire-information-boundary-metrics">
        {plan.metrics.map((metric) => (
          <span
            data-wire-information-boundary-metric={metric.key}
            data-wire-information-boundary-metric-state={metric.state}
            key={metric.key}
          >
            <small>{metric.label}</small>
            <strong>{metric.value}</strong>
          </span>
        ))}
      </div>

      <ol className="wire-information-boundary-rows">
        {plan.rows.map((row) => (
          <li
            data-wire-information-boundary-row={row.key}
            data-wire-information-boundary-row-state={row.state}
            key={row.key}
          >
            <span>{row.label}</span>
            <strong>{row.stateLabel}</strong>
            <small>{row.value}</small>
            <em>{row.detail}</em>
          </li>
        ))}
      </ol>
    </section>
  );
}

function stateTone(state: WireInformationBoundaryState): "bad" | "good" | "info" | "neutral" | "warn" {
  if (state === "safe") {
    return "good";
  }

  if (state === "leak") {
    return "bad";
  }

  if (state === "mixed") {
    return "warn";
  }

  return "info";
}

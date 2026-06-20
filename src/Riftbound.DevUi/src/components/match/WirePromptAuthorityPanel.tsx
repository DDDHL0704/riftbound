import type { ActionPromptDto } from "../../types/protocol";
import type { ServerSubmissionGatePlan } from "../../utils/serverSubmissionGatePlan";
import { StatusPill } from "../ui/StatusPill";
import { buildWirePromptAuthorityPlan, type WirePromptAuthorityState } from "./wirePromptAuthorityPlan";

export function WirePromptAuthorityPanel({
  playerId,
  prompt,
  submissionGate
}: {
  playerId: string;
  prompt?: ActionPromptDto;
  submissionGate?: ServerSubmissionGatePlan;
}) {
  const plan = buildWirePromptAuthorityPlan({ playerId, prompt, submissionGate });

  return (
    <section
      aria-label="服务端行动窗口契约"
      className="wire-prompt-authority"
      data-wire-prompt-authority-state={plan.state}
    >
      <header className="wire-prompt-authority-header">
        <div>
          <strong>行动窗口契约</strong>
          <span>{plan.summary}</span>
        </div>
        <StatusPill tone={stateTone(plan.state)}>{plan.stateLabel}</StatusPill>
      </header>

      <div className="wire-prompt-authority-metrics">
        {plan.metrics.map((metric) => (
          <span
            data-wire-prompt-authority-metric={metric.key}
            data-wire-prompt-authority-metric-state={metric.state}
            key={metric.key}
          >
            <small>{metric.label}</small>
            <strong>{metric.value}</strong>
          </span>
        ))}
      </div>

      <ol className="wire-prompt-authority-rows">
        {plan.rows.map((row) => (
          <li
            data-wire-prompt-authority-row={row.key}
            data-wire-prompt-authority-row-state={row.state}
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

function stateTone(state: WirePromptAuthorityState): "good" | "info" | "neutral" | "warn" {
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

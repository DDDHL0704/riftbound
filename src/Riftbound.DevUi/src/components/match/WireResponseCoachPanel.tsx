import type { ActionPromptDto, ConnectionStatus, SnapshotDto } from "../../types/protocol";
import type { CandidateSelectionDraft } from "../../utils/candidateSelectionDraft";
import type { ServerSubmissionGatePlan } from "../../utils/serverSubmissionGatePlan";
import { buildWireResponseCoachPlan } from "../../utils/wireResponseCoachPlan";
import { StatusPill } from "../ui/StatusPill";

export function WireResponseCoachPanel({
  connectionStatus,
  playerId,
  prompt,
  selectionDraft,
  snapshot,
  submissionGate
}: {
  connectionStatus: ConnectionStatus;
  playerId: string;
  prompt?: ActionPromptDto;
  selectionDraft?: CandidateSelectionDraft;
  snapshot?: SnapshotDto;
  submissionGate?: ServerSubmissionGatePlan;
}) {
  const plan = buildWireResponseCoachPlan({
    connectionStatus,
    playerId,
    prompt,
    selectionDraft,
    snapshot,
    submissionGate
  });

  return (
    <section
      aria-label="当前响应导航"
      className="wire-response-coach"
      data-wire-response-coach-state={plan.state}
      data-wire-response-coach-step-role={plan.stepRole}
    >
      <header className="wire-response-coach-header">
        <div>
          <strong>{plan.primaryLabel}</strong>
          <span>{plan.summary}</span>
        </div>
        <StatusPill tone={plan.tone}>{plan.stateLabel}</StatusPill>
      </header>

      <div className="wire-response-coach-next">
        <small>下一步</small>
        <strong>{plan.nextStepLabel}</strong>
        <span>{plan.reason}</span>
      </div>

      <div className="wire-response-coach-metrics">
        {plan.metrics.map((metric) => (
          <span data-wire-response-coach-metric={metric.key} key={metric.key}>
            <small>{metric.label}</small>
            <strong>{metric.value}</strong>
          </span>
        ))}
      </div>

      <ol className="wire-response-coach-rows">
        {plan.rows.map((row) => (
          <li
            data-wire-response-coach-row={row.key}
            data-wire-response-coach-row-state={row.state}
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

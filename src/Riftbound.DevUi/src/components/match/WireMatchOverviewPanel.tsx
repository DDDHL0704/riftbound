import type { ActionPromptDto, ConnectionStatus, GameEvent, SnapshotDto } from "../../types/protocol";
import type { ServerSubmissionGatePlan } from "../../utils/serverSubmissionGatePlan";
import type { TableObjectContext } from "../../utils/tableObjectContext";
import {
  buildWireMatchOverviewPlan,
  type WireMatchOverviewTimelineDetail
} from "../../utils/wireMatchOverviewPlan";
import { StatusPill } from "../ui/StatusPill";

export function WireMatchOverviewPanel({
  connectionStatus,
  events,
  playerId,
  prompt,
  selectedObjectContext,
  selectedObjectId,
  snapshot,
  submissionGate,
  timelineDetail
}: {
  connectionStatus: ConnectionStatus;
  events?: GameEvent[];
  playerId: string;
  prompt?: ActionPromptDto;
  selectedObjectContext?: TableObjectContext;
  selectedObjectId?: string;
  snapshot?: SnapshotDto;
  submissionGate?: ServerSubmissionGatePlan;
  timelineDetail?: WireMatchOverviewTimelineDetail;
}) {
  const plan = buildWireMatchOverviewPlan({
    connectionStatus,
    events,
    playerId,
    prompt,
    selectedObjectContext,
    selectedObjectId,
    snapshot,
    submissionGate,
    timelineDetail
  });

  return (
    <section
      aria-label="当前对局态势总览"
      className="wire-match-overview"
      data-wire-match-overview-state={plan.state}
    >
      <header className="wire-match-overview-header">
        <div>
          <strong>{plan.headline}</strong>
          <span>{plan.nextStepLabel}</span>
        </div>
        <StatusPill tone={plan.tone}>{plan.stateLabel}</StatusPill>
      </header>

      <dl className="wire-match-overview-metrics">
        {plan.metrics.map((metric) => (
          <div data-wire-match-overview-metric={metric.key} key={metric.key}>
            <dt>{metric.label}</dt>
            <dd>{metric.value}</dd>
          </div>
        ))}
      </dl>

      <ol className="wire-match-overview-rows">
        {plan.rows.map((row) => (
          <li
            data-wire-match-overview-row={row.key}
            data-wire-match-overview-row-count={row.count}
            data-wire-match-overview-row-source={row.sourceLabel}
            data-wire-match-overview-row-state={row.state}
            key={row.key}
          >
            <span>{row.label}</span>
            <strong>{row.stateLabel}</strong>
            <small>{row.value}</small>
            <em>{row.summary}</em>
          </li>
        ))}
      </ol>
    </section>
  );
}

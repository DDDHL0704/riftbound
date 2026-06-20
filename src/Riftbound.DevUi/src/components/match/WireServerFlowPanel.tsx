import type { ActionPromptDto, ConnectionStatus, GameEvent, SnapshotDto } from "../../types/protocol";
import type { CandidateSelectionDraft } from "../../utils/candidateSelectionDraft";
import type { ServerSubmissionGatePlan } from "../../utils/serverSubmissionGatePlan";
import { buildWireServerFlowPlan, type WireServerFlowDetail } from "../../utils/wireServerFlowPlan";
import { StatusPill } from "../ui/StatusPill";
import { WireObjectRefChips, type WireObjectIndex } from "./WireObjectRefChips";
import type { WireTimelineDetail } from "./WireTimelineDetailPanel";

export function WireServerFlowPanel({
  connectionStatus,
  events,
  objectIndex,
  onInspectObject,
  onSelectDetail,
  playerId,
  prompt,
  selectionDraft,
  selectedObjectId,
  snapshot,
  submissionGate
}: {
  connectionStatus: ConnectionStatus;
  events?: GameEvent[];
  objectIndex?: WireObjectIndex;
  onInspectObject?: (objectId: string) => void;
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  playerId: string;
  prompt?: ActionPromptDto;
  selectionDraft?: CandidateSelectionDraft;
  selectedObjectId?: string;
  snapshot?: SnapshotDto;
  submissionGate?: ServerSubmissionGatePlan;
}) {
  const plan = buildWireServerFlowPlan({
    connectionStatus,
    events,
    playerId,
    prompt,
    selectionDraft,
    snapshot,
    submissionGate
  });

  return (
    <section
      aria-label="服务端结算与行动总览"
      className="wire-server-flow"
      data-wire-server-flow-detail-id={plan.detail?.id ?? ""}
      data-wire-server-flow-related-count={plan.relatedObjectCount}
      data-wire-server-flow-state={plan.state}
    >
      <header className="wire-server-flow-header">
        <div>
          <strong>{plan.primaryLabel}</strong>
          <span>{plan.summary}</span>
        </div>
        <StatusPill tone={plan.tone}>{plan.stateLabel}</StatusPill>
      </header>

      <div className="wire-server-flow-next">
        <small>下一步</small>
        <strong>{plan.nextStepLabel}</strong>
        <span>{plan.reason}</span>
      </div>

      <div className="wire-server-flow-metrics">
        {plan.metrics.map((metric) => (
          <span data-wire-server-flow-metric={metric.key} key={metric.key}>
            <small>{metric.label}</small>
            <strong>{metric.value}</strong>
          </span>
        ))}
      </div>

      {objectIndex && plan.relatedObjectIds.length > 0 ? (
        <WireObjectRefChips
          className="wire-server-flow-related-refs"
          objects={objectIndex}
          onInspectObject={onInspectObject}
          refs={plan.relatedObjectRefs}
          selectedObjectId={selectedObjectId}
          source="rule"
        />
      ) : null}

      {plan.relatedActionRows.length > 0 ? (
        <ol className="wire-server-flow-action-bridge" aria-label="服务端关联对象候选桥">
          {plan.relatedActionRows.map((row) => {
            const canInspect = Boolean(objectIndex?.[row.objectId] && onInspectObject);
            const content = (
              <>
                <span>{row.serverRoleLabel}</span>
                <strong>{row.actionRoleLabels.join(" / ") || "无候选角色"}</strong>
                <small>{row.enabledCandidateCount} 可 / {row.disabledCandidateCount} 阻</small>
                <em>{row.nextStepLabel}</em>
              </>
            );

            return (
              <li
                data-server-flow-action-object-id={row.objectId}
                data-server-flow-action-state={row.state}
                key={row.key}
              >
                {canInspect ? (
                  <button
                    data-server-flow-action-inspectable="true"
                    onClick={() => onInspectObject?.(row.objectId)}
                    type="button"
                  >
                    {content}
                  </button>
                ) : (
                  <span data-server-flow-action-inspectable="false">{content}</span>
                )}
              </li>
            );
          })}
        </ol>
      ) : null}

      <ol className="wire-server-flow-lanes" aria-label="规则通道总览">
        {plan.lanes.map((lane) => (
          <li data-wire-server-flow-lane={lane.key} data-wire-server-flow-lane-state={lane.state} key={lane.key}>
            <span>{lane.label}</span>
            <strong>{lane.count}</strong>
            <small>{lane.headline}</small>
          </li>
        ))}
      </ol>

      <ol className="wire-server-flow-steps" aria-label="服务端行动顺序总览">
        {plan.steps.map((step) => (
          <li data-wire-server-flow-step={step.key} data-wire-server-flow-step-state={step.state} key={step.key}>
            <span>{step.label}</span>
            <strong>{step.stateLabel}</strong>
            <small>{step.value}</small>
            <em>{step.detail}</em>
          </li>
        ))}
      </ol>

      <button
        className="wire-server-flow-detail-button"
        data-wire-server-flow-detail-button={plan.detail ? "available" : "empty"}
        disabled={!plan.detail}
        onClick={() => {
          if (plan.detail) {
            onSelectDetail?.(timelineDetailFromServerFlow(plan.detail));
          }
        }}
        type="button"
      >
        {plan.detailButtonLabel}
      </button>
    </section>
  );
}

function timelineDetailFromServerFlow(detail: WireServerFlowDetail): WireTimelineDetail {
  return {
    id: detail.id,
    lines: detail.lines,
    refs: detail.refs,
    source: "rule",
    subtitle: detail.subtitle,
    title: detail.title
  };
}

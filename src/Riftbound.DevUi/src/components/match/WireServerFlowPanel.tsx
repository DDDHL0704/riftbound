import { useState } from "react";
import type { ActionPromptDto, ConnectionStatus, GameEvent, SnapshotDto } from "../../types/protocol";
import type { CandidateSelectionDraft } from "../../utils/candidateSelectionDraft";
import type { ServerSubmissionGatePlan } from "../../utils/serverSubmissionGatePlan";
import {
  buildWireServerFlowPlan,
  type WireServerFlowDetail,
  type WireServerFlowLane,
  type WireServerFlowMetric,
  type WireServerFlowPlan,
  type WireServerFlowRelatedActionRow,
  type WireServerFlowStep
} from "../../utils/wireServerFlowPlan";
import { StatusPill } from "../ui/StatusPill";
import { useWireDialogFocus } from "./useWireDialogFocus";
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
  const [flowLayerOpen, setFlowLayerOpen] = useState(false);
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

      <button
        aria-controls="wire-server-flow-layer"
        aria-expanded={flowLayerOpen}
        className="wire-server-flow-open-layer"
        data-wire-server-flow-open-layer={plan.state}
        onClick={() => setFlowLayerOpen(true)}
        type="button"
      >
        打开流程检查层
      </button>

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
                {row.stepSummary && <small data-server-flow-action-step-summary>{row.stepSummary}</small>}
                <em>{row.nextStepLabel}</em>
              </>
            );

            return (
              <li
                data-server-flow-action-object-id={row.objectId}
                data-server-flow-action-state={row.state}
                data-server-flow-action-step-summary={row.stepSummary ? "present" : "empty"}
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
        {plan.steps.map((step) => {
          const content = (
            <>
              <span>{step.label}</span>
              <strong>{step.stateLabel}</strong>
              <small>{step.value}</small>
              <em>{step.detail}</em>
            </>
          );
          return (
            <li
              data-wire-server-flow-step={step.key}
              data-wire-server-flow-step-detail={step.timelineDetail ? "available" : "empty"}
              data-wire-server-flow-step-state={step.state}
              key={step.key}
            >
              {step.timelineDetail && onSelectDetail ? (
                <button
                  aria-label={`查看服务端流程步骤：${step.label}`}
                  className="wire-server-flow-step-button"
                  onClick={() => onSelectDetail(timelineDetailFromServerFlow(step.timelineDetail!))}
                  type="button"
                >
                  {content}
                </button>
              ) : content}
            </li>
          );
        })}
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

      {flowLayerOpen ? (
        <WireServerFlowLayer
          objectIndex={objectIndex}
          onClose={() => setFlowLayerOpen(false)}
          onInspectObject={onInspectObject}
          onSelectDetail={onSelectDetail}
          plan={plan}
          selectedObjectId={selectedObjectId}
        />
      ) : null}
    </section>
  );
}

function WireServerFlowLayer({
  objectIndex,
  onClose,
  onInspectObject,
  onSelectDetail,
  plan,
  selectedObjectId
}: {
  objectIndex?: WireObjectIndex;
  onClose: () => void;
  onInspectObject?: (objectId: string) => void;
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  plan: WireServerFlowPlan;
  selectedObjectId?: string;
}) {
  const { closeButtonRef, dialogRef } = useWireDialogFocus(onClose);

  return (
    <div
      aria-labelledby="wire-server-flow-layer-title"
      aria-modal="true"
      className="wire-server-flow-layer"
      data-wire-server-flow-layer-flow-state={plan.state}
      data-wire-server-flow-layer-lane-count={plan.lanes.length}
      data-wire-server-flow-layer-related-count={plan.relatedObjectCount}
      data-wire-server-flow-layer-state="open"
      data-wire-server-flow-layer-step-count={plan.steps.length}
      id="wire-server-flow-layer"
      role="dialog"
    >
      <button
        aria-label="关闭服务端流程检查层"
        className="wire-server-flow-layer-scrim"
        onClick={onClose}
        type="button"
      />
      <aside className="wire-server-flow-dialog" ref={dialogRef} tabIndex={-1}>
        <header className="wire-server-flow-layer-header">
          <div>
            <span>服务端结算与行动流程</span>
            <h2 id="wire-server-flow-layer-title">服务端流程检查层</h2>
          </div>
          <button
            className="wire-server-flow-layer-close"
            onClick={onClose}
            ref={closeButtonRef}
            type="button"
          >
            关闭检查层
          </button>
        </header>

        <div className="wire-server-flow-layer-body">
          <section data-wire-server-flow-layer-section="summary">
            <strong>{plan.primaryLabel}</strong>
            <span>{plan.summary}</span>
            <small>{plan.reason}</small>
            <em>{plan.nextStepLabel}</em>
          </section>

          <section data-wire-server-flow-layer-section="metrics">
            <strong>流程指标</strong>
            <WireServerFlowLayerMetrics metrics={plan.metrics} />
          </section>

          <section data-wire-server-flow-layer-section="lanes">
            <strong>规则通道</strong>
            <WireServerFlowLayerLanes lanes={plan.lanes} />
          </section>

          <section data-wire-server-flow-layer-section="steps">
            <strong>服务端步骤</strong>
            <WireServerFlowLayerSteps
              onClose={onClose}
              onSelectDetail={onSelectDetail}
              steps={plan.steps}
            />
          </section>

          <section data-wire-server-flow-layer-section="related">
            <strong>关联对象与候选</strong>
            {objectIndex && plan.relatedObjectRefs.length > 0 ? (
              <WireObjectRefChips
                className="wire-server-flow-layer-related-refs"
                objects={objectIndex}
                onInspectObject={onInspectObject}
                refs={plan.relatedObjectRefs}
                selectedObjectId={selectedObjectId}
                source="rule"
              />
            ) : null}
            <WireServerFlowLayerActions
              objectIndex={objectIndex}
              onInspectObject={onInspectObject}
              rows={plan.relatedActionRows}
            />
          </section>
        </div>

        <footer className="wire-server-flow-layer-footer">
          <span data-wire-server-flow-layer-authority="server">
            流程、责任、候选和对象关联来自服务端 prompt / 快照投影
          </span>
        </footer>
      </aside>
    </div>
  );
}

function WireServerFlowLayerMetrics({ metrics }: { metrics: WireServerFlowMetric[] }) {
  return (
    <div className="wire-server-flow-layer-metrics">
      {metrics.map((metric) => (
        <span data-wire-server-flow-layer-metric={metric.key} key={metric.key}>
          <small>{metric.label}</small>
          <strong>{metric.value}</strong>
        </span>
      ))}
    </div>
  );
}

function WireServerFlowLayerLanes({ lanes }: { lanes: WireServerFlowLane[] }) {
  return (
    <ol className="wire-server-flow-layer-lanes">
      {lanes.map((lane) => (
        <li
          data-wire-server-flow-layer-lane={lane.key}
          data-wire-server-flow-layer-lane-state={lane.state}
          key={lane.key}
        >
          <span>{lane.label}</span>
          <strong>{lane.count}</strong>
          <small>{lane.headline}</small>
        </li>
      ))}
    </ol>
  );
}

function WireServerFlowLayerSteps({
  onClose,
  onSelectDetail,
  steps
}: {
  onClose: () => void;
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  steps: WireServerFlowStep[];
}) {
  return (
    <ol className="wire-server-flow-layer-steps">
      {steps.map((step) => {
        const content = (
          <>
            <span>{step.label}</span>
            <strong>{step.stateLabel}</strong>
            <small>{step.value}</small>
            <em>{step.detail}</em>
          </>
        );

        return (
          <li
            data-wire-server-flow-layer-step={step.key}
            data-wire-server-flow-layer-step-detail={step.timelineDetail ? "available" : "empty"}
            data-wire-server-flow-layer-step-state={step.state}
            key={step.key}
          >
            {step.timelineDetail && onSelectDetail ? (
              <button
                aria-label={`查看服务端流程检查步骤：${step.label}`}
                className="wire-server-flow-layer-step-button"
                onClick={() => {
                  onSelectDetail(timelineDetailFromServerFlow(step.timelineDetail!));
                  onClose();
                }}
                type="button"
              >
                {content}
              </button>
            ) : content}
          </li>
        );
      })}
    </ol>
  );
}

function WireServerFlowLayerActions({
  objectIndex,
  onInspectObject,
  rows
}: {
  objectIndex?: WireObjectIndex;
  onInspectObject?: (objectId: string) => void;
  rows: WireServerFlowRelatedActionRow[];
}) {
  if (rows.length === 0) {
    return <span className="wire-server-flow-layer-empty">暂无关联候选对象</span>;
  }

  return (
    <ol className="wire-server-flow-layer-actions">
      {rows.map((row) => {
        const canInspect = Boolean(objectIndex?.[row.objectId] && onInspectObject);
        const content = (
          <>
            <span>{row.serverRoleLabel}</span>
            <strong>{row.actionRoleLabels.join(" / ") || "无候选角色"}</strong>
            <small>{row.enabledCandidateCount} 可 / {row.disabledCandidateCount} 阻</small>
            {row.stepSummary ? <small>{row.stepSummary}</small> : null}
            <em>{row.nextStepLabel}</em>
          </>
        );

        return (
          <li
            data-wire-server-flow-layer-action-inspectable={canInspect ? "true" : "false"}
            data-wire-server-flow-layer-action-object-id={row.objectId}
            data-wire-server-flow-layer-action-state={row.state}
            key={row.key}
          >
            {canInspect ? (
              <button onClick={() => onInspectObject?.(row.objectId)} type="button">
                {content}
              </button>
            ) : (
              <span>{content}</span>
            )}
          </li>
        );
      })}
    </ol>
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

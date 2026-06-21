import type { ActionPromptDto, GameEvent, SnapshotDto } from "../../types/protocol";
import { useState } from "react";
import type { CandidateSelectionDraft } from "../../utils/candidateSelectionDraft";
import type { CommandSubmissionFeedback } from "../../stores/useMatchController";
import {
  buildCommandSubmissionFollowupPlan,
  type CommandSubmitHandler,
  type CommandSubmissionFollowupEventRow,
  type CommandSubmissionUiSource,
  type CommandSubmissionFollowupServerEventKind
} from "../../utils/commandSubmissionFollowupPlan";
import type { ServerSubmissionGatePlan } from "../../utils/serverSubmissionGatePlan";
import {
  buildWireActionMapPlan,
  type WireActionBlockerPlan,
  type WireActionCommandReviewPlan,
  type WireActionContractPlan,
  type WireActionCoverageMetricPlan,
  type WireActionCoveragePlan,
  type WireActionGrammarCandidatePlan,
  type WireActionMapMetric,
  type WireActionMapPlan,
  type WireActionRoleCoveragePlan,
  type WireActionRoutePlan
} from "../../utils/wireActionMapPlan";
import type { WireActionSubmissionGatePlan, WireActionWindowGatePlan } from "../../utils/wireActionGates";
import { StatusPill } from "../ui/StatusPill";
import { WireCommandFollowupPanel } from "./WireCommandFollowupPanel";
import { buildWireActionLayoutProjectionPlan, type WireActionLayoutProjectionPlan } from "./wireActionLayoutProjectionPlan";
import type { WireTableViewModel } from "./wireTableViewModel";
import { useWireDialogFocus } from "./useWireDialogFocus";

type WireActionMapPanelProps = {
  events?: GameEvent[];
  onChooseObject?: (objectId: string) => void;
  onCommand?: CommandSubmitHandler;
  onInspectObject?: (objectId: string) => void;
  onSelectFollowupEvent?: (event: CommandSubmissionFollowupEventRow) => void;
  onSelectServerEventKind?: (eventKind: CommandSubmissionFollowupServerEventKind) => void;
  playerId: string;
  prompt?: ActionPromptDto;
  selectedObjectId?: string;
  selectionDraft?: CandidateSelectionDraft;
  snapshot?: SnapshotDto;
  submissionFeedback?: CommandSubmissionFeedback;
  submissionGate?: ServerSubmissionGatePlan;
  table: WireTableViewModel;
};

export function WireActionMapPanel({
  events,
  onChooseObject,
  onCommand,
  onInspectObject,
  onSelectFollowupEvent,
  onSelectServerEventKind,
  playerId,
  prompt,
  selectedObjectId,
  selectionDraft,
  snapshot,
  submissionFeedback,
  submissionGate,
  table
}: WireActionMapPanelProps) {
  const plan = buildWireActionMapPlan({ playerId, prompt, selectedObjectId, selectionDraft, snapshot, submissionGate });
  const layoutProjection = buildWireActionLayoutProjectionPlan({
    actionMap: plan,
    selectedObjectId,
    table
  });

  return (
    <section className="wire-action-map" aria-label="服务端合法操作地图">
      <header className="wire-action-map-header">
        <div>
          <strong>合法操作地图</strong>
          <span>只投影服务端候选，不在前端推断规则。</span>
        </div>
        <StatusPill tone={plan.canAct ? "good" : "neutral"}>{plan.canAct ? "可操作窗口" : "只读窗口"}</StatusPill>
      </header>

      <div className="wire-action-map-metrics">
        {plan.metrics.map((metric) => <Metric key={metric.key} metric={metric} />)}
      </div>

      <SubmissionGateStrip gate={plan.submissionGate} />
      <WindowGateStrip gate={plan.windowGate} />
      <ActionCoveragePanel coverage={plan.coverage} />
      <ActionLayoutProjectionPanel plan={layoutProjection} />
      {plan.contract && <PromptContractStrip contract={plan.contract} />}
      <CommandReviewPanel onCommand={onCommand} review={plan.commandReview} />
      <CommandSubmissionFeedbackPanel
        events={events}
        feedback={submissionFeedback}
        onInspectObject={onInspectObject}
        onSelectFollowupEvent={onSelectFollowupEvent}
        onSelectServerEventKind={onSelectServerEventKind}
        snapshot={snapshot}
        table={table}
      />
      <CurrentRouteStrip route={plan.route} />

      <div aria-label="服务端可操作对象入口" className="wire-action-entry-strip" role="group" tabIndex={0}>
        {plan.objectEntries.length === 0 && <span className="empty-hint">当前没有服务端标记为可操作的场上对象。</span>}
        {plan.objectEntries.map((entry) => (
          <button
            className="wire-action-object-chip"
            data-action-object-id={entry.objectId}
            data-action-object-state="enabled"
            data-selected={entry.selected ? "true" : "false"}
            key={entry.objectId}
            onClick={() => onInspectObject?.(entry.objectId)}
            type="button"
          >
            <strong>{entry.label}</strong>
            <small>{entry.enabledCandidateCount} 项</small>
          </button>
        ))}
        {plan.objectEntryOverflowCount > 0 && <span className="wire-action-object-chip">等 {plan.objectEntryOverflowCount} 个对象</span>}
        {plan.blockedObjectEntries.map((entry) => (
          <button
            className="wire-action-object-chip"
            data-action-object-id={entry.objectId}
            data-action-object-state="blocked"
            data-selected={entry.selected ? "true" : "false"}
            key={`blocked:${entry.objectId}`}
            onClick={() => onInspectObject?.(entry.objectId)}
            type="button"
          >
            <strong>{entry.label}</strong>
            <small>{entry.disabledCandidateCount} 阻断</small>
          </button>
        ))}
        {plan.blockedObjectEntryOverflowCount > 0 && <span className="wire-action-object-chip" data-action-object-state="blocked">等 {plan.blockedObjectEntryOverflowCount} 个阻断对象</span>}
      </div>

      {plan.focus && <FocusedActionBridge onChooseObject={onChooseObject ?? onInspectObject} plan={plan} />}

      <div className="wire-action-group-list">
        {plan.groups.length === 0 && <span className="empty-hint">等待服务端行动窗口。</span>}
        {plan.groups.map((group) => (
          <article
            className={group.enabled ? "wire-action-group is-enabled" : "wire-action-group"}
            data-action-group-category={group.category}
            data-action-group-intent={group.intent}
            data-action-group-priority={group.priority}
            data-action-group-ui-hint={group.uiHint}
            key={group.key}
          >
            <div className="wire-action-group-heading">
              <strong>{group.label}</strong>
              <span>{group.enabledCount} / {group.totalCount}</span>
            </div>
            <div className="wire-action-role-grid">
              {group.roleCounts.map((role) => (
                <span key={role.role}>
                  {role.label} {role.count}
                </span>
              ))}
            </div>
            <small>{group.category} / {group.intent} / {group.uiHint}</small>
            <small>{group.reason}</small>
          </article>
        ))}
      </div>

      <CandidateInteractionPlanList onChooseObject={onChooseObject ?? onInspectObject} plan={plan} />

      <div className="wire-action-grammar" role="group" aria-label="服务端候选交互语法">
        <strong>交互语法</strong>
        {plan.grammarCandidateTotalCount === 0 && <span className="empty-hint">暂无候选步骤。</span>}
        {plan.grammarCandidates.map((candidate) => (
          <article className="wire-action-sequence" key={candidate.key}>
            <div className="wire-action-sequence-title">
              <span>{candidate.label}</span>
              <small>{candidate.stepCount} 步 / {candidate.commandFieldCount} 字段</small>
            </div>
            <ol>
              {candidate.steps.map((step) => (
                <li className={`wire-action-step wire-action-step-${step.role} ${step.required ? "is-required" : ""}`} key={step.key}>
                  <span>{step.label}</span>
                  <strong>{step.count}</strong>
                  <small>{step.required ? "必需；" : ""}{step.sampleLabel}</small>
                </li>
              ))}
            </ol>
            <CommandFieldList candidate={candidate} />
          </article>
        ))}
      </div>
    </section>
  );
}

function ActionLayoutProjectionPanel({ plan }: { plan: WireActionLayoutProjectionPlan }) {
  return (
    <section
      aria-label="合法操作桌面区域投影"
      className="wire-action-layout-projection"
      data-action-layout-projection-located-count={plan.locatedCount}
      data-action-layout-projection-ready-count={plan.readyCount}
      data-action-layout-projection-state={plan.state}
      data-action-layout-projection-total-count={plan.totalCount}
    >
      <div className="wire-action-layout-projection-heading">
        <strong>桌面区域投影</strong>
        <span>{plan.stateLabel}</span>
      </div>
      <small>{plan.summary}</small>
      {plan.rows.length === 0 ? (
        <span className="empty-hint">等待服务端候选对象。</span>
      ) : (
        <ol>
          {plan.rows.map((row) => (
            <li
              data-action-layout-projection-capacity-row={row.capacityRowKey ?? ""}
              data-action-layout-projection-kind={row.layoutKind}
              data-action-layout-projection-object={row.objectId}
              data-action-layout-projection-role={row.roleLabel}
              data-action-layout-projection-row={row.key}
              data-action-layout-projection-selected={row.selected ? "true" : "false"}
              data-action-layout-projection-source={row.source}
              data-action-layout-projection-state={row.actionState}
              data-action-layout-projection-zone={row.zoneKey ?? ""}
              key={row.key}
            >
              <span>{row.roleLabel} / {row.zoneLabel}</span>
              <strong>{row.objectLabel}</strong>
              <small>{row.sourceLabel} / {row.actionStateLabel} / {row.actionLabel}</small>
            </li>
          ))}
        </ol>
      )}
      {plan.overflowCount > 0 && <em>另有 {plan.overflowCount} 个候选对象未展开。</em>}
    </section>
  );
}

function CommandSubmissionFeedbackPanel({
  events,
  feedback,
  onInspectObject,
  onSelectFollowupEvent,
  onSelectServerEventKind,
  snapshot,
  table
}: {
  events?: GameEvent[];
  feedback?: CommandSubmissionFeedback;
  onInspectObject?: (objectId: string) => void;
  onSelectFollowupEvent?: (event: CommandSubmissionFollowupEventRow) => void;
  onSelectServerEventKind?: (eventKind: CommandSubmissionFollowupServerEventKind) => void;
  snapshot?: SnapshotDto;
  table: WireTableViewModel;
}) {
  const [layerOpen, setLayerOpen] = useState(false);
  const followup = buildCommandSubmissionFollowupPlan({ events, feedback, snapshot });

  if (!feedback) {
    return (
      <section
        aria-label="服务端提交反馈"
        className="wire-command-submission-feedback"
        data-command-submission-state="empty"
      >
        <div className="wire-command-submission-heading">
          <strong>提交反馈</strong>
          <span>尚未提交</span>
        </div>
        <span>等待右侧路线或候选操作提交给服务端。</span>
        <button
          className="wire-command-submission-open-layer"
          data-command-submission-open-layer-state="empty"
          disabled
          type="button"
        >
          打开回执检查层
        </button>
        <WireCommandFollowupPanel
          ariaLabel="提交反馈服务端后续事件"
          onInspectObject={onInspectObject}
          onSelectFollowupEvent={onSelectFollowupEvent}
          onSelectServerEventKind={onSelectServerEventKind}
          plan={followup}
          table={table}
        />
      </section>
    );
  }

  return (
    <section
      aria-label="服务端提交反馈"
      className="wire-command-submission-feedback"
      data-command-submission-state={feedback.state}
    >
      <div className="wire-command-submission-heading">
        <strong>提交反馈</strong>
        <span>{feedback.stateLabel}</span>
      </div>
      <small>{feedback.message}</small>
      <div className="wire-command-submission-metrics">
        <span data-command-submission-metric="command">
          <b>命令</b>
          <strong>{feedback.cmdType}</strong>
        </span>
        <span data-command-submission-metric="receipt">
          <b>回执</b>
          <strong>{feedback.receiptState ?? feedback.state}</strong>
        </span>
        {feedback.followup ? (
          <span data-command-submission-metric="followup">
            <b>后续</b>
            <strong>{feedback.followup.state}</strong>
          </span>
        ) : null}
        <span data-command-submission-metric="prompt">
          <b>提示</b>
          <strong>{feedback.promptId ?? "无"}</strong>
        </span>
        <span data-command-submission-metric="snapshot">
          <b>快照</b>
          <strong>{feedback.snapshotTick ?? "无"}</strong>
        </span>
        <span data-command-submission-metric="server">
          <b>服务端</b>
          <strong>{feedback.serverTick ?? "无"}</strong>
        </span>
        {feedback.errorCode ? (
          <span data-command-submission-metric="error">
            <b>错误</b>
            <strong>{feedback.errorCode}</strong>
          </span>
        ) : null}
        <span data-command-submission-metric="intent">
          <b>追踪</b>
          <strong>{shortIntentId(feedback.clientIntentId)}</strong>
        </span>
      </div>
      <button
        aria-controls="wire-command-submission-layer"
        aria-expanded={layerOpen}
        className="wire-command-submission-open-layer"
        data-command-submission-open-layer-state={feedback.state}
        onClick={() => setLayerOpen(true)}
        type="button"
      >
        打开回执检查层
      </button>
      <WireCommandFollowupPanel
        ariaLabel="提交反馈服务端后续事件"
        onInspectObject={onInspectObject}
        onSelectFollowupEvent={onSelectFollowupEvent}
        onSelectServerEventKind={onSelectServerEventKind}
        plan={followup}
        table={table}
      />
      {layerOpen && (
        <CommandSubmissionFeedbackLayer
          feedback={feedback}
          followup={followup}
          onClose={() => setLayerOpen(false)}
          onInspectObject={onInspectObject}
          onSelectFollowupEvent={onSelectFollowupEvent}
          onSelectServerEventKind={onSelectServerEventKind}
          table={table}
        />
      )}
    </section>
  );
}

function shortIntentId(clientIntentId: string): string {
  return clientIntentId.length > 8 ? clientIntentId.slice(-8) : clientIntentId;
}

function CommandSubmissionFeedbackLayer({
  feedback,
  followup,
  onClose,
  onInspectObject,
  onSelectFollowupEvent,
  onSelectServerEventKind,
  table
}: {
  feedback: CommandSubmissionFeedback;
  followup: ReturnType<typeof buildCommandSubmissionFollowupPlan>;
  onClose: () => void;
  onInspectObject?: (objectId: string) => void;
  onSelectFollowupEvent?: (event: CommandSubmissionFollowupEventRow) => void;
  onSelectServerEventKind?: (eventKind: CommandSubmissionFollowupServerEventKind) => void;
  table: WireTableViewModel;
}) {
  const { closeButtonRef, dialogRef } = useWireDialogFocus(onClose);

  return (
    <div
      aria-labelledby="wire-command-submission-layer-title"
      aria-modal="true"
      className="wire-command-submission-layer"
      data-command-submission-layer-cmd-type={feedback.cmdType}
      data-command-submission-layer-event-count={followup.events.length}
      data-command-submission-layer-followup-state={followup.state}
      data-command-submission-layer-hidden-count={followup.hiddenEventCount}
      data-command-submission-layer-receipt-state={feedback.receiptState ?? feedback.state}
      data-command-submission-layer-server-state={followup.serverFollowupState}
      data-command-submission-layer-source-detail={followup.uiSource?.detailId ?? ""}
      data-command-submission-layer-source-object={followup.uiSource?.objectId ?? ""}
      data-command-submission-layer-source-surface={followup.uiSource?.surface ?? ""}
      data-command-submission-layer-state="open"
      id="wire-command-submission-layer"
      role="dialog"
    >
      <button aria-label="关闭回执检查层" className="wire-command-submission-layer-scrim" onClick={onClose} type="button" />
      <aside className="wire-command-submission-dialog" ref={dialogRef} tabIndex={-1}>
        <header className="wire-command-submission-layer-header">
          <div>
            <span>回执检查层</span>
            <h2 id="wire-command-submission-layer-title">{feedback.cmdType}</h2>
          </div>
          <button className="wire-command-submission-layer-close" onClick={onClose} ref={closeButtonRef} type="button">
            关闭检查层
          </button>
        </header>
        <div className="wire-command-submission-layer-body">
          <section data-command-submission-layer-section="receipt">
            <strong>服务端回执</strong>
            <span>{feedback.stateLabel}</span>
            <small>{feedback.message}</small>
          </section>
          <section data-command-submission-layer-section="identity">
            <strong>提交身份</strong>
            <div className="wire-command-submission-layer-metrics">
              <span data-command-submission-layer-metric="command">
                <b>命令</b>
                <small>{feedback.cmdType}</small>
              </span>
              <span data-command-submission-layer-metric="receipt">
                <b>回执</b>
                <small>{feedback.receiptState ?? feedback.state}</small>
              </span>
              <span data-command-submission-layer-metric="intent">
                <b>追踪</b>
                <small>{shortIntentId(feedback.clientIntentId)}</small>
              </span>
            </div>
          </section>
          <section data-command-submission-layer-section="authority">
            <strong>服务端权威</strong>
            <div className="wire-command-submission-layer-metrics">
              <span data-command-submission-layer-metric="server">
                <b>服务端 tick</b>
                <small>{feedback.serverTick ?? "无"}</small>
              </span>
              <span data-command-submission-layer-metric="snapshot">
                <b>命令快照</b>
                <small>{feedback.snapshotTick ?? "无"}</small>
              </span>
              <span data-command-submission-layer-metric="prompt">
                <b>提示</b>
                <small>{feedback.promptId ?? "无"}</small>
              </span>
            </div>
          </section>
          {feedback.errorCode ? (
            <section data-command-submission-layer-section="error">
              <strong>错误</strong>
              <span>{feedback.errorCode}</span>
              <small>服务端拒绝或本地提交失败时显示。</small>
            </section>
          ) : null}
          <WireCommandFollowupPanel
            ariaLabel="回执检查层后续事件"
            className="wire-command-submission-layer-followup"
            onInspectObject={onInspectObject}
            onSelectFollowupEvent={onSelectFollowupEvent}
            onSelectServerEventKind={onSelectServerEventKind}
            plan={followup}
            table={table}
          />
        </div>
        <footer className="wire-command-submission-layer-footer">
          <span data-command-submission-layer-authority="server">后续事件、快照和提示均以服务端广播为准</span>
          <span data-command-submission-layer-hidden-count={followup.hiddenEventCount}>隐藏事件 {followup.hiddenEventCount}</span>
        </footer>
      </aside>
    </div>
  );
}

function CommandReviewPanel({ onCommand, review }: { onCommand?: CommandSubmitHandler; review: WireActionCommandReviewPlan }) {
  const [layerOpen, setLayerOpen] = useState(false);
  const canSubmit = review.canSubmit && Boolean(review.command) && Boolean(onCommand);

  return (
    <>
      <section
        aria-label="服务端候选提交审阅"
        className="wire-command-review"
        data-command-review-state={review.state}
      >
        <div className="wire-command-review-heading">
          <strong>提交审阅</strong>
          <span>{review.stateLabel}</span>
        </div>
        <small>{review.summary}</small>
        <div className="wire-command-review-metrics">
          {review.metrics.map((metric) => (
            <span data-command-review-metric={metric.key} key={metric.key}>
              <b>{metric.label}</b>
              <strong>{metric.value}</strong>
            </span>
          ))}
        </div>
        <div className="wire-command-review-next">下一步：{review.nextStepLabel}</div>
        {review.commandPreview.length === 0 ? (
          <span className="empty-hint">当前没有提交草稿。</span>
        ) : (
          <ol className="wire-command-review-fields" aria-label={`${review.candidateLabel} 命令字段审阅`}>
            {review.commandPreview.map((field) => (
              <li
                data-command-review-field={field.field}
                data-command-review-field-state={field.state}
                key={field.key}
              >
                <span>{field.label}</span>
                <strong>{field.stateLabel}</strong>
                <small>{field.required ? "必需" : "可选"} / {field.sourceLabel}</small>
              </li>
            ))}
          </ol>
        )}
        <div className="wire-command-review-controls">
          <button
            aria-controls="wire-command-review-layer"
            aria-expanded={layerOpen}
            className="wire-command-review-open-layer"
            data-command-review-open-layer-state={review.state}
            onClick={() => setLayerOpen(true)}
            type="button"
          >
            打开提交检查层
          </button>
          <button
            className="wire-command-review-submit"
            data-command-review-submit-state={canSubmit ? "ready" : "blocked"}
            disabled={!canSubmit}
            onClick={() => {
              if (!review.command || !onCommand) {
                return;
              }

              onCommand(review.command, commandReviewUiSource(review));
            }}
            title={review.submitReason}
            type="button"
          >
            {review.submitLabel}
          </button>
        </div>
      </section>
      {layerOpen && (
        <CommandReviewLayer
          canSubmit={canSubmit}
          onClose={() => setLayerOpen(false)}
          onCommand={onCommand}
          review={review}
        />
      )}
    </>
  );
}

function CommandReviewLayer({
  canSubmit,
  onClose,
  onCommand,
  review
}: {
  canSubmit: boolean;
  onClose: () => void;
  onCommand?: CommandSubmitHandler;
  review: WireActionCommandReviewPlan;
}) {
  const { closeButtonRef, dialogRef } = useWireDialogFocus(onClose);

  const submitFromLayer = () => {
    if (!canSubmit || !review.command || !onCommand) {
      return;
    }

    onCommand(review.command, commandReviewUiSource(review));
    onClose();
  };

  return (
    <div
      aria-labelledby="wire-command-review-layer-title"
      aria-modal="true"
      className="wire-command-review-layer"
      data-command-review-layer-can-submit={canSubmit ? "true" : "false"}
      data-command-review-layer-command-type={review.commandType}
      data-command-review-layer-review-state={review.state}
      data-command-review-layer-state="open"
      id="wire-command-review-layer"
      role="dialog"
    >
      <button aria-label="关闭提交检查层" className="wire-command-review-layer-scrim" onClick={onClose} type="button" />
      <aside className="wire-command-review-dialog" ref={dialogRef} tabIndex={-1}>
        <header className="wire-command-review-layer-header">
          <div>
            <span>提交检查层</span>
            <h2 id="wire-command-review-layer-title">{review.candidateLabel}</h2>
          </div>
          <button className="wire-command-review-layer-close" onClick={onClose} ref={closeButtonRef} type="button">
            关闭检查层
          </button>
        </header>
        <div className="wire-command-review-layer-body" id="wire-command-review-layer-body">
          <section data-command-review-layer-section="state">
            <strong>状态</strong>
            <span>{review.stateLabel}</span>
            <small>{review.summary}</small>
          </section>
          <section data-command-review-layer-section="next-step">
            <strong>下一步</strong>
            <span>{review.nextStepLabel}</span>
            <small>{review.submitReason}</small>
          </section>
          <section data-command-review-layer-section="metrics">
            <strong>路线指标</strong>
            <div className="wire-command-review-layer-metrics">
              {review.metrics.map((metric) => (
                <span data-command-review-layer-metric={metric.key} key={metric.key}>
                  <b>{metric.label}</b>
                  <small>{metric.value}</small>
                </span>
              ))}
            </div>
          </section>
          <section data-command-review-layer-section="fields">
            <strong>服务端字段覆盖</strong>
            {review.commandPreview.length === 0 ? (
              <span className="empty-hint">当前没有命令字段草稿。</span>
            ) : (
              <ol className="wire-command-review-layer-fields">
                {review.commandPreview.map((field) => (
                  <li
                    data-command-review-layer-field={field.field}
                    data-command-review-layer-field-state={field.state}
                    key={field.key}
                  >
                    <span>{field.label}</span>
                    <strong>{field.stateLabel}</strong>
                    <small>{field.required ? "必需" : "可选"} / {field.sourceLabel}</small>
                  </li>
                ))}
              </ol>
            )}
          </section>
          <section data-command-review-layer-section="checks">
            <strong>提交审计</strong>
            {review.checkRows.length === 0 ? (
              <span className="empty-hint">等待服务端候选路线。</span>
            ) : (
              <ol className="wire-command-review-layer-checks">
                {review.checkRows.map((check) => (
                  <li
                    data-command-review-layer-check={check.key}
                    data-command-review-layer-check-state={check.state}
                    key={check.key}
                  >
                    <span>{check.label}</span>
                    <strong>{check.stateLabel}</strong>
                    <small>{check.reason}</small>
                  </li>
                ))}
              </ol>
            )}
          </section>
        </div>
        <footer className="wire-command-review-layer-footer">
          <span data-command-review-layer-authority="server">最终仍由服务端规则校验</span>
          <button
            className="wire-command-review-layer-submit"
            data-command-review-layer-submit-state={canSubmit ? "ready" : "blocked"}
            disabled={!canSubmit}
            onClick={submitFromLayer}
            type="button"
          >
            提交检查层路线
          </button>
        </footer>
      </aside>
    </div>
  );
}

function commandReviewUiSource(review: WireActionCommandReviewPlan): Partial<CommandSubmissionUiSource> {
  return {
    candidateLabel: review.candidateLabel,
    commandSource: review.commandSource,
    commandSourceDetail: review.commandSourceDetail,
    commandSourceLabel: review.commandSourceLabel,
    label: review.candidateLabel
  };
}

function ActionCoveragePanel({ coverage }: { coverage: WireActionCoveragePlan }) {
  return (
    <section
      aria-label="服务端候选覆盖审计"
      className="wire-action-coverage"
      data-action-coverage-state={coverage.state}
    >
      <div className="wire-action-coverage-heading">
        <strong>候选覆盖审计</strong>
        <span>{coverage.stateLabel}</span>
      </div>
      <small className="wire-action-coverage-summary">{coverage.summary}</small>
      <div className="wire-action-coverage-metrics">
        {coverage.metrics.map((metric) => <CoverageMetric key={metric.key} metric={metric} />)}
      </div>
      <div className="wire-action-coverage-roles" aria-label="选择角色覆盖">
        {coverage.roles.map((role) => <RoleCoverage key={role.key} role={role} />)}
      </div>
      <div className="wire-action-coverage-command" aria-label="命令模板覆盖">
        {coverage.commandRows.map((metric) => <CoverageMetric key={metric.key} metric={metric} />)}
      </div>
      <BlockerList blockers={coverage.blockers} />
      <small className="wire-action-coverage-boundary">{coverage.hiddenBoundaryLabel}</small>
    </section>
  );
}

function CoverageMetric({ metric }: { metric: WireActionCoverageMetricPlan }) {
  return (
    <span data-action-coverage-metric={metric.key} data-action-coverage-state={metric.state}>
      <b>{metric.label}</b>
      <strong>{metric.value}</strong>
    </span>
  );
}

function RoleCoverage({ role }: { role: WireActionRoleCoveragePlan }) {
  return (
    <article data-action-role-coverage={role.role} data-action-role-coverage-state={role.state}>
      <div>
        <strong>{role.label}</strong>
        <span>{role.requiredCandidateCount > 0 ? "必选参与" : role.candidateCount > 0 ? "可选参与" : "未参与"}</span>
      </div>
      <small>{role.summary}</small>
      <small>{role.sampleLabel}</small>
      {(role.emptyRequiredCount > 0 || role.hiddenChoiceCount > 0 || role.unknownObjectCount > 0) && (
        <small>
          空必选 {role.emptyRequiredCount}
          {" / 隐藏引用 "}{role.hiddenChoiceCount}
          {" / 未映射对象 "}{role.unknownObjectCount}
        </small>
      )}
    </article>
  );
}

function BlockerList({ blockers }: { blockers: WireActionBlockerPlan[] }) {
  return (
    <div className="wire-action-coverage-blockers" aria-label="服务端阻断原因">
      <strong>阻断原因</strong>
      {blockers.length === 0 && <span className="empty-hint">当前没有服务端阻断候选。</span>}
      {blockers.map((blocker) => (
        <span data-action-coverage-blocker={blocker.key} key={blocker.key}>
          <b>{blocker.count} 项</b>
          <small>{blocker.reason}</small>
          <small>{blocker.actions.join(" / ")}</small>
        </span>
      ))}
    </div>
  );
}

function SubmissionGateStrip({ gate }: { gate: WireActionSubmissionGatePlan }) {
  return (
    <div
      className="wire-action-route-strip"
      data-action-submission-gate-state={gate.state}
      role="group"
      aria-label="提交门禁"
    >
      <div className="wire-action-route-heading">
        <strong>提交门禁</strong>
        <span>{gate.stateLabel}</span>
      </div>
      <small>{gate.reason}</small>
    </div>
  );
}

function WindowGateStrip({ gate }: { gate: WireActionWindowGatePlan }) {
  return (
    <div
      className="wire-action-route-strip"
      data-action-window-gate-state={gate.state}
      role="group"
      aria-label="行动窗口门禁"
    >
      <div className="wire-action-route-heading">
        <strong>行动窗口</strong>
        <span>{gate.stateLabel}</span>
      </div>
      <small>{gate.reason}</small>
    </div>
  );
}

function CurrentRouteStrip({ route }: { route?: WireActionRoutePlan }) {
  const [inspectorOpen, setInspectorOpen] = useState(false);

  if (!route) {
    return (
      <div className="wire-action-route-strip" data-action-route-state="empty" role="group" aria-label="当前候选路径">
        <strong>当前路径</strong>
        <span>点击服务端候选对象后显示选择路线。</span>
      </div>
    );
  }

  return (
    <div className="wire-action-route-strip" data-action-route-state={route.state} role="group" aria-label="当前候选路径">
      <div className="wire-action-route-heading">
        <strong>{route.candidateLabel}</strong>
        <span>{route.stateLabel}</span>
      </div>
      <small>
        {route.commandType ?? "未公开命令"} / 已选步骤 {route.selectedStepCount}
        {" / 缺少选择 "}{route.missingRequiredSelectionCount}
        {" / 缺少字段 "}{route.missingRequiredFieldCount}
        {" / 服务端字段 "}{route.serverInjectedFieldCount}
        {" / 审计 "}{route.checkSummary}
        {" / "}{route.nextStepLabel}
      </small>
      <ol>
        {route.steps.map((step) => (
          <li data-route-step-role={step.role} data-route-step-state={step.state} key={step.key}>
            <span>{step.label}</span>
            <strong>{step.stateLabel}</strong>
            <small>{step.required ? "必需" : "可选"} / 候选 {step.totalCount} / 已选 {step.selectedCount}</small>
          </li>
        ))}
      </ol>
      {route.fields.length > 0 && (
        <div className="wire-action-route-fields" aria-label={`${route.candidateLabel} 命令字段覆盖`}>
          {route.fields.map((field) => (
            <span
              data-route-field={field.field}
              data-route-field-state={field.state}
              data-route-field-role={field.role ?? "server"}
              key={field.key}
            >
              <b>{field.label}</b>
              <small>{field.required ? "必需" : "可选"} / {field.sourceLabel} / {field.stateLabel}</small>
            </span>
          ))}
        </div>
      )}
      <ol className="wire-action-route-checks" aria-label={`${route.candidateLabel} 提交审计`}>
        {route.checkRows.map((check) => (
          <li
            data-route-check={check.key}
            data-route-check-state={check.state}
            key={check.key}
          >
            <span>{check.label}</span>
            <strong>{check.stateLabel}</strong>
            <small>{check.reason}</small>
          </li>
        ))}
      </ol>
      <button
        aria-expanded={inspectorOpen}
        className="wire-action-route-inspector-toggle"
        data-action-route-inspector-toggle="true"
        onClick={() => setInspectorOpen((open) => !open)}
        type="button"
      >
        {inspectorOpen ? "收起路线检查" : "展开路线检查"}
      </button>
      <aside
        aria-label={`${route.candidateLabel} 路线检查器`}
        className="wire-action-route-inspector"
        data-action-route-inspector-state={inspectorOpen ? "open" : "closed"}
        hidden={!inspectorOpen}
        role="dialog"
      >
        <header>
          <strong>路线检查</strong>
          <span>{route.summary}</span>
        </header>
        <section>
          <strong>提交审计</strong>
          <ol>
            {route.checkRows.map((check) => (
              <li data-route-inspector-check={check.key} data-route-inspector-check-state={check.state} key={check.key}>
                <span>{check.label}</span>
                <small>{check.stateLabel} / {check.reason}</small>
              </li>
            ))}
          </ol>
        </section>
        <section>
          <strong>步骤覆盖</strong>
          <ol>
            {route.steps.map((step) => (
              <li data-route-inspector-step-role={step.role} data-route-inspector-step-state={step.state} key={step.key}>
                <span>{step.label}</span>
                <small>{step.required ? "必需" : "可选"} / {step.stateLabel} / 候选 {step.totalCount} / 已选 {step.selectedCount}</small>
              </li>
            ))}
          </ol>
        </section>
        <section>
          <strong>字段覆盖</strong>
          <ol>
            {route.fields.map((field) => (
              <li data-route-inspector-field={field.field} data-route-inspector-field-state={field.state} key={field.key}>
                <span>{field.label}</span>
                <small>{field.required ? "必需" : "可选"} / {field.sourceLabel} / {field.stateLabel}</small>
              </li>
            ))}
          </ol>
        </section>
        <footer>
          <span>服务端候选 {route.enabled ? "开放" : "阻断"}</span>
          <span>提交审计 {route.checkSummary}</span>
          <span>缺少选择 {route.missingRequiredSelectionCount}</span>
          <span>缺少字段 {route.missingRequiredFieldCount}</span>
          <span>服务端字段 {route.serverInjectedFieldCount}</span>
        </footer>
      </aside>
    </div>
  );
}

function FocusedActionBridge({ onChooseObject, plan }: { onChooseObject?: (objectId: string) => void; plan: WireActionMapPlan }) {
  const focus = plan.focus;
  if (!focus) {
    return null;
  }

  return (
    <section
      aria-label="焦点对象合法操作联动"
      className="wire-action-focus-bridge"
      data-action-focus-state={focus.enabledCandidateCount > 0 ? "enabled" : focus.candidateCount > 0 ? "blocked" : "empty"}
    >
      <div className="wire-action-focus-bridge-heading">
        <strong>{focus.label}</strong>
        <span>{focus.stateLabel}</span>
      </div>
      <div className="wire-action-focus-bridge-metrics">
        <small>对象 {focus.objectId}</small>
        <small>{focus.roleLabels.length > 0 ? `角色 ${focus.roleLabels.join(" / ")}` : "无候选角色"}</small>
        <small>{focus.enabledCandidateCount} 可提交 / {focus.disabledCandidateCount} 阻断</small>
      </div>
      {focus.relatedCandidates.length > 0 ? (
        <ol className="wire-action-focus-candidate-list">
          {focus.relatedCandidates.map((candidate) => (
            <li
              className={candidate.enabled ? "is-enabled" : "is-disabled"}
              data-action-focus-candidate-category={candidate.category}
              data-action-focus-candidate-intent={candidate.intent}
              data-action-focus-candidate-priority={candidate.priority}
              data-action-focus-candidate-ui-hint={candidate.uiHint}
              key={candidate.key}
            >
              <span>{candidate.label}</span>
              <strong>{candidate.nextStepLabel}</strong>
              <small>{candidate.roleLabels.join(" / ")} / {candidate.commandType ?? "未公开命令"} / {candidate.stateLabel} / {candidate.intent}</small>
              {!candidate.enabled && <small>{candidate.reason}</small>}
              {candidate.nextObjectRefs.length > 0 && (
                <div className="wire-action-focus-choice-list" role="group" aria-label={`${candidate.label} 下一步对象`}>
                  {candidate.nextObjectRefs.map((ref) => (
                    <button
                      data-action-focus-choice-object-id={ref.objectId}
                      key={ref.key}
                      onClick={() => onChooseObject?.(ref.objectId)}
                      type="button"
                    >
                      <span>{ref.roleLabel}</span>
                      <strong>{ref.label}</strong>
                    </button>
                  ))}
                </div>
              )}
            </li>
          ))}
        </ol>
      ) : (
        <span className="empty-hint">焦点对象当前没有服务端候选。</span>
      )}
    </section>
  );
}

function CandidateInteractionPlanList({ onChooseObject, plan }: { onChooseObject?: (objectId: string) => void; plan: WireActionMapPlan }) {
  return (
    <div className="wire-action-candidate-plan" role="group" aria-label="服务端候选步骤计划">
      <div className="wire-action-candidate-plan-heading">
        <strong>候选步骤</strong>
        <span>{plan.candidatePlanTotalCount} 项</span>
      </div>
      {plan.candidatePlans.length === 0 && <span className="empty-hint">暂无服务端候选。</span>}
      {plan.candidatePlans.map((candidatePlan) => (
        <article
          className={candidatePlan.enabled ? "wire-action-candidate-plan-card is-enabled" : "wire-action-candidate-plan-card"}
          data-candidate-plan-action={candidatePlan.action}
          data-candidate-plan-draft-active={candidatePlan.draftActive ? "true" : "false"}
          data-candidate-plan-enabled={candidatePlan.enabled ? "true" : "false"}
          key={candidatePlan.key}
        >
          <div className="wire-action-candidate-plan-title">
            <span>{candidatePlan.candidateLabel}</span>
            <small>{candidatePlan.summary}</small>
          </div>
          <div className="wire-action-candidate-plan-next" data-candidate-plan-next-step={candidatePlan.nextRequiredStep?.role ?? "none"}>
            下一步：{candidatePlan.nextRequiredStep?.label ?? "等待服务端候选"}
          </div>
          <ol>
            {candidatePlan.stepRows.slice(0, 4).map((step) => (
              <li
                className={step.required ? "is-required" : ""}
                data-step-progress={step.selectionState}
                data-step-role={step.role}
                data-step-state={step.state}
                key={step.key}
              >
                <span>{step.label}</span>
                <strong>{step.count}</strong>
                <small>{step.stateLabel} / {step.sampleLabels.length > 0 ? step.sampleLabels.join(" / ") : "由服务端候选决定"}</small>
                <small data-step-progress-label={step.selectionState}>
                  {step.progressLabel}{step.selectedLabels.length > 0 ? ` / ${step.selectedLabels.join(" / ")}` : ""}
                </small>
                {step.objectRefs.length > 0 && (
                  <div className="wire-action-candidate-step-ref-list" role="group" aria-label={`${candidatePlan.candidateLabel} ${step.label}对象`}>
                    {step.objectRefs.map((ref) => (
                      <button
                        data-action-candidate-step-object-id={ref.objectId}
                        data-action-candidate-step-role={step.role}
                        key={ref.key}
                        onClick={() => onChooseObject?.(ref.objectId)}
                        type="button"
                      >
                        <span>{ref.roleLabel}</span>
                        <strong>{ref.label}</strong>
                      </button>
                    ))}
                  </div>
                )}
              </li>
            ))}
          </ol>
          <small>命令字段 {candidatePlan.commandFieldCount}{candidatePlan.commandType ? ` / ${candidatePlan.commandType}` : ""}</small>
        </article>
      ))}
    </div>
  );
}

function PromptContractStrip({ contract }: { contract: WireActionContractPlan }) {
  return (
    <div className="wire-action-contract-strip" aria-label="当前提示服务端契约">
      <div>
        <strong>提示契约</strong>
        <span>{contract.promptKind} / {contract.candidateAction}</span>
      </div>
      <ContractMetric label="提交字段" value={contract.requiredPayloadCount} />
      <ContractMetric label="合法选项" value={contract.legalChoicesCount} />
      <ContractMetric label="公开数据" value={contract.visibleMetadataCount} />
      <span>
        <b>隐藏数据</b>
        <small>{contract.hiddenMetadataCount} 项由服务端保留</small>
      </span>
    </div>
  );
}

function ContractMetric({ label, value }: { label: string; value: number }) {
  return (
    <span>
      <b>{label}</b>
      <small>{value} 项</small>
    </span>
  );
}

function CommandFieldList({ candidate }: { candidate: WireActionGrammarCandidatePlan }) {
  if (candidate.commandFields.length === 0) {
    return <span className="wire-action-command-empty">命令字段：服务端未公开模板</span>;
  }

  return (
    <div className="wire-action-command" aria-label={`${candidate.label} 服务端命令字段`}>
      <div className="wire-action-command-title">
        <span>命令字段</span>
        <strong>{candidate.commandType}</strong>
      </div>
      <ol>
        {candidate.commandFields.map((field) => (
          <li className={field.required ? "is-required" : ""} data-command-field={field.field} key={field.key}>
            <span>{field.label}</span>
            <small>{field.required ? "必需" : "可选"} / {field.sourceLabel}</small>
          </li>
        ))}
      </ol>
    </div>
  );
}

function Metric({ metric }: { metric: WireActionMapMetric }) {
  return (
    <span className="wire-action-map-metric">
      <span>{metric.label}</span>
      <strong>{metric.value}</strong>
    </span>
  );
}

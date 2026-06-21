import type { TableObjectContext } from "../../utils/tableObjectContext";
import { useState } from "react";
import type { ActionPromptDto, GameCommand, SnapshotDto } from "../../types/protocol";
import type { CandidateSelectionDraft } from "../../utils/candidateSelectionDraft";
import {
  buildCommandSubmissionFollowupPlan,
  type CommandSubmissionFollowupFeedback,
  type ObservedGameEvent
} from "../../utils/commandSubmissionFollowupPlan";
import {
  buildWireTimelineDetailPlan,
  type WireTimelineCommandBridgeRow,
  type WireTimelineDetailInspectorPlan,
  type WireTimelineEvidenceRow,
  type WireTimelineNavigationRow,
  type WireTimelineNextStepPlan,
  type WireTimelineRouteSummaryPlan
} from "../../utils/wireTimelineDetailPlan";
import { WireCommandFollowupPanel } from "./WireCommandFollowupPanel";
import { WireObjectContextSummary } from "./WireObjectContextSummary";
import { WireObjectRefChips, type WireObjectIndex, type WireObjectRef } from "./WireObjectRefChips";
import type { WireTableViewModel } from "./wireTableViewModel";

export type WireTimelineDetailLine = {
  label: string;
  mine?: boolean;
  value: string;
};

export type WireTimelineDetail = {
  id: string;
  lines: WireTimelineDetailLine[];
  refs: WireObjectRef[];
  source: "event" | "rule";
  subtitle?: string;
  title: string;
};

export function WireTimelineDetailPanel({
  bodyId = "wire-timeline-detail-body",
  detail,
  disabledByConnection = false,
  events,
  objectContextById,
  objectIndex,
  onChooseObject,
  onCommand,
  onClear,
  onInspectObject,
  onOpenLayer,
  onOpenObjectDetail,
  onSelectServerEventKind,
  prompt,
  selectionDraft,
  selectedObjectContext,
  selectedObjectId,
  snapshot,
  submissionFeedback,
  table
}: {
  bodyId?: string;
  detail?: WireTimelineDetail;
  disabledByConnection?: boolean;
  events?: readonly ObservedGameEvent[];
  objectContextById?: Record<string, TableObjectContext>;
  objectIndex: WireObjectIndex;
  onChooseObject?: (objectId: string) => void;
  onCommand?: (command: GameCommand) => void;
  onClear: () => void;
  onInspectObject?: (objectId: string) => void;
  onOpenLayer?: () => void;
  onOpenObjectDetail?: (objectId: string) => void;
  onSelectServerEventKind?: (kind: string) => void;
  prompt?: ActionPromptDto;
  selectionDraft?: CandidateSelectionDraft;
  selectedObjectContext?: TableObjectContext;
  selectedObjectId?: string;
  snapshot?: SnapshotDto;
  submissionFeedback?: CommandSubmissionFollowupFeedback;
  table?: WireTableViewModel;
}) {
  const [inspectorOpen, setInspectorOpen] = useState(false);
  const plan = buildWireTimelineDetailPlan({
    detail,
    disabledByConnection,
    objectContextById,
    objectIndex,
    prompt,
    selectionDraft,
    selectedObjectContext,
    selectedObjectId
  });
  const submissionFollowup = buildCommandSubmissionFollowupPlan({
    events,
    feedback: submissionFeedback,
    snapshot
  });

  const detailState = detail?.source ?? (selectedObjectContext ? "object" : "empty");

  return (
    <section
      className="wire-timeline-detail"
      aria-label="规则与事件详情"
      data-wire-timeline-action-candidate-count={plan.inspector.actionCandidateCount}
      data-wire-timeline-command-bridge-count={plan.inspector.commandBridgeCount}
      data-wire-timeline-detail-id={detail?.id ?? ""}
      data-wire-timeline-detail-state={detailState}
      data-wire-timeline-hidden-ref-count={plan.inspector.hiddenRefCount}
      data-wire-timeline-missing-ref-count={plan.inspector.missingRefCount}
      data-wire-timeline-source={detail?.source ?? ""}
      data-wire-timeline-visible-ref-count={plan.inspector.visibleRefCount}
    >
      <header className="wire-timeline-detail-header">
        <div>
          <strong>{plan.headerTitle}</strong>
          <span>{plan.headerSubtitle}</span>
        </div>
        <div className="wire-timeline-detail-actions">
          {(detail || selectedObjectContext) && onOpenLayer && (
            <button
              aria-controls="wire-timeline-detail-layer"
              className="wire-detail-open-layer"
              data-wire-timeline-layer-open-trigger="true"
              onClick={onOpenLayer}
              type="button"
            >
              打开检查层
            </button>
          )}
          {detail && (
            <button aria-controls={bodyId} className="wire-detail-clear" onClick={onClear} type="button">
              清除
            </button>
          )}
        </div>
      </header>
      <div className="wire-timeline-detail-status-grid" aria-label="规则详情桌面投影摘要">
        {plan.statusCards.map((card) => (
          <span key={card.label}>
            <small>{card.label}</small>
            <strong>{card.value}</strong>
          </span>
        ))}
      </div>
      <TimelineEvidenceRows rows={plan.evidenceRows} />
      <TimelineRouteSummary plan={plan.routeSummary} />
      <div className="wire-timeline-detail-body" id={bodyId}>
        {detail ? (
          <>
            <TimelineNextStep
              onChooseObject={onChooseObject ?? onInspectObject}
              plan={plan.nextStep}
            />
            <button
              aria-expanded={inspectorOpen}
              className="wire-timeline-inspector-toggle"
              data-timeline-inspector-toggle="true"
              onClick={() => setInspectorOpen((open) => !open)}
              type="button"
            >
              {inspectorOpen ? "收起事件检查" : "展开事件检查"}
            </button>
            <TimelineNavigator
              onInspectObject={onInspectObject}
              rows={plan.navigationRows}
            />
            <TimelineCommandBridge
              onCommand={onCommand}
              onChooseObject={onChooseObject ?? onInspectObject}
              onOpenObjectDetail={onOpenObjectDetail}
              rows={plan.commandBridgeRows}
            />
            <WireCommandFollowupPanel
              ariaLabel="规则详情服务端后续事件"
              className="wire-command-followup wire-timeline-command-followup"
              onInspectObject={onInspectObject}
              onSelectServerEventKind={onSelectServerEventKind}
              plan={submissionFollowup}
              table={table}
            />
            <TimelineInspector open={inspectorOpen} plan={plan.inspector} />
            <div className="wire-timeline-detail-lines">
              {detail.lines.map((line) => (
                <span className={line.mine ? "wire-timeline-detail-line is-mine" : "wire-timeline-detail-line"} key={`${line.label}-${line.value}`}>
                  <span>{line.label}</span>
                  <strong>{line.value || "无"}</strong>
                </span>
              ))}
            </div>
            {plan.projectionRows.length > 0 && (
              <ol className="wire-timeline-projection-list" aria-label="详情对象桌面投影">
                {plan.projectionRows.map((row) => (
                  <li data-projection-state={row.state} key={row.key}>
                    <span>{row.role}</span>
                    <strong>{row.label}</strong>
                    <small>{row.stateLabel}</small>
                  </li>
                ))}
              </ol>
            )}
            {plan.actionHintRows.length > 0 && (
              <ol className="wire-timeline-action-hint-list" aria-label="详情对象服务端候选">
                {plan.actionHintRows.map((row) => (
                  <ActionHintRow key={row.key} onInspectObject={onInspectObject} row={row} />
                ))}
              </ol>
            )}
            <WireObjectRefChips
              objects={objectIndex}
              onInspectObject={onInspectObject}
              refs={detail.refs}
              selectedObjectId={selectedObjectId}
              source={detail.source}
            />
            {selectedObjectContext && (
              <ObjectContextDetail
                context={selectedObjectContext}
                objectIndex={objectIndex}
                onInspectObject={onInspectObject}
                selectedObjectId={selectedObjectId}
                title="当前桌面焦点"
              />
            )}
          </>
        ) : selectedObjectContext ? (
          <ObjectContextDetail
            context={selectedObjectContext}
            objectIndex={objectIndex}
            onInspectObject={onInspectObject}
            selectedObjectId={selectedObjectId}
            title="焦点对象"
          />
        ) : (
          <span className="empty-hint">暂无焦点事件。</span>
        )}
      </div>
    </section>
  );
}

function TimelineRouteSummary({ plan }: { plan: WireTimelineRouteSummaryPlan }) {
  return (
    <section
      aria-label="候选提交路线摘要"
      className="wire-timeline-route-summary"
      data-timeline-route-summary-state={plan.state}
    >
      <header>
        <span>{plan.headline}</span>
        <strong>{plan.stateLabel}</strong>
        <small>{plan.totalCount} 路径 / {plan.draftCount} 草稿</small>
      </header>
      <p>{plan.body}</p>
      <small>{plan.nextStepLabel}</small>
      <ol aria-label="候选路线状态计数">
        {plan.rows.map((row) => (
          <li
            data-timeline-route-count={row.key}
            data-timeline-route-count-state={row.state}
            key={row.key}
          >
            <span>{row.label}</span>
            <strong>{row.value}</strong>
          </li>
        ))}
      </ol>
    </section>
  );
}

function TimelineNextStep({
  onChooseObject,
  plan
}: {
  onChooseObject?: (objectId: string) => void;
  plan: WireTimelineNextStepPlan;
}) {
  if (plan.state === "empty") {
    return null;
  }

  return (
    <section
      aria-label="规则事件下一步"
      className="wire-timeline-next-step"
      data-timeline-next-step={plan.key}
      data-timeline-next-step-state={plan.state}
    >
      <header>
        <strong>{plan.headline}</strong>
        <span>{plan.commandType ?? "服务端上下文"}</span>
      </header>
      <p>{plan.body}</p>
      <small>{plan.detail}</small>
      {plan.steps.length > 0 && (
        <ol className="wire-timeline-next-step-grammar" aria-label="下一步选择语法">
          {plan.steps.map((step) => (
            <li
              data-timeline-next-step-grammar-role={step.role}
              data-timeline-next-step-grammar-state={step.state}
              key={step.key}
            >
              <span>{step.label}</span>
              <strong>{step.stateLabel}</strong>
              <small>{step.required ? "必需" : "可选"} / {step.selectedCount}/{step.availableCount}</small>
            </li>
          ))}
        </ol>
      )}
      {plan.checks.length > 0 && (
        <ol className="wire-timeline-next-step-checks" aria-label="下一步提交门禁">
          {plan.checks.map((check) => (
            <li
              data-timeline-next-step-check={check.key}
              data-timeline-next-step-check-state={check.state}
              key={check.key}
            >
              <span>{check.label}</span>
              <strong>{check.stateLabel}</strong>
              <small>{check.detail}</small>
            </li>
          ))}
        </ol>
      )}
      {plan.refs.length > 0 && (
        <div className="wire-timeline-next-step-refs" role="group" aria-label="下一步对象">
          {plan.refs.map((ref) => (
            <button
              data-timeline-next-step-object-id={ref.objectId}
              data-timeline-next-step-role={ref.roleLabel}
              disabled={!onChooseObject}
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
    </section>
  );
}

function TimelineEvidenceRows({ rows }: { rows: WireTimelineEvidenceRow[] }) {
  if (rows.length === 0) {
    return null;
  }

  return (
    <ol className="wire-timeline-evidence-list" aria-label="规则事件证据摘要">
      {rows.map((row) => (
        <li
          data-timeline-evidence={row.key}
          data-timeline-evidence-state={row.state}
          key={row.key}
        >
          <span>{row.label}</span>
          <strong>{row.value}</strong>
          <small>{row.stateLabel}</small>
        </li>
      ))}
    </ol>
  );
}

function TimelineCommandBridge({
  onCommand,
  onChooseObject,
  onOpenObjectDetail,
  rows
}: {
  onCommand?: (command: GameCommand) => void;
  onChooseObject?: (objectId: string) => void;
  onOpenObjectDetail?: (objectId: string) => void;
  rows: WireTimelineCommandBridgeRow[];
}) {
  if (rows.length === 0) {
    return null;
  }

  return (
    <section className="wire-timeline-command-bridge" aria-label="详情候选路径">
      <header>
        <strong>候选路径</strong>
        <span>{rows.length} 条服务端候选关联</span>
      </header>
      <ol>
        {rows.map((row) => {
          const canSubmit = row.submitPlan.canSubmit && Boolean(row.submitPlan.command) && Boolean(onCommand);
          return (
            <li
              data-timeline-command-bridge-draft-active={row.draftActive ? "true" : "false"}
              data-timeline-command-bridge-enabled={row.enabled ? "true" : "false"}
              data-timeline-command-bridge-detail-role={row.detailRoleLabel}
              data-timeline-command-bridge-object-id={row.detailObjectId}
              data-timeline-command-bridge-route-state={row.routeState}
              data-timeline-command-bridge-server-role={row.serverRoleSummary}
              key={row.key}
            >
            <div className="wire-timeline-command-bridge-main">
              <span>{row.label}</span>
              <strong>{row.nextStepLabel}</strong>
              <small>{row.detailLinkLabel}</small>
              <small>{row.roleLabels.join(" / ")} / {row.commandType ?? "未公开命令"} / {row.stateLabel}</small>
              <small>{row.selectionLabel} / {row.routeStateLabel} / {row.selectedStepCount}/{row.totalStepCount}</small>
              <small>{row.commandFieldSummary}</small>
              <small>{row.grammarSummary}</small>
              <small>提交门禁 / {row.gateSummary}</small>
              {!row.enabled && <small>{row.reasonLabel}</small>}
            </div>
            {onOpenObjectDetail && (
              <div className="wire-timeline-command-bridge-actions" role="group" aria-label={`${row.label} 检查入口`}>
                <button
                  data-timeline-command-open-detail-object-id={row.detailObjectId}
                  onClick={() => onOpenObjectDetail(row.detailObjectId)}
                  type="button"
                >
                  检查 / 组合
                </button>
              </div>
            )}
            {row.gateRows.length > 0 && (
              <ol className="wire-timeline-command-bridge-gates" aria-label={`${row.label} 提交门禁`}>
                {row.gateRows.map((gate) => (
                  <li
                    data-timeline-command-gate={gate.key}
                    data-timeline-command-gate-state={gate.state}
                    key={gate.key}
                  >
                    <span>{gate.label}</span>
                    <strong>{gate.stateLabel}</strong>
                    <small>{gate.reason}</small>
                  </li>
                ))}
              </ol>
            )}
            {row.grammarSteps.length > 0 && (
              <ol
                className="wire-timeline-command-bridge-grammar"
                aria-label={`${row.label} 提交语法`}
                data-timeline-command-grammar-state={row.grammarState}
              >
                {row.grammarSteps.map((step) => (
                  <li
                    data-timeline-command-grammar-role={step.role}
                    data-timeline-command-grammar-step-state={step.state}
                    key={step.key}
                  >
                    <span>{step.label}</span>
                    <small>{step.required ? "必需" : "可选"} / {step.selectedCount}/{step.availableCount} / {step.stateLabel}</small>
                  </li>
                ))}
              </ol>
            )}
            {row.commandFields.length > 0 && (
              <ol className="wire-timeline-command-bridge-fields" aria-label={`${row.label} 命令字段覆盖`}>
                {row.commandFields.map((field) => (
                  <li
                    data-timeline-command-field={field.field}
                    data-timeline-command-field-state={field.state}
                    key={field.key}
                  >
                    <span>{field.label}</span>
                    <small>{field.required ? "必需" : "可选"} / {field.sourceLabel} / {field.stateLabel}</small>
                  </li>
                ))}
              </ol>
            )}
            <div
              className="wire-timeline-command-submit-plan"
              data-timeline-command-submit-can-submit={row.submitPlan.canSubmit ? "true" : "false"}
              data-timeline-command-submit-command-ready={row.submitPlan.command ? "true" : "false"}
              data-timeline-command-submit-state={row.submitPlan.state}
              data-timeline-command-submit-type={row.submitPlan.commandType ?? ""}
            >
              <span>命令预览</span>
              <strong>{row.submitPlan.stateLabel}</strong>
              <small>{row.submitPlan.submitLabel}</small>
              <small>{row.submitPlan.fieldSummary}</small>
              <small>{row.submitPlan.reason}</small>
              <button
                data-timeline-command-submit="true"
                data-timeline-command-submit-enabled={canSubmit ? "true" : "false"}
                disabled={!canSubmit}
                onClick={() => {
                  if (!row.submitPlan.command || !onCommand) {
                    return;
                  }

                  onCommand(row.submitPlan.command);
                }}
                type="button"
              >
                {row.submitPlan.submitLabel}
              </button>
              {row.submitPlan.fields.length > 0 && (
                <ol aria-label={`${row.label} 可提交命令字段`}>
                  {row.submitPlan.fields.map((field) => (
                    <li
                      data-timeline-command-submit-field={field.field}
                      data-timeline-command-submit-field-state={field.state}
                      key={field.key}
                    >
                      <span>{field.label}</span>
                      <small>{field.stateLabel}</small>
                    </li>
                  ))}
                </ol>
              )}
            </div>
            {row.nextObjectRefs.length > 0 && (
              <div className="wire-timeline-command-bridge-refs" role="group" aria-label={`${row.label} 下一步对象`}>
                {row.nextObjectRefs.map((ref) => (
                  <button
                    data-timeline-command-bridge-next-object-id={ref.objectId}
                    data-timeline-command-bridge-next-role={ref.roleLabel}
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
          );
        })}
      </ol>
    </section>
  );
}

function TimelineNavigator({
  onInspectObject,
  rows
}: {
  onInspectObject?: (objectId: string) => void;
  rows: WireTimelineNavigationRow[];
}) {
  if (rows.length === 0) {
    return null;
  }

  return (
    <ol className="wire-timeline-navigation-list" aria-label="详情对象定位路径">
      {rows.map((row) => (
        <TimelineNavigatorRow key={row.key} onInspectObject={onInspectObject} row={row} />
      ))}
    </ol>
  );
}

function TimelineNavigatorRow({
  onInspectObject,
  row
}: {
  onInspectObject?: (objectId: string) => void;
  row: WireTimelineNavigationRow;
}) {
  const canInspect = Boolean(row.objectId && row.canFocus && onInspectObject);
  const content = (
    <>
      <span>{row.role}</span>
      <strong>{row.label}</strong>
      <small>{row.zoneLabel}</small>
      <small>{row.focusLabel}</small>
      <small>{row.actionLabel}</small>
    </>
  );

  return (
    <li
      data-timeline-navigation-action-state={row.actionState}
      data-timeline-navigation-focus-state={row.focusState}
      data-timeline-navigation-object-id={canInspect ? row.objectId : undefined}
      data-timeline-navigation-projection-state={row.projectionState}
      data-timeline-navigation-selected={row.selected ? "true" : undefined}
    >
      {canInspect ? (
        <button
          aria-label={`定位详情对象：${row.role} ${row.label}`}
          className="wire-timeline-navigation-button"
          onClick={() => onInspectObject?.(row.objectId ?? "")}
          type="button"
        >
          {content}
        </button>
      ) : (
        <div className="wire-timeline-navigation-static">{content}</div>
      )}
    </li>
  );
}

function TimelineInspector({ open, plan }: { open: boolean; plan: WireTimelineDetailInspectorPlan }) {
  return (
    <aside
      aria-label="规则事件检查器"
      className="wire-timeline-inspector"
      data-timeline-inspector-state={open ? "open" : "closed"}
      hidden={!open}
    >
      <header>
        <strong>事件检查</strong>
        <span>{plan.summary}</span>
      </header>
      <section>
        <strong>对象投影</strong>
        <ol className="wire-timeline-inspector-projections">
          {plan.projectionRows.map((row) => (
            <li data-timeline-inspector-projection={row.key} key={row.key}>
              <span>{row.label}</span>
              <strong>{row.count}</strong>
            </li>
          ))}
        </ol>
      </section>
      <section>
        <strong>关联候选</strong>
        {plan.candidateRows.length > 0 ? (
          <ol className="wire-timeline-inspector-candidates">
            {plan.candidateRows.map((row) => (
              <li data-timeline-inspector-candidate={row.key} key={row.key}>
                <span>{row.role}</span>
                <strong>{row.label}</strong>
                <small>{row.zoneLabel}</small>
                <small>{row.stateLabel}</small>
              </li>
            ))}
          </ol>
        ) : (
          <span className="empty-hint">当前详情对象没有服务端候选关联。</span>
        )}
      </section>
      <footer>
        <span>来源 {plan.sourceLabel}</span>
        <span>可定位 {plan.visibleRefCount}</span>
        <span>隐藏 {plan.hiddenRefCount}</span>
        <span>未公开 {plan.missingRefCount}</span>
        <span>候选 {plan.actionCandidateCount}</span>
        <span>路径 {plan.commandBridgeCount}</span>
      </footer>
    </aside>
  );
}

type ActionHintRowProps = {
  onInspectObject?: (objectId: string) => void;
  row: ReturnType<typeof buildWireTimelineDetailPlan>["actionHintRows"][number];
};

function ActionHintRow({ onInspectObject, row }: ActionHintRowProps) {
  const roleSummary = compactList(row.selectionRoleLabels, 3);
  const requiredFieldSummary = compactList(row.requiredCommandFieldLabels, 3);
  const commandFieldSummary = compactList(row.commandFieldLabels, 3);
  const reasonSummary = compactList(row.reasonLabels, 2);
  const content = (
    <>
      <span>{row.role}</span>
      <strong>{row.label}</strong>
      <small>{row.stateLabel}</small>
      <small>{row.zoneLabel}</small>
      <small>{row.commandTypes.length > 0 ? row.commandTypes.join(" / ") : "服务端候选"}</small>
      {roleSummary && <small>角色 {roleSummary}</small>}
      {requiredFieldSummary && <small>必填 {requiredFieldSummary}</small>}
      {!requiredFieldSummary && commandFieldSummary && <small>字段 {commandFieldSummary}</small>}
      {reasonSummary && <small>阻断 {reasonSummary}</small>}
    </>
  );

  return (
    <li data-action-hint-clickable={onInspectObject ? "true" : "false"} data-action-object-id={row.objectId}>
      {onInspectObject ? (
        <button
          aria-label={`聚焦关联候选：${row.label}`}
          className="wire-timeline-action-hint-button"
          data-action-hint-object-id={row.objectId}
          onClick={() => onInspectObject(row.objectId)}
          type="button"
        >
          {content}
        </button>
      ) : (
        content
      )}
    </li>
  );
}

function compactList(values: string[], limit: number): string {
  const visible = values.slice(0, limit);
  if (visible.length === 0) {
    return "";
  }

  return values.length > limit
    ? `${visible.join(" / ")} +${values.length - limit}`
    : visible.join(" / ");
}

function ObjectContextDetail({
  context,
  objectIndex,
  onInspectObject,
  selectedObjectId,
  title
}: {
  context: TableObjectContext;
  objectIndex: WireObjectIndex;
  onInspectObject?: (objectId: string) => void;
  selectedObjectId?: string;
  title: string;
}) {
  return (
    <div className="wire-selected-object-context" data-wire-selected-object-context={context.objectId}>
      <strong>{title}</strong>
      <WireObjectRefChips
        objects={objectIndex}
        onInspectObject={onInspectObject}
        refs={[{ id: context.objectId, label: context.cardNo ?? context.object?.cardNo ?? undefined, role: "对象" }]}
        selectedObjectId={selectedObjectId}
        source="rule"
      />
      <WireObjectContextSummary context={context} />
    </div>
  );
}

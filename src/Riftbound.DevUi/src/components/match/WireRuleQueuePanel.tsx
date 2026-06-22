import type { ActionPromptDto, GameEvent, SnapshotDto } from "../../types/protocol";
import { Children, type ReactNode, useState } from "react";
import type { InspectedCard } from "../cards/CardFace";
import { buildWireRuleQueuePlan, type WireRuleQueueCoverageRow, type WireRuleQueueFocusPlan, type WireRuleQueueInspectorPlan, type WireRuleQueueItemPlan, type WireRuleQueueLane, type WireRuleQueueResponsibilityItem, type WireRuleQueueResponsibilityPlan, type WireRuleQueueSelectedObjectPlan, type WireRuleQueueSequenceItem } from "../../utils/wireRuleQueuePlan";
import { buildCardObjectIndex } from "../../utils/snapshotObjectIndex";
import type { CommandSubmitHandler } from "../../utils/commandSubmissionFollowupPlan";
import type { ServerSubmissionGatePlan } from "../../utils/serverSubmissionGatePlan";
import { buildTableObjectContextModel, type TableObjectContext } from "../../utils/tableObjectContext";
import type { WireFocusedInteractionPlan } from "../../utils/wireFocusedInteractionPlan";
import { StatusPill } from "../ui/StatusPill";
import { WireDetailTrigger } from "./WireDetailTrigger";
import { WireObjectCommandTray } from "./WireObjectCommandTray";
import { WireObjectInspectionSummary } from "./WireObjectInspectionSummary";
import { WireObjectRefChips, type WireObjectIndex } from "./WireObjectRefChips";
import { WireRuleAuthorityPanel } from "./WireRuleAuthorityPanel";
import type { WireTimelineDetail } from "./WireTimelineDetailPanel";
import { useWireDialogFocus } from "./useWireDialogFocus";

type WireRuleQueuePanelProps = {
  disabledByConnection?: boolean;
  events?: GameEvent[];
  focusedPlan?: WireFocusedInteractionPlan;
  inspectedCard?: InspectedCard;
  onClearInspectedCard?: () => void;
  onCommand?: CommandSubmitHandler;
  onInspectObject?: (objectId: string) => void;
  onOpenDetail?: (card: InspectedCard) => void;
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  playerId: string;
  prompt?: ActionPromptDto;
  selectedDetailId?: string;
  selectedObjectId?: string;
  snapshot?: SnapshotDto;
  submissionGate?: ServerSubmissionGatePlan;
};

type ObjectIndex = WireObjectIndex;

export function WireRuleQueuePanel({
  disabledByConnection = false,
  events,
  focusedPlan,
  inspectedCard,
  onClearInspectedCard,
  onCommand,
  onInspectObject,
  onOpenDetail,
  onSelectDetail,
  playerId,
  prompt,
  selectedDetailId,
  selectedObjectId,
  snapshot,
  submissionGate
}: WireRuleQueuePanelProps) {
  const [inspectorOpen, setInspectorOpen] = useState(false);
  const [responsibilityLayerOpen, setResponsibilityLayerOpen] = useState(false);
  const plan = buildWireRuleQueuePlan({ events, playerId, prompt, selectedObjectId, snapshot });
  const objects = buildCardObjectIndex(snapshot);
  const objectContext = selectedObjectId
    ? buildTableObjectContextModel({ events, perspectivePlayerId: playerId, prompt, snapshot }).byId[selectedObjectId]
    : undefined;

  return (
    <section className="wire-rule-queue" aria-label="服务端规则队列" data-wire-rule-queue-state={plan.state}>
      <header className="wire-rule-queue-header">
        <div>
          <strong>{plan.header.title}</strong>
          <span>{plan.header.subtitle}</span>
        </div>
        <StatusPill tone={plan.header.statusTone}>{plan.header.statusLabel}</StatusPill>
      </header>

      <WireRuleAuthorityPanel events={events} snapshot={snapshot} />

      <section className="wire-rule-flow" aria-label="服务端规则队列地图">
        <div className="wire-rule-flow-heading">
          <strong>规则队列地图</strong>
          <span>{plan.stateLabel}</span>
        </div>
        <ol className="wire-rule-lanes">
          {plan.lanes.map((lane) => (
            <RuleLaneCard
              key={lane.key}
              lane={lane}
              onSelectDetail={onSelectDetail}
              selectedDetailId={selectedDetailId}
            />
          ))}
        </ol>
        <div className="wire-rule-flow-next" data-wire-rule-next-lane={plan.activeLaneKey}>
          下一步：{plan.nextStepLabel}
        </div>
        <RuleResponsibilityTimeline
          layerOpen={responsibilityLayerOpen}
          objects={objects}
          onInspectObject={onInspectObject}
          onOpenLayer={() => setResponsibilityLayerOpen(true)}
          onSelectDetail={onSelectDetail}
          plan={plan.responsibility}
          selectedDetailId={selectedDetailId}
          selectedObjectId={selectedObjectId}
        />
        {responsibilityLayerOpen && (
          <RuleResponsibilityLayer
            objects={objects}
            onClose={() => setResponsibilityLayerOpen(false)}
            onInspectObject={onInspectObject}
            onSelectDetail={onSelectDetail}
            plan={plan.responsibility}
            selectedDetailId={selectedDetailId}
            selectedObjectId={selectedObjectId}
          />
        )}
        <RuleCoverageStrip
          coverage={plan.coverage}
          onSelectDetail={onSelectDetail}
          selectedDetailId={selectedDetailId}
        />
        {plan.sequence.length > 0 && (
          <ol className="wire-rule-sequence" aria-label="服务端规则队列顺序">
            {plan.sequence.map((item) => (
              <RuleSequenceItem
                item={item}
                key={item.key}
                objects={objects}
                onInspectObject={onInspectObject}
                onSelectDetail={onSelectDetail}
                selectedDetailId={selectedDetailId}
                selectedObjectId={selectedObjectId}
              />
            ))}
          </ol>
        )}
        <button
          aria-expanded={inspectorOpen}
          className="wire-rule-inspector-toggle"
          data-rule-inspector-toggle="true"
          onClick={() => setInspectorOpen((open) => !open)}
          type="button"
        >
          {inspectorOpen ? "收起规则检查" : "展开规则检查"}
        </button>
        <RuleQueueInspector
          objects={objects}
          onInspectObject={onInspectObject}
          onSelectDetail={onSelectDetail}
          open={inspectorOpen}
          plan={plan.inspector}
          selectedDetailId={selectedDetailId}
          selectedObjectId={selectedObjectId}
        />
      </section>

      <RuleFocus
        focus={plan.focus}
        objects={objects}
        onInspectObject={onInspectObject}
        onSelectDetail={onSelectDetail}
        selectedDetailId={selectedDetailId}
        selectedObjectId={selectedObjectId}
      />

      <RuleSelectedObjectProjection
        context={objectContext}
        contract={prompt?.contract}
        disabledByConnection={disabledByConnection}
        focusedPlan={focusedPlan}
        inspectedCard={inspectedCard}
        onClearInspectedCard={onClearInspectedCard}
        onCommand={onCommand}
        onOpenDetail={onOpenDetail}
        onSelectDetail={onSelectDetail}
        plan={plan.selectedObject}
        prompt={prompt}
        selectedDetailId={selectedDetailId}
        snapshot={snapshot}
        submissionGate={submissionGate}
      />

      <div className="wire-rule-state-grid">
        {plan.metrics.map((metric) => (
          <RuleMetric key={metric.key} label={metric.label} mine={metric.mine} value={metric.value} />
        ))}
      </div>

      {plan.sections.map((section) => (
        <RuleSection emptyLabel={section.emptyLabel} key={section.key} sectionKey={section.key} title={section.title}>
          {section.notes.length > 0 && (
            <div className="wire-rule-note">
              {section.notes.map((note) => (
                <span key={note}>{note}</span>
              ))}
            </div>
          )}
          {section.items.map((item) => (
            <RuleQueueItem
              item={item}
              key={item.key}
              objects={objects}
              onInspectObject={onInspectObject}
              onSelectDetail={onSelectDetail}
              selectedDetailId={selectedDetailId}
              selectedObjectId={selectedObjectId}
            />
          ))}
        </RuleSection>
      ))}
    </section>
  );
}

function RuleSelectedObjectProjection({
  context,
  contract,
  disabledByConnection,
  focusedPlan,
  inspectedCard,
  onClearInspectedCard,
  onCommand,
  onOpenDetail,
  onSelectDetail,
  plan,
  prompt,
  selectedDetailId,
  snapshot,
  submissionGate
}: {
  context?: TableObjectContext;
  contract?: ActionPromptDto["contract"];
  disabledByConnection: boolean;
  focusedPlan?: WireFocusedInteractionPlan;
  inspectedCard?: InspectedCard;
  onClearInspectedCard?: () => void;
  onCommand?: CommandSubmitHandler;
  onOpenDetail?: (card: InspectedCard) => void;
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  plan: WireRuleQueueSelectedObjectPlan;
  prompt?: ActionPromptDto;
  selectedDetailId?: string;
  snapshot?: SnapshotDto;
  submissionGate?: ServerSubmissionGatePlan;
}) {
  const inspectedObjectId = inspectedCard?.objectId ?? inspectedCard?.object?.objectId;
  const canRenderCommandTray = Boolean(
    plan.objectId
    && inspectedCard
    && focusedPlan
    && onClearInspectedCard
    && onOpenDetail
    && inspectedObjectId === plan.objectId
  );

  return (
    <section
      aria-label="选中对象规则投影"
      className="wire-rule-selected-object"
      data-rule-selected-object={plan.objectId ?? ""}
      data-rule-selected-object-relation-count={plan.relationCount}
      data-rule-selected-object-state={plan.state}
    >
      <div className="wire-rule-selected-object-heading">
        <strong>选中对象投影</strong>
        <span>{plan.summary}</span>
      </div>
      {context && <WireObjectInspectionSummary context={context} contract={contract} />}
      {canRenderCommandTray && focusedPlan && inspectedCard && onClearInspectedCard && onOpenDetail && (
        <section
          aria-label="选中对象规则命令托盘"
          className="wire-rule-selected-object-command"
          data-rule-selected-object-command-object={plan.objectId ?? ""}
          data-rule-selected-object-command-state={focusedPlan.readiness.state}
        >
          <WireObjectCommandTray
            disabledByConnection={disabledByConnection}
            focusedPlan={focusedPlan}
            inspectedCard={inspectedCard}
            objectContext={context}
            onClear={onClearInspectedCard}
            onCommand={onCommand}
            onOpenDetail={onOpenDetail}
            prompt={prompt}
            snapshot={snapshot}
            submissionGate={submissionGate}
          />
        </section>
      )}
      {plan.syntaxRows.length > 0 && (
        <section
          aria-label="选中对象候选语法"
          className="wire-rule-selected-object-syntax"
          data-rule-selected-object-syntax-count={plan.syntaxRows.length}
          data-rule-selected-object-syntax-missing-required-count={plan.missingRequiredSyntaxCount}
          data-rule-selected-object-syntax-usable-count={plan.usableSyntaxCount}
        >
          <strong>候选语法</strong>
          <span data-rule-selected-object-syntax-summary>{plan.syntaxSummary}</span>
          <ol>
            {plan.syntaxRows.slice(0, 6).map((row) => (
              <li
                data-rule-selected-object-syntax-role={row.role}
                data-rule-selected-object-syntax-source={row.source}
                data-rule-selected-object-syntax-state={row.state}
                key={row.key}
              >
                <span>{row.sourceLabel} / {row.candidateLabel}</span>
                <strong>{row.roleLabel} / {row.stateLabel}</strong>
                <small>{row.objectChoiceCount}/{row.choiceCount} 选项{row.required ? " / 必选" : " / 可选"}</small>
              </li>
            ))}
          </ol>
        </section>
      )}
      {plan.relations.length === 0 ? (
        <span className="empty-hint">{plan.summary}</span>
      ) : (
        <ol className="wire-rule-selected-object-relations" aria-label="选中对象关联规则线索">
          {plan.relations.slice(0, 8).map((relation) => (
            <li
              data-rule-selected-object-relation={relation.key}
              data-rule-selected-object-relation-actions={relation.candidateActions.join("|")}
              data-rule-selected-object-relation-detail={relation.detailId ?? ""}
              data-rule-selected-object-relation-lane={relation.laneKey ?? ""}
              data-rule-selected-object-relation-source={relation.source}
              data-rule-selected-object-relation-state={relation.state}
              key={relation.key}
              title={relation.boundaryLabel}
            >
              <span>{relation.sourceLabel} / {relation.laneLabel}</span>
              <strong>{relation.roleLabel} / {relation.stateLabel}</strong>
              <small>{relation.detailLabel}</small>
              {relation.candidateActions.length > 0 && <small>{relation.candidateActions.join(" / ")}</small>}
              <small>{relation.stepSummary ?? "无步骤摘要"}</small>
              {relation.candidateCount != null && (
                <em>{relation.enabledCandidateCount ?? 0}/{relation.candidateCount} 候选</em>
              )}
              {relation.detail && (
                <WireDetailTrigger
                  detail={relation.detail}
                  label="规则详情"
                  onSelectDetail={onSelectDetail}
                  selectedDetailId={selectedDetailId}
                />
              )}
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

function RuleFocus({
  focus,
  objects,
  onInspectObject,
  onSelectDetail,
  selectedDetailId,
  selectedObjectId
}: {
  focus: WireRuleQueueFocusPlan;
  objects: ObjectIndex;
  onInspectObject?: (objectId: string) => void;
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  selectedDetailId?: string;
  selectedObjectId?: string;
}) {
  return (
    <section
      className="wire-rule-focus"
      data-rule-focus-detail-id={focus.detail?.id ?? ""}
      data-rule-focus-lane={focus.laneKey}
    >
      <div className="wire-rule-focus-heading">
        <div>
          <strong>当前规则焦点</strong>
          <span>{focus.laneLabel} / {focus.reasonLabel}</span>
        </div>
        {focus.detail && (
          <RuleDetailButton detail={focus.detail} onSelectDetail={onSelectDetail} selectedDetailId={selectedDetailId} />
        )}
      </div>
      {focus.detail ? (
        <>
          <div className="wire-rule-focus-lines">
            {focus.detail.lines.slice(0, 4).map((line) => (
              <RuleLine key={`${line.label}-${line.value}`} label={line.label} mine={line.mine} value={line.value} />
            ))}
          </div>
          <WireObjectRefChips objects={objects} onInspectObject={onInspectObject} refs={focus.detail.refs} selectedObjectId={selectedObjectId} source="rule" />
          <RuleFocusActionBridge
            focus={focus}
            objects={objects}
            onInspectObject={onInspectObject}
            selectedObjectId={selectedObjectId}
          />
        </>
      ) : (
        <span className="empty-hint">{focus.emptyLabel}</span>
      )}
    </section>
  );
}

function RuleFocusActionBridge({
  focus,
  objects,
  onInspectObject,
  selectedObjectId
}: {
  focus: WireRuleQueueFocusPlan;
  objects: ObjectIndex;
  onInspectObject?: (objectId: string) => void;
  selectedObjectId?: string;
}) {
  if (focus.actionRows.length === 0) {
    return null;
  }

  return (
    <ol className="wire-rule-focus-action-bridge" aria-label="规则焦点关联服务端候选">
      {focus.actionRows.map((row) => {
        const canInspect = Boolean(objects[row.objectId] && onInspectObject);
        const content = (
          <>
            <span>{row.serverRoleLabel || "规则对象"}</span>
            <strong>{row.actionRoleLabels.length > 0 ? row.actionRoleLabels.join(" / ") : row.stateLabel}</strong>
            <small>{row.enabledCandidateCount}/{row.candidateCount} 候选</small>
            <small>{row.semanticSummary}</small>
            <small>{row.authorityLabel} / {row.selectionStepSummary}</small>
            <em>{row.nextStepLabel}</em>
          </>
        );

        return (
          <li
            data-rule-focus-action-authority={row.authorityLabel}
            data-rule-focus-action-boundary={row.candidateBoundaryLabel ?? ""}
            data-rule-focus-action-candidate-count={row.candidateCount}
            data-rule-focus-action-object-id={row.objectId}
            data-rule-focus-action-selected={selectedObjectId === row.objectId ? "true" : "false"}
            data-rule-focus-action-semantic={row.semanticSummary}
            data-rule-focus-action-state={row.state}
            data-rule-focus-action-steps={row.selectionStepSummary}
            key={row.key}
            title={row.candidateBoundaryLabel}
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

function RuleQueueInspector({
  objects,
  onInspectObject,
  onSelectDetail,
  open,
  plan,
  selectedDetailId,
  selectedObjectId
}: {
  objects: ObjectIndex;
  onInspectObject?: (objectId: string) => void;
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  open: boolean;
  plan: WireRuleQueueInspectorPlan;
  selectedDetailId?: string;
  selectedObjectId?: string;
}) {
  return (
    <aside
      aria-label="规则队列检查器"
      className="wire-rule-inspector"
      data-rule-inspector-state={open ? "open" : "closed"}
      hidden={!open}
    >
      <header>
        <strong>规则检查</strong>
        <span>{plan.summary}</span>
      </header>
      <section>
        <strong>通道</strong>
        <ol className="wire-rule-inspector-lanes">
          {plan.lanes.map((lane) => (
            <li
              data-rule-inspector-lane={lane.key}
              data-rule-inspector-lane-detail-id={lane.detail?.id ?? ""}
              data-rule-inspector-lane-state={lane.state}
              key={lane.key}
            >
              <span>{lane.label}</span>
              <strong>{lane.count} 项 / {lane.stateLabel}</strong>
              <small>{lane.headline}</small>
              <small>{lane.hint}</small>
              {lane.detail ? (
                <RuleDetailButton
                  detail={lane.detail}
                  onSelectDetail={onSelectDetail}
                  selectedDetailId={selectedDetailId}
                />
              ) : null}
            </li>
          ))}
        </ol>
      </section>
      <section>
        <strong>事件覆盖</strong>
        <ol className="wire-rule-inspector-coverage">
          {plan.coverage.map((row) => (
            <RuleCoverageItem key={row.key} row={row} />
          ))}
        </ol>
      </section>
      <section>
        <strong>顺序</strong>
        {plan.sequence.length > 0 ? (
          <ol className="wire-rule-inspector-sequence">
            {plan.sequence.map((item) => (
              <li
                data-rule-inspector-sequence-detail-id={item.detail?.id ?? ""}
                data-rule-inspector-sequence-lane={item.laneLabel}
                key={item.key}
              >
                <span>{item.label}</span>
                <strong>{item.laneLabel} / {item.detailLabel}</strong>
                <small>{item.stateLabel} / {item.tickLabel ?? `${item.objectCount} 对象`}</small>
                {item.detail ? (
                  <RuleDetailButton
                    detail={item.detail}
                    onSelectDetail={onSelectDetail}
                    selectedDetailId={selectedDetailId}
                  />
                ) : null}
                <WireObjectRefChips
                  className="wire-rule-inspector-object-refs"
                  objects={objects}
                  onInspectObject={onInspectObject}
                  refs={item.refs}
                  selectedObjectId={selectedObjectId}
                  source="rule"
                />
              </li>
            ))}
          </ol>
        ) : (
          <span className="empty-hint">当前无服务端队列顺序。</span>
        )}
      </section>
      <footer>
        <span>状态 {plan.stateLabel}</span>
        <span>活动 {plan.activeLaneLabel}</span>
        <span>下一步 {plan.nextStepLabel}</span>
      </footer>
    </aside>
  );
}

function RuleCoverageStrip({
  coverage,
  onSelectDetail,
  selectedDetailId
}: {
  coverage: WireRuleQueueCoverageRow[];
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  selectedDetailId?: string;
}) {
  return (
    <ol className="wire-rule-coverage" aria-label="规则事件覆盖">
      {coverage.map((row) => (
        <RuleCoverageItem
          key={row.key}
          onSelectDetail={onSelectDetail}
          row={row}
          selectedDetailId={selectedDetailId}
        />
      ))}
    </ol>
  );
}

function RuleCoverageItem({
  onSelectDetail,
  row,
  selectedDetailId
}: {
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  row: WireRuleQueueCoverageRow;
  selectedDetailId?: string;
}) {
  return (
    <li
      data-rule-coverage={row.key}
      data-rule-coverage-detail-id={row.detail?.id ?? ""}
      data-rule-coverage-state={row.state}
    >
      <small>{row.label}</small>
      <strong>{row.stateLabel}</strong>
      <span>快照 {row.liveCount} / 事件 {row.eventCount}</span>
      <em>{row.hint}</em>
      {row.detail ? (
        <RuleDetailButton
          detail={row.detail}
          onSelectDetail={onSelectDetail}
          selectedDetailId={selectedDetailId}
        />
      ) : null}
    </li>
  );
}

function RuleLaneCard({
  lane,
  onSelectDetail,
  selectedDetailId
}: {
  lane: WireRuleQueueLane;
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  selectedDetailId?: string;
}) {
  return (
    <li
      data-rule-lane={lane.key}
      data-rule-lane-detail-id={lane.detail?.id ?? ""}
      data-rule-lane-state={lane.state}
    >
      <small>{lane.label}</small>
      <strong>{lane.count} 项</strong>
      <span>{lane.headline}</span>
      <em>{lane.hint}</em>
      {lane.detail ? (
        <RuleDetailButton
          detail={lane.detail}
          onSelectDetail={onSelectDetail}
          selectedDetailId={selectedDetailId}
        />
      ) : null}
    </li>
  );
}

function RuleResponsibilityTimeline({
  layerOpen,
  objects,
  onInspectObject,
  onOpenLayer,
  onSelectDetail,
  plan,
  selectedDetailId,
  selectedObjectId
}: {
  layerOpen: boolean;
  objects: ObjectIndex;
  onInspectObject?: (objectId: string) => void;
  onOpenLayer: () => void;
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  plan: WireRuleQueueResponsibilityPlan;
  selectedDetailId?: string;
  selectedObjectId?: string;
}) {
  return (
    <section
      aria-label="响应责任时间线"
      className="wire-rule-responsibility"
      data-rule-responsibility-active-count={plan.activeCount}
    >
      <div className="wire-rule-responsibility-heading">
        <strong>响应责任时间线</strong>
        <div>
          <span>{plan.stateLabel}</span>
          <button
            aria-controls="wire-rule-responsibility-layer"
            aria-expanded={layerOpen}
            className="wire-rule-responsibility-open-layer"
            data-rule-responsibility-layer-trigger={plan.items.length > 0 ? "available" : "empty"}
            onClick={onOpenLayer}
            type="button"
          >
            打开责任检查层
          </button>
        </div>
      </div>
      <small>{plan.summary}</small>
      {plan.items.length === 0 ? (
        <span className="empty-hint">当前没有服务端队列项。</span>
      ) : (
        <ol>
          {plan.items.map((item) => (
            <RuleResponsibilityItem
              item={item}
              key={item.key}
              objects={objects}
              onInspectObject={onInspectObject}
              onSelectDetail={onSelectDetail}
              selectedDetailId={selectedDetailId}
              selectedObjectId={selectedObjectId}
            />
          ))}
        </ol>
      )}
    </section>
  );
}

function RuleResponsibilityLayer({
  objects,
  onClose,
  onInspectObject,
  onSelectDetail,
  plan,
  selectedDetailId,
  selectedObjectId
}: {
  objects: ObjectIndex;
  onClose: () => void;
  onInspectObject?: (objectId: string) => void;
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  plan: WireRuleQueueResponsibilityPlan;
  selectedDetailId?: string;
  selectedObjectId?: string;
}) {
  const { closeButtonRef, dialogRef } = useWireDialogFocus(onClose);

  return (
    <div
      aria-labelledby="wire-rule-responsibility-layer-title"
      aria-modal="true"
      className="wire-rule-responsibility-layer"
      data-rule-responsibility-layer-active-count={plan.activeCount}
      data-rule-responsibility-layer-item-count={plan.items.length}
      data-rule-responsibility-layer-ready-count={plan.submitReadyCount}
      data-rule-responsibility-layer-state="open"
      id="wire-rule-responsibility-layer"
      role="dialog"
    >
      <button aria-label="关闭响应责任检查层" className="wire-rule-responsibility-layer-scrim" onClick={onClose} type="button" />
      <aside className="wire-rule-responsibility-dialog" ref={dialogRef} tabIndex={-1}>
        <header className="wire-rule-responsibility-layer-header">
          <div>
            <span>响应责任检查层</span>
            <h2 id="wire-rule-responsibility-layer-title">{plan.stateLabel}</h2>
          </div>
          <button className="wire-rule-responsibility-layer-close" onClick={onClose} ref={closeButtonRef} type="button">
            关闭检查层
          </button>
        </header>
        <div className="wire-rule-responsibility-layer-body" id="wire-rule-responsibility-layer-body">
          <section data-rule-responsibility-layer-section="summary">
            <strong>责任状态</strong>
            <span>{plan.summary}</span>
            <div className="wire-rule-responsibility-layer-metrics">
              <span data-rule-responsibility-layer-metric="items">
                <b>队列项</b>
                <small>{plan.items.length}</small>
              </span>
              <span data-rule-responsibility-layer-metric="active">
                <b>需关注</b>
                <small>{plan.activeCount}</small>
              </span>
              <span data-rule-responsibility-layer-metric="ready">
                <b>可提交</b>
                <small>{plan.submitReadyCount}</small>
              </span>
            </div>
          </section>
          <section data-rule-responsibility-layer-section="submits">
            <strong>提交入口</strong>
            {plan.items.length === 0 ? (
              <span className="empty-hint">当前没有服务端队列项。</span>
            ) : (
              <ol className="wire-rule-responsibility-layer-submits">
                {plan.items.map((item) => (
                  <li
                    data-rule-responsibility-layer-submit-item={item.key}
                    data-rule-responsibility-layer-submit-ready={item.submit.canSubmit ? "true" : "false"}
                    data-rule-responsibility-layer-submit-semantic={item.submit.semanticSummary}
                    data-rule-responsibility-layer-submit-state={item.submit.state}
                    key={`submit:${item.key}`}
                  >
                    <span>{item.actionLabel}</span>
                    <strong>{item.submit.stateLabel}</strong>
                    <small>{item.submit.enabledCandidateCount}/{item.submit.candidateCount} 候选 / {item.submit.promptType}</small>
                    <small>动作：{item.submit.semanticSummary}</small>
                    <RuleSubmitSemanticRows item={item} layer />
                    <small>{item.submit.reason}</small>
                  </li>
                ))}
              </ol>
            )}
          </section>
          <section data-rule-responsibility-layer-section="items">
            <strong>责任项目</strong>
            {plan.items.length === 0 ? (
              <span className="empty-hint">当前没有结算链、规则任务、触发或近期事件。</span>
            ) : (
              <ol className="wire-rule-responsibility-layer-items">
                {plan.items.map((item) => (
                  <RuleResponsibilityLayerItem
                    item={item}
                    key={item.key}
                    objects={objects}
                    onInspectObject={onInspectObject}
                    onSelectDetail={onSelectDetail}
                    selectedDetailId={selectedDetailId}
                    selectedObjectId={selectedObjectId}
                  />
                ))}
              </ol>
            )}
          </section>
        </div>
        <footer className="wire-rule-responsibility-layer-footer">
          <span data-rule-responsibility-layer-authority="server">响应责任和候选入口来自服务端 prompt 与规则队列投影</span>
          <span data-rule-responsibility-layer-hidden-boundary="true">隐藏对象仅显示边界，不泄漏牌面</span>
        </footer>
      </aside>
    </div>
  );
}

function RuleResponsibilityLayerItem({
  item,
  objects,
  onInspectObject,
  onSelectDetail,
  selectedDetailId,
  selectedObjectId
}: {
  item: WireRuleQueueResponsibilityItem;
  objects: ObjectIndex;
  onInspectObject?: (objectId: string) => void;
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  selectedDetailId?: string;
  selectedObjectId?: string;
}) {
  return (
    <li
      data-rule-responsibility-layer-detail-id={item.detail?.id ?? ""}
      data-rule-responsibility-layer-item={item.key}
      data-rule-responsibility-layer-lane={item.lane}
      data-rule-responsibility-layer-state={item.state}
    >
      <div>
        <small>{item.label}</small>
        <strong>{item.stateLabel}</strong>
      </div>
      <span>{item.actionLabel} / {item.detailLabel}</span>
      <em>{item.actorLabel} / {item.objectCount} 对象</em>
      {item.detail ? (
        <RuleDetailButton
          detail={item.detail}
          onSelectDetail={onSelectDetail}
          selectedDetailId={selectedDetailId}
        />
      ) : null}
      <small>{item.reason}</small>
      <WireObjectRefChips
        className="wire-rule-responsibility-layer-object-refs"
        objects={objects}
        onInspectObject={onInspectObject}
        refs={item.refs}
        selectedObjectId={selectedObjectId}
        source="rule"
      />
    </li>
  );
}

function RuleResponsibilityItem({
  item,
  objects,
  onInspectObject,
  onSelectDetail,
  selectedDetailId,
  selectedObjectId
}: {
  item: WireRuleQueueResponsibilityItem;
  objects: ObjectIndex;
  onInspectObject?: (objectId: string) => void;
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  selectedDetailId?: string;
  selectedObjectId?: string;
}) {
  return (
    <li
      data-rule-responsibility-detail-id={item.detail?.id ?? ""}
      data-rule-responsibility-lane={item.lane}
      data-rule-responsibility-state={item.state}
      data-rule-responsibility-submit-ready={item.submit.canSubmit ? "true" : "false"}
      data-rule-responsibility-submit-semantic={item.submit.semanticSummary}
      data-rule-responsibility-submit-state={item.submit.state}
    >
      <div>
        <small>{item.label}</small>
        <strong>{item.stateLabel}</strong>
      </div>
      <span>{item.actionLabel} / {item.detailLabel}</span>
      <em>{item.actorLabel} / {item.objectCount} 对象</em>
      {item.detail ? (
        <RuleDetailButton
          detail={item.detail}
          onSelectDetail={onSelectDetail}
          selectedDetailId={selectedDetailId}
        />
      ) : null}
      <span
        className="wire-rule-responsibility-submit"
        data-rule-responsibility-submit={item.submit.state}
      >
        {item.submit.stateLabel} / {item.submit.enabledCandidateCount}/{item.submit.candidateCount} 候选 / {item.submit.reason}
      </span>
      <RuleSubmitSemanticRows item={item} />
      <small>{item.reason}</small>
      <WireObjectRefChips
        className="wire-rule-responsibility-object-refs"
        objects={objects}
        onInspectObject={onInspectObject}
        refs={item.refs}
        selectedObjectId={selectedObjectId}
        source="rule"
      />
    </li>
  );
}

function RuleSubmitSemanticRows({
  item,
  layer = false
}: {
  item: WireRuleQueueResponsibilityItem;
  layer?: boolean;
}) {
  if (item.submit.semanticRows.length === 0) {
    return <small data-rule-responsibility-submit-semantic-empty="true">动作：无动作语义</small>;
  }

  return (
    <ol
      aria-label={`${item.label} 服务端候选动作语义`}
      className="wire-rule-responsibility-semantics"
      data-rule-responsibility-submit-semantic-list={layer ? "layer" : "timeline"}
    >
      {item.submit.semanticRows.map((row) => (
        <li
          data-rule-responsibility-submit-semantic-category={row.category}
          data-rule-responsibility-submit-semantic-enabled-count={row.enabledCount}
          data-rule-responsibility-submit-semantic-intent={row.intent}
          data-rule-responsibility-submit-semantic-priority={row.priority}
          data-rule-responsibility-submit-semantic-ui-hint={row.uiHint}
          key={row.key}
        >
          <span>{row.category}</span>
          <strong>{row.intent}</strong>
          <small>{row.enabledCount}/{row.count}</small>
        </li>
      ))}
    </ol>
  );
}

function RuleSequenceItem({
  item,
  objects,
  onInspectObject,
  onSelectDetail,
  selectedDetailId,
  selectedObjectId
}: {
  item: WireRuleQueueSequenceItem;
  objects: ObjectIndex;
  onInspectObject?: (objectId: string) => void;
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  selectedDetailId?: string;
  selectedObjectId?: string;
}) {
  return (
    <li data-rule-sequence-detail-id={item.detail?.id ?? ""} data-rule-sequence-lane={item.lane}>
      <small>{item.label}</small>
      <strong>{item.detailLabel}</strong>
      <span>{item.stateLabel}</span>
      <em>{item.tickLabel ?? `${item.objectCount} 对象`}</em>
      {item.detail ? (
        <RuleDetailButton
          detail={item.detail}
          onSelectDetail={onSelectDetail}
          selectedDetailId={selectedDetailId}
        />
      ) : null}
      <WireObjectRefChips
        className="wire-rule-sequence-object-refs"
        objects={objects}
        onInspectObject={onInspectObject}
        refs={item.refs}
        selectedObjectId={selectedObjectId}
        source="rule"
      />
    </li>
  );
}

function RuleMetric({ label, mine, value }: { label: string; mine?: boolean; value: string }) {
  return (
    <div className={mine ? "wire-rule-metric is-mine" : "wire-rule-metric"}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function RuleSection({
  children,
  emptyLabel,
  sectionKey,
  title
}: {
  children: ReactNode;
  emptyLabel: string;
  sectionKey: string;
  title: string;
}) {
  const childArray = Children.toArray(children).filter(Boolean);
  return (
    <section className="wire-rule-section" data-rule-section-key={sectionKey}>
      <h3>{title}</h3>
      {childArray.length === 0 ? <span className="empty-hint">{emptyLabel}</span> : childArray}
    </section>
  );
}

function RuleQueueItem({
  item,
  objects,
  onInspectObject,
  onSelectDetail,
  selectedDetailId,
  selectedObjectId
}: {
  item: WireRuleQueueItemPlan;
  objects: ObjectIndex;
  onInspectObject?: (objectId: string) => void;
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  selectedDetailId?: string;
  selectedObjectId?: string;
}) {
  return (
    <article
      className={selectedDetailId === item.detail.id ? "wire-rule-item is-detail-selected" : "wire-rule-item"}
      data-rule-item-key={item.key}
    >
      <div>
        <strong>{item.title}</strong>
        <span>{item.subtitle}</span>
        <RuleDetailButton detail={item.detail} onSelectDetail={onSelectDetail} selectedDetailId={selectedDetailId} />
      </div>
      {item.lines.map((line) => (
        <RuleLine key={`${line.label}-${line.value}`} label={line.label} mine={line.mine} value={line.value} />
      ))}
      <WireObjectRefChips objects={objects} onInspectObject={onInspectObject} refs={item.refs} selectedObjectId={selectedObjectId} source="rule" />
    </article>
  );
}

function RuleLine({ label, mine, value }: { label: string; mine?: boolean; value: string }) {
  return (
    <span className={mine ? "wire-rule-line is-mine" : "wire-rule-line"}>
      <span>{label}</span>
      <strong>{value || "无"}</strong>
    </span>
  );
}

function RuleDetailButton({
  detail,
  onSelectDetail,
  selectedDetailId
}: {
  detail: WireTimelineDetail;
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  selectedDetailId?: string;
}) {
  return <WireDetailTrigger detail={detail} onSelectDetail={onSelectDetail} selectedDetailId={selectedDetailId} />;
}

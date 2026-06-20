import type { ActionPromptDto, GameEvent, SnapshotDto } from "../../types/protocol";
import { Children, type ReactNode, useState } from "react";
import { buildWireRuleQueuePlan, type WireRuleQueueCoverageRow, type WireRuleQueueFocusPlan, type WireRuleQueueInspectorPlan, type WireRuleQueueItemPlan, type WireRuleQueueLane, type WireRuleQueueSequenceItem } from "../../utils/wireRuleQueuePlan";
import { buildCardObjectIndex } from "../../utils/snapshotObjectIndex";
import { StatusPill } from "../ui/StatusPill";
import { WireDetailTrigger } from "./WireDetailTrigger";
import { WireObjectRefChips, type WireObjectIndex } from "./WireObjectRefChips";
import { WireRuleAuthorityPanel } from "./WireRuleAuthorityPanel";
import type { WireTimelineDetail } from "./WireTimelineDetailPanel";

type WireRuleQueuePanelProps = {
  events?: GameEvent[];
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  onInspectObject?: (objectId: string) => void;
  playerId: string;
  prompt?: ActionPromptDto;
  selectedDetailId?: string;
  selectedObjectId?: string;
  snapshot?: SnapshotDto;
};

type ObjectIndex = WireObjectIndex;

export function WireRuleQueuePanel({ events, onInspectObject, onSelectDetail, playerId, prompt, selectedDetailId, selectedObjectId, snapshot }: WireRuleQueuePanelProps) {
  const [inspectorOpen, setInspectorOpen] = useState(false);
  const plan = buildWireRuleQueuePlan({ events, playerId, prompt, snapshot });
  const objects = buildCardObjectIndex(snapshot);

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
            <RuleLaneCard key={lane.key} lane={lane} />
          ))}
        </ol>
        <div className="wire-rule-flow-next" data-wire-rule-next-lane={plan.activeLaneKey}>
          下一步：{plan.nextStepLabel}
        </div>
        <RuleCoverageStrip coverage={plan.coverage} />
        {plan.sequence.length > 0 && (
          <ol className="wire-rule-sequence" aria-label="服务端规则队列顺序">
            {plan.sequence.map((item) => (
              <RuleSequenceItem
                item={item}
                key={item.key}
                objects={objects}
                onInspectObject={onInspectObject}
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
          open={inspectorOpen}
          plan={plan.inspector}
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
        </>
      ) : (
        <span className="empty-hint">{focus.emptyLabel}</span>
      )}
    </section>
  );
}

function RuleQueueInspector({
  objects,
  onInspectObject,
  open,
  plan,
  selectedObjectId
}: {
  objects: ObjectIndex;
  onInspectObject?: (objectId: string) => void;
  open: boolean;
  plan: WireRuleQueueInspectorPlan;
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
            <li data-rule-inspector-lane={lane.key} data-rule-inspector-lane-state={lane.state} key={lane.key}>
              <span>{lane.label}</span>
              <strong>{lane.count} 项 / {lane.stateLabel}</strong>
              <small>{lane.headline}</small>
              <small>{lane.hint}</small>
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
              <li data-rule-inspector-sequence-lane={item.laneLabel} key={item.key}>
                <span>{item.label}</span>
                <strong>{item.laneLabel} / {item.detailLabel}</strong>
                <small>{item.stateLabel} / {item.tickLabel ?? `${item.objectCount} 对象`}</small>
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

function RuleCoverageStrip({ coverage }: { coverage: WireRuleQueueCoverageRow[] }) {
  return (
    <ol className="wire-rule-coverage" aria-label="规则事件覆盖">
      {coverage.map((row) => (
        <RuleCoverageItem key={row.key} row={row} />
      ))}
    </ol>
  );
}

function RuleCoverageItem({ row }: { row: WireRuleQueueCoverageRow }) {
  return (
    <li data-rule-coverage={row.key} data-rule-coverage-state={row.state}>
      <small>{row.label}</small>
      <strong>{row.stateLabel}</strong>
      <span>快照 {row.liveCount} / 事件 {row.eventCount}</span>
      <em>{row.hint}</em>
    </li>
  );
}

function RuleLaneCard({ lane }: { lane: WireRuleQueueLane }) {
  return (
    <li data-rule-lane={lane.key} data-rule-lane-state={lane.state}>
      <small>{lane.label}</small>
      <strong>{lane.count} 项</strong>
      <span>{lane.headline}</span>
      <em>{lane.hint}</em>
    </li>
  );
}

function RuleSequenceItem({
  item,
  objects,
  onInspectObject,
  selectedObjectId
}: {
  item: WireRuleQueueSequenceItem;
  objects: ObjectIndex;
  onInspectObject?: (objectId: string) => void;
  selectedObjectId?: string;
}) {
  return (
    <li data-rule-sequence-lane={item.lane}>
      <small>{item.label}</small>
      <strong>{item.detailLabel}</strong>
      <span>{item.stateLabel}</span>
      <em>{item.tickLabel ?? `${item.objectCount} 对象`}</em>
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

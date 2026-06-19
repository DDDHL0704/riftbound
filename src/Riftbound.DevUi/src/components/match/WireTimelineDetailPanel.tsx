import type { TableObjectContext } from "../../utils/tableObjectContext";
import { buildWireTimelineDetailPlan } from "../../utils/wireTimelineDetailPlan";
import { WireObjectContextSummary } from "./WireObjectContextSummary";
import { WireObjectRefChips, type WireObjectIndex, type WireObjectRef } from "./WireObjectRefChips";

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
  detail,
  objectContextById,
  objectIndex,
  onClear,
  onInspectObject,
  selectedObjectContext,
  selectedObjectId
}: {
  detail?: WireTimelineDetail;
  objectContextById?: Record<string, TableObjectContext>;
  objectIndex: WireObjectIndex;
  onClear: () => void;
  onInspectObject?: (objectId: string) => void;
  selectedObjectContext?: TableObjectContext;
  selectedObjectId?: string;
}) {
  const plan = buildWireTimelineDetailPlan({
    detail,
    objectContextById,
    objectIndex,
    selectedObjectContext,
    selectedObjectId
  });

  return (
    <section className="wire-timeline-detail" aria-label="规则与事件详情">
      <header className="wire-timeline-detail-header">
        <div>
          <strong>{plan.headerTitle}</strong>
          <span>{plan.headerSubtitle}</span>
        </div>
        {detail && (
          <button className="wire-detail-clear" onClick={onClear} type="button">
            清除
          </button>
        )}
      </header>
      <div className="wire-timeline-detail-status-grid" aria-label="规则详情桌面投影摘要">
        {plan.statusCards.map((card) => (
          <span key={card.label}>
            <small>{card.label}</small>
            <strong>{card.value}</strong>
          </span>
        ))}
      </div>
      {detail ? (
        <>
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
                <li data-action-object-id={row.objectId} key={row.key}>
                  <span>{row.role}</span>
                  <strong>{row.label}</strong>
                  <small>{row.stateLabel}</small>
                  <small>{row.zoneLabel}</small>
                  <small>{row.commandTypes.length > 0 ? row.commandTypes.join(" / ") : "服务端候选"}</small>
                  {row.reasonLabels.length > 0 && <small>{row.reasonLabels.slice(0, 2).join(" / ")}</small>}
                </li>
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
    </section>
  );
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

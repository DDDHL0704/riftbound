import type { ErrorDto, GameEvent } from "../../types/protocol";
import { buildEventLogPlan, type LogDensity } from "../../utils/eventLogPlan";
import { WireDetailTrigger } from "./WireDetailTrigger";
import { WireObjectRefChips, type WireObjectIndex } from "./WireObjectRefChips";
import type { WireTimelineDetail } from "./WireTimelineDetailPanel";

export { eventDescriptionLabel, eventKindLabel } from "../../utils/eventLogPlan";
export type { LogDensity } from "../../utils/eventLogPlan";

export function EventLog({
  density = "standard",
  errors,
  events,
  objectIndex = {},
  onInspectObject,
  onSelectDetail,
  selectedDetailId,
  selectedObjectId
}: {
  density?: LogDensity;
  errors: ErrorDto[];
  events: GameEvent[];
  objectIndex?: WireObjectIndex;
  onInspectObject?: (objectId: string) => void;
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  selectedDetailId?: string;
  selectedObjectId?: string;
}) {
  const plan = buildEventLogPlan({ density, errors, events, objectIndex });

  return (
    <section
      className={`side-panel event-log event-log-${plan.density}`}
      data-event-log-error-count={plan.errorCount}
      data-event-log-hidden-count={plan.hiddenEventCount}
      data-event-log-hidden-ref-count={plan.collapsedSummary?.hiddenRefCount ?? 0}
      data-event-log-missing-ref-count={plan.collapsedSummary?.missingRefCount ?? 0}
      data-event-log-server-ref-count={plan.collapsedSummary?.serverRefCount ?? 0}
      data-event-log-state={plan.state}
      data-event-log-visible-count={plan.visibleEventCount}
    >
      <header>
        <span className="eyebrow">服务端日志</span>
        <h2>事件 / 错误</h2>
      </header>
      {plan.hiddenEventCount > 0 && <span className="empty-hint">简洁模式显示最近 {plan.visibleEventCount} 条服务端事件。</span>}
      {plan.collapsedSummary && (
        <span className="empty-hint">
          已折叠 {plan.collapsedSummary.eventCount} 条，服务端对象 {plan.collapsedSummary.serverRefCount} 项，隐藏 {plan.collapsedSummary.hiddenRefCount} 项，缺失 {plan.collapsedSummary.missingRefCount} 项。
        </span>
      )}
      {plan.errors.map((error) => (
        <article className="log-row log-error" data-event-log-row-kind="error" key={error.key}>
          <strong>{error.title}</strong>
          <span>{error.message}</span>
        </article>
      ))}
      {plan.emptyLabel && <span className="empty-hint">{plan.emptyLabel}</span>}
      {plan.events.map((event) => (
        <article
          className={selectedDetailId === event.detail.id ? "log-row is-detail-selected" : "log-row"}
          data-event-log-row-kind={event.kind}
          data-event-log-row-ref-count={event.refs.length}
          key={event.key}
        >
          <div className="log-row-heading">
            <strong>{event.title}</strong>
            <WireDetailTrigger detail={event.detail} onSelectDetail={onSelectDetail} selectedDetailId={selectedDetailId} />
          </div>
          <span>{event.description}</span>
          <WireObjectRefChips
            className="log-object-refs"
            objects={objectIndex}
            onInspectObject={onInspectObject}
            refs={event.refs}
            selectedObjectId={selectedObjectId}
            source="event"
          />
        </article>
      ))}
    </section>
  );
}

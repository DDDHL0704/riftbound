import type {
  CommandSubmissionFollowupMetric,
  CommandSubmissionFollowupPlan
} from "../../utils/commandSubmissionFollowupPlan";
import {
  buildWireCommandFollowupLayoutProjectionPlan,
  type WireCommandFollowupLayoutProjectionPlan
} from "./wireCommandFollowupLayoutProjectionPlan";
import type { WireTableViewModel } from "./wireTableViewModel";

export function WireCommandFollowupPanel({
  ariaLabel = "服务端后续事件",
  className = "wire-command-followup",
  onInspectObject,
  plan,
  table
}: {
  ariaLabel?: string;
  className?: string;
  onInspectObject?: (objectId: string) => void;
  plan: CommandSubmissionFollowupPlan;
  table?: WireTableViewModel;
}) {
  const layoutProjection = table
    ? buildWireCommandFollowupLayoutProjectionPlan({ plan, table })
    : undefined;

  return (
    <section
      aria-label={ariaLabel}
      className={className}
      data-command-followup-event-count={plan.events.length}
      data-command-followup-hidden-count={plan.hiddenEventCount}
      data-command-followup-server-state={plan.serverFollowupState}
      data-command-followup-state={plan.state}
    >
      <div
        className="wire-command-followup-bridge"
        data-command-followup-bridge-server-state={plan.serverFollowupState}
        data-command-followup-bridge-state={plan.bridge.state}
      >
        <div className="wire-command-followup-bridge-heading">
          <strong>{plan.bridge.headline}</strong>
          <span>{plan.bridge.stateLabel}</span>
          <small>{plan.bridge.serverStateLabel}</small>
        </div>
        <p>{plan.bridge.summary}</p>
        <strong className="wire-command-followup-bridge-next">下一步：{plan.bridge.nextStepLabel}</strong>
        <ol className="wire-command-followup-bridge-rows" aria-label="提交后续桥接状态">
          {plan.bridge.rows.map((row) => (
            <li
              data-command-followup-bridge-row={row.key}
              data-command-followup-bridge-row-state={row.state}
              key={row.key}
            >
              <span>{row.label}</span>
              <strong>{row.value}</strong>
              <small>{row.stateLabel}</small>
            </li>
          ))}
        </ol>
      </div>
      <div className="wire-command-followup-heading">
        <strong>后续事件</strong>
        <span>{plan.summary}</span>
      </div>
      {plan.uiSource && (
        <div
          aria-label="提交界面来源"
          className="wire-command-followup-source"
          data-command-followup-source-detail={plan.uiSource.detailId ?? ""}
          data-command-followup-source-object={plan.uiSource.objectId ?? ""}
          data-command-followup-source-surface={plan.uiSource.surface}
        >
          <small>提交入口</small>
          <strong>{plan.uiSource.label}</strong>
        </div>
      )}
      {layoutProjection && (
        <CommandFollowupLayoutProjectionPanel onInspectObject={onInspectObject} plan={layoutProjection} />
      )}
      <div className="wire-command-followup-metrics">
        {plan.metrics.map((metric) => <CommandSubmissionFollowupMetricCell key={metric.key} metric={metric} />)}
      </div>
      {plan.events.length === 0 ? (
        <span className="empty-hint">当前没有同 tick 的公开事件。</span>
      ) : (
        <ol className="wire-command-followup-events" aria-label="同 tick 服务端事件">
          {plan.events.map((event) => (
            <li data-command-followup-event-kind={event.kind} key={event.key}>
              <div>
                <strong>{event.title}</strong>
                <span>{event.messageType ?? "EVENTS"}</span>
              </div>
              <small>{event.description}</small>
              <em>{event.refCount} 引用</em>
              {event.refs.length > 0 ? (
                <div className="wire-command-followup-refs" aria-label={`${event.title} 对象引用`}>
                  {event.refs.map((ref) => (
                    <button
                      data-command-followup-ref-state={ref.hidden ? "hidden" : "public"}
                      disabled={ref.hidden || !ref.objectId || !onInspectObject}
                      key={ref.key}
                      onClick={() => {
                        if (ref.objectId) {
                          onInspectObject?.(ref.objectId);
                        }
                      }}
                      title={ref.hidden ? "隐藏对象不会暴露身份" : `检查 ${ref.objectId}`}
                      type="button"
                    >
                      {ref.label}
                    </button>
                  ))}
                </div>
              ) : null}
            </li>
          ))}
        </ol>
      )}
      {plan.hiddenEventCount > 0 && <small>另有 {plan.hiddenEventCount} 条同 tick 事件。</small>}
    </section>
  );
}

function CommandFollowupLayoutProjectionPanel({
  onInspectObject,
  plan
}: {
  onInspectObject?: (objectId: string) => void;
  plan: WireCommandFollowupLayoutProjectionPlan;
}) {
  return (
    <section
      aria-label="回执对象桌面投影"
      className="wire-command-followup-layout-projection"
      data-command-followup-layout-hidden-count={plan.hiddenRefCount}
      data-command-followup-layout-located-count={plan.locatedCount}
      data-command-followup-layout-public-count={plan.publicRefCount}
      data-command-followup-layout-state={plan.state}
      data-command-followup-layout-total-count={plan.totalRefCount}
      data-command-followup-layout-unknown-count={plan.unknownCount}
    >
      <div className="wire-command-followup-layout-heading">
        <strong>回执桌面投影</strong>
        <span>{plan.stateLabel}</span>
      </div>
      <small>{plan.summary}</small>
      {plan.rows.length === 0 ? (
        <span className="empty-hint">
          {plan.hiddenRefCount > 0 ? "仅有隐藏引用，不暴露对象身份。" : "没有公开引用。"}
        </span>
      ) : (
        <ol aria-label="回执公开对象区域投影">
          {plan.rows.map((row) => (
            <li
              data-command-followup-layout-capacity-row={row.capacityRowKey ?? ""}
              data-command-followup-layout-event-kind={row.eventKind}
              data-command-followup-layout-kind={row.layoutKind}
              data-command-followup-layout-object={row.objectId}
              data-command-followup-layout-role={row.refRole}
              data-command-followup-layout-row=""
              data-command-followup-layout-state={row.state}
              data-command-followup-layout-zone={row.zoneKey ?? ""}
              key={row.key}
            >
              <button
                disabled={!onInspectObject}
                onClick={() => onInspectObject?.(row.objectId)}
                title={`检查 ${row.objectId}`}
                type="button"
              >
                <strong>{row.zoneLabel}</strong>
                <span>{row.refRole} · {row.eventTitle}</span>
                <small>{row.objectLabel}</small>
              </button>
            </li>
          ))}
        </ol>
      )}
      {plan.overflowCount > 0 && <small>另有 {plan.overflowCount} 个公开引用未展开。</small>}
    </section>
  );
}

function CommandSubmissionFollowupMetricCell({ metric }: { metric: CommandSubmissionFollowupMetric }) {
  return (
    <span data-command-followup-metric={metric.key} data-command-followup-metric-state={metric.state}>
      <b>{metric.label}</b>
      <strong>{metric.value}</strong>
    </span>
  );
}

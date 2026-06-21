import type {
  CommandSubmissionFollowupMetric,
  CommandSubmissionFollowupPlan
} from "../../utils/commandSubmissionFollowupPlan";

export function WireCommandFollowupPanel({
  ariaLabel = "服务端后续事件",
  className = "wire-command-followup",
  onInspectObject,
  plan
}: {
  ariaLabel?: string;
  className?: string;
  onInspectObject?: (objectId: string) => void;
  plan: CommandSubmissionFollowupPlan;
}) {
  return (
    <section
      aria-label={ariaLabel}
      className={className}
      data-command-followup-event-count={plan.events.length}
      data-command-followup-hidden-count={plan.hiddenEventCount}
      data-command-followup-server-state={plan.serverFollowupState}
      data-command-followup-state={plan.state}
    >
      <div className="wire-command-followup-heading">
        <strong>后续事件</strong>
        <span>{plan.summary}</span>
      </div>
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

function CommandSubmissionFollowupMetricCell({ metric }: { metric: CommandSubmissionFollowupMetric }) {
  return (
    <span data-command-followup-metric={metric.key} data-command-followup-metric-state={metric.state}>
      <b>{metric.label}</b>
      <strong>{metric.value}</strong>
    </span>
  );
}

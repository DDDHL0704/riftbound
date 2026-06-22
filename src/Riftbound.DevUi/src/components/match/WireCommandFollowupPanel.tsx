import type { ActionPromptContractDto } from "../../types/protocol";
import type {
  CommandSubmissionFollowupEventRow,
  CommandSubmissionFollowupMetric,
  CommandSubmissionFollowupPlan,
  CommandSubmissionFollowupServerEventKind
} from "../../utils/commandSubmissionFollowupPlan";
import type { TableObjectContext } from "../../utils/tableObjectContext";
import { WireObjectInspectionSummary } from "./WireObjectInspectionSummary";
import {
  buildWireCommandFollowupLayoutProjectionPlan,
  type WireCommandFollowupLayoutProjectionPlan
} from "./wireCommandFollowupLayoutProjectionPlan";
import type { WireTableViewModel } from "./wireTableViewModel";

export function WireCommandFollowupPanel({
  ariaLabel = "服务端后续事件",
  className = "wire-command-followup",
  contract,
  objectContextById,
  onInspectObject,
  onSelectFollowupEvent,
  onSelectServerEventKind,
  plan,
  selectedObjectId,
  table
}: {
  ariaLabel?: string;
  className?: string;
  contract?: ActionPromptContractDto | null;
  objectContextById?: Record<string, TableObjectContext>;
  onInspectObject?: (objectId: string) => void;
  onSelectFollowupEvent?: (event: CommandSubmissionFollowupEventRow) => void;
  onSelectServerEventKind?: (eventKind: CommandSubmissionFollowupServerEventKind) => void;
  plan: CommandSubmissionFollowupPlan;
  selectedObjectId?: string;
  table?: WireTableViewModel;
}) {
  const layoutProjection = table
    ? buildWireCommandFollowupLayoutProjectionPlan({ plan, table })
    : undefined;
  const inspectionObjectId = chooseCommandFollowupInspectionObjectId({
    layoutProjection,
    objectContextById,
    selectedObjectId
  });
  const inspectionContext = inspectionObjectId ? objectContextById?.[inspectionObjectId] : undefined;

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
      {plan.sourceRows.length > 0 && (
        <div
          aria-label="提交界面来源"
          className="wire-command-followup-source"
          data-command-followup-source-candidate-action={plan.uiSource?.candidateAction ?? ""}
          data-command-followup-source-candidate-label={plan.uiSource?.candidateLabel ?? ""}
          data-command-followup-source-command-source={plan.uiSource?.commandSource ?? ""}
          data-command-followup-source-command-source-label={plan.uiSource?.commandSourceLabel ?? ""}
          data-command-followup-source-detail={plan.uiSource?.detailId ?? ""}
          data-command-followup-source-object={plan.uiSource?.objectId ?? ""}
          data-command-followup-source-surface={plan.uiSource?.surface ?? ""}
        >
          <small>提交入口</small>
          <ol aria-label="提交来源字段">
            {plan.sourceRows.map((row) => (
              <li
                data-command-followup-source-row={row.key}
                data-command-followup-source-row-state={row.state}
                key={row.key}
              >
                <span>{row.label}</span>
                <strong>{row.value}</strong>
                {row.detail && <small>{row.detail}</small>}
              </li>
            ))}
          </ol>
        </div>
      )}
      {plan.serverEventKinds.length > 0 && (
        <ol className="wire-command-followup-server-kinds" aria-label="服务端回执事件种类">
          {plan.serverEventKinds.map((eventKind) => {
            const linked = plan.events.some((event) => {
              if (event.kind !== eventKind.kind) {
                return false;
              }

              return eventKind.source === "kind"
                || (event.serverTick === eventKind.serverTick && event.order === eventKind.order);
            });
            return (
              <li
                data-command-followup-server-event-kind={eventKind.kind}
                data-command-followup-server-event-kind-source={eventKind.source}
                data-command-followup-server-event-kind-state={linked ? "linked" : "declared"}
                data-command-followup-server-event-order={eventKind.order ?? ""}
                data-command-followup-server-event-tick={eventKind.serverTick ?? ""}
                key={eventKind.key}
              >
                <button
                  data-command-followup-server-event-kind-action={eventKind.kind}
                  data-command-followup-server-event-order-action={eventKind.order ?? ""}
                  data-command-followup-server-event-tick-action={eventKind.serverTick ?? ""}
                  disabled={!onSelectServerEventKind}
                  onClick={() => onSelectServerEventKind?.(eventKind)}
                  type="button"
                >
                  <span>{eventKind.label}</span>
                  <small>{eventKind.source === "event-ref" ? `${eventKind.kind} #${(eventKind.order ?? 0) + 1}` : eventKind.kind}</small>
                </button>
              </li>
            );
          })}
        </ol>
      )}
      {layoutProjection && (
        <CommandFollowupLayoutProjectionPanel onInspectObject={onInspectObject} plan={layoutProjection} />
      )}
      <CommandFollowupObjectInspectionSummary
        context={inspectionContext}
        contract={contract}
        objectId={inspectionObjectId}
        plan={layoutProjection}
      />
      <div className="wire-command-followup-metrics">
        {plan.metrics.map((metric) => <CommandSubmissionFollowupMetricCell key={metric.key} metric={metric} />)}
      </div>
      {plan.events.length === 0 ? (
        <span className="empty-hint">当前没有同 tick 的公开事件。</span>
      ) : (
        <ol className="wire-command-followup-events" aria-label="同 tick 服务端事件">
          {plan.events.map((event) => (
            <li
              data-command-followup-event-kind={event.kind}
              data-command-followup-event-order={event.order ?? ""}
              data-command-followup-event-tick={event.serverTick ?? ""}
              key={event.key}
            >
              <div>
                <button
                  className="wire-command-followup-event-open"
                  data-command-followup-event-action={event.kind}
                  data-command-followup-event-order-action={event.order ?? ""}
                  data-command-followup-event-tick-action={event.serverTick ?? ""}
                  disabled={!onSelectFollowupEvent}
                  onClick={() => onSelectFollowupEvent?.(event)}
                  type="button"
                >
                  <strong>{event.title}</strong>
                </button>
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

function chooseCommandFollowupInspectionObjectId({
  layoutProjection,
  objectContextById,
  selectedObjectId
}: {
  layoutProjection?: WireCommandFollowupLayoutProjectionPlan;
  objectContextById?: Record<string, TableObjectContext>;
  selectedObjectId?: string;
}): string | undefined {
  if (!layoutProjection || layoutProjection.rows.length === 0) {
    return undefined;
  }

  if (selectedObjectId) {
    const selectedRow = layoutProjection.rows.find((row) => row.objectId === selectedObjectId);
    if (selectedRow && objectContextById?.[selectedRow.objectId]) {
      return selectedRow.objectId;
    }
  }

  return layoutProjection.rows.find((row) => objectContextById?.[row.objectId])?.objectId
    ?? layoutProjection.rows[0]?.objectId;
}

function CommandFollowupObjectInspectionSummary({
  context,
  contract,
  objectId,
  plan
}: {
  context?: TableObjectContext;
  contract?: ActionPromptContractDto | null;
  objectId?: string;
  plan?: WireCommandFollowupLayoutProjectionPlan;
}) {
  if (!plan || plan.rows.length === 0) {
    return null;
  }

  return (
    <section
      aria-label="回执对象检查摘要"
      className="wire-command-followup-object-inspection"
      data-command-followup-selected-inspection-object={objectId ?? ""}
      data-command-followup-selected-inspection-row-count={plan.rows.length}
      data-command-followup-selected-inspection-state={context ? "ready" : "missing-context"}
    >
      <div className="wire-command-followup-object-inspection-heading">
        <strong>回执对象检查</strong>
        <span>{context ? "已接入对象上下文" : "公开引用未建立对象上下文"}</span>
      </div>
      {context ? (
        <WireObjectInspectionSummary context={context} contract={contract} />
      ) : (
        <span className="empty-hint">回执公开引用存在，但当前快照或提示未提供可检查对象上下文。</span>
      )}
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

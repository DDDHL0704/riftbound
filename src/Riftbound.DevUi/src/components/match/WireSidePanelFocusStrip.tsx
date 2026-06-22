import { Maximize2, X } from "lucide-react";
import type { InspectedCard } from "../cards/CardFace";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";
import type { WireTimelineDetail } from "./WireTimelineDetailPanel";
import type { WireSidePanelSlot } from "./wireTableLayout";
import type { WireSidePanelFocusPlan } from "../../utils/wireSidePanelFocusPlan";

type WireSidePanelFocusStripProps = {
  inspectedCard?: InspectedCard;
  onClear: () => void;
  onOpenDetail: (card: InspectedCard) => void;
  onSelectDetail: (detail: WireTimelineDetail) => void;
  onSelectSlot: (slot: WireSidePanelSlot) => void;
  plan: WireSidePanelFocusPlan;
};

export function WireSidePanelFocusStrip({
  inspectedCard,
  onClear,
  onOpenDetail,
  onSelectDetail,
  onSelectSlot,
  plan
}: WireSidePanelFocusStripProps) {
  return (
    <section
      aria-label="右侧焦点对象摘要"
      className="wire-side-panel-focus-strip"
      data-wire-side-panel-focus-event-count={plan.eventCount}
      data-wire-side-panel-focus-object={plan.objectId ?? ""}
      data-wire-side-panel-focus-relation-count={plan.relationCount}
      data-wire-side-panel-focus-state={plan.state}
      data-wire-side-panel-focus-visible={plan.visible ? "true" : "false"}
      tabIndex={0}
    >
      <header>
        <div>
          <small>焦点</small>
          <strong>{plan.title}</strong>
          <span>{plan.subtitle}</span>
        </div>
        <StatusPill tone={plan.tone}>{plan.stateLabel}</StatusPill>
      </header>

      {plan.metrics.length > 0 && (
        <dl className="wire-side-panel-focus-metrics" aria-label="焦点对象命令摘要">
          {plan.metrics.map((metric) => (
            <div data-wire-side-panel-focus-metric={metric.key} key={metric.key}>
              <dt>{metric.label}</dt>
              <dd>{metric.value}</dd>
            </div>
          ))}
        </dl>
      )}

      {plan.contextMetrics.length > 0 && (
        <dl className="wire-side-panel-focus-context" aria-label="焦点对象规则上下文">
          {plan.contextMetrics.map((metric) => (
            <div data-wire-side-panel-focus-context-metric={metric.key} key={metric.key}>
              <dt>{metric.label}</dt>
              <dd>{metric.value}</dd>
            </div>
          ))}
        </dl>
      )}

      {plan.relations.length > 0 && (
        <ol className="wire-side-panel-focus-relations" aria-label="焦点对象规则关联">
          {plan.relations.map((relation) => (
            <li
              data-wire-side-panel-focus-relation={relation.key}
              data-wire-side-panel-focus-relation-source={relation.sourceLabel}
              data-wire-side-panel-focus-relation-state={relation.state}
              key={relation.key}
            >
              {relation.detail ? (
                <button
                  data-wire-side-panel-focus-relation-detail-id={relation.detail.id}
                  onClick={() => {
                    onSelectDetail(relation.detail!);
                    onSelectSlot("timelineDetail");
                  }}
                  type="button"
                >
                  <span>{relation.sourceLabel}</span>
                  <strong>{relation.label}</strong>
                  <small>{relation.value}</small>
                  <em>{relation.stateLabel}</em>
                </button>
              ) : (
                <div data-wire-side-panel-focus-relation-detail-id="">
                  <span>{relation.sourceLabel}</span>
                  <strong>{relation.label}</strong>
                  <small>{relation.value}</small>
                  <em>{relation.stateLabel}</em>
                </div>
              )}
            </li>
          ))}
        </ol>
      )}

      <div className="wire-side-panel-focus-next">
        <small>下一步</small>
        <strong>{plan.nextStepLabel}</strong>
      </div>

      <div className="wire-side-panel-focus-routes" aria-label="焦点对象导航">
        {plan.routes.map((route) => {
          const slot = route.slot;
          return slot ? (
            <button
              aria-label={`${route.label}：${route.actionLabel}，${route.stateLabel}。${route.reason}`}
              data-wire-side-panel-focus-route-action-label={route.actionLabel}
              data-wire-side-panel-focus-route-reason={route.reason}
              data-wire-side-panel-focus-route={route.key}
              data-wire-side-panel-focus-route-state-label={route.stateLabel}
              data-wire-side-panel-focus-route-state={route.state}
              data-wire-side-panel-focus-route-target-kind={route.targetKind}
              data-wire-side-panel-focus-route-target-label={route.targetLabel}
              data-wire-side-panel-focus-route-target-slot={slot}
              disabled={route.state === "disabled"}
              key={route.key}
              onClick={() => onSelectSlot(slot)}
              title={route.reason}
              type="button"
            >
              <span>{route.label}</span>
              <strong>{route.actionLabel}</strong>
              <small>{route.stateLabel}</small>
            </button>
          ) : (
            <button
              aria-label={`${route.label}：${route.actionLabel}，${route.stateLabel}。${route.reason}`}
              data-wire-side-panel-focus-route-action-label={route.actionLabel}
              data-wire-side-panel-focus-route-reason={route.reason}
              data-wire-side-panel-focus-route={route.key}
              data-wire-side-panel-focus-route-state-label={route.stateLabel}
              data-wire-side-panel-focus-route-state={route.state}
              data-wire-side-panel-focus-route-target-kind={route.targetKind}
              data-wire-side-panel-focus-route-target-label={route.targetLabel}
              data-wire-side-panel-focus-route-target-slot=""
              disabled={route.state === "disabled" || !inspectedCard}
              key={route.key}
              onClick={() => {
                if (inspectedCard) {
                  onOpenDetail(inspectedCard);
                }
              }}
              title={route.reason}
              type="button"
            >
              <span>{route.label}</span>
              <strong>{route.actionLabel}</strong>
              <small>{route.stateLabel}</small>
            </button>
          );
        })}
        <Button disabled={!inspectedCard} icon={<Maximize2 size={14} />} onClick={() => inspectedCard && onOpenDetail(inspectedCard)} variant="secondary">大图</Button>
        <Button disabled={!plan.visible} icon={<X size={14} />} onClick={onClear} variant="ghost">清除</Button>
      </div>
    </section>
  );
}

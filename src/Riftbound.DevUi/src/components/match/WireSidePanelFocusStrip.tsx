import { Maximize2, X } from "lucide-react";
import type { InspectedCard } from "../cards/CardFace";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";
import type { WireSidePanelSlot } from "./wireTableLayout";
import type { WireSidePanelFocusPlan } from "../../utils/wireSidePanelFocusPlan";

type WireSidePanelFocusStripProps = {
  inspectedCard?: InspectedCard;
  onClear: () => void;
  onOpenDetail: (card: InspectedCard) => void;
  onSelectSlot: (slot: WireSidePanelSlot) => void;
  plan: WireSidePanelFocusPlan;
};

export function WireSidePanelFocusStrip({
  inspectedCard,
  onClear,
  onOpenDetail,
  onSelectSlot,
  plan
}: WireSidePanelFocusStripProps) {
  return (
    <section
      aria-label="右侧焦点对象摘要"
      className="wire-side-panel-focus-strip"
      data-wire-side-panel-focus-object={plan.objectId ?? ""}
      data-wire-side-panel-focus-state={plan.state}
      data-wire-side-panel-focus-visible={plan.visible ? "true" : "false"}
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

      <div className="wire-side-panel-focus-next">
        <small>下一步</small>
        <strong>{plan.nextStepLabel}</strong>
      </div>

      <div className="wire-side-panel-focus-routes" aria-label="焦点对象导航">
        {plan.routes.map((route) => {
          const slot = route.slot;
          return slot ? (
            <button
              data-wire-side-panel-focus-route={route.key}
              data-wire-side-panel-focus-route-state={route.state}
              disabled={route.state === "disabled"}
              key={route.key}
              onClick={() => onSelectSlot(slot)}
              type="button"
            >
              {route.label}
            </button>
          ) : (
            <button
              data-wire-side-panel-focus-route={route.key}
              data-wire-side-panel-focus-route-state={route.state}
              disabled={route.state === "disabled" || !inspectedCard}
              key={route.key}
              onClick={() => {
                if (inspectedCard) {
                  onOpenDetail(inspectedCard);
                }
              }}
              type="button"
            >
              {route.label}
            </button>
          );
        })}
        <Button disabled={!inspectedCard} icon={<Maximize2 size={14} />} onClick={() => inspectedCard && onOpenDetail(inspectedCard)} variant="secondary">大图</Button>
        <Button disabled={!plan.visible} icon={<X size={14} />} onClick={onClear} variant="ghost">清除</Button>
      </div>
    </section>
  );
}

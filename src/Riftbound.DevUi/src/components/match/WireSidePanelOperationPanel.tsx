import type {
  WireSidePanelOperationPlan,
  WireSidePanelOperationSection,
  WireSidePanelOperationState
} from "../../utils/wireSidePanelOperationPlan";
import { StatusPill } from "../ui/StatusPill";
import type { WireSidePanelSlot } from "./wireTableLayout";

export function WireSidePanelOperationPanel({
  activeSlot,
  onSelectSlot,
  plan
}: {
  activeSlot: WireSidePanelSlot;
  onSelectSlot: (slot: WireSidePanelSlot) => void;
  plan: WireSidePanelOperationPlan;
}) {
  return (
    <section
      aria-label="右侧规则操作面板"
      className="wire-side-panel-operation"
      data-wire-side-panel-operation-active={plan.activeSectionKey}
      data-wire-side-panel-operation-issue-count={plan.issueCount}
      data-wire-side-panel-operation-ready-count={plan.readyCount}
      data-wire-side-panel-operation-state={plan.state}
    >
      <header className="wire-side-panel-operation-header">
        <div>
          <strong>规则操作</strong>
          <span>{plan.summary}</span>
        </div>
        <StatusPill tone={operationTone(plan.state)}>{operationStateLabel(plan.state)}</StatusPill>
      </header>

      <ol className="wire-side-panel-operation-sections" aria-label="规则操作分区">
        {plan.sections.map((section) => (
          <OperationSection
            activeSlot={activeSlot}
            key={section.key}
            onSelectSlot={onSelectSlot}
            section={section}
          />
        ))}
      </ol>
    </section>
  );
}

function OperationSection({
  activeSlot,
  onSelectSlot,
  section
}: {
  activeSlot: WireSidePanelSlot;
  onSelectSlot: (slot: WireSidePanelSlot) => void;
  section: WireSidePanelOperationSection;
}) {
  const active = section.primarySlot === activeSlot || section.routes.some((route) => route.slot === activeSlot);

  return (
    <li
      data-wire-side-panel-operation-section={section.key}
      data-wire-side-panel-operation-section-active={active}
      data-wire-side-panel-operation-section-count={section.count}
      data-wire-side-panel-operation-section-state={section.state}
    >
      <button
        className="wire-side-panel-operation-section-main"
        data-wire-side-panel-operation-section-primary={section.primarySlot}
        onClick={() => onSelectSlot(section.primarySlot)}
        type="button"
      >
        <span>{section.label}</span>
        <strong>{section.title}</strong>
        <small>{section.stateLabel} / {section.count}</small>
        <em>{section.summary}</em>
      </button>
      <div className="wire-side-panel-operation-routes" role="group" aria-label={`${section.label}入口`}>
        {section.routes.map((route) => (
          <button
            data-wire-side-panel-operation-route={route.key}
            data-wire-side-panel-operation-route-state={route.state}
            data-wire-side-panel-operation-route-slot={route.slot}
            disabled={route.state === "disabled"}
            key={route.key}
            onClick={() => onSelectSlot(route.slot)}
            type="button"
          >
            {route.label}
          </button>
        ))}
      </div>
    </li>
  );
}

function operationStateLabel(state: WireSidePanelOperationState): string {
  switch (state) {
    case "active":
      return "进行中";
    case "blocked":
      return "有阻断";
    case "empty":
      return "空";
    case "ready":
      return "可操作";
    case "review":
      return "复核";
    case "waiting":
      return "等待";
  }
}

function operationTone(state: WireSidePanelOperationState): "bad" | "good" | "info" | "neutral" | "warn" {
  switch (state) {
    case "blocked":
      return "warn";
    case "active":
    case "ready":
      return "good";
    case "review":
      return "info";
    case "empty":
    case "waiting":
      return "neutral";
  }
}

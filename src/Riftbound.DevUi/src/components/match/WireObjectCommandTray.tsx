import { Maximize2, X } from "lucide-react";
import type { InspectedCard } from "../cards/CardFace";
import type { ActionPromptDto, GameCommand, SnapshotDto } from "../../types/protocol";
import type { ServerSubmissionGatePlan } from "../../utils/serverSubmissionGatePlan";
import type { TableObjectContext } from "../../utils/tableObjectContext";
import {
  buildWireObjectCommandTrayPlan,
  type WireObjectCommandTrayPlan
} from "../../utils/wireObjectCommandTrayPlan";
import type { WireFocusedInteractionPlan } from "../../utils/wireFocusedInteractionPlan";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";
import { WireFocusedActionEntryList } from "./WireFocusedActionEntryList";
import { WireObjectRouteReview } from "./WireObjectRouteReview";

type WireObjectCommandTrayProps = {
  disabledByConnection: boolean;
  focusedPlan: WireFocusedInteractionPlan;
  inspectedCard?: InspectedCard;
  objectContext?: TableObjectContext;
  onClear: () => void;
  onCommand?: (command: GameCommand) => void;
  onOpenDetail: (card: InspectedCard) => void;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
  submissionGate?: ServerSubmissionGatePlan;
};

export function WireObjectCommandTray({
  disabledByConnection,
  focusedPlan,
  inspectedCard,
  objectContext,
  onClear,
  onCommand,
  onOpenDetail,
  prompt,
  snapshot,
  submissionGate
}: WireObjectCommandTrayProps) {
  const trayPlan = buildWireObjectCommandTrayPlan({
    card: inspectedCard,
    focusedPlan,
    objectContext
  });

  if (!trayPlan.visible || !inspectedCard) {
    return null;
  }

  return (
    <section
      aria-label="桌面焦点操作托盘"
      className={`wire-object-command-tray is-${trayPlan.state}`}
      data-wire-object-command-tray-object={trayPlan.objectId ?? ""}
      data-wire-object-command-tray-state={trayPlan.state}
      data-wire-object-command-tray-visible={trayPlan.visible ? "true" : "false"}
    >
      <div className="wire-object-command-tray-main">
        <header className="wire-object-command-tray-heading">
          <div>
            <strong>{trayPlan.title}</strong>
            <span>{trayPlan.subtitle}</span>
          </div>
          <StatusPill tone={trayPlan.tone}>{trayPlan.stateLabel}</StatusPill>
        </header>
        <MetricGrid plan={trayPlan} />
        <SemanticRows plan={trayPlan} />
        <SelectionRows plan={focusedPlan} />
        <strong className="wire-object-command-tray-next">{trayPlan.nextStepLabel}</strong>
        <ContextRows plan={trayPlan} />
        <WireObjectRouteReview
          className="wire-object-command-tray-route"
          onCommand={onCommand}
          review={focusedPlan.commandReview}
          route={focusedPlan.route}
        />
      </div>

      {trayPlan.canShowActions ? (
        <WireFocusedActionEntryList
          className="wire-object-command-tray-actions"
          dataAttributes={{
            count: "data-wire-object-command-tray-action-count",
            entry: "data-wire-object-command-tray-action",
            mode: "data-wire-object-command-tray-action-mode"
          }}
          disabledByConnection={disabledByConnection}
          entryClassName="wire-object-command-tray-action"
          maxEntries={trayPlan.actionLimit}
          onCommand={onCommand}
          plan={focusedPlan}
          prompt={prompt}
          snapshot={snapshot}
          submissionGate={submissionGate}
        />
      ) : (
        <span className="wire-object-command-tray-empty">{trayPlan.primaryLabel}</span>
      )}

      <div className="wire-object-command-tray-buttons">
        <Button icon={<Maximize2 size={16} />} onClick={() => onOpenDetail(inspectedCard)} variant="secondary">详情</Button>
        <Button icon={<X size={16} />} onClick={onClear} variant="ghost">清除</Button>
      </div>
    </section>
  );
}

function SelectionRows({ plan }: { plan: WireFocusedInteractionPlan }) {
  if (plan.selectionRows.length === 0) {
    return null;
  }

  return (
    <ol className="wire-object-command-tray-selection" aria-label="桌面点选草稿明细">
      {plan.selectionRows.map((row) => (
        <li
          data-wire-object-command-tray-selection={row.role}
          data-wire-object-command-tray-selection-choice={row.choiceId}
          data-wire-object-command-tray-selection-object-ids={row.objectIds.join("|")}
          key={row.key}
        >
          <span>{row.roleLabel}</span>
          <strong>{row.label}</strong>
          <small>{row.objectLabel}</small>
        </li>
      ))}
    </ol>
  );
}

function SemanticRows({ plan }: { plan: WireObjectCommandTrayPlan }) {
  if (plan.semanticRows.length === 0) {
    return null;
  }

  return (
    <div className="wire-object-command-tray-semantics" aria-label="焦点对象服务端动作语义">
      {plan.semanticRows.map((row) => (
        <span
          data-wire-object-command-tray-semantic-category={row.category}
          data-wire-object-command-tray-semantic-intent={row.intent}
          data-wire-object-command-tray-semantic-priority={row.priority}
          data-wire-object-command-tray-semantic-ui-hint={row.uiHint}
          key={row.key}
        >
          <small>{row.category}</small>
          <strong>{row.intent}</strong>
          <small>{row.count}</small>
        </span>
      ))}
    </div>
  );
}

function ContextRows({ plan }: { plan: WireObjectCommandTrayPlan }) {
  if (plan.contextRows.length === 0) {
    return null;
  }

  return (
    <dl className="wire-object-command-tray-context" aria-label="焦点对象服务端上下文">
      {plan.contextRows.map((row) => (
        <div data-wire-object-command-tray-context={row.key} data-wire-object-command-tray-context-tone={row.tone} key={row.key}>
          <dt>{row.label}</dt>
          <dd>{row.value}</dd>
        </div>
      ))}
    </dl>
  );
}

function MetricGrid({ plan }: { plan: WireObjectCommandTrayPlan }) {
  return (
    <div className="wire-object-command-tray-metrics">
      {plan.metrics.map((metric) => (
        <span data-wire-object-command-tray-metric={metric.key} key={metric.key}>
          <small>{metric.label}</small>
          <strong>{metric.value}</strong>
        </span>
      ))}
    </div>
  );
}

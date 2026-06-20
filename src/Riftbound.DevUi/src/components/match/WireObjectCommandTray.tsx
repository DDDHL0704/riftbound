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
        <strong className="wire-object-command-tray-next">{trayPlan.nextStepLabel}</strong>
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

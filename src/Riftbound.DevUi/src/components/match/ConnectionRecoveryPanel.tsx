import { PlugZap, RefreshCw, Unplug } from "lucide-react";
import type { ConnectionStatus } from "../../types/protocol";
import {
  buildConnectionRecoveryPlan,
  type ConnectionRecoveryActionId
} from "../../utils/connectionRecoveryPlan";
import { connectionStatusTone } from "../../utils/formatters";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";

type ConnectionRecoveryPanelProps = {
  actionsDisabled?: boolean;
  connectionStatus: ConnectionStatus;
  density?: "compact" | "full";
  hasSnapshot: boolean;
  lastSystemMessage?: string | null;
  onConnect: () => void;
  onDisconnect: () => void;
  onResync: () => void;
  promptSnapshotTick?: number | null;
  snapshotTick?: number | null;
  surface: "match" | "room";
};

export function ConnectionRecoveryPanel({
  actionsDisabled = false,
  connectionStatus,
  density = "full",
  hasSnapshot,
  lastSystemMessage,
  onConnect,
  onDisconnect,
  onResync,
  promptSnapshotTick,
  snapshotTick,
  surface
}: ConnectionRecoveryPanelProps) {
  const plan = buildConnectionRecoveryPlan({
    connectionStatus,
    hasSnapshot,
    lastSystemMessage,
    promptSnapshotTick,
    snapshotTick
  });

  return (
    <section
      className={`connection-recovery-panel connection-recovery-panel-${density}`}
      data-connection-recovery-panel
      data-connection-recovery-state={plan.state}
      data-connection-recovery-surface={surface}
      data-connection-recovery-tick-label={plan.tickLabel}
    >
      <header className="connection-recovery-header">
        <div>
          <span className="eyebrow">连接恢复</span>
          <h2>{plan.headline}</h2>
        </div>
        <StatusPill tone={connectionStatusTone(connectionStatus)}>{plan.statusLabel}</StatusPill>
      </header>
      <div className="connection-recovery-body">
        <p>{plan.detail}</p>
        <span>{plan.tickLabel}</span>
        {density === "full" && <small>{plan.nextStep}</small>}
      </div>
      <div className="connection-recovery-actions" role="group" aria-label="连接恢复操作">
        {plan.actions.map((action) => {
          const disabled = action.disabled || actionsDisabled;

          return (
            <Button
              data-connection-recovery-action={action.id}
              data-connection-recovery-action-disabled={disabled ? "true" : "false"}
              data-connection-recovery-action-state={action.state}
              disabled={disabled}
              icon={iconForAction(action.id)}
              key={action.id}
              onClick={handlerForAction(action.id, { onConnect, onDisconnect, onResync })}
              title={actionsDisabled ? "前端样例模式不操作实时连接" : action.title}
              variant={action.state === "primary" ? "secondary" : "ghost"}
            >
              {action.label}
            </Button>
          );
        })}
      </div>
    </section>
  );
}

function handlerForAction(
  id: ConnectionRecoveryActionId,
  handlers: Pick<ConnectionRecoveryPanelProps, "onConnect" | "onDisconnect" | "onResync">
) {
  switch (id) {
    case "connect":
      return handlers.onConnect;
    case "disconnect":
      return handlers.onDisconnect;
    case "resync":
      return handlers.onResync;
  }
}

function iconForAction(id: ConnectionRecoveryActionId) {
  switch (id) {
    case "connect":
      return <PlugZap size={16} />;
    case "disconnect":
      return <Unplug size={16} />;
    case "resync":
      return <RefreshCw size={16} />;
  }
}

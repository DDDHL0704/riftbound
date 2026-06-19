import type { ActionPromptDto, ConnectionStatus, SnapshotDto } from "../../types/protocol";
import { matchPhaseLabel, roomStatusLabel, timingStateLabel } from "../../utils/formatters";
import { buildWireTurnWindowPlan } from "../../utils/wireTurnWindowPlan";
import { StatusPill } from "../ui/StatusPill";

export function WireTurnWindowPanel({
  connectionStatus,
  playerId,
  prompt,
  snapshot
}: {
  connectionStatus: ConnectionStatus;
  playerId: string;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
}) {
  const plan = buildWireTurnWindowPlan({ connectionStatus, playerId, prompt, snapshot });

  return (
    <section className="wire-window-plan" aria-label="服务端窗口总览" data-wire-window-state={plan.state}>
      <header className="wire-window-plan-header">
        <div>
          <strong>窗口总览</strong>
          <span>{plan.promptTitle}</span>
        </div>
        <StatusPill tone={plan.tone}>{plan.stateLabel}</StatusPill>
      </header>

      <div className="wire-window-plan-primary">
        <span>
          <small>阶段</small>
          <strong>{matchPhaseLabel(plan.phase)}</strong>
        </span>
        <span>
          <small>窗口</small>
          <strong>{timingStateLabel(plan.windowState)}</strong>
        </span>
        <span>
          <small>房间</small>
          <strong>{roomStatusLabel(plan.roomStatus)}</strong>
        </span>
      </div>

      <div className="wire-window-plan-next" data-wire-window-next-step>
        下一步：{plan.nextStepLabel}
      </div>

      <div className="wire-window-plan-metrics">
        {plan.metrics.map((metric) => (
          <span className={metric.mine ? "is-mine" : ""} data-window-metric={metric.key} key={metric.key}>
            <small>{metric.label}</small>
            <strong>{metric.value}</strong>
          </span>
        ))}
      </div>

      <div className="wire-window-plan-contract">
        <span>提示类型：{plan.promptType}</span>
        <span>{plan.queueStateLabel}</span>
      </div>
    </section>
  );
}

import type { ActionPromptDto, ConnectionStatus, SnapshotDto } from "../../types/protocol";
import { matchPhaseLabel, roomStatusLabel, timingStateLabel } from "../../utils/formatters";
import { buildWirePriorityRailPlan } from "../../utils/wirePriorityRailPlan";
import { buildWireTurnWindowPlan } from "../../utils/wireTurnWindowPlan";
import { buildWireWindowEvidencePlan } from "../../utils/wireWindowEvidencePlan";
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
  const rail = buildWirePriorityRailPlan({ connectionStatus, playerId, prompt, snapshot });
  const evidence = buildWireWindowEvidencePlan({ connectionStatus, playerId, prompt, snapshot });

  return (
    <section
      className="wire-window-plan"
      aria-label="服务端窗口总览"
      data-wire-priority-mode={rail.mode}
      data-wire-window-state={plan.state}
    >
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

      <div className="wire-window-evidence" aria-label="服务端窗口证据" data-wire-window-evidence="true">
        <div className="wire-window-evidence-heading">
          <strong>证据摘要</strong>
          <span>{evidence.headline}</span>
        </div>
        <ol>
          {evidence.rows.map((row) => (
            <li
              className={row.mine ? "is-mine" : ""}
              data-window-evidence-key={row.key}
              data-window-evidence-state={row.state}
              key={row.key}
            >
              <small>{row.label}</small>
              <strong>{row.value}</strong>
              <span>{row.source}</span>
            </li>
          ))}
        </ol>
      </div>

      <div className="wire-priority-rail" aria-label="服务端优先权轨道">
        <div className="wire-priority-rail-heading">
          <strong>优先权轨道</strong>
          <span>{rail.modeLabel}</span>
        </div>
        <ol>
          {rail.steps.map((step) => (
            <li
              className={`${step.state === "active" ? "is-active" : ""} ${step.state === "blocked" ? "is-blocked" : ""} ${step.mine ? "is-mine" : ""}`}
              data-priority-step={step.key}
              data-priority-step-state={step.state}
              key={step.key}
            >
              <small>{step.label}</small>
              <strong>{step.value}</strong>
              <span>{step.hint}</span>
            </li>
          ))}
        </ol>
        <div className="wire-priority-rail-next" data-wire-priority-next-step={rail.activeStepKey}>
          {rail.headline}
        </div>
        <div className="wire-priority-rail-blocker">
          {rail.blockingReasonLabel}
        </div>
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

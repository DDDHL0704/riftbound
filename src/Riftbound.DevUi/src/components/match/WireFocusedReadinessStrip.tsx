import type { WireFocusedInteractionPlan } from "../../utils/wireFocusedInteractionPlan";
import { StatusPill } from "../ui/StatusPill";

export function WireFocusedReadinessStrip({ plan }: { plan: WireFocusedInteractionPlan }) {
  return (
    <section
      aria-label="焦点行动就绪状态"
      className="wire-focused-readiness"
      data-wire-focused-readiness-can-submit={plan.readiness.canSubmit ? "true" : "false"}
      data-wire-focused-readiness-command={plan.readiness.commandType ?? ""}
      data-wire-focused-readiness-enabled-count={plan.readiness.enabledCount}
      data-wire-focused-readiness-missing-required-count={plan.readiness.missingRequiredCount}
      data-wire-focused-readiness-state={plan.readiness.state}
      data-wire-focused-readiness-submission-gate={plan.submissionGate.state}
      data-wire-focused-readiness-window-gate={plan.windowGate.state}
    >
      <div className="wire-focused-readiness-heading">
        <strong>行动状态</strong>
        <StatusPill tone={plan.readiness.tone}>{plan.readiness.stateLabel}</StatusPill>
      </div>
      <div className="wire-focused-readiness-grid">
        <span>
          <small>候选</small>
          <strong>{plan.readiness.candidateLabel}</strong>
        </span>
        <span>
          <small>可提交</small>
          <strong>{plan.readiness.enabledCount}</strong>
        </span>
        <span>
          <small>缺少</small>
          <strong>{plan.readiness.missingRequiredCount}</strong>
        </span>
        <span>
          <small>命令</small>
          <strong>{plan.readiness.commandType ?? "无"}</strong>
        </span>
        <span>
          <small>门禁</small>
          <strong>{plan.submissionGate.stateLabel}</strong>
        </span>
        <span>
          <small>窗口</small>
          <strong>{plan.windowGate.stateLabel}</strong>
        </span>
      </div>
      <span className="wire-focused-readiness-next">{plan.readiness.nextStepLabel}</span>
    </section>
  );
}

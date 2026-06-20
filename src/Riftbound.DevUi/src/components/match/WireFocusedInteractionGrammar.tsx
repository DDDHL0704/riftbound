import type { FocusedInteractionGrammarPlan } from "../../utils/focusedInteractionGrammarPlan";
import { StatusPill } from "../ui/StatusPill";

export function WireFocusedInteractionGrammar({ plan }: { plan: FocusedInteractionGrammarPlan }) {
  return (
    <div
      aria-label="焦点交互语法"
      className="wire-focused-grammar"
      data-wire-focused-grammar-composer-state={plan.composerState}
      data-wire-focused-grammar-state={plan.state}
      role="group"
    >
      <div className="wire-focused-grammar-heading">
        <strong>交互语法</strong>
        <StatusPill tone={plan.state === "ready" ? "good" : "neutral"}>{plan.stateLabel}</StatusPill>
      </div>
      <div className="wire-focused-grammar-summary">
        <span>{plan.candidateLabel}</span>
        <small>下一步：{plan.nextStepLabel}</small>
        <small>命令：{plan.commandType ?? "未公开"} / 字段 {plan.commandFieldCount}</small>
        <small title={plan.composerReason}>组合：{plan.composerStateLabel}</small>
      </div>
      {plan.steps.length > 0 ? (
        <ol className="wire-focused-grammar-steps">
          {plan.steps.map((step) => (
            <li className={`is-${step.state}`} data-wire-grammar-role={step.role} key={step.key}>
              <span>{step.label}</span>
              <strong>{step.stateLabel}</strong>
              <small>
                {step.required ? "必需" : "可选"}
                {"；候选 "}{step.availableCount}
                {"；已选 "}{step.selectedCount}
              </small>
              {step.sampleLabels.length > 0 && <small>{step.sampleLabels.slice(0, 3).join(" / ")}</small>}
            </li>
          ))}
        </ol>
      ) : (
        <span className="empty-hint">点击服务端候选对象后显示命令语法。</span>
      )}
    </div>
  );
}

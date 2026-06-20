import type { WireFocusedInteractionPlan } from "../../utils/wireFocusedInteractionPlan";

export function WireFocusedSelectionGuide({ plan }: { plan: WireFocusedInteractionPlan }) {
  if (!plan.draft && plan.sourceCandidatePaths.length === 0) {
    return null;
  }

  return (
    <section
      aria-label="焦点候选选择路径"
      className="wire-focused-selection-guide"
      data-wire-focused-selection-draft={plan.draft ? "present" : "empty"}
      data-wire-focused-selection-path-count={plan.sourceCandidatePaths.length}
    >
      {plan.draft && (
        <div className="wire-selection-draft" role="group" aria-label="已点选候选草稿">
          <strong>桌面点选</strong>
          <span>目标 {plan.draft.targetCount}</span>
          <span>位置 {plan.draft.destinationSelected ? "已选" : "未选"}</span>
          <span>费用 {plan.draft.optionalCostCount}</span>
        </div>
      )}
      {plan.sourceCandidatePaths.length > 0 && (
        <div className="wire-focused-path" role="group" aria-label="焦点候选路径">
          {plan.sourceCandidatePaths.map((path) => (
            <article key={path.key}>
              <strong>{path.label}</strong>
              <ol>
                {path.steps.map((step) => (
                  <li className={step.required ? "is-required" : ""} key={step.key}>
                    <span>{step.label}</span>
                    <small>{step.required ? "必需；" : ""}{step.sampleLabel}</small>
                  </li>
                ))}
              </ol>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

import type { WireFocusedInteractionPlan } from "../../utils/wireFocusedInteractionPlan";

export function WireFocusedSelectionGuide({ plan }: { plan: WireFocusedInteractionPlan }) {
  if (!plan.draft && plan.selectionRows.length === 0 && plan.sourceCandidatePaths.length === 0) {
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
          {plan.selectionRows.length > 0 && (
            <ol className="wire-selection-row-list" aria-label="已点选对象明细">
              {plan.selectionRows.map((row) => (
                <li
                  data-wire-selection-row={row.role}
                  data-wire-selection-row-choice={row.choiceId}
                  data-wire-selection-row-object-ids={row.objectIds.join("|")}
                  key={row.key}
                >
                  <span>{row.roleLabel}</span>
                  <strong>{row.label}</strong>
                  <small>{row.objectLabel}</small>
                </li>
              ))}
            </ol>
          )}
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

import type { FocusedActionModel } from "../../utils/focusedActionModel";

export function WireFocusedActionSummary({ focusModel }: { focusModel: FocusedActionModel }) {
  return (
    <div
      aria-label="焦点行动摘要"
      className="wire-focused-action-summary"
      data-wire-focused-action-state={focusModel.submittedByServer ? "server-candidate" : "no-candidate"}
      role="group"
    >
      <div className="wire-focused-action-metrics">
        <span>
          <small>服务端状态</small>
          <strong>{focusModel.stateLabel}</strong>
        </span>
        <span>
          <small>可提交</small>
          <strong>{focusModel.enabledCount}</strong>
        </span>
        <span>
          <small>阻断</small>
          <strong>{focusModel.blockedCount}</strong>
        </span>
      </div>
      <span className="wire-focused-next-step" data-wire-focused-next-step>
        {focusModel.nextStepLabel}
      </span>
      {focusModel.blockingReasons.length > 0 && (
        <div className="wire-focused-blockers">
          {focusModel.blockingReasons.map((reason) => (
            <small key={reason}>阻断：{reason}</small>
          ))}
        </div>
      )}
      {focusModel.candidates.length > 0 && (
        <ol className="wire-focused-candidate-plan">
          {focusModel.candidates.slice(0, 4).map(({ candidate, key, nextStep, stateLabel }) => (
            <li className={candidate.enabled ? "is-enabled" : "is-disabled"} key={key}>
              <span>{candidate.label}</span>
              <small>{stateLabel}{nextStep ? `；下一步 ${nextStep.label}` : ""}</small>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}

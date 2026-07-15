import type { CommandSubmitHandler, CommandSubmissionUiSource } from "../../utils/commandSubmissionFollowupPlan";
import type {
  WireActionCommandReviewPlan,
  WireActionRoutePlan
} from "../../utils/wireActionMapPlan";

type WireObjectRouteReviewProps = {
  className?: string;
  onCommand?: CommandSubmitHandler;
  presentation?: "diagnostic" | "arena";
  review: WireActionCommandReviewPlan;
  route?: WireActionRoutePlan;
};

export function WireObjectRouteReview({
  className = "",
  onCommand,
  presentation = "diagnostic",
  review,
  route
}: WireObjectRouteReviewProps) {
  if (presentation === "arena" && !route) {
    return null;
  }

  const canSubmit = review.canSubmit && Boolean(review.command) && Boolean(onCommand);

  return (
    <section
      aria-label="焦点对象提交路线"
      className={`wire-object-route-review ${className}`.trim()}
      data-wire-object-route-review-presentation={presentation}
      data-wire-object-route-review-state={review.state}
      data-wire-object-route-state={route?.state ?? "empty"}
    >
      <div className="wire-object-route-review-heading">
        <div>
          <strong>{route?.candidateLabel ?? review.candidateLabel}</strong>
          <span>{review.stateLabel}</span>
        </div>
        <small>{review.summary}</small>
      </div>

      {presentation === "diagnostic" ? (
        <div className="wire-object-route-review-metrics">
          {review.metrics.map((metric) => (
            <span data-wire-object-route-review-metric={metric.key} key={metric.key}>
              <b>{metric.label}</b>
              <strong>{metric.value}</strong>
            </span>
          ))}
        </div>
      ) : null}

      <strong className="wire-object-route-review-next">下一步：{review.nextStepLabel}</strong>

      {presentation === "diagnostic" && route ? (
        <>
          <ol className="wire-object-route-review-steps" aria-label="焦点对象路线步骤">
            {route.steps.map((step) => (
              <li
                data-wire-object-route-step-role={step.role}
                data-wire-object-route-step-state={step.state}
                key={step.key}
              >
                <span>{step.label}</span>
                <strong>{step.stateLabel}</strong>
                <small>{step.required ? "必需" : "可选"} / 候选 {step.totalCount} / 已选 {step.selectedCount}</small>
              </li>
            ))}
          </ol>

          {route.fields.length > 0 && (
            <div className="wire-object-route-review-fields" aria-label="焦点对象命令字段覆盖">
              {route.fields.map((field) => (
                <span
                  data-wire-object-route-field={field.field}
                  data-wire-object-route-field-role={field.role ?? "server"}
                  data-wire-object-route-field-state={field.state}
                  key={field.key}
                >
                  <b>{field.label}</b>
                  <small>{field.required ? "必需" : "可选"} / {field.sourceLabel} / {field.stateLabel}</small>
                </span>
              ))}
            </div>
          )}

          <ol className="wire-object-route-review-checks" aria-label="焦点对象提交审计">
            {route.checkRows.map((check) => (
              <li
                data-wire-object-route-check={check.key}
                data-wire-object-route-check-state={check.state}
                key={check.key}
              >
                <span>{check.label}</span>
                <strong>{check.stateLabel}</strong>
                <small>{check.reason}</small>
              </li>
            ))}
          </ol>
        </>
      ) : presentation === "diagnostic" ? (
        <span className="empty-hint">点击服务端候选并建立选择草稿后显示提交路线。</span>
      ) : null}

      <button
        className="wire-object-route-review-submit"
        data-wire-object-route-review-submit-state={canSubmit ? "ready" : "blocked"}
        disabled={!canSubmit}
        onClick={() => {
          if (!review.command || !onCommand) {
            return;
          }

          onCommand(review.command, commandReviewUiSource(review));
        }}
        title={review.submitReason}
        type="button"
      >
        {presentation === "arena" ? "确认行动" : review.submitLabel}
      </button>
    </section>
  );
}

function commandReviewUiSource(review: WireActionCommandReviewPlan): Partial<CommandSubmissionUiSource> {
  return {
    candidateLabel: review.candidateLabel,
    commandSource: review.commandSource,
    commandSourceDetail: review.commandSourceDetail,
    commandSourceLabel: review.commandSourceLabel,
    label: review.candidateLabel
  };
}

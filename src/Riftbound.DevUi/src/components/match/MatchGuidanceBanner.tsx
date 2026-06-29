import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";
import type { MatchGuidancePlan, MatchGuidanceTone } from "../../utils/matchGuidancePlan";

export type MatchGuidancePrimaryAction = {
  id: string;
  label: string;
  disabled: boolean;
};

const TONE_PILL: Record<MatchGuidanceTone, "good" | "warn" | "neutral" | "bad"> = {
  good: "good",
  warn: "warn",
  neutral: "neutral",
  bad: "bad"
};

const TURN_STATE_PILL_LABEL: Record<MatchGuidancePlan["turnState"], string> = {
  yours: "你的回合",
  opponent: "对手回合",
  offline: "未连接",
  over: "已结束"
};

export function MatchGuidanceBanner({
  plan,
  primaryActions = [],
  onRunPrimaryAction
}: {
  plan: MatchGuidancePlan;
  primaryActions?: MatchGuidancePrimaryAction[];
  onRunPrimaryAction?: (id: string) => void;
}) {
  const hasActionsRow = primaryActions.length > 0 || plan.youCanLabels.length > 0;
  return (
    <section
      aria-label="对局向导"
      className="match-guidance-banner"
      data-match-guidance-banner
      data-match-guidance-turn-state={plan.turnState}
    >
      <div className="match-guidance-main">
        <span className="eyebrow">对局向导</span>
        <div className="match-guidance-headline-row">
          <h2 className="match-guidance-headline">{plan.headline}</h2>
          <StatusPill tone={TONE_PILL[plan.tone]}>{TURN_STATE_PILL_LABEL[plan.turnState]}</StatusPill>
        </div>
        <p className="match-guidance-detail">{plan.detail}</p>
      </div>
      {hasActionsRow && (
        <div className="match-guidance-actions" data-match-guidance-actions>
          {primaryActions.length > 0 && (
            <div className="match-guidance-primary" data-match-guidance-primary>
              {primaryActions.map((action) => (
                <Button
                  data-match-guidance-primary-action={action.id}
                  disabled={action.disabled}
                  key={action.id}
                  onClick={() => onRunPrimaryAction?.(action.id)}
                  variant="primary"
                >
                  {action.label}
                </Button>
              ))}
            </div>
          )}
          {plan.youCanLabels.length > 0 && (
            <div className="match-guidance-chip-group">
              <span className="match-guidance-actions-label">你可以：</span>
              <div className="match-guidance-chips">
                {plan.youCanLabels.map((label) => (
                  <span className="match-guidance-chip" data-match-guidance-chip={label} key={label}>
                    {label}
                  </span>
                ))}
              </div>
            </div>
          )}
        </div>
      )}
    </section>
  );
}

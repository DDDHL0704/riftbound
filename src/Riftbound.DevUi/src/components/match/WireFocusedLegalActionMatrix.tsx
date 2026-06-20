import {
  type WireFocusedInteractionPlan,
  type WireFocusedLegalActionRowPlan,
  type WireFocusedLegalActionState
} from "../../utils/wireFocusedInteractionPlan";
import { StatusPill } from "../ui/StatusPill";

export function WireFocusedLegalActionMatrix({ plan }: { plan: WireFocusedInteractionPlan }) {
  return (
    <section
      aria-label="焦点合法操作矩阵"
      className="wire-focused-legal-actions"
      data-wire-focused-legal-action-count={plan.legalActionRows.length}
    >
      <div className="wire-focused-legal-actions-heading">
        <strong>合法操作矩阵</strong>
        <StatusPill tone={plan.legalActionRows.some((row) => row.state === "ready") ? "good" : "neutral"}>
          {plan.legalActionRows.length > 0 ? `${plan.legalActionRows.length} 项` : "无关联"}
        </StatusPill>
      </div>
      {plan.legalActionRows.length === 0 ? (
        <span className="empty-hint">该对象当前没有出现在服务端行动候选中。</span>
      ) : (
        <ol className="wire-focused-legal-action-list">
          {plan.legalActionRows.slice(0, 6).map((row) => (
            <FocusedLegalActionRow key={row.key} row={row} />
          ))}
        </ol>
      )}
    </section>
  );
}

function FocusedLegalActionRow({ row }: { row: WireFocusedLegalActionRowPlan }) {
  return (
    <li
      className={`is-${row.state}`}
      data-wire-focused-legal-action={row.action}
      data-wire-focused-legal-action-state={row.state}
    >
      <div>
        <strong>{row.label}</strong>
        <StatusPill tone={legalActionTone(row.state)}>{row.stateLabel}</StatusPill>
      </div>
      <span>{row.nextStepLabel}</span>
      <small>角色：{row.roleLabels.length > 0 ? row.roleLabels.join(" / ") : "无"}</small>
      <small>命令：{row.commandType ?? "未公开"}</small>
      {row.missingRequiredLabels.length > 0 && <small>缺少：{row.missingRequiredLabels.join(" / ")}</small>}
      <small>{row.reason}</small>
    </li>
  );
}

function legalActionTone(state: WireFocusedLegalActionState): "good" | "neutral" | "warn" {
  switch (state) {
    case "ready":
      return "good";
    case "blocked":
    case "needs-selection":
      return "warn";
    case "informational":
      return "neutral";
  }
}

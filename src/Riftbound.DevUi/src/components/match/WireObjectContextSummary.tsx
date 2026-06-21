import type { ActionPromptContractDto } from "../../types/protocol";
import type { FocusedActionModel } from "../../utils/focusedActionModel";
import { buildFocusedObjectCommandPlan } from "../../utils/focusedObjectCommandPlan";
import type { TableObjectContext } from "../../utils/tableObjectContext";

export function WireObjectContextSummary({
  context,
  contract,
  focusModel
}: {
  context?: TableObjectContext;
  contract?: ActionPromptContractDto | null;
  focusModel?: FocusedActionModel;
}) {
  const plan = buildFocusedObjectCommandPlan({ context, contract, focusModel });
  if (!plan) {
    return <span className="empty-hint">服务端未提供该对象的公开上下文。</span>;
  }

  return (
    <div
      className="wire-object-context"
      role="group"
      aria-label="焦点对象上下文"
      data-wire-object-context-authority={plan.authorityState}
      data-wire-object-context-source={plan.contextSourceLabel}
    >
      <div className="wire-object-context-grid">
        {plan.statusCards.map((card, index) => (
          <span data-wire-object-status-card={index} key={card.label}>
            <small>{card.label}</small>
            <strong>{card.value}</strong>
          </span>
        ))}
      </div>
      <ol className="wire-object-section-map" aria-label="焦点对象结构分区">
        {plan.sectionRows.map((section) => (
          <li
            data-wire-object-section={section.key}
            data-wire-object-section-count={section.count}
            data-wire-object-section-source={section.sourceLabel}
            data-wire-object-section-state={section.state}
            key={section.key}
          >
            <span>{section.label}</span>
            <strong>{section.stateLabel}</strong>
            <small>{section.summary}</small>
          </li>
        ))}
      </ol>
      <span className="wire-object-context-line" data-wire-object-context-authority-label>权威：{plan.authorityLabel}</span>
      <span className="wire-object-context-line" data-wire-object-context-boundary>{plan.boundaryLabel}</span>
      {plan.contract && (
        <div
          className="wire-object-contract-block"
          aria-label="焦点对象提示契约"
          data-wire-object-contract="true"
          data-wire-object-contract-kind={plan.contract.promptKind}
        >
          <span className="wire-object-context-line">提示契约：{plan.contract.promptKind} / {plan.contract.candidateAction}</span>
          <div>
            <small>提交 {plan.contract.requiredPayloadCount}</small>
            <small>合法 {plan.contract.legalChoicesCount}</small>
            <small>公开 {plan.contract.visibleMetadataCount}</small>
            <small>隐藏 {plan.contract.hiddenMetadataCount}</small>
          </div>
        </div>
      )}
      {plan.stackRoles.length > 0 && (
        <span className="wire-object-context-line">结算链：{plan.stackRoles.join(" / ")}</span>
      )}
      {plan.serverRelationRows.length > 0 && (
        <div className="wire-object-server-relation-block">
          <span className="wire-object-context-line">服务端关联对象</span>
          <ol className="wire-object-server-relation-list" aria-label="焦点对象服务端关联角色">
            {plan.serverRelationRows.slice(0, 4).map((row) => (
              <li
                data-wire-object-server-relation={row.key}
                data-wire-object-server-relation-actions={row.actionLabels.join("|")}
                data-wire-object-server-relation-source={row.sourceLabel}
                key={row.key}
              >
                <span>{row.roles.length > 0 ? row.roles.join(" / ") : "服务端关联"}</span>
                <strong>{row.candidateSummary}</strong>
                {row.actionLabels.length > 0 ? <small>{row.actionLabels.join(" / ")}</small> : null}
                <small>{row.stepSummary}</small>
              </li>
            ))}
          </ol>
        </div>
      )}
      {plan.syntax.rows.length > 0 && (
        <div
          aria-label="焦点对象候选语法"
          className="wire-object-syntax-block"
          data-wire-object-syntax-count={plan.syntax.rows.length}
          data-wire-object-syntax-missing-required-count={plan.syntax.missingRequiredCount}
          data-wire-object-syntax-usable-count={plan.syntax.usableCount}
        >
          <span className="wire-object-context-line" data-wire-object-syntax-summary>{plan.syntax.summary}</span>
          <ol className="wire-object-syntax-list">
            {plan.syntax.rows.slice(0, 4).map((row) => (
              <li
                data-wire-object-syntax-role={row.role}
                data-wire-object-syntax-source={row.source}
                data-wire-object-syntax-state={row.state}
                key={row.key}
              >
                <span>{row.sourceLabel} / {row.candidateLabel}</span>
                <strong>{row.roleLabel} / {row.stateLabel}</strong>
                <small>{row.objectChoiceCount}/{row.choiceCount} 选项{row.required ? " / 必选" : " / 可选"}</small>
              </li>
            ))}
          </ol>
        </div>
      )}
      {plan.nextStepRows.length > 0 && (
        <ol className="wire-object-next-step-list" aria-label="焦点对象候选步骤">
          {plan.nextStepRows.slice(0, 3).map((row) => (
            <li
              className={row.enabled ? "is-enabled" : "is-disabled"}
              data-wire-object-next-step-enabled={row.enabled ? "true" : "false"}
              key={`${row.candidateLabel}-${row.nextStepLabel ?? row.stateLabel}`}
            >
              <span>{row.candidateLabel}</span>
              <strong>{row.stateLabel}{row.nextStepLabel ? `；下一步 ${row.nextStepLabel}` : ""}</strong>
            </li>
          ))}
        </ol>
      )}
      {plan.commandRows.length > 0 && (
        <div className="wire-object-command-block">
          <span className="wire-object-context-line">服务端命令</span>
          <ol className="wire-object-command-list" aria-label="焦点对象服务端命令字段">
            {plan.commandRows.slice(0, 4).map((row) => (
              <li
                className={row.enabled ? "is-enabled" : "is-disabled"}
                data-wire-object-command-category={row.category}
                data-wire-object-command-composer-state={row.composerState}
                data-wire-object-command-intent={row.intent}
                data-wire-object-command-priority={row.priority}
                data-wire-object-command-ui-hint={row.uiHint}
                key={row.key}
              >
                <span>{row.enabled ? "可提交" : "阻断"}{row.roles.length > 0 ? ` / ${row.roles.join("/")}` : ""}</span>
                <strong>{row.commandType ?? row.label}{row.requiredFields.length > 0 ? `：${row.requiredFields.join(" / ")}` : ""}</strong>
                <small>语义：{row.category} / {row.intent} / {row.uiHint}</small>
                {row.stepSummary && <small>步骤：{row.stepSummary}</small>}
                <small title={row.composerReason}>组合：{row.composerStateLabel}</small>
                {row.secondaryFields.length > 0 && <small>{row.secondaryFields.join(" / ")}</small>}
                {!row.enabled && <small>{row.reason}</small>}
              </li>
            ))}
          </ol>
        </div>
      )}
      {plan.eventRows.length > 0 ? (
        <div className="wire-object-event-block">
          <span className="wire-object-context-line">近期事件</span>
          <ol className="wire-object-event-list" aria-label="焦点对象近期事件">
            {plan.eventRows.map((event, index) => (
              <li
                data-wire-object-event-kind={event.kind}
                data-wire-object-event-role={event.role}
                key={`${event.kind}-${event.role}-${index}`}
              >
                <span>{event.role}</span>
                <strong>{event.description}</strong>
              </li>
            ))}
          </ol>
        </div>
      ) : (
        <span className="wire-object-context-line">近期事件：无公开关联事件</span>
      )}
    </div>
  );
}

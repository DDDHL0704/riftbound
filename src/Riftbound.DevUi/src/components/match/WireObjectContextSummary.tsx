import type { TableObjectContext } from "../../utils/tableObjectContext";

export function WireObjectContextSummary({ context }: { context?: TableObjectContext }) {
  if (!context) {
    return <span className="empty-hint">服务端未提供该对象的公开上下文。</span>;
  }

  const latestEvents = context.eventLinks.slice(-3).reverse();
  const enabledCandidates = context.candidateLinks.filter((candidate) => candidate.enabled);
  const disabledCandidates = context.candidateLinks.filter((candidate) => !candidate.enabled);

  return (
    <div className="wire-object-context" role="group" aria-label="焦点对象上下文">
      <div className="wire-object-context-grid">
        <span>
          <small>位置</small>
          <strong>{context.zone.label}</strong>
        </span>
        <span>
          <small>状态</small>
          <strong>{context.stateLabels.slice(0, 3).join(" / ")}</strong>
        </span>
        <span>
          <small>候选</small>
          <strong>{context.promptEnabledCount} 可用 / {context.promptDisabledCount} 阻断</strong>
        </span>
      </div>
      {context.stackRoles.length > 0 && (
        <span className="wire-object-context-line">结算链：{context.stackRoles.join(" / ")}</span>
      )}
      {enabledCandidates.length > 0 && (
        <span className="wire-object-context-line">可用：{enabledCandidates.slice(0, 2).map(candidate => `${candidate.label}(${candidate.roles.join("/")})`).join("、")}</span>
      )}
      {disabledCandidates.length > 0 && enabledCandidates.length === 0 && (
        <span className="wire-object-context-line">阻断：{disabledCandidates.slice(0, 2).map(candidate => candidate.reason).join("、")}</span>
      )}
      {latestEvents.length > 0 ? (
        <div className="wire-object-event-block">
          <span className="wire-object-context-line">近期事件</span>
          <ol className="wire-object-event-list" aria-label="焦点对象近期事件">
            {latestEvents.map((event, index) => (
              <li key={`${event.kind}-${event.role}-${index}`}>
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

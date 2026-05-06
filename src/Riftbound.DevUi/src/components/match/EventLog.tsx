import { ErrorDto, GameEvent } from "../../types/protocol";

export function EventLog({ events, errors }: { events: GameEvent[]; errors: ErrorDto[] }) {
  return (
    <section className="side-panel event-log">
      <header>
        <span className="eyebrow">服务端日志</span>
        <h2>事件 / 错误</h2>
      </header>
      {errors.map((error, index) => (
        <article className="log-row log-error" key={`error-${index}`}>
          <strong>{error.code}</strong>
          <span>{error.message}</span>
        </article>
      ))}
      {events.length === 0 && errors.length === 0 && <span className="empty-hint">暂无服务端事件。</span>}
      {events.map((event, index) => (
        <article className="log-row" key={`${event.kind}-${index}`}>
          <strong>{event.kind}</strong>
          <span>{event.description}</span>
        </article>
      ))}
    </section>
  );
}

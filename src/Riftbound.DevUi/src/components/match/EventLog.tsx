import { ErrorDto, GameEvent } from "../../types/protocol";

const eventKindLabels: Record<string, string> = {
  BATTLE_CLOSED: "战斗结束",
  BATTLE_DECLARED: "声明战斗",
  BATTLEFIELD_CONTESTED: "战场争夺",
  BATTLEFIELD_CONTROL_RESOLVED: "战场控制结算",
  BATTLEFIELD_CONQUERED: "征服战场",
  BATTLEFIELD_HELD: "据守战场",
  CARD_DRAWN: "抽牌",
  CARD_PLAYED: "打出卡牌",
  COST_PAID: "支付费用",
  DAMAGE_APPLIED: "造成伤害",
  FOCUS_PASSED: "让过焦点",
  PRIORITY_PASSED: "让过优先权",
  SPELL_DUEL_CLOSED: "法术对决关闭",
  SPELL_DUEL_STARTED: "法术对决开始",
  STACK_ITEM_ADDED: "加入结算链",
  STACK_ITEM_RESOLVED: "结算链项目结算",
  UNIT_DESTROYED: "单位摧毁"
};

function eventKindLabel(kind: string) {
  return eventKindLabels[kind] ?? "服务端事件";
}

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
          <strong title={event.kind}>{eventKindLabel(event.kind)}</strong>
          <span>{event.description}</span>
        </article>
      ))}
    </section>
  );
}

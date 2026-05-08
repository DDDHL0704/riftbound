import { SnapshotDto } from "../../types/protocol";
import { asArray, asRecord, asString } from "../../utils/collections";

const taskKindLabels: Record<string, string> = {
  BATTLEFIELD_CONTESTED: "战场控制检查",
  DESTROY_LETHAL_UNIT: "致命伤害清理",
  DESTROY_ZERO_POWER_UNIT: "0 战力清理",
  REMOVE_ILLEGAL_STANDBY: "待命清理",
  START_BATTLE: "开始战斗",
  START_SPELL_DUEL: "开始法术对决"
};

const phaseLabels: Record<string, string> = {
  BATTLE_TASKS: "战斗任务",
  BATTLEFIELD_TASKS: "战场任务",
  IDLE: "空闲",
  SPELL_DUEL_TASKS: "法术对决任务",
  STATE_BASED_CLEANUP: "状态清理"
};

const resolutionKindLabels: Record<string, string> = {
  CONQUERED: "征服",
  CONTROL_RESOLVED: "控制结算",
  HELD: "据守"
};

const battleResolutionKindLabels: Record<string, string> = {
  CLOSED: "战斗结束",
  NO_RESULT: "战斗无结果"
};

const stackEffectLabels: Record<string, string> = {
  SPELL: "法术",
  ABILITY: "技能",
  HEXTECH_RAY_DAMAGE_3: "海克斯射线伤害",
  LEGEND_ABILITY: "传奇技能",
  TRIGGER: "触发",
  REVEAL_CARD: "翻开待命"
};

function labelFor(map: Record<string, string>, value: unknown, fallback = "无") {
  const key = asString(value, "");
  return key ? (map[key] ?? key) : fallback;
}

function stackEffectLabel(value: unknown) {
  const key = asString(value, "");
  return key ? (stackEffectLabels[key] ?? "服务端效果") : "效果";
}

export function StackPanel({ snapshot }: { snapshot?: SnapshotDto }) {
  const stack = snapshot?.stack ?? [];
  const timing = asRecord(snapshot?.timing);
  const queue = asRecord(timing.pendingTaskQueue);
  const tasks = asArray<Record<string, unknown>>(queue.tasks);
  const battleResolutions = asArray<Record<string, unknown>>(timing.battleResolutions);
  const battlefieldResolutions = asArray<Record<string, unknown>>(timing.battlefieldResolutions);

  return (
    <section className="side-panel">
      <header>
        <span className="eyebrow">规则队列</span>
        <h2>结算链 / 任务</h2>
      </header>
      <div className="stack-list">
        {stack.length === 0 && <span className="empty-hint">当前无结算链项目</span>}
        {stack.map((item, index) => <StackItemSummary item={item} key={index} ordinal={stack.length - index} />)}
      </div>
      <div className="task-queue">
        <strong>待处理任务</strong>
        <span>阶段：{labelFor(phaseLabels, queue.phase)}</span>
        <span>活动任务：{asString(queue.activeTaskId, "无")}</span>
        <span>{queue.isBlocking ? "阻塞普通行动" : "不阻塞"}</span>
        {tasks.slice(0, 4).map((task, index) => (
          <span key={asString(task.taskId, `task-${index}`)}>
            {labelFor(taskKindLabels, task.kind, "任务")}：{asString(task.reason, "服务端规则")}
          </span>
        ))}
        {battleResolutions.slice(0, 3).map((resolution, index) => (
          <span key={asString(resolution.resolutionId, `battle-resolution-${index}`)}>
            {labelFor(battleResolutionKindLabels, resolution.kind, "战斗结果")}：
            {asString(resolution.winnerPlayerId, "无胜者")}
          </span>
        ))}
        {battlefieldResolutions.slice(0, 3).map((resolution, index) => (
          <span key={asString(resolution.resolutionId, `battlefield-resolution-${index}`)}>
            {labelFor(resolutionKindLabels, resolution.kind, "战场结果")}：
            {asString(resolution.playerId ?? resolution.controllerId, "无控制者")}
          </span>
        ))}
      </div>
    </section>
  );
}

function StackItemSummary({ item, ordinal }: { item: unknown; ordinal: number }) {
  const record = asRecord(item);
  const cardNo = asString(record.cardNo, "");
  const targets = asArray<unknown>(record.targetObjectIds).length;
  const destination = asString(record.destination, "");

  return (
    <article className="stack-item">
      <strong>项目 {ordinal}</strong>
      <span>控制者：{asString(record.controllerId, "未知")}</span>
      <span>来源：{cardNo || "服务端技能"}</span>
      <span>类型：{stackEffectLabel(record.effectKind)}</span>
      {targets > 0 && <span>目标：{targets} 个</span>}
      {destination && <span>去向：{destination}</span>}
    </article>
  );
}

import { Crosshair, Flag, Gauge, Swords } from "lucide-react";
import type { ReactNode } from "react";
import { ActionPromptDto, SnapshotDto } from "../../types/protocol";
import { asArray, asNumber, asRecord, asString } from "../../utils/collections";
import { matchPhaseLabel, roomStatusLabel, roomStatusTone, timingStateLabel } from "../../utils/formatters";
import { StatusPill } from "../ui/StatusPill";

export function MatchStatusPanel({
  playerId,
  prompt,
  snapshot
}: {
  playerId: string;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
}) {
  const timing = asRecord(snapshot?.timing);
  const turnWindow = asRecord(timing.turnWindow);
  const spellDuel = asRecord(timing.spellDuel);
  const battle = asRecord(timing.battle);
  const queue = asRecord(timing.pendingTaskQueue);
  const battlefieldTasks = asArray<Record<string, unknown>>(timing.battlefieldTasks);
  const triggerQueue = asArray<Record<string, unknown>>(timing.triggerQueue);
  const players = Object.entries(snapshot?.players ?? {});
  const winningScore = asNumber(timing.winningScore, 8);
  const roomStatus = asString(timing.roomStatus, "未知");
  const phase = asString(timing.phase, snapshot?.turnState ?? "");
  const windowState = asString(turnWindow.state, asString(timing.timingState, ""));
  const actingPlayerId = asString(turnWindow.actingPlayerId, asString(timing.priorityPlayerId, "无"));
  const focusPlayerId = asString(spellDuel.focusPlayerId, asString(timing.focusPlayerId, "无"));
  const promptOwner = prompt?.playerId ?? asString(timing.promptPlayerId, "无");

  return (
    <section className="side-panel match-status-panel">
      <header>
        <div>
          <span className="eyebrow">正式桌面状态</span>
          <h2>对局总览</h2>
        </div>
        <StatusPill tone={roomStatusTone(roomStatus)}>{roomStatusLabel(roomStatus)}</StatusPill>
      </header>
      <div className="match-status-grid">
        <StatusMetric icon={<Gauge size={15} />} label="阶段" value={matchPhaseLabel(phase)} />
        <StatusMetric label="窗口" value={timingStateLabel(windowState)} />
        <StatusMetric icon={<Crosshair size={15} />} label="行动权" mine={actingPlayerId === playerId} value={actingPlayerId} />
        <StatusMetric label="焦点" mine={focusPlayerId === playerId} value={focusPlayerId} />
        <StatusMetric label="提示归属" mine={promptOwner === playerId} value={promptOwner} />
        <StatusMetric label="候选" value={`${prompt?.candidates?.filter((candidate) => candidate.enabled).length ?? 0} 项`} />
      </div>
      <div className="score-track-list">
        {players.length === 0 && <span className="empty-hint">等待服务端玩家快照。</span>}
        {players.map(([id, player]) => (
          <ScoreTrack key={id} label={id === playerId ? "我方" : "对手"} playerId={id} score={player.score ?? 0} winningScore={winningScore} />
        ))}
      </div>
      <div className="lifecycle-grid">
        <LifecycleCard
          icon={<Flag size={16} />}
          title="法术对决"
          active={Boolean(spellDuel.isActive)}
          rows={[
            ["ID", asString(spellDuel.spellDuelId, "无")],
            ["关联战场", asString(spellDuel.battlefieldObjectId, "无")],
            ["焦点", focusPlayerId],
            ["已让过", `${asArray(spellDuel.passedFocusPlayerIds).length} 人`]
          ]}
        />
        <LifecycleCard
          icon={<Swords size={16} />}
          title="战斗"
          active={Boolean(battle.isActive)}
          rows={[
            ["ID", asString(battle.battleId, "无")],
            ["关联战场", asString(battle.battlefieldObjectId, "无")],
            ["进攻单位", `${asArray(battle.attackerObjectIds).length} 个`],
            ["防守单位", `${asArray(battle.defenderObjectIds).length} 个`]
          ]}
        />
      </div>
      <div className="rule-state-strip">
        <span>结算链 {snapshot?.stack.length ?? 0} 项</span>
        <span>清理队列 {asArray(queue.tasks).length} 项{queue.isBlocking ? " · 阻塞" : ""}</span>
        <span>战场任务 {battlefieldTasks.length} 项</span>
        <span>触发队列 {triggerQueue.length} 项</span>
      </div>
    </section>
  );
}

function StatusMetric({
  icon,
  label,
  mine,
  value
}: {
  icon?: ReactNode;
  label: string;
  mine?: boolean;
  value: string;
}) {
  return (
    <article className={`status-metric ${mine ? "is-mine" : ""}`}>
      <span>{icon}{label}</span>
      <strong>{value}</strong>
    </article>
  );
}

function ScoreTrack({
  label,
  playerId,
  score,
  winningScore
}: {
  label: string;
  playerId: string;
  score: number;
  winningScore: number;
}) {
  const clamped = Math.max(0, Math.min(score, winningScore));
  const percent = winningScore > 0 ? (clamped / winningScore) * 100 : 0;

  return (
    <article className="score-track">
      <header>
        <strong>{label}</strong>
        <span>{playerId} · {score}/{winningScore}</span>
      </header>
      <div className="score-track-bar" aria-label={`${playerId} 得分 ${score}/${winningScore}`}>
        <span style={{ width: `${percent}%` }} />
      </div>
    </article>
  );
}

function LifecycleCard({
  active,
  icon,
  rows,
  title
}: {
  active: boolean;
  icon: ReactNode;
  rows: Array<[string, string]>;
  title: string;
}) {
  return (
    <article className={`lifecycle-card ${active ? "is-active" : ""}`}>
      <header>
        <span>{icon}{title}</span>
        <StatusPill tone={active ? "warn" : "neutral"}>{active ? "进行中" : "未开启"}</StatusPill>
      </header>
      {rows.map(([label, value]) => (
        <span key={label}>{label}：{value}</span>
      ))}
    </article>
  );
}

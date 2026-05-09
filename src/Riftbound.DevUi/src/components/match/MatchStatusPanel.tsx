import { Crosshair, Flag, Gauge, ListChecks, Swords } from "lucide-react";
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
  const queueTasks = asArray<Record<string, unknown>>(queue.tasks);
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
  const promptType = prompt?.view?.type ?? "";
  const spellDuelStackItemIds = asArray<string>(spellDuel.stackItemIds);
  const battleAttackerIds = asArray<string>(battle.attackerObjectIds);
  const battleDefenderIds = asArray<string>(battle.defenderObjectIds);
  const battleParticipantControllers = new Set(
    Object.values(asRecord(battle.participantControllerIds))
      .filter((value): value is string => typeof value === "string" && value.trim().length > 0)
  );

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
        <StatusMetric label="支付费用" value={promptType === "PAY_COST" ? "服务端窗口" : "等待服务端窗口"} />
        <StatusMetric label="触发排序" value={promptType === "ORDER_TRIGGERS" ? "服务端窗口" : "等待服务端窗口"} />
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
            ["当前状态", spellDuelStateLabel(spellDuel, windowState)],
            ["ID", asString(spellDuel.spellDuelId, "无")],
            ["战场 ID", asString(spellDuel.battlefieldObjectId, "无")],
            ["焦点玩家", focusPlayerId],
            ["优先行动玩家", actingPlayerId],
            ["结算链", idsSummary(spellDuelStackItemIds)],
            ["已让过", `${asArray(spellDuel.passedFocusPlayerIds).length} 人`]
          ]}
        />
        <LifecycleCard
          icon={<Swords size={16} />}
          title="战斗"
          active={Boolean(battle.isActive)}
          rows={[
            ["当前状态", battleStateLabel(battle)],
            ["ID", asString(battle.battleId, "无")],
            ["战场 ID", asString(battle.battlefieldObjectId, "无")],
            ["优先行动玩家", actingPlayerId],
            ["进攻方", idsSummary(battleAttackerIds)],
            ["防守方", idsSummary(battleDefenderIds)],
            ["参与控制者", `${battleParticipantControllers.size} 项`],
            ["伤害分配", prompt?.view?.type === "ASSIGN_COMBAT_DAMAGE" ? "服务端窗口" : "等待服务端窗口"]
          ]}
        />
        <LifecycleCard
          icon={<ListChecks size={16} />}
          title="中央清理"
          active={Boolean(queue.hasTasks ?? queue.isBlocking)}
          rows={[
            ["阶段", cleanupPhaseLabel(asString(queue.phase, ""))],
            ["活动任务", activeTaskLabel(queue.activeTaskId)],
            ["待处理", `${queueTasks.length} 项`],
            ["首项", cleanupTaskKindLabel(asString(queueTasks[0]?.kind, ""))]
          ]}
        />
      </div>
      <div className="rule-state-strip">
        <span>结算链 {snapshot?.stack.length ?? 0} 项</span>
        <span>清理队列 {queueTasks.length} 项{queue.isBlocking ? " · 阻塞" : ""}</span>
        <span>战场任务 {battlefieldTasks.length} 项</span>
        <span>触发队列 {triggerQueue.length} 项</span>
        <span>支付费用 {promptType === "PAY_COST" ? "服务端窗口" : "等待服务端窗口"}</span>
        <span>触发排序 {promptType === "ORDER_TRIGGERS" ? "服务端窗口" : "等待服务端窗口"}</span>
      </div>
    </section>
  );
}

function cleanupPhaseLabel(phase: string): string {
  switch (phase) {
    case "BATTLE_TASKS":
      return "战斗任务";
    case "BATTLEFIELD_TASKS":
      return "战场任务";
    case "IDLE":
      return "空闲";
    case "SPELL_DUEL_TASKS":
      return "法术对决任务";
    case "STATE_BASED_CLEANUP":
      return "状态清理";
    default:
      return phase ? "服务端阶段" : "无";
  }
}

function cleanupTaskKindLabel(kind: string): string {
  switch (kind) {
    case "BATTLEFIELD_CONTESTED":
      return "战场争夺";
    case "DESTROY_LETHAL_UNIT":
      return "致命伤害";
    case "DESTROY_ZERO_POWER_UNIT":
      return "0 战力";
    case "RECALL_UNATTACHED_EQUIPMENT":
      return "装备清理";
    case "REMOVE_ILLEGAL_STANDBY":
      return "待命清理";
    case "START_BATTLE":
      return "开始战斗";
    case "START_SPELL_DUEL":
      return "开始法术对决";
    default:
      return kind ? "服务端任务" : "无";
  }
}

function activeTaskLabel(value: unknown): string {
  return asString(value, "") ? "处理中" : "无";
}

function spellDuelStateLabel(spellDuel: Record<string, unknown>, windowState: string): string {
  if (!spellDuel.isActive) {
    return "未开启";
  }
  if (spellDuel.isClosed) {
    return "已关闭";
  }
  return timingStateLabel(windowState);
}

function battleStateLabel(battle: Record<string, unknown>): string {
  return battle.isActive ? "进行中" : "未开启";
}

function idsSummary(ids: string[]): string {
  const safeIds = ids.filter((id) => typeof id === "string" && id.trim().length > 0);
  if (safeIds.length === 0) {
    return "无";
  }

  return `${safeIds.slice(0, 2).join("、")}${safeIds.length > 2 ? ` 等 ${safeIds.length} 项` : ""}`;
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

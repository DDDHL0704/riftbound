import { Activity, Database } from "lucide-react";
import type { ReactNode } from "react";
import { ActionPromptDto, PlayerSnapshotView, SnapshotDto } from "../../types/protocol";
import { asArray, asNumber, asRecord, asString } from "../../utils/collections";
import { redactInternalText } from "../../utils/redaction";
import { StatusPill } from "../ui/StatusPill";

type SnapshotDebugPanelProps = {
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
};

export function SnapshotDebugPanel({ prompt, snapshot }: SnapshotDebugPanelProps) {
  const timing = asRecord(snapshot?.timing);
  const queue = asRecord(timing.pendingTaskQueue);
  const queueTasks = asArray<unknown>(queue.tasks);
  const stackCount = snapshot?.stack.length ?? 0;
  const playerEntries = Object.entries(snapshot?.players ?? {});
  const promptType = prompt?.view?.type?.trim() || "无";
  const roomStatus = asString(timing.roomStatus, "未知");
  const phase = asString(timing.phase, asString(timing.timingState, "未知"));

  return (
    <section className="side-panel snapshot-debug-panel">
      <header>
        <div>
          <span className="eyebrow">Snapshot Viewer</span>
          <h2>权威快照摘要</h2>
        </div>
        <StatusPill tone={snapshot ? "good" : "neutral"}>{snapshot ? `tick ${snapshot.tick}` : "无快照"}</StatusPill>
      </header>
      <p className="snapshot-debug-note">
        只显示服务端快照的数量和窗口摘要，不展开隐藏对象、牌堆顺序或完整原始 JSON。
      </p>
      <div className="snapshot-debug-grid">
        <SnapshotMetric icon={<Database size={14} />} label="回合" value={snapshot ? String(snapshot.turnNumber) : "无"} />
        <SnapshotMetric label="行动玩家" value={snapshot?.activePlayerId ?? "无"} />
        <SnapshotMetric label="房间状态" value={safeDebugText(roomStatus)} />
        <SnapshotMetric label="阶段" value={safeDebugText(phase)} />
        <SnapshotMetric icon={<Activity size={14} />} label="结算链" value={`${stackCount} 项`} />
        <SnapshotMetric label="任务队列" value={`${queueTasks.length} 项`} />
      </div>
      <div className="snapshot-debug-section">
        <strong>当前 Prompt</strong>
        <span>类型：{safeDebugText(promptType)}</span>
        <span>归属：{prompt?.playerId ?? "无"}</span>
        <span>可操作：{prompt?.actionable ? "是" : "否"}</span>
        <span>候选：{prompt?.candidates?.length ?? 0} 项</span>
        <span>戳：{prompt?.promptId ? "已提供" : "无"} / {prompt?.snapshotTick ?? "无"}</span>
      </div>
      <div className="snapshot-debug-section">
        <strong>玩家视角</strong>
        {playerEntries.length === 0 && <span className="empty-hint">等待服务端玩家快照。</span>}
        {playerEntries.map(([playerId, player]) => (
          <PlayerSnapshotSummary key={playerId} player={player} playerId={playerId} />
        ))}
      </div>
    </section>
  );
}

function SnapshotMetric({
  icon,
  label,
  value
}: {
  icon?: ReactNode;
  label: string;
  value: string;
}) {
  return (
    <div className="snapshot-debug-metric">
      <span>{icon}{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function PlayerSnapshotSummary({ player, playerId }: { player: PlayerSnapshotView; playerId: string }) {
  const zones = player.zones;
  const handCount = player.handSize ?? zones?.handHidden ?? zones?.hand?.length ?? 0;
  const objectCount = Object.keys(player.objects ?? {}).length;
  const baseCount = zones?.base?.length ?? 0;
  const battlefieldCount = zones?.battlefields?.length ?? 0;
  const graveyardCount = zones?.graveyard?.length ?? 0;
  const banishedCount = zones?.banished?.length ?? 0;

  return (
    <article className="snapshot-player-row">
      <strong>{safeDebugText(player.name ?? player.id ?? playerId)}</strong>
      <span>分数 {asNumber(player.score)} / 经验 {asNumber(player.experience)}</span>
      <span>手牌 {handCount} / 对象 {objectCount}</span>
      <span>基地 {baseCount} / 战场 {battlefieldCount} / 废牌 {graveyardCount} / 放逐 {banishedCount}</span>
      <span>{player.deckSubmitted ? "已提交卡组" : "未提交卡组"} · {player.ready ? "已准备" : "未准备"}</span>
    </article>
  );
}

function safeDebugText(value: string): string {
  return redactInternalText(value) || "无";
}

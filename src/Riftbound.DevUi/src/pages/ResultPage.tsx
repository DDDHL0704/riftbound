import { RefreshCw } from "lucide-react";
import { useEffect } from "react";
import { Button } from "../components/ui/Button";
import { StatusPill } from "../components/ui/StatusPill";
import { useSettings } from "../stores/settingsStore";
import { useMatchController } from "../stores/useMatchController";
import { asNumber, asRecord, asString } from "../utils/collections";
import { roomStatusLabel, roomStatusTone } from "../utils/formatters";

export function ResultPage({ matchId }: { matchId: string; onNavigate: unknown }) {
  const { settings } = useSettings();
  const controller = useMatchController(settings.serverUrl, matchId, settings.playerId);
  const snapshot = controller.state.snapshot;
  const timing = asRecord(snapshot?.timing);
  const roomStatus = asString(timing.roomStatus, "未知");
  const winnerPlayerId = asString(timing.winnerPlayerId, "");
  const players = Object.entries(snapshot?.players ?? {});

  useEffect(() => {
    void controller.join().catch(() => undefined);
  }, [controller.join]);

  return (
    <div className="page-grid">
      <section className="page-header">
        <div>
          <span className="eyebrow">结算</span>
          <h1>{winnerPlayerId ? `胜者：${winnerPlayerId}` : matchId}</h1>
          <p>结果只读取服务端 authoritative snapshot，不根据本地分数推断胜负。</p>
        </div>
        <StatusPill tone={roomStatusTone(roomStatus)}>{roomStatusLabel(roomStatus)}</StatusPill>
      </section>
      <section className="match-command-row">
        <Button icon={<RefreshCw size={16} />} onClick={() => void controller.join()} variant="secondary">连接/重连</Button>
        <Button onClick={() => void controller.requestSnapshot()} variant="ghost">重新同步 snapshot</Button>
        <span>房间/Match：{matchId}</span>
        <span>当前玩家：{settings.playerId}</span>
      </section>
      <section className="status-grid">
        <article>
          <span className="eyebrow">服务端状态</span>
          <h2>{roomStatusLabel(roomStatus)}</h2>
          <p>Tick {snapshot?.tick ?? 0} / 第 {snapshot?.turnNumber ?? 0} 回合</p>
        </article>
        <article>
          <span className="eyebrow">胜者</span>
          <h2>{winnerPlayerId || "未决"}</h2>
          <p>胜利分数：{asNumber(timing.winningScore, 0) || "未公开"}</p>
        </article>
        {players.map(([playerId, player]) => {
          const view = asRecord(player);
          return (
            <article key={playerId}>
              <span className="eyebrow">{playerId}</span>
              <h2>{asNumber(view.score)} 分</h2>
              <p>经验 {asNumber(view.experience)} / {playerId === winnerPlayerId ? "胜者" : "非胜者"}</p>
            </article>
          );
        })}
      </section>
    </div>
  );
}

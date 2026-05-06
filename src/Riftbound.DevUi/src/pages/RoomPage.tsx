import { RefreshCw, Swords } from "lucide-react";
import { AppRoute } from "../app/router";
import { Button } from "../components/ui/Button";
import { StatusPill } from "../components/ui/StatusPill";
import { useMatchController } from "../stores/useMatchController";
import { useSettings } from "../stores/settingsStore";
import { candidateListLabel } from "../components/match/ActionPanel";

export function RoomPage({ roomId, onNavigate }: { roomId: string; onNavigate: (route: AppRoute) => void }) {
  const { settings } = useSettings();
  const controller = useMatchController(settings.serverUrl, roomId, settings.playerId);
  const snapshot = controller.state.snapshot;

  return (
    <div className="page-grid">
      <section className="page-header">
        <div>
          <span className="eyebrow">房间</span>
          <h1>{roomId}</h1>
          <p>入座、提交卡组、准备和开局均通过 GameHub 服务端确认。</p>
        </div>
        <StatusPill tone={controller.state.status === "connected" ? "good" : "warn"}>{controller.state.status}</StatusPill>
      </section>
      <section className="room-actions">
        <Button icon={<RefreshCw size={18} />} onClick={() => void controller.join()}>连接/重连并入座</Button>
        <Button onClick={() => void controller.submitStarterDeck()} variant="secondary">提交测试卡组</Button>
        <Button onClick={() => void controller.ready()} variant="secondary">准备</Button>
        <Button icon={<Swords size={18} />} onClick={() => onNavigate({ name: "match", matchId: roomId })}>进入对战桌面</Button>
      </section>
      <section className="seat-grid">
        {Object.entries(snapshot?.players ?? {}).map(([playerId, player]) => (
          <article className="seat-card" key={playerId}>
            <span className="eyebrow">{player.seat ?? "席位"}</span>
            <h2>{player.name ?? playerId}</h2>
            <p>分数 {player.score ?? 0} / 手牌 {player.handSize ?? player.zones?.handHidden ?? player.zones?.hand?.length ?? 0}</p>
            <div className="player-pills">
              <StatusPill tone={player.deckSubmitted ? "good" : "warn"}>{player.deckSubmitted ? "已提交卡组" : "未提交卡组"}</StatusPill>
              <StatusPill tone={player.ready ? "good" : "neutral"}>{player.ready ? "已准备" : "未准备"}</StatusPill>
            </div>
          </article>
        ))}
        {!snapshot && <div className="empty-panel">尚未收到房间 snapshot，请先连接。</div>}
      </section>
      <section className="audit-banner">
        <strong>当前候选行动：</strong>
        <span>{candidateListLabel(controller.state.prompt)}</span>
      </section>
    </div>
  );
}

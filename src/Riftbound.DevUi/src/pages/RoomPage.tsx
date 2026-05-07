import { Check, Hourglass, RefreshCw, Send, Swords } from "lucide-react";
import { AppRoute } from "../app/router";
import { Button } from "../components/ui/Button";
import { StatusPill } from "../components/ui/StatusPill";
import { useMatchController } from "../stores/useMatchController";
import { useSettings } from "../stores/settingsStore";
import { candidateListLabel } from "../components/match/ActionPanel";
import { ActionPromptCandidateDto } from "../types/protocol";
import { promptActionLabel } from "../utils/formatters";

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
        <RoomPromptButtons
          candidates={controller.state.prompt?.candidates ?? []}
          onReady={() => void controller.ready()}
          onSubmitStarterDeck={() => void controller.submitStarterDeck()}
        />
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
      <section className="room-log-panel">
        <header>
          <div>
            <span className="eyebrow">房间日志</span>
            <h2>服务端消息</h2>
          </div>
          <StatusPill tone={controller.state.errors.length > 0 ? "bad" : "good"}>
            {controller.state.errors.length > 0 ? `${controller.state.errors.length} 个错误` : "无错误"}
          </StatusPill>
        </header>
        <p>{controller.state.lastSystemMessage ?? "等待服务端房间消息。"}</p>
        <div className="room-log-list">
          {controller.state.errors.length === 0 && controller.state.events.length === 0 && <span className="empty-hint">暂无服务端事件或错误。</span>}
          {controller.state.errors.map((error, index) => (
            <article className="room-log-entry is-error" key={`${error.code}-${index}`}>
              <strong>{error.code}</strong>
              <span>{error.message}</span>
            </article>
          ))}
          {controller.state.events.slice(0, 8).map((event, index) => (
            <article className="room-log-entry" key={`${event.kind}-${index}`}>
              <strong>{event.kind}</strong>
              <span>{event.description}</span>
            </article>
          ))}
        </div>
      </section>
    </div>
  );
}

function RoomPromptButtons({
  candidates,
  onReady,
  onSubmitStarterDeck
}: {
  candidates: ActionPromptCandidateDto[];
  onReady: () => void;
  onSubmitStarterDeck: () => void;
}) {
  if (candidates.length === 0) {
    return <span className="empty-hint">等待服务端候选。</span>;
  }

  return candidates.map((candidate) => {
    if (candidate.action === "SUBMIT_DECK") {
      return (
        <Button disabled={!candidate.enabled} icon={<Send size={16} />} key={candidate.action} onClick={onSubmitStarterDeck} title={candidate.reason} variant="secondary">
          {promptActionLabel(candidate)}
        </Button>
      );
    }

    if (candidate.action === "READY") {
      return (
        <Button disabled={!candidate.enabled} icon={<Check size={16} />} key={candidate.action} onClick={onReady} title={candidate.reason} variant="secondary">
          {promptActionLabel(candidate)}
        </Button>
      );
    }

    return (
      <Button disabled icon={<Hourglass size={16} />} key={candidate.action} title={candidate.reason} variant="ghost">
        {promptActionLabel(candidate)}
      </Button>
    );
  });
}

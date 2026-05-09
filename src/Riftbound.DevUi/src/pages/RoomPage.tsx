import { Check, RefreshCw, Send, Swords } from "lucide-react";
import { AppRoute } from "../app/router";
import { Button } from "../components/ui/Button";
import { StatusPill } from "../components/ui/StatusPill";
import { useMatchController } from "../stores/useMatchController";
import { useSettings } from "../stores/settingsStore";
import { candidateListLabel } from "../components/match/ActionPanel";
import { eventDescriptionLabel, eventKindLabel } from "../components/match/EventLog";
import { ActionPromptCandidateDto } from "../types/protocol";
import { errorCodeLabel, errorMessageLabel } from "../utils/errors";
import { connectionStatusLabel, connectionStatusTone, promptActionLabel, promptReasonTitle } from "../utils/formatters";

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
          <p>入座、提交卡组、准备和开局均通过服务端实时连接确认。</p>
        </div>
        <StatusPill tone={connectionStatusTone(controller.state.status)}>
          {connectionStatusLabel(controller.state.status)}
        </StatusPill>
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
      <RoomSetupChecklist
        candidateActions={controller.state.prompt?.candidates?.filter((candidate) => candidate.enabled).map((candidate) => candidate.action) ?? []}
        playerCount={Object.keys(snapshot?.players ?? {}).length}
        players={Object.values(snapshot?.players ?? {})}
      />
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
        {!snapshot && <div className="empty-panel">尚未收到房间快照，请先连接。</div>}
      </section>
      <section className="audit-banner">
        <strong>当前可提交行动：</strong>
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
              <strong>{errorCodeLabel(error.code)}</strong>
              <span>{errorMessageLabel(error)}</span>
            </article>
          ))}
          {controller.state.events.slice(0, 8).map((event, index) => (
            <article className="room-log-entry" key={`${event.kind}-${index}`}>
              <strong>{eventKindLabel(event.kind)}</strong>
              <span>{eventDescriptionLabel(event)}</span>
            </article>
          ))}
        </div>
      </section>
    </div>
  );
}

function RoomSetupChecklist({
  candidateActions,
  playerCount,
  players
}: {
  candidateActions: string[];
  playerCount: number;
  players: Array<{ deckSubmitted?: boolean; ready?: boolean }>;
}) {
  const submittedCount = players.filter((player) => player.deckSubmitted).length;
  const readyCount = players.filter((player) => player.ready).length;
  const canSubmitDeck = candidateActions.includes("SUBMIT_DECK");
  const canReady = candidateActions.includes("READY");

  return (
    <section className="room-flow-panel">
      <article>
        <strong>1. 入座</strong>
        <span>{playerCount}/2 名玩家由服务端快照确认。</span>
      </article>
      <article>
        <strong>2. 选择卡组</strong>
        <span>{submittedCount}/2 已提交；{canSubmitDeck ? "当前 prompt 可提交测试卡组。" : "等待服务端提交候选。"}</span>
      </article>
      <article>
        <strong>3. 准备开始</strong>
        <span>{readyCount}/2 已准备；{canReady ? "当前 prompt 可准备。" : "等待服务端准备候选。"}</span>
      </article>
    </section>
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
  const roomCandidates = candidates.filter((candidate) =>
    candidate.enabled && (candidate.action === "SUBMIT_DECK" || candidate.action === "READY"));

  if (roomCandidates.length === 0) {
    const hasOtherEnabledCandidate = candidates.some((candidate) => candidate.enabled);
    return <span className="empty-hint">{hasOtherEnabledCandidate ? "其他可提交行动请进入对战桌面。" : "等待服务端可提交候选。"}</span>;
  }

  return roomCandidates.map((candidate) => {
    if (candidate.action === "SUBMIT_DECK") {
      return (
        <Button icon={<Send size={16} />} key={candidate.action} onClick={onSubmitStarterDeck} title={promptReasonTitle(candidate.reason)} variant="secondary">
          {promptActionLabel(candidate)}
        </Button>
      );
    }

    if (candidate.action === "READY") {
      return (
        <Button icon={<Check size={16} />} key={candidate.action} onClick={onReady} title={promptReasonTitle(candidate.reason)} variant="secondary">
          {promptActionLabel(candidate)}
        </Button>
      );
    }

    return null;
  });
}

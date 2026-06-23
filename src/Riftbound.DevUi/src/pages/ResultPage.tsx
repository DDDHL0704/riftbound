import { ArrowLeft, Home, RefreshCw, Swords } from "lucide-react";
import { useEffect } from "react";
import { AppRoute } from "../app/router";
import { eventDescriptionLabel, eventKindLabel } from "../components/match/EventLog";
import { Button } from "../components/ui/Button";
import { StatusPill } from "../components/ui/StatusPill";
import { useSettings } from "../stores/settingsStore";
import { useMatchController } from "../stores/useMatchController";
import { asNumber, asRecord, asString } from "../utils/collections";
import { errorCodeLabel, errorMessageLabel } from "../utils/errors";
import { connectionStatusLabel, connectionStatusTone, roomStatusLabel, roomStatusTone } from "../utils/formatters";

export function ResultPage({ matchId, onNavigate }: { matchId: string; onNavigate: (route: AppRoute) => void }) {
  const { settings } = useSettings();
  const controller = useMatchController(settings.serverUrl, matchId, settings.playerId);
  const snapshot = controller.state.snapshot;
  const timing = asRecord(snapshot?.timing);
  const roomStatus = asString(timing.roomStatus, "未知");
  const winnerPlayerId = asString(timing.winnerPlayerId, "");
  const players = Object.entries(snapshot?.players ?? {});
  const finalState = resultFinalState({
    hasSnapshot: Boolean(snapshot),
    roomStatus,
    winnerPlayerId
  });
  const latestEvent = controller.state.events[0];
  const latestError = controller.state.errors[0];

  useEffect(() => {
    void controller.join().catch(() => undefined);
  }, [controller.join]);

  return (
    <div className="page-grid">
      <section className="page-header">
        <div>
          <span className="eyebrow">结算</span>
          <h1>{winnerPlayerId ? `胜者：${winnerPlayerId}` : matchId}</h1>
          <p>结果只读取服务端权威快照，不根据本地分数推断胜负。</p>
        </div>
        <StatusPill tone={roomStatusTone(roomStatus)}>{roomStatusLabel(roomStatus)}</StatusPill>
      </section>
      <section className="match-command-row">
        <Button icon={<ArrowLeft size={16} />} onClick={() => onNavigate({ name: "room", roomId: matchId })} variant="secondary">返回房间</Button>
        <Button icon={<Home size={16} />} onClick={() => onNavigate({ name: "lobby" })} variant="ghost">返回大厅</Button>
        <Button icon={<Swords size={16} />} onClick={() => onNavigate({ name: "match", matchId })} variant="ghost">查看对战桌面</Button>
        <Button icon={<RefreshCw size={16} />} onClick={() => void controller.join()} variant="secondary">连接/重连</Button>
        <Button onClick={() => void controller.requestSnapshot()} variant="ghost">重新同步快照</Button>
        <span>房间/对局：{matchId}</span>
        <span>当前玩家：{settings.playerId}</span>
      </section>
      <section className="status-grid">
        <article>
          <span className="eyebrow">最终状态</span>
          <h2>{finalState.label}</h2>
          <p>{finalState.detail}</p>
          <StatusPill tone={finalState.tone}>{roomStatusLabel(roomStatus)}</StatusPill>
        </article>
        <article>
          <span className="eyebrow">胜者</span>
          <h2>{winnerPlayerId || "未决"}</h2>
          <p>胜利分数：{asNumber(timing.winningScore, 0) || "未公开"}</p>
        </article>
        <article>
          <span className="eyebrow">事件入口</span>
          <h2>{controller.state.events.length} 条</h2>
          <p>{latestEvent ? `${eventKindLabel(latestEvent.kind)}：${eventDescriptionLabel(latestEvent)}` : "尚未收到服务端事件。"}</p>
        </article>
        <article>
          <span className="eyebrow">错误入口</span>
          <h2>{controller.state.errors.length} 个</h2>
          <p>{latestError ? `${errorCodeLabel(latestError.code)}：${errorMessageLabel(latestError)}` : "没有服务端错误。"}</p>
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
      <section className="room-log-panel">
        <header>
          <div>
            <span className="eyebrow">结算日志入口</span>
            <h2>事件 / 错误</h2>
          </div>
          <StatusPill tone={controller.state.errors.length > 0 ? "bad" : controller.state.events.length > 0 ? "good" : "neutral"}>
            {controller.state.errors.length > 0 ? `${controller.state.errors.length} 个错误` : `${controller.state.events.length} 条事件`}
          </StatusPill>
        </header>
        <p>
          连接状态：{connectionStatusLabel(controller.state.status)}；服务端帧 {snapshot?.tick ?? 0} / 第 {snapshot?.turnNumber ?? 0} 回合。
        </p>
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
      <section className="audit-banner">
        <strong>返回路径：</strong>
        <span>房间用于继续入座/准备流程，大厅用于选择新房间，对战桌面用于查看事件和错误上下文。</span>
        <StatusPill tone={connectionStatusTone(controller.state.status)}>{connectionStatusLabel(controller.state.status)}</StatusPill>
      </section>
    </div>
  );
}

function resultFinalState({
  hasSnapshot,
  roomStatus,
  winnerPlayerId
}: {
  hasSnapshot: boolean;
  roomStatus: string;
  winnerPlayerId: string;
}): { detail: string; label: string; tone: "neutral" | "good" | "warn" | "bad" | "info" } {
  if (!hasSnapshot) {
    return {
      detail: "尚未收到服务端结果快照，请连接或重新同步。",
      label: "等待结果快照",
      tone: "warn"
    };
  }

  if (roomStatus === "FINISHED") {
    return {
      detail: winnerPlayerId ? `服务端宣告胜者为 ${winnerPlayerId}。` : "服务端已结束对局，但尚未公开胜者。",
      label: "服务端已结算",
      tone: "good"
    };
  }

  if (roomStatus === "IN_PROGRESS") {
    return {
      detail: "对局仍在进行，结果页只显示当前权威快照。",
      label: "尚未结算",
      tone: "info"
    };
  }

  return {
    detail: "服务端尚未进入结束状态，继续从房间或对战桌面查看流程。",
    label: "等待最终状态",
    tone: "warn"
  };
}

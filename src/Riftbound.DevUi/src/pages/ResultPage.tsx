import { ArrowLeft, Home, RefreshCw, Swords, UserRound } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { AppRoute } from "../app/router";
import { eventDescriptionLabel, eventKindLabel } from "../components/match/EventLog";
import { Button } from "../components/ui/Button";
import { StatusPill } from "../components/ui/StatusPill";
import { ApiClient } from "../services/apiClient";
import { useSettings } from "../stores/settingsStore";
import { useMatchController } from "../stores/useMatchController";
import { PlayerMatchDto } from "../types/protocol";
import { asNumber, asRecord, asString } from "../utils/collections";
import { errorCodeLabel, errorMessageLabel } from "../utils/errors";
import { connectionStatusLabel, connectionStatusTone, roomStatusLabel, roomStatusTone } from "../utils/formatters";

type ResultRecordStatus = {
  match?: PlayerMatchDto;
  message: string;
  state: "checking" | "error" | "recorded" | "unavailable" | "waiting";
};

export function ResultPage({ matchId, onNavigate }: { matchId: string; onNavigate: (route: AppRoute) => void }) {
  const { settings } = useSettings();
  const api = useMemo(() => new ApiClient(settings.serverUrl), [settings.serverUrl]);
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
  const [recordStatus, setRecordStatus] = useState<ResultRecordStatus>({
    message: "正在查询公开终局记录。",
    state: "checking"
  });
  const resultActionCount = 5;

  const refreshRecordStatus = useCallback(
    async (signal?: AbortSignal) => {
      const playerId = settings.playerId.trim();
      if (!playerId) {
        setRecordStatus({
          message: "当前玩家名为空，无法查询公开终局记录。",
          state: "unavailable"
        });
        return;
      }

      setRecordStatus({
        message: "正在查询公开终局记录。",
        state: "checking"
      });

      try {
        const matches = await api.playerMatches(playerId, 50, signal);
        if (signal?.aborted) {
          return;
        }

        const recordedMatch = matches.find((match) => match.roomId === matchId);
        if (recordedMatch) {
          setRecordStatus({
            match: recordedMatch,
            message: `公开终局已记录，胜者 ${recordedMatch.winnerPlayerId}。`,
            state: "recorded"
          });
          return;
        }

        setRecordStatus({
          message: roomStatus === "FINISHED" ? "服务端已结算，公开记录仍在等待查询命中。" : "对局尚未进入公开终局记录。",
          state: "waiting"
        });
      } catch (error) {
        if (error instanceof Error && error.name === "AbortError") {
          return;
        }

        setRecordStatus({
          message: error instanceof Error ? error.message : "记录查询失败。",
          state: "error"
        });
      }
    },
    [api, matchId, roomStatus, settings.playerId]
  );

  useEffect(() => {
    void controller.join().catch(() => undefined);
  }, [controller.join]);

  useEffect(() => {
    const abort = new AbortController();
    void refreshRecordStatus(abort.signal);
    return () => abort.abort();
  }, [refreshRecordStatus]);

  return (
    <div
      className="page-grid"
      data-result-authority="server-snapshot"
      data-result-has-snapshot={snapshot ? "true" : "false"}
      data-result-match-id={matchId}
      data-result-player-id={settings.playerId}
      data-result-room-status={roomStatus}
      data-result-snapshot-tick={snapshot?.tick ?? 0}
      data-result-state={finalState.state}
      data-result-surface
      data-result-winner-player-id={winnerPlayerId}
    >
      <section className="page-header">
        <div>
          <span className="eyebrow">结算</span>
          <h1>{winnerPlayerId ? `胜者：${winnerPlayerId}` : matchId}</h1>
          <p>结果只读取服务端权威快照，不根据本地分数推断胜负。</p>
        </div>
        <StatusPill tone={roomStatusTone(roomStatus)}>{roomStatusLabel(roomStatus)}</StatusPill>
      </section>
      <section className="match-command-row">
        <Button
          data-result-action="room"
          data-result-action-route="room"
          data-result-action-state="available"
          icon={<ArrowLeft size={16} />}
          onClick={() => onNavigate({ name: "room", roomId: matchId })}
          variant="secondary"
        >
          返回房间
        </Button>
        <Button
          data-result-action="lobby"
          data-result-action-route="lobby"
          data-result-action-state="available"
          icon={<Home size={16} />}
          onClick={() => onNavigate({ name: "lobby" })}
          variant="ghost"
        >
          返回大厅
        </Button>
        <Button
          data-result-action="match"
          data-result-action-route="match"
          data-result-action-state="available"
          icon={<Swords size={16} />}
          onClick={() => onNavigate({ name: "match", matchId })}
          variant="ghost"
        >
          查看对战桌面
        </Button>
        <Button
          data-result-action="connect"
          data-result-action-route="connection"
          data-result-action-state="available"
          icon={<RefreshCw size={16} />}
          onClick={() => void controller.join()}
          variant="secondary"
        >
          连接/重连
        </Button>
        <Button
          data-result-action="resync"
          data-result-action-route="snapshot"
          data-result-action-state={snapshot ? "available" : "waiting-snapshot"}
          onClick={() => void controller.requestSnapshot()}
          variant="ghost"
        >
          重新同步快照
        </Button>
        <span>房间/对局：{matchId}</span>
        <span>当前玩家：{settings.playerId}</span>
      </section>
      <section className="status-grid">
        <article
          data-result-authority="server-snapshot"
          data-result-final-label={finalState.label}
          data-result-final-state={finalState.state}
          data-result-has-snapshot={snapshot ? "true" : "false"}
        >
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
        <article
          data-result-recorded-player-id={settings.playerId}
          data-result-recorded-room-id={recordStatus.match?.roomId ?? ""}
          data-result-recorded-status={recordStatus.state}
        >
          <span className="eyebrow">战绩记录</span>
          <h2>{recordStatusLabel(recordStatus)}</h2>
          <p>{recordStatus.message}</p>
          <div className="result-record-actions">
            <StatusPill tone={recordStatusTone(recordStatus)}>{recordStatus.state}</StatusPill>
            <Button icon={<RefreshCw size={16} />} onClick={() => void refreshRecordStatus()} variant="ghost">
              刷新记录
            </Button>
            <Button icon={<UserRound size={16} />} onClick={() => onNavigate({ name: "profile", handle: settings.playerId.trim() || winnerPlayerId || "player" })} variant="ghost">
              资料
            </Button>
          </div>
        </article>
        <article data-result-event-count={controller.state.events.length} data-result-event-summary>
          <span className="eyebrow">事件入口</span>
          <h2>{controller.state.events.length} 条</h2>
          <p>{latestEvent ? `${eventKindLabel(latestEvent.kind)}：${eventDescriptionLabel(latestEvent)}` : "尚未收到服务端事件。"}</p>
        </article>
        <article data-result-error-count={controller.state.errors.length} data-result-error-summary>
          <span className="eyebrow">错误入口</span>
          <h2>{controller.state.errors.length} 个</h2>
          <p>{latestError ? `${errorCodeLabel(latestError.code)}：${errorMessageLabel(latestError)}` : "没有服务端错误。"}</p>
        </article>
        {players.map(([playerId, player]) => {
          const view = asRecord(player);
          const score = asNumber(view.score);
          return (
            <article
              data-result-player-id={playerId}
              data-result-player-score={score}
              data-result-player-winner={playerId === winnerPlayerId ? "true" : "false"}
              key={playerId}
            >
              <span className="eyebrow">{playerId}</span>
              <h2>{score} 分</h2>
              <p>经验 {asNumber(view.experience)} / {playerId === winnerPlayerId ? "胜者" : "非胜者"}</p>
            </article>
          );
        })}
      </section>
      <section
        className="room-log-panel"
        data-result-error-count={controller.state.errors.length}
        data-result-event-count={controller.state.events.length}
      >
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
            <article className="room-log-entry is-error" data-result-log-entry="error" data-result-log-kind={error.code} key={`${error.code}-${index}`}>
              <strong>{errorCodeLabel(error.code)}</strong>
              <span>{errorMessageLabel(error)}</span>
            </article>
          ))}
          {controller.state.events.slice(0, 8).map((event, index) => (
            <article className="room-log-entry" data-result-log-entry="event" data-result-log-kind={event.kind} key={`${event.kind}-${index}`}>
              <strong>{eventKindLabel(event.kind)}</strong>
              <span>{eventDescriptionLabel(event)}</span>
            </article>
          ))}
        </div>
      </section>
      <section
        className="audit-banner"
        data-result-return-action-count={resultActionCount}
        data-result-return-path
      >
        <strong>返回路径：</strong>
        <span>房间用于继续入座/准备流程，大厅用于选择新房间，对战桌面用于查看事件和错误上下文。</span>
        <StatusPill tone={connectionStatusTone(controller.state.status)}>{connectionStatusLabel(controller.state.status)}</StatusPill>
      </section>
    </div>
  );
}

function recordStatusLabel(status: ResultRecordStatus): string {
  switch (status.state) {
    case "recorded":
      return "本局已记录";
    case "checking":
      return "查询中";
    case "waiting":
      return "等待记录";
    case "unavailable":
      return "不可查询";
    case "error":
      return "记录查询失败";
  }
}

function recordStatusTone(status: ResultRecordStatus): "neutral" | "good" | "warn" | "bad" | "info" {
  switch (status.state) {
    case "recorded":
      return "good";
    case "checking":
      return "info";
    case "waiting":
      return "warn";
    case "unavailable":
      return "neutral";
    case "error":
      return "bad";
  }
}

function resultFinalState({
  hasSnapshot,
  roomStatus,
  winnerPlayerId
}: {
  hasSnapshot: boolean;
  roomStatus: string;
  winnerPlayerId: string;
}): { detail: string; label: string; state: "finished" | "in-progress" | "waiting-final" | "waiting-snapshot"; tone: "neutral" | "good" | "warn" | "bad" | "info" } {
  if (!hasSnapshot) {
    return {
      detail: "尚未收到服务端结果快照，请连接或重新同步。",
      label: "等待结果快照",
      state: "waiting-snapshot",
      tone: "warn"
    };
  }

  if (roomStatus === "FINISHED") {
    return {
      detail: winnerPlayerId ? `服务端宣告胜者为 ${winnerPlayerId}。` : "服务端已结束对局，但尚未公开胜者。",
      label: "服务端已结算",
      state: "finished",
      tone: "good"
    };
  }

  if (roomStatus === "IN_PROGRESS") {
    return {
      detail: "对局仍在进行，结果页只显示当前权威快照。",
      label: "尚未结算",
      state: "in-progress",
      tone: "info"
    };
  }

  return {
    detail: "服务端尚未进入结束状态，继续从房间或对战桌面查看流程。",
    label: "等待最终状态",
    state: "waiting-final",
    tone: "warn"
  };
}

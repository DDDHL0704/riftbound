import { Globe2, LogIn, Plus, RefreshCw, Search, UsersRound, X } from "lucide-react";
import { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { AppRoute } from "../app/router";
import { Button } from "../components/ui/Button";
import { ApiClient } from "../services/apiClient";
import { MatchSocket } from "../services/matchSocket";
import { useSettings } from "../stores/settingsStore";
import { rememberPlayerSession } from "../stores/useMatchController";
import { MatchmakingStatusDto, PublicMatchDto } from "../types/protocol";
import { getOrCreatePlayerKey } from "../utils/playerKey";
import { generateRoomCode } from "../utils/roomCode";

type QueueState = "idle" | "authenticating" | "queued" | "matched" | "error";

export function LobbyPage({ onNavigate }: { onNavigate: (route: AppRoute) => void }) {
  const { settings, updateSettings } = useSettings();
  const [roomId, setRoomId] = useState("");
  const [queueState, setQueueState] = useState<QueueState>("idle");
  const [statusMessage, setStatusMessage] = useState("选择快速匹配，或公开一个等待房。");
  const [publicMatches, setPublicMatches] = useState<PublicMatchDto[]>([]);
  const [publicMatchesLoading, setPublicMatchesLoading] = useState(false);
  const [publicMatchesError, setPublicMatchesError] = useState<string | null>(null);
  const socketRef = useRef<MatchSocket | undefined>(undefined);
  const socketServerUrlRef = useRef<string>("");
  const api = useMemo(() => new ApiClient(settings.serverUrl), [settings.serverUrl]);

  const createRoom = () => {
    onNavigate({ name: "room", roomId: generateRoomCode() });
  };

  const joinRoom = (event: FormEvent) => {
    event.preventDefault();
    if (roomId.trim()) {
      onNavigate({ name: "room", roomId: roomId.trim() });
    }
  };

  const handleMatchmakingStatus = useCallback(
    (status: MatchmakingStatusDto) => {
      if (status.state === "MATCHED" && status.roomId && status.playerSession) {
        rememberPlayerSession(status.roomId, status.playerSession);
        updateSettings({ playerId: status.playerSession.playerId });
        setQueueState("matched");
        setStatusMessage(`已匹配 ${status.opponentPlayerId ?? "对手"}，正在进入房间。`);
        onNavigate({ name: "room", roomId: status.roomId });
        return;
      }

      if (status.state === "QUEUED") {
        setQueueState("queued");
        setStatusMessage("已进入快速匹配队列，等待下一名玩家。");
        return;
      }

      if (status.state === "CANCELLED" || status.state === "IDLE") {
        setQueueState("idle");
        setStatusMessage("已离开快速匹配队列。");
        return;
      }

      if (status.state === "REJECTED") {
        setQueueState("error");
        setStatusMessage(status.message ?? "匹配请求被服务端拒绝。");
      }
    },
    [onNavigate, updateSettings]
  );

  const ensureSocket = useCallback(() => {
    if (socketRef.current && socketServerUrlRef.current === settings.serverUrl) {
      return socketRef.current;
    }

    socketRef.current?.disconnect().catch(() => undefined);
    socketServerUrlRef.current = settings.serverUrl;
    socketRef.current = new MatchSocket(settings.serverUrl, {
      onJoined: () => undefined,
      onSnapshot: () => undefined,
      onPrompt: () => undefined,
      onEvents: () => undefined,
      onMatchmaking: (message) => handleMatchmakingStatus(message.payload),
      onError: (message) => {
        setQueueState("error");
        setStatusMessage(message.payload.message);
      },
      onStatus: () => undefined
    });
    return socketRef.current;
  }, [handleMatchmakingStatus, settings.serverUrl]);

  useEffect(() => () => {
    socketRef.current?.disconnect().catch(() => undefined);
    socketRef.current = undefined;
  }, []);

  const authenticateLobbySocket = useCallback(async () => {
    const socket = ensureSocket();
    await socket.connect();
    const auth = await socket.authenticate(settings.playerId, getOrCreatePlayerKey());
    if (!auth.authenticated && auth.status !== "IDENTITY_NOT_CONFIGURED") {
      setQueueState("error");
      setStatusMessage(identityRejectionMessage(auth.status));
      return null;
    }

    if (auth.handle && auth.handle !== settings.playerId) {
      updateSettings({ playerId: auth.handle });
    }

    return auth.handle || settings.playerId.trim();
  }, [ensureSocket, settings.playerId, updateSettings]);

  const refreshPublicMatches = useCallback(async () => {
    setPublicMatchesLoading(true);
    setPublicMatchesError(null);
    try {
      setPublicMatches(await api.publicMatches());
    } catch (error) {
      setPublicMatchesError(error instanceof Error ? error.message : "公开对局列表刷新失败。");
    } finally {
      setPublicMatchesLoading(false);
    }
  }, [api]);

  useEffect(() => {
    void refreshPublicMatches();
  }, [refreshPublicMatches]);

  const startQuickMatch = useCallback(async () => {
    setQueueState("authenticating");
    setStatusMessage("正在认证并进入快速匹配。");
    try {
      const playerId = await authenticateLobbySocket();
      if (!playerId) {
        return;
      }

      const status = await ensureSocket().enqueueMatchmaking(playerId);
      handleMatchmakingStatus(status);
    } catch (error) {
      setQueueState("error");
      setStatusMessage(error instanceof Error ? error.message : "快速匹配失败。");
    }
  }, [authenticateLobbySocket, ensureSocket, handleMatchmakingStatus]);

  const cancelQuickMatch = useCallback(async () => {
    try {
      const playerId = await authenticateLobbySocket();
      if (!playerId) {
        return;
      }

      const status = await ensureSocket().cancelMatchmaking(playerId);
      handleMatchmakingStatus(status);
    } catch (error) {
      setQueueState("error");
      setStatusMessage(error instanceof Error ? error.message : "取消匹配失败。");
    }
  }, [authenticateLobbySocket, ensureSocket, handleMatchmakingStatus]);

  const createPublicMatch = useCallback(async () => {
    setStatusMessage("正在创建公开等待房。");
    try {
      const playerId = await authenticateLobbySocket();
      if (!playerId) {
        return;
      }

      const result = await ensureSocket().createPublicMatch(playerId);
      if (!result) {
        setStatusMessage("公开房创建被服务端拒绝。");
        return;
      }

      rememberPlayerSession(result.match.roomId, result.playerSession);
      updateSettings({ playerId: result.playerSession.playerId });
      setStatusMessage(`公开房 ${result.match.roomId} 已创建。`);
      await refreshPublicMatches();
      onNavigate({ name: "room", roomId: result.match.roomId });
    } catch (error) {
      setQueueState("error");
      setStatusMessage(error instanceof Error ? error.message : "公开房创建失败。");
    }
  }, [authenticateLobbySocket, ensureSocket, onNavigate, refreshPublicMatches, updateSettings]);

  return (
    <div className="page-grid lobby-page" data-play-lobby>
      <section className="page-header">
        <div>
          <span className="eyebrow">大厅</span>
          <h1>开始一场对局</h1>
          <p>快速匹配一位对手，或使用房间码和朋友开局。</p>
        </div>
        <Button icon={<Plus size={18} />} onClick={createRoom}>创建私人房间</Button>
      </section>
      <div className="lobby-content-grid">
        <section className="lobby-discovery-panel lobby-quick-match" aria-label="寻找对手">
          <div className="lobby-discovery-header">
            <div>
              <span className="eyebrow">快速对战</span>
              <h2>寻找一位在线对手</h2>
            </div>
            <span className={`status-pill ${queueState === "error" ? "status-bad" : queueState === "queued" ? "status-info" : "status-neutral"}`}>
              {queueStateLabel(queueState)}
            </span>
          </div>
          <p className="lobby-status-line">{statusMessage || "使用当前预构筑卡组进入 1v1 匹配。"}</p>
          <div className="lobby-action-row">
            <Button disabled={queueState === "authenticating" || queueState === "queued"} icon={<Search size={17} />} onClick={startQuickMatch}>
              快速匹配
            </Button>
            <Button disabled={queueState !== "queued"} icon={<X size={17} />} onClick={cancelQuickMatch} variant="secondary">
              取消匹配
            </Button>
            <Button icon={<Globe2 size={17} />} onClick={createPublicMatch} variant="secondary">
              创建公开房
            </Button>
            <Button disabled={publicMatchesLoading} icon={<RefreshCw size={17} />} onClick={refreshPublicMatches} variant="ghost">
              刷新列表
            </Button>
          </div>
          <div className="public-match-list-header">
            <span>公开对局</span>
            <small>{publicMatchesLoading ? "刷新中" : `${publicMatches.length} 个等待房`}</small>
          </div>
          {publicMatchesError ? <p className="lobby-error-line">{publicMatchesError}</p> : null}
          <ul className="public-match-list">
            {publicMatches.map((match) => (
              <li className="public-match-row" key={match.roomId}>
                <div>
                  <strong>{match.roomId}</strong>
                  <span className="public-match-meta">
                    <UsersRound size={14} />
                    {match.hostPlayerId} · {match.seatCount}/{match.capacity}
                  </span>
                </div>
                <Button icon={<LogIn size={16} />} onClick={() => onNavigate({ name: "room", roomId: match.roomId })} variant="secondary">
                  加入
                </Button>
              </li>
            ))}
          </ul>
          {publicMatches.length === 0 && !publicMatchesLoading ? <p className="empty-hint">暂无公开等待房。</p> : null}
        </section>
        <form className="lobby-form lobby-join-room" onSubmit={joinRoom}>
          <div className="lobby-form-heading">
            <span className="eyebrow">好友对战</span>
            <h2>加入私人房间</h2>
          </div>
          <label>
            <span>玩家名称</span>
            <input value={settings.playerId} onChange={(event) => updateSettings({ playerId: event.target.value })} />
          </label>
          <label>
            <span>房间码</span>
            <input value={roomId} onChange={(event) => setRoomId(event.target.value)} placeholder="输入邀请房间码" />
          </label>
          <Button icon={<LogIn size={18} />} type="submit">加入房间</Button>
          <details className="lobby-server-settings">
            <summary>连接设置</summary>
            <label>
              <span>服务端地址</span>
              <input value={settings.serverUrl} onChange={(event) => updateSettings({ serverUrl: event.target.value })} />
            </label>
          </details>
        </form>
      </div>
    </div>
  );
}

function queueStateLabel(state: QueueState): string {
  switch (state) {
    case "authenticating":
      return "连接中";
    case "queued":
      return "队列中";
    case "matched":
      return "已匹配";
    case "error":
      return "需处理";
    default:
      return "空闲";
  }
}

function identityRejectionMessage(status: string): string {
  switch (status) {
    case "HandleClaimed":
      return "该玩家名已被其他设备占用。";
    case "InvalidHandle":
      return "玩家名不能为空。";
    case "WeakKey":
      return "本地身份密钥无效，请刷新页面后重试。";
    default:
      return "身份校验未通过。";
  }
}

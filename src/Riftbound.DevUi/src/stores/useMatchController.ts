import { useCallback, useMemo, useRef, useState } from "react";
import { MatchSocket } from "../services/matchSocket";
import { buildStarterDeck } from "../services/starterDeck";
import {
  ActionPromptDto,
  ConnectionStatus,
  ErrorDto,
  GameCommand,
  GameEvent,
  PlayerSessionDto,
  SnapshotDto,
  WsServerMessage
} from "../types/protocol";

export type MatchControllerState = {
  status: ConnectionStatus;
  session?: PlayerSessionDto;
  snapshot?: SnapshotDto;
  prompt?: ActionPromptDto;
  events: GameEvent[];
  errors: ErrorDto[];
  lastSystemMessage?: string;
};

export function useMatchController(serverUrl: string, roomId: string, playerId: string) {
  const [state, setState] = useState<MatchControllerState>({
    status: "idle",
    events: [],
    errors: []
  });
  const socketRef = useRef<MatchSocket | undefined>(undefined);

  const rememberSession = useCallback(
    (session: PlayerSessionDto) => {
      localStorage.setItem(sessionKey(roomId, session.playerId), JSON.stringify(session));
    },
    [roomId]
  );

  const socket = useMemo(() => {
    socketRef.current?.disconnect().catch(() => undefined);
    const nextSocket = new MatchSocket(serverUrl, {
      onJoined: (message: WsServerMessage<PlayerSessionDto>) => {
        rememberSession(message.payload);
        setState((current) => ({
          ...current,
          session: message.payload,
          lastSystemMessage: `${message.payload.seat} 已进入房间`
        }));
      },
      onSnapshot: (message: WsServerMessage<SnapshotDto>) => {
        setState((current) => ({ ...current, snapshot: message.payload }));
      },
      onPrompt: (message: WsServerMessage<ActionPromptDto>) => {
        setState((current) => ({ ...current, prompt: message.payload }));
      },
      onEvents: (message: WsServerMessage<GameEvent[]>) => {
        setState((current) => ({
          ...current,
          events: [...message.payload, ...current.events].slice(0, 160)
        }));
      },
      onError: (message: WsServerMessage<ErrorDto>) => {
        setState((current) => ({
          ...current,
          errors: [message.payload, ...current.errors].slice(0, 20),
          lastSystemMessage: message.payload.message
        }));
      },
      onStatus: (status) => setState((current) => ({ ...current, status }))
    });
    socketRef.current = nextSocket;
    return nextSocket;
  }, [rememberSession, serverUrl]);

  const join = useCallback(async () => {
    const stored = loadSession(roomId, playerId);
    await socket.connect();
    if (stored?.reconnectToken) {
      try {
        await socket.reconnect(roomId, playerId, stored.reconnectToken);
        return;
      } catch {
        forgetSession(roomId, playerId);
        setState((current) => ({
          ...current,
          lastSystemMessage: "重连凭据已过期，正在重新入座"
        }));
      }
    }

    await socket.joinRoom(roomId, playerId);
  }, [playerId, roomId, socket]);

  const requestSnapshot = useCallback(async () => {
    setState((current) => ({ ...current, status: "resyncing" }));
    try {
      await socket.requestSnapshot(roomId, playerId);
      setState((current) => ({ ...current, status: "connected" }));
    } catch (error) {
      setState((current) => ({
        ...current,
        status: "error",
        lastSystemMessage: error instanceof Error ? error.message : "重新同步失败"
      }));
      throw error;
    }
  }, [playerId, roomId, socket]);

  const ready = useCallback(async () => {
    await socket.ready(roomId, playerId, intentId(playerId, "READY"));
  }, [playerId, roomId, socket]);

  const submitCommand = useCallback(
    async (command: GameCommand) => {
      await socket.submitIntent(roomId, playerId, intentId(playerId, command.cmdType), command);
    },
    [playerId, roomId, socket]
  );

  const submitStarterDeck = useCallback(async () => {
    await submitCommand(buildStarterDeck());
  }, [submitCommand]);

  const disconnect = useCallback(async () => {
    await socket.disconnect();
    setState((current) => ({ ...current, status: "disconnected" }));
  }, [socket]);

  return {
    state,
    join,
    ready,
    requestSnapshot,
    submitCommand,
    submitStarterDeck,
    disconnect
  };
}

function intentId(playerId: string, commandType: string): string {
  return `${playerId}-${commandType}-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function sessionKey(roomId: string, playerId: string): string {
  return `riftbound.session.${roomId}.${playerId}`;
}

function forgetSession(roomId: string, playerId: string): void {
  localStorage.removeItem(sessionKey(roomId, playerId));
}

function loadSession(roomId: string, playerId: string): PlayerSessionDto | undefined {
  const raw = localStorage.getItem(sessionKey(roomId, playerId));
  if (!raw) {
    return undefined;
  }

  try {
    return JSON.parse(raw) as PlayerSessionDto;
  } catch {
    return undefined;
  }
}

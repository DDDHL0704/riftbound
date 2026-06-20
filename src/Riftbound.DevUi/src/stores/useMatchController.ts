import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { MatchSocket } from "../services/matchSocket";
import { buildStarterDeck } from "../services/starterDeck";
import {
  ActionPromptDto,
  CommandReceiptDto,
  ConnectionStatus,
  ErrorDto,
  GameCommand,
  GameEvent,
  PlayerSessionDto,
  SnapshotDto,
  WsServerMessage
} from "../types/protocol";
import { errorMessageLabel } from "../utils/errors";
import type { ObservedGameEvent } from "../utils/commandSubmissionFollowupPlan";

export type MatchControllerState = {
  status: ConnectionStatus;
  session?: PlayerSessionDto;
  snapshot?: SnapshotDto;
  prompt?: ActionPromptDto;
  events: ObservedGameEvent[];
  errors: ErrorDto[];
  lastCommandSubmission?: CommandSubmissionFeedback;
  lastSystemMessage?: string;
};

export type CommandSubmissionState = "failed" | "sent" | "submitting";

export type CommandSubmissionFeedback = {
  clientIntentId: string;
  cmdType: string;
  message: string;
  errorCode?: string | null;
  promptId?: string | null;
  receiptState?: string | null;
  serverTick?: number | null;
  snapshotTick?: number | null;
  state: CommandSubmissionState;
  stateLabel: string;
  submittedAt: number;
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
        const receivedAt = Date.now();
        const observedEvents = message.payload.map((event, index) => ({
          ...event,
          receivedAt,
          receivedBatchIndex: index,
          receivedMessageType: message.type,
          receivedServerTick: message.serverTick
        }));
        setState((current) => ({
          ...current,
          events: [...observedEvents, ...current.events].slice(0, 160)
        }));
      },
      onError: (message: WsServerMessage<ErrorDto>) => {
        setState((current) => ({
          ...current,
          errors: [message.payload, ...current.errors].slice(0, 20),
          lastSystemMessage: errorMessageLabel(message.payload)
        }));
      },
      onStatus: (status) => setState((current) => ({ ...current, status }))
    });
    socketRef.current = nextSocket;
    return nextSocket;
  }, [rememberSession, serverUrl]);

  useEffect(() => () => {
    socketRef.current?.disconnect().catch(() => undefined);
    socketRef.current = undefined;
  }, []);

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

    try {
      await socket.joinRoom(roomId, playerId);
    } catch (error) {
      setState((current) => ({
        ...current,
        lastSystemMessage: isErrorDto(error) ? errorMessageLabel(error) : "入座失败，请稍后重试。"
      }));
    }
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

  const submitReceiptBackedCommand = useCallback(
    async (
      command: GameCommand,
      submit: (clientIntentId: string, stampedCommand: GameCommand) => Promise<CommandReceiptDto>
    ) => {
      const stampedCommand = withCurrentPromptStamp(command, state.prompt);
      const clientIntentId = intentId(playerId, command.cmdType);
      const pending = commandSubmissionFeedback({
        clientIntentId,
        command: stampedCommand,
        message: "命令已从前端发出，等待服务端入口确认。",
        state: "submitting",
        stateLabel: "提交中"
      });
      setState((current) => ({
        ...current,
        lastCommandSubmission: pending,
        lastSystemMessage: pending.message
      }));

      try {
        const receipt = await submit(clientIntentId, stampedCommand);
        const sent = receipt.accepted
          ? commandSubmissionFeedbackFromReceipt({
            command: stampedCommand,
            receipt,
            state: "sent",
            stateLabel: "服务端已接受"
          })
          : commandSubmissionFeedbackFromReceipt({
            command: stampedCommand,
            receipt,
            state: "failed",
            stateLabel: "服务端拒绝"
          });
        setState((current) => ({
          ...current,
          lastCommandSubmission: current.lastCommandSubmission?.clientIntentId === clientIntentId ? sent : current.lastCommandSubmission,
          lastSystemMessage: sent.message
        }));
      } catch (error) {
        const message = submitErrorMessage(error);
        const failed = commandSubmissionFeedback({
          clientIntentId,
          command: stampedCommand,
          message,
          state: "failed",
          stateLabel: "提交失败"
        });
        setState((current) => ({
          ...current,
          lastCommandSubmission: current.lastCommandSubmission?.clientIntentId === clientIntentId ? failed : current.lastCommandSubmission,
          lastSystemMessage: message
        }));
        throw error;
      }
    },
    [playerId, state.prompt]
  );

  const ready = useCallback(async () => {
    await submitReceiptBackedCommand(
      { cmdType: "READY" },
      (clientIntentId) => socket.ready(roomId, playerId, clientIntentId)
    );
  }, [playerId, roomId, socket, submitReceiptBackedCommand]);

  const submitCommand = useCallback(
    async (command: GameCommand) => {
      await submitReceiptBackedCommand(
        command,
        (clientIntentId, stampedCommand) => socket.submitIntent(roomId, playerId, clientIntentId, stampedCommand)
      );
    },
    [playerId, roomId, socket, submitReceiptBackedCommand]
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

function withCurrentPromptStamp(command: GameCommand, prompt: ActionPromptDto | undefined): GameCommand {
  if (!prompt || (command.promptId != null && command.snapshotTick != null)) {
    return command;
  }

  return {
    ...command,
    promptId: command.promptId ?? prompt.promptId ?? null,
    snapshotTick: command.snapshotTick ?? prompt.snapshotTick ?? null
  };
}

function commandSubmissionFeedback({
  clientIntentId,
  command,
  message,
  state,
  stateLabel
}: {
  clientIntentId: string;
  command: GameCommand;
  message: string;
  state: CommandSubmissionState;
  stateLabel: string;
}): CommandSubmissionFeedback {
  return {
    clientIntentId,
    cmdType: command.cmdType,
    message,
    promptId: typeof command.promptId === "string" ? command.promptId : command.promptId ?? undefined,
    receiptState: undefined,
    serverTick: undefined,
    snapshotTick: typeof command.snapshotTick === "number" ? command.snapshotTick : command.snapshotTick ?? undefined,
    state,
    stateLabel,
    submittedAt: Date.now()
  };
}

function commandSubmissionFeedbackFromReceipt({
  command,
  receipt,
  state,
  stateLabel
}: {
  command: GameCommand;
  receipt: CommandReceiptDto;
  state: CommandSubmissionState;
  stateLabel: string;
}): CommandSubmissionFeedback {
  return {
    clientIntentId: receipt.clientIntentId,
    cmdType: receipt.cmdType || command.cmdType,
    errorCode: receipt.errorCode ?? undefined,
    message: receipt.message,
    promptId: receipt.promptId ?? (typeof command.promptId === "string" ? command.promptId : command.promptId ?? undefined),
    receiptState: receipt.state,
    serverTick: receipt.serverTick,
    snapshotTick: typeof receipt.snapshotTick === "number"
      ? receipt.snapshotTick
      : typeof command.snapshotTick === "number"
        ? command.snapshotTick
        : command.snapshotTick ?? undefined,
    state,
    stateLabel,
    submittedAt: Date.now()
  };
}

function submitErrorMessage(error: unknown): string {
  if (isErrorDto(error)) {
    return errorMessageLabel(error);
  }

  return error instanceof Error ? error.message : "命令提交失败，等待重新同步。";
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

function isErrorDto(value: unknown): value is ErrorDto {
  return Boolean(
    value
    && typeof value === "object"
    && "code" in value
    && "message" in value
  );
}

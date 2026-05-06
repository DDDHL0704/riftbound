import * as signalR from "@microsoft/signalr";
import {
  ActionPromptDto,
  ErrorDto,
  GameCommand,
  GameEvent,
  PlayerSessionDto,
  SnapshotDto,
  WsServerMessage
} from "../types/protocol";
import { apiBase } from "./apiClient";

type MatchSocketHandlers = {
  onJoined: (message: WsServerMessage<PlayerSessionDto>) => void;
  onSnapshot: (message: WsServerMessage<SnapshotDto>) => void;
  onPrompt: (message: WsServerMessage<ActionPromptDto>) => void;
  onEvents: (message: WsServerMessage<GameEvent[]>) => void;
  onError: (message: WsServerMessage<ErrorDto>) => void;
  onStatus: (status: "connecting" | "connected" | "reconnecting" | "disconnected" | "error") => void;
};

export class MatchSocket {
  private connection?: signalR.HubConnection;

  constructor(
    private readonly serverUrl: string,
    private readonly handlers: MatchSocketHandlers
  ) {}

  async connect(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    this.handlers.onStatus("connecting");
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${apiBase(this.serverUrl)}/hubs/game`)
      .withAutomaticReconnect()
      .build();

    connection.onreconnecting(() => this.handlers.onStatus("reconnecting"));
    connection.onreconnected(() => this.handlers.onStatus("connected"));
    connection.onclose(() => this.handlers.onStatus("disconnected"));
    connection.on("Joined", this.handlers.onJoined);
    connection.on("Snapshot", this.handlers.onSnapshot);
    connection.on("Prompt", this.handlers.onPrompt);
    connection.on("Events", this.handlers.onEvents);
    connection.on("Error", this.handlers.onError);

    this.connection = connection;
    try {
      await connection.start();
      this.handlers.onStatus("connected");
    } catch (error) {
      this.handlers.onStatus("error");
      throw error;
    }
  }

  async joinRoom(roomId: string, playerId: string, reconnectToken?: string): Promise<void> {
    await this.invoke("JoinRoom", roomId, playerId, reconnectToken ?? null);
  }

  async reconnect(roomId: string, playerId: string, reconnectToken: string): Promise<void> {
    await this.invoke("Reconnect", roomId, playerId, reconnectToken);
  }

  async requestSnapshot(roomId: string, playerId: string): Promise<void> {
    await this.invoke("RequestSnapshot", roomId, playerId);
  }

  async ready(roomId: string, playerId: string, clientIntentId: string): Promise<void> {
    await this.invoke("Ready", roomId, playerId, clientIntentId);
  }

  async submitIntent(roomId: string, playerId: string, clientIntentId: string, command: GameCommand): Promise<void> {
    await this.invoke("SubmitIntent", roomId, playerId, clientIntentId, command);
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = undefined;
    }
  }

  private async invoke(method: string, ...args: unknown[]): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      await this.connect();
    }

    await this.connection!.invoke(method, ...args);
  }
}

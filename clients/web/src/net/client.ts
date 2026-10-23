import * as signalR from "@microsoft/signalr";
import type {
  CommandReceipt,
  GameEvent,
  PlayCostQuote,
  PlayerSession,
  Prompt,
  ServerMessage,
  Snapshot,
} from "../game/types";

export type LogEntry = { id: number; tick: number; kind: string; text: string; tone: "event" | "error" | "info" };

export type EventBatch = { seq: number; events: GameEvent[] };

export type Phase = "offline" | "connecting" | "lobby" | "matchmaking" | "room";

export type ClientState = {
  phase: Phase;
  server: string;
  handle: string;
  roomId: string;
  session: PlayerSession | null;
  snapshot: Snapshot | null;
  prompt: Prompt | null;
  log: LogEntry[];
  pending: boolean;
  toast: { id: number; text: string; tone: "error" | "info" } | null;
  connectionNote: string;
  /**
   * Recent event batches in arrival order. Several batches can land before React renders,
   * so consumers read every batch newer than the last one they handled.
   */
  eventBatches: EventBatch[];
};

// The room seat lives in sessionStorage so two tabs can hold two different players;
// handle keys and the last login live in localStorage so a name keeps its identity.
const SESSION_KEY = "riftbound.web.session";
const LAST_KEY = "riftbound.web.last";
const KEYS_KEY = "riftbound.web.keys";

type Saved = { server: string; handle: string; key: string; roomId?: string; reconnectToken?: string };

function readJson<T>(storage: () => Storage, key: string): T | null {
  try {
    const raw = storage().getItem(key);
    return raw ? (JSON.parse(raw) as T) : null;
  } catch {
    return null;
  }
}

function writeJson(storage: () => Storage, key: string, value: unknown) {
  try {
    if (value == null) storage().removeItem(key);
    else storage().setItem(key, JSON.stringify(value));
  } catch {
    /* storage unavailable: the session simply won't survive a reload */
  }
}

function keyFor(handle: string) {
  return readJson<Record<string, string>>(() => localStorage, KEYS_KEY)?.[handle];
}

function loadSaved(): Saved | null {
  const session = readJson<Saved>(() => sessionStorage, SESSION_KEY);
  if (session) return session;
  const last = readJson<{ server: string; handle: string }>(() => localStorage, LAST_KEY);
  return last ? { ...last, key: keyFor(last.handle) ?? "" } : null;
}

function save(saved: Saved | null) {
  writeJson(() => sessionStorage, SESSION_KEY, saved);
  if (!saved) return;
  writeJson(() => localStorage, LAST_KEY, { server: saved.server, handle: saved.handle });
  const keys = readJson<Record<string, string>>(() => localStorage, KEYS_KEY) ?? {};
  writeJson(() => localStorage, KEYS_KEY, { ...keys, [saved.handle]: saved.key });
}

function randomKey() {
  const bytes = new Uint8Array(18);
  crypto.getRandomValues(bytes);
  return Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("");
}

export function defaultServer() {
  const fromQuery = new URLSearchParams(location.search).get("server");
  return fromQuery || import.meta.env.VITE_SERVER || "http://127.0.0.1:5088";
}

let seq = 0;

// Error codes from src/Riftbound.Contracts/Protocol.cs. Most server messages are already
// Chinese; a few rule messages are English, so those fall back to the code's meaning.
const ERROR_TEXT: Record<string, string> = {
  INSUFFICIENT_COST: "费用不足，无法执行这个操作。",
  INVALID_TARGET: "选择的目标或选项不合法。",
  PHASE_NOT_ALLOWED: "当前时机不能这样操作。",
  PROMPT_EXPIRED: "局面已更新，请按最新提示重新操作。",
  INVALID_PAYLOAD: "提交的内容不完整，请重新选择。",
  CARD_NOT_IN_HAND: "这张牌已不在手牌中。",
  UNSUPPORTED_COMMAND: "服务端暂不支持这个操作。",
  UNSUPPORTED_CARD_BEHAVIOR: "这张牌的效果服务端暂未支持。",
  MATCH_FINISHED: "对局已经结束。",
  MATCH_NOT_STARTED: "对局尚未开始。",
  INVALID_DECK: "卡组不符合规则。",
  ROOM_FULL: "房间已满。",
  PLAYER_NOT_IN_ROOM: "你不在这个房间里。",
  INVALID_RECONNECT_TOKEN: "重连凭证已失效，请重新加入。",
};

const STALE_TEXT = "行动窗口已更新：局面刚刚变化，请按最新提示重新操作。";

export function readableError(code: string | undefined, message: string | undefined) {
  if (message && /[\u4e00-\u9fff]/.test(message)) return message;
  return (code && ERROR_TEXT[code]) || message || "服务端拒绝了这个操作。";
}

export class GameClient {
  private connection: signalR.HubConnection | null = null;
  private listeners = new Set<() => void>();
  private key = "";
  state: ClientState;

  constructor() {
    const saved = loadSaved();
    this.state = {
      phase: "offline",
      server: saved?.server || defaultServer(),
      handle: saved?.handle || "",
      roomId: saved?.roomId || "",
      session: null,
      snapshot: null,
      prompt: null,
      log: [],
      pending: false,
      toast: null,
      connectionNote: "",
      eventBatches: [],
    };
  }

  subscribe = (fn: () => void) => {
    this.listeners.add(fn);
    return () => this.listeners.delete(fn);
  };

  getState = () => this.state;

  private set(patch: Partial<ClientState>) {
    this.state = { ...this.state, ...patch };
    this.listeners.forEach((fn) => fn());
  }

  private pushLog(entries: Omit<LogEntry, "id">[]) {
    if (!entries.length) return;
    const log = [...this.state.log, ...entries.map((e) => ({ ...e, id: ++seq }))].slice(-300);
    this.set({ log });
  }

  toast(text: string, tone: "error" | "info" = "error") {
    this.set({ toast: { id: ++seq, text, tone } });
  }

  dismissToast() {
    this.set({ toast: null });
  }

  /** A saved room means a reload mid-match: rejoin it without showing the lobby. */
  hasResumableRoom() {
    const saved = loadSaved();
    return Boolean(saved?.handle && saved.key && saved.roomId && saved.reconnectToken);
  }

  async connect(server: string, handle: string) {
    const saved = loadSaved();
    const normalized = handle.trim();
    this.key = keyFor(normalized) || randomKey();
    this.set({ phase: "connecting", server, handle: normalized, connectionNote: "" });

    await this.connection?.stop().catch(() => undefined);
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${server.replace(/\/$/, "")}/hubs/game`)
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();
    this.connection = connection;

    connection.on("Joined", (m: ServerMessage<PlayerSession>) => {
      const session = m.payload;
      this.set({ session, roomId: m.roomId, phase: "room" });
      save({ server, handle: this.state.handle, key: this.key, roomId: m.roomId, reconnectToken: session.reconnectToken });
    });
    connection.on("Snapshot", (m: ServerMessage<Snapshot>) => {
      if (m.playerId !== this.state.handle) return;
      const current = this.state.snapshot;
      if (current && m.payload.tick < current.tick) return;
      this.set({ snapshot: m.payload });
    });
    connection.on("Prompt", (m: ServerMessage<Prompt>) => {
      if (m.playerId !== this.state.handle) return;
      this.set({ prompt: m.payload });
    });
    connection.on("Events", (m: ServerMessage<GameEvent[]>) => {
      const events = m.payload || [];
      this.set({ eventBatches: [...this.state.eventBatches, { seq: ++seq, events }].slice(-20) });
      this.pushLog(events.map((e) => ({ tick: m.serverTick, kind: e.kind, text: e.description, tone: "event" as const })));
    });
    connection.on("Error", (m: ServerMessage<{ code: string; message: string }>) => {
      const text = readableError(m.payload?.code, m.payload?.message);
      this.pushLog([{ tick: m.serverTick, kind: m.payload?.code || "ERROR", text, tone: "error" }]);
      if (m.payload?.code === "INVALID_RECONNECT_TOKEN" || m.payload?.code === "PLAYER_NOT_IN_ROOM") {
        this.leaveRoom();
      }
    });
    connection.on("Matchmaking", (m: ServerMessage<{ state: string; roomId?: string; message?: string }>) => {
      const status = m.payload;
      if (status.state === "MATCHED" && status.roomId) {
        void this.joinRoom(status.roomId);
      } else if (status.state === "REJECTED") {
        this.set({ phase: "lobby" });
        this.toast(status.message || "匹配请求被拒绝");
      }
    });
    connection.onreconnecting(() => this.set({ connectionNote: "连接中断，正在重连…" }));
    connection.onreconnected(async () => {
      this.set({ connectionNote: "" });
      if (!(await this.authenticate())) return;
      const resume = loadSaved();
      if (this.state.phase === "room" && resume?.roomId && resume.reconnectToken) {
        await connection.invoke("Reconnect", resume.roomId, this.state.handle, resume.reconnectToken);
      }
    });
    connection.onclose(() => this.set({ connectionNote: "已与服务器断开，请刷新页面重新连接。" }));

    try {
      await connection.start();
    } catch {
      this.set({ phase: "offline", connectionNote: `无法连接 ${server}，请确认服务端已启动。` });
      return false;
    }
    if (!(await this.authenticate())) return false;

    if (saved && saved.handle === normalized && saved.roomId && saved.reconnectToken) {
      this.set({ phase: "room", roomId: saved.roomId });
      await connection.invoke("Reconnect", saved.roomId, normalized, saved.reconnectToken);
    } else {
      save({ server, handle: normalized, key: this.key });
      this.set({ phase: "lobby" });
    }
    return true;
  }

  async resume() {
    const saved = loadSaved();
    if (!saved) return false;
    return this.connect(saved.server, saved.handle);
  }

  private async authenticate() {
    const result = await this.connection!.invoke<{ authenticated: boolean; status: string }>(
      "Authenticate",
      this.state.handle,
      this.key,
    );
    if (!result.authenticated) {
      const why =
        result.status === "HandleClaimed"
          ? "这个名字已被其他玩家使用，请换一个。"
          : result.status === "InvalidHandle"
            ? "名字不可用：请使用字母、数字或下划线。"
            : `身份验证失败（${result.status}）`;
      this.set({ phase: "offline", connectionNote: why });
      await this.connection?.stop();
      return false;
    }
    return true;
  }

  async joinRoom(roomId: string) {
    this.set({ snapshot: null, prompt: null, log: [], roomId });
    await this.connection!.invoke("JoinRoom", roomId, this.state.handle, null);
  }

  async createRoom() {
    this.set({ snapshot: null, prompt: null, log: [] });
    const result = await this.connection!.invoke<{ match: { roomId: string } } | null>(
      "CreatePublicMatch",
      this.state.handle,
    );
    return result?.match.roomId ?? null;
  }

  async listRooms() {
    return (
      (await this.connection?.invoke<{ roomId: string; hostPlayerId: string; seatCount: number; capacity: number; status: string }[]>(
        "ListPublicMatches",
      )) ?? []
    );
  }

  async quickMatch() {
    this.set({ phase: "matchmaking" });
    await this.connection!.invoke("EnqueueMatchmaking", this.state.handle);
  }

  async cancelMatch() {
    await this.connection!.invoke("CancelMatchmaking", this.state.handle);
    this.set({ phase: "lobby" });
  }

  leaveRoom() {
    save({ server: this.state.server, handle: this.state.handle, key: this.key });
    this.set({ phase: "lobby", roomId: "", session: null, snapshot: null, prompt: null, log: [] });
  }

  /**
   * Asks the server what a PLAY_CARD command would cost right now (target taxes included).
   * The quote is informational; submission is still authorized by the server.
   */
  async previewPlayCard(cmd: Record<string, unknown>): Promise<PlayCostQuote | null> {
    const prompt = this.state.prompt;
    if (!this.connection || !prompt?.promptId) return null;
    try {
      return await this.connection.invoke<PlayCostQuote>("PreviewPlayCard", this.state.roomId, {
        requestId: `quote-${++seq}`,
        promptId: prompt.promptId,
        snapshotTick: prompt.snapshotTick,
        command: cmd,
      });
    } catch {
      return null;
    }
  }

  /**
   * Sends intent bound to the prompt it answers; the server decides legality. `forPrompt` names the
   * prompt a selection was built from: once that prompt is gone the selection is stale, so it is
   * dropped here instead of being sent under the newer prompt's identity.
   */
  async submit(cmd: Record<string, unknown>, forPrompt?: string): Promise<CommandReceipt | null> {
    if (!this.connection || this.state.pending) return null;
    const prompt = this.state.prompt;
    if (forPrompt && prompt?.promptId !== forPrompt) {
      this.toast(STALE_TEXT, "info");
      return null;
    }
    const body = { ...cmd, promptId: prompt?.promptId, snapshotTick: prompt?.snapshotTick };
    this.set({ pending: true });
    try {
      const receipt = await this.connection.invoke<CommandReceipt>(
        "SubmitIntent",
        this.state.roomId,
        this.state.handle,
        `web-${Date.now().toString(36)}-${++seq}`,
        body,
      );
      if (!receipt.accepted) this.toast(readableError(receipt.errorCode, receipt.message));
      return receipt;
    } catch (error) {
      this.toast(error instanceof Error ? error.message : "提交失败");
      return null;
    } finally {
      this.set({ pending: false });
    }
  }
}

export const client = new GameClient();

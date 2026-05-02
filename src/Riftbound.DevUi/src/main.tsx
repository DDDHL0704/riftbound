import React, { useMemo, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import * as signalR from "@microsoft/signalr";
import "./styles.css";

type PlayerKey = "p1" | "p2";
type ConnectionStatus = "disconnected" | "connecting" | "connected" | "reconnecting" | "reconnected" | "closed" | "error";

type WsServerMessage<T = unknown> = {
  type: string;
  roomId: string;
  playerId: string;
  serverTick: number;
  payload: T;
  protocolVersion: number;
  schemaVersion: number;
};

type PlayerSessionDto = {
  playerId: string;
  seat: string;
  reconnectToken: string;
};

type SnapshotDto = {
  tick: number;
  turnNumber: number;
  activePlayerId: string;
  players: Record<string, PlayerSummary>;
  lanes: Record<string, unknown>;
  stack: unknown[];
  timing: Record<string, unknown>;
  turnState: string;
};

type PlayerSummary = {
  id?: string;
  name?: string;
  seat?: string;
  ready?: boolean;
  handSize?: number;
  score?: number;
};

type ActionPromptDto = {
  playerId: string;
  actionable: boolean;
  reason: string;
  actions: string[];
};

type GameEvent = {
  kind: string;
  description: string;
  payload: Record<string, unknown>;
};

type ErrorDto = {
  code: string;
  message: string;
};

type CommandLogEntry = {
  id: number;
  at: string;
  playerId: string;
  method: string;
  clientIntentId?: string;
  payload?: unknown;
  status: "sent" | "failed" | "received";
};

type PlayerState = {
  label: string;
  playerId: string;
  status: ConnectionStatus;
  reconnectStatus: string;
  session?: PlayerSessionDto;
  snapshot?: SnapshotDto;
  prompt?: ActionPromptDto;
  events: WsServerMessage<GameEvent[]>[];
  errors: WsServerMessage<ErrorDto>[];
  commandLog: CommandLogEntry[];
  jsonIntent: string;
  clientIntentId: string;
};

const playerKeys: PlayerKey[] = ["p1", "p2"];

const defaultServerUrl = localStorage.getItem("riftbound.devUi.serverUrl") ?? "http://127.0.0.1:5088";
const defaultRoomId = `dev-${new Date().toISOString().slice(0, 10)}-smoke`;

const initialPlayers: Record<PlayerKey, PlayerState> = {
  p1: {
    label: "P1",
    playerId: "P1",
    status: "disconnected",
    reconnectStatus: "idle",
    events: [],
    errors: [],
    commandLog: [],
    jsonIntent: '{\n  "cmdType": "PASS_PRIORITY"\n}',
    clientIntentId: ""
  },
  p2: {
    label: "P2",
    playerId: "P2",
    status: "disconnected",
    reconnectStatus: "idle",
    events: [],
    errors: [],
    commandLog: [],
    jsonIntent: '{\n  "cmdType": "PASS_PRIORITY"\n}',
    clientIntentId: ""
  }
};

function App() {
  const [serverUrl, setServerUrl] = useState(defaultServerUrl);
  const [roomId, setRoomId] = useState(defaultRoomId);
  const [players, setPlayers] = useState(initialPlayers);
  const connections = useRef<Record<PlayerKey, signalR.HubConnection | null>>({ p1: null, p2: null });
  const logCounter = useRef(1);
  const intentCounter = useRef(1);

  const latestSnapshot = players.p1.snapshot ?? players.p2.snapshot;
  const roomSummary = useMemo(() => summarizeRoom(latestSnapshot), [latestSnapshot]);

  function updatePlayer(key: PlayerKey, updater: (state: PlayerState) => PlayerState) {
    setPlayers((current) => ({
      ...current,
      [key]: updater(current[key])
    }));
  }

  function appendLog(
    key: PlayerKey,
    entry: Omit<CommandLogEntry, "id" | "at" | "playerId">
  ) {
    updatePlayer(key, (state) => ({
      ...state,
      commandLog: [
        {
          id: logCounter.current++,
          at: new Date().toLocaleTimeString(),
          playerId: state.playerId,
          ...entry
        },
        ...state.commandLog
      ].slice(0, 40)
    }));
  }

  function nextIntentId(playerId: string, cmdType: string) {
    return `ui-${roomId}-${playerId}-${cmdType.toLowerCase()}-${Date.now()}-${intentCounter.current++}`;
  }

  function hubUrl() {
    return `${serverUrl.replace(/\/+$/, "")}/hubs/game`;
  }

  async function connectAndJoin(key: PlayerKey) {
    const state = players[key];
    const playerId = state.playerId.trim();
    if (!playerId || !roomId.trim()) {
      appendLog(key, { method: "JoinRoom", status: "failed", payload: "roomId and playerId are required" });
      return;
    }

    localStorage.setItem("riftbound.devUi.serverUrl", serverUrl);
    await connections.current[key]?.stop();
    updatePlayer(key, (current) => ({ ...current, status: "connecting", reconnectStatus: "starting" }));

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl())
      .withAutomaticReconnect([0, 1000, 3000, 5000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.onreconnecting(() => {
      updatePlayer(key, (current) => ({ ...current, status: "reconnecting", reconnectStatus: "retrying" }));
    });
    connection.onreconnected(async () => {
      updatePlayer(key, (current) => ({ ...current, status: "reconnected", reconnectStatus: "reconnected" }));
      try {
        await connection.invoke("JoinRoom", roomId.trim(), playerId, null);
        appendLog(key, { method: "JoinRoom", status: "sent", payload: "rejoined after reconnect" });
      } catch (error) {
        updatePlayer(key, (current) => ({ ...current, status: "error", reconnectStatus: errorToText(error) }));
        appendLog(key, { method: "JoinRoom", status: "failed", payload: errorToText(error) });
      }
    });
    connection.onclose(() => {
      updatePlayer(key, (current) => ({ ...current, status: "closed", reconnectStatus: "closed" }));
    });

    connection.on("Joined", (message: WsServerMessage<PlayerSessionDto>) => {
      updatePlayer(key, (current) => ({
        ...current,
        status: "connected",
        reconnectStatus: "joined",
        session: message.payload
      }));
      appendLog(key, { method: "Joined", status: "received", payload: message.payload });
    });

    connection.on("Snapshot", (message: WsServerMessage<SnapshotDto>) => {
      updatePlayer(key, (current) => ({ ...current, snapshot: message.payload }));
    });

    connection.on("Prompt", (message: WsServerMessage<ActionPromptDto>) => {
      updatePlayer(key, (current) => ({ ...current, prompt: message.payload }));
    });

    connection.on("Events", (message: WsServerMessage<GameEvent[]>) => {
      updatePlayer(key, (current) => ({
        ...current,
        events: [message, ...current.events].slice(0, 30)
      }));
    });

    connection.on("Error", (message: WsServerMessage<ErrorDto>) => {
      updatePlayer(key, (current) => ({
        ...current,
        errors: [message, ...current.errors].slice(0, 30)
      }));
      appendLog(key, { method: "Error", status: "received", payload: message.payload });
    });

    try {
      await connection.start();
      connections.current[key] = connection;
      updatePlayer(key, (current) => ({ ...current, status: "connected", reconnectStatus: "transport ready" }));
      await connection.invoke("JoinRoom", roomId.trim(), playerId, null);
      appendLog(key, { method: "JoinRoom", status: "sent", payload: { roomId: roomId.trim(), playerId } });
    } catch (error) {
      connections.current[key] = null;
      updatePlayer(key, (current) => ({ ...current, status: "error", reconnectStatus: errorToText(error) }));
      appendLog(key, { method: "JoinRoom", status: "failed", payload: errorToText(error) });
    }
  }

  async function disconnect(key: PlayerKey) {
    await connections.current[key]?.stop();
    connections.current[key] = null;
    updatePlayer(key, (current) => ({ ...current, status: "disconnected", reconnectStatus: "manual stop" }));
  }

  async function invoke(key: PlayerKey, method: string, args: unknown[], clientIntentId?: string, payload?: unknown) {
    const connection = connections.current[key];
    if (!connection) {
      appendLog(key, { method, status: "failed", clientIntentId, payload: "not connected" });
      return;
    }

    try {
      await connection.invoke(method, ...args);
      appendLog(key, { method, status: "sent", clientIntentId, payload });
    } catch (error) {
      appendLog(key, { method, status: "failed", clientIntentId, payload: errorToText(error) });
    }
  }

  async function ready(key: PlayerKey) {
    const playerId = players[key].playerId.trim();
    const clientIntentId = nextIntentId(playerId, "READY");
    await invoke(key, "Ready", [roomId.trim(), playerId, clientIntentId], clientIntentId, { cmdType: "READY" });
  }

  async function requestSnapshot(key: PlayerKey) {
    const playerId = players[key].playerId.trim();
    await invoke(key, "RequestSnapshot", [roomId.trim(), playerId], undefined, { roomId: roomId.trim(), playerId });
  }

  async function submitIntent(key: PlayerKey, command: Record<string, unknown>, requestedIntentId?: string) {
    const playerId = players[key].playerId.trim();
    const cmdType = typeof command.cmdType === "string" ? command.cmdType : "UNKNOWN";
    const clientIntentId = requestedIntentId?.trim() || nextIntentId(playerId, cmdType);
    await invoke(
      key,
      "SubmitIntent",
      [roomId.trim(), playerId, clientIntentId, command],
      clientIntentId,
      command
    );
  }

  async function submitJsonIntent(key: PlayerKey) {
    const state = players[key];
    try {
      const parsed = JSON.parse(state.jsonIntent) as Record<string, unknown>;
      await submitIntent(key, parsed, state.clientIntentId);
      updatePlayer(key, (current) => ({ ...current, clientIntentId: "" }));
    } catch (error) {
      appendLog(key, { method: "SubmitIntent", status: "failed", payload: `Invalid JSON: ${errorToText(error)}` });
    }
  }

  async function submitPromptAction(key: PlayerKey, action: string) {
    await submitIntent(key, { cmdType: action });
  }

  async function runForBoth(action: (key: PlayerKey) => Promise<void>) {
    for (const key of playerKeys) {
      await action(key);
    }
  }

  return (
    <main className="app-shell">
      <section className="top-bar" aria-label="Connection settings">
        <div>
          <p className="eyebrow">P2.5 Dev Test UI</p>
          <h1>Riftbound GameHub Test Bench</h1>
        </div>
        <label>
          Server URL
          <input
            data-testid="server-url"
            value={serverUrl}
            onChange={(event) => setServerUrl(event.target.value)}
            spellCheck={false}
          />
        </label>
        <label>
          Room ID
          <input
            data-testid="room-id"
            value={roomId}
            onChange={(event) => setRoomId(event.target.value)}
            spellCheck={false}
          />
        </label>
        <button data-testid="join-both" onClick={() => runForBoth(connectAndJoin)}>
          Join Both
        </button>
        <button data-testid="ready-both" onClick={() => runForBoth(ready)}>
          Ready Both
        </button>
        <button data-testid="snapshot-both" onClick={() => runForBoth(requestSnapshot)}>
          Snapshot Both
        </button>
      </section>

      <section className="room-strip" aria-label="Room summary">
        {roomSummary.map((item) => (
          <div className="metric" key={item.label}>
            <span>{item.label}</span>
            <strong>{item.value}</strong>
          </div>
        ))}
      </section>

      <section className="players-grid">
        {playerKeys.map((key) => (
          <PlayerPanel
            key={key}
            playerKey={key}
            state={players[key]}
            onPatch={(patch) => updatePlayer(key, (current) => ({ ...current, ...patch }))}
            onJoin={() => connectAndJoin(key)}
            onDisconnect={() => disconnect(key)}
            onReady={() => ready(key)}
            onSnapshot={() => requestSnapshot(key)}
            onPromptAction={(action) => submitPromptAction(key, action)}
            onSubmitJson={() => submitJsonIntent(key)}
          />
        ))}
      </section>
    </main>
  );
}

function PlayerPanel({
  playerKey,
  state,
  onPatch,
  onJoin,
  onDisconnect,
  onReady,
  onSnapshot,
  onPromptAction,
  onSubmitJson
}: {
  playerKey: PlayerKey;
  state: PlayerState;
  onPatch: (patch: Partial<PlayerState>) => void;
  onJoin: () => void;
  onDisconnect: () => void;
  onReady: () => void;
  onSnapshot: () => void;
  onPromptAction: (action: string) => void;
  onSubmitJson: () => void;
}) {
  const allowedActions = new Set(state.prompt?.actions ?? []);
  const promptButtons = ["END_TURN", "PASS_PRIORITY", "PASS_FOCUS"].filter((action) => allowedActions.has(action));

  return (
    <article className="player-panel" data-testid={`${playerKey}-panel`}>
      <header className="panel-header">
        <div>
          <p className="eyebrow">{state.label} Client</p>
          <h2>{state.playerId || state.label}</h2>
        </div>
        <StatusPill status={state.status} />
      </header>

      <div className="player-settings">
        <label>
          Player ID
          <input
            data-testid={`${playerKey}-player-id`}
            value={state.playerId}
            onChange={(event) => onPatch({ playerId: event.target.value })}
            spellCheck={false}
          />
        </label>
        <div className="button-row">
          <button data-testid={`${playerKey}-join`} onClick={onJoin}>
            JoinRoom
          </button>
          <button data-testid={`${playerKey}-disconnect`} onClick={onDisconnect}>
            Stop
          </button>
          <button data-testid={`${playerKey}-ready`} onClick={onReady}>
            Ready
          </button>
          <button data-testid={`${playerKey}-snapshot`} onClick={onSnapshot}>
            RequestSnapshot
          </button>
        </div>
      </div>

      <dl className="connection-details">
        <div>
          <dt>Reconnect</dt>
          <dd>{state.reconnectStatus}</dd>
        </div>
        <div>
          <dt>Seat</dt>
          <dd>{state.session?.seat ?? "-"}</dd>
        </div>
        <div>
          <dt>Tick</dt>
          <dd>{state.snapshot?.tick ?? "-"}</dd>
        </div>
        <div>
          <dt>Prompt</dt>
          <dd>{state.prompt?.actionable ? "actionable" : "waiting"}</dd>
        </div>
      </dl>

      <section className="prompt-panel" aria-label={`${state.label} action prompt`}>
        <div className="section-title">
          <h3>Action Prompt</h3>
          <span>{state.prompt?.reason ?? "No prompt yet"}</span>
        </div>
        <div className="button-row">
          {promptButtons.length === 0 ? (
            <button disabled>No prompt action</button>
          ) : (
            promptButtons.map((action) => (
              <button
                key={action}
                data-testid={`${playerKey}-${action.toLowerCase().replaceAll("_", "-")}`}
                disabled={!state.prompt?.actionable}
                onClick={() => onPromptAction(action)}
              >
                {action}
              </button>
            ))
          )}
        </div>
      </section>

      <section className="intent-panel" aria-label={`${state.label} submit intent`}>
        <div className="section-title">
          <h3>SubmitIntent JSON</h3>
          <span>Sent exactly as cmd payload</span>
        </div>
        <input
          data-testid={`${playerKey}-intent-id`}
          className="intent-id"
          value={state.clientIntentId}
          placeholder="clientIntentId override, optional"
          onChange={(event) => onPatch({ clientIntentId: event.target.value })}
          spellCheck={false}
        />
        <textarea
          data-testid={`${playerKey}-json-intent`}
          value={state.jsonIntent}
          onChange={(event) => onPatch({ jsonIntent: event.target.value })}
          spellCheck={false}
        />
        <button data-testid={`${playerKey}-submit-json`} onClick={onSubmitJson}>
          Submit JSON
        </button>
      </section>

      <DebugGrid state={state} />
    </article>
  );
}

function DebugGrid({ state }: { state: PlayerState }) {
  return (
    <div className="debug-grid">
      <DebugBlock title="Snapshot" value={state.snapshot} testId={`${state.label.toLowerCase()}-snapshot-json`} />
      <DebugBlock title="Prompt JSON" value={state.prompt} testId={`${state.label.toLowerCase()}-prompt-json`} />
      <DebugBlock
        title="Server Events"
        value={state.events.flatMap((message) => message.payload)}
        testId={`${state.label.toLowerCase()}-events-json`}
      />
      <DebugBlock title="Errors" value={state.errors.map((message) => message.payload)} testId={`${state.label.toLowerCase()}-errors-json`} />
      <DebugBlock title="Command Log" value={state.commandLog} testId={`${state.label.toLowerCase()}-command-log`} wide />
    </div>
  );
}

function DebugBlock({
  title,
  value,
  testId,
  wide = false
}: {
  title: string;
  value: unknown;
  testId: string;
  wide?: boolean;
}) {
  return (
    <section className={wide ? "debug-block wide" : "debug-block"}>
      <h3>{title}</h3>
      <pre data-testid={testId}>{value ? JSON.stringify(value, null, 2) : "null"}</pre>
    </section>
  );
}

function StatusPill({ status }: { status: ConnectionStatus }) {
  return <span className={`status-pill ${status}`}>{status}</span>;
}

function summarizeRoom(snapshot?: SnapshotDto) {
  if (!snapshot) {
    return [
      { label: "Status", value: "no snapshot" },
      { label: "Turn", value: "-" },
      { label: "Active", value: "-" },
      { label: "Timing", value: "-" },
      { label: "Stack", value: "-" }
    ];
  }

  return [
    { label: "Status", value: String(snapshot.timing?.roomStatus ?? snapshot.turnState ?? "-") },
    { label: "Turn", value: `#${snapshot.turnNumber}` },
    { label: "Active", value: snapshot.activePlayerId || "-" },
    { label: "Timing", value: String(snapshot.timing?.timingState ?? snapshot.turnState ?? "-") },
    { label: "Stack", value: String(snapshot.stack?.length ?? 0) }
  ];
}

function errorToText(error: unknown) {
  return error instanceof Error ? error.message : String(error);
}

createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);

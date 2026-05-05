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

type RunePoolView = {
  mana?: number;
  power?: number;
};

type ZoneView = {
  mainDeckCount?: number;
  runeDeckCount?: number;
  hand?: string[];
  handHidden?: number;
  base?: string[];
  battlefields?: string[];
  graveyard?: string[];
  banished?: string[];
  legendZone?: string[];
  championZone?: string[];
};

type ObjectView = {
  objectId?: string;
  cardNo?: string | null;
  damage?: number;
  power?: number;
  untilEndOfTurnPowerModifier?: number;
  isExhausted?: boolean;
  isFaceDown?: boolean;
  isAttacking?: boolean;
  isDefending?: boolean;
  tags?: string[];
  untilEndOfTurnEffects?: string[];
  manaCost?: number;
  attachedToObjectId?: string | null;
  ownerId?: string | null;
  controllerId?: string | null;
};

type PlayerSummary = {
  id?: string;
  name?: string;
  seat?: string;
  ready?: boolean;
  handSize?: number;
  score?: number;
  experience?: number;
  runePool?: RunePoolView;
  zones?: ZoneView;
  objects?: Record<string, ObjectView>;
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

type PlayCardDraft = {
  sourceObjectId: string;
  cardNo: string;
  targetObjectIds: string;
  mode: string;
  optionalCosts: string;
};

type ScenarioPreset = {
  id: string;
  title: string;
  description: string;
  command: Record<string, unknown>;
};

const playerKeys: PlayerKey[] = ["p1", "p2"];
const replayableActions = new Set(["READY", "PASS", "PASS_PRIORITY", "PASS_FOCUS", "END_TURN"]);

const defaultServerUrl = localStorage.getItem("riftbound.devUi.serverUrl") ?? "http://127.0.0.1:5088";
const defaultRoomId = localStorage.getItem("riftbound.p7.roomId") ?? `p7-${new Date().toISOString().slice(0, 10)}-battle`;
const defaultP1Id = localStorage.getItem("riftbound.p7.player.p1") ?? "P1";
const defaultP2Id = localStorage.getItem("riftbound.p7.player.p2") ?? "P2";

const initialDraft: PlayCardDraft = {
  sourceObjectId: "",
  cardNo: "",
  targetObjectIds: "",
  mode: "",
  optionalCosts: ""
};

const scenarioPresets: ScenarioPreset[] = [
  {
    id: "basic-play",
    title: "Basic Play",
    description: "P1 hand has Mighty Faerie and 4 mana.",
    command: {
      cmdType: "PLAY_CARD",
      sourceObjectId: "P1-UNIT-MIGHTY-FAERIE",
      cardNo: "SFD·125/221",
      targetObjectIds: []
    }
  },
  {
    id: "movement",
    title: "Movement",
    description: "P1 can play Ride the Wind on a friendly battlefield unit.",
    command: {
      cmdType: "PLAY_CARD",
      sourceObjectId: "P1-SPELL-RIDE-THE-WIND",
      cardNo: "OGN·173/298",
      targetObjectIds: ["P1-BATTLEFIELD-UNIT-001"]
    }
  },
  {
    id: "spell-duel",
    title: "Spell Window",
    description: "P1 can play Hextech Ray against a P2 battlefield unit.",
    command: {
      cmdType: "PLAY_CARD",
      sourceObjectId: "P1-SPELL-HEXTECH-RAY",
      cardNo: "OGN·009/298",
      targetObjectIds: ["P2-UNIT-001"]
    }
  },
  {
    id: "equipment",
    title: "Equipment",
    description: "P1 hand has Long Sword and 2 mana.",
    command: {
      cmdType: "PLAY_CARD",
      sourceObjectId: "P1-EQUIPMENT-LONG-SWORD",
      cardNo: "SFD·022/221",
      targetObjectIds: []
    }
  },
  {
    id: "control",
    title: "Control",
    description: "P1 can play Hostile Takeover on an exhausted P2 unit.",
    command: {
      cmdType: "PLAY_CARD",
      sourceObjectId: "P1-SPELL-HOSTILE-TAKEOVER",
      cardNo: "SFD·202/221",
      targetObjectIds: ["P2-HOSTILE-TAKEOVER-TARGET"]
    }
  },
  {
    id: "battle-score",
    title: "Battle Score",
    description: "Seeds battlefield objects and an empty P2 rune deck for END_TURN score smoke.",
    command: {
      cmdType: "END_TURN"
    }
  },
  {
    id: "specified-hand",
    title: "Specified Hand",
    description: "P1 receives multiple known playable cards for ad hoc fixture replay.",
    command: {
      cmdType: "PLAY_CARD",
      sourceObjectId: "P1-SPELL-HEXTECH-RAY",
      cardNo: "OGN·009/298",
      targetObjectIds: ["P2-UNIT-001"]
    }
  }
];

const initialPlayers: Record<PlayerKey, PlayerState> = {
  p1: {
    label: "P1",
    playerId: defaultP1Id,
    status: "disconnected",
    reconnectStatus: "idle",
    events: [],
    errors: [],
    commandLog: [],
    jsonIntent: formatJson({ cmdType: "PASS_PRIORITY" }),
    clientIntentId: ""
  },
  p2: {
    label: "P2",
    playerId: defaultP2Id,
    status: "disconnected",
    reconnectStatus: "idle",
    events: [],
    errors: [],
    commandLog: [],
    jsonIntent: formatJson({ cmdType: "PASS_PRIORITY" }),
    clientIntentId: ""
  }
};

function App() {
  const [serverUrl, setServerUrl] = useState(defaultServerUrl);
  const [roomId, setRoomId] = useState(defaultRoomId);
  const [activeKey, setActiveKey] = useState<PlayerKey>("p1");
  const [players, setPlayers] = useState(initialPlayers);
  const [playDraft, setPlayDraft] = useState(initialDraft);
  const [fixtureDraft, setFixtureDraft] = useState("");
  const [fixtureStatus, setFixtureStatus] = useState("idle");
  const connections = useRef<Record<PlayerKey, signalR.HubConnection | null>>({ p1: null, p2: null });
  const playersRef = useRef(initialPlayers);
  const logCounter = useRef(1);
  const intentCounter = useRef(1);

  const activePlayer = players[activeKey];
  const latestSnapshot = activePlayer.snapshot ?? players.p1.snapshot ?? players.p2.snapshot;
  const roomSummary = useMemo(() => summarizeRoom(latestSnapshot), [latestSnapshot]);
  const visibleObjectIds = useMemo(() => collectVisibleObjectIds(latestSnapshot), [latestSnapshot]);
  const fixtureText = fixtureDraft || buildFixtureDraft(roomId, players);

  function updatePlayer(key: PlayerKey, updater: (state: PlayerState) => PlayerState) {
    setPlayers((current) => {
      const next = {
        ...current,
        [key]: updater(current[key])
      };
      playersRef.current = next;
      return next;
    });
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
      ].slice(0, 80)
    }));
  }

  function nextIntentId(playerId: string, cmdType: string) {
    return `ui-${roomId}-${playerId}-${cmdType.toLowerCase()}-${Date.now()}-${intentCounter.current++}`;
  }

  function hubUrl() {
    return `${serverUrl.replace(/\/+$/, "")}/hubs/game`;
  }

  function registerHandlers(key: PlayerKey, connection: signalR.HubConnection, playerId: string) {
    connection.onreconnecting(() => {
      updatePlayer(key, (current) => ({ ...current, status: "reconnecting", reconnectStatus: "retrying" }));
    });
    connection.onreconnected(async () => {
      updatePlayer(key, (current) => ({ ...current, status: "reconnected", reconnectStatus: "transport reconnected" }));
      const token = playersRef.current[key].session?.reconnectToken ?? loadStoredSession(roomId.trim(), playerId)?.reconnectToken;
      try {
        if (token) {
          await connection.invoke("Reconnect", roomId.trim(), playerId, token);
          appendLog(key, { method: "Reconnect", status: "sent", payload: "automatic reconnect" });
        } else {
          await connection.invoke("JoinRoom", roomId.trim(), playerId, null);
          appendLog(key, { method: "JoinRoom", status: "sent", payload: "automatic rejoin" });
        }
      } catch (error) {
        updatePlayer(key, (current) => ({ ...current, status: "error", reconnectStatus: errorToText(error) }));
        appendLog(key, { method: "Reconnect", status: "failed", payload: errorToText(error) });
      }
    });
    connection.onclose(() => {
      updatePlayer(key, (current) => ({ ...current, status: "closed", reconnectStatus: "closed" }));
    });

    connection.on("Joined", (message: WsServerMessage<PlayerSessionDto>) => {
      rememberSession(roomId.trim(), message.payload);
      updatePlayer(key, (current) => ({
        ...current,
        status: "connected",
        reconnectStatus: message.type === "RECONNECT" ? "reconnected" : "joined",
        session: message.payload
      }));
      appendLog(key, { method: message.type === "RECONNECT" ? "Reconnected" : "Joined", status: "received", payload: message.payload });
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
        events: [message, ...current.events].slice(0, 50)
      }));
    });

    connection.on("Error", (message: WsServerMessage<ErrorDto>) => {
      updatePlayer(key, (current) => ({
        ...current,
        errors: [message, ...current.errors].slice(0, 50)
      }));
      appendLog(key, { method: "Error", status: "received", payload: message.payload });
    });
  }

  async function startConnection(key: PlayerKey) {
    const playerId = players[key].playerId.trim();
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl())
      .withAutomaticReconnect([0, 1000, 3000, 5000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    registerHandlers(key, connection, playerId);
    await connection.start();
    connections.current[key] = connection;
    updatePlayer(key, (current) => ({ ...current, status: "connected", reconnectStatus: "transport ready" }));
    return connection;
  }

  async function connectAndJoin(key: PlayerKey) {
    const state = players[key];
    const playerId = state.playerId.trim();
    if (!playerId || !roomId.trim()) {
      appendLog(key, { method: "JoinRoom", status: "failed", payload: "roomId and playerId are required" });
      return;
    }

    localStorage.setItem("riftbound.devUi.serverUrl", serverUrl);
    localStorage.setItem("riftbound.p7.roomId", roomId.trim());
    localStorage.setItem(`riftbound.p7.player.${key}`, playerId);
    await connections.current[key]?.stop();
    connections.current[key] = null;
    updatePlayer(key, (current) => ({ ...current, status: "connecting", reconnectStatus: "starting" }));

    try {
      const connection = await startConnection(key);
      await connection.invoke("JoinRoom", roomId.trim(), playerId, null);
      appendLog(key, { method: "JoinRoom", status: "sent", payload: { roomId: roomId.trim(), playerId } });
    } catch (error) {
      connections.current[key] = null;
      updatePlayer(key, (current) => ({ ...current, status: "error", reconnectStatus: errorToText(error) }));
      appendLog(key, { method: "JoinRoom", status: "failed", payload: errorToText(error) });
    }
  }

  async function reconnect(key: PlayerKey) {
    const state = players[key];
    const playerId = state.playerId.trim();
    const storedSession = loadStoredSession(roomId.trim(), playerId);
    const token = state.session?.reconnectToken ?? storedSession?.reconnectToken;
    if (!token) {
      appendLog(key, { method: "Reconnect", status: "failed", payload: "no reconnect token" });
      return;
    }

    await connections.current[key]?.stop();
    connections.current[key] = null;
    updatePlayer(key, (current) => ({ ...current, status: "connecting", reconnectStatus: "reconnecting with token" }));

    try {
      const connection = await startConnection(key);
      await connection.invoke("Reconnect", roomId.trim(), playerId, token);
      appendLog(key, { method: "Reconnect", status: "sent", payload: { roomId: roomId.trim(), playerId } });
    } catch (error) {
      connections.current[key] = null;
      updatePlayer(key, (current) => ({ ...current, status: "error", reconnectStatus: errorToText(error) }));
      appendLog(key, { method: "Reconnect", status: "failed", payload: errorToText(error) });
    }
  }

  async function disconnect(key: PlayerKey) {
    await connections.current[key]?.stop();
    connections.current[key] = null;
    updatePlayer(key, (current) => ({ ...current, status: "disconnected", reconnectStatus: "manual stop" }));
  }

  async function newRoom() {
    for (const key of playerKeys) {
      await connections.current[key]?.stop();
      connections.current[key] = null;
    }

    const nextRoomId = `p7-${Date.now()}`;
    setRoomId(nextRoomId);
    localStorage.setItem("riftbound.p7.roomId", nextRoomId);
    const nextPlayers = {
      p1: { ...initialPlayers.p1, playerId: players.p1.playerId },
      p2: { ...initialPlayers.p2, playerId: players.p2.playerId }
    };
    setPlayers(nextPlayers);
    playersRef.current = nextPlayers;
    setFixtureDraft("");
    setFixtureStatus("idle");
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

  async function seedScenario(preset: ScenarioPreset) {
    const key: PlayerKey = "p1";
    const playerId = players[key].playerId.trim();
    const clientIntentId = nextIntentId(playerId, `seed-${preset.id}`);
    await invoke(
      key,
      "SeedScenario",
      [roomId.trim(), playerId, preset.id, clientIntentId],
      clientIntentId,
      { cmdType: "DEV_SEED_SCENARIO", scenarioId: preset.id }
    );
    setActiveKey(key);
    setCommandForPlayer(key, preset.command);
    setPlayDraft(draftFromCommand(preset.command));
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

  async function submitPlayCardDraft() {
    await submitIntent(activeKey, buildPlayCardCommand(playDraft), players[activeKey].clientIntentId);
    updatePlayer(activeKey, (current) => ({ ...current, clientIntentId: "" }));
  }

  async function submitPromptAction(key: PlayerKey, action: string) {
    if (action === "READY") {
      await ready(key);
      return;
    }

    if (action === "PLAY_CARD") {
      setActiveKey(key);
      setCommandForPlayer(key, buildPlayCardCommand(playDraft));
      return;
    }

    if (replayableActions.has(action)) {
      await submitIntent(key, { cmdType: action });
    }
  }

  async function runForBoth(action: (key: PlayerKey) => Promise<void>) {
    for (const key of playerKeys) {
      await action(key);
    }
  }

  function setCommandForPlayer(key: PlayerKey, command: Record<string, unknown>) {
    updatePlayer(key, (current) => ({ ...current, jsonIntent: formatJson(command) }));
  }

  function addTargetObjectId(objectId: string) {
    setPlayDraft((current) => {
      const values = parseList(current.targetObjectIds);
      if (!values.includes(objectId)) {
        values.push(objectId);
      }

      return { ...current, targetObjectIds: values.join(", ") };
    });
  }

  function refreshFixtureDraft() {
    setFixtureDraft(buildFixtureDraft(roomId, players));
    setFixtureStatus("draft refreshed");
  }

  async function copyFixtureDraft() {
    const text = fixtureDraft || buildFixtureDraft(roomId, players);
    setFixtureDraft(text);
    try {
      await navigator.clipboard.writeText(text);
      setFixtureStatus("copied");
    } catch {
      setFixtureStatus("copy blocked");
    }
  }

  return (
    <main className="app-shell">
      <section className="top-bar" aria-label="Connection settings">
        <div>
          <p className="eyebrow">P7 Web Battle</p>
          <h1>符文战场</h1>
          <span className="subtitle">服务端权威双人对战房间</span>
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
        <label>
          View
          <select
            data-testid="active-player"
            value={activeKey}
            onChange={(event) => setActiveKey(event.target.value as PlayerKey)}
          >
            <option value="p1">P1</option>
            <option value="p2">P2</option>
          </select>
        </label>
        <button data-testid="new-room" onClick={() => void newRoom()}>
          新房间
        </button>
        <button data-testid="join-both" onClick={() => void runForBoth(connectAndJoin)}>
          双人入座
        </button>
        <button data-testid="ready-both" onClick={() => void runForBoth(ready)}>
          双方准备
        </button>
        <button data-testid="snapshot-both" onClick={() => void runForBoth(requestSnapshot)}>
          同步状态
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

      <section className="workspace-grid">
        <BattleDesk snapshot={latestSnapshot} activePlayerId={activePlayer.playerId} onPickObject={addTargetObjectId} />
        <CommandWorkbench
          activeKey={activeKey}
          activePlayer={activePlayer}
          playDraft={playDraft}
          visibleObjectIds={visibleObjectIds}
          fixtureText={fixtureText}
          fixtureStatus={fixtureStatus}
          onActiveKey={setActiveKey}
          onPlayDraft={setPlayDraft}
          onPickObject={addTargetObjectId}
          onSubmitPlayCard={() => void submitPlayCardDraft()}
          onSeedScenario={(preset) => void seedScenario(preset)}
          onRefreshFixture={refreshFixtureDraft}
          onCopyFixture={() => void copyFixtureDraft()}
        />
      </section>

      <section className="players-grid">
        {playerKeys.map((key) => (
          <PlayerPanel
            key={key}
            playerKey={key}
            state={players[key]}
            onPatch={(patch) => updatePlayer(key, (current) => ({ ...current, ...patch }))}
            onJoin={() => void connectAndJoin(key)}
            onReconnect={() => void reconnect(key)}
            onDisconnect={() => void disconnect(key)}
            onReady={() => void ready(key)}
            onSnapshot={() => void requestSnapshot(key)}
            onPromptAction={(action) => void submitPromptAction(key, action)}
            onSubmitJson={() => void submitJsonIntent(key)}
          />
        ))}
      </section>
    </main>
  );
}

function BattleDesk({
  snapshot,
  activePlayerId,
  onPickObject
}: {
  snapshot?: SnapshotDto;
  activePlayerId: string;
  onPickObject: (objectId: string) => void;
}) {
  const players = Object.entries(snapshot?.players ?? {}).sort(([, left], [, right]) =>
    String(left.seat ?? "").localeCompare(String(right.seat ?? ""))
  );

  return (
    <section className="desk-panel" data-testid="battle-desk">
      <div className="section-title">
        <h2>Battle Desk</h2>
        <span>{snapshot ? `${snapshot.turnState} / ${activePlayerId}` : "no snapshot"}</span>
      </div>
      <div className="lane-board">
        {players.map(([playerId, player]) => (
          <PlayerDesk key={playerId} playerId={playerId} player={player} onPickObject={onPickObject} />
        ))}
        {players.length === 0 ? <EmptyDesk /> : null}
      </div>
      <section className="stack-strip">
        <div className="section-title">
          <h3>Stack</h3>
          <span>{snapshot?.stack?.length ?? 0}</span>
        </div>
        <ObjectList ids={(snapshot?.stack ?? []).map((item, index) => stackLabel(item, index))} onPickObject={onPickObject} />
      </section>
    </section>
  );
}

function PlayerDesk({
  playerId,
  player,
  onPickObject
}: {
  playerId: string;
  player: PlayerSummary;
  onPickObject: (objectId: string) => void;
}) {
  const zones = player.zones ?? {};
  return (
    <article className="desk-player">
      <header>
        <div>
          <p className="eyebrow">{player.seat ?? "-"}</p>
          <h3>{playerId}</h3>
        </div>
        <div className="score-box">
          <span>Score</span>
          <strong>{player.score ?? 0}</strong>
        </div>
      </header>
      <div className="resource-row">
        <span>法力 {player.runePool?.mana ?? 0}</span>
        <span>战力符能 {player.runePool?.power ?? 0}</span>
        <span>经验 {player.experience ?? 0}</span>
        <span>主牌堆 {zones.mainDeckCount ?? 0}</span>
        <span>符文 {zones.runeDeckCount ?? 0}</span>
        <span>手牌 {player.handSize ?? 0}</span>
      </div>
      <div className="identity-row">
        <ZoneRow title="传奇" ids={zones.legendZone ?? []} objects={player.objects} onPickObject={onPickObject} compact />
        <ZoneRow title="英雄" ids={zones.championZone ?? []} objects={player.objects} onPickObject={onPickObject} compact />
      </div>
      <ZoneRow title="手牌" ids={zones.hand ?? []} hiddenCount={zones.handHidden ?? 0} objects={player.objects} onPickObject={onPickObject} />
      <div className="battlefield-pair">
        <ZoneRow title="战场 I" ids={(zones.battlefields ?? []).filter((_, index) => index % 2 === 0)} objects={player.objects} onPickObject={onPickObject} />
        <ZoneRow title="战场 II" ids={(zones.battlefields ?? []).filter((_, index) => index % 2 === 1)} objects={player.objects} onPickObject={onPickObject} />
      </div>
      <ZoneRow title="基地" ids={zones.base ?? []} objects={player.objects} onPickObject={onPickObject} />
      <div className="zone-minor-grid">
        <ZoneRow title="废牌堆" ids={zones.graveyard ?? []} objects={player.objects} onPickObject={onPickObject} compact />
        <ZoneRow title="放逐区" ids={zones.banished ?? []} objects={player.objects} onPickObject={onPickObject} compact />
      </div>
    </article>
  );
}

function ZoneRow({
  title,
  ids,
  hiddenCount = 0,
  objects,
  onPickObject,
  compact = false
}: {
  title: string;
  ids: string[];
  hiddenCount?: number;
  objects?: Record<string, ObjectView>;
  onPickObject: (objectId: string) => void;
  compact?: boolean;
}) {
  return (
    <section className={compact ? "zone-row compact" : "zone-row"}>
      <div className="zone-title">
        <span>{title}</span>
        <strong>{hiddenCount > 0 ? hiddenCount : ids.length}</strong>
      </div>
      {hiddenCount > 0 ? <div className="hidden-card">{hiddenCount} 张隐藏手牌</div> : null}
      <ObjectList ids={ids} objects={objects} onPickObject={onPickObject} />
    </section>
  );
}

function ObjectList({
  ids,
  objects,
  onPickObject
}: {
  ids: string[];
  objects?: Record<string, ObjectView>;
  onPickObject: (objectId: string) => void;
}) {
  if (ids.length === 0) {
    return <div className="empty-zone">empty</div>;
  }

  return (
    <div className="object-list">
      {ids.map((id) => (
        <button className={objectClassName(objects?.[id])} key={id} onClick={() => onPickObject(id)}>
          <span>{cardTitle(id, objects?.[id])}</span>
          <ObjectMeta object={objects?.[id]} />
        </button>
      ))}
    </div>
  );
}

function ObjectMeta({ object }: { object?: ObjectView }) {
  if (!object) {
    return null;
  }

  const combatBits = [
    object.power !== undefined ? `战力 ${effectivePower(object)}` : "",
    object.damage ? `伤害 ${object.damage}` : "",
    object.manaCost !== undefined ? `费用 ${object.manaCost}` : ""
  ].filter(Boolean);
  const stateBits = [
    object.isExhausted ? "横置" : "",
    object.isAttacking ? "攻击" : "",
    object.isDefending ? "防守" : "",
    object.isFaceDown ? "盖放" : "",
    object.attachedToObjectId ? `贴附 ${object.attachedToObjectId}` : "",
    object.controllerId && object.ownerId && object.controllerId !== object.ownerId ? `控制 ${object.controllerId}` : ""
  ].filter(Boolean);

  return (
    <>
      {object.cardNo ? <small>{object.cardNo}</small> : null}
      {combatBits.length > 0 ? <small>{combatBits.join(" / ")}</small> : null}
      {stateBits.length > 0 ? <small>{stateBits.join(" / ")}</small> : null}
      {object.tags?.length ? <small>{object.tags.slice(0, 4).join(", ")}</small> : null}
      {object.untilEndOfTurnEffects?.length ? <small>{object.untilEndOfTurnEffects.join(", ")}</small> : null}
    </>
  );
}

function CommandWorkbench({
  activeKey,
  activePlayer,
  playDraft,
  visibleObjectIds,
  fixtureText,
  fixtureStatus,
  onActiveKey,
  onPlayDraft,
  onPickObject,
  onSubmitPlayCard,
  onSeedScenario,
  onRefreshFixture,
  onCopyFixture
}: {
  activeKey: PlayerKey;
  activePlayer: PlayerState;
  playDraft: PlayCardDraft;
  visibleObjectIds: string[];
  fixtureText: string;
  fixtureStatus: string;
  onActiveKey: (key: PlayerKey) => void;
  onPlayDraft: (draft: PlayCardDraft) => void;
  onPickObject: (objectId: string) => void;
  onSubmitPlayCard: () => void;
  onSeedScenario: (preset: ScenarioPreset) => void;
  onRefreshFixture: () => void;
  onCopyFixture: () => void;
}) {
  const promptActions = activePlayer.prompt?.actions ?? [];
  const canPlayCard = promptActions.includes("PLAY_CARD");

  return (
    <section className="workbench-panel" data-testid="command-workbench">
      <div className="section-title">
        <h2>Operation Panel</h2>
        <span>{activePlayer.prompt?.reason ?? "no prompt"}</span>
      </div>
      <div className="workbench-controls">
        <label>
          Active client
          <select value={activeKey} onChange={(event) => onActiveKey(event.target.value as PlayerKey)}>
            <option value="p1">P1</option>
            <option value="p2">P2</option>
          </select>
        </label>
        <div className="action-chips">
          {promptActions.map((action) => (
            <span className={activePlayer.prompt?.actionable ? "action-chip actionable" : "action-chip"} key={action}>
              {action}
            </span>
          ))}
        </div>
      </div>

      <section className="scenario-panel">
        <div className="section-title">
          <h3>Scenario Seeds</h3>
          <span>development only</span>
        </div>
        <div className="scenario-grid">
          {scenarioPresets.map((preset) => (
            <button data-testid={`seed-${preset.id}`} key={preset.id} onClick={() => onSeedScenario(preset)}>
              <strong>{preset.title}</strong>
              <small>{preset.description}</small>
            </button>
          ))}
        </div>
      </section>

      <section className="play-card-panel">
        <div className="section-title">
          <h3>PLAY_CARD Builder</h3>
          <span>{canPlayCard ? "prompt allows PLAY_CARD" : "manual submit still available below"}</span>
        </div>
        <div className="form-grid">
          <label>
            sourceObjectId
            <input
              data-testid="play-source"
              value={playDraft.sourceObjectId}
              onChange={(event) => onPlayDraft({ ...playDraft, sourceObjectId: event.target.value })}
              spellCheck={false}
            />
          </label>
          <label>
            cardNo
            <input
              data-testid="play-card-no"
              value={playDraft.cardNo}
              onChange={(event) => onPlayDraft({ ...playDraft, cardNo: event.target.value })}
              spellCheck={false}
            />
          </label>
          <label>
            mode
            <input
              data-testid="play-mode"
              value={playDraft.mode}
              onChange={(event) => onPlayDraft({ ...playDraft, mode: event.target.value })}
              spellCheck={false}
            />
          </label>
          <label>
            targetObjectIds
            <input
              data-testid="play-targets"
              value={playDraft.targetObjectIds}
              onChange={(event) => onPlayDraft({ ...playDraft, targetObjectIds: event.target.value })}
              spellCheck={false}
            />
          </label>
          <label className="wide-input">
            optionalCosts
            <input
              data-testid="play-optional-costs"
              value={playDraft.optionalCosts}
              onChange={(event) => onPlayDraft({ ...playDraft, optionalCosts: event.target.value })}
              spellCheck={false}
            />
          </label>
        </div>
        <div className="target-palette">
          {visibleObjectIds.map((objectId) => (
            <button type="button" key={objectId} onClick={() => onPickObject(objectId)}>
              {objectId}
            </button>
          ))}
        </div>
        <button data-testid="submit-play-card" onClick={onSubmitPlayCard}>
          Submit PLAY_CARD
        </button>
      </section>

      <section className="fixture-panel">
        <div className="section-title">
          <h3>Fixture Draft</h3>
          <span>{fixtureStatus}</span>
        </div>
        <div className="button-row">
          <button data-testid="refresh-fixture" onClick={onRefreshFixture}>
            Refresh Draft
          </button>
          <button data-testid="copy-fixture" onClick={onCopyFixture}>
            Copy Draft
          </button>
        </div>
        <pre data-testid="fixture-draft">{fixtureText}</pre>
      </section>
    </section>
  );
}

function PlayerPanel({
  playerKey,
  state,
  onPatch,
  onJoin,
  onReconnect,
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
  onReconnect: () => void;
  onDisconnect: () => void;
  onReady: () => void;
  onSnapshot: () => void;
  onPromptAction: (action: string) => void;
  onSubmitJson: () => void;
}) {
  const promptActions = state.prompt?.actions ?? [];

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
          <button data-testid={`${playerKey}-reconnect`} onClick={onReconnect}>
            Reconnect
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
          {promptActions.length === 0 ? (
            <button disabled>No prompt action</button>
          ) : (
            promptActions.map((action) => (
              <button
                key={action}
                data-testid={`${playerKey}-${action.toLowerCase().replaceAll("_", "-")}`}
                disabled={!state.prompt?.actionable || (!replayableActions.has(action) && action !== "PLAY_CARD")}
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
          <span>raw cmd payload</span>
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
      <pre data-testid={testId}>{value ? formatJson(value) : "null"}</pre>
    </section>
  );
}

function StatusPill({ status }: { status: ConnectionStatus }) {
  return <span className={`status-pill ${status}`}>{status}</span>;
}

function EmptyDesk() {
  return (
    <div className="empty-desk">
      <strong>No player snapshot</strong>
      <span>Join both clients to populate the desk.</span>
    </div>
  );
}

function summarizeRoom(snapshot?: SnapshotDto) {
  if (!snapshot) {
    return [
      { label: "Status", value: "no snapshot" },
      { label: "Turn", value: "-" },
      { label: "Active", value: "-" },
      { label: "Timing", value: "-" },
      { label: "Stack", value: "-" },
      { label: "Winner", value: "-" }
    ];
  }

  return [
    { label: "Status", value: String(snapshot.timing?.roomStatus ?? snapshot.turnState ?? "-") },
    { label: "Turn", value: `#${snapshot.turnNumber}` },
    { label: "Active", value: snapshot.activePlayerId || "-" },
    { label: "Timing", value: String(snapshot.timing?.timingState ?? snapshot.turnState ?? "-") },
    { label: "Stack", value: String(snapshot.stack?.length ?? 0) },
    { label: "Winner", value: String(snapshot.timing?.winnerPlayerId ?? "-") }
  ];
}

function buildPlayCardCommand(draft: PlayCardDraft) {
  const command: Record<string, unknown> = {
    cmdType: "PLAY_CARD",
    sourceObjectId: draft.sourceObjectId.trim(),
    cardNo: draft.cardNo.trim(),
    targetObjectIds: parseList(draft.targetObjectIds)
  };
  if (draft.mode.trim()) {
    command.mode = draft.mode.trim();
  }
  const optionalCosts = parseList(draft.optionalCosts);
  if (optionalCosts.length > 0) {
    command.optionalCosts = optionalCosts;
  }
  return command;
}

function draftFromCommand(command: Record<string, unknown>): PlayCardDraft {
  return {
    sourceObjectId: typeof command.sourceObjectId === "string" ? command.sourceObjectId : "",
    cardNo: typeof command.cardNo === "string" ? command.cardNo : "",
    targetObjectIds: Array.isArray(command.targetObjectIds) ? command.targetObjectIds.join(", ") : "",
    mode: typeof command.mode === "string" ? command.mode : "",
    optionalCosts: Array.isArray(command.optionalCosts) ? command.optionalCosts.join(", ") : ""
  };
}

function parseList(value: string) {
  return value
    .split(/[,\n]/)
    .map((item) => item.trim())
    .filter(Boolean);
}

function collectVisibleObjectIds(snapshot?: SnapshotDto) {
  if (!snapshot) {
    return [];
  }

  const ids = new Set<string>();
  for (const player of Object.values(snapshot.players)) {
    for (const list of [
      player.zones?.hand,
      player.zones?.base,
      player.zones?.battlefields,
      player.zones?.graveyard,
      player.zones?.banished,
      player.zones?.legendZone,
      player.zones?.championZone,
      Object.keys(player.objects ?? {})
    ]) {
      for (const id of list ?? []) {
        ids.add(id);
      }
    }
  }

  return [...ids].sort();
}

function stackLabel(item: unknown, index: number) {
  if (item && typeof item === "object" && "stackItemId" in item) {
    return String((item as { stackItemId?: unknown }).stackItemId ?? `stack-${index + 1}`);
  }
  return `stack-${index + 1}`;
}

function cardTitle(objectId: string, object?: ObjectView) {
  return object?.cardNo ? `${objectId} · ${object.cardNo}` : objectId;
}

function effectivePower(object: ObjectView) {
  return (object.power ?? 0) + (object.untilEndOfTurnPowerModifier ?? 0);
}

function objectClassName(object?: ObjectView) {
  const tags = object?.tags ?? [];
  const classes = ["object-card"];
  if (tags.some((tag) => tag.includes("UNIT"))) {
    classes.push("unit");
  }
  if (tags.some((tag) => tag.includes("EQUIPMENT") || tag.includes("武装"))) {
    classes.push("equipment");
  }
  if (object?.isFaceDown) {
    classes.push("face-down");
  }
  if (object?.isAttacking || object?.isDefending) {
    classes.push("in-combat");
  }
  if (object?.attachedToObjectId) {
    classes.push("attached");
  }
  return classes.join(" ");
}

function buildFixtureDraft(roomId: string, players: Record<PlayerKey, PlayerState>) {
  const commands = playerKeys
    .flatMap((key) => players[key].commandLog)
    .filter((entry) => entry.status === "sent" && entry.clientIntentId && (entry.method === "Ready" || entry.method === "SubmitIntent"))
    .sort((left, right) => left.id - right.id)
    .map((entry) => ({
      playerId: entry.playerId,
      clientIntentId: entry.clientIntentId,
      cmd: entry.payload
    }));

  return formatJson({
    schemaVersion: 2,
    fixtureId: `${roomId}-manual-dev-ui`,
    description: "Dev UI exported command draft.",
    source: "p2.5-dev-ui",
    auditStatus: "NEEDS_RULE_AUDIT",
    roomId,
    players: playerKeys.map((key) => players[key].playerId),
    commands
  });
}

function formatJson(value: unknown) {
  return JSON.stringify(value, null, 2);
}

function errorToText(error: unknown) {
  return error instanceof Error ? error.message : String(error);
}

function sessionStorageKey(roomId: string, playerId: string) {
  return `riftbound.p7.session.${roomId.trim()}.${playerId.trim()}`;
}

function rememberSession(roomId: string, session: PlayerSessionDto) {
  if (!roomId.trim() || !session.playerId.trim()) {
    return;
  }

  localStorage.setItem(sessionStorageKey(roomId, session.playerId), JSON.stringify(session));
}

function loadStoredSession(roomId: string, playerId: string): PlayerSessionDto | undefined {
  const raw = localStorage.getItem(sessionStorageKey(roomId, playerId));
  if (!raw) {
    return undefined;
  }

  try {
    const parsed = JSON.parse(raw) as Partial<PlayerSessionDto>;
    if (typeof parsed.playerId === "string" && typeof parsed.seat === "string" && typeof parsed.reconnectToken === "string") {
      return {
        playerId: parsed.playerId,
        seat: parsed.seat,
        reconnectToken: parsed.reconnectToken
      };
    }
  } catch {
    localStorage.removeItem(sessionStorageKey(roomId, playerId));
  }

  return undefined;
}

createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);

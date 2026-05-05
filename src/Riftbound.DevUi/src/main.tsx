import React, { useEffect, useMemo, useRef, useState } from "react";
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
  destination: string;
};

type MoveUnitDraft = {
  sourceObjectId: string;
  origin: string;
  destination: string;
  optionalCosts: string;
};

type AssembleDraft = {
  sourceObjectId: string;
  targetObjectId: string;
  optionalCosts: string;
};

type BattleDraft = {
  battlefieldId: string;
  attackerObjectIds: string;
  defenderObjectIds: string;
  optionalCosts: string;
};

type BehaviorSpecDto = {
  cardNo: string;
  cardName: string;
  cardCategoryName: string;
  functionalUnitId: string;
  status: string;
  reason: string;
  officialText: string;
  templateIds?: string[];
  implementedEffectKind?: string | null;
  implementedByCardNo?: string | null;
  keywords?: { keyword: string; rawText: string; value?: string | null }[];
  targets?: { scope: string; minCount: number; maxCount?: number | null; text: string; optional?: boolean }[];
  triggers?: { kind: string; timing: string; text: string; reason?: string }[];
  activatedAbilities?: { costText: string; effectText: string; templateIds: string[]; status: string; reason: string }[];
  staticAbilities?: { kind: string; text: string; status: string; reason: string }[];
  effects?: { templateId: string; phrase: string; status: string; reason: string }[];
};

type CatalogFilter = "all" | "conformance-pass" | "manual-deferred" | "blocked";

type ScenarioPreset = {
  id: string;
  title: string;
  description: string;
  command: Record<string, unknown>;
};

type StatusBadge = {
  label: string;
  tone?: "combat" | "defense" | "warning" | "control" | "attachment";
};

type TimelineEvent = {
  key: string;
  serverTick: number;
  kind: string;
  description: string;
  payload: Record<string, unknown>;
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
  optionalCosts: "",
  destination: ""
};

const initialMoveDraft: MoveUnitDraft = {
  sourceObjectId: "",
  origin: "BATTLEFIELD",
  destination: "BASE",
  optionalCosts: ""
};

const initialAssembleDraft: AssembleDraft = {
  sourceObjectId: "",
  targetObjectId: "",
  optionalCosts: "ASSEMBLE_RED"
};

const initialBattleDraft: BattleDraft = {
  battlefieldId: "BATTLEFIELD:P1-MAIN",
  attackerObjectIds: "",
  defenderObjectIds: "",
  optionalCosts: "COMBAT_ASSIGNMENT"
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
    id: "status-showcase",
    title: "Status Showcase",
    description: "Seeded snapshot for equipment attachment, control, damage, shields, and temporary states.",
    command: {
      cmdType: "PASS"
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
    id: "battle-declare",
    title: "Battle Declare",
    description: "P1 attacker and P2 defender are ready for combat declaration.",
    command: {
      cmdType: "DECLARE_BATTLE",
      battlefieldId: "BATTLEFIELD:P1-MAIN",
      attackerObjectIds: ["P1-BATTLE-ATTACKER-001"],
      defenderObjectIds: ["P2-BATTLE-DEFENDER-001"],
      optionalCosts: ["COMBAT_ASSIGNMENT"]
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
  const [moveDraft, setMoveDraft] = useState(initialMoveDraft);
  const [assembleDraft, setAssembleDraft] = useState(initialAssembleDraft);
  const [battleDraft, setBattleDraft] = useState(initialBattleDraft);
  const [fixtureDraft, setFixtureDraft] = useState("");
  const [fixtureStatus, setFixtureStatus] = useState("idle");
  const [catalog, setCatalog] = useState<BehaviorSpecDto[]>([]);
  const [catalogStatus, setCatalogStatus] = useState("loading");
  const [catalogQuery, setCatalogQuery] = useState("");
  const [catalogFilter, setCatalogFilter] = useState<CatalogFilter>("conformance-pass");
  const [selectedCardNo, setSelectedCardNo] = useState("");
  const connections = useRef<Record<PlayerKey, signalR.HubConnection | null>>({ p1: null, p2: null });
  const playersRef = useRef(initialPlayers);
  const logCounter = useRef(1);
  const intentCounter = useRef(1);

  const activePlayer = players[activeKey];
  const latestSnapshot = activePlayer.snapshot ?? players.p1.snapshot ?? players.p2.snapshot;
  const roomSummary = useMemo(() => summarizeRoom(latestSnapshot), [latestSnapshot]);
  const visibleObjectIds = useMemo(() => collectVisibleObjectIds(latestSnapshot), [latestSnapshot]);
  const catalogSummary = useMemo(() => summarizeCatalog(catalog), [catalog]);
  const fixtureText = fixtureDraft || buildFixtureDraft(roomId, players);

  useEffect(() => {
    const controller = new AbortController();

    async function loadCatalog() {
      setCatalogStatus("loading");
      try {
        const response = await fetch(`${apiBase(serverUrl)}/catalog/behavior-specs`, { signal: controller.signal });
        if (!response.ok) {
          throw new Error(`catalog request failed: ${response.status}`);
        }
        const specs = (await response.json()) as BehaviorSpecDto[];
        if (!controller.signal.aborted) {
          setCatalog(specs);
          setSelectedCardNo((current) => current || specs.find((spec) => spec.status === "implemented")?.cardNo || specs[0]?.cardNo || "");
          setCatalogStatus(`loaded ${specs.length}`);
        }
      } catch (error) {
        if (!controller.signal.aborted) {
          setCatalogStatus(errorToText(error));
        }
      }
    }

    void loadCatalog();

    return () => controller.abort();
  }, [serverUrl]);

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
    if (preset.id === "movement") {
      setMoveDraft({
        sourceObjectId: "P1-BATTLEFIELD-UNIT-001",
        origin: "BATTLEFIELD",
        destination: "BASE",
        optionalCosts: ""
      });
    }
    if (preset.id === "equipment") {
      setAssembleDraft({
        sourceObjectId: "P1-EQUIPMENT-LONG-SWORD",
        targetObjectId: "P1-UNIT-ASSEMBLE-TARGET",
        optionalCosts: "ASSEMBLE_RED"
      });
    }
    if (preset.id === "battle-declare") {
      setBattleDraft({
        battlefieldId: "BATTLEFIELD:P1-MAIN",
        attackerObjectIds: "P1-BATTLE-ATTACKER-001",
        defenderObjectIds: "P2-BATTLE-DEFENDER-001",
        optionalCosts: "COMBAT_ASSIGNMENT"
      });
    }
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

  async function submitMoveDraft() {
    await submitIntent(activeKey, buildMoveUnitCommand(moveDraft), players[activeKey].clientIntentId);
    updatePlayer(activeKey, (current) => ({ ...current, clientIntentId: "" }));
  }

  async function submitAssembleDraft() {
    await submitIntent(activeKey, buildAssembleCommand(assembleDraft), players[activeKey].clientIntentId);
    updatePlayer(activeKey, (current) => ({ ...current, clientIntentId: "" }));
  }

  async function submitBattleDraft() {
    await submitIntent(activeKey, buildDeclareBattleCommand(battleDraft), players[activeKey].clientIntentId);
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
    setMoveDraft((current) => ({ ...current, sourceObjectId: current.sourceObjectId || objectId }));
    setAssembleDraft((current) => ({
      ...current,
      sourceObjectId: current.sourceObjectId || objectId,
      targetObjectId: current.sourceObjectId ? current.targetObjectId || objectId : current.targetObjectId
    }));
    setBattleDraft((current) => {
      const attackers = parseList(current.attackerObjectIds);
      const defenders = parseList(current.defenderObjectIds);
      if (attackers.length === 0) {
        attackers.push(objectId);
      } else if (!defenders.includes(objectId)) {
        defenders.push(objectId);
      }

      return {
        ...current,
        attackerObjectIds: attackers.join(", "),
        defenderObjectIds: defenders.join(", ")
      };
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
          snapshot={latestSnapshot}
          playDraft={playDraft}
          moveDraft={moveDraft}
          assembleDraft={assembleDraft}
          battleDraft={battleDraft}
          visibleObjectIds={visibleObjectIds}
          fixtureText={fixtureText}
          fixtureStatus={fixtureStatus}
          onActiveKey={setActiveKey}
          onPlayDraft={setPlayDraft}
          onMoveDraft={setMoveDraft}
          onAssembleDraft={setAssembleDraft}
          onBattleDraft={setBattleDraft}
          onPickObject={addTargetObjectId}
          onSubmitPlayCard={() => void submitPlayCardDraft()}
          onSubmitMove={() => void submitMoveDraft()}
          onSubmitAssemble={() => void submitAssembleDraft()}
          onSubmitBattle={() => void submitBattleDraft()}
          onPromptAction={(action) => void submitPromptAction(activeKey, action)}
          onSeedScenario={(preset) => void seedScenario(preset)}
          onRefreshFixture={refreshFixtureDraft}
          onCopyFixture={() => void copyFixtureDraft()}
        />
      </section>

      <MatchTelemetry players={players} snapshot={latestSnapshot} roomId={roomId} />

      <CardCatalogPanel
        specs={catalog}
        status={catalogStatus}
        summary={catalogSummary}
        query={catalogQuery}
        filter={catalogFilter}
        selectedCardNo={selectedCardNo}
        onQuery={setCatalogQuery}
        onFilter={setCatalogFilter}
        onSelect={setSelectedCardNo}
      />

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
      {ids.map((id) => {
        const attachments = attachedObjectsFor(id, objects);
        return (
          <div className="object-cell" key={id}>
            <button className={objectClassName(objects?.[id])} onClick={() => onPickObject(id)}>
              <span>{cardTitle(id, objects?.[id])}</span>
              <ObjectMeta object={objects?.[id]} />
            </button>
            {attachments.length > 0 ? (
              <div className="attached-strip" aria-label={`${id} attached equipment`}>
                {attachments.map((attachment) => (
                  <button
                    className="attachment-chip"
                    key={attachment.objectId}
                    onClick={() => onPickObject(attachment.objectId ?? "")}
                    type="button"
                  >
                    {attachment.cardNo ?? attachment.objectId}
                  </button>
                ))}
              </div>
            ) : null}
          </div>
        );
      })}
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
  const badges = statusBadges(object);
  const tags = displayTags(object);

  return (
    <>
      {object.cardNo ? <small>{object.cardNo}</small> : null}
      {combatBits.length > 0 ? <small>{combatBits.join(" / ")}</small> : null}
      {stateBits.length > 0 ? <small>{stateBits.join(" / ")}</small> : null}
      {badges.length > 0 ? (
        <span className="status-badges">
          {badges.map((badge) => (
            <span className={badge.tone ? `status-badge ${badge.tone}` : "status-badge"} key={badge.label}>
              {badge.label}
            </span>
          ))}
        </span>
      ) : null}
      {tags.length ? <small>{tags.slice(0, 4).join(", ")}</small> : null}
      {object.untilEndOfTurnEffects?.length ? <small>{object.untilEndOfTurnEffects.join(", ")}</small> : null}
    </>
  );
}

function CommandWorkbench({
  activeKey,
  activePlayer,
  snapshot,
  playDraft,
  moveDraft,
  assembleDraft,
  battleDraft,
  visibleObjectIds,
  fixtureText,
  fixtureStatus,
  onActiveKey,
  onPlayDraft,
  onMoveDraft,
  onAssembleDraft,
  onBattleDraft,
  onPickObject,
  onSubmitPlayCard,
  onSubmitMove,
  onSubmitAssemble,
  onSubmitBattle,
  onPromptAction,
  onSeedScenario,
  onRefreshFixture,
  onCopyFixture
}: {
  activeKey: PlayerKey;
  activePlayer: PlayerState;
  snapshot?: SnapshotDto;
  playDraft: PlayCardDraft;
  moveDraft: MoveUnitDraft;
  assembleDraft: AssembleDraft;
  battleDraft: BattleDraft;
  visibleObjectIds: string[];
  fixtureText: string;
  fixtureStatus: string;
  onActiveKey: (key: PlayerKey) => void;
  onPlayDraft: (draft: PlayCardDraft) => void;
  onMoveDraft: (draft: MoveUnitDraft) => void;
  onAssembleDraft: (draft: AssembleDraft) => void;
  onBattleDraft: (draft: BattleDraft) => void;
  onPickObject: (objectId: string) => void;
  onSubmitPlayCard: () => void;
  onSubmitMove: () => void;
  onSubmitAssemble: () => void;
  onSubmitBattle: () => void;
  onPromptAction: (action: string) => void;
  onSeedScenario: (preset: ScenarioPreset) => void;
  onRefreshFixture: () => void;
  onCopyFixture: () => void;
}) {
  const promptActions = activePlayer.prompt?.actions ?? [];
  const promptIsActionable = Boolean(activePlayer.prompt?.actionable);
  const canPlayCard = promptIsActionable && promptActions.includes("PLAY_CARD");
  const canMove = promptIsActionable && promptActions.includes("MOVE_UNIT");
  const canAssemble = promptIsActionable && promptActions.includes("ASSEMBLE_EQUIPMENT");
  const canDeclareBattle = promptIsActionable && promptActions.includes("DECLARE_BATTLE");
  const selectedTargets = parseList(playDraft.targetObjectIds);
  const selectedOptionalCosts = parseList(playDraft.optionalCosts);

  function togglePlayTarget(objectId: string) {
    const next = selectedTargets.includes(objectId)
      ? selectedTargets.filter((targetId) => targetId !== objectId)
      : [...selectedTargets, objectId];
    onPlayDraft({ ...playDraft, targetObjectIds: next.join(", ") });
  }

  function addOptionalCost(cost: string) {
    if (!selectedOptionalCosts.includes(cost)) {
      onPlayDraft({ ...playDraft, optionalCosts: [...selectedOptionalCosts, cost].join(", ") });
    }
  }

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
            <button
              className={promptIsActionable ? "action-chip actionable" : "action-chip"}
              data-testid={`workbench-action-${action.toLowerCase().replaceAll("_", "-")}`}
              disabled={!promptIsActionable || !replayableActions.has(action)}
              key={action}
              onClick={() => onPromptAction(action)}
              type="button"
            >
              {action}
            </button>
          ))}
        </div>
      </div>

      <ResponseWindowPanel snapshot={snapshot} prompt={activePlayer.prompt} />

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
          <span>{canPlayCard ? "prompt allows PLAY_CARD" : "blocked by current prompt"}</span>
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
          <label>
            destination
            <input
              data-testid="play-destination"
              value={playDraft.destination}
              onChange={(event) => onPlayDraft({ ...playDraft, destination: event.target.value })}
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
            <button
              className={selectedTargets.includes(objectId) ? "selected" : ""}
              data-testid={`target-${objectId}`}
              type="button"
              key={objectId}
              onClick={() => {
                onPickObject(objectId);
                togglePlayTarget(objectId);
              }}
            >
              {objectId}
            </button>
          ))}
        </div>
        <div className="selection-strip">
          {selectedTargets.length === 0 ? <span>无目标</span> : selectedTargets.map((targetId) => <span key={targetId}>{targetId}</span>)}
          <button
            data-testid="clear-play-targets"
            disabled={selectedTargets.length === 0}
            onClick={() => onPlayDraft({ ...playDraft, targetObjectIds: "" })}
            type="button"
          >
            清空目标
          </button>
        </div>
        <div className="cost-palette">
          {["ECHO", "ASSEMBLE_RED", "COMBAT_ASSIGNMENT", "STANDBY_REVEAL_0", "ROAM", "SPEND_POWER:1"].map((cost) => (
            <button
              className={selectedOptionalCosts.includes(cost) ? "selected" : ""}
              data-testid={`cost-${cost.toLowerCase().replaceAll(":", "-")}`}
              key={cost}
              onClick={() => addOptionalCost(cost)}
              type="button"
            >
              {cost}
            </button>
          ))}
        </div>
        <button data-testid="submit-play-card" disabled={!canPlayCard} onClick={onSubmitPlayCard}>
          Submit PLAY_CARD
        </button>
      </section>

      <section className="command-panel">
        <div className="section-title">
          <h3>MOVE_UNIT</h3>
          <span>{canMove ? "prompt allows MOVE_UNIT" : "blocked by current prompt"}</span>
        </div>
        <div className="form-grid">
          <label>
            sourceObjectId
            <input
              data-testid="move-source"
              value={moveDraft.sourceObjectId}
              onChange={(event) => onMoveDraft({ ...moveDraft, sourceObjectId: event.target.value })}
              spellCheck={false}
            />
          </label>
          <label>
            origin
            <input
              data-testid="move-origin"
              value={moveDraft.origin}
              onChange={(event) => onMoveDraft({ ...moveDraft, origin: event.target.value })}
              spellCheck={false}
            />
          </label>
          <label>
            destination
            <input
              data-testid="move-destination"
              value={moveDraft.destination}
              onChange={(event) => onMoveDraft({ ...moveDraft, destination: event.target.value })}
              spellCheck={false}
            />
          </label>
          <label>
            optionalCosts
            <input
              data-testid="move-optional-costs"
              value={moveDraft.optionalCosts}
              onChange={(event) => onMoveDraft({ ...moveDraft, optionalCosts: event.target.value })}
              spellCheck={false}
            />
          </label>
        </div>
        <button data-testid="submit-move-unit" disabled={!canMove} onClick={onSubmitMove}>
          Submit MOVE_UNIT
        </button>
      </section>

      <section className="command-panel">
        <div className="section-title">
          <h3>ASSEMBLE_EQUIPMENT</h3>
          <span>{canAssemble ? "prompt allows ASSEMBLE_EQUIPMENT" : "blocked by current prompt"}</span>
        </div>
        <div className="form-grid">
          <label>
            sourceObjectId
            <input
              data-testid="assemble-source"
              value={assembleDraft.sourceObjectId}
              onChange={(event) => onAssembleDraft({ ...assembleDraft, sourceObjectId: event.target.value })}
              spellCheck={false}
            />
          </label>
          <label>
            targetObjectId
            <input
              data-testid="assemble-target"
              value={assembleDraft.targetObjectId}
              onChange={(event) => onAssembleDraft({ ...assembleDraft, targetObjectId: event.target.value })}
              spellCheck={false}
            />
          </label>
          <label className="wide-input">
            optionalCosts
            <input
              data-testid="assemble-optional-costs"
              value={assembleDraft.optionalCosts}
              onChange={(event) => onAssembleDraft({ ...assembleDraft, optionalCosts: event.target.value })}
              spellCheck={false}
            />
          </label>
        </div>
        <button data-testid="submit-assemble-equipment" disabled={!canAssemble} onClick={onSubmitAssemble}>
          Submit ASSEMBLE_EQUIPMENT
        </button>
      </section>

      <section className="command-panel">
        <div className="section-title">
          <h3>DECLARE_BATTLE</h3>
          <span>{canDeclareBattle ? "prompt allows DECLARE_BATTLE" : "blocked by current prompt"}</span>
        </div>
        <div className="form-grid">
          <label>
            battlefieldId
            <input
              data-testid="battlefield-id"
              value={battleDraft.battlefieldId}
              onChange={(event) => onBattleDraft({ ...battleDraft, battlefieldId: event.target.value })}
              spellCheck={false}
            />
          </label>
          <label>
            attackerObjectIds
            <input
              data-testid="battle-attackers"
              value={battleDraft.attackerObjectIds}
              onChange={(event) => onBattleDraft({ ...battleDraft, attackerObjectIds: event.target.value })}
              spellCheck={false}
            />
          </label>
          <label>
            defenderObjectIds
            <input
              data-testid="battle-defenders"
              value={battleDraft.defenderObjectIds}
              onChange={(event) => onBattleDraft({ ...battleDraft, defenderObjectIds: event.target.value })}
              spellCheck={false}
            />
          </label>
          <label>
            optionalCosts
            <input
              data-testid="battle-optional-costs"
              value={battleDraft.optionalCosts}
              onChange={(event) => onBattleDraft({ ...battleDraft, optionalCosts: event.target.value })}
              spellCheck={false}
            />
          </label>
        </div>
        <button data-testid="submit-declare-battle" disabled={!canDeclareBattle} onClick={onSubmitBattle}>
          Submit DECLARE_BATTLE
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

function ResponseWindowPanel({ snapshot, prompt }: { snapshot?: SnapshotDto; prompt?: ActionPromptDto }) {
  const timingState = String(snapshot?.timing?.timingState ?? "-");
  const priorityPlayerId = String(snapshot?.timing?.priorityPlayerId ?? "-");
  const focusPlayerId = String(snapshot?.timing?.focusPlayerId ?? "-");
  const stackLabels = (snapshot?.stack ?? []).map((item, index) => stackLabel(item, index));

  return (
    <section className="response-panel" data-testid="response-window">
      <div className="section-title">
        <h3>响应窗口</h3>
        <span>{prompt?.reason ?? "no prompt"}</span>
      </div>
      <div className="response-grid">
        <div>
          <span>Timing</span>
          <strong>{timingState}</strong>
        </div>
        <div>
          <span>Priority</span>
          <strong>{priorityPlayerId}</strong>
        </div>
        <div>
          <span>Focus</span>
          <strong>{focusPlayerId}</strong>
        </div>
        <div>
          <span>Stack</span>
          <strong>{stackLabels.length}</strong>
        </div>
      </div>
      <div className="stack-tags">
        {stackLabels.length === 0 ? <span>stack empty</span> : stackLabels.map((label) => <span key={label}>{label}</span>)}
      </div>
    </section>
  );
}

function MatchTelemetry({
  players,
  snapshot,
  roomId
}: {
  players: Record<PlayerKey, PlayerState>;
  snapshot?: SnapshotDto;
  roomId: string;
}) {
  const timeline = useMemo(() => collectTimelineEvents(players), [players]);
  const playerReports = Object.entries(snapshot?.players ?? {}).sort(([, left], [, right]) =>
    String(left.seat ?? "").localeCompare(String(right.seat ?? ""))
  );
  const roomStatus = String(snapshot?.timing?.roomStatus ?? snapshot?.turnState ?? "-");
  const winner = String(snapshot?.timing?.winnerPlayerId ?? "-");

  return (
    <section className="telemetry-grid" data-testid="match-telemetry">
      <section className="event-log-panel" data-testid="event-log">
        <div className="section-title">
          <h2>事件日志</h2>
          <span>{timeline.length} events</span>
        </div>
        <ol className="event-list">
          {timeline.length === 0 ? (
            <li className="empty-row">等待服务器事件</li>
          ) : (
            timeline.slice(0, 18).map((event) => (
              <li key={event.key}>
                <span className="event-kind">{event.kind}</span>
                <strong>{event.description}</strong>
                <small>{eventPayloadSummary(event.payload)}</small>
              </li>
            ))
          )}
        </ol>
      </section>

      <section className="report-panel" data-testid="match-report">
        <div className="section-title">
          <h2>战报摘要</h2>
          <span>{roomStatus}</span>
        </div>
        <div className="report-summary">
          <div>
            <span>Room</span>
            <strong>{roomId || "-"}</strong>
          </div>
          <div>
            <span>Turn</span>
            <strong>{snapshot ? `#${snapshot.turnNumber}` : "-"}</strong>
          </div>
          <div>
            <span>Active</span>
            <strong>{snapshot?.activePlayerId ?? "-"}</strong>
          </div>
          <div>
            <span>Winner</span>
            <strong>{winner}</strong>
          </div>
        </div>
        <div className="player-report-list">
          {playerReports.length === 0 ? (
            <div className="empty-row">暂无战报</div>
          ) : (
            playerReports.map(([playerId, player]) => (
              <div className="player-report" key={playerId}>
                <strong>{playerId}</strong>
                <span>分数 {player.score ?? 0}</span>
                <span>手牌 {player.handSize ?? 0}</span>
                <span>基地 {player.zones?.base?.length ?? 0}</span>
                <span>战场 {player.zones?.battlefields?.length ?? 0}</span>
                <span>废牌 {player.zones?.graveyard?.length ?? 0}</span>
              </div>
            ))
          )}
        </div>
      </section>

      <section className="replay-panel" data-testid="replay-spectator">
        <div className="section-title">
          <h2>回放 / 观战</h2>
          <span>deferred boundary</span>
        </div>
        <p>
          当前 Web 端只展示本房间连接收到的服务器事件和最终 snapshot；后端已有 journal/recovery 基础，但尚未开放产品级回放或旁观 API。
        </p>
        <div className="button-row">
          <button data-testid="open-replay" disabled>
            打开回放
          </button>
          <button data-testid="open-spectator" disabled>
            观战入口
          </button>
        </div>
      </section>
    </section>
  );
}

function CardCatalogPanel({
  specs,
  status,
  summary,
  query,
  filter,
  selectedCardNo,
  onQuery,
  onFilter,
  onSelect
}: {
  specs: BehaviorSpecDto[];
  status: string;
  summary: Record<string, number>;
  query: string;
  filter: CatalogFilter;
  selectedCardNo: string;
  onQuery: (query: string) => void;
  onFilter: (filter: CatalogFilter) => void;
  onSelect: (cardNo: string) => void;
}) {
  const filtered = useMemo(() => filterCatalog(specs, query, filter), [specs, query, filter]);
  const selected = specs.find((spec) => spec.cardNo === selectedCardNo) ?? filtered[0] ?? specs[0];
  const visible = filtered.slice(0, 80);

  return (
    <section className="catalog-panel" data-testid="card-catalog">
      <div className="section-title">
        <div>
          <p className="eyebrow">Card Catalog</p>
          <h2>图鉴与卡牌详情</h2>
        </div>
        <span>{status}</span>
      </div>

      <div className="catalog-toolbar">
        <label>
          Search
          <input
            data-testid="catalog-search"
            value={query}
            onChange={(event) => onQuery(event.target.value)}
            placeholder="card no, name, rule text"
            spellCheck={false}
          />
        </label>
        <label>
          Status
          <select data-testid="catalog-filter" value={filter} onChange={(event) => onFilter(event.target.value as CatalogFilter)}>
            <option value="conformance-pass">CONFORMANCE_PASS</option>
            <option value="manual-deferred">P6 manual deferred</option>
            <option value="blocked">Blocked / unimplemented</option>
            <option value="all">All statuses</option>
          </select>
        </label>
        <div className="catalog-counts" data-testid="catalog-counts">
          <span>CONFORMANCE_PASS {summary.implemented ?? 0}</span>
          <span>Manual deferred {summary["manual-rule-required"] ?? 0}</span>
          <span>Blocked {summary.unimplemented ?? 0}</span>
        </div>
      </div>

      <div className="catalog-layout">
        <div className="catalog-list" data-testid="catalog-results">
          <div className="catalog-list-summary">
            <strong>{filtered.length}</strong>
            <span>showing {visible.length}</span>
          </div>
          {visible.length === 0 ? (
            <div className="empty-row">No catalog cards match this filter.</div>
          ) : (
            visible.map((spec) => (
              <button
                className={spec.cardNo === selected?.cardNo ? "catalog-card selected" : "catalog-card"}
                data-testid={`catalog-card-${cssSafeId(spec.cardNo)}`}
                key={spec.cardNo}
                onClick={() => onSelect(spec.cardNo)}
              >
                <strong>{spec.cardName || spec.cardNo}</strong>
                <span>{spec.cardNo}</span>
                <StatusBadgeView spec={spec} />
              </button>
            ))
          )}
        </div>

        <CardDetail spec={selected} />
      </div>
    </section>
  );
}

function CardDetail({ spec }: { spec?: BehaviorSpecDto }) {
  if (!spec) {
    return (
      <section className="card-detail" data-testid="card-detail">
        <div className="empty-row">Select a card.</div>
      </section>
    );
  }

  const isManual = spec.status === "manual-rule-required";

  return (
    <section className="card-detail" data-testid="card-detail">
      <div className="card-detail-header">
        <div>
          <p className="eyebrow">{spec.cardCategoryName}</p>
          <h2>{spec.cardName || spec.cardNo}</h2>
          <span>{spec.cardNo}</span>
        </div>
        <StatusBadgeView spec={spec} />
      </div>

      {isManual ? (
        <div className="catalog-boundary" data-testid="catalog-deferred-boundary">
          P6 manual deferred: this legend/battlefield or non-PLAY_CARD surface is blocked from P7 playable controls until a backend domain implements it.
        </div>
      ) : null}

      <dl className="detail-grid">
        <div>
          <dt>Functional unit</dt>
          <dd>{spec.functionalUnitId}</dd>
        </div>
        <div>
          <dt>Implemented by</dt>
          <dd>{spec.implementedByCardNo ?? "-"}</dd>
        </div>
        <div>
          <dt>Effect kind</dt>
          <dd>{spec.implementedEffectKind ?? "-"}</dd>
        </div>
        <div>
          <dt>Templates</dt>
          <dd>{spec.templateIds?.length ? spec.templateIds.join(", ") : "-"}</dd>
        </div>
      </dl>

      <section className="card-text-block">
        <h3>Official Text</h3>
        <p>{spec.officialText || "No official rule text."}</p>
      </section>
      <section className="card-text-block">
        <h3>Behavior Reason</h3>
        <p>{spec.reason}</p>
      </section>

      <div className="spec-pill-grid">
        <SpecPills title="Keywords" values={(spec.keywords ?? []).map((item) => item.value ? `${item.keyword} ${item.value}` : item.keyword)} />
        <SpecPills title="Targets" values={(spec.targets ?? []).map((item) => `${item.scope} ${item.minCount}-${item.maxCount ?? item.minCount}`)} />
        <SpecPills title="Triggers" values={(spec.triggers ?? []).map((item) => `${item.kind}: ${item.timing}`)} />
        <SpecPills title="Effects" values={(spec.effects ?? []).map((item) => `${item.templateId}: ${statusLabel(item.status)}`)} />
      </div>
    </section>
  );
}

function StatusBadgeView({ spec }: { spec: BehaviorSpecDto }) {
  return <span className={`catalog-status ${catalogStatusClass(spec.status)}`}>{statusLabel(spec.status)}</span>;
}

function SpecPills({ title, values }: { title: string; values: string[] }) {
  return (
    <section className="spec-pill-section">
      <h3>{title}</h3>
      <div className="spec-pills">
        {values.length === 0 ? <span>none</span> : values.slice(0, 12).map((value) => <span key={value}>{value}</span>)}
      </div>
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

function summarizeCatalog(specs: BehaviorSpecDto[]) {
  return specs.reduce<Record<string, number>>((counts, spec) => {
    counts[spec.status] = (counts[spec.status] ?? 0) + 1;
    return counts;
  }, {});
}

function filterCatalog(specs: BehaviorSpecDto[], query: string, filter: CatalogFilter) {
  const normalizedQuery = query.trim().toLocaleLowerCase();
  return specs.filter((spec) => {
    if (filter === "conformance-pass" && spec.status !== "implemented") {
      return false;
    }
    if (filter === "manual-deferred" && spec.status !== "manual-rule-required") {
      return false;
    }
    if (filter === "blocked" && spec.status !== "unimplemented") {
      return false;
    }
    if (!normalizedQuery) {
      return true;
    }

    return [spec.cardNo, spec.cardName, spec.cardCategoryName, spec.officialText, spec.reason, ...(spec.templateIds ?? [])]
      .join(" ")
      .toLocaleLowerCase()
      .includes(normalizedQuery);
  });
}

function statusLabel(status: string) {
  if (status === "implemented") {
    return "CONFORMANCE_PASS";
  }
  if (status === "manual-rule-required") {
    return "P6 MANUAL DEFERRED";
  }
  if (status === "unimplemented") {
    return "BLOCKED";
  }
  return status;
}

function catalogStatusClass(status: string) {
  if (status === "implemented") {
    return "pass";
  }
  if (status === "manual-rule-required") {
    return "manual";
  }
  return "blocked";
}

function cssSafeId(value: string) {
  return value.replace(/[^a-zA-Z0-9_-]/g, "-");
}

function apiBase(serverUrl: string) {
  return serverUrl.replace(/\/+$/, "");
}

function collectTimelineEvents(players: Record<PlayerKey, PlayerState>): TimelineEvent[] {
  const seen = new Set<string>();
  const timeline: TimelineEvent[] = [];

  for (const key of playerKeys) {
    for (const message of players[key].events) {
      message.payload.forEach((event, index) => {
        const fingerprint = `${message.serverTick}:${event.kind}:${event.description}:${JSON.stringify(event.payload)}`;
        if (seen.has(fingerprint)) {
          return;
        }
        seen.add(fingerprint);
        timeline.push({
          key: `${fingerprint}:${index}`,
          serverTick: message.serverTick,
          kind: event.kind,
          description: event.description,
          payload: event.payload
        });
      });
    }
  }

  return timeline.sort((left, right) => right.serverTick - left.serverTick || left.kind.localeCompare(right.kind));
}

function eventPayloadSummary(payload: Record<string, unknown>) {
  const parts = Object.entries(payload)
    .filter(([, value]) => value !== undefined && value !== null && value !== "")
    .slice(0, 4)
    .map(([key, value]) => `${key}: ${formatPayloadValue(value)}`);
  return parts.length > 0 ? parts.join(" / ") : "no payload";
}

function formatPayloadValue(value: unknown): string {
  if (Array.isArray(value)) {
    return `[${value.map((item) => String(item)).join(", ")}]`;
  }
  if (typeof value === "object" && value !== null) {
    return JSON.stringify(value);
  }
  return String(value);
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
  if (draft.destination.trim()) {
    command.destination = draft.destination.trim();
  }
  const optionalCosts = parseList(draft.optionalCosts);
  if (optionalCosts.length > 0) {
    command.optionalCosts = optionalCosts;
  }
  return command;
}

function buildMoveUnitCommand(draft: MoveUnitDraft) {
  const command: Record<string, unknown> = {
    cmdType: "MOVE_UNIT",
    sourceObjectId: draft.sourceObjectId.trim(),
    origin: draft.origin.trim(),
    destination: draft.destination.trim()
  };
  const optionalCosts = parseList(draft.optionalCosts);
  if (optionalCosts.length > 0) {
    command.optionalCosts = optionalCosts;
  }
  return command;
}

function buildAssembleCommand(draft: AssembleDraft) {
  const command: Record<string, unknown> = {
    cmdType: "ASSEMBLE_EQUIPMENT",
    sourceObjectId: draft.sourceObjectId.trim(),
    targetObjectId: draft.targetObjectId.trim()
  };
  const optionalCosts = parseList(draft.optionalCosts);
  if (optionalCosts.length > 0) {
    command.optionalCosts = optionalCosts;
  }
  return command;
}

function buildDeclareBattleCommand(draft: BattleDraft) {
  const command: Record<string, unknown> = {
    cmdType: "DECLARE_BATTLE",
    battlefieldId: draft.battlefieldId.trim(),
    attackerObjectIds: parseList(draft.attackerObjectIds),
    defenderObjectIds: parseList(draft.defenderObjectIds)
  };
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
    optionalCosts: Array.isArray(command.optionalCosts) ? command.optionalCosts.join(", ") : "",
    destination: typeof command.destination === "string" ? command.destination : ""
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

function attachedObjectsFor(objectId: string, objects?: Record<string, ObjectView>) {
  return Object.entries(objects ?? {})
    .map(([fallbackId, object]) => ({ ...object, objectId: object.objectId || fallbackId }))
    .filter((object) => object.attachedToObjectId === objectId)
    .sort((left, right) => String(left.objectId).localeCompare(String(right.objectId)));
}

function effectivePower(object: ObjectView) {
  return (object.power ?? 0) + (object.untilEndOfTurnPowerModifier ?? 0);
}

function statusBadges(object: ObjectView): StatusBadge[] {
  const badges: StatusBadge[] = [];
  if (object.damage && object.damage > 0) {
    badges.push({ label: `${object.damage} 伤害`, tone: "combat" });
  }
  if (object.untilEndOfTurnPowerModifier && object.untilEndOfTurnPowerModifier !== 0) {
    badges.push({ label: formatPowerModifier(object.untilEndOfTurnPowerModifier), tone: "combat" });
  }
  if (object.isExhausted) {
    badges.push({ label: "横置", tone: "warning" });
  }
  if (object.isAttacking) {
    badges.push({ label: "攻击中", tone: "combat" });
  }
  if (object.isDefending) {
    badges.push({ label: "防守中", tone: "combat" });
  }
  if (object.isFaceDown) {
    badges.push({ label: "盖放", tone: "warning" });
  }
  if (object.attachedToObjectId) {
    badges.push({ label: "已贴附", tone: "attachment" });
  }
  if (object.ownerId && object.controllerId && object.ownerId !== object.controllerId) {
    badges.push({ label: `控制 ${object.controllerId}`, tone: "control" });
  }
  if (hasObjectMarker(object, ["法盾", "SPELLSHIELD"])) {
    badges.push({ label: "法盾", tone: "defense" });
  }
  if (hasObjectMarker(object, ["眩晕", "STUN"])) {
    badges.push({ label: "眩晕", tone: "warning" });
  }
  if (hasObjectMarker(object, ["瞬息", "EPHEMERAL"])) {
    badges.push({ label: "瞬息", tone: "warning" });
  }
  if (hasObjectMarker(object, ["迅捷", "SWIFT"])) {
    badges.push({ label: "迅捷", tone: "defense" });
  }
  if (hasObjectMarker(object, ["待命", "STANDBY"])) {
    badges.push({ label: "待命", tone: "warning" });
  }
  if (hasObjectMarker(object, ["伏击", "AMBUSH"])) {
    badges.push({ label: "伏击", tone: "warning" });
  }
  if (hasObjectMarker(object, ["回响", "ECHO"])) {
    badges.push({ label: "回响", tone: "defense" });
  }
  if (hasObjectMarker(object, ["增益", "BOON"])) {
    badges.push({ label: "增益", tone: "defense" });
  }
  if (hasObjectMarker(object, ["游走", "ROAM"])) {
    badges.push({ label: "游走", tone: "defense" });
  }

  return uniqueBadges(badges);
}

function displayTags(object: ObjectView) {
  return (object.tags ?? []).filter((tag) => !tag.startsWith("CARD_TYPE:"));
}

function formatPowerModifier(value: number) {
  return `${value > 0 ? "+" : ""}${value} 战力`;
}

function hasObjectMarker(object: ObjectView, needles: string[]) {
  const haystack = [...(object.tags ?? []), ...(object.untilEndOfTurnEffects ?? [])].map((value) => value.toUpperCase());
  return needles.some((needle) => {
    const normalized = needle.toUpperCase();
    return haystack.some((value) => value.includes(normalized));
  });
}

function uniqueBadges(badges: StatusBadge[]) {
  const labels = new Set<string>();
  return badges.filter((badge) => {
    if (labels.has(badge.label)) {
      return false;
    }
    labels.add(badge.label);
    return true;
  });
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
  if (object?.damage && object.damage > 0) {
    classes.push("damaged");
  }
  if (object && hasObjectMarker(object, ["法盾", "SPELLSHIELD"])) {
    classes.push("shielded");
  }
  if (object && hasObjectMarker(object, ["瞬息", "EPHEMERAL"])) {
    classes.push("ephemeral");
  }
  if (object?.ownerId && object.controllerId && object.ownerId !== object.controllerId) {
    classes.push("controlled");
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

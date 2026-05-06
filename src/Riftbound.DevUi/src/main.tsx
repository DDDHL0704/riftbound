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
  promptId?: string;
  snapshotTick?: number;
  candidates?: ActionPromptCandidateDto[];
};

type ActionPromptCandidateDto = {
  action: string;
  label: string;
  enabled: boolean;
  reason: string;
  sources?: ActionPromptChoiceDto[];
  targets?: ActionPromptChoiceDto[];
  destinations?: ActionPromptChoiceDto[];
  modes?: ActionPromptChoiceDto[];
  optionalCosts?: ActionPromptChoiceDto[];
  metadata?: Record<string, unknown>;
};

type ActionPromptChoiceDto = {
  id: string;
  label: string;
  reason?: string;
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
  battlefieldTargetObjectIds: string;
  optionalCosts: string;
};

type LegendDraft = {
  sourceObjectId: string;
  abilityId: string;
  targetObjectIds: string;
  optionalCosts: string;
};

type ActivateDraft = {
  sourceObjectId: string;
  abilityId: string;
  targetObjectIds: string;
  optionalCosts: string;
};

type OperationMode = "play" | "ability" | "move" | "assemble" | "battle" | "legend";

type CardContextRole = "source" | "target" | "destination";

type CardContextAction = {
  id: string;
  action: string;
  role: CardContextRole;
  label: string;
  reason: string;
};

type SelectionIntent =
  | "play-source"
  | "play-target"
  | "move-source"
  | "assemble-source"
  | "assemble-target"
  | "battle-attacker"
  | "battle-defender"
  | "legend-source"
  | "ability-source"
  | "ability-target";

type BehaviorSpecDto = {
  cardNo: string;
  cardName: string;
  cardCategoryName: string;
  functionalUnitId: string;
  status: string;
  reason: string;
  officialText: string;
  cost?: ParsedCostDto;
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

type ParsedCostDto = {
  mana?: number | null;
  returnEnergy?: number | null;
  power?: number | null;
  additionalCosts?: string[];
  optionalCosts?: string[];
};

type CatalogFilter = "all" | "conformance-pass" | "manual-deferred" | "blocked";
type ProductPanel = "actions" | "setup" | "log" | "catalog";

type ScenarioPreset = {
  id: string;
  title: string;
  description: string;
  command: Record<string, unknown>;
};

type GuidedAction = {
  action: string;
  title: string;
  description: string;
  scenarioIds: string[];
};

type StatusBadge = {
  label: string;
  tone?: "combat" | "defense" | "warning" | "control" | "attachment";
};

type SelectionIntentOption = {
  id: SelectionIntent;
  label: string;
  hint: string;
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
const directPromptActions = new Set([...replayableActions, "PLAY_CARD", "ACTIVATE_ABILITY", "LEGEND_ACT"]);
const operationTabs: { id: OperationMode; label: string }[] = [
  { id: "play", label: "出牌" },
  { id: "ability", label: "能力" },
  { id: "move", label: "移动" },
  { id: "assemble", label: "装备" },
  { id: "battle", label: "战斗" },
  { id: "legend", label: "传奇" }
];

const guidedActions: GuidedAction[] = [
  {
    action: "PLAY_CARD",
    title: "打出卡牌",
    description: "从手牌或服务端来源选择卡牌，再选择目标、目的地与费用。",
    scenarioIds: ["test-decks", "basic-play"]
  },
  {
    action: "ACTIVATE_ABILITY",
    title: "激活能力",
    description: "需要场上有可横置或可支付的能力来源。",
    scenarioIds: ["battlefield-unit-experience-ability"]
  },
  {
    action: "MOVE_UNIT",
    title: "移动单位",
    description: "需要服务端给出可移动单位和合法目的地。",
    scenarioIds: ["movement"]
  },
  {
    action: "ASSEMBLE_EQUIPMENT",
    title: "装配装备",
    description: "需要装备来源、宿主目标和对应装配费用。",
    scenarioIds: ["equipment"]
  },
  {
    action: "DECLARE_BATTLE",
    title: "声明战斗",
    description: "需要合法攻击方、防守方和战场分配。",
    scenarioIds: ["battle-declare", "battle-multi-defender"]
  },
  {
    action: "LEGEND_ACT",
    title: "传奇行动",
    description: "需要传奇来源、经验或法力等服务端认可的费用。",
    scenarioIds: ["legend-act", "legend-active-actions"]
  }
];

const selectionIntentOptions: SelectionIntentOption[] = [
  { id: "play-source", label: "出牌来源", hint: "点击手牌或可见对象作为出牌来源" },
  { id: "play-target", label: "出牌目标", hint: "点击对象加入或移除出牌目标" },
  { id: "move-source", label: "移动单位", hint: "点击要移动的单位" },
  { id: "assemble-source", label: "装备来源", hint: "点击要装配的装备" },
  { id: "assemble-target", label: "装备目标", hint: "点击装备宿主" },
  { id: "battle-attacker", label: "攻击方", hint: "点击单位加入战斗攻击方" },
  { id: "battle-defender", label: "防守方", hint: "点击单位加入战斗防守方" },
  { id: "legend-source", label: "传奇来源", hint: "点击传奇作为行动来源" },
  { id: "ability-source", label: "能力来源", hint: "点击单位作为能力来源" },
  { id: "ability-target", label: "能力目标", hint: "点击对象加入或移除能力目标" }
];

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
  battlefieldTargetObjectIds: "",
  optionalCosts: "COMBAT_ASSIGNMENT"
};

const initialLegendDraft: LegendDraft = {
  sourceObjectId: "",
  abilityId: "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW",
  targetObjectIds: "",
  optionalCosts: "SPEND_EXPERIENCE:3"
};

const initialActivateDraft: ActivateDraft = {
  sourceObjectId: "",
  abilityId: "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE",
  targetObjectIds: "",
  optionalCosts: ""
};

const scenarioPresets: ScenarioPreset[] = [
  {
    id: "basic-play",
    title: "基础出牌",
    description: "P1 手牌有强力妖精，并拥有 4 点法力。",
    command: {
      cmdType: "PLAY_CARD",
      sourceObjectId: "P1-UNIT-MIGHTY-FAERIE",
      cardNo: "SFD·125/221",
      targetObjectIds: []
    }
  },
  {
    id: "movement",
    title: "移动测试",
    description: "P1 可对友方战场单位打出乘风而行。",
    command: {
      cmdType: "PLAY_CARD",
      sourceObjectId: "P1-SPELL-RIDE-THE-WIND",
      cardNo: "OGN·173/298",
      targetObjectIds: ["P1-BATTLEFIELD-UNIT-001"]
    }
  },
  {
    id: "spell-duel",
    title: "法术窗口",
    description: "P1 可对 P2 战场单位打出海克斯射线。",
    command: {
      cmdType: "PLAY_CARD",
      sourceObjectId: "P1-SPELL-HEXTECH-RAY",
      cardNo: "OGN·009/298",
      targetObjectIds: ["P2-UNIT-001"]
    }
  },
  {
    id: "equipment",
    title: "装备测试",
    description: "P1 手牌有长剑，并拥有 2 点法力。",
    command: {
      cmdType: "PLAY_CARD",
      sourceObjectId: "P1-EQUIPMENT-LONG-SWORD",
      cardNo: "SFD·022/221",
      targetObjectIds: []
    }
  },
  {
    id: "status-showcase",
    title: "状态展示",
    description: "预置贴附、控制权、伤害、法盾和临时状态。",
    command: {
      cmdType: "PASS"
    }
  },
  {
    id: "control",
    title: "控制权",
    description: "P1 可对已横置的 P2 单位打出敌意接管。",
    command: {
      cmdType: "PLAY_CARD",
      sourceObjectId: "P1-SPELL-HOSTILE-TAKEOVER",
      cardNo: "SFD·202/221",
      targetObjectIds: ["P2-HOSTILE-TAKEOVER-TARGET"]
    }
  },
  {
    id: "battle-score",
    title: "战斗得分",
    description: "预置战场对象和空 P2 符文牌堆，用于结束回合得分测试。",
    command: {
      cmdType: "END_TURN"
    }
  },
  {
    id: "battle-declare",
    title: "声明战斗",
    description: "P1 攻击方与 P2 防守方已准备好声明战斗。",
    command: {
      cmdType: "DECLARE_BATTLE",
      battlefieldId: "BATTLEFIELD:P1-MAIN",
      attackerObjectIds: ["P1-BATTLE-ATTACKER-001"],
      defenderObjectIds: ["P2-BATTLE-DEFENDER-001"],
      optionalCosts: ["COMBAT_ASSIGNMENT"]
    }
  },
  {
    id: "battle-prompt-filter",
    title: "战斗候选过滤",
    description: "P1 只会看到服务端下发的合法攻击/防守候选。",
    command: {
      cmdType: "DECLARE_BATTLE",
      battlefieldId: "BATTLEFIELD:P1-MAIN",
      attackerObjectIds: ["P1-BATTLE-PROMPT-ATTACKER"],
      defenderObjectIds: ["P2-BATTLE-PROMPT-DEFENDER"],
      optionalCosts: ["COMBAT_ASSIGNMENT"]
    }
  },
  {
    id: "battle-multi-defender",
    title: "多防守者战斗",
    description: "P1 攻击壁垒与后排防守者，伤害顺序由服务端决定。",
    command: {
      cmdType: "DECLARE_BATTLE",
      battlefieldId: "BATTLEFIELD:P1-MAIN",
      attackerObjectIds: ["P1-BATTLE-MULTI-VOLIBEAR"],
      defenderObjectIds: ["P2-BATTLE-MULTI-LEBLANC", "P2-BATTLE-MULTI-KITTEN"],
      optionalCosts: ["COMBAT_ASSIGNMENT"]
    }
  },
  {
    id: "legend-act",
    title: "传奇行动",
    description: "P1 拥有波比传奇、3 点经验和可抽取卡牌。",
    command: {
      cmdType: "LEGEND_ACT",
      sourceObjectId: "P1-LEGEND-POPPY",
      abilityId: "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW",
      targetObjectIds: [],
      optionalCosts: ["SPEND_EXPERIENCE:3"]
    }
  },
  {
    id: "legend-active-actions",
    title: "传奇能力组合",
    description: "P1 拥有已实现的传奇行动来源、法力、经验和友方单位目标。",
    command: {
      cmdType: "LEGEND_ACT",
      sourceObjectId: "P1-LEGEND-YASUO",
      abilityId: "LEGEND_PAY_2_EXHAUST_MOVE_FRIENDLY_UNIT",
      targetObjectIds: ["P1-LEGEND-BATTLEFIELD-UNIT"],
      optionalCosts: ["SPEND_MANA:2"]
    }
  },
  {
    id: "battlefield-unit-experience-ability",
    title: "蜕变花园",
    description: "P1 控制蜕变花园，可横置战场单位获得经验。",
    command: {
      cmdType: "ACTIVATE_ABILITY",
      sourceObjectId: "P1-BATTLEFIELD-EXPERIENCE-UNIT",
      abilityId: "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE",
      targetObjectIds: []
    }
  },
  {
    id: "specified-hand",
    title: "指定手牌",
    description: "P1 获得多张已知可玩卡牌，用于临时复现场景。",
    command: {
      cmdType: "PLAY_CARD",
      sourceObjectId: "P1-SPELL-HEXTECH-RAY",
      cardNo: "OGN·009/298",
      targetObjectIds: ["P2-UNIT-001"]
    }
  },
  {
    id: "test-decks",
    title: "双人测试牌组",
    description: "P1/P2 各有主牌堆、符文牌堆、手牌、基地、传奇、英雄和战场单位。",
    command: {
      cmdType: "PLAY_CARD",
      sourceObjectId: "P1-UNIT-MIGHTY-FAERIE",
      cardNo: "SFD·125/221",
      targetObjectIds: []
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
  const [legendDraft, setLegendDraft] = useState(initialLegendDraft);
  const [activateDraft, setActivateDraft] = useState(initialActivateDraft);
  const [selectionIntent, setSelectionIntent] = useState<SelectionIntent>("play-target");
  const [contextObjectId, setContextObjectId] = useState("");
  const [workbenchOpen, setWorkbenchOpen] = useState(false);
  const [operationMode, setOperationMode] = useState<OperationMode>("play");
  const [devToolsOpen, setDevToolsOpen] = useState(false);
  const [productPanel, setProductPanel] = useState<ProductPanel>("actions");
  const [catalogOpen, setCatalogOpen] = useState(false);
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
  const cardNamesByNo = useMemo(() => buildCardNameMap(catalog), [catalog]);
  const cardSpecsByNo = useMemo(() => buildCardSpecMap(catalog), [catalog]);
  const systemNotices = useMemo(() => buildSystemNotices(players, catalogStatus), [players, catalogStatus]);
  const fixtureText = fixtureDraft || buildFixtureDraft(roomId, players);
  const selectedObjectIds = useMemo(
    () => collectDraftObjectSelections(playDraft, moveDraft, assembleDraft, battleDraft, legendDraft, activateDraft),
    [playDraft, moveDraft, assembleDraft, battleDraft, legendDraft, activateDraft]
  );

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
    setContextObjectId("");
    setWorkbenchOpen(false);
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
    if (preset.command.cmdType === "ACTIVATE_ABILITY") {
      setActivateDraft(activateDraftFromCommand(preset.command));
    }
    if (preset.command.cmdType === "DECLARE_BATTLE") {
      setBattleDraft(battleDraftFromCommand(preset.command));
    }
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
    if (preset.id === "legend-act") {
      setLegendDraft({
        sourceObjectId: "P1-LEGEND-POPPY",
        abilityId: "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW",
        targetObjectIds: "",
        optionalCosts: "SPEND_EXPERIENCE:3"
      });
    }
    if (preset.id === "legend-active-actions") {
      setLegendDraft({
        sourceObjectId: "P1-LEGEND-YASUO",
        abilityId: "LEGEND_PAY_2_EXHAUST_MOVE_FRIENDLY_UNIT",
        targetObjectIds: "P1-LEGEND-BATTLEFIELD-UNIT",
        optionalCosts: "SPEND_MANA:2"
      });
    }
    if (preset.id === "battlefield-unit-experience-ability") {
      setActivateDraft({
        sourceObjectId: "P1-BATTLEFIELD-EXPERIENCE-UNIT",
        abilityId: "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE",
        targetObjectIds: "",
        optionalCosts: ""
      });
    }
    if (preset.id === "test-decks") {
      setMoveDraft({
        sourceObjectId: "P1-BATTLEFIELD-UNIT-001",
        origin: "BATTLEFIELD",
        destination: "BASE",
        optionalCosts: ""
      });
      setAssembleDraft({
        sourceObjectId: "P1-EQUIPMENT-LONG-SWORD",
        targetObjectId: "P1-BATTLEFIELD-UNIT-001",
        optionalCosts: "ASSEMBLE_RED"
      });
      setBattleDraft({
        battlefieldId: "BATTLEFIELD:P1-MAIN",
        attackerObjectIds: "P1-BATTLE-ATTACKER-001",
        defenderObjectIds: "P2-BATTLE-DEFENDER-001",
        battlefieldTargetObjectIds: "",
        optionalCosts: "COMBAT_ASSIGNMENT"
      });
      setLegendDraft({
        sourceObjectId: "P1-LEGEND-POPPY",
        abilityId: "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW",
        targetObjectIds: "",
        optionalCosts: "SPEND_EXPERIENCE:3"
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

  async function submitLegendDraft() {
    await submitIntent(activeKey, buildLegendActCommand(legendDraft), players[activeKey].clientIntentId);
    updatePlayer(activeKey, (current) => ({ ...current, clientIntentId: "" }));
  }

  async function submitActivateDraft() {
    await submitIntent(activeKey, buildActivateAbilityCommand(activateDraft), players[activeKey].clientIntentId);
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

    if (action === "ACTIVATE_ABILITY") {
      setActiveKey(key);
      setCommandForPlayer(key, buildActivateAbilityCommand(activateDraft));
      return;
    }

    if (action === "LEGEND_ACT") {
      setActiveKey(key);
      setCommandForPlayer(key, buildLegendActCommand(legendDraft));
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

  function handleObjectPick(objectId: string) {
    if (!objectId) {
      return;
    }

    if (selectionIntent === "play-source") {
      const object = findObject(latestSnapshot, objectId);
      setPlayDraft((current) => ({
        ...current,
        sourceObjectId: objectId,
        cardNo: object?.cardNo ?? current.cardNo
      }));
      return;
    }

    if (selectionIntent === "play-target") {
      setPlayDraft((current) => ({
        ...current,
        targetObjectIds: toggleListValue(current.targetObjectIds, objectId).join(", ")
      }));
      return;
    }

    if (selectionIntent === "move-source") {
      setMoveDraft((current) => ({ ...current, sourceObjectId: objectId }));
      return;
    }

    if (selectionIntent === "assemble-source") {
      setAssembleDraft((current) => ({ ...current, sourceObjectId: objectId }));
      return;
    }

    if (selectionIntent === "assemble-target") {
      setAssembleDraft((current) => ({ ...current, targetObjectId: objectId }));
      return;
    }

    if (selectionIntent === "battle-attacker") {
      setBattleDraft((current) => ({
        ...current,
        attackerObjectIds: toggleListValue(current.attackerObjectIds, objectId).join(", ")
      }));
      return;
    }

    if (selectionIntent === "legend-source") {
      setLegendDraft((current) => ({ ...current, sourceObjectId: objectId }));
      return;
    }

    if (selectionIntent === "ability-source") {
      setActivateDraft((current) => ({ ...current, sourceObjectId: objectId }));
      return;
    }

    if (selectionIntent === "ability-target") {
      setActivateDraft((current) => ({
        ...current,
        targetObjectIds: toggleListValue(current.targetObjectIds, objectId).join(", ")
      }));
      return;
    }

    setBattleDraft((current) => ({
      ...current,
      defenderObjectIds: toggleListValue(current.defenderObjectIds, objectId).join(", ")
    }));
  }

  function openWorkbenchForAction(action: string) {
    const mode = operationModeForAction(action);
    if (!mode) {
      return;
    }

    setProductPanel("actions");
    setOperationMode(mode);
    setWorkbenchOpen(true);
    setSelectionIntent(defaultSelectionIntentForOperation(mode));
  }

  function handleCardContextAction(contextAction: CardContextAction, objectId: string) {
    if (!objectId) {
      return;
    }

    setContextObjectId(objectId);
    openWorkbenchForAction(contextAction.action);

    const object = findObject(latestSnapshot, objectId);
    if (contextAction.action === "PLAY_CARD") {
      if (contextAction.role === "source") {
        setPlayDraft((current) => ({
          ...current,
          sourceObjectId: objectId,
          cardNo: object?.cardNo ?? current.cardNo
        }));
        setSelectionIntent("play-target");
      } else {
        setPlayDraft((current) => ({
          ...current,
          targetObjectIds: toggleListValue(current.targetObjectIds, objectId).join(", ")
        }));
        setSelectionIntent("play-target");
      }
      return;
    }

    if (contextAction.action === "MOVE_UNIT") {
      const origin = objectCoarseZone(latestSnapshot, objectId);
      setMoveDraft((current) => ({
        ...current,
        sourceObjectId: objectId,
        origin: origin ?? current.origin,
        destination: origin === "BASE" ? "BATTLEFIELD" : origin === "BATTLEFIELD" ? "BASE" : current.destination
      }));
      setSelectionIntent("move-source");
      return;
    }

    if (contextAction.action === "ASSEMBLE_EQUIPMENT") {
      if (contextAction.role === "source") {
        setAssembleDraft((current) => ({ ...current, sourceObjectId: objectId }));
        setSelectionIntent("assemble-target");
      } else {
        setAssembleDraft((current) => ({ ...current, targetObjectId: objectId }));
        setSelectionIntent("assemble-target");
      }
      return;
    }

    if (contextAction.action === "DECLARE_BATTLE") {
      if (contextAction.role === "source") {
        setBattleDraft((current) => ({
          ...current,
          attackerObjectIds: toggleListValue(current.attackerObjectIds, objectId).join(", ")
        }));
        setSelectionIntent("battle-defender");
      } else if (contextAction.role === "target") {
        setBattleDraft((current) => ({
          ...current,
          defenderObjectIds: toggleListValue(current.defenderObjectIds, objectId).join(", ")
        }));
        setSelectionIntent("battle-defender");
      } else {
        setBattleDraft((current) => ({ ...current, battlefieldId: contextAction.id.replace(/^DECLARE_BATTLE-destination-/, "") }));
      }
      return;
    }

    if (contextAction.action === "LEGEND_ACT") {
      if (contextAction.role === "source") {
        setLegendDraft((current) => ({ ...current, sourceObjectId: objectId }));
        setSelectionIntent("legend-source");
      } else {
        setLegendDraft((current) => ({
          ...current,
          targetObjectIds: toggleListValue(current.targetObjectIds, objectId).join(", ")
        }));
        setSelectionIntent("legend-source");
      }
      return;
    }

    if (contextAction.action === "ACTIVATE_ABILITY") {
      if (contextAction.role === "source") {
        setActivateDraft((current) => ({ ...current, sourceObjectId: objectId }));
        setSelectionIntent("ability-target");
      } else {
        setActivateDraft((current) => ({
          ...current,
          targetObjectIds: toggleListValue(current.targetObjectIds, objectId).join(", ")
        }));
        setSelectionIntent("ability-target");
      }
    }
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
    <main className="product-shell">
      <section className="product-hud" aria-label="本地对战入口">
        <div className="product-brand">
          <p className="eyebrow">本地产品级对战</p>
          <h1>符文战场</h1>
          <span className="subtitle">服务端权威双人卡牌对战</span>
        </div>
        <label>
          服务器
          <input
            data-testid="server-url"
            value={serverUrl}
            onChange={(event) => setServerUrl(event.target.value)}
            spellCheck={false}
          />
        </label>
        <label>
          房间
          <input
            data-testid="room-id"
            value={roomId}
            onChange={(event) => setRoomId(event.target.value)}
            spellCheck={false}
          />
        </label>
        <label>
          视角
          <select
            data-testid="active-player"
            value={activeKey}
            onChange={(event) => setActiveKey(event.target.value as PlayerKey)}
          >
            <option value="p1">P1</option>
            <option value="p2">P2</option>
          </select>
        </label>
        <div className="product-hud-actions">
          <button data-testid="new-room" onClick={() => void newRoom()} type="button">
            新房间
          </button>
          <button data-testid="join-both" onClick={() => void runForBoth(connectAndJoin)} type="button">
            双人入座
          </button>
          <button data-testid="ready-both" onClick={() => void runForBoth(ready)} type="button">
            双方准备
          </button>
          <button data-testid="snapshot-both" onClick={() => void runForBoth(requestSnapshot)} type="button">
            同步
          </button>
          <button
            className={devToolsOpen ? "secondary-toggle active" : "secondary-toggle"}
            data-testid="toggle-dev-tools"
            onClick={() => setDevToolsOpen((current) => !current)}
            type="button"
          >
            高级
          </button>
        </div>
      </section>

      <section className="product-room-strip" aria-label="房间摘要">
        {roomSummary.map((item) => (
          <div className="product-metric" key={item.label}>
            <span>{item.label}</span>
            <strong>{item.value}</strong>
          </div>
        ))}
      </section>

      <SystemNotice notices={systemNotices} />

      <section className="product-stage" aria-label="产品级对战桌面">
        <ProductBattleArena
          snapshot={latestSnapshot}
          activePlayerId={activePlayer.playerId}
          prompt={activePlayer.prompt}
          contextObjectId={contextObjectId}
          selectedObjectIds={selectedObjectIds}
          selectionIntent={selectionIntent}
          cardNamesByNo={cardNamesByNo}
          cardSpecsByNo={cardSpecsByNo}
          onPickObject={handleObjectPick}
          onContextObject={setContextObjectId}
          onCardAction={handleCardContextAction}
        />

        <aside className="product-drawer" aria-label="对战控制台">
          <div className="product-panel-tabs" data-testid="product-panel-tabs">
            <button className={productPanel === "actions" ? "selected" : ""} onClick={() => setProductPanel("actions")} type="button">
              操作
            </button>
            <button className={productPanel === "setup" ? "selected" : ""} onClick={() => setProductPanel("setup")} type="button">
              席位
            </button>
            <button className={productPanel === "log" ? "selected" : ""} onClick={() => setProductPanel("log")} type="button">
              日志
            </button>
            <button className={productPanel === "catalog" ? "selected" : ""} onClick={() => setProductPanel("catalog")} type="button">
              图鉴
            </button>
          </div>

          {productPanel === "actions" ? (
            <ProductActionPanel
              activeKey={activeKey}
              activePlayer={activePlayer}
              snapshot={latestSnapshot}
              playDraft={playDraft}
              moveDraft={moveDraft}
              assembleDraft={assembleDraft}
              battleDraft={battleDraft}
              legendDraft={legendDraft}
              activateDraft={activateDraft}
              visibleObjectIds={visibleObjectIds}
              selectionIntent={selectionIntent}
              cardNamesByNo={cardNamesByNo}
              devToolsOpen={devToolsOpen}
              fixtureText={fixtureText}
              fixtureStatus={fixtureStatus}
              workbenchOpen={workbenchOpen}
              operationMode={operationMode}
              onActiveKey={setActiveKey}
              onSelectionIntent={setSelectionIntent}
              onWorkbenchOpen={setWorkbenchOpen}
              onOperationMode={setOperationMode}
              onPlayDraft={setPlayDraft}
              onMoveDraft={setMoveDraft}
              onAssembleDraft={setAssembleDraft}
              onBattleDraft={setBattleDraft}
              onLegendDraft={setLegendDraft}
              onActivateDraft={setActivateDraft}
              onSubmitPlayCard={() => void submitPlayCardDraft()}
              onSubmitMove={() => void submitMoveDraft()}
              onSubmitAssemble={() => void submitAssembleDraft()}
              onSubmitBattle={() => void submitBattleDraft()}
              onSubmitLegend={() => void submitLegendDraft()}
              onSubmitActivate={() => void submitActivateDraft()}
              onPromptAction={(action) => void submitPromptAction(activeKey, action)}
              onSeedScenario={(preset) => void seedScenario(preset)}
              onRefreshFixture={refreshFixtureDraft}
              onCopyFixture={() => void copyFixtureDraft()}
            />
          ) : null}

          {productPanel === "setup" ? (
            <PlayerDock
              players={players}
              devToolsOpen={devToolsOpen}
              onPatch={(key, patch) => updatePlayer(key, (current) => ({ ...current, ...patch }))}
              onJoin={(key) => void connectAndJoin(key)}
              onReconnect={(key) => void reconnect(key)}
              onDisconnect={(key) => void disconnect(key)}
              onReady={(key) => void ready(key)}
              onSnapshot={(key) => void requestSnapshot(key)}
              onPromptAction={(key, action) => void submitPromptAction(key, action)}
              onSubmitJson={(key) => void submitJsonIntent(key)}
            />
          ) : null}

          {productPanel === "log" ? (
            <MatchTelemetry players={players} snapshot={latestSnapshot} roomId={roomId} />
          ) : null}

          {productPanel === "catalog" ? (
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
              compact
              onOpenFull={() => setCatalogOpen(true)}
            />
          ) : null}
        </aside>
      </section>

      {catalogOpen ? (
        <div className="modal-backdrop" role="presentation">
          <section className="modal-panel" aria-label="完整图鉴">
            <div className="modal-header">
              <div>
                <p className="eyebrow">全卡图鉴</p>
                <h2>卡牌与规则详情</h2>
              </div>
              <button className="secondary-toggle" data-testid="close-catalog-modal" onClick={() => setCatalogOpen(false)} type="button">
                关闭
              </button>
            </div>
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
          </section>
        </div>
      ) : null}
    </main>
  );
}

function SystemNotice({ notices }: { notices: string[] }) {
  if (notices.length === 0) {
    return null;
  }

  return (
    <section className="system-notice" data-testid="system-notice" aria-live="polite">
      {notices.map((notice) => (
        <span key={notice}>{notice}</span>
      ))}
    </section>
  );
}

function ProductBattleArena({
  snapshot,
  activePlayerId,
  prompt,
  contextObjectId,
  selectedObjectIds,
  selectionIntent,
  cardNamesByNo,
  cardSpecsByNo,
  onPickObject,
  onContextObject,
  onCardAction
}: {
  snapshot?: SnapshotDto;
  activePlayerId: string;
  prompt?: ActionPromptDto;
  contextObjectId: string;
  selectedObjectIds: Set<string>;
  selectionIntent: SelectionIntent;
  cardNamesByNo: Record<string, string>;
  cardSpecsByNo: Record<string, BehaviorSpecDto>;
  onPickObject: (objectId: string) => void;
  onContextObject: (objectId: string) => void;
  onCardAction: (contextAction: CardContextAction, objectId: string) => void;
}) {
  const seats = productSeats(snapshot, activePlayerId);
  const bottomSeat = seats.find((seat) => seat.playerId === activePlayerId) ?? seats[seats.length - 1];
  const topSeat = seats.find((seat) => seat.playerId !== bottomSeat?.playerId) ?? seats[0];
  const timing = timingLabel(String(snapshot?.timing?.timingState ?? snapshot?.turnState ?? "-"));
  const stackLabels = (snapshot?.stack ?? []).map((item, index) => stackLabel(item, index));

  return (
    <section className="product-arena" data-testid="battle-desk" aria-label="产品级对战桌面">
      <div className="arena-glow" aria-hidden="true" />
      {snapshot && topSeat && bottomSeat ? (
        <>
          <ProductPlayerBanner
            seat={topSeat}
            activePlayerId={snapshot.activePlayerId}
            perspective="opponent"
          />

          <section className="product-board" aria-label="战场区域">
            <ProductSupportZones
              seat={topSeat}
              selectedObjectIds={selectedObjectIds}
              contextObjectId={contextObjectId}
              prompt={prompt}
              selectionIntent={selectionIntent}
              cardNamesByNo={cardNamesByNo}
              cardSpecsByNo={cardSpecsByNo}
              perspectivePlayerId={activePlayerId}
              onPickObject={onPickObject}
              onContextObject={onContextObject}
              onCardAction={onCardAction}
              compact
            />

            <div className="product-lane-mat">
              <ProductLane
                title="左战场"
                opponent={topSeat}
                player={bottomSeat}
                laneIndex={0}
                selectedObjectIds={selectedObjectIds}
                contextObjectId={contextObjectId}
                prompt={prompt}
                selectionIntent={selectionIntent}
                cardNamesByNo={cardNamesByNo}
                cardSpecsByNo={cardSpecsByNo}
                perspectivePlayerId={activePlayerId}
                onPickObject={onPickObject}
                onContextObject={onContextObject}
                onCardAction={onCardAction}
              />
              <ProductStackWell timing={timing} stackLabels={stackLabels} />
              <ProductLane
                title="右战场"
                opponent={topSeat}
                player={bottomSeat}
                laneIndex={1}
                selectedObjectIds={selectedObjectIds}
                contextObjectId={contextObjectId}
                prompt={prompt}
                selectionIntent={selectionIntent}
                cardNamesByNo={cardNamesByNo}
                cardSpecsByNo={cardSpecsByNo}
                perspectivePlayerId={activePlayerId}
                onPickObject={onPickObject}
                onContextObject={onContextObject}
                onCardAction={onCardAction}
              />
            </div>

            <ProductSupportZones
              seat={bottomSeat}
              selectedObjectIds={selectedObjectIds}
              contextObjectId={contextObjectId}
              prompt={prompt}
              selectionIntent={selectionIntent}
              cardNamesByNo={cardNamesByNo}
              cardSpecsByNo={cardSpecsByNo}
              perspectivePlayerId={activePlayerId}
              onPickObject={onPickObject}
              onContextObject={onContextObject}
              onCardAction={onCardAction}
            />
          </section>

          <ProductPlayerBanner
            seat={bottomSeat}
            activePlayerId={snapshot.activePlayerId}
            perspective="player"
            selectionIntent={selectionIntent}
          />
          <ProductHandShelf
            seat={bottomSeat}
            selectedObjectIds={selectedObjectIds}
            contextObjectId={contextObjectId}
            prompt={prompt}
            selectionIntent={selectionIntent}
            cardNamesByNo={cardNamesByNo}
            cardSpecsByNo={cardSpecsByNo}
            perspectivePlayerId={activePlayerId}
            onPickObject={onPickObject}
            onContextObject={onContextObject}
            onCardAction={onCardAction}
          />
        </>
      ) : (
        <ProductEmptyArena />
      )}
    </section>
  );
}

function ProductActionPanel(props: {
  activeKey: PlayerKey;
  activePlayer: PlayerState;
  snapshot?: SnapshotDto;
  playDraft: PlayCardDraft;
  moveDraft: MoveUnitDraft;
  assembleDraft: AssembleDraft;
  battleDraft: BattleDraft;
  legendDraft: LegendDraft;
  activateDraft: ActivateDraft;
  visibleObjectIds: string[];
  selectionIntent: SelectionIntent;
  cardNamesByNo: Record<string, string>;
  devToolsOpen: boolean;
  fixtureText: string;
  fixtureStatus: string;
  workbenchOpen: boolean;
  operationMode: OperationMode;
  onActiveKey: (key: PlayerKey) => void;
  onSelectionIntent: (intent: SelectionIntent) => void;
  onWorkbenchOpen: (open: boolean) => void;
  onOperationMode: (mode: OperationMode) => void;
  onPlayDraft: (draft: PlayCardDraft) => void;
  onMoveDraft: (draft: MoveUnitDraft) => void;
  onAssembleDraft: (draft: AssembleDraft) => void;
  onBattleDraft: (draft: BattleDraft) => void;
  onLegendDraft: (draft: LegendDraft) => void;
  onActivateDraft: (draft: ActivateDraft) => void;
  onSubmitPlayCard: () => void;
  onSubmitMove: () => void;
  onSubmitAssemble: () => void;
  onSubmitBattle: () => void;
  onSubmitLegend: () => void;
  onSubmitActivate: () => void;
  onPromptAction: (action: string) => void;
  onSeedScenario: (preset: ScenarioPreset) => void;
  onRefreshFixture: () => void;
  onCopyFixture: () => void;
}) {
  const prompt = props.activePlayer.prompt;
  const candidates = promptCandidatesFor(prompt);
  const actionableCandidates = candidates.filter((candidate) => candidate.enabled && candidate.action !== "WAIT");

  function handlePromptCandidate(candidate: ActionPromptCandidateDto) {
    const mode = operationModeForAction(candidate.action);
    if (mode) {
      props.onOperationMode(mode);
      props.onWorkbenchOpen(true);
      props.onSelectionIntent(defaultSelectionIntentForOperation(mode));
      return;
    }

    props.onPromptAction(candidate.action);
  }

  return (
    <section className="product-action-panel" aria-label="当前操作">
      <div className="action-spotlight">
        <div>
          <p className="eyebrow">当前提示</p>
          <h2>{prompt?.actionable ? "轮到你行动" : "等待服务器提示"}</h2>
          <span>{promptReasonLabel(prompt?.reason) || "所有可点击操作均来自服务端 ActionPrompt。"}</span>
        </div>
        <div className="prompt-version">
          <span>状态</span>
          <strong>{prompt?.snapshotTick ?? "-"}</strong>
        </div>
      </div>

      <GuidedActionCoach
        snapshot={props.snapshot}
        prompt={prompt}
        candidates={candidates}
        onSeedScenario={props.onSeedScenario}
      />

      <div className="product-prompt-actions" data-testid="product-prompt-actions">
        {candidates.length === 0 ? (
          <div className="product-prompt-empty">等待服务端候选</div>
        ) : (
          candidates.map((candidate) => (
            <button
              className={candidate.enabled ? "primary-action" : ""}
              data-testid={`product-action-${candidate.action.toLowerCase().replaceAll("_", "-")}`}
              disabled={!candidate.enabled || (!replayableActions.has(candidate.action) && !operationModeForAction(candidate.action))}
              key={candidate.action}
              onClick={() => handlePromptCandidate(candidate)}
              title={candidate.enabled ? promptReasonLabel(candidate.reason) : promptReasonLabel(candidate.reason)}
              type="button"
            >
              {candidate.label}
            </button>
          ))
        )}
      </div>

      <div className="action-boundary">
        <strong>{actionableCandidates.length}</strong>
        <span>项服务端允许操作。复杂行动需要在下方选择来源、目标与费用后提交。</span>
      </div>

      <details
        className="product-workbench-drawer"
        open={props.workbenchOpen}
        onToggle={(event) => props.onWorkbenchOpen(event.currentTarget.open)}
      >
        <summary>
          <span>目标选择与提交</span>
          <strong>{selectionIntentLabel(props.selectionIntent)}</strong>
        </summary>
        <CommandWorkbench {...props} operationMode={props.operationMode} onOperationMode={props.onOperationMode} />
      </details>
    </section>
  );
}

function GuidedActionCoach({
  snapshot,
  prompt,
  candidates,
  onSeedScenario
}: {
  snapshot?: SnapshotDto;
  prompt?: ActionPromptDto;
  candidates: ActionPromptCandidateDto[];
  onSeedScenario: (preset: ScenarioPreset) => void;
}) {
  const enabledCandidates = candidates.filter((candidate) => candidate.enabled && candidate.action !== "WAIT");
  const headline = coachHeadline(snapshot, prompt, enabledCandidates);
  const steps = coachSteps(snapshot, prompt, enabledCandidates);

  return (
    <section className="guided-coach" data-testid="guided-coach" aria-label="新手引导">
      <div className="guided-coach-header">
        <div>
          <p className="eyebrow">新手引导</p>
          <h3>{headline}</h3>
        </div>
        <span>{enabledCandidates.length} 项可执行</span>
      </div>
      <ol className="coach-steps">
        {steps.map((step) => (
          <li key={step}>{step}</li>
        ))}
      </ol>
      <div className="coach-action-grid">
        {guidedActions.map((item) => {
          const candidate = candidateFor(candidates, item.action);
          const status = guidedActionStatus(prompt, candidate);
          const scenario = firstScenarioPreset(item.scenarioIds);

          return (
            <article className={status.tone === "ready" ? "coach-action-card ready" : "coach-action-card"} key={item.action}>
              <header>
                <strong>{item.title}</strong>
                <span>{status.label}</span>
              </header>
              <p>{status.detail || item.description}</p>
              {scenario ? (
                <button
                  className="coach-scenario-button"
                  data-testid={`coach-seed-${scenario.id}`}
                  onClick={() => onSeedScenario(scenario)}
                  type="button"
                >
                  载入{scenario.title}
                </button>
              ) : null}
            </article>
          );
        })}
      </div>
    </section>
  );
}

type ProductSeat = {
  playerId: string;
  player: PlayerSummary;
};

function productSeats(snapshot: SnapshotDto | undefined, activePlayerId: string): ProductSeat[] {
  const seats = Object.entries(snapshot?.players ?? {})
    .map(([playerId, player]) => ({ playerId, player }))
    .sort((left, right) => String(left.player.seat ?? "").localeCompare(String(right.player.seat ?? "")));

  if (seats.length <= 1) {
    return seats;
  }

  const activeSeat = seats.find((seat) => seat.playerId === activePlayerId);
  if (!activeSeat) {
    return seats;
  }

  return [...seats.filter((seat) => seat.playerId !== activePlayerId), activeSeat];
}

function ProductPlayerBanner({
  seat,
  activePlayerId,
  perspective,
  selectionIntent
}: {
  seat: ProductSeat;
  activePlayerId: string;
  perspective: "player" | "opponent";
  selectionIntent?: SelectionIntent;
}) {
  const player = seat.player;
  const zones = player.zones ?? {};
  const isActive = seat.playerId === activePlayerId;

  return (
    <header className={isActive ? `product-player-banner ${perspective} active` : `product-player-banner ${perspective}`}>
      <div className="player-portrait">
        <span>{player.seat ?? seat.playerId.slice(0, 2).toUpperCase()}</span>
      </div>
      <div className="player-title">
        <span>{perspective === "player" ? "当前视角" : "对手"}</span>
        <strong>{seat.playerId}</strong>
      </div>
      <div className="player-resource-strip">
        <ProductResource label="分数" value={player.score ?? 0} />
        <ProductResource label="法力" value={player.runePool?.mana ?? 0} />
        <ProductResource label="符能" value={player.runePool?.power ?? 0} />
        <ProductResource label="经验" value={player.experience ?? 0} />
        <ProductResource label="主牌" value={zones.mainDeckCount ?? 0} />
        <ProductResource label="符文" value={zones.runeDeckCount ?? 0} />
        <ProductResource label="手牌" value={player.handSize ?? zones.hand?.length ?? 0} />
      </div>
      <div className="turn-indicator">
        <span>{isActive ? "行动方" : "等待"}</span>
        <strong>{selectionIntent ? selectionIntentLabel(selectionIntent) : "服务端状态"}</strong>
      </div>
    </header>
  );
}

function ProductResource({ label, value }: { label: string; value: number | string }) {
  return (
    <div>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function ProductSupportZones({
  seat,
  selectedObjectIds,
  contextObjectId,
  prompt,
  selectionIntent,
  cardNamesByNo,
  cardSpecsByNo,
  perspectivePlayerId,
  onPickObject,
  onContextObject,
  onCardAction,
  compact = false
}: {
  seat: ProductSeat;
  selectedObjectIds: Set<string>;
  contextObjectId: string;
  prompt?: ActionPromptDto;
  selectionIntent: SelectionIntent;
  cardNamesByNo: Record<string, string>;
  cardSpecsByNo: Record<string, BehaviorSpecDto>;
  perspectivePlayerId: string;
  onPickObject: (objectId: string) => void;
  onContextObject: (objectId: string) => void;
  onCardAction: (contextAction: CardContextAction, objectId: string) => void;
  compact?: boolean;
}) {
  const zones = seat.player.zones ?? {};
  return (
    <aside className={compact ? "product-support-zones compact" : "product-support-zones"}>
      <ProductMiniZone
        title="传奇"
        ids={zones.legendZone ?? []}
        objects={seat.player.objects}
        selectedObjectIds={selectedObjectIds}
        contextObjectId={contextObjectId}
        prompt={prompt}
        selectionIntent={selectionIntent}
        cardNamesByNo={cardNamesByNo}
        cardSpecsByNo={cardSpecsByNo}
        perspectivePlayerId={perspectivePlayerId}
        onPickObject={onPickObject}
        onContextObject={onContextObject}
        onCardAction={onCardAction}
      />
      <ProductMiniZone
        title="英雄"
        ids={zones.championZone ?? []}
        objects={seat.player.objects}
        selectedObjectIds={selectedObjectIds}
        contextObjectId={contextObjectId}
        prompt={prompt}
        selectionIntent={selectionIntent}
        cardNamesByNo={cardNamesByNo}
        cardSpecsByNo={cardSpecsByNo}
        perspectivePlayerId={perspectivePlayerId}
        onPickObject={onPickObject}
        onContextObject={onContextObject}
        onCardAction={onCardAction}
      />
      <ProductMiniZone
        title="基地"
        ids={zones.base ?? []}
        objects={seat.player.objects}
        selectedObjectIds={selectedObjectIds}
        contextObjectId={contextObjectId}
        prompt={prompt}
        selectionIntent={selectionIntent}
        cardNamesByNo={cardNamesByNo}
        cardSpecsByNo={cardSpecsByNo}
        perspectivePlayerId={perspectivePlayerId}
        onPickObject={onPickObject}
        onContextObject={onContextObject}
        onCardAction={onCardAction}
        wide
      />
    </aside>
  );
}

function ProductMiniZone({
  title,
  ids,
  objects,
  selectedObjectIds,
  contextObjectId,
  prompt,
  selectionIntent,
  cardNamesByNo,
  cardSpecsByNo,
  perspectivePlayerId,
  onPickObject,
  onContextObject,
  onCardAction,
  wide = false
}: {
  title: string;
  ids: string[];
  objects?: Record<string, ObjectView>;
  selectedObjectIds: Set<string>;
  contextObjectId: string;
  prompt?: ActionPromptDto;
  selectionIntent: SelectionIntent;
  cardNamesByNo: Record<string, string>;
  cardSpecsByNo: Record<string, BehaviorSpecDto>;
  perspectivePlayerId: string;
  onPickObject: (objectId: string) => void;
  onContextObject: (objectId: string) => void;
  onCardAction: (contextAction: CardContextAction, objectId: string) => void;
  wide?: boolean;
}) {
  return (
    <section className={wide ? "product-mini-zone wide" : "product-mini-zone"}>
      <div>
        <span>{title}</span>
        <strong>{ids.length}</strong>
      </div>
      <ProductCardList
        ids={ids}
        objects={objects}
        selectedObjectIds={selectedObjectIds}
        contextObjectId={contextObjectId}
        prompt={prompt}
        selectionIntent={selectionIntent}
        cardNamesByNo={cardNamesByNo}
        cardSpecsByNo={cardSpecsByNo}
        perspectivePlayerId={perspectivePlayerId}
        onPickObject={onPickObject}
        onContextObject={onContextObject}
        onCardAction={onCardAction}
        compact
      />
    </section>
  );
}

function ProductLane({
  title,
  opponent,
  player,
  laneIndex,
  selectedObjectIds,
  contextObjectId,
  prompt,
  selectionIntent,
  cardNamesByNo,
  cardSpecsByNo,
  perspectivePlayerId,
  onPickObject,
  onContextObject,
  onCardAction
}: {
  title: string;
  opponent: ProductSeat;
  player: ProductSeat;
  laneIndex: number;
  selectedObjectIds: Set<string>;
  contextObjectId: string;
  prompt?: ActionPromptDto;
  selectionIntent: SelectionIntent;
  cardNamesByNo: Record<string, string>;
  cardSpecsByNo: Record<string, BehaviorSpecDto>;
  perspectivePlayerId: string;
  onPickObject: (objectId: string) => void;
  onContextObject: (objectId: string) => void;
  onCardAction: (contextAction: CardContextAction, objectId: string) => void;
}) {
  return (
    <section className="product-lane" aria-label={title}>
      <div className="lane-title">
        <span>{title}</span>
        <strong>战场</strong>
      </div>
      <ProductCardList
        ids={laneIds(opponent.player.zones?.battlefields ?? [], laneIndex)}
        objects={opponent.player.objects}
        selectedObjectIds={selectedObjectIds}
        contextObjectId={contextObjectId}
        prompt={prompt}
        selectionIntent={selectionIntent}
        cardNamesByNo={cardNamesByNo}
        cardSpecsByNo={cardSpecsByNo}
        perspectivePlayerId={perspectivePlayerId}
        onPickObject={onPickObject}
        onContextObject={onContextObject}
        onCardAction={onCardAction}
      />
      <div className="lane-centerline" aria-hidden="true" />
      <ProductCardList
        ids={laneIds(player.player.zones?.battlefields ?? [], laneIndex)}
        objects={player.player.objects}
        selectedObjectIds={selectedObjectIds}
        contextObjectId={contextObjectId}
        prompt={prompt}
        selectionIntent={selectionIntent}
        cardNamesByNo={cardNamesByNo}
        cardSpecsByNo={cardSpecsByNo}
        perspectivePlayerId={perspectivePlayerId}
        onPickObject={onPickObject}
        onContextObject={onContextObject}
        onCardAction={onCardAction}
      />
    </section>
  );
}

function ProductStackWell({ timing, stackLabels }: { timing: string; stackLabels: string[] }) {
  return (
    <section className="product-stack-well" aria-label="响应窗口">
      <span>{timing}</span>
      <strong>结算栈 {stackLabels.length}</strong>
      <div>
        {stackLabels.length === 0 ? <em>空</em> : stackLabels.slice(0, 4).map((label) => <em key={label}>{label}</em>)}
      </div>
    </section>
  );
}

function ProductHandShelf({
  seat,
  selectedObjectIds,
  contextObjectId,
  prompt,
  selectionIntent,
  cardNamesByNo,
  cardSpecsByNo,
  perspectivePlayerId,
  onPickObject,
  onContextObject,
  onCardAction
}: {
  seat: ProductSeat;
  selectedObjectIds: Set<string>;
  contextObjectId: string;
  prompt?: ActionPromptDto;
  selectionIntent: SelectionIntent;
  cardNamesByNo: Record<string, string>;
  cardSpecsByNo: Record<string, BehaviorSpecDto>;
  perspectivePlayerId: string;
  onPickObject: (objectId: string) => void;
  onContextObject: (objectId: string) => void;
  onCardAction: (contextAction: CardContextAction, objectId: string) => void;
}) {
  const zones = seat.player.zones ?? {};
  const handIds = zones.hand ?? [];
  return (
    <section className="product-hand-shelf" aria-label="手牌">
      <div className="hand-title">
        <span>手牌</span>
        <strong>{handIds.length || zones.handHidden || 0}</strong>
      </div>
      {zones.handHidden && handIds.length === 0 ? (
        <div className="product-card-back-row">
          {Array.from({ length: Math.min(zones.handHidden, 7) }).map((_, index) => (
            <div className="product-card-back" key={index}>符文</div>
          ))}
        </div>
      ) : (
        <ProductCardList
          ids={handIds}
          objects={seat.player.objects}
          selectedObjectIds={selectedObjectIds}
          contextObjectId={contextObjectId}
          prompt={prompt}
          selectionIntent={selectionIntent}
          cardNamesByNo={cardNamesByNo}
          cardSpecsByNo={cardSpecsByNo}
          perspectivePlayerId={perspectivePlayerId}
          onPickObject={onPickObject}
          onContextObject={onContextObject}
          onCardAction={onCardAction}
          hand
        />
      )}
    </section>
  );
}

function ProductCardList({
  ids,
  objects,
  selectedObjectIds,
  contextObjectId,
  prompt,
  selectionIntent,
  cardNamesByNo,
  cardSpecsByNo,
  perspectivePlayerId,
  onPickObject,
  onContextObject,
  onCardAction,
  compact = false,
  hand = false
}: {
  ids: string[];
  objects?: Record<string, ObjectView>;
  selectedObjectIds: Set<string>;
  contextObjectId: string;
  prompt?: ActionPromptDto;
  selectionIntent: SelectionIntent;
  cardNamesByNo: Record<string, string>;
  cardSpecsByNo: Record<string, BehaviorSpecDto>;
  perspectivePlayerId: string;
  onPickObject: (objectId: string) => void;
  onContextObject: (objectId: string) => void;
  onCardAction: (contextAction: CardContextAction, objectId: string) => void;
  compact?: boolean;
  hand?: boolean;
}) {
  if (ids.length === 0) {
    return <div className="product-empty-zone">空</div>;
  }

  return (
    <div className={hand ? "product-card-list hand" : compact ? "product-card-list compact" : "product-card-list"}>
      {ids.map((id) => (
        <ProductCard
          key={id}
          objectId={id}
          object={objects?.[id]}
          attachments={attachedObjectsFor(id, objects)}
          selected={selectedObjectIds.has(id)}
          contextOpen={contextObjectId === id}
          contextActions={cardContextActionsForObject(id, prompt)}
          selectionIntent={selectionIntent}
          cardNamesByNo={cardNamesByNo}
          cardSpecsByNo={cardSpecsByNo}
          perspectivePlayerId={perspectivePlayerId}
          onPickObject={onPickObject}
          onContextObject={onContextObject}
          onCardAction={onCardAction}
          compact={compact}
        />
      ))}
    </div>
  );
}

function ProductCard({
  objectId,
  object,
  attachments,
  selected,
  contextOpen,
  contextActions,
  selectionIntent,
  cardNamesByNo,
  cardSpecsByNo,
  perspectivePlayerId,
  onPickObject,
  onContextObject,
  onCardAction,
  compact = false
}: {
  objectId: string;
  object?: ObjectView;
  attachments: (ObjectView & { objectId?: string })[];
  selected: boolean;
  contextOpen: boolean;
  contextActions: CardContextAction[];
  selectionIntent: SelectionIntent;
  cardNamesByNo: Record<string, string>;
  cardSpecsByNo: Record<string, BehaviorSpecDto>;
  perspectivePlayerId: string;
  onPickObject: (objectId: string) => void;
  onContextObject: (objectId: string) => void;
  onCardAction: (contextAction: CardContextAction, objectId: string) => void;
  compact?: boolean;
}) {
  const spec = cardSpecForObject(object, cardSpecsByNo);
  const badges = object ? statusBadges(object).slice(0, compact ? 2 : 4) : [];
  const stats = object ? productCardStats(object, spec) : [];
  const rules = productCardRuleLines(spec, compact);
  const isHighlighted = selected || contextOpen;
  const controller = cardControllerBadge(object, perspectivePlayerId);

  return (
    <article className="product-card-wrap">
      <button
        className={productCardClassName(object, isHighlighted, perspectivePlayerId)}
        onClick={() => onContextObject(contextOpen ? "" : objectId)}
        title={productCardTooltip(objectId, object, spec, cardNamesByNo)}
        type="button"
      >
        {controller ? (
          <span className={`controller-ribbon ${controller.tone}`}>
            {controller.label}
          </span>
        ) : null}
        <span className="product-card-name">{productCardName(objectId, object, cardNamesByNo)}</span>
        {compact ? null : <small>{object?.cardNo ?? objectId}</small>}
        {stats.length > 0 ? (
          <div className="product-card-stats">
            {stats.map((stat) => <span key={stat}>{stat}</span>)}
          </div>
        ) : null}
        {rules.length > 0 ? (
          <div className="product-card-rules">
            {rules.map((line) => <span key={line}>{line}</span>)}
          </div>
        ) : null}
        {badges.length > 0 ? (
          <div className="product-status-badges">
            {badges.map((badge) => <span className={badge.tone ?? ""} key={badge.label}>{badge.label}</span>)}
          </div>
        ) : null}
      </button>
      {attachments.length > 0 ? (
        <div className="product-attachments">
          {attachments.slice(0, 3).map((attachment) => (
            <button
              key={attachment.objectId}
              onClick={() => onContextObject(attachment.objectId ?? "")}
              type="button"
            >
              {productCardName(attachment.objectId ?? "装备", attachment, cardNamesByNo)}
            </button>
          ))}
        </div>
      ) : null}
      {contextOpen ? (
        <CardContextMenu
          actions={contextActions}
          objectId={objectId}
          selectionIntent={selectionIntent}
          onPickObject={onPickObject}
          onCardAction={onCardAction}
        />
      ) : null}
    </article>
  );
}

function CardContextMenu({
  actions,
  objectId,
  selectionIntent,
  onPickObject,
  onCardAction
}: {
  actions: CardContextAction[];
  objectId: string;
  selectionIntent: SelectionIntent;
  onPickObject: (objectId: string) => void;
  onCardAction: (contextAction: CardContextAction, objectId: string) => void;
}) {
  return (
    <div className="card-context-menu" data-testid={`card-context-${cssSafeId(objectId)}`}>
      <strong>这张牌可操作</strong>
      {actions.length === 0 ? (
        <span>当前没有服务端开放的卡牌动作</span>
      ) : (
        actions.map((action) => (
          <button
            data-testid={`card-context-action-${cssSafeId(action.id)}`}
            key={action.id}
            onClick={(event) => {
              event.stopPropagation();
              onCardAction(action, objectId);
            }}
            title={action.reason}
            type="button"
          >
            {action.label}
          </button>
        ))
      )}
      <button
        className="ghost-context-action"
        onClick={(event) => {
          event.stopPropagation();
          onPickObject(objectId);
        }}
        type="button"
      >
        作为{selectionIntentLabel(selectionIntent)}
      </button>
    </div>
  );
}

function ProductEmptyArena() {
  return (
    <section className="product-empty-arena">
      <div>
        <p className="eyebrow">等待对局</p>
        <h2>创建房间后让双方入座并准备</h2>
        <span>这里会展示正式对战桌面、两条战场、基地、手牌、传奇、英雄和服务端下发的可执行操作。</span>
      </div>
    </section>
  );
}

function laneIds(ids: string[], laneIndex: number) {
  return ids.filter((_, index) => index % 2 === laneIndex);
}

function productCardName(objectId: string, object: ObjectView | undefined, cardNamesByNo: Record<string, string>) {
  if (object?.isFaceDown) {
    return "盖放卡牌";
  }
  if (object?.cardNo && cardNamesByNo[normalizeCardNo(object.cardNo)]) {
    return cardNamesByNo[normalizeCardNo(object.cardNo)];
  }
  if (object?.cardNo) {
    return object.cardNo;
  }
  return objectId;
}

function productCardStats(object: ObjectView, spec?: BehaviorSpecDto) {
  const stats: string[] = [];
  if (!object.isFaceDown) {
    const manaCost = object.manaCost ?? spec?.cost?.mana;
    const runeCost = spec?.cost?.returnEnergy;
    const printedPower = object.power ?? spec?.cost?.power;

    if (manaCost !== undefined && manaCost !== null) {
      stats.push(`法力费用 ${manaCost}`);
    } else if (spec) {
      stats.push("法力费用 -");
    }
    if (runeCost !== undefined && runeCost !== null) {
      stats.push(`符能费用 ${runeCost}`);
    } else if (spec) {
      stats.push("符能费用 -");
    }
    if (printedPower !== undefined && printedPower !== null) {
      stats.push(`战力 ${object.power !== undefined ? effectivePower(object) : printedPower}`);
    } else if (spec && isUnitLikeSpec(spec)) {
      stats.push("战力 -");
    }
  }
  if (object.damage && object.damage > 0) {
    stats.push(`已受伤 ${object.damage}`);
  }
  return stats;
}

function productCardRuleLines(spec: BehaviorSpecDto | undefined, compact: boolean) {
  if (!spec?.officialText) {
    return [];
  }

  const maxLines = compact ? 1 : 3;
  return splitRulesText(spec.officialText)
    .slice(0, maxLines)
    .map(formatCardRulesText);
}

function productCardTooltip(
  objectId: string,
  object: ObjectView | undefined,
  spec: BehaviorSpecDto | undefined,
  cardNamesByNo: Record<string, string>
) {
  const lines = [
    productCardName(objectId, object, cardNamesByNo),
    object?.cardNo ?? objectId,
    ...productCardStats(object ?? {}, spec),
    ...(spec?.officialText ? splitRulesText(spec.officialText).map(formatCardRulesText) : [])
  ];
  return lines.filter(Boolean).join("\n");
}

function cardSpecForObject(object: ObjectView | undefined, cardSpecsByNo: Record<string, BehaviorSpecDto>) {
  if (!object?.cardNo || object.isFaceDown) {
    return undefined;
  }

  return cardSpecsByNo[normalizeCardNo(object.cardNo)];
}

function splitRulesText(text: string) {
  return text
    .split(/\n+|(?<=。)/)
    .map((line) => line.trim())
    .filter(Boolean);
}

function formatCardRulesText(text: string) {
  return text
    .replace(/\{\{S\}\}/g, "战力")
    .replace(/\{\{A\}\}/g, "符能")
    .replace(/\{\{([^}]+)\}\}/g, "$1")
    .replace(/\s+/g, " ")
    .trim();
}

function isUnitLikeSpec(spec: BehaviorSpecDto) {
  return spec.cardCategoryName.includes("单位");
}

function productCardClassName(object: ObjectView | undefined, selected: boolean, perspectivePlayerId: string) {
  const classes = ["product-card"];
  const tags = object?.tags ?? [];
  const controllerId = object?.controllerId ?? object?.ownerId;
  if (controllerId) {
    classes.push(controllerId === perspectivePlayerId ? "own-card" : "enemy-card");
  }
  if (tags.some((tag) => tag.includes("UNIT"))) {
    classes.push("unit");
  }
  if (tags.some((tag) => tag.includes("EQUIPMENT") || tag.includes("武装"))) {
    classes.push("equipment");
  }
  if (object?.isFaceDown) {
    classes.push("face-down");
  }
  if (object?.isExhausted) {
    classes.push("exhausted");
  }
  if (object?.isAttacking || object?.isDefending) {
    classes.push("combat");
  }
  if (object?.ownerId && object.controllerId && object.ownerId !== object.controllerId) {
    classes.push("controlled");
  }
  if (selected) {
    classes.push("selected");
  }
  return classes.join(" ");
}

function cardControllerBadge(object: ObjectView | undefined, perspectivePlayerId: string) {
  const controllerId = object?.controllerId ?? object?.ownerId;
  if (!controllerId) {
    return undefined;
  }

  const isOwn = controllerId === perspectivePlayerId;
  const ownerSuffix = object?.ownerId && object.controllerId && object.ownerId !== object.controllerId
    ? ` / 原属 ${object.ownerId}`
    : "";
  return {
    label: `${isOwn ? "我方" : "对方"} ${controllerId}${ownerSuffix}`,
    tone: isOwn ? "own" : "enemy"
  };
}

function BattleDesk({
  snapshot,
  activePlayerId,
  selectedObjectIds,
  selectionIntent,
  cardNamesByNo,
  onPickObject
}: {
  snapshot?: SnapshotDto;
  activePlayerId: string;
  selectedObjectIds: Set<string>;
  selectionIntent: SelectionIntent;
  cardNamesByNo: Record<string, string>;
  onPickObject: (objectId: string) => void;
}) {
  const players = Object.entries(snapshot?.players ?? {}).sort(([, left], [, right]) =>
    String(left.seat ?? "").localeCompare(String(right.seat ?? ""))
  );

  return (
    <section className="desk-panel" data-testid="battle-desk">
      <div className="section-title">
        <h2>对战桌面</h2>
        <span>{snapshot ? `${selectionIntentLabel(selectionIntent)} / ${activePlayerId}` : "尚未同步状态"}</span>
      </div>
      <div className="lane-board">
        {players.map(([playerId, player]) => (
          <PlayerDesk
            key={playerId}
            playerId={playerId}
            player={player}
            selectedObjectIds={selectedObjectIds}
            cardNamesByNo={cardNamesByNo}
            onPickObject={onPickObject}
          />
        ))}
        {players.length === 0 ? <EmptyDesk /> : null}
      </div>
      <section className="stack-strip">
        <div className="section-title">
          <h3>结算栈</h3>
          <span>{snapshot?.stack?.length ?? 0}</span>
        </div>
        <ObjectList ids={(snapshot?.stack ?? []).map((item, index) => stackLabel(item, index))} cardNamesByNo={cardNamesByNo} onPickObject={onPickObject} />
      </section>
    </section>
  );
}

function PlayerDesk({
  playerId,
  player,
  selectedObjectIds,
  cardNamesByNo,
  onPickObject
}: {
  playerId: string;
  player: PlayerSummary;
  selectedObjectIds: Set<string>;
  cardNamesByNo: Record<string, string>;
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
          <span>分数</span>
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
        <ZoneRow title="传奇" ids={zones.legendZone ?? []} objects={player.objects} selectedObjectIds={selectedObjectIds} cardNamesByNo={cardNamesByNo} onPickObject={onPickObject} compact />
        <ZoneRow title="英雄" ids={zones.championZone ?? []} objects={player.objects} selectedObjectIds={selectedObjectIds} cardNamesByNo={cardNamesByNo} onPickObject={onPickObject} compact />
      </div>
      <ZoneRow title="手牌" ids={zones.hand ?? []} hiddenCount={zones.handHidden ?? 0} objects={player.objects} selectedObjectIds={selectedObjectIds} cardNamesByNo={cardNamesByNo} onPickObject={onPickObject} />
      <div className="battlefield-pair">
        <ZoneRow title="战场 I" ids={(zones.battlefields ?? []).filter((_, index) => index % 2 === 0)} objects={player.objects} selectedObjectIds={selectedObjectIds} cardNamesByNo={cardNamesByNo} onPickObject={onPickObject} />
        <ZoneRow title="战场 II" ids={(zones.battlefields ?? []).filter((_, index) => index % 2 === 1)} objects={player.objects} selectedObjectIds={selectedObjectIds} cardNamesByNo={cardNamesByNo} onPickObject={onPickObject} />
      </div>
      <ZoneRow title="基地" ids={zones.base ?? []} objects={player.objects} selectedObjectIds={selectedObjectIds} cardNamesByNo={cardNamesByNo} onPickObject={onPickObject} />
      <div className="zone-minor-grid">
        <ZoneRow title="废牌堆" ids={zones.graveyard ?? []} objects={player.objects} selectedObjectIds={selectedObjectIds} cardNamesByNo={cardNamesByNo} onPickObject={onPickObject} compact />
        <ZoneRow title="放逐区" ids={zones.banished ?? []} objects={player.objects} selectedObjectIds={selectedObjectIds} cardNamesByNo={cardNamesByNo} onPickObject={onPickObject} compact />
      </div>
    </article>
  );
}

function ZoneRow({
  title,
  ids,
  hiddenCount = 0,
  objects,
  selectedObjectIds,
  cardNamesByNo,
  onPickObject,
  compact = false
}: {
  title: string;
  ids: string[];
  hiddenCount?: number;
  objects?: Record<string, ObjectView>;
  selectedObjectIds: Set<string>;
  cardNamesByNo: Record<string, string>;
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
      <ObjectList ids={ids} objects={objects} selectedObjectIds={selectedObjectIds} cardNamesByNo={cardNamesByNo} onPickObject={onPickObject} />
    </section>
  );
}

function ObjectList({
  ids,
  objects,
  selectedObjectIds,
  cardNamesByNo,
  onPickObject
}: {
  ids: string[];
  objects?: Record<string, ObjectView>;
  selectedObjectIds?: Set<string>;
  cardNamesByNo: Record<string, string>;
  onPickObject: (objectId: string) => void;
}) {
  if (ids.length === 0) {
    return <div className="empty-zone">空区域</div>;
  }

  return (
    <div className="object-list">
      {ids.map((id) => {
        const attachments = attachedObjectsFor(id, objects);
        return (
          <div className="object-cell" key={id}>
            <button
              className={selectedObjectIds?.has(id) ? `${objectClassName(objects?.[id])} selected` : objectClassName(objects?.[id])}
              onClick={() => onPickObject(id)}
              type="button"
            >
              <span>{cardTitle(id, objects?.[id], cardNamesByNo)}</span>
              <ObjectMeta object={objects?.[id]} />
            </button>
            {attachments.length > 0 ? (
              <div className="attached-strip" aria-label={`${id} 贴附装备`}>
                {attachments.map((attachment) => (
                  <button
                    className="attachment-chip"
                    key={attachment.objectId}
                    onClick={() => onPickObject(attachment.objectId ?? "")}
                    type="button"
                  >
                    {cardDisplayName(attachment.cardNo, attachment.objectId ?? "装备", cardNamesByNo)}
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
  legendDraft,
  activateDraft,
  visibleObjectIds,
  selectionIntent,
  cardNamesByNo,
  devToolsOpen,
  fixtureText,
  fixtureStatus,
  onActiveKey,
  onSelectionIntent,
  onPlayDraft,
  onMoveDraft,
  onAssembleDraft,
  onBattleDraft,
  onLegendDraft,
  onActivateDraft,
  onSubmitPlayCard,
  onSubmitMove,
  onSubmitAssemble,
  onSubmitBattle,
  onSubmitLegend,
  onSubmitActivate,
  onPromptAction,
  onSeedScenario,
  onRefreshFixture,
  onCopyFixture,
  operationMode,
  onOperationMode
}: {
  activeKey: PlayerKey;
  activePlayer: PlayerState;
  snapshot?: SnapshotDto;
  playDraft: PlayCardDraft;
  moveDraft: MoveUnitDraft;
  assembleDraft: AssembleDraft;
  battleDraft: BattleDraft;
  legendDraft: LegendDraft;
  activateDraft: ActivateDraft;
  visibleObjectIds: string[];
  selectionIntent: SelectionIntent;
  cardNamesByNo: Record<string, string>;
  devToolsOpen: boolean;
  fixtureText: string;
  fixtureStatus: string;
  onActiveKey: (key: PlayerKey) => void;
  onSelectionIntent: (intent: SelectionIntent) => void;
  onPlayDraft: (draft: PlayCardDraft) => void;
  onMoveDraft: (draft: MoveUnitDraft) => void;
  onAssembleDraft: (draft: AssembleDraft) => void;
  onBattleDraft: (draft: BattleDraft) => void;
  onLegendDraft: (draft: LegendDraft) => void;
  onActivateDraft: (draft: ActivateDraft) => void;
  onSubmitPlayCard: () => void;
  onSubmitMove: () => void;
  onSubmitAssemble: () => void;
  onSubmitBattle: () => void;
  onSubmitLegend: () => void;
  onSubmitActivate: () => void;
  onPromptAction: (action: string) => void;
  onSeedScenario: (preset: ScenarioPreset) => void;
  onRefreshFixture: () => void;
  onCopyFixture: () => void;
  operationMode: OperationMode;
  onOperationMode: (mode: OperationMode) => void;
}) {
  const promptActions = activePlayer.prompt?.actions ?? [];
  const promptCandidates = promptCandidatesFor(activePlayer.prompt);
  const promptIsActionable = Boolean(activePlayer.prompt?.actionable);
  const canPlayCard = promptIsActionable && promptActions.includes("PLAY_CARD");
  const canMove = promptIsActionable && promptActions.includes("MOVE_UNIT");
  const canAssemble = promptIsActionable && promptActions.includes("ASSEMBLE_EQUIPMENT");
  const canDeclareBattle = promptIsActionable && promptActions.includes("DECLARE_BATTLE");
  const canActivateAbility = promptIsActionable && promptActions.includes("ACTIVATE_ABILITY");
  const selectedTargets = parseList(playDraft.targetObjectIds);
  const selectedOptionalCosts = parseList(playDraft.optionalCosts);
  const playCandidate = candidateFor(promptCandidates, "PLAY_CARD");
  const activateCandidate = candidateFor(promptCandidates, "ACTIVATE_ABILITY");
  const moveCandidate = candidateFor(promptCandidates, "MOVE_UNIT");
  const assembleCandidate = candidateFor(promptCandidates, "ASSEMBLE_EQUIPMENT");
  const battleCandidate = candidateFor(promptCandidates, "DECLARE_BATTLE");
  const legendCandidate = candidateFor(promptCandidates, "LEGEND_ACT");
  const activateAbilityEnabled = Boolean(activateCandidate?.enabled && activateCandidate.sources?.length);
  const canLegendAct = Boolean(legendCandidate?.enabled && legendCandidate.sources?.length);
  const playTargetChoices: ActionPromptChoiceDto[] = playCandidate?.targets?.length
    ? playCandidate.targets
    : visibleObjectIds.map((objectId): ActionPromptChoiceDto => ({
        id: objectId,
        label: objectChoiceLabel(snapshot, objectId, cardNamesByNo)
      }));
  const playOptionalCostChoices: ActionPromptChoiceDto[] = playCandidate?.optionalCosts?.length
    ? playCandidate.optionalCosts
    : ["ECHO", "STANDBY_REVEAL_0", "ROAM", "SPEND_POWER:1"].map((cost): ActionPromptChoiceDto => ({ id: cost, label: optionalCostLabel(cost) }));

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

  function setPlaySource(choice: ActionPromptChoiceDto) {
    const object = findObject(snapshot, choice.id);
    onPlayDraft({
      ...playDraft,
      sourceObjectId: choice.id,
      cardNo: object?.cardNo ?? playDraft.cardNo
    });
  }

  function handleWorkbenchCandidate(candidate: ActionPromptCandidateDto) {
    const mode = operationModeForAction(candidate.action);
    if (mode) {
      onOperationMode(mode);
      onSelectionIntent(defaultSelectionIntentForOperation(mode));
      return;
    }

    onPromptAction(candidate.action);
  }

  return (
    <section className="workbench-panel" data-testid="command-workbench">
      <div className="section-title">
        <h2>操作面板</h2>
        <span>{promptReasonLabel(activePlayer.prompt?.reason) || "等待服务端提示"}</span>
      </div>
      <div className="workbench-controls">
        <label>
          当前玩家
          <select value={activeKey} onChange={(event) => onActiveKey(event.target.value as PlayerKey)}>
            <option value="p1">P1</option>
            <option value="p2">P2</option>
          </select>
        </label>
        <div className="action-chips">
          {promptCandidates.map((candidate) => (
            <button
              className={candidate.enabled ? "action-chip actionable" : "action-chip"}
              data-testid={`workbench-action-${candidate.action.toLowerCase().replaceAll("_", "-")}`}
              disabled={!candidate.enabled || (!replayableActions.has(candidate.action) && !operationModeForAction(candidate.action))}
              key={candidate.action}
              onClick={() => handleWorkbenchCandidate(candidate)}
              title={promptReasonLabel(candidate.reason)}
              type="button"
            >
              {candidate.label}
            </button>
          ))}
        </div>
      </div>

      <WorkbenchSupportPanel
        snapshot={snapshot}
        prompt={activePlayer.prompt}
        candidates={promptCandidates}
        cardNamesByNo={cardNamesByNo}
        selectionIntent={selectionIntent}
        onSelectionIntent={onSelectionIntent}
        playDraft={playDraft}
        moveDraft={moveDraft}
        assembleDraft={assembleDraft}
        battleDraft={battleDraft}
        legendDraft={legendDraft}
        activateDraft={activateDraft}
      />

      <div className="operation-tabs" data-testid="operation-tabs">
        {operationTabs.map((tab) => (
          <button
            className={operationMode === tab.id ? "selected" : ""}
            data-testid={`operation-tab-${tab.id}`}
            key={tab.id}
            onClick={() => onOperationMode(tab.id)}
            type="button"
          >
            {tab.label}
          </button>
        ))}
      </div>

      {operationMode === "play" ? (
      <section className="play-card-panel">
        <div className="section-title">
          <h3>打出卡牌</h3>
          <span>{canPlayCard ? "服务端允许打出卡牌" : "当前提示不允许"}</span>
        </div>
        <details className="advanced-fields">
          <summary>
            <span>手动参数</span>
            <strong>来源、卡号、目标、费用</strong>
          </summary>
          <div className="form-grid">
            <label>
              来源对象
              <input
                data-testid="play-source"
                value={playDraft.sourceObjectId}
                onChange={(event) => onPlayDraft({ ...playDraft, sourceObjectId: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label>
              卡牌编号
              <input
                data-testid="play-card-no"
                value={playDraft.cardNo}
                onChange={(event) => onPlayDraft({ ...playDraft, cardNo: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label>
              模式
              <input
                data-testid="play-mode"
                value={playDraft.mode}
                onChange={(event) => onPlayDraft({ ...playDraft, mode: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label>
              目标对象
              <input
                data-testid="play-targets"
                value={playDraft.targetObjectIds}
                onChange={(event) => onPlayDraft({ ...playDraft, targetObjectIds: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label>
              目的地
              <input
                data-testid="play-destination"
                value={playDraft.destination}
                onChange={(event) => onPlayDraft({ ...playDraft, destination: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label className="wide-input">
              可选费用
              <input
                data-testid="play-optional-costs"
                value={playDraft.optionalCosts}
                onChange={(event) => onPlayDraft({ ...playDraft, optionalCosts: event.target.value })}
                spellCheck={false}
              />
            </label>
          </div>
        </details>
        <ChoiceChipRow title="服务端来源" testIdPrefix="play-source-choice" choices={playCandidate?.sources} cardNamesByNo={cardNamesByNo} onPick={setPlaySource} />
        <ChoiceChipRow title="服务端目的地" testIdPrefix="play-destination-choice" choices={playCandidate?.destinations} cardNamesByNo={cardNamesByNo} onPick={(choice) => onPlayDraft({ ...playDraft, destination: choice.id })} />
        <ChoiceChipRow title="服务端模式" testIdPrefix="play-mode-choice" choices={playCandidate?.modes} cardNamesByNo={cardNamesByNo} onPick={(choice) => onPlayDraft({ ...playDraft, mode: choice.id })} />
        <div className="target-palette">
          {playTargetChoices.map((choice) => (
            <button
              className={selectedTargets.includes(choice.id) ? "selected" : ""}
              data-testid={`target-${choice.id}`}
              type="button"
              key={choice.id}
              onClick={() => {
                togglePlayTarget(choice.id);
              }}
              title={promptReasonLabel(choice.reason)}
            >
              {choiceDisplayLabel(choice, cardNamesByNo)}
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
          {playOptionalCostChoices.map((cost) => (
            <button
              className={selectedOptionalCosts.includes(cost.id) ? "selected" : ""}
              data-testid={`cost-${cost.id.toLowerCase().replaceAll(":", "-")}`}
              key={cost.id}
              onClick={() => addOptionalCost(cost.id)}
              title={promptReasonLabel(cost.reason)}
              type="button"
            >
            {choiceDisplayLabel(cost, cardNamesByNo)}
            </button>
          ))}
        </div>
        <button data-testid="submit-play-card" disabled={!canPlayCard} onClick={onSubmitPlayCard}>
          提交打出
        </button>
      </section>
      ) : null}

      {devToolsOpen ? (
        <section className="scenario-panel">
          <div className="section-title">
            <h3>本地场景</h3>
            <span>仅调试使用</span>
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
      ) : null}

      {operationMode === "ability" ? (
      <section className="command-panel">
        <div className="section-title">
          <h3>激活能力</h3>
          <span>{activateAbilityEnabled ? "服务端允许激活能力" : canActivateAbility ? "暂无可用来源" : "当前提示不允许"}</span>
        </div>
        <details className="advanced-fields">
          <summary>
            <span>手动参数</span>
            <strong>来源、能力、目标、费用</strong>
          </summary>
          <div className="form-grid">
            <label>
              来源对象
              <input
                data-testid="ability-source"
                value={activateDraft.sourceObjectId}
                onChange={(event) => onActivateDraft({ ...activateDraft, sourceObjectId: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label>
              能力
              <input
                data-testid="ability-id"
                value={activateDraft.abilityId}
                onChange={(event) => onActivateDraft({ ...activateDraft, abilityId: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label>
              目标对象
              <input
                data-testid="ability-targets"
                value={activateDraft.targetObjectIds}
                onChange={(event) => onActivateDraft({ ...activateDraft, targetObjectIds: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label>
              可选费用
              <input
                data-testid="ability-optional-costs"
                value={activateDraft.optionalCosts}
                onChange={(event) => onActivateDraft({ ...activateDraft, optionalCosts: event.target.value })}
                spellCheck={false}
              />
            </label>
          </div>
        </details>
        <ChoiceChipRow title="服务端来源" testIdPrefix="ability-source-choice" choices={activateCandidate?.sources} cardNamesByNo={cardNamesByNo} onPick={(choice) => onActivateDraft({ ...activateDraft, sourceObjectId: choice.id })} />
        <ChoiceChipRow title="服务端能力" testIdPrefix="ability-id-choice" choices={activateCandidate?.modes} cardNamesByNo={cardNamesByNo} onPick={(choice) => onActivateDraft({ ...activateDraft, abilityId: choice.id })} />
        <ChoiceChipRow
          title="服务端目标"
          testIdPrefix="ability-target-choice"
          choices={activateCandidate?.targets}
          cardNamesByNo={cardNamesByNo}
          onPick={(choice) => onActivateDraft({ ...activateDraft, targetObjectIds: toggleListValue(activateDraft.targetObjectIds, choice.id).join(", ") })}
        />
        <ChoiceChipRow title="服务端费用" testIdPrefix="ability-cost-choice" choices={activateCandidate?.optionalCosts} cardNamesByNo={cardNamesByNo} onPick={(choice) => onActivateDraft({ ...activateDraft, optionalCosts: choice.id })} />
        <button data-testid="submit-activate-ability" disabled={!activateAbilityEnabled} onClick={onSubmitActivate}>
          提交激活
        </button>
      </section>
      ) : null}

      {operationMode === "move" ? (
      <section className="command-panel">
        <div className="section-title">
          <h3>移动单位</h3>
          <span>{canMove ? "服务端允许移动" : "当前提示不允许"}</span>
        </div>
        <details className="advanced-fields">
          <summary>
            <span>手动参数</span>
            <strong>来源、区域、目的地</strong>
          </summary>
          <div className="form-grid">
            <label>
              来源单位
              <input
                data-testid="move-source"
                value={moveDraft.sourceObjectId}
                onChange={(event) => onMoveDraft({ ...moveDraft, sourceObjectId: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label>
              来源区域
              <input
                data-testid="move-origin"
                value={moveDraft.origin}
                onChange={(event) => onMoveDraft({ ...moveDraft, origin: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label>
              目的地
              <input
                data-testid="move-destination"
                value={moveDraft.destination}
                onChange={(event) => onMoveDraft({ ...moveDraft, destination: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label>
              可选费用
              <input
                data-testid="move-optional-costs"
                value={moveDraft.optionalCosts}
                onChange={(event) => onMoveDraft({ ...moveDraft, optionalCosts: event.target.value })}
                spellCheck={false}
              />
            </label>
          </div>
        </details>
        <ChoiceChipRow title="服务端来源" testIdPrefix="move-source-choice" choices={moveCandidate?.sources} cardNamesByNo={cardNamesByNo} onPick={(choice) => onMoveDraft({ ...moveDraft, sourceObjectId: choice.id })} />
        <ChoiceChipRow title="服务端目的地" testIdPrefix="move-destination-choice" choices={moveCandidate?.destinations} cardNamesByNo={cardNamesByNo} onPick={(choice) => onMoveDraft({ ...moveDraft, destination: choice.id })} />
        <button data-testid="submit-move-unit" disabled={!canMove} onClick={onSubmitMove}>
          提交移动
        </button>
      </section>
      ) : null}

      {operationMode === "assemble" ? (
      <section className="command-panel">
        <div className="section-title">
          <h3>装配装备</h3>
          <span>{canAssemble ? "服务端允许装配" : "当前提示不允许"}</span>
        </div>
        <details className="advanced-fields">
          <summary>
            <span>手动参数</span>
            <strong>装备、宿主、费用</strong>
          </summary>
          <div className="form-grid">
            <label>
              装备来源
              <input
                data-testid="assemble-source"
                value={assembleDraft.sourceObjectId}
                onChange={(event) => onAssembleDraft({ ...assembleDraft, sourceObjectId: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label>
              装备宿主
              <input
                data-testid="assemble-target"
                value={assembleDraft.targetObjectId}
                onChange={(event) => onAssembleDraft({ ...assembleDraft, targetObjectId: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label className="wide-input">
              可选费用
              <input
                data-testid="assemble-optional-costs"
                value={assembleDraft.optionalCosts}
                onChange={(event) => onAssembleDraft({ ...assembleDraft, optionalCosts: event.target.value })}
                spellCheck={false}
              />
            </label>
          </div>
        </details>
        <ChoiceChipRow title="服务端装备" testIdPrefix="assemble-source-choice" choices={assembleCandidate?.sources} cardNamesByNo={cardNamesByNo} onPick={(choice) => onAssembleDraft({ ...assembleDraft, sourceObjectId: choice.id })} />
        <ChoiceChipRow title="服务端宿主" testIdPrefix="assemble-target-choice" choices={assembleCandidate?.targets} cardNamesByNo={cardNamesByNo} onPick={(choice) => onAssembleDraft({ ...assembleDraft, targetObjectId: choice.id })} />
        <ChoiceChipRow
          title="服务端费用"
          testIdPrefix="assemble-cost-choice"
          choices={assembleCandidate?.optionalCosts}
          cardNamesByNo={cardNamesByNo}
          onPick={(choice) => onAssembleDraft({ ...assembleDraft, optionalCosts: choice.id })}
        />
        <button data-testid="submit-assemble-equipment" disabled={!canAssemble} onClick={onSubmitAssemble}>
          提交装配
        </button>
      </section>
      ) : null}

      {operationMode === "battle" ? (
      <section className="command-panel">
        <div className="section-title">
          <h3>声明战斗</h3>
          <span>{canDeclareBattle ? "服务端允许声明战斗" : "当前提示不允许"}</span>
        </div>
        <details className="advanced-fields">
          <summary>
            <span>手动参数</span>
            <strong>战场、攻防、分配</strong>
          </summary>
          <div className="form-grid">
            <label>
              战场
              <input
                data-testid="battlefield-id"
                value={battleDraft.battlefieldId}
                onChange={(event) => onBattleDraft({ ...battleDraft, battlefieldId: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label>
              攻击方
              <input
                data-testid="battle-attackers"
                value={battleDraft.attackerObjectIds}
                onChange={(event) => onBattleDraft({ ...battleDraft, attackerObjectIds: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label>
              防守方
              <input
                data-testid="battle-defenders"
                value={battleDraft.defenderObjectIds}
                onChange={(event) => onBattleDraft({ ...battleDraft, defenderObjectIds: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label>
              战场目标
              <input
                data-testid="battlefield-targets"
                value={battleDraft.battlefieldTargetObjectIds}
                onChange={(event) => onBattleDraft({ ...battleDraft, battlefieldTargetObjectIds: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label>
              可选费用
              <input
                data-testid="battle-optional-costs"
                value={battleDraft.optionalCosts}
                onChange={(event) => onBattleDraft({ ...battleDraft, optionalCosts: event.target.value })}
                spellCheck={false}
              />
            </label>
          </div>
        </details>
        <ChoiceChipRow title="服务端战场" testIdPrefix="battle-destination-choice" choices={battleCandidate?.destinations} cardNamesByNo={cardNamesByNo} onPick={(choice) => onBattleDraft({ ...battleDraft, battlefieldId: choice.id })} />
        <ChoiceChipRow
          title="服务端攻击方"
          testIdPrefix="battle-attacker-choice"
          choices={battleCandidate?.sources}
          cardNamesByNo={cardNamesByNo}
          onPick={(choice) => onBattleDraft({ ...battleDraft, attackerObjectIds: toggleListValue(battleDraft.attackerObjectIds, choice.id).join(", ") })}
        />
        <ChoiceChipRow
          title="服务端防守方"
          testIdPrefix="battle-defender-choice"
          choices={battleCandidate?.targets}
          cardNamesByNo={cardNamesByNo}
          onPick={(choice) => onBattleDraft({ ...battleDraft, defenderObjectIds: toggleListValue(battleDraft.defenderObjectIds, choice.id).join(", ") })}
        />
        <ChoiceChipRow
          title="服务端战场目标"
          testIdPrefix="battlefield-target-choice"
          choices={battleCandidate?.targets}
          cardNamesByNo={cardNamesByNo}
          onPick={(choice) => onBattleDraft({ ...battleDraft, battlefieldTargetObjectIds: toggleListValue(battleDraft.battlefieldTargetObjectIds, choice.id).join(", ") })}
        />
        <ChoiceChipRow
          title="服务端费用"
          testIdPrefix="battle-cost-choice"
          choices={battleCandidate?.optionalCosts}
          cardNamesByNo={cardNamesByNo}
          onPick={(choice) => onBattleDraft({ ...battleDraft, optionalCosts: choice.id })}
        />
        <button data-testid="submit-declare-battle" disabled={!canDeclareBattle} onClick={onSubmitBattle}>
          提交战斗
        </button>
      </section>
      ) : null}

      {operationMode === "legend" ? (
      <section className="command-panel">
        <div className="section-title">
          <h3>传奇行动</h3>
          <span>{canLegendAct ? "服务端允许传奇行动" : "当前提示不允许"}</span>
        </div>
        <details className="advanced-fields">
          <summary>
            <span>手动参数</span>
            <strong>传奇、能力、目标</strong>
          </summary>
          <div className="form-grid">
            <label>
              传奇来源
              <input
                data-testid="legend-source"
                value={legendDraft.sourceObjectId}
                onChange={(event) => onLegendDraft({ ...legendDraft, sourceObjectId: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label>
              能力
              <input
                data-testid="legend-ability"
                value={legendDraft.abilityId}
                onChange={(event) => onLegendDraft({ ...legendDraft, abilityId: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label>
              目标对象
              <input
                data-testid="legend-targets"
                value={legendDraft.targetObjectIds}
                onChange={(event) => onLegendDraft({ ...legendDraft, targetObjectIds: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label>
              可选费用
              <input
                data-testid="legend-optional-costs"
                value={legendDraft.optionalCosts}
                onChange={(event) => onLegendDraft({ ...legendDraft, optionalCosts: event.target.value })}
                spellCheck={false}
              />
            </label>
          </div>
        </details>
        <ChoiceChipRow title="服务端传奇" testIdPrefix="legend-source-choice" choices={legendCandidate?.sources} cardNamesByNo={cardNamesByNo} onPick={(choice) => onLegendDraft({ ...legendDraft, sourceObjectId: choice.id })} />
        <ChoiceChipRow title="服务端能力" testIdPrefix="legend-ability-choice" choices={legendCandidate?.modes} cardNamesByNo={cardNamesByNo} onPick={(choice) => onLegendDraft({ ...legendDraft, abilityId: choice.id })} />
        <ChoiceChipRow
          title="服务端目标"
          testIdPrefix="legend-target-choice"
          choices={legendCandidate?.targets}
          cardNamesByNo={cardNamesByNo}
          onPick={(choice) => onLegendDraft({ ...legendDraft, targetObjectIds: toggleListValue(legendDraft.targetObjectIds, choice.id).join(", ") })}
        />
        <ChoiceChipRow title="服务端费用" testIdPrefix="legend-cost-choice" choices={legendCandidate?.optionalCosts} cardNamesByNo={cardNamesByNo} onPick={(choice) => onLegendDraft({ ...legendDraft, optionalCosts: choice.id })} />
        <button data-testid="submit-legend-act" disabled={!canLegendAct} onClick={onSubmitLegend}>
          提交传奇行动
        </button>
      </section>
      ) : null}

      {devToolsOpen ? (
      <section className="fixture-panel">
        <div className="section-title">
          <h3>Fixture 草稿</h3>
          <span>{fixtureStatus}</span>
        </div>
        <div className="button-row">
          <button data-testid="refresh-fixture" onClick={onRefreshFixture}>
            刷新草稿
          </button>
          <button data-testid="copy-fixture" onClick={onCopyFixture}>
            复制草稿
          </button>
        </div>
        <pre data-testid="fixture-draft">{fixtureText}</pre>
      </section>
      ) : null}
    </section>
  );
}

function WorkbenchSupportPanel({
  snapshot,
  prompt,
  candidates,
  cardNamesByNo,
  selectionIntent,
  onSelectionIntent,
  playDraft,
  moveDraft,
  assembleDraft,
  battleDraft,
  legendDraft,
  activateDraft
}: {
  snapshot?: SnapshotDto;
  prompt?: ActionPromptDto;
  candidates: ActionPromptCandidateDto[];
  cardNamesByNo: Record<string, string>;
  selectionIntent: SelectionIntent;
  onSelectionIntent: (intent: SelectionIntent) => void;
  playDraft: PlayCardDraft;
  moveDraft: MoveUnitDraft;
  assembleDraft: AssembleDraft;
  battleDraft: BattleDraft;
  legendDraft: LegendDraft;
  activateDraft: ActivateDraft;
}) {
  const actionableCandidates = candidates.filter((candidate) => candidate.enabled && candidate.action !== "WAIT");
  const timing = timingLabel(String(snapshot?.timing?.timingState ?? snapshot?.turnState ?? "-"));
  const stackCount = snapshot?.stack?.length ?? 0;

  return (
    <div className="workbench-support-panel" data-testid="workbench-support">
      <details className="workbench-fold">
        <summary>
          <span>响应窗口</span>
          <strong>{timing} / 结算栈 {stackCount}</strong>
        </summary>
        <ResponseWindowPanel snapshot={snapshot} prompt={prompt} />
      </details>
      <details className="workbench-fold">
        <summary>
          <span>服务端候选</span>
          <strong>{actionableCandidates.length} 项可执行</strong>
        </summary>
        <PromptCandidatePanel candidates={candidates} cardNamesByNo={cardNamesByNo} />
      </details>
      <details className="workbench-fold">
        <summary>
          <span>点击与草稿</span>
          <strong>{selectionIntentLabel(selectionIntent)}</strong>
        </summary>
        <SelectionModePanel selectionIntent={selectionIntent} onSelectionIntent={onSelectionIntent} />
        <IntentSummaryPanel
          prompt={prompt}
          selectionIntent={selectionIntent}
          playDraft={playDraft}
          moveDraft={moveDraft}
          assembleDraft={assembleDraft}
          battleDraft={battleDraft}
          legendDraft={legendDraft}
          activateDraft={activateDraft}
        />
      </details>
    </div>
  );
}

function PromptCandidatePanel({
  candidates,
  cardNamesByNo
}: {
  candidates: ActionPromptCandidateDto[];
  cardNamesByNo: Record<string, string>;
}) {
  const actionableCandidates = candidates.filter((candidate) => candidate.enabled && candidate.action !== "WAIT");
  return (
    <section className="prompt-candidate-panel" data-testid="prompt-candidates">
      <div className="section-title">
        <h3>服务端候选</h3>
        <span>{actionableCandidates.length} 项可执行</span>
      </div>
      <div className="prompt-candidate-list">
        {actionableCandidates.length === 0 ? (
          <div className="empty-row">等待服务端开放可执行候选</div>
        ) : (
          actionableCandidates.map((candidate) => (
            <article key={candidate.action}>
              <header>
                <strong>{candidate.label}</strong>
                <span>{candidate.enabled ? "可执行" : "禁用"}</span>
              </header>
              <ChoicePreview title="来源" choices={candidate.sources} cardNamesByNo={cardNamesByNo} />
              <ChoicePreview title="目标" choices={candidate.targets} cardNamesByNo={cardNamesByNo} />
              <ChoicePreview title="目的地" choices={candidate.destinations} cardNamesByNo={cardNamesByNo} />
              <ChoicePreview title="模式" choices={candidate.modes} cardNamesByNo={cardNamesByNo} />
              <ChoicePreview title="费用" choices={candidate.optionalCosts} cardNamesByNo={cardNamesByNo} />
            </article>
          ))
        )}
      </div>
    </section>
  );
}

function ChoiceChipRow({
  title,
  testIdPrefix,
  choices,
  cardNamesByNo,
  onPick
}: {
  title: string;
  testIdPrefix: string;
  choices?: ActionPromptChoiceDto[];
  cardNamesByNo: Record<string, string>;
  onPick: (choice: ActionPromptChoiceDto) => void;
}) {
  if (!choices?.length) {
    return null;
  }

  return (
    <div className="choice-chip-row" data-testid={`${testIdPrefix}-row`}>
      <span>{title}</span>
      <div>
        {choices.map((choice) => (
          <button
            data-testid={`${testIdPrefix}-${cssSafeId(choice.id)}`}
            key={choice.id}
            onClick={() => onPick(choice)}
            title={promptReasonLabel(choice.reason)}
            type="button"
          >
            {choiceDisplayLabel(choice, cardNamesByNo)}
          </button>
        ))}
      </div>
    </div>
  );
}

function ChoicePreview({
  title,
  choices,
  cardNamesByNo
}: {
  title: string;
  choices?: ActionPromptChoiceDto[];
  cardNamesByNo: Record<string, string>;
}) {
  if (!choices?.length) {
    return null;
  }

  return (
    <div className="choice-preview">
      <span>{title}</span>
      <p>{choices.slice(0, 6).map((choice) => choiceDisplayLabel(choice, cardNamesByNo)).join("，")}</p>
    </div>
  );
}

function choiceDisplayLabel(choice: ActionPromptChoiceDto, cardNamesByNo: Record<string, string> = {}) {
  if (!choice.label || choice.label === choice.id) {
    return optionalCostLabel(choice.id);
  }
  return labelWithCardName(choice.label, cardNamesByNo);
}

function optionalCostLabel(cost: string) {
  return (
    {
      ECHO: "回响",
      STANDBY_REVEAL_0: "待命揭示",
      ROAM: "游走",
      "SPEND_POWER:1": "支付 1 战力符能",
      "SPEND_MANA:1": "支付 1 法力",
      "SPEND_MANA:2": "支付 2 法力",
      "SPEND_MANA:3": "支付 3 法力",
      "SPEND_MANA:4": "支付 4 法力",
      "SPEND_EXPERIENCE:1": "支付 1 经验",
      "SPEND_EXPERIENCE:2": "支付 2 经验",
      "SPEND_EXPERIENCE:3": "支付 3 经验",
      STANDBY_A: "支付 1 法力布置待命",
      STANDBY_FREE: "免费布置待命",
      STANDBY_TEEMO_MANA: "提莫布置待命",
      COMBAT_ASSIGNMENT: "战斗分配",
      ASSEMBLE_RED: "红色装配"
    }[cost] ?? cost
  );
}

function SelectionModePanel({
  selectionIntent,
  onSelectionIntent
}: {
  selectionIntent: SelectionIntent;
  onSelectionIntent: (intent: SelectionIntent) => void;
}) {
  return (
    <section className="selection-mode-panel" data-testid="selection-mode-panel">
      <div className="section-title">
        <h3>桌面点击模式</h3>
        <span>{selectionIntentHint(selectionIntent)}</span>
      </div>
      <div className="selection-mode-grid">
        {selectionIntentOptions.map((option) => (
          <button
            className={selectionIntent === option.id ? "selected" : ""}
            data-testid={`selection-mode-${option.id}`}
            key={option.id}
            onClick={() => onSelectionIntent(option.id)}
            title={option.hint}
            type="button"
          >
            {option.label}
          </button>
        ))}
      </div>
    </section>
  );
}

function IntentSummaryPanel({
  prompt,
  selectionIntent,
  playDraft,
  moveDraft,
  assembleDraft,
  battleDraft,
  legendDraft,
  activateDraft
}: {
  prompt?: ActionPromptDto;
  selectionIntent: SelectionIntent;
  playDraft: PlayCardDraft;
  moveDraft: MoveUnitDraft;
  assembleDraft: AssembleDraft;
  battleDraft: BattleDraft;
  legendDraft: LegendDraft;
  activateDraft: ActivateDraft;
}) {
  const summaryRows = [
    {
      label: "打出卡牌",
      value: summarizeCommand(buildPlayCardCommand(playDraft))
    },
    {
      label: "移动单位",
      value: summarizeCommand(buildMoveUnitCommand(moveDraft))
    },
    {
      label: "装配装备",
      value: summarizeCommand(buildAssembleCommand(assembleDraft))
    },
    {
      label: "声明战斗",
      value: summarizeCommand(buildDeclareBattleCommand(battleDraft))
    },
    {
      label: "激活能力",
      value: summarizeCommand(buildActivateAbilityCommand(activateDraft))
    },
    {
      label: "传奇行动",
      value: summarizeCommand(buildLegendActCommand(legendDraft))
    }
  ];

  return (
    <section className="intent-summary-panel" data-testid="intent-summary">
      <div className="section-title">
        <h3>待提交操作</h3>
        <span>{prompt?.promptId ? `提示 ${prompt.promptId}` : selectionIntentLabel(selectionIntent)}</span>
      </div>
      <div className="intent-summary-grid">
        <div>
          <span>提示版本</span>
          <strong>{prompt?.snapshotTick ?? "-"}</strong>
        </div>
        <div>
          <span>当前模式</span>
          <strong>{selectionIntentLabel(selectionIntent)}</strong>
        </div>
      </div>
      <div className="intent-summary-list">
        {summaryRows.map((row) => (
          <div key={row.label}>
            <span>{row.label}</span>
            <strong>{row.value}</strong>
          </div>
        ))}
      </div>
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
        <span>{prompt?.reason ?? "等待服务端提示"}</span>
      </div>
      <div className="response-grid">
        <div>
          <span>时点</span>
          <strong>{timingLabel(timingState)}</strong>
        </div>
        <div>
          <span>优先权</span>
          <strong>{priorityPlayerId}</strong>
        </div>
        <div>
          <span>焦点</span>
          <strong>{focusPlayerId}</strong>
        </div>
        <div>
          <span>结算栈</span>
          <strong>{stackLabels.length}</strong>
        </div>
      </div>
      <div className="stack-tags">
        {stackLabels.length === 0 ? <span>结算栈为空</span> : stackLabels.map((label) => <span key={label}>{label}</span>)}
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
      <section className="event-log-panel" data-testid="event-log" aria-live="polite">
        <div className="section-title">
          <h2>事件日志</h2>
          <span>{timeline.length} 条事件</span>
        </div>
        <ol className="event-list">
          {timeline.length === 0 ? (
            <li className="empty-row">等待服务器事件</li>
          ) : (
            timeline.slice(0, 32).map((event) => (
              <li key={event.key}>
                <span className="event-kind">{eventKindLabel(event.kind)}</span>
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
          <span>{roomStatusLabel(roomStatus)}</span>
        </div>
        <div className="report-summary">
          <div>
            <span>房间</span>
            <strong>{roomId || "-"}</strong>
          </div>
          <div>
            <span>回合</span>
            <strong>{snapshot ? `#${snapshot.turnNumber}` : "-"}</strong>
          </div>
          <div>
            <span>行动方</span>
            <strong>{snapshot?.activePlayerId ?? "-"}</strong>
          </div>
          <div>
            <span>胜者</span>
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
          <span>本地只读边界</span>
        </div>
        <div className="replay-boundary-summary" data-testid="replay-boundary-summary">
          <div>
            <span>本地事件</span>
            <strong>{timeline.length}</strong>
          </div>
          <div>
            <span>状态版本</span>
            <strong>{snapshot?.tick ?? "-"}</strong>
          </div>
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
  onSelect,
  compact = false,
  onOpenFull
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
  compact?: boolean;
  onOpenFull?: () => void;
}) {
  const filtered = useMemo(() => filterCatalog(specs, query, filter), [specs, query, filter]);
  const selected = filtered.find((spec) => spec.cardNo === selectedCardNo) ?? filtered[0] ?? specs[0];
  const visible = compact ? filtered.slice(0, 6) : filtered;
  const implementedCount = summary.implemented ?? 0;
  const manualDeferredCount = summary["manual-rule-required"] ?? 0;
  const blockedCount = summary.unimplemented ?? 0;
  const allSpecsConformancePass = specs.length > 0
    && implementedCount === specs.length
    && manualDeferredCount === 0
    && blockedCount === 0;

  return (
    <section className={compact ? "catalog-panel compact" : "catalog-panel"} data-testid="card-catalog">
      <div className="section-title">
        <div>
          <p className="eyebrow">卡牌图鉴</p>
          <h2>{compact ? "卡牌与规则" : "图鉴与卡牌详情"}</h2>
        </div>
        <div className="section-actions">
          <span>{catalogLoadStatusLabel(status)}</span>
          {onOpenFull ? (
            <button className="secondary-toggle" data-testid="open-catalog-modal" onClick={onOpenFull} type="button">
              完整图鉴
            </button>
          ) : null}
        </div>
      </div>

      <div className="catalog-toolbar">
        <label>
          搜索卡牌
          <input
            data-testid="catalog-search"
            value={query}
            onChange={(event) => onQuery(event.target.value)}
            placeholder="输入卡号、名称或规则文本"
            spellCheck={false}
          />
        </label>
        <label>
          状态
          <select data-testid="catalog-filter" value={filter} onChange={(event) => onFilter(event.target.value as CatalogFilter)}>
            <option value="conformance-pass">可玩</option>
            <option value="manual-deferred">人工边界</option>
            <option value="blocked">已阻止</option>
            <option value="all">全部状态</option>
          </select>
        </label>
        <div className="catalog-counts" data-testid="catalog-counts">
          <span>已通过 {implementedCount}/{specs.length}</span>
          <span>人工边界 {manualDeferredCount}</span>
          <span>阻止 {blockedCount}</span>
          <span>当前 {filtered.length}</span>
        </div>
      </div>

      <div
        className={allSpecsConformancePass ? "catalog-boundary pass" : "catalog-boundary"}
        data-testid="catalog-playable-boundary"
      >
        {allSpecsConformancePass
          ? `P7.9 全量可玩状态：${implementedCount}/${specs.length} 已通过一致性，0 个人工边界，0 个阻止项。`
          : `图鉴审计状态：${implementedCount}/${specs.length} 已通过一致性，${manualDeferredCount} 个人工边界，${blockedCount} 个阻止项。`}
      </div>

      <div className="catalog-layout">
        <div className="catalog-list" data-testid="catalog-results">
          <div className="catalog-list-summary">
            <strong>{filtered.length}</strong>
            <span>{compact ? "显示前几项" : `显示 ${visible.length} 项`}</span>
          </div>
          {visible.length === 0 ? (
            <div className="empty-row">没有符合条件的卡牌</div>
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

        <CardDetail spec={selected} compact={compact} />
      </div>
    </section>
  );
}

function CardDetail({ spec, compact = false }: { spec?: BehaviorSpecDto; compact?: boolean }) {
  if (!spec) {
    return (
      <section className="card-detail" data-testid="card-detail">
        <div className="empty-row">请选择一张卡牌</div>
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
          P6 人工边界：这张卡在后端规则域实现前不会开放为可玩按钮。
        </div>
      ) : null}

      <dl className="detail-grid">
        <div>
          <dt>功能状态</dt>
          <dd>{statusLabel(spec.status)}</dd>
        </div>
        <div>
          <dt>操作入口</dt>
          <dd>{catalogOperationSurface(spec)}</dd>
        </div>
        <div>
          <dt>实现卡牌</dt>
          <dd>{spec.implementedByCardNo ?? "-"}</dd>
        </div>
        <div>
          <dt>效果类型</dt>
          <dd>{spec.implementedEffectKind ? effectKindLabel(spec.implementedEffectKind) : "-"}</dd>
        </div>
        <div>
          <dt>模板</dt>
          <dd>{spec.templateIds?.length ? spec.templateIds.map(effectKindLabel).join("、") : "-"}</dd>
        </div>
      </dl>

      <section className="card-text-block">
        <h3>官方规则文本</h3>
        <p>{spec.officialText || "暂无官方规则文本。"}</p>
      </section>
      {compact ? null : (
        <section className="card-text-block">
          <h3>行为说明</h3>
          <p>{behaviorReasonLabel(spec)}</p>
        </section>
      )}

      {compact ? null : (
        <div className="spec-pill-grid">
          <SpecPills title="关键词" values={(spec.keywords ?? []).map((item) => item.value ? `${item.keyword} ${item.value}` : item.keyword)} />
          <SpecPills title="目标" values={(spec.targets ?? []).map((item) => `${targetScopeLabel(item.scope)} ${item.minCount}-${item.maxCount ?? item.minCount}`)} />
          <SpecPills title="触发" values={(spec.triggers ?? []).map((item) => `${triggerKindLabel(item.kind)}：${triggerTimingLabel(item.timing)}`)} />
          <SpecPills title="效果" values={(spec.effects ?? []).map((item) => `${effectKindLabel(item.templateId)}：${statusLabel(item.status)}`)} />
        </div>
      )}
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
        {values.length === 0 ? <span>无</span> : values.slice(0, 12).map((value) => <span key={value}>{value}</span>)}
      </div>
    </section>
  );
}

function PlayerDock({
  players,
  devToolsOpen,
  onPatch,
  onJoin,
  onReconnect,
  onDisconnect,
  onReady,
  onSnapshot,
  onPromptAction,
  onSubmitJson
}: {
  players: Record<PlayerKey, PlayerState>;
  devToolsOpen: boolean;
  onPatch: (key: PlayerKey, patch: Partial<PlayerState>) => void;
  onJoin: (key: PlayerKey) => void;
  onReconnect: (key: PlayerKey) => void;
  onDisconnect: (key: PlayerKey) => void;
  onReady: (key: PlayerKey) => void;
  onSnapshot: (key: PlayerKey) => void;
  onPromptAction: (key: PlayerKey, action: string) => void;
  onSubmitJson: (key: PlayerKey) => void;
}) {
  return (
    <section className="seat-dock" data-testid="seat-dock" aria-label="玩家席位">
      <div className="section-title">
        <h2>玩家席位</h2>
        <span>入座、准备、重连</span>
      </div>
      <div className="seat-grid">
        {playerKeys.map((key) => {
          const state = players[key];
          const promptCandidates = promptCandidatesFor(state.prompt);
          return (
            <article className="seat-card" data-testid={`${key}-panel`} key={key}>
              <header>
                <div>
                  <p className="eyebrow">{state.label} 客户端</p>
                  <h3>{state.playerId || state.label}</h3>
                </div>
                <StatusPill status={state.status} />
              </header>
              <label>
                玩家编号
                <input
                  data-testid={`${key}-player-id`}
                  value={state.playerId}
                  onChange={(event) => onPatch(key, { playerId: event.target.value })}
                  spellCheck={false}
                />
              </label>
              <div className="button-row compact-actions">
                <button data-testid={`${key}-join`} onClick={() => onJoin(key)} type="button">
                  入座
                </button>
                <button data-testid={`${key}-ready`} onClick={() => onReady(key)} type="button">
                  准备
                </button>
                <button data-testid={`${key}-reconnect`} onClick={() => onReconnect(key)} type="button">
                  重连
                </button>
                <button data-testid={`${key}-disconnect`} onClick={() => onDisconnect(key)} type="button">
                  断开
                </button>
                <button data-testid={`${key}-snapshot`} onClick={() => onSnapshot(key)} type="button">
                  同步
                </button>
              </div>
              <dl className="seat-stats">
                <div>
                  <dt>座位</dt>
                  <dd>{state.session?.seat ?? "-"}</dd>
                </div>
                <div>
                  <dt>提示</dt>
                  <dd>{state.prompt?.actionable ? "可行动" : "等待"} / {state.prompt?.snapshotTick ?? "-"}</dd>
                </div>
                <div>
                  <dt>重连</dt>
                  <dd>{connectionStatusLabel(state.reconnectStatus)}</dd>
                </div>
              </dl>
              <div className="button-row prompt-actions">
                {promptCandidates.length === 0 ? (
                  <button disabled type="button">等待服务端提示</button>
                ) : (
                  promptCandidates.map((candidate) => (
                    <button
                      key={candidate.action}
                      data-testid={`${key}-${candidate.action.toLowerCase().replaceAll("_", "-")}`}
                      disabled={!candidate.enabled || !directPromptActions.has(candidate.action)}
                      onClick={() => onPromptAction(key, candidate.action)}
                      title={promptReasonLabel(candidate.reason)}
                      type="button"
                    >
                      {candidate.label}
                    </button>
                  ))
                )}
              </div>
              {devToolsOpen ? (
                <section className="intent-panel compact-json" aria-label={`${state.label} 原始命令`}>
                  <div className="section-title">
                    <h3>原始命令</h3>
                    <span>本地调试</span>
                  </div>
                  <input
                    data-testid={`${key}-intent-id`}
                    className="intent-id"
                    value={state.clientIntentId}
                    placeholder="可选 intentId"
                    onChange={(event) => onPatch(key, { clientIntentId: event.target.value })}
                    spellCheck={false}
                  />
                  <textarea
                    data-testid={`${key}-json-intent`}
                    value={state.jsonIntent}
                    onChange={(event) => onPatch(key, { jsonIntent: event.target.value })}
                    spellCheck={false}
                  />
                  <button data-testid={`${key}-submit-json`} onClick={() => onSubmitJson(key)} type="button">
                    提交原始命令
                  </button>
                </section>
              ) : null}
            </article>
          );
        })}
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
  onSubmitJson,
  devToolsOpen
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
  devToolsOpen: boolean;
}) {
  const promptCandidates = promptCandidatesFor(state.prompt);

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
          <dd>{state.prompt?.actionable ? "actionable" : "waiting"} / {state.prompt?.snapshotTick ?? "-"}</dd>
        </div>
      </dl>

      <section className="prompt-panel" aria-label={`${state.label} action prompt`}>
        <div className="section-title">
          <h3>Action Prompt</h3>
          <span>{state.prompt?.reason ?? "No prompt yet"}</span>
        </div>
        <div className="button-row">
          {promptCandidates.length === 0 ? (
            <button disabled>No prompt action</button>
          ) : (
            promptCandidates.map((candidate) => (
              <button
                key={candidate.action}
                data-testid={`${playerKey}-${candidate.action.toLowerCase().replaceAll("_", "-")}`}
                disabled={!candidate.enabled || !directPromptActions.has(candidate.action)}
                onClick={() => onPromptAction(candidate.action)}
                title={candidate.reason}
              >
                {candidate.label}
              </button>
            ))
          )}
        </div>
      </section>

      {devToolsOpen ? (
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
      ) : null}

      {devToolsOpen ? <DebugGrid state={state} /> : null}
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
  return <span className={`status-pill ${status}`}>{connectionStateLabel(status)}</span>;
}

function connectionStateLabel(status: ConnectionStatus) {
  return (
    {
      disconnected: "未连接",
      connecting: "连接中",
      connected: "已连接",
      reconnecting: "重连中",
      reconnected: "已重连",
      closed: "已关闭",
      error: "错误"
    }[status] ?? status
  );
}

function EmptyDesk() {
  return (
    <div className="empty-desk">
      <strong>尚无玩家状态</strong>
      <span>请先让两名玩家入座并准备。</span>
    </div>
  );
}

function promptCandidatesFor(prompt?: ActionPromptDto): ActionPromptCandidateDto[] {
  if (prompt?.candidates?.length) {
    return prompt.candidates.map((candidate) => ({
      ...candidate,
      label: promptActionLabel(candidate.action),
      reason: promptReasonLabel(candidate.reason)
    }));
  }

  return (prompt?.actions ?? []).map((action) => ({
    action,
    label: promptActionLabel(action),
    enabled: Boolean(prompt?.actionable) && action !== "WAIT",
    reason: promptReasonLabel(prompt?.reason)
  }));
}

function cardContextActionsForObject(objectId: string, prompt?: ActionPromptDto): CardContextAction[] {
  const actions: CardContextAction[] = [];
  for (const candidate of promptCandidatesFor(prompt)) {
    if (!candidate.enabled || candidate.action === "WAIT") {
      continue;
    }

    if (choiceListContainsObject(candidate.sources, objectId)) {
      actions.push({
        id: `${candidate.action}-source-${objectId}`,
        action: candidate.action,
        role: "source",
        label: sourceContextActionLabel(candidate.action),
        reason: promptReasonLabel(candidate.reason)
      });
    }

    if (choiceListContainsObject(candidate.targets, objectId)) {
      actions.push({
        id: `${candidate.action}-target-${objectId}`,
        action: candidate.action,
        role: "target",
        label: targetContextActionLabel(candidate.action),
        reason: promptReasonLabel(candidate.reason)
      });
    }
  }

  return dedupeCardContextActions(actions);
}

function choiceListContainsObject(choices: ActionPromptChoiceDto[] | undefined, objectId: string) {
  return choices?.some((choice) => choice.id === objectId || choice.id === `BATTLEFIELD:${objectId}`) ?? false;
}

function dedupeCardContextActions(actions: CardContextAction[]) {
  const seen = new Set<string>();
  return actions.filter((action) => {
    const key = `${action.action}:${action.role}`;
    if (seen.has(key)) {
      return false;
    }

    seen.add(key);
    return true;
  });
}

function sourceContextActionLabel(action: string) {
  return (
    {
      PLAY_CARD: "打出这张牌",
      ACTIVATE_ABILITY: "激活这个能力",
      MOVE_UNIT: "移动这个单位",
      ASSEMBLE_EQUIPMENT: "装配这件装备",
      DECLARE_BATTLE: "用它进攻",
      LEGEND_ACT: "发动传奇行动"
    }[action] ?? promptActionLabel(action)
  );
}

function targetContextActionLabel(action: string) {
  return (
    {
      PLAY_CARD: "选为出牌目标",
      ACTIVATE_ABILITY: "选为能力目标",
      ASSEMBLE_EQUIPMENT: "装备到这张牌",
      DECLARE_BATTLE: "指定为防守方",
      LEGEND_ACT: "选为传奇目标"
    }[action] ?? promptActionLabel(action)
  );
}

function coachHeadline(
  snapshot: SnapshotDto | undefined,
  prompt: ActionPromptDto | undefined,
  enabledCandidates: ActionPromptCandidateDto[]
) {
  if (!snapshot) {
    return "先创建房间、双人入座并准备";
  }
  if (!prompt) {
    return "已进入桌面，等待服务端下发可操作提示";
  }
  if (!prompt.actionable) {
    return "当前视角没有操作权";
  }
  if (enabledCandidates.length === 0) {
    return "当前没有服务端允许的复杂操作";
  }
  if (enabledCandidates.length === 1) {
    return `现在可以执行：${enabledCandidates[0].label}`;
  }
  return `现在可以执行：${enabledCandidates.map((candidate) => candidate.label).slice(0, 3).join("、")}`;
}

function coachSteps(
  snapshot: SnapshotDto | undefined,
  prompt: ActionPromptDto | undefined,
  enabledCandidates: ActionPromptCandidateDto[]
) {
  if (!snapshot) {
    return ["点顶部“新房间”", "点“双人入座”", "点“双方准备”，进入对局后再看这里的下一步"];
  }
  if (!prompt) {
    return ["点顶部“同步”获取最新状态", "确认右上角视角是当前行动方", "需要测试具体能力时，可直接载入下方测试局面"];
  }
  if (!prompt.actionable) {
    return ["切换到当前行动方视角，或等待对手让过", "服务端未开放的按钮不会生效", "想单独测试某个能力，可载入下方对应局面"];
  }
  if (enabledCandidates.length === 0) {
    return ["当前局面没有可执行候选", "可以点“同步”确认状态，或载入下方测试局面", "不要手填灰色动作，服务端会拒绝非法操作"];
  }
  if (enabledCandidates.length === 1 && enabledCandidates[0].action === "END_TURN") {
    return ["当前只有“结束回合”合法", "点“结束回合”推进到下一名玩家", "要测出牌、移动、装备或战斗，可载入下方对应局面"];
  }
  return [
    "优先点上方亮起的服务端动作",
    "复杂动作先展开“目标选择与提交”，选择来源、目标和费用",
    "提交后看日志与桌面状态是否按服务端快照更新"
  ];
}

function guidedActionStatus(prompt: ActionPromptDto | undefined, candidate: ActionPromptCandidateDto | undefined) {
  if (candidate?.enabled) {
    if (directPromptActions.has(candidate.action)) {
      return { label: "当前可点", detail: promptReasonLabel(candidate.reason), tone: "ready" };
    }
    return { label: "可在下方提交", detail: "展开“目标选择与提交”，按服务端来源、目标和费用填写后提交。", tone: "ready" };
  }
  if (candidate) {
    return {
      label: "当前不可用",
      detail: promptReasonLabel(candidate.reason) || "服务端认为当前局面没有合法来源、目标或费用。",
      tone: "blocked"
    };
  }
  if (!prompt) {
    return { label: "待同步", detail: "服务端还没有下发这个动作的提示。", tone: "blocked" };
  }
  if (!prompt.actionable) {
    return { label: "等待权限", detail: "当前视角不是行动方，或还没轮到你响应。", tone: "blocked" };
  }
  return { label: "换局面测试", detail: "当前服务端提示没有包含这个动作，通常是时机、资源或对象不满足。", tone: "blocked" };
}

function firstScenarioPreset(ids: string[]) {
  return ids
    .map((id) => scenarioPresets.find((preset) => preset.id === id))
    .find((preset): preset is ScenarioPreset => Boolean(preset));
}

function promptActionLabel(action: string) {
  return (
    {
      READY: "准备",
      WAIT: "等待",
      PLAY_CARD: "打出卡牌",
      ACTIVATE_ABILITY: "激活能力",
      ASSEMBLE_EQUIPMENT: "装配装备",
      MOVE_UNIT: "移动单位",
      DECLARE_BATTLE: "声明战斗",
      HIDE_CARD: "隐藏卡牌",
      TAP_RUNE: "横置符文",
      LEGEND_ACT: "传奇行动",
      PASS: "让过",
      PASS_PRIORITY: "让过优先权",
      PASS_FOCUS: "让过焦点",
      END_TURN: "结束回合"
    }[action] ?? action
  );
}

function candidateFor(candidates: ActionPromptCandidateDto[], action: string) {
  return candidates.find((candidate) => candidate.action === action);
}

function operationModeForAction(action: string): OperationMode | undefined {
  return (
    {
      PLAY_CARD: "play",
      ACTIVATE_ABILITY: "ability",
      MOVE_UNIT: "move",
      ASSEMBLE_EQUIPMENT: "assemble",
      DECLARE_BATTLE: "battle",
      LEGEND_ACT: "legend"
    } as Partial<Record<string, OperationMode>>
  )[action];
}

function defaultSelectionIntentForOperation(mode: OperationMode): SelectionIntent {
  return (
    {
      play: "play-source",
      ability: "ability-source",
      move: "move-source",
      assemble: "assemble-source",
      battle: "battle-attacker",
      legend: "legend-source"
    } satisfies Record<OperationMode, SelectionIntent>
  )[mode];
}

function selectionIntentLabel(intent: SelectionIntent) {
  return selectionIntentOptions.find((option) => option.id === intent)?.label ?? intent;
}

function selectionIntentHint(intent: SelectionIntent) {
  return selectionIntentOptions.find((option) => option.id === intent)?.hint ?? "点击桌面对象填入当前操作";
}

function findObject(snapshot: SnapshotDto | undefined, objectId: string) {
  if (!snapshot) {
    return undefined;
  }

  for (const player of Object.values(snapshot.players)) {
    const candidate = player.objects?.[objectId];
    if (candidate) {
      return candidate;
    }
  }

  return undefined;
}

function objectCoarseZone(snapshot: SnapshotDto | undefined, objectId: string) {
  if (!snapshot) {
    return undefined;
  }

  for (const player of Object.values(snapshot.players)) {
    const zones = player.zones;
    if (!zones) {
      continue;
    }

    if (zones.base?.includes(objectId)) {
      return "BASE";
    }
    if (zones.battlefields?.includes(objectId)) {
      return "BATTLEFIELD";
    }
  }

  return undefined;
}

function toggleListValue(value: string, nextValue: string) {
  const values = parseList(value);
  return values.includes(nextValue)
    ? values.filter((valueItem) => valueItem !== nextValue)
    : [...values, nextValue];
}

function collectDraftObjectSelections(
  playDraft: PlayCardDraft,
  moveDraft: MoveUnitDraft,
  assembleDraft: AssembleDraft,
  battleDraft: BattleDraft,
  legendDraft: LegendDraft,
  activateDraft: ActivateDraft
) {
  return new Set(
    [
      playDraft.sourceObjectId,
      ...parseList(playDraft.targetObjectIds),
      moveDraft.sourceObjectId,
      assembleDraft.sourceObjectId,
      assembleDraft.targetObjectId,
      ...parseList(battleDraft.attackerObjectIds),
      ...parseList(battleDraft.defenderObjectIds),
      ...parseList(battleDraft.battlefieldTargetObjectIds),
      legendDraft.sourceObjectId,
      ...parseList(legendDraft.targetObjectIds),
      activateDraft.sourceObjectId,
      ...parseList(activateDraft.targetObjectIds)
    ].filter(Boolean)
  );
}

function summarizeCommand(command: Record<string, unknown>) {
  const parts = Object.entries(command)
    .filter(([, value]) => {
      if (Array.isArray(value)) {
        return value.length > 0;
      }
      return value !== undefined && value !== null && value !== "";
    })
    .map(([key, value]) => `${commandFieldLabel(key)}=${formatPayloadValue(value)}`);
  return parts.length ? parts.join(" / ") : promptActionLabel(String(command.cmdType ?? "-"));
}

function commandFieldLabel(key: string) {
  return (
    {
      cmdType: "动作",
      sourceObjectId: "来源",
      cardNo: "卡号",
      targetObjectIds: "目标",
      mode: "模式",
      optionalCosts: "费用",
      destination: "目的地",
      origin: "来源区域",
      targetObjectId: "宿主",
      battlefieldId: "战场",
      attackerObjectIds: "攻击方",
      defenderObjectIds: "防守方",
      battlefieldTargetObjectIds: "战场目标",
      abilityId: "能力"
    }[key] ?? key
  );
}

function summarizeRoom(snapshot?: SnapshotDto) {
  if (!snapshot) {
    return [
      { label: "房间", value: "未同步" },
      { label: "回合", value: "-" },
      { label: "行动方", value: "-" },
      { label: "时点", value: "-" },
      { label: "结算栈", value: "-" },
      { label: "胜者", value: "-" }
    ];
  }

  return [
    { label: "房间", value: roomStatusLabel(String(snapshot.timing?.roomStatus ?? snapshot.turnState ?? "-")) },
    { label: "回合", value: `#${snapshot.turnNumber}` },
    { label: "行动方", value: snapshot.activePlayerId || "-" },
    { label: "时点", value: timingLabel(String(snapshot.timing?.timingState ?? snapshot.turnState ?? "-")) },
    { label: "结算栈", value: String(snapshot.stack?.length ?? 0) },
    { label: "胜者", value: String(snapshot.timing?.winnerPlayerId ?? "-") }
  ];
}

function roomStatusLabel(status: string) {
  return (
    {
      EMPTY: "空房间",
      SEATING: "入座中",
      IN_PROGRESS: "对局中",
      FINISHED: "已结束",
      READY: "已准备"
    }[status] ?? status
  );
}

function timingLabel(timing: string) {
  return (
    {
      ACTION: "行动窗口",
      PRIORITY: "优先权",
      FOCUS: "焦点",
      COMBAT: "战斗",
      CLEANUP: "清理",
      START: "开始",
      IN_PROGRESS: "进行中",
      NEUTRAL_OPEN: "普通行动窗口",
      SPELL_DUEL_OPEN: "法术对决窗口"
    }[timing] ?? timing
  );
}

function buildSystemNotices(players: Record<PlayerKey, PlayerState>, catalogStatus: string) {
  const notices: string[] = [];
  const statuses = playerKeys.map((key) => players[key].status);
  if (statuses.every((status) => status === "disconnected")) {
    notices.push("等待双人入座");
  }
  if (statuses.some((status) => status === "reconnecting")) {
    notices.push("连接恢复中");
  }
  if (statuses.some((status) => status === "error" || status === "closed")) {
    notices.push("存在断线或错误状态");
  }
  if (catalogStatus === "loading") {
    notices.push("图鉴加载中");
  } else if (!catalogStatus.startsWith("loaded")) {
    notices.push(`图鉴状态: ${catalogStatus}`);
  }

  return notices;
}

function summarizeCatalog(specs: BehaviorSpecDto[]) {
  return specs.reduce<Record<string, number>>((counts, spec) => {
    counts[spec.status] = (counts[spec.status] ?? 0) + 1;
    return counts;
  }, {});
}

function buildCardNameMap(specs: BehaviorSpecDto[]) {
  return specs.reduce<Record<string, string>>((names, spec) => {
    if (spec.cardNo && spec.cardName) {
      names[spec.cardNo] = spec.cardName;
      names[normalizeCardNo(spec.cardNo)] = spec.cardName;
    }
    return names;
  }, {});
}

function buildCardSpecMap(specs: BehaviorSpecDto[]) {
  return specs.reduce<Record<string, BehaviorSpecDto>>((cardSpecs, spec) => {
    if (spec.cardNo) {
      cardSpecs[spec.cardNo] = spec;
      cardSpecs[normalizeCardNo(spec.cardNo)] = spec;
    }
    return cardSpecs;
  }, {});
}

function normalizeCardNo(cardNo: string) {
  return cardNo.trim().replace(/[·・]/g, ".").toLocaleUpperCase();
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
    return "已通过一致性";
  }
  if (status === "manual-rule-required") {
    return "人工边界";
  }
  if (status === "unimplemented") {
    return "已阻止";
  }
  return status;
}

function catalogLoadStatusLabel(status: string) {
  if (status === "loading") {
    return "加载中";
  }
  if (status.startsWith("loaded ")) {
    return `已载入 ${status.replace("loaded ", "")} 张`;
  }
  return status;
}

function connectionStatusLabel(status: string) {
  return (
    {
      idle: "待机",
      retrying: "重试中",
      "transport ready": "连接就绪",
      joined: "已入座",
      reconnected: "已重连",
      closed: "已关闭",
      starting: "连接中",
      "manual stop": "手动断开",
      "reconnecting with token": "凭令牌重连"
    }[status] ?? status
  );
}

function promptReasonLabel(reason?: string) {
  if (!reason) {
    return "";
  }

  const disabledPrompt = reason.match(/^当前 prompt 不允许执行 (.+)$/);
  if (disabledPrompt) {
    return `当前提示不允许执行${promptActionLabel(disabledPrompt[1])}`;
  }

  const missingCandidate = reason.match(/^(.+) 当前没有服务端可执行候选$/);
  if (missingCandidate) {
    return `${promptActionLabel(missingCandidate[1])} 当前没有服务端可执行候选`;
  }

  return (
    {
      "roomId and playerId are required": "需要房间编号和玩家编号",
      "automatic rejoin": "自动重新入座",
      "Invalid JSON": "原始命令不是有效 JSON"
    }[reason] ?? reasonCodeLabel(reason)
  );
}

function behaviorReasonLabel(spec: BehaviorSpecDto) {
  if (spec.status === "implemented") {
    return `这张卡已接入${catalogOperationSurface(spec)}，并通过后端一致性验证；页面只会按服务端提示开放可执行操作。`;
  }
  if (spec.status === "manual-rule-required") {
    return "这张卡仍属于人工规则边界，页面会明确标记并阻止它进入可玩按钮。";
  }
  return "这张卡当前被阻止进入可玩流程，需要后端规则和一致性证据补齐后再开放。";
}

function catalogOperationSurface(spec: BehaviorSpecDto) {
  const effectKind = spec.implementedEffectKind ?? "";
  if (effectKind === "RUNE_RESOURCE_DOMAIN" || spec.cardCategoryName === "符文") {
    return "符文资源";
  }
  if (effectKind === "TOKEN_FACTORY_DOMAIN" || spec.cardCategoryName.startsWith("指示物")) {
    return "指示物生成";
  }
  if (effectKind === "LEGEND_ACTION_DOMAIN" || spec.cardCategoryName === "传奇") {
    return "传奇行动";
  }
  if (effectKind === "BATTLEFIELD_RULE_DOMAIN" || spec.cardCategoryName === "战场") {
    return "战场规则";
  }
  if (spec.activatedAbilities?.length) {
    return "激活能力";
  }
  return "打出卡牌";
}

function targetScopeLabel(scope: string) {
  return (
    {
      any: "任意",
      friendly: "友方",
      enemy: "敌方",
      unit: "单位",
      battlefield: "战场",
      hero: "英雄",
      legend: "传奇"
    }[scope] ?? scope
  );
}

function triggerKindLabel(kind: string) {
  return (
    {
      automatic: "自动",
      activated: "激活",
      replacement: "替代",
      static: "静态"
    }[kind] ?? kind
  );
}

function triggerTimingLabel(timing: string) {
  return (
    {
      "turn-start": "回合开始",
      "turn-end": "回合结束",
      play: "打出时",
      battle: "战斗时",
      conquest: "征服时",
      hold: "据守时"
    }[timing] ?? timing
  );
}

function effectKindLabel(templateId: string) {
  const labels: Record<string, string> = {
    RUNE_RESOURCE_DOMAIN: "符文资源规则",
    TOKEN_FACTORY_DOMAIN: "指示物生成规则",
    LEGEND_ACTION_DOMAIN: "传奇行动规则",
    BATTLEFIELD_RULE_DOMAIN: "战场规则",
    DAMAGE: "造成伤害",
    DRAW: "抽牌",
    MOVE: "移动",
    BUFF: "强化",
    DESTROY: "摧毁",
    STUN: "眩晕",
    READY: "重置",
    RECALL: "召回",
    RECYCLE: "回收",
    CREATE: "生成",
    ATTACH: "贴附",
    EQUIPMENT: "装备",
    SPELLSHIELD: "法盾",
    BARRIER: "屏障",
    POWER: "战力",
    SCORE: "得分",
    EXPERIENCE: "经验",
    RUNE: "符文",
    UNIT: "单位",
    TARGET: "目标",
    HAND: "手牌",
    BASE: "基地",
    BATTLEFIELD: "战场",
    LEGEND: "传奇"
  };
  const exact = labels[templateId];
  if (exact) {
    return exact;
  }

  return templateId
    .split("_")
    .filter(Boolean)
    .map((part) => labels[part] ?? part.toLocaleLowerCase())
    .join(" / ");
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

function eventKindLabel(kind: string) {
  return (
    {
      CARD_PLAYED: "打出卡牌",
      PRIORITY_PASSED: "让过优先权",
      FOCUS_PASSED: "让过焦点",
      TURN_END_DECLARED: "宣告结束回合",
      TURN_PLAYER_ADVANCED: "回合推进",
      BATTLE_DECLARED: "声明战斗",
      DAMAGE_APPLIED: "造成伤害",
      LEGEND_ABILITY_ACTIVATED: "传奇行动",
      ABILITY_ACTIVATED: "激活能力",
      BATTLEFIELD_TRIGGER_RESOLVED: "战场触发",
      COST_PAID: "支付费用",
      RUNES_CALLED: "召唤符文",
      SCORE_CHANGED: "分数变化",
      MATCH_STARTED: "对局开始",
      PLAYER_READY: "玩家准备",
      PLAYER_JOINED: "玩家入座"
    }[kind] ?? kind
  );
}

function payloadKeyLabel(key: string) {
  return (
    {
      assignmentIndex: "分配序号",
      assignmentRole: "分配角色",
      sourceObjectId: "来源",
      targetObjectId: "目标",
      targetObjectIds: "目标",
      damage: "伤害",
      battlefieldId: "战场",
      playerId: "玩家",
      controllerId: "控制者",
      ownerId: "拥有者",
      trigger: "触发",
      cardNo: "卡号",
      stackItemId: "结算项",
      effectKind: "效果",
      score: "分数",
      amount: "数量",
      reason: "原因",
      preventedReason: "阻止原因"
    }[key] ?? key
  );
}

function eventPayloadSummary(payload: Record<string, unknown>) {
  const visibleEntries = Object.entries(payload)
    .filter(([, value]) => value !== undefined && value !== null && value !== "");
  const priorityKeys = [
    "assignmentIndex",
    "assignmentRole",
    "sourceObjectId",
    "targetObjectId",
    "damage",
    "battlefieldId",
    "playerId",
    "trigger"
  ];
  const orderedEntries = [
    ...priorityKeys.flatMap((priorityKey) => visibleEntries.filter(([key]) => key === priorityKey)),
    ...visibleEntries.filter(([key]) => !priorityKeys.includes(key))
  ];
  const parts = orderedEntries
    .slice(0, 6)
    .map(([key, value]) => `${payloadKeyLabel(key)}：${formatPayloadEntryValue(key, value)}`);
  return parts.length > 0 ? parts.join(" / ") : "无载荷";
}

function formatPayloadEntryValue(key: string, value: unknown): string {
  if (key === "assignmentRole") {
    return combatAssignmentRoleLabel(value);
  }
  if (key.toLocaleLowerCase().includes("reason") && typeof value === "string") {
    return reasonCodeLabel(value);
  }
  return formatPayloadValue(value);
}

function combatAssignmentRoleLabel(value: unknown): string {
  if (value === "BULWARK_FIRST") {
    return "壁垒优先";
  }
  if (value === "BACK_ROW_LAST") {
    return "后排最后";
  }
  return formatPayloadValue(value);
}

function reasonCodeLabel(value: string) {
  const labels: Record<string, string> = {
    OPTIONAL_COST: "可选费用",
    LETHAL_DAMAGE: "致命伤害",
    MIGHTY_FAERIE_MOVE_PAYMENT_PLAY_UNIT: "大力仙灵支付移动出牌",
    BATTLEFIELD_END_TURN_READY_RUNES: "回合结束重置符文",
    BATTLEFIELD_CONQUERED_POWERFUL_PAY_1_DRAW: "征服后支付 1 抽牌",
    BATTLEFIELD_HIGH_COST_SPELL_INSIGHT_RECYCLE: "高费法术触发洞察回收",
    BATTLEFIELD_UNIT_RETURNED_PAY_1_CALL_RUNE: "单位返回后支付 1 召唤符文",
    BATTLEFIELD_PLAY_UNIT_PAY_1_GRANT_BOON: "打出单位后支付 1 给予增益",
    BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE: "首个单位入场后移动其他单位回基地",
    BATTLEFIELD_FIRST_TURN_GAIN_SCORE: "首回合得分",
    BATTLEFIELD_TURN_START_DAMAGE_ALL_UNITS: "回合开始对所有单位造成伤害",
    BATTLEFIELD_TURN_START_DESTROY_UNIT_DRAW: "回合开始摧毁单位并抽牌",
    BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE: "据守后支付 4 符能得分",
    BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL: "战斗摧毁后支付 3 召回",
    BATTLEFIELD_HELD_SEVEN_UNITS_WIN: "据守七个单位获胜",
    BATTLEFIELD_CONQUERED_REVEAL_TOP_TWO_RECYCLE: "征服后揭示两张并回收",
    BATTLEFIELD_UNIT_MOVED_POWER_PLUS_1: "单位移动后战力 +1",
    UNIT_PLAYED_POWER_PLUS_1: "单位入场后战力 +1",
    UNIT_CONQUEST_READY_SELF_ONCE_PER_TURN: "单位征服后每回合重置自身一次"
  };

  if (labels[value]) {
    return labels[value];
  }

  return value
    .split("_")
    .filter(Boolean)
    .map((part) => reasonWordLabel(part))
    .join("");
}

function reasonWordLabel(part: string) {
  return (
    {
      BATTLEFIELD: "战场",
      END: "结束",
      TURN: "回合",
      READY: "重置",
      RUNES: "符文",
      RUNE: "符文",
      CONQUERED: "征服",
      CONQUEST: "征服",
      POWERFUL: "强力",
      MIGHTY: "大力",
      FAERIE: "仙灵",
      PAY: "支付",
      PAYMENT: "支付",
      DRAW: "抽牌",
      HIGH: "高",
      COST: "费",
      SPELL: "法术",
      INSIGHT: "洞察",
      RECYCLE: "回收",
      UNIT: "单位",
      RETURNED: "返回",
      PLAY: "打出",
      PLAYED: "入场",
      GRANT: "给予",
      BOON: "增益",
      FIRST: "首次",
      MOVE: "移动",
      MOVED: "移动",
      OTHER: "其他",
      BASE: "基地",
      GAIN: "获得",
      SCORE: "分数",
      DAMAGE: "伤害",
      ALL: "全部",
      UNITS: "单位",
      DESTROY: "摧毁",
      DESTROYED: "被摧毁",
      HELD: "据守",
      POWER: "符能",
      BATTLE: "战斗",
      RECALL: "召回",
      SEVEN: "七个",
      WIN: "获胜",
      REVEAL: "揭示",
      TOP: "牌堆顶",
      TWO: "两张",
      ONCE: "一次",
      PER: "每",
      SELF: "自身"
    }[part] ?? part.toLocaleLowerCase()
  );
}

function formatPayloadValue(value: unknown): string {
  if (Array.isArray(value)) {
    return `[${value.map((item) => formatPayloadValue(item)).join(", ")}]`;
  }
  if (typeof value === "object" && value !== null) {
    return JSON.stringify(value);
  }
  if (typeof value === "string") {
    return displayCodeLabel(value);
  }
  return String(value);
}

function displayCodeLabel(value: string) {
  const actionLabel = promptActionLabel(value);
  if (actionLabel !== value) {
    return actionLabel;
  }

  const costLabel = optionalCostLabel(value);
  if (costLabel !== value) {
    return costLabel;
  }

  if (/^[A-Z0-9_:]+$/.test(value) && value.includes("_")) {
    return reasonCodeLabel(value);
  }

  return value;
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
  const battlefieldTargetObjectIds = parseList(draft.battlefieldTargetObjectIds);
  if (battlefieldTargetObjectIds.length > 0) {
    command.battlefieldTargetObjectIds = battlefieldTargetObjectIds;
  }
  const optionalCosts = parseList(draft.optionalCosts);
  if (optionalCosts.length > 0) {
    command.optionalCosts = optionalCosts;
  }
  return command;
}

function buildLegendActCommand(draft: LegendDraft) {
  const command: Record<string, unknown> = {
    cmdType: "LEGEND_ACT",
    sourceObjectId: draft.sourceObjectId.trim(),
    abilityId: draft.abilityId.trim(),
    targetObjectIds: parseList(draft.targetObjectIds)
  };
  const optionalCosts = parseList(draft.optionalCosts);
  if (optionalCosts.length > 0) {
    command.optionalCosts = optionalCosts;
  }
  return command;
}

function buildActivateAbilityCommand(draft: ActivateDraft) {
  const command: Record<string, unknown> = {
    cmdType: "ACTIVATE_ABILITY",
    sourceObjectId: draft.sourceObjectId.trim(),
    abilityId: draft.abilityId.trim(),
    targetObjectIds: parseList(draft.targetObjectIds)
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

function activateDraftFromCommand(command: Record<string, unknown>): ActivateDraft {
  return {
    sourceObjectId: typeof command.sourceObjectId === "string" ? command.sourceObjectId : "",
    abilityId: typeof command.abilityId === "string" ? command.abilityId : initialActivateDraft.abilityId,
    targetObjectIds: Array.isArray(command.targetObjectIds) ? command.targetObjectIds.join(", ") : "",
    optionalCosts: Array.isArray(command.optionalCosts) ? command.optionalCosts.join(", ") : ""
  };
}

function battleDraftFromCommand(command: Record<string, unknown>): BattleDraft {
  return {
    battlefieldId: typeof command.battlefieldId === "string" ? command.battlefieldId : initialBattleDraft.battlefieldId,
    attackerObjectIds: Array.isArray(command.attackerObjectIds) ? command.attackerObjectIds.join(", ") : "",
    defenderObjectIds: Array.isArray(command.defenderObjectIds) ? command.defenderObjectIds.join(", ") : "",
    battlefieldTargetObjectIds: Array.isArray(command.battlefieldTargetObjectIds) ? command.battlefieldTargetObjectIds.join(", ") : "",
    optionalCosts: Array.isArray(command.optionalCosts) ? command.optionalCosts.join(", ") : initialBattleDraft.optionalCosts
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

function objectChoiceLabel(snapshot: SnapshotDto | undefined, objectId: string, cardNamesByNo: Record<string, string>) {
  return cardTitle(objectId, findObject(snapshot, objectId), cardNamesByNo);
}

function cardTitle(objectId: string, object: ObjectView | undefined, cardNamesByNo: Record<string, string>) {
  return object?.cardNo ? `${cardDisplayName(object.cardNo, objectId, cardNamesByNo)} / ${objectId}` : objectId;
}

function cardDisplayName(cardNo: string | null | undefined, fallback: string, cardNamesByNo: Record<string, string>) {
  if (!cardNo) {
    return fallback;
  }

  const cardName = cardNamesByNo[cardNo];
  return cardName ? `${cardName}（${cardNo}）` : cardNo;
}

function labelWithCardName(label: string, cardNamesByNo: Record<string, string>) {
  const cardNo = extractCardNo(label);
  if (!cardNo || !cardNamesByNo[cardNo]) {
    return optionalCostLabel(label);
  }

  return label.replace(cardNo, `${cardNamesByNo[cardNo]}（${cardNo}）`);
}

function extractCardNo(value: string) {
  return value.match(/[A-Z]{2,4}[·-]\d{3}[a-z*]?\/\d{3}/i)?.[0];
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
  const message = error instanceof Error ? error.message : String(error);
  if (message.includes("Failed to fetch")) {
    return "无法连接服务器";
  }
  if (message.includes("not connected")) {
    return "尚未连接";
  }
  if (message.includes("no reconnect token")) {
    return "没有可用的重连令牌";
  }
  if (message.includes("Invalid JSON")) {
    return message.replace("Invalid JSON", "JSON 格式错误");
  }
  return message;
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

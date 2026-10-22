// Shapes mirror src/Riftbound.Contracts/Protocol.cs as serialized by the hub (camelCase).
// Only the fields the battle table reads are typed; everything else stays loose.

export type ServerMessage<T> = {
  type: string;
  roomId: string;
  playerId: string;
  serverTick: number;
  payload: T;
};

export type PlayerSession = { playerId: string; seat: string; reconnectToken: string };

export type Location = {
  playerId: string;
  zone: string;
  zoneKind: string;
  zoneLabel: string;
  battlefieldObjectId?: string | null;
};

export type GameObject = {
  objectId: string;
  cardNo?: string | null;
  damage: number;
  power: number;
  basePower: number;
  effectivePower: number;
  isExhausted: boolean;
  isFaceDown: boolean;
  isAttacking: boolean;
  isDefending: boolean;
  tags: string[];
  manaCost: number;
  attachedToObjectId?: string | null;
  ownerId: string;
  controllerId?: string | null;
  location: Location;
  untilEndOfTurnEffects?: unknown[];
};

export type Zones = {
  mainDeckCount: number;
  runeDeckCount: number;
  hand: string[];
  handHidden: number;
  base: string[];
  baseCards: string[];
  baseRunes: string[];
  battlefields: string[];
  graveyard: string[];
  banished: string[];
  legendZone: string[];
  championZone: string[];
};

export type RunePool = { mana: number; power: number; untypedPower: number; powerByTrait: Record<string, number> };

export type SnapshotPlayer = {
  id: string;
  name: string;
  seat: string;
  ready: boolean;
  handSize: number;
  score: number;
  experience: number;
  deckSubmitted: boolean;
  mulliganCompleted: boolean;
  runePool: RunePool;
  zones: Zones;
  objects: Record<string, GameObject>;
};

export type TableBattlefield = {
  index: number;
  battlefieldObjectId: string;
  zonePlayerId: string;
  cardNo?: string | null;
  controllerId?: string | null;
  status: string;
  contested: boolean;
  occupantObjectIds: string[];
  unitsBySide: Record<string, string[]>;
  standbyObjectIds: string[];
  faceDownStandbyCount: number;
  hiddenStandbyCount: number;
  scoredThisTurn: boolean;
  pendingTaskKinds: string[];
};

export type StackItem = Record<string, unknown> & {
  stackItemId?: string;
  sourceObjectId?: string;
  cardNo?: string;
  controllerId?: string;
};

export type Snapshot = {
  tick: number;
  turnNumber: number;
  activePlayerId: string;
  players: Record<string, SnapshotPlayer>;
  stack: StackItem[];
  timing: {
    phase: string;
    timingState: string;
    turnPlayerId: string;
    priorityPlayerId?: string | null;
    focusPlayerId?: string | null;
    winnerPlayerId?: string | null;
    winningScore: number;
    roomStatus: string;
    battle?: { isActive: boolean; battlefieldObjectId?: string | null; attackerObjectIds: string[]; defenderObjectIds: string[] };
    spellDuel?: { isActive: boolean; battlefieldObjectId?: string | null };
  };
  turnState: string;
  table?: {
    viewerPlayerId: string;
    players: { playerId: string; seat: string; perspective: string; isViewer: boolean }[];
    battlefields: TableBattlefield[];
  };
};

export type Choice = { id: string; label: string; reason?: string | null; objectIds?: string[] };

export type SelectionStep = { role: string; label: string; required: boolean; choices: Choice[] };

export type Binding = {
  field: string;
  source: string;
  required: boolean;
  asArray: boolean;
  omitEmpty: boolean;
  metadataKey?: string;
  metadataKeys?: string[];
};

export type Candidate = {
  action: string;
  label: string;
  enabled: boolean;
  reason: string;
  sources?: Choice[];
  targets?: Choice[];
  destinations?: Choice[];
  modes?: Choice[];
  optionalCosts?: Choice[];
  metadata?: Record<string, any>;
  selectionSteps?: SelectionStep[];
  commandTemplate?: { cmdType: string; bindings: Binding[] };
  presentation?: { category: string; intent: string; uiHint: string; priority: number };
};

export type PromptView = {
  type: string;
  title: string;
  message: string;
  relatedBattlefieldId?: string;
  relatedStackItemId?: string;
  relatedBattleId?: string;
  minSelection?: number;
  maxSelection?: number;
  metadata?: Record<string, any>;
};

export type Prompt = {
  playerId: string;
  actionable: boolean;
  reason: string;
  actions: string[];
  promptId?: string;
  snapshotTick?: number;
  candidates?: Candidate[];
  view?: PromptView;
};

export type GameEvent = {
  kind: string;
  description: string;
  payload: Record<string, unknown>;
  objectRefs?: { objectId: string; role: string; cardNo?: string | null }[] | null;
};

export type CommandReceipt = {
  accepted: boolean;
  state: string;
  message: string;
  errorCode?: string;
  serverTick: number;
};

export type PreconstructedDeck = {
  id: string;
  name: string;
  description: string;
  legendCardNo: string;
  championCardNo: string;
  mainDeck: string[];
  runeDeck: string[];
  battlefields: string[];
};

export type CardInfo = {
  name: string;
  sub?: string;
  category: string;
  colors: string[];
  cost?: number;
  power?: number;
  effect?: string;
  tag?: string;
  image?: string;
};

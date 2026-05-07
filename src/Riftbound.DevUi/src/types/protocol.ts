export type ConnectionStatus = "idle" | "connecting" | "connected" | "reconnecting" | "resyncing" | "disconnected" | "error";

export type WsServerMessage<T = unknown> = {
  type: string;
  roomId: string;
  playerId: string;
  serverTick: number;
  payload: T;
  protocolVersion: number;
  schemaVersion: number;
};

export type PlayerSessionDto = {
  playerId: string;
  seat: string;
  reconnectToken: string;
};

export type ErrorDto = {
  code: string;
  message: string;
};

export type GameEvent = {
  kind: string;
  description: string;
  payload: Record<string, unknown>;
};

export type ActionPromptChoiceDto = {
  id: string;
  label: string;
  reason?: string | null;
};

export type ActionPromptCandidateDto = {
  action: string;
  label: string;
  enabled: boolean;
  reason: string;
  sources?: ActionPromptChoiceDto[] | null;
  targets?: ActionPromptChoiceDto[] | null;
  destinations?: ActionPromptChoiceDto[] | null;
  modes?: ActionPromptChoiceDto[] | null;
  optionalCosts?: ActionPromptChoiceDto[] | null;
  metadata?: Record<string, unknown> | null;
};

export type ActionPromptDto = {
  playerId: string;
  actionable: boolean;
  reason: string;
  actions: string[];
  promptId?: string | null;
  snapshotTick?: number | null;
  candidates?: ActionPromptCandidateDto[] | null;
};

export type RunePoolView = {
  mana?: number;
  power?: number;
  totalPower?: number;
  untypedPower?: number;
  powerByTrait?: Record<string, number>;
};

export type ZoneView = {
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

export type CardObjectView = {
  objectId?: string;
  cardNo?: string | null;
  damage?: number;
  basePower?: number;
  effectivePower?: number;
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
  location?: Record<string, unknown> | null;
};

export type PlayerSnapshotView = {
  id?: string;
  name?: string;
  seat?: string;
  ready?: boolean;
  deckSubmitted?: boolean;
  mulliganCompleted?: boolean;
  handSize?: number;
  score?: number;
  experience?: number;
  runePool?: RunePoolView;
  zones?: ZoneView;
  objects?: Record<string, CardObjectView>;
};

export type SnapshotDto = {
  tick: number;
  turnNumber: number;
  activePlayerId: string;
  players: Record<string, PlayerSnapshotView>;
  lanes: Record<string, unknown>;
  stack: unknown[];
  timing: Record<string, unknown>;
  turnState: string;
};

export type SubmitDeckCommand = {
  cmdType: "SUBMIT_DECK";
  legendCardNo: string;
  championCardNo: string;
  mainDeck: string[];
  runeDeck: string[];
  battlefields: string[];
};

export type GameCommand =
  | SubmitDeckCommand
  | { cmdType: "MULLIGAN"; handObjectIds: string[] }
  | { cmdType: "PASS_PRIORITY" }
  | { cmdType: "PASS_FOCUS" }
  | { cmdType: "PASS" }
  | { cmdType: "END_TURN" }
  | { cmdType: "PLAY_CARD"; sourceObjectId: string; cardNo: string; targetObjectIds: string[]; mode?: string; optionalCosts?: string[]; destination?: string }
  | { cmdType: "HIDE_CARD"; sourceObjectId: string; cardNo: string; destination?: string; optionalCosts?: string[] }
  | { cmdType: "REVEAL_CARD"; sourceObjectId: string; cardNo: string; mode?: string; destination?: string; optionalCosts?: string[]; targetObjectIds?: string[] }
  | { cmdType: "TAP_RUNE"; sourceObjectId: string }
  | { cmdType: "RECYCLE_RUNE"; sourceObjectId: string }
  | { cmdType: "MOVE_UNIT"; sourceObjectId: string; origin?: string; destination?: string; optionalCosts?: string[] }
  | { cmdType: "ASSEMBLE_EQUIPMENT"; sourceObjectId: string; targetObjectId?: string; optionalCosts?: string[] }
  | { cmdType: "DECLARE_BATTLE"; battlefieldId?: string; attackerObjectIds?: string[]; defenderObjectIds?: string[]; battlefieldTargetObjectIds?: string[]; optionalCosts?: string[] }
  | { cmdType: "ACTIVATE_ABILITY"; sourceObjectId: string; abilityId: string; targetObjectIds: string[]; optionalCosts?: string[] }
  | { cmdType: "LEGEND_ACT"; sourceObjectId: string; abilityId: string; targetObjectIds: string[]; optionalCosts?: string[] };

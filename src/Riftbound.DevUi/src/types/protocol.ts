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

export type KnownPromptType =
  | "ROOM_SETUP"
  | "MULLIGAN"
  | "MAIN_ACTION"
  | "STACK_PRIORITY"
  | "SPELL_DUEL_FOCUS"
  | "SPELL_DUEL_ACTION"
  | "BATTLE_DECLARATION"
  | "HAND_CHOICE"
  | "ASSIGN_COMBAT_DAMAGE"
  | "PAY_COST"
  | "ORDER_TRIGGERS"
  | "TASK_QUEUE"
  | "WAIT"
  | "MATCH_RESULT";

export type PromptType = KnownPromptType | (string & {});

export type PromptViewDto = {
  type: PromptType;
  title: string;
  message: string;
  relatedBattlefieldId?: string | null;
  relatedStackItemId?: string | null;
  relatedBattleId?: string | null;
  relatedSpellDuelId?: string | null;
  minSelection?: number | null;
  maxSelection?: number | null;
  metadata?: Record<string, unknown> | null;
};

export type ActionPromptContractDto = {
  promptKind: string;
  candidateAction: string;
  requiredPayload: string[];
  legalChoices: string[];
  validationErrors: string[];
  visibleMetadata: string[];
  hiddenMetadata: string[];
};

export type ActionPromptContracts = Record<string, ActionPromptContractDto>;

export type ActionPromptDto = {
  playerId: string;
  actionable: boolean;
  reason: string;
  actions: string[];
  promptId?: string | null;
  snapshotTick?: number | null;
  candidates?: ActionPromptCandidateDto[] | null;
  view?: PromptViewDto | null;
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
  cardsPlayedThisTurn?: number;
  runePool?: RunePoolView;
  zones?: ZoneView;
  objects?: Record<string, CardObjectView>;
};

export type BattlefieldSnapshotView = {
  battlefieldObjectId?: string;
  zonePlayerId?: string;
  cardNo?: string | null;
  controllerId?: string | null;
  status?: string;
  contested?: boolean;
  occupantObjectIds?: string[];
  occupantControllerIds?: string[];
  standbyObjectIds?: string[];
  faceDownStandbyCount?: number;
  pendingTaskKinds?: string[];
  scoredThisTurn?: boolean;
  scoredPlayerId?: string | null;
  scoreStatus?: string | null;
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

type PromptStampedCommand = {
  promptId?: string | null;
  snapshotTick?: number | null;
};

export type CombatDamageAssignmentDto = {
  sourceObjectId: string;
  targetObjectId: string;
  damage: number;
};

export type GameCommand = PromptStampedCommand & (
  | SubmitDeckCommand
  | { cmdType: "MULLIGAN"; handObjectIds: string[] }
  | { cmdType: "PASS_PRIORITY" }
  | { cmdType: "PASS_FOCUS" }
  | { cmdType: "PASS" }
  | { cmdType: "END_TURN" }
  | { cmdType: "SURRENDER" }
  | { cmdType: "PLAY_CARD"; sourceObjectId: string; cardNo: string; targetObjectIds: string[]; mode?: string; optionalCosts?: string[]; destination?: string }
  | { cmdType: "HIDE_CARD"; sourceObjectId: string; cardNo: string; destination?: string; optionalCosts?: string[] }
  | { cmdType: "REVEAL_CARD"; sourceObjectId: string; cardNo: string; mode?: string; destination?: string; optionalCosts?: string[]; targetObjectIds?: string[] }
  | { cmdType: "TAP_RUNE"; sourceObjectId: string }
  | { cmdType: "RECYCLE_RUNE"; sourceObjectId: string }
  | { cmdType: "MOVE_UNIT"; sourceObjectId: string; origin?: string; destination?: string; optionalCosts?: string[] }
  | { cmdType: "ASSEMBLE_EQUIPMENT"; sourceObjectId: string; targetObjectId?: string; optionalCosts?: string[] }
  | { cmdType: "DECLARE_BATTLE"; battlefieldId?: string; attackerObjectIds?: string[]; defenderObjectIds?: string[]; battlefieldTargetObjectIds?: string[]; optionalCosts?: string[] }
  | { cmdType: "ACTIVATE_ABILITY"; sourceObjectId: string; abilityId: string; targetObjectIds: string[]; optionalCosts?: string[] }
  | { cmdType: "LEGEND_ACT"; sourceObjectId: string; abilityId: string; targetObjectIds: string[]; optionalCosts?: string[] }
  | { cmdType: "PAY_COST"; paymentId?: string; paymentWindow?: string; paymentChoiceIds?: string[] | null }
  | { cmdType: "ASSIGN_COMBAT_DAMAGE"; battleId?: string; battlefieldId?: string; assignments?: CombatDamageAssignmentDto[] | null }
  | { cmdType: "ORDER_TRIGGERS"; orderedTriggerIds?: string[] | null; triggerIds?: string[] | null }
  | { cmdType: "CHOOSE_HAND_CARDS"; choiceId: string; choiceWindow: string; chosenObjectIds: string[] }
);

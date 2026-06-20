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

export type CommandReceiptDto = {
  accepted: boolean;
  clientIntentId: string;
  cmdType: string;
  errorCode?: string | null;
  message: string;
  playerId: string;
  promptId?: string | null;
  roomId: string;
  serverTick: number;
  snapshotTick?: number | null;
  state: "ACCEPTED" | "FAILED" | "REJECTED" | (string & {});
};

export type GameEventObjectRef = {
  objectId: string;
  role: string;
  cardNo?: string | null;
  ownerId?: string | null;
  controllerId?: string | null;
  zone?: string | null;
  battlefieldObjectId?: string | null;
  isFaceDown?: boolean;
  isHidden?: boolean;
};

export type GameEvent = {
  kind: string;
  description: string;
  objectRefs?: GameEventObjectRef[] | null;
  payload: Record<string, unknown>;
};

export type BattlefieldResolutionView = {
  resolutionId?: string;
  tick?: number;
  kind?: string;
  reason?: string;
  battlefieldObjectId?: string;
  playerId?: string | null;
  previousControllerId?: string | null;
  controllerId?: string | null;
  sourceObjectId?: string | null;
  participantObjectIds?: string[];
  relatedEventKinds?: string[];
};

export type BattleResolutionView = {
  resolutionId?: string;
  tick?: number;
  kind?: string;
  reason?: string;
  battlefieldId?: string;
  attackingPlayerId?: string | null;
  defendingPlayerId?: string | null;
  winnerPlayerId?: string | null;
  attackerObjectIds?: string[];
  defenderObjectIds?: string[];
  survivingAttackerObjectIds?: string[];
  survivingDefenderObjectIds?: string[];
  destroyedObjectIds?: string[];
  relatedEventKinds?: string[];
};

export type StackItemView = {
  stackItemId?: string;
  controllerId?: string;
  sourceObjectId?: string;
  effectKind?: string;
  cardNo?: string | null;
  targetObjectIds?: string[];
  damageAmount?: number;
  destination?: string;
};

export type PendingTaskView = {
  taskId?: string;
  kind?: string;
  status?: string;
  reason?: string;
  battlefieldObjectId?: string;
  participantControllerIds?: string[];
  participantObjectIds?: string[];
  actingPlayerId?: string | null;
  stackItemIds?: string[];
  spellDuelId?: string;
  battleId?: string;
};

export type PendingTaskQueueView = {
  activeTaskId?: string | null;
  hasTasks?: boolean;
  isBlocking?: boolean;
  phase?: string;
  tasks?: PendingTaskView[];
};

export type TriggerQueueItemView = {
  triggerId?: string;
  controllerId?: string;
  sourceObjectId?: string;
  sourceVisibility?: string;
  effectKind?: string;
  triggeredByEventKind?: string;
};

export type TurnWindowView = {
  state?: string;
  isSpellDuel?: boolean;
  isClosed?: boolean;
  hasStack?: boolean;
  actingPlayerId?: string | null;
};

export type RuleQueueCoverageSnapshotRow = {
  key?: "battle" | "payment" | "stack" | "trigger" | "window" | (string & {});
  liveCount?: number;
  evidenceKeys?: string[];
};

export type MatchTimingView = Record<string, unknown> & {
  battle?: { isActive?: boolean } | null;
  battleResolutions?: BattleResolutionView[];
  battlefieldResolutions?: BattlefieldResolutionView[];
  battlefieldTasks?: PendingTaskView[];
  pendingPayment?: Record<string, unknown> | null;
  pendingTaskQueue?: PendingTaskQueueView | null;
  phase?: string;
  ruleQueueCoverage?: RuleQueueCoverageSnapshotRow[] | null;
  roomStatus?: string;
  timingState?: string;
  triggerQueue?: TriggerQueueItemView[];
  turnWindow?: TurnWindowView | null;
};

export type ActionPromptChoiceDto = {
  id: string;
  label: string;
  objectIds?: string[] | null;
  reason?: string | null;
};

export type ActionPromptSelectionChoiceDto = {
  id: string;
  label: string;
  objectIds: string[];
  reason?: string | null;
};

export type ActionPromptSelectionStepDto = {
  role: "source" | "target" | "destination" | "mode" | "optionalCost" | (string & {});
  label: string;
  required: boolean;
  choices: ActionPromptSelectionChoiceDto[];
};

export type ActionPromptCommandTemplateBindingDto = {
  field: string;
  label?: string | null;
  source:
    | "selectedSource"
    | "selectedTarget"
    | "selectedTargets"
    | "selectedDestination"
    | "selectedMode"
    | "selectedOptionalCosts"
    | "candidateMetadata"
    | "requirementMetadata"
    | (string & {});
  required?: boolean;
  asArray?: boolean;
  omitEmpty?: boolean;
  metadataKey?: string | null;
  metadataKeys?: string[] | null;
};

export type ActionPromptCommandTemplateDto = {
  cmdType: string;
  bindings: ActionPromptCommandTemplateBindingDto[];
};

export type ActionPromptComposerDto = {
  supported: boolean;
  reason: string;
  selectionRoles: string[];
  requiredSelectionRoles: string[];
  commandFields: string[];
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
  selectionSteps?: ActionPromptSelectionStepDto[] | null;
  commandTemplate?: ActionPromptCommandTemplateDto | null;
  composer?: ActionPromptComposerDto | null;
};

export type ActionPromptObjectCandidateDto = {
  action: string;
  label: string;
  enabled: boolean;
  reason: string;
  roles: string[];
  commandType?: string | null;
  requiredCommandFields?: string[] | null;
  commandFields?: string[] | null;
  composer?: ActionPromptComposerDto | null;
};

export type ActionPromptObjectInspectionRowDto = {
  key: string;
  label: string;
  value: string;
  tone?: string | null;
};

export type ActionPromptObjectInspectionGroupDto = {
  key: string;
  title: string;
  rows: ActionPromptObjectInspectionRowDto[];
  emptyLabel?: string | null;
};

export type ActionPromptObjectInspectionDto = {
  source: string;
  boundary: string;
  summaryRows: ActionPromptObjectInspectionRowDto[];
  groups: ActionPromptObjectInspectionGroupDto[];
};

export type ActionPromptInspectionRowDto = {
  key: string;
  label: string;
  value: string;
  tone?: string | null;
};

export type ActionPromptInspectionGroupDto = {
  key: string;
  title: string;
  rows: ActionPromptInspectionRowDto[];
  emptyLabel?: string | null;
};

export type ActionPromptInspectionDto = {
  source: string;
  boundary: string;
  summaryRows: ActionPromptInspectionRowDto[];
  groups: ActionPromptInspectionGroupDto[];
};

export type ActionPromptObjectContextDto = {
  objectId: string;
  enabledCandidateCount: number;
  disabledCandidateCount: number;
  candidates: ActionPromptObjectCandidateDto[];
  inspection?: ActionPromptObjectInspectionDto | null;
  source?: string | null;
  boundary?: string | null;
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
  responsibility?: PromptResponsibilityDto | null;
};

export type PromptResponsibilityDto = {
  promptType: PromptType;
  promptPlayerId: string;
  responsiblePlayerId?: string | null;
  isResponsiblePlayer: boolean;
  actionableForPromptPlayer: boolean;
  state: string;
  nextStep: string;
  queueCounts: Record<string, number>;
  relatedObjectIds: string[];
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
  contract?: ActionPromptContractDto | null;
  inspection?: ActionPromptInspectionDto | null;
  objectContexts?: ActionPromptObjectContextDto[] | null;
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
  baseCards?: string[];
  baseRunes?: string[];
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
  unitsBySide?: Record<string, string[]>;
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
  stack: StackItemView[];
  timing: MatchTimingView;
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
  | { cmdType: "READY" }
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

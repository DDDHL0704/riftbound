export type ParsedCostSpec = {
  mana?: number | null;
  returnEnergy?: number | null;
  power?: number | null;
  additionalCosts: string[];
  optionalCosts: string[];
};

export type KeywordSpec = {
  keyword: string;
  rawText: string;
  value?: string | null;
};

export type StaticAuraSpec = {
  kind: string;
  layer: string;
  duration: string;
  targetScope: string;
  participantScope: string;
  powerDeltaPerParticipant: number;
  text: string;
  status: string;
  reason: string;
  targetFilter?: string | null;
  grantedKeyword?: string | null;
};

export type BehaviorSpec = {
  cardNo: string;
  cardName: string;
  cardCategoryName: string;
  functionalUnitId: string;
  status: string;
  reason: string;
  officialText: string;
  frontImage: string;
  backImage: string;
  cost: ParsedCostSpec;
  keywords: KeywordSpec[];
  targets: Array<{ scope: string; minCount: number; maxCount?: number | null; text: string; optional?: boolean }>;
  triggers: Array<{
    kind: string;
    timing: string;
    text: string;
    reason: string;
    targetScope?: string | null;
    powerDelta?: number | null;
    duration?: string | null;
    manaDelta?: number | null;
    drawCount?: number | null;
    drawCountPerParticipant?: number | null;
    minimumPaidMana?: number | null;
    revealCount?: number | null;
    revealSourceZone?: string | null;
    recycleCount?: number | null;
    recycleSourceZone?: string | null;
    recycleDestinationZone?: string | null;
    millCount?: number | null;
    millSourceZone?: string | null;
    millDestinationZone?: string | null;
    discardCount?: number | null;
    discardSourceZone?: string | null;
    discardDestinationZone?: string | null;
    manaCost?: number | null;
    boonCount?: number | null;
    consumedBoonCount?: number | null;
    runeCallCount?: number | null;
    moveCount?: number | null;
    moveDestination?: string | null;
    oncePerTurn?: boolean | null;
    excludesTokens?: boolean | null;
    createdTokenCount?: number | null;
    createdTokenName?: string | null;
    createdTokenPower?: number | null;
    createdTokenDestination?: string | null;
    returnCount?: number | null;
    requiredEmptyZone?: string | null;
    returnOriginZone?: string | null;
    returnDestinationZone?: string | null;
    returnCardFilter?: string | null;
    requiredUnitCount?: number | null;
    winsGame?: boolean | null;
  }>;
  replacements: Array<{ kind: string; appliesTo: string; text: string; reason: string }>;
  activatedAbilities: Array<{ costText: string; effectText: string; templateIds: string[]; status: string; reason: string }>;
  staticAbilities: Array<{ kind: string; text: string; status: string; reason: string }>;
  staticAuras: StaticAuraSpec[];
  effects: Array<{ templateId: string; phrase: string; status: string; reason: string }>;
  templateIds: string[];
  implementedEffectKind?: string | null;
  implementedByCardNo?: string | null;
  conformanceTier: string;
  conformanceReason: string;
};

export type KeywordCoverageReport = {
  behaviorSpecs: number;
  cardsWithKeywordProfiles: number;
  statusCounts: Record<string, number>;
  families: KeywordCoverageFamily[];
};

export type KeywordCoverageFamily = {
  family: string;
  statusCounts: Record<string, number>;
  deferredCards: Array<{
    cardNo: string;
    cardName: string;
    keywords: string[];
    status: string;
    reason: string;
  }>;
};

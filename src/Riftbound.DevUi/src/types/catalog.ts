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

export type BehaviorSpec = {
  cardNo: string;
  cardName: string;
  cardCategoryName: string;
  functionalUnitId: string;
  status: string;
  reason: string;
  officialText: string;
  cost: ParsedCostSpec;
  keywords: KeywordSpec[];
  targets: Array<{ scope: string; minCount: number; maxCount?: number | null; text: string; optional?: boolean }>;
  triggers: Array<{ kind: string; timing: string; text: string; reason: string }>;
  replacements: Array<{ kind: string; appliesTo: string; text: string; reason: string }>;
  activatedAbilities: Array<{ costText: string; effectText: string; templateIds: string[]; status: string; reason: string }>;
  staticAbilities: Array<{ kind: string; text: string; status: string; reason: string }>;
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

import type { ActionPromptCandidateDto, ActionPromptChoiceDto, ActionPromptDto, CardObjectView, SnapshotDto } from "../types/protocol";
import { redactInternalText } from "./redaction";

export type HandChoiceItem = {
  objectId: string;
  label: string;
  reason?: string;
};

export type HandChoiceModel = {
  choiceId: string;
  choiceWindow: string;
  choosingPlayerId?: string;
  effectKind?: string;
  reason?: string;
  requiredCount?: number;
  maxCount?: number;
  handChoices: HandChoiceItem[];
  resetKey: string;
};

export type DamageAssignmentChoice = {
  key: string;
  sourceObjectId: string;
  sourceLabel: string;
  targetObjectId: string;
  targetLabel: string;
  existingDamage?: number;
  lethalThreshold?: number;
  sourceDamagePool?: number;
};

export type DamageAssignmentModel = {
  battleId: string;
  battlefieldId: string;
  damagePoolLabel?: string;
  choices: DamageAssignmentChoice[];
  resetKey: string;
};

export type TriggerOrderItem = {
  triggerId: string;
  label: string;
  source?: string;
  controller?: string;
  summary?: string;
  constraint?: string;
};

export type OrderTriggersModel = {
  constraints: string[];
  triggeredByEventKind?: string;
  triggers: TriggerOrderItem[];
  resetKey: string;
};

export type PaymentChoiceItem = {
  id: string;
  label: string;
  reason?: string;
  source: "resource" | "spend";
};

export type PayCostModel = {
  choices: PaymentChoiceItem[];
  costLabel?: string;
  paymentChoiceIds: string[];
  paymentId: string;
  paymentWindow: string;
  resetKey: string;
};

export function buildHandChoiceModel(candidate: ActionPromptCandidateDto | undefined, prompt: ActionPromptDto | undefined): HandChoiceModel {
  const metadata = candidate?.metadata ?? {};
  const requiredCount = numberMetadata(metadata, "requiredCount") ?? prompt?.view?.minSelection ?? undefined;
  const maxCount = numberMetadata(metadata, "maxCount") ?? prompt?.view?.maxSelection ?? requiredCount;
  const legalObjectIds = new Set(stringArrayMetadata(metadata, "legalObjectIds") ?? []);
  const handChoices = handChoiceItems(metadata.handChoices)
    .filter((choice) => legalObjectIds.size === 0 || legalObjectIds.has(choice.objectId));

  return {
    choiceId: stringMetadata(metadata, "choiceId") ?? "",
    choiceWindow: stringMetadata(metadata, "choiceWindow") ?? "",
    choosingPlayerId: safeOptionalText(stringMetadata(metadata, "choosingPlayerId")),
    effectKind: safeOptionalText(stringMetadata(metadata, "effectKind")),
    reason: safeOptionalText(stringMetadata(metadata, "reason")),
    requiredCount,
    maxCount,
    handChoices,
    resetKey: [
      stringMetadata(metadata, "choiceId") ?? "none",
      stringMetadata(metadata, "choiceWindow") ?? "none",
      requiredCount ?? "none",
      maxCount ?? "none",
      handChoices.map((choice) => choice.objectId).join("|")
    ].join("::")
  };
}

export function buildDamageAssignmentModel(
  candidate: ActionPromptCandidateDto,
  prompt: ActionPromptDto | undefined,
  snapshot: SnapshotDto | undefined
): DamageAssignmentModel {
  const metadata = candidate.metadata ?? {};
  const battleId = stringMetadata(metadata, "battleId") ?? prompt?.view?.relatedBattleId ?? "";
  const battlefieldId = stringMetadata(metadata, "battlefieldId")
    ?? stringMetadata(metadata, "battlefieldObjectId")
    ?? prompt?.serverFlow?.relatedBattlefieldId
    ?? prompt?.view?.relatedBattlefieldId
    ?? "";
  const scalarDamagePool = firstNumberMetadata(metadata, ["damagePool", "totalDamage", "assignableDamage"]);
  const damagePoolBySource = numberMapMetadata(metadata, ["damagePool", "damagePoolBySource"]);
  const damagePoolLabel = scalarDamagePool == null
    ? damagePoolBySource.size > 0 ? `${damagePoolBySource.size} 个来源` : undefined
    : String(scalarDamagePool);
  const assignmentRecords = recordArrayMetadata(metadata.assignmentChoices);
  const existingDamageByTarget = numberMapMetadata(metadata, ["existingDamage", "existingDamageByTarget", "damageByTarget"]);
  const lethalThresholdByTarget = numberMapMetadata(metadata, ["lethalThreshold", "lethalThresholdByTarget", "lethalThresholds", "lethalDamageThreshold"]);
  const legalTargetsBySource = stringListMapMetadata(metadata, ["legalTargets", "legalTargetsBySource"]);
  const choices = assignmentRecords
    .map((record) => damageChoiceFromRecord(record, snapshot, existingDamageByTarget, lethalThresholdByTarget, damagePoolBySource))
    .filter((choice): choice is DamageAssignmentChoice => choice != null);

  const fallbackChoices = choices.length > 0
    ? choices
    : legalTargetsBySource.size > 0
      ? damageChoicesFromLegalTargets(legalTargetsBySource, snapshot, existingDamageByTarget, lethalThresholdByTarget, damagePoolBySource)
      : damageChoicesFromCandidate(candidate, snapshot, existingDamageByTarget, lethalThresholdByTarget, damagePoolBySource);
  const dedupedChoices = uniqueDamageChoices(fallbackChoices);

  return {
    battleId,
    battlefieldId,
    damagePoolLabel,
    choices: dedupedChoices,
    resetKey: [
      battleId,
      battlefieldId,
      damagePoolLabel ?? "none",
      dedupedChoices.map((choice) => choice.key).join("|")
    ].join("::")
  };
}

export function buildOrderTriggersModel(candidate: ActionPromptCandidateDto | undefined, prompt: ActionPromptDto | undefined): OrderTriggersModel {
  const metadata = {
    ...(prompt?.view?.metadata ?? {}),
    ...(candidate?.metadata ?? {})
  };
  const triggerChoices = choiceArrayMetadata(metadata.triggerChoices);
  const triggerRecords = [
    ...recordArrayMetadata(metadata.triggers),
    ...recordArrayMetadata(metadata.triggerOrdering)
  ];
  const preferredOrder = firstStringArrayMetadata(metadata, ["orderedTriggerIds", "triggerIds"])
    ?? firstStringArrayFromRecord(metadata.triggerOrdering, ["orderedTriggerIds", "triggerIds", "order"])
    ?? stringArrayFromValue(metadata.triggerOrdering, true)
    ?? triggerChoices.map((choice) => choice.id);
  const sourceItems = [
    ...triggerRecords.map(triggerItemFromRecord).filter((item): item is TriggerOrderItem => item != null),
    ...triggerChoices.map(triggerItemFromChoice),
    ...(candidate?.sources ?? []).map(triggerItemFromChoice),
    ...preferredOrder.map((triggerId) => triggerItemFromId(triggerId))
  ];
  const triggers = orderTriggerItems(uniqueTriggerItems(sourceItems), preferredOrder);
  const constraints = constraintSummaries(
    metadata.legalOrderingConstraints
      ?? metadata.orderingConstraints
      ?? metadata.constraints
      ?? (isRecord(metadata.triggerOrdering) ? metadata.triggerOrdering.constraints : undefined)
  );
  const triggeredByEventKind = safeOptionalText(stringMetadata(metadata, "triggeredByEventKind"));

  return {
    constraints,
    triggeredByEventKind,
    triggers,
    resetKey: [
      triggeredByEventKind ?? "none",
      triggers.map((trigger) => trigger.triggerId).join("|"),
      constraints.join("|")
    ].join("::")
  };
}

export function buildPayCostModel(candidate: ActionPromptCandidateDto | undefined, prompt: ActionPromptDto | undefined): PayCostModel {
  const metadata = {
    ...(prompt?.view?.metadata ?? {}),
    ...(candidate?.metadata ?? {})
  };
  const resourceChoices = paymentChoiceItems(metadata.paymentResourceChoices, "resource");
  const spendChoices = paymentChoiceItems(metadata.paymentChoices, "spend");
  const choices = uniquePaymentChoices([...resourceChoices, ...spendChoices]);
  const visibleChoiceIds = new Set(choices.map((choice) => choice.id));
  const paymentChoiceIds = (firstStringArrayMetadata(metadata, ["paymentChoiceIds", "legalPaymentChoiceIds", "defaultPaymentChoiceIds"]) ?? [])
    .filter((choiceId) => visibleChoiceIds.size === 0 || visibleChoiceIds.has(choiceId));

  return {
    choices,
    costLabel: safePromptSummary(metadata.cost ?? metadata.costSummary ?? metadata.paymentCost),
    paymentChoiceIds,
    paymentId: stringMetadata(metadata, "paymentId") ?? "",
    paymentWindow: stringMetadata(metadata, "paymentWindow") ?? "",
    resetKey: [
      stringMetadata(metadata, "paymentId") ?? "none",
      stringMetadata(metadata, "paymentWindow") ?? "none",
      paymentChoiceIds.join("|"),
      choices.map((choice) => `${choice.source}:${choice.id}`).join("|")
    ].join("::")
  };
}

export function clampDamageInput(value: number): number {
  return Number.isFinite(value) && value > 0 ? Math.floor(value) : 0;
}

export function findCardNo(snapshot: SnapshotDto | undefined, objectId: string): string | undefined {
  return findObject(snapshot, objectId)?.cardNo ?? undefined;
}

function paymentChoiceItems(value: unknown, source: PaymentChoiceItem["source"]): PaymentChoiceItem[] {
  if (Array.isArray(value)) {
    return value
      .map((item) => paymentChoiceItem(item, source))
      .filter((choice): choice is PaymentChoiceItem => choice != null);
  }

  const singleChoice = paymentChoiceItem(value, source);
  if (singleChoice) {
    return [singleChoice];
  }

  return recordArrayMetadata(value)
    .map((record) => paymentChoiceItem(record, source))
    .filter((choice): choice is PaymentChoiceItem => choice != null);
}

function paymentChoiceItem(value: unknown, source: PaymentChoiceItem["source"]): PaymentChoiceItem | undefined {
  if (typeof value === "string" && value.trim().length > 0) {
    const id = value.trim();
    return {
      id,
      label: id,
      source
    };
  }

  if (!isRecord(value)) {
    return undefined;
  }

  const id = firstStringFromRecord(value, ["id", "choiceId", "paymentChoiceId", "resourceChoiceId", "objectId", "actionId"]);
  if (!id) {
    return undefined;
  }

  return {
    id,
    label: safeOptionalText(firstStringFromRecord(value, ["label", "summary", "visibleText", "text", "title", "name"]))
      ?? id,
    reason: safeOptionalText(firstStringFromRecord(value, ["reason", "description"])),
    source
  };
}

function uniquePaymentChoices(choices: PaymentChoiceItem[]): PaymentChoiceItem[] {
  const byId = new Map<string, PaymentChoiceItem>();
  for (const choice of choices) {
    if (!byId.has(choice.id)) {
      byId.set(choice.id, choice);
    }
  }

  return [...byId.values()];
}

function handChoiceItems(value: unknown): HandChoiceItem[] {
  if (Array.isArray(value)) {
    return value
      .map(handChoiceItem)
      .filter((choice): choice is HandChoiceItem => choice != null);
  }

  return [];
}

function handChoiceItem(value: unknown): HandChoiceItem | undefined {
  if (typeof value === "string" && value.trim().length > 0) {
    return {
      objectId: value.trim(),
      label: "服务端手牌候选"
    };
  }

  if (!isRecord(value)) {
    return undefined;
  }

  const objectId = firstStringFromRecord(value, ["objectId", "id", "choiceId", "sourceObjectId"]);
  if (!objectId) {
    return undefined;
  }

  return {
    objectId,
    label: safeOptionalText(firstStringFromRecord(value, ["label", "cardName", "cardNo", "summary", "visibleText", "text", "title", "name"]))
      ?? "服务端手牌候选",
    reason: safeOptionalText(firstStringFromRecord(value, ["reason", "description"]))
  };
}

function damageChoiceFromRecord(
  record: Record<string, unknown>,
  snapshot: SnapshotDto | undefined,
  existingDamageByTarget: Map<string, number>,
  lethalThresholdByTarget: Map<string, number>,
  damagePoolBySource: Map<string, number>
): DamageAssignmentChoice | undefined {
  const parsedChoiceId = parseAssignmentChoiceId(firstStringFromRecord(record, ["id", "choiceId"]));
  const sourceObjectId = firstStringFromRecord(record, ["sourceObjectId", "sourceId", "attackerObjectId", "objectId"])
    ?? parsedChoiceId?.sourceObjectId;
  const targetObjectId = firstStringFromRecord(record, ["targetObjectId", "targetId", "defenderObjectId", "legalTargetId"])
    ?? parsedChoiceId?.targetObjectId;
  if (!sourceObjectId || !targetObjectId) {
    return undefined;
  }

  return makeDamageChoice({
    sourceObjectId,
    sourceLabel: firstStringFromRecord(record, ["sourceLabel", "sourceName"]) ?? objectLabel(snapshot, sourceObjectId),
    targetObjectId,
    targetLabel: firstStringFromRecord(record, ["targetLabel", "targetName", "label"]) ?? objectLabel(snapshot, targetObjectId),
    existingDamage: firstNumberFromRecord(record, ["existingDamage", "currentDamage"])
      ?? existingDamageByTarget.get(targetObjectId)
      ?? findObject(snapshot, targetObjectId)?.damage,
    lethalThreshold: firstNumberFromRecord(record, ["lethalThreshold", "lethalDamage", "lethalAt"])
      ?? lethalThresholdByTarget.get(targetObjectId),
    sourceDamagePool: firstNumberFromRecord(record, ["sourceDamagePool", "damagePool", "assignableDamage", "maxDamage"])
      ?? damagePoolBySource.get(sourceObjectId)
  });
}

function damageChoicesFromLegalTargets(
  legalTargetsBySource: Map<string, string[]>,
  snapshot: SnapshotDto | undefined,
  existingDamageByTarget: Map<string, number>,
  lethalThresholdByTarget: Map<string, number>,
  damagePoolBySource: Map<string, number>
): DamageAssignmentChoice[] {
  return [...legalTargetsBySource.entries()].flatMap(([sourceObjectId, targetObjectIds]) =>
    targetObjectIds.map((targetObjectId) => makeDamageChoice({
      sourceObjectId,
      sourceLabel: objectLabel(snapshot, sourceObjectId),
      targetObjectId,
      targetLabel: objectLabel(snapshot, targetObjectId),
      existingDamage: existingDamageByTarget.get(targetObjectId) ?? findObject(snapshot, targetObjectId)?.damage,
      lethalThreshold: lethalThresholdByTarget.get(targetObjectId),
      sourceDamagePool: damagePoolBySource.get(sourceObjectId)
    }))
  );
}

function damageChoicesFromCandidate(
  candidate: ActionPromptCandidateDto,
  snapshot: SnapshotDto | undefined,
  existingDamageByTarget: Map<string, number>,
  lethalThresholdByTarget: Map<string, number>,
  damagePoolBySource: Map<string, number>
): DamageAssignmentChoice[] {
  const sources = candidate.sources ?? [];
  const targets = candidate.targets ?? [];
  if (sources.length !== 1 || targets.length === 0) {
    return [];
  }

  const source = sources[0];
  return targets.map((target) => makeDamageChoice({
    sourceObjectId: source.id,
    sourceLabel: choiceLabel(source),
    targetObjectId: target.id,
    targetLabel: choiceLabel(target),
    existingDamage: existingDamageByTarget.get(target.id) ?? findObject(snapshot, target.id)?.damage,
    lethalThreshold: lethalThresholdByTarget.get(target.id),
    sourceDamagePool: damagePoolBySource.get(source.id)
  }));
}

function makeDamageChoice(choice: Omit<DamageAssignmentChoice, "key">): DamageAssignmentChoice {
  return {
    ...choice,
    key: `${choice.sourceObjectId}->${choice.targetObjectId}`
  };
}

function uniqueDamageChoices(choices: DamageAssignmentChoice[]): DamageAssignmentChoice[] {
  const seen = new Set<string>();
  const result: DamageAssignmentChoice[] = [];
  for (const choice of choices) {
    if (!seen.has(choice.key)) {
      seen.add(choice.key);
      result.push(choice);
    }
  }
  return result;
}

function triggerItemFromChoice(choice: ActionPromptChoiceDto): TriggerOrderItem {
  return {
    triggerId: choice.id,
    label: choiceLabel(choice),
    summary: safeOptionalText(choice.reason ?? undefined)
  };
}

function triggerItemFromId(triggerId: string): TriggerOrderItem {
  return {
    triggerId,
    label: triggerId
  };
}

function triggerItemFromRecord(record: Record<string, unknown>): TriggerOrderItem | undefined {
  const triggerId = firstStringFromRecord(record, ["triggerId", "id", "choiceId"]);
  if (!triggerId) {
    return undefined;
  }

  return {
    triggerId,
    label: safeOptionalText(firstStringFromRecord(record, ["label", "summary", "visibleText", "text", "title", "name"])) ?? triggerId,
    source: safeOptionalText(firstStringFromRecord(record, ["source", "sourceLabel", "sourceId", "sourceObjectId", "sourceCardNo"])),
    controller: safeOptionalText(firstStringFromRecord(record, ["controller", "controllerId", "playerId"])),
    summary: safeOptionalText(firstStringFromRecord(record, ["summary", "visibleText", "text", "description"])),
    constraint: safePromptSummary(record.legalOrderingConstraint ?? record.orderingConstraint ?? record.constraint ?? record.constraints)
  };
}

function uniqueTriggerItems(items: TriggerOrderItem[]): TriggerOrderItem[] {
  const byId = new Map<string, TriggerOrderItem>();
  for (const item of items) {
    const existing = byId.get(item.triggerId);
    byId.set(item.triggerId, existing ? { ...item, ...existing } : item);
  }
  return [...byId.values()];
}

function orderTriggerItems(items: TriggerOrderItem[], preferredOrder: string[]): TriggerOrderItem[] {
  if (preferredOrder.length === 0) {
    return items;
  }

  const byId = new Map(items.map((item) => [item.triggerId, item]));
  const ordered = preferredOrder
    .map((triggerId) => byId.get(triggerId) ?? triggerItemFromId(triggerId))
    .filter((item, index, array) => array.findIndex((candidate) => candidate.triggerId === item.triggerId) === index);
  const orderedIds = new Set(ordered.map((item) => item.triggerId));
  return [...ordered, ...items.filter((item) => !orderedIds.has(item.triggerId))];
}

function findObject(snapshot: SnapshotDto | undefined, objectId: string): CardObjectView | undefined {
  if (!snapshot) {
    return undefined;
  }

  for (const player of Object.values(snapshot.players)) {
    const cardObject = player.objects?.[objectId];
    if (cardObject) {
      return cardObject;
    }
  }

  return undefined;
}

function objectLabel(snapshot: SnapshotDto | undefined, objectId: string): string {
  const cardNo = findCardNo(snapshot, objectId);
  return cardNo ? `${cardNo} · ${objectId}` : objectId;
}

function choiceLabel(choice: ActionPromptChoiceDto): string {
  return redactInternalText(choice.label || choice.id || "服务端选项");
}

function numberMetadata(metadata: Record<string, unknown> | null | undefined, key: string): number | undefined {
  const value = metadata?.[key];
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

function firstNumberMetadata(metadata: Record<string, unknown>, keys: string[]): number | undefined {
  for (const key of keys) {
    const value = numberMetadata(metadata, key);
    if (value != null) {
      return value;
    }
  }
  return undefined;
}

function stringMetadata(metadata: Record<string, unknown>, key: string): string | undefined {
  const value = metadata[key];
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : undefined;
}

function stringArrayMetadata(metadata: Record<string, unknown>, key: string): string[] | undefined {
  const value = metadata[key];
  return stringArrayFromValue(value);
}

function firstStringArrayMetadata(metadata: Record<string, unknown>, keys: string[]): string[] | undefined {
  for (const key of keys) {
    const values = stringArrayFromValue(metadata[key], true);
    if (values && values.length > 0) {
      return values;
    }
  }
  return undefined;
}

function firstStringArrayFromRecord(value: unknown, keys: string[]): string[] | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  return firstStringArrayMetadata(value, keys);
}

function stringArrayFromValue(value: unknown, requireNonEmpty = false): string[] | undefined {
  if (!Array.isArray(value)) {
    return undefined;
  }

  const values = value.map((item) => typeof item === "string" ? item.trim() : "");
  return (!requireNonEmpty || values.length > 0) && values.every((item) => item.length > 0) ? values : undefined;
}

function choiceArrayMetadata(value: unknown): ActionPromptChoiceDto[] {
  const choices: ActionPromptChoiceDto[] = [];
  for (const record of recordArrayMetadata(value)) {
    const id = firstStringFromRecord(record, ["id", "triggerId", "choiceId"]);
    if (id) {
      choices.push({
        id,
        label: firstStringFromRecord(record, ["label", "summary", "visibleText", "text", "title", "name"]) ?? id,
        reason: firstStringFromRecord(record, ["reason", "description"])
      });
    }
  }

  return choices;
}

function recordArrayMetadata(value: unknown): Array<Record<string, unknown>> {
  if (Array.isArray(value)) {
    return value.filter(isRecord);
  }

  if (isRecord(value)) {
    return Object.values(value).filter(isRecord);
  }

  return [];
}

function numberMapMetadata(metadata: Record<string, unknown>, keys: string[]): Map<string, number> {
  for (const key of keys) {
    const value = metadata[key];
    if (isRecord(value)) {
      return new Map(
        Object.entries(value)
          .filter((entry): entry is [string, number] => typeof entry[1] === "number" && Number.isFinite(entry[1]))
      );
    }
  }

  return new Map();
}

function stringListMapMetadata(metadata: Record<string, unknown>, keys: string[]): Map<string, string[]> {
  for (const key of keys) {
    const value = metadata[key];
    if (isRecord(value)) {
      return new Map(
        Object.entries(value)
          .map(([sourceObjectId, targets]) => [
            sourceObjectId,
            Array.isArray(targets)
              ? targets.filter((target): target is string => typeof target === "string" && target.trim().length > 0)
              : []
          ] as const)
          .filter(([, targets]) => targets.length > 0)
      );
    }
  }

  return new Map();
}

function parseAssignmentChoiceId(value: string | undefined): { sourceObjectId: string; targetObjectId: string } | undefined {
  if (!value) {
    return undefined;
  }

  const [sourceObjectId, targetObjectId] = value.split("->").map((part) => part.trim());
  return sourceObjectId && targetObjectId ? { sourceObjectId, targetObjectId } : undefined;
}

function constraintSummaries(value: unknown): string[] {
  if (Array.isArray(value)) {
    return value
      .map(safePromptSummary)
      .filter((item): item is string => item != null)
      .slice(0, 12);
  }

  const summary = safePromptSummary(value);
  return summary ? [summary] : [];
}

function safePromptSummary(value: unknown): string | undefined {
  if (typeof value === "string") {
    return safeOptionalText(value);
  }

  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }

  if (Array.isArray(value)) {
    const stringValues = value
      .filter((item): item is string => typeof item === "string" && item.trim().length > 0)
      .map((item) => safeOptionalText(item))
      .filter((item): item is string => item != null);
    return stringValues.length > 0 ? stringValues.slice(0, 3).join("、") : `${value.length} 项`;
  }

  if (isRecord(value)) {
    const label = firstStringFromRecord(value, ["label", "summary", "visibleText", "text", "description"]);
    return safeOptionalText(label) ?? `${Object.keys(value).length} 项`;
  }

  return undefined;
}

function safeOptionalText(value: string | undefined): string | undefined {
  if (!value?.trim()) {
    return undefined;
  }

  return redactInternalText(value);
}

function firstStringFromRecord(record: Record<string, unknown>, keys: string[]): string | undefined {
  for (const key of keys) {
    const value = record[key];
    if (typeof value === "string" && value.trim().length > 0) {
      return value.trim();
    }
  }
  return undefined;
}

function firstNumberFromRecord(record: Record<string, unknown>, keys: string[]): number | undefined {
  for (const key of keys) {
    const value = record[key];
    if (typeof value === "number" && Number.isFinite(value)) {
      return value;
    }
  }
  return undefined;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

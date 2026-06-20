import type {
  ActionPromptCandidateDto,
  ActionPromptChoiceDto,
  GameCommand,
  SnapshotDto
} from "../types/protocol";
import { commandFromActionPromptTemplate } from "./actionPromptCommandTemplate";
import type { CandidateSelectionDraft } from "./candidateSelectionDraft";
import { redactInternalText } from "./redaction";
import type { ServerSubmissionGatePlan } from "./serverSubmissionGatePlan";

export type ChoiceGroup = {
  key: string;
  label: string;
  choices: ActionPromptChoiceDto[];
  required: boolean;
};

export type CandidateComposerState = {
  destinationId?: string;
  mode?: string;
  optionalCostIds: string[];
  sourceId?: string;
  targetIdsByGroup: Record<string, string>;
};

export type CandidateComposerModel = {
  resetKey: string;
  sourceRequirements: Array<Record<string, unknown>>;
  sourceRequirementById: Map<string, Record<string, unknown>>;
};

export type CandidateComposerControls = {
  destinationChoices: ActionPromptChoiceDto[];
  destinationRequired: boolean;
  modeChoices: ActionPromptChoiceDto[];
  optionalCostChoices: ActionPromptChoiceDto[];
  requiredOptionalCostIds: string[];
  sourceRequired: boolean;
  sources: ActionPromptChoiceDto[];
  targetGroups: ChoiceGroup[];
};

export type CandidateComposerSubmissionPlan = {
  blockReason?: string;
  canSubmit: boolean;
  command?: GameCommand;
  gateCanSubmit: boolean;
  gateReason: string;
  gateStateLabel: string;
  missingRequiredTarget: boolean;
  optionalCostIds: string[];
  selectedTargetIds: string[];
  stateLabel: string;
  unsupportedReason?: string;
};

export type CandidateCommandPreviewPlan = {
  costLabels: string[];
  destinationLabel?: string;
  modeLabel?: string;
  sourceLabel: string;
  targetLabels: string[];
};

export function candidateComposerKey(candidate: Pick<ActionPromptCandidateDto, "action" | "label">): string {
  return `${candidate.action}::${candidate.label}`;
}

export function buildCandidateComposerModel(candidate: ActionPromptCandidateDto): CandidateComposerModel {
  const sourceRequirements = recordArrayMetadata(candidate.metadata?.sourceRequirements);
  const sourceRequirementById = new Map<string, Record<string, unknown>>();
  for (const requirement of sourceRequirements) {
    const sourceObjectId = stringMetadata(requirement, "sourceObjectId");
    if (sourceObjectId) {
      sourceRequirementById.set(sourceObjectId, requirement);
    }
  }

  const resetKey = [
    candidate.action,
    candidate.label,
    (candidate.sources ?? []).map((choice) => choice.id).join("|"),
    sourceRequirements
      .map((requirement) => [
        stringMetadata(requirement, "sourceObjectId"),
        stringMetadata(requirement, "cardNo") ?? stringMetadata(requirement, "equipmentCardNo"),
        stringMetadata(requirement, "abilityId"),
        choiceArrayMetadata(requirement.destinationChoices).map((choice) => choice.id).join(","),
        targetChoiceGroupCount(requirement.targetChoicesByIndex),
        choiceArrayMetadata(requirement.optionalCostChoices).map((choice) => choice.id).join(",")
      ].filter(Boolean).join(":"))
      .join("|")
  ].join("::");

  return { resetKey, sourceRequirements, sourceRequirementById };
}

export function initialComposerState(
  candidate: ActionPromptCandidateDto,
  model: CandidateComposerModel,
  forcedSourceId?: string,
  selectionDraft?: CandidateSelectionDraft
): CandidateComposerState {
  const fallbackSourceId = forcedSourceId
    ?? selectionDraft?.sourceObjectId
    ?? candidate.sources?.[0]?.id
    ?? firstRequirementSourceId(model);
  const requirement = selectedRequirement(model, fallbackSourceId);
  const controls = composerControls(candidate, model, requirement, forcedSourceId);
  const sourceId = forcedSourceId
    ?? fallbackSourceId
    ?? controls.sources[0]?.id;
  const destinationId = controls.destinationChoices[0]?.id;
  const targetIdsByGroup: Record<string, string> = {};
  for (const group of controls.targetGroups) {
    if (group.required && group.choices[0]?.id) {
      targetIdsByGroup[group.key] = group.choices[0].id;
    }
  }

  if (selectionDraft?.candidateKey === candidateComposerKey(candidate)
    && selectionDraft.sourceObjectId === sourceId) {
    for (const [index, targetChoiceId] of selectionDraft.targetChoiceIds.entries()) {
      const group = controls.targetGroups.find((candidateGroup) =>
        !targetIdsByGroup[candidateGroup.key]
        && candidateGroup.choices.some((choice) => choice.id === targetChoiceId))
        ?? controls.targetGroups[index];
      if (group?.choices.some((choice) => choice.id === targetChoiceId)) {
        targetIdsByGroup[group.key] = targetChoiceId;
      }
    }
  }

  return {
    destinationId: selectionDraft?.candidateKey === candidateComposerKey(candidate)
      && controls.destinationChoices.some((choice) => choice.id === selectionDraft.destinationId)
      ? selectionDraft.destinationId
      : destinationId,
    mode: selectionDraft?.candidateKey === candidateComposerKey(candidate)
      && controls.modeChoices.some((choice) => choice.id === selectionDraft.mode)
      ? selectionDraft.mode
      : controls.modeChoices[0]?.id,
    optionalCostIds: selectionDraft?.candidateKey === candidateComposerKey(candidate)
      ? uniqueStrings(selectionDraft.optionalCostIds.filter((id) => controls.optionalCostChoices.some((choice) => choice.id === id)))
      : [],
    sourceId,
    targetIdsByGroup
  };
}

export function selectedRequirement(
  model: CandidateComposerModel,
  sourceId: string | undefined
): Record<string, unknown> | undefined {
  if (sourceId) {
    const requirement = model.sourceRequirementById.get(sourceId);
    if (requirement) {
      return requirement;
    }
  }

  return model.sourceRequirements[0];
}

export function composerControls(
  candidate: ActionPromptCandidateDto,
  model: CandidateComposerModel,
  requirement: Record<string, unknown> | undefined,
  forcedSourceObjectId?: string
): CandidateComposerControls {
  const minTargetCount = requirement ? firstNumberMetadata(requirement, ["minTargetCount", "requiredTargetCount"]) ?? 0 : 0;
  const minDefenderCount = requirement ? firstNumberMetadata(requirement, ["minDefenderCount"]) ?? 0 : 0;
  const sources = sourceChoicesForCandidate(candidate, model, forcedSourceObjectId);
  const modeChoices = uniqueChoices([
    ...(candidate.modes ?? []),
    ...modeChoiceForRequirement(requirement)
  ]);
  const destinationChoices = uniqueChoices([
    ...(candidate.destinations ?? []),
    ...choiceArrayMetadata(requirement?.destinationChoices),
    ...choiceArrayMetadata(requirement?.battlefieldChoices)
  ]);
  const targetGroups = targetGroupsForRequirement(candidate, requirement, minTargetCount, minDefenderCount);
  const requiredOptionalCostIds = uniqueStrings([
    ...(stringArrayFromValue(requirement?.requiredOptionalCosts) ?? [])
  ]);
  const optionalCostChoices = uniqueChoices([
    ...(candidate.optionalCosts ?? []),
    ...choiceArrayMetadata(requirement?.optionalCostChoices),
    ...choiceArrayMetadata(requirement?.additionalCostChoices),
    ...choiceArrayMetadata(requirement?.paymentResourceChoices),
    ...requiredOptionalCostIds.map((id) => ({ id, label: id }))
  ]);

  return {
    destinationChoices,
    destinationRequired: destinationChoices.length > 0,
    modeChoices,
    optionalCostChoices,
    requiredOptionalCostIds,
    sourceRequired: true,
    sources,
    targetGroups
  };
}

export function composerCommand(
  candidate: ActionPromptCandidateDto,
  snapshot: SnapshotDto | undefined,
  state: CandidateComposerState,
  requirement: Record<string, unknown> | undefined,
  targetObjectIds: string[],
  optionalCostIds: string[]
): GameCommand | undefined {
  const sourceObjectId = state.sourceId;
  const cardNo = cardNoForRequirement(requirement, snapshot, sourceObjectId);
  const optionalCosts = optionalCostIds.length > 0 ? optionalCostIds : undefined;
  const destination = state.destinationId || undefined;
  const mode = state.mode || undefined;
  const templatedCommand = commandFromActionPromptTemplate(
    candidate.commandTemplate,
    {
      destinationId: state.destinationId,
      mode: state.mode,
      optionalCostIds,
      sourceId: state.sourceId,
      targetObjectIds
    },
    requirement
  );
  if (templatedCommand) {
    return templatedCommand;
  }

  switch (candidate.action) {
    case "PLAY_CARD":
      return sourceObjectId && cardNo
        ? { cmdType: "PLAY_CARD", sourceObjectId, cardNo, targetObjectIds, mode, destination, optionalCosts }
        : undefined;
    case "HIDE_CARD":
      return sourceObjectId && cardNo
        ? { cmdType: "HIDE_CARD", sourceObjectId, cardNo, destination, optionalCosts }
        : undefined;
    case "REVEAL_CARD":
      return sourceObjectId && cardNo
        ? { cmdType: "REVEAL_CARD", sourceObjectId, cardNo, mode, destination, targetObjectIds, optionalCosts }
        : undefined;
    case "MOVE_UNIT":
      return sourceObjectId
        ? { cmdType: "MOVE_UNIT", sourceObjectId, origin: stringMetadata(requirement ?? {}, "origin"), destination, optionalCosts }
        : undefined;
    case "ASSEMBLE_EQUIPMENT":
      return sourceObjectId
        ? { cmdType: "ASSEMBLE_EQUIPMENT", sourceObjectId, targetObjectId: targetObjectIds[0], optionalCosts }
        : undefined;
    case "DECLARE_BATTLE":
      return sourceObjectId
        ? {
            cmdType: "DECLARE_BATTLE",
            attackerObjectIds: [sourceObjectId],
            battlefieldId: destination,
            battlefieldTargetObjectIds: destination ? [destination] : undefined,
            defenderObjectIds: targetObjectIds,
            optionalCosts
          }
        : undefined;
    case "ACTIVATE_ABILITY":
      return sourceObjectId && stringMetadata(requirement ?? {}, "abilityId")
        ? {
            cmdType: "ACTIVATE_ABILITY",
            sourceObjectId,
            abilityId: stringMetadata(requirement ?? {}, "abilityId")!,
            targetObjectIds,
            optionalCosts
          }
        : undefined;
    case "LEGEND_ACT":
      return sourceObjectId && stringMetadata(requirement ?? {}, "abilityId")
        ? {
            cmdType: "LEGEND_ACT",
            sourceObjectId,
            abilityId: stringMetadata(requirement ?? {}, "abilityId")!,
            targetObjectIds,
            optionalCosts
          }
        : undefined;
    default:
      return undefined;
  }
}

export function buildCandidateComposerSubmissionPlan({
  candidate,
  controls,
  disabledByConnection,
  requirement,
  snapshot,
  submissionGate,
  state
}: {
  candidate: ActionPromptCandidateDto;
  controls: CandidateComposerControls;
  disabledByConnection: boolean;
  requirement: Record<string, unknown> | undefined;
  snapshot: SnapshotDto | undefined;
  submissionGate?: ServerSubmissionGatePlan;
  state: CandidateComposerState;
}): CandidateComposerSubmissionPlan {
  const selectedTargetIds = controls.targetGroups
    .map((group) => state.targetIdsByGroup[group.key])
    .filter((id): id is string => Boolean(id));
  const optionalCostIds = uniqueStrings([...controls.requiredOptionalCostIds, ...state.optionalCostIds]);
  const command = composerCommand(candidate, snapshot, state, requirement, selectedTargetIds, optionalCostIds);
  const unsupportedReason = requirement && !booleanFromRecord(requirement, "composable", true)
    ? safePromptSummary(requirement.unsupportedReason) ?? "服务端暂未开放该候选的组合提交。"
    : undefined;
  const missingRequiredTarget = controls.targetGroups
    .some((group) => group.required && !state.targetIdsByGroup[group.key]);
  const gateCanSubmit = submissionGate?.canSubmit ?? !disabledByConnection;
  const gateReason = submissionGate?.reason ?? (disabledByConnection
    ? "当前入口不可提交，等待服务端窗口或连接恢复。"
    : "当前未提供额外提交门禁。");
  const gateStateLabel = submissionGate?.stateLabel ?? (gateCanSubmit ? "可提交" : "连接未就绪");
  const missingRequiredSource = controls.sourceRequired && !state.sourceId;
  const missingRequiredDestination = controls.destinationRequired && !state.destinationId;
  const missingCommand = !command;
  const canSubmit = !disabledByConnection
    && gateCanSubmit
    && candidate.enabled
    && !unsupportedReason
    && !missingCommand
    && !missingRequiredSource
    && !missingRequiredDestination
    && !missingRequiredTarget;
  const blockReason = composerBlockReason({
    candidate,
    disabledByConnection,
    gateCanSubmit,
    gateReason,
    missingCommand,
    missingRequiredDestination,
    missingRequiredSource,
    missingRequiredTarget,
    unsupportedReason
  });

  return {
    blockReason,
    canSubmit,
    command,
    gateCanSubmit,
    gateReason,
    gateStateLabel,
    missingRequiredTarget,
    optionalCostIds,
    selectedTargetIds,
    stateLabel: canSubmit ? "待服务端校验" : blockReason ? gateStateLabelForBlock(blockReason, gateStateLabel) : "需要选择",
    unsupportedReason
  };
}

function composerBlockReason({
  candidate,
  disabledByConnection,
  gateCanSubmit,
  gateReason,
  missingCommand,
  missingRequiredDestination,
  missingRequiredSource,
  missingRequiredTarget,
  unsupportedReason
}: {
  candidate: ActionPromptCandidateDto;
  disabledByConnection: boolean;
  gateCanSubmit: boolean;
  gateReason: string;
  missingCommand: boolean;
  missingRequiredDestination: boolean;
  missingRequiredSource: boolean;
  missingRequiredTarget: boolean;
  unsupportedReason?: string;
}): string | undefined {
  if (!gateCanSubmit) {
    return gateReason;
  }

  if (disabledByConnection) {
    return "当前入口不可提交，等待服务端窗口或连接恢复。";
  }

  if (!candidate.enabled) {
    return candidate.reason || "服务端当前阻断该候选。";
  }

  if (unsupportedReason) {
    return unsupportedReason;
  }

  if (missingRequiredSource) {
    return "缺少服务端候选要求的来源。";
  }

  if (missingRequiredDestination) {
    return "缺少服务端候选要求的位置。";
  }

  if (missingRequiredTarget) {
    return "缺少服务端候选要求的目标。";
  }

  if (missingCommand) {
    return "命令模板尚未齐备，不能提交。";
  }

  return undefined;
}

function gateStateLabelForBlock(blockReason: string, gateStateLabel: string): string {
  return gateStateLabel && gateStateLabel !== "可提交" ? gateStateLabel : blockReason.includes("缺少") ? "需要选择" : "暂不可提交";
}

export function buildCandidateCommandPreviewPlan(
  controls: CandidateComposerControls,
  state: CandidateComposerState
): CandidateCommandPreviewPlan {
  const sourceLabel = choiceLabelById(controls.sources, state.sourceId) ?? "未选择";
  const modeLabel = choiceLabelById(controls.modeChoices, state.mode);
  const destinationLabel = choiceLabelById(controls.destinationChoices, state.destinationId);
  const targetLabels = controls.targetGroups
    .map((group) => choiceLabelById(group.choices, state.targetIdsByGroup[group.key]))
    .filter((label): label is string => Boolean(label));
  const costLabels = uniqueStrings([
    ...controls.requiredOptionalCostIds
      .map((costId) => choiceLabelById(controls.optionalCostChoices, costId) ?? costId),
    ...state.optionalCostIds
      .map((costId) => choiceLabelById(controls.optionalCostChoices, costId) ?? costId)
  ]);

  return {
    costLabels,
    destinationLabel,
    modeLabel,
    sourceLabel,
    targetLabels
  };
}

export function uniqueStrings(values: string[]): string[] {
  return [...new Set(values.filter((value) => value.trim().length > 0))];
}

export function booleanFromRecord(record: Record<string, unknown>, key: string, fallback: boolean): boolean {
  const value = record[key];
  return typeof value === "boolean" ? value : fallback;
}

export function choiceLabel(choice: ActionPromptChoiceDto): string {
  return redactInternalText(choice.label || choice.id || "服务端选项");
}

export function choiceLabelById(choices: ActionPromptChoiceDto[], id: string | undefined): string | undefined {
  if (!id) {
    return undefined;
  }

  const choice = choices.find((candidate) => candidate.id === id);
  return choice ? choiceLabel(choice) : undefined;
}

export function safePromptSummary(value: unknown): string | undefined {
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

function sourceChoicesForCandidate(
  candidate: ActionPromptCandidateDto,
  model: CandidateComposerModel,
  forcedSourceObjectId: string | undefined
): ActionPromptChoiceDto[] {
  const requirementChoices = model.sourceRequirements
    .map((requirement) => {
      const id = stringMetadata(requirement, "sourceObjectId");
      if (!id) {
        return undefined;
      }

      return {
        id,
        label: firstStringFromRecord(requirement, ["displayName", "cardNo", "equipmentCardNo", "sourceObjectId"]) ?? id
      };
    })
    .filter((choice): choice is ActionPromptChoiceDto => Boolean(choice));
  const choices = uniqueChoices([...(candidate.sources ?? []), ...requirementChoices]);
  return forcedSourceObjectId
    ? choices.filter((choice) => choice.id === forcedSourceObjectId)
    : choices;
}

function modeChoiceForRequirement(requirement: Record<string, unknown> | undefined): ActionPromptChoiceDto[] {
  if (!requirement) {
    return [];
  }

  const mode = stringMetadata(requirement, "mode");
  if (!mode) {
    return [];
  }

  return [{ id: mode, label: stringMetadata(requirement, "modeLabel") ?? mode }];
}

function targetGroupsForRequirement(
  candidate: ActionPromptCandidateDto,
  requirement: Record<string, unknown> | undefined,
  minTargetCount: number,
  minDefenderCount: number
): ChoiceGroup[] {
  const indexedTargetGroups = choiceGroupsByIndex(
    requirement?.targetChoicesByIndex,
    candidate.action === "DECLARE_BATTLE" ? minDefenderCount : minTargetCount,
    candidate.action === "DECLARE_BATTLE" ? "防守方" : "目标"
  );
  if (indexedTargetGroups.length > 0) {
    return indexedTargetGroups;
  }

  const targetChoices = uniqueChoices([
    ...choiceArrayMetadata(requirement?.targetChoices),
    ...(candidate.targets ?? [])
  ]);
  if (targetChoices.length === 0) {
    return [];
  }

  return [{
    choices: targetChoices,
    key: "target-0",
    label: candidate.action === "DECLARE_BATTLE" ? "防守方 1" : "目标 1",
    required: candidate.action === "ASSEMBLE_EQUIPMENT"
      || candidate.action === "DECLARE_BATTLE"
      || minTargetCount > 0
      || minDefenderCount > 0
  }];
}

function choiceGroupsByIndex(value: unknown, requiredCount: number, labelPrefix: string): ChoiceGroup[] {
  if (!isRecord(value)) {
    return [];
  }

  return Object.entries(value)
    .map(([key, rawChoices]) => ({ key, choices: choiceArrayMetadata(rawChoices) }))
    .filter((group) => group.choices.length > 0)
    .sort((left, right) => numericKey(left.key) - numericKey(right.key))
    .map((group, index) => ({
      choices: group.choices,
      key: `${labelPrefix}-${group.key}`,
      label: `${labelPrefix} ${index + 1}`,
      required: index < requiredCount
    }));
}

function cardNoForRequirement(
  requirement: Record<string, unknown> | undefined,
  snapshot: SnapshotDto | undefined,
  sourceObjectId: string | undefined
): string | undefined {
  return (requirement && (stringMetadata(requirement, "cardNo") ?? stringMetadata(requirement, "equipmentCardNo")))
    ?? (sourceObjectId ? findCardNo(snapshot, sourceObjectId) : undefined);
}

function findCardNo(snapshot: SnapshotDto | undefined, objectId: string): string | undefined {
  return findObject(snapshot, objectId)?.cardNo ?? undefined;
}

function findObject(snapshot: SnapshotDto | undefined, objectId: string) {
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

function targetChoiceGroupCount(value: unknown): number {
  if (!isRecord(value)) {
    return 0;
  }

  return Object.values(value).filter((choices) => choiceArrayMetadata(choices).length > 0).length;
}

function firstRequirementSourceId(model: CandidateComposerModel): string | undefined {
  for (const requirement of model.sourceRequirements) {
    const sourceObjectId = stringMetadata(requirement, "sourceObjectId");
    if (sourceObjectId) {
      return sourceObjectId;
    }
  }
  return undefined;
}

function numericKey(value: string): number {
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) ? parsed : Number.MAX_SAFE_INTEGER;
}

function uniqueChoices(choices: ActionPromptChoiceDto[]): ActionPromptChoiceDto[] {
  const byId = new Map<string, ActionPromptChoiceDto>();
  for (const choice of choices) {
    if (!choice.id || byId.has(choice.id)) {
      continue;
    }
    byId.set(choice.id, {
      id: choice.id,
      label: choice.label || choice.id,
      objectIds: normalizedObjectIds(choice.objectIds),
      reason: choice.reason
    });
  }
  return [...byId.values()];
}

function choiceArrayMetadata(value: unknown): ActionPromptChoiceDto[] {
  const choices: ActionPromptChoiceDto[] = [];
  for (const record of recordArrayMetadata(value)) {
    const id = firstStringFromRecord(record, ["id", "triggerId", "choiceId"]);
    if (id) {
      choices.push({
        id,
        label: firstStringFromRecord(record, ["label", "summary", "visibleText", "text", "title", "name"]) ?? id,
        objectIds: stringArrayFromValue(record.objectIds),
        reason: firstStringFromRecord(record, ["reason", "description"])
      });
    }
  }

  return choices;
}

function normalizedObjectIds(objectIds: string[] | null | undefined): string[] | undefined {
  if (!Array.isArray(objectIds)) {
    return undefined;
  }

  const normalized = objectIds
    .map((objectId) => objectId.trim())
    .filter(Boolean);
  return normalized.length > 0 ? [...new Set(normalized)] : undefined;
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

function stringArrayFromValue(value: unknown, requireNonEmpty = false): string[] | undefined {
  if (!Array.isArray(value)) {
    return undefined;
  }

  const values = value.map((item) => typeof item === "string" ? item.trim() : "");
  return (!requireNonEmpty || values.length > 0) && values.every((item) => item.length > 0) ? values : undefined;
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

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

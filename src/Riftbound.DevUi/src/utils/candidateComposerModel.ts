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

export type CandidateComposerCheckState = "blocked" | "ready" | "waiting";

export type CandidateComposerCheckPlan = {
  key: string;
  label: string;
  reason: string;
  state: CandidateComposerCheckState;
  stateLabel: string;
};

export type CandidateComposerSubmissionPlan = {
  blockReason?: string;
  canSubmit: boolean;
  checkRows: CandidateComposerCheckPlan[];
  checkSummary: string;
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
    (candidate.selectionSteps ?? [])
      .map((step) => [
        step.role,
        step.required ? "required" : "optional",
        step.choices.map((choice) => choice.id).join(",")
      ].join(":"))
      .join("|"),
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
  const firstServerSourceId = selectionStepChoices(candidate, "source")[0]?.id;
  const fallbackSourceId = forcedSourceId
    ?? selectionDraft?.sourceObjectId
    ?? firstServerSourceId
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
  const modeChoices = selectionStepChoicesOrFallback(candidate, "mode", [
    ...(candidate.modes ?? []),
    ...modeChoiceForRequirement(requirement)
  ]);
  const destinationChoices = selectionStepChoicesOrFallback(candidate, "destination", [
    ...(candidate.destinations ?? []),
    ...choiceArrayMetadata(requirement?.destinationChoices),
    ...choiceArrayMetadata(requirement?.battlefieldChoices)
  ]);
  const targetGroups = targetGroupsForRequirement(candidate, requirement, minTargetCount, minDefenderCount);
  const requiredOptionalCostIds = uniqueStrings([
    ...(stringArrayFromValue(requirement?.requiredOptionalCosts) ?? [])
  ]);
  const optionalCostChoices = selectionStepChoicesOrFallback(candidate, "optionalCost", [
    ...(candidate.optionalCosts ?? []),
    ...choiceArrayMetadata(requirement?.optionalCostChoices),
    ...choiceArrayMetadata(requirement?.additionalCostChoices),
    ...choiceArrayMetadata(requirement?.paymentResourceChoices),
    ...requiredOptionalCostIds.map((id) => ({ id, label: id }))
  ]);

  return {
    destinationChoices,
    destinationRequired: destinationChoices.length > 0
      && roleRequiresSelection(candidate, "destination", true),
    modeChoices,
    optionalCostChoices,
    requiredOptionalCostIds,
    sourceRequired: roleRequiresSelection(candidate, "source", true),
    sources,
    targetGroups
  };
}

export function composerCommand(
  candidate: ActionPromptCandidateDto,
  state: CandidateComposerState,
  requirement: Record<string, unknown> | undefined,
  targetObjectIds: string[],
  optionalCostIds: string[]
): GameCommand | undefined {
  return commandFromActionPromptTemplate(
    candidate.commandTemplate,
    {
      destinationId: state.destinationId,
      mode: state.mode,
      optionalCostIds,
      sourceId: state.sourceId,
      targetObjectIds
    },
    { candidateMetadata: candidate.metadata, requirement }
  );
}

export function buildCandidateComposerSubmissionPlan({
  candidate,
  controls,
  disabledByConnection,
  requirement,
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
  const command = composerCommand(candidate, state, requirement, selectedTargetIds, optionalCostIds);
  const requirementUnsupportedReason = requirement && !booleanFromRecord(requirement, "composable", true)
    ? safePromptSummary(requirement.unsupportedReason) ?? "服务端暂未开放该候选的组合提交。"
    : undefined;
  const composerUnsupportedReason = candidate.composer && !candidate.composer.supported
    ? candidate.composer.reason
    : undefined;
  const unsupportedReason = requirementUnsupportedReason ?? composerUnsupportedReason;
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
  const checkRows = composerCheckRows({
    candidateEnabled: candidate.enabled,
    gateCanSubmit,
    gateReason,
    gateStateLabel,
    missingCommand,
    missingRequiredDestination,
    missingRequiredSource,
    missingRequiredTarget,
    unsupportedReason,
    supportReason: candidate.composer?.reason
  });

  return {
    blockReason,
    canSubmit,
    checkRows,
    checkSummary: composerCheckSummary(checkRows),
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

function composerCheckRows({
  candidateEnabled,
  gateCanSubmit,
  gateReason,
  gateStateLabel,
  missingCommand,
  missingRequiredDestination,
  missingRequiredSource,
  missingRequiredTarget,
  supportReason,
  unsupportedReason
}: {
  candidateEnabled: boolean;
  gateCanSubmit: boolean;
  gateReason: string;
  gateStateLabel: string;
  missingCommand: boolean;
  missingRequiredDestination: boolean;
  missingRequiredSource: boolean;
  missingRequiredTarget: boolean;
  supportReason?: string;
  unsupportedReason?: string;
}): CandidateComposerCheckPlan[] {
  return [
    {
      key: "server-candidate",
      label: "服务端候选",
      reason: candidateEnabled ? "候选仍由服务端标记为开放。" : "服务端当前阻断该候选。",
      state: candidateEnabled ? "ready" : "blocked",
      stateLabel: candidateEnabled ? "开放" : "阻断"
    },
    {
      key: "submission-gate",
      label: "提交门禁",
      reason: gateReason,
      state: gateCanSubmit ? "ready" : "blocked",
      stateLabel: gateStateLabel
    },
    {
      key: "source",
      label: "来源",
      reason: missingRequiredSource ? "缺少服务端候选要求的来源。" : "来源已由服务端候选或桌面点选确定。",
      state: missingRequiredSource ? "waiting" : "ready",
      stateLabel: missingRequiredSource ? "待选" : "已选"
    },
    {
      key: "destination",
      label: "位置",
      reason: missingRequiredDestination ? "缺少服务端候选要求的位置。" : "位置要求已满足，或该候选不需要位置。",
      state: missingRequiredDestination ? "waiting" : "ready",
      stateLabel: missingRequiredDestination ? "待选" : "齐备"
    },
    {
      key: "target",
      label: "目标",
      reason: missingRequiredTarget ? "缺少服务端候选要求的目标。" : "目标要求已满足，或该候选不需要目标。",
      state: missingRequiredTarget ? "waiting" : "ready",
      stateLabel: missingRequiredTarget ? "待选" : "齐备"
    },
    {
      key: "command",
      label: "命令模板",
      reason: missingCommand ? "命令模板尚未齐备，不能提交。" : "命令已由服务端候选和当前选择组装完成。",
      state: missingCommand ? "blocked" : "ready",
      stateLabel: missingCommand ? "缺失" : "齐备"
    },
    {
      key: "backend-support",
      label: "后端支持",
      reason: unsupportedReason ?? supportReason ?? "候选声明为可由当前前端组合，最终仍交给服务端校验。",
      state: unsupportedReason ? "blocked" : "ready",
      stateLabel: unsupportedReason ? "未开放" : "已开放"
    }
  ];
}

function composerCheckSummary(rows: CandidateComposerCheckPlan[]): string {
  const readyCount = rows.filter((row) => row.state === "ready").length;
  const blockedCount = rows.filter((row) => row.state === "blocked").length;
  const waitingCount = rows.filter((row) => row.state === "waiting").length;
  return `${readyCount} 通过 / ${blockedCount} 阻断 / ${waitingCount} 等待`;
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
  const choices = selectionStepChoicesOrFallback(candidate, "source", [
    ...(candidate.sources ?? []),
    ...requirementChoices
  ]);
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
  const requiredCount = candidate.action === "DECLARE_BATTLE" ? minDefenderCount : minTargetCount;
  const indexedTargetGroups = choiceGroupsByIndex(
    requirement?.targetChoicesByIndex,
    requiredCount,
    candidate.action === "DECLARE_BATTLE" ? "防守方" : "目标"
  );
  if (indexedTargetGroups.length > 0) {
    return indexedTargetGroups;
  }

  const selectionTargetGroups = targetGroupsFromSelectionSteps(
    candidate,
    requiredCount,
    candidate.action === "DECLARE_BATTLE" ? "防守方" : "目标"
  );
  if (selectionTargetGroups.length > 0) {
    return selectionTargetGroups;
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
    required: roleRequiresSelection(candidate, "target", candidate.action === "ASSEMBLE_EQUIPMENT"
      || candidate.action === "DECLARE_BATTLE"
      || minTargetCount > 0
      || minDefenderCount > 0)
  }];
}

function targetGroupsFromSelectionSteps(
  candidate: ActionPromptCandidateDto,
  requiredCount: number,
  labelPrefix: string
): ChoiceGroup[] {
  const targetSteps = selectionStepsForRole(candidate, "target");
  if (targetSteps.length === 0) {
    return [];
  }

  return targetSteps
    .map((step, index) => ({
      choices: uniqueChoices(step.choices.map(selectionChoiceToPromptChoice)),
      key: `target-step-${index}`,
      label: step.label || `${labelPrefix} ${index + 1}`,
      required: step.required
        || roleRequiresSelection(candidate, "target", false)
        || index < requiredCount
    }))
    .filter((group) => group.choices.length > 0 || group.required);
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

function selectionStepsForRole(
  candidate: ActionPromptCandidateDto,
  role: "source" | "target" | "destination" | "mode" | "optionalCost"
) {
  return (candidate.selectionSteps ?? []).filter((step) => step.role === role);
}

function selectionStepChoices(
  candidate: ActionPromptCandidateDto,
  role: "source" | "destination" | "mode" | "optionalCost"
): ActionPromptChoiceDto[] {
  return uniqueChoices(selectionStepsForRole(candidate, role)
    .flatMap((step) => step.choices.map(selectionChoiceToPromptChoice)));
}

function selectionStepChoicesOrFallback(
  candidate: ActionPromptCandidateDto,
  role: "source" | "destination" | "mode" | "optionalCost",
  fallbackChoices: ActionPromptChoiceDto[]
): ActionPromptChoiceDto[] {
  const stepChoices = selectionStepChoices(candidate, role);
  return stepChoices.length > 0 ? stepChoices : uniqueChoices(fallbackChoices);
}

function selectionChoiceToPromptChoice(choice: {
  id: string;
  label: string;
  objectIds?: string[] | null;
  reason?: string | null;
}): ActionPromptChoiceDto {
  return {
    id: choice.id,
    label: choice.label || choice.id,
    objectIds: normalizedObjectIds(choice.objectIds),
    reason: choice.reason ?? undefined
  };
}

function roleRequiresSelection(
  candidate: ActionPromptCandidateDto,
  role: "source" | "target" | "destination" | "mode" | "optionalCost",
  fallback: boolean
): boolean {
  const steps = selectionStepsForRole(candidate, role);
  if (steps.some((step) => step.required)) {
    return true;
  }

  return commandTemplateRequiresRole(candidate, role) || fallback;
}

function commandTemplateRequiresRole(
  candidate: ActionPromptCandidateDto,
  role: "source" | "target" | "destination" | "mode" | "optionalCost"
): boolean {
  const sources = commandBindingSourcesForRole(role);
  return (candidate.commandTemplate?.bindings ?? []).some((binding) =>
    Boolean(binding.required) && sources.includes(binding.source));
}

function commandBindingSourcesForRole(role: "source" | "target" | "destination" | "mode" | "optionalCost"): string[] {
  switch (role) {
    case "source":
      return ["selectedSource"];
    case "target":
      return ["selectedTarget", "selectedTargets"];
    case "destination":
      return ["selectedDestination"];
    case "mode":
      return ["selectedMode"];
    case "optionalCost":
      return ["selectedOptionalCosts"];
    default:
      return [];
  }
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

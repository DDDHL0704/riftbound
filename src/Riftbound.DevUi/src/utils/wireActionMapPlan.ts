import type { ActionPromptCandidateDto, ActionPromptContractDto, ActionPromptDto, CardObjectView, SnapshotDto } from "../types/protocol";
import type { CandidateSelectionDraft } from "./candidateSelectionDraft";
import {
  buildCandidateInteractionPlans,
  type CandidateInteractionPlan,
  type CandidateInteractionStepPlan
} from "./candidateInteractionPlan";
import { commandBindingDisplayLabel, commandBindingFieldKey } from "./commandFieldDisplay";
import { promptActionLabel, promptReasonLabel } from "./formatters";
import {
  buildPromptInteractionModel,
  promptChoiceSummaryObjectIds,
  promptChoiceRoleOrder,
  promptChoiceRoleLabel,
  promptCommandBindingLabel,
  promptCommandBindingSourceLabel,
  type PromptCandidateSummary,
  type PromptCommandBindingSummary,
  type PromptChoiceRole,
  type PromptInteractionModel
} from "./promptInteraction";

export type WireActionMapMetric = {
  key: string;
  label: string;
  value: string;
};

export type WireActionObjectEntry = {
  disabledCandidateCount: number;
  enabledCandidateCount: number;
  label: string;
  objectId: string;
  selected: boolean;
};

export type WireActionFocusCandidatePlan = {
  commandType?: string;
  enabled: boolean;
  key: string;
  label: string;
  nextObjectRefs: WireActionFocusObjectRef[];
  nextStepLabel: string;
  reason: string;
  roleLabels: string[];
  stateLabel: string;
};

export type WireActionFocusObjectRef = {
  key: string;
  label: string;
  objectId: string;
  roleLabel: string;
};

export type WireActionCandidateStepPlan = CandidateInteractionStepPlan & {
  objectRefs: WireActionFocusObjectRef[];
  progressLabel: string;
  selectedCount: number;
  selectedLabels: string[];
  selectionState: "inactive" | "selected" | "unselected";
};

export type WireActionCandidatePlan = Omit<CandidateInteractionPlan, "nextRequiredStep" | "stepRows"> & {
  draftActive: boolean;
  nextRequiredStep?: WireActionCandidateStepPlan;
  stepRows: WireActionCandidateStepPlan[];
};

export type WireActionRouteState = "blocked" | "incomplete" | "ready";

export type WireActionRouteStepPlan = {
  key: string;
  label: string;
  required: boolean;
  role: PromptChoiceRole;
  selectedCount: number;
  state: "open" | "selected";
  stateLabel: string;
  totalCount: number;
};

export type WireActionRoutePlan = {
  candidateLabel: string;
  commandType?: string;
  enabled: boolean;
  key: string;
  missingRequiredSelectionCount: number;
  nextStepLabel: string;
  selectedStepCount: number;
  state: WireActionRouteState;
  stateLabel: string;
  steps: WireActionRouteStepPlan[];
  summary: string;
};

export type WireActionFocusPlan = {
  candidateCount: number;
  disabledCandidateCount: number;
  enabledCandidateCount: number;
  label: string;
  objectId: string;
  relatedCandidates: WireActionFocusCandidatePlan[];
  roleLabels: string[];
  stateLabel: string;
};

export type WireActionRoleCount = {
  count: number;
  label: string;
  role: PromptChoiceRole;
};

export type WireActionGroupPlan = {
  action: string;
  enabled: boolean;
  enabledCount: number;
  key: string;
  label: string;
  reason: string;
  roleCounts: WireActionRoleCount[];
  totalCount: number;
};

export type WireActionContractPlan = {
  candidateAction: string;
  hiddenMetadataCount: number;
  legalChoicesCount: number;
  promptKind: string;
  requiredPayloadCount: number;
  visibleMetadataCount: number;
};

export type WireActionGrammarStepPlan = {
  count: number;
  key: string;
  label: string;
  required: boolean;
  role: PromptChoiceRole;
  sampleLabel: string;
};

export type WireActionCommandFieldPlan = {
  field: string;
  key: string;
  label: string;
  required: boolean;
  sourceLabel: string;
};

export type WireActionGrammarCandidatePlan = {
  commandFieldCount: number;
  commandFields: WireActionCommandFieldPlan[];
  commandType?: string;
  key: string;
  label: string;
  stepCount: number;
  steps: WireActionGrammarStepPlan[];
};

export type WireActionMapPlan = {
  canAct: boolean;
  blockedObjectEntries: WireActionObjectEntry[];
  blockedObjectEntryOverflowCount: number;
  candidatePlanTotalCount: number;
  candidatePlans: WireActionCandidatePlan[];
  contract?: WireActionContractPlan;
  disabledOnlyObjectCount: number;
  grammarCandidateTotalCount: number;
  grammarCandidates: WireActionGrammarCandidatePlan[];
  groupTotalCount: number;
  groups: WireActionGroupPlan[];
  focus?: WireActionFocusPlan;
  metrics: WireActionMapMetric[];
  objectEntries: WireActionObjectEntry[];
  objectEntryOverflowCount: number;
  route?: WireActionRoutePlan;
};

type BuildWireActionMapPlanOptions = {
  maxActionGroups?: number;
  maxCandidatePlans?: number;
  maxGrammarCandidates?: number;
  maxObjectEntries?: number;
  playerId: string;
  prompt?: ActionPromptDto;
  selectedObjectId?: string;
  selectionDraft?: CandidateSelectionDraft;
  snapshot?: SnapshotDto;
};

type ObjectIndex = Record<string, CardObjectView>;

type ActionGroup = {
  action: string;
  candidates: PromptCandidateSummary[];
  enabledCount: number;
};

export function buildWireActionMapPlan({
  maxActionGroups = 5,
  maxCandidatePlans = 5,
  maxGrammarCandidates = 4,
  maxObjectEntries = 6,
  playerId,
  prompt,
  selectedObjectId,
  selectionDraft,
  snapshot
}: BuildWireActionMapPlanOptions): WireActionMapPlan {
  const model = buildPromptInteractionModel(prompt);
  const objects = objectIndex(snapshot);
  const enabledCandidates = model.candidates.filter((candidate) => candidate.enabled);
  const enabledObjects = [...model.enabledObjectIds];
  const disabledOnlyObjects = [...model.disabledObjectIds];
  const knownEnabledObjects = enabledObjects.filter((objectId) => objects[objectId]);
  const knownDisabledOnlyObjects = disabledOnlyObjects.filter((objectId) => objects[objectId]);
  const groups = actionGroups(model);
  const candidateByKey = new Map(model.candidates.map((candidate) => [candidateKey(candidate), candidate]));
  const candidatePlans = buildCandidateInteractionPlans(model.candidates)
    .map((candidatePlan) => wireActionCandidatePlan(candidatePlan, candidateByKey.get(candidatePlan.key), objects, selectionDraft));
  const grammarCandidates = model.candidates.filter((candidate) => candidate.enabled);
  const objectLimit = nonNegativeLimit(maxObjectEntries);

  return {
    canAct: Boolean(prompt?.actionable && prompt.playerId === playerId),
    blockedObjectEntries: knownDisabledOnlyObjects
      .slice(0, objectLimit)
      .map((objectId) => objectEntryPlan(objectId, objects, model, selectedObjectId)),
    blockedObjectEntryOverflowCount: Math.max(knownDisabledOnlyObjects.length - objectLimit, 0),
    candidatePlanTotalCount: candidatePlans.length,
    candidatePlans: candidatePlans.slice(0, nonNegativeLimit(maxCandidatePlans)),
    contract: contractPlan(prompt?.contract),
    disabledOnlyObjectCount: knownDisabledOnlyObjects.length,
    grammarCandidateTotalCount: grammarCandidates.length,
    grammarCandidates: grammarCandidates
      .slice(0, nonNegativeLimit(maxGrammarCandidates))
      .map(grammarCandidatePlan),
    groupTotalCount: groups.length,
    groups: groups
      .slice(0, nonNegativeLimit(maxActionGroups))
      .map(actionGroupPlan),
    focus: focusPlan(selectedObjectId, objects, model),
    metrics: [
      { key: "enabled", label: "可提交", value: `${enabledCandidates.length}` },
      { key: "total", label: "全部候选", value: `${model.candidates.length}` },
      { key: "entry", label: "对象入口", value: `${knownEnabledObjects.length}` },
      { key: "blocked", label: "不可提交关联", value: `${knownDisabledOnlyObjects.length}` }
    ],
    objectEntries: knownEnabledObjects
      .slice(0, objectLimit)
      .map((objectId) => objectEntryPlan(objectId, objects, model, selectedObjectId)),
    objectEntryOverflowCount: Math.max(knownEnabledObjects.length - objectLimit, 0),
    route: wireActionRoutePlan(candidatePlans)
  };
}

function focusPlan(
  selectedObjectId: string | undefined,
  objects: ObjectIndex,
  model: PromptInteractionModel
): WireActionFocusPlan | undefined {
  if (!selectedObjectId) {
    return undefined;
  }

  const relatedCandidates = model.candidates
    .map((candidate) => focusCandidatePlan(candidate, selectedObjectId, objects))
    .filter((candidate): candidate is WireActionFocusCandidatePlan => Boolean(candidate))
    .sort(focusCandidateSort);
  const summary = model.objectById.get(selectedObjectId);
  const roleLabels = uniqueStrings(relatedCandidates.flatMap((candidate) => candidate.roleLabels));
  const enabledCandidateCount = summary?.enabledCandidateCount ?? relatedCandidates.filter((candidate) => candidate.enabled).length;
  const disabledCandidateCount = summary?.disabledCandidateCount ?? relatedCandidates.filter((candidate) => !candidate.enabled).length;

  return {
    candidateCount: relatedCandidates.length,
    disabledCandidateCount,
    enabledCandidateCount,
    label: objectLabel(selectedObjectId, objects),
    objectId: selectedObjectId,
    relatedCandidates: relatedCandidates.slice(0, 4),
    roleLabels,
    stateLabel: focusStateLabel(relatedCandidates.length, enabledCandidateCount)
  };
}

function wireActionCandidatePlan(
  candidatePlan: CandidateInteractionPlan,
  candidate: PromptCandidateSummary | undefined,
  objects: ObjectIndex,
  selectionDraft: CandidateSelectionDraft | undefined
): WireActionCandidatePlan {
  const draftActive = Boolean(candidate && selectionDraft?.candidateKey === candidateDraftKey(candidate));
  const stepRows = candidatePlan.stepRows.map((step) => ({
    ...step,
    objectRefs: candidate ? objectRefsForStep(candidate, step.role, objects) : [],
    ...stepSelectionPlan(candidate, step.role, selectionDraft, draftActive)
  }));

  return {
    ...candidatePlan,
    draftActive,
    nextRequiredStep: candidatePlan.nextRequiredStep
      ? stepRows.find((step) => step.key === candidatePlan.nextRequiredStep?.key)
      : undefined,
    stepRows
  };
}

function stepSelectionPlan(
  candidate: PromptCandidateSummary | undefined,
  role: PromptChoiceRole,
  selectionDraft: CandidateSelectionDraft | undefined,
  draftActive: boolean
): Pick<WireActionCandidateStepPlan, "progressLabel" | "selectedCount" | "selectedLabels" | "selectionState"> {
  if (!candidate || !selectionDraft || !draftActive) {
    return {
      progressLabel: "未进入当前草稿",
      selectedCount: 0,
      selectedLabels: [],
      selectionState: "inactive"
    };
  }

  const selectedChoices = candidate.choices.filter((choice) => choice.role === role && choiceSelectedForDraft(choice, role, selectionDraft));
  const selectedLabels = uniqueStrings(selectedChoices.map((choice) => choice.label)).slice(0, 3);
  const fallbackCount = selectedFallbackCount(role, selectionDraft);
  const selectedCount = selectedChoices.length > 0 ? selectedChoices.length : fallbackCount;

  return {
    progressLabel: selectedCount > 0 ? `已选 ${selectedCount}` : "未选",
    selectedCount,
    selectedLabels,
    selectionState: selectedCount > 0 ? "selected" : "unselected"
  };
}

function choiceSelectedForDraft(
  choice: PromptCandidateSummary["choices"][number],
  role: PromptChoiceRole,
  selectionDraft: CandidateSelectionDraft
): boolean {
  switch (role) {
    case "source":
      return choiceMatchesSelection(choice, selectionDraft.sourceObjectId);
    case "target":
      return selectionDraft.targetChoiceIds.some((choiceId) => choiceMatchesSelection(choice, choiceId));
    case "destination":
      return choiceMatchesSelection(choice, selectionDraft.destinationId);
    case "mode":
      return choiceMatchesSelection(choice, selectionDraft.mode);
    case "optionalCost":
      return selectionDraft.optionalCostIds.some((choiceId) => choiceMatchesSelection(choice, choiceId));
  }
}

function selectedFallbackCount(role: PromptChoiceRole, selectionDraft: CandidateSelectionDraft): number {
  switch (role) {
    case "source":
      return selectionDraft.sourceObjectId ? 1 : 0;
    case "target":
      return selectionDraft.targetChoiceIds.length;
    case "destination":
      return selectionDraft.destinationId ? 1 : 0;
    case "mode":
      return selectionDraft.mode ? 1 : 0;
    case "optionalCost":
      return selectionDraft.optionalCostIds.length;
  }
}

function choiceMatchesSelection(choice: PromptCandidateSummary["choices"][number], selectedId: string | undefined): boolean {
  if (!selectedId) {
    return false;
  }

  return choice.id === selectedId || promptChoiceSummaryObjectIds(choice).includes(selectedId);
}

function wireActionRoutePlan(candidatePlans: WireActionCandidatePlan[]): WireActionRoutePlan | undefined {
  const candidatePlan = candidatePlans.find((candidate) => candidate.draftActive);
  if (!candidatePlan) {
    return undefined;
  }

  const missingRequiredSelectionCount = candidatePlan.stepRows.filter((step) => step.required && step.selectedCount <= 0).length;
  const selectedStepCount = candidatePlan.stepRows.filter((step) => step.selectedCount > 0).length;
  const nextStep = candidatePlan.stepRows.find((step) => step.required && step.selectedCount <= 0)
    ?? candidatePlan.stepRows.find((step) => !step.required && step.count > 0 && step.selectedCount <= 0);
  const state: WireActionRouteState = !candidatePlan.enabled
    ? "blocked"
    : missingRequiredSelectionCount > 0
      ? "incomplete"
      : "ready";
  const stateLabel = routeStateLabel(state);
  const nextStepLabel = nextStep ? `继续选择${nextStep.label}` : "可送服务端校验";

  return {
    candidateLabel: candidatePlan.candidateLabel,
    commandType: candidatePlan.commandType,
    enabled: candidatePlan.enabled,
    key: candidatePlan.key,
    missingRequiredSelectionCount,
    nextStepLabel,
    selectedStepCount,
    state,
    stateLabel,
    steps: candidatePlan.stepRows.map((step) => ({
      key: step.key,
      label: step.label,
      required: step.required,
      role: step.role,
      selectedCount: step.selectedCount,
      state: step.selectedCount > 0 ? "selected" : "open",
      stateLabel: step.selectedCount > 0 ? "已选" : step.required ? "待选" : "可选",
      totalCount: step.count
    })),
    summary: `${candidatePlan.candidateLabel} / ${stateLabel} / ${nextStepLabel}`
  };
}

function routeStateLabel(state: WireActionRouteState): string {
  switch (state) {
    case "blocked":
      return "服务端阻断";
    case "incomplete":
      return "缺少必需选择";
    case "ready":
      return "可送服务端校验";
  }
}

function focusCandidatePlan(
  candidate: PromptCandidateSummary,
  objectId: string,
  objects: ObjectIndex
): WireActionFocusCandidatePlan | undefined {
  const roleLabels = uniqueStrings(candidate.choices
    .filter((choice) => promptChoiceSummaryObjectIds(choice).includes(objectId))
    .map((choice) => promptChoiceRoleLabel(choice.role)));
  if (roleLabels.length === 0) {
    return undefined;
  }

  const nextStep = nextStepForFocusedCandidate(candidate, roleLabels);
  return {
    commandType: candidate.command?.cmdType ?? candidate.action,
    enabled: candidate.enabled,
    key: `${candidate.action}:${candidate.label}:${objectId}`,
    label: candidate.label,
    nextObjectRefs: nextStep ? objectRefsForStep(candidate, nextStep.role, objects) : [],
    nextStepLabel: nextStepLabelForFocusedCandidate(candidate, nextStep),
    reason: candidate.reason,
    roleLabels,
    stateLabel: candidate.enabled ? "可提交" : "暂不可提交"
  };
}

function nextStepForFocusedCandidate(
  candidate: PromptCandidateSummary,
  selectedRoleLabels: string[]
): PromptCandidateSummary["steps"][number] | undefined {
  return candidate.steps.find((step) =>
    step.required && !selectedRoleLabels.includes(promptChoiceRoleLabel(step.role)))
    ?? candidate.steps.find((step) =>
      step.count > 0 && !selectedRoleLabels.includes(promptChoiceRoleLabel(step.role)));
}

function nextStepLabelForFocusedCandidate(
  candidate: PromptCandidateSummary,
  nextStep: PromptCandidateSummary["steps"][number] | undefined
): string {
  if (nextStep) {
    return nextStep.required ? `需要${nextStep.label}` : `可选${nextStep.label}`;
  }

  return candidate.enabled ? "可提交给服务端" : "等待服务端窗口";
}

function objectRefsForStep(
  candidate: PromptCandidateSummary,
  role: PromptChoiceRole,
  objects: ObjectIndex
): WireActionFocusObjectRef[] {
  const refs: WireActionFocusObjectRef[] = [];
  const seen = new Set<string>();
  for (const choice of candidate.choices.filter((item) => item.role === role)) {
    const objectId = promptChoiceSummaryObjectIds(choice).find((id) => objects[id]);
    if (!objectId || seen.has(objectId)) {
      continue;
    }

    seen.add(objectId);
    refs.push({
      key: `${candidate.action}:${role}:${choice.id}:${objectId}`,
      label: choice.label || objectLabel(objectId, objects),
      objectId,
      roleLabel: promptChoiceRoleLabel(role)
    });
  }

  return refs.slice(0, 4);
}

function candidateKey(candidate: PromptCandidateSummary): string {
  return `${candidate.action}:${candidate.label}`;
}

function candidateDraftKey(candidate: PromptCandidateSummary): string {
  return `${candidate.action}::${candidate.label}`;
}

function focusStateLabel(candidateCount: number, enabledCount: number): string {
  if (candidateCount === 0) {
    return "焦点对象不在服务端候选中";
  }

  if (enabledCount > 0) {
    return `${enabledCount} 个可提交候选`;
  }

  return "仅有关联但当前阻断";
}

function focusCandidateSort(left: WireActionFocusCandidatePlan, right: WireActionFocusCandidatePlan): number {
  if (left.enabled !== right.enabled) {
    return left.enabled ? -1 : 1;
  }

  return left.label.localeCompare(right.label, "zh-Hans-CN");
}

function objectEntryPlan(
  objectId: string,
  objects: ObjectIndex,
  model: PromptInteractionModel,
  selectedObjectId: string | undefined
): WireActionObjectEntry {
  const summary = model.objectById.get(objectId);
  return {
    disabledCandidateCount: summary?.disabledCandidateCount ?? 0,
    enabledCandidateCount: summary?.enabledCandidateCount ?? 0,
    label: objectLabel(objectId, objects),
    objectId,
    selected: selectedObjectId === objectId
  };
}

function actionGroupPlan(group: ActionGroup): WireActionGroupPlan {
  return {
    action: group.action,
    enabled: group.enabledCount > 0,
    enabledCount: group.enabledCount,
    key: group.action,
    label: actionGroupLabel(group.action, group.candidates),
    reason: groupReason(group.candidates),
    roleCounts: promptChoiceRoleOrder.map((role) => ({
      count: roleCount(group.candidates, role),
      label: promptChoiceRoleLabel(role),
      role
    })),
    totalCount: group.candidates.length
  };
}

function grammarCandidatePlan(candidate: PromptCandidateSummary): WireActionGrammarCandidatePlan {
  const commandFields = candidate.command?.bindings.map((binding, index) =>
    commandFieldPlan(candidate, binding, index)) ?? [];

  return {
    commandFieldCount: commandFields.length,
    commandFields,
    commandType: candidate.command?.cmdType,
    key: `${candidate.action}:${candidate.label}`,
    label: candidate.label,
    stepCount: candidate.steps.length,
    steps: candidate.steps.map((step) => ({
      count: step.count,
      key: `${candidate.action}:${step.role}:${step.label}`,
      label: step.label,
      required: step.required,
      role: step.role,
      sampleLabel: step.sampleLabels.length > 0
        ? step.sampleLabels.join(" / ")
        : "由服务端候选决定"
    }))
  };
}

function commandFieldPlan(
  candidate: PromptCandidateSummary,
  binding: PromptCommandBindingSummary,
  index: number
): WireActionCommandFieldPlan {
  return {
    field: commandBindingFieldKey(binding, index),
    key: `${candidate.action}:${commandBindingFieldKey(binding, index)}:${binding.source}:${index}`,
    label: commandBindingDisplayLabel(binding, promptCommandBindingLabel(binding)),
    required: binding.required,
    sourceLabel: promptCommandBindingSourceLabel(binding)
  };
}

function contractPlan(contract?: ActionPromptContractDto | null): WireActionContractPlan | undefined {
  if (!contract) {
    return undefined;
  }

  return {
    candidateAction: contract.candidateAction,
    hiddenMetadataCount: contract.hiddenMetadata.length,
    legalChoicesCount: contract.legalChoices.length,
    promptKind: contract.promptKind,
    requiredPayloadCount: contract.requiredPayload.length,
    visibleMetadataCount: contract.visibleMetadata.length
  };
}

function actionGroups(model: PromptInteractionModel): ActionGroup[] {
  const byAction = new Map<string, PromptCandidateSummary[]>();
  for (const candidate of model.candidates) {
    byAction.set(candidate.action, [...(byAction.get(candidate.action) ?? []), candidate]);
  }

  return [...byAction.entries()]
    .map(([action, candidates]) => ({
      action,
      candidates,
      enabledCount: candidates.filter((candidate) => candidate.enabled).length
    }))
    .sort((left, right) =>
      right.enabledCount - left.enabledCount
      || right.candidates.length - left.candidates.length
      || left.action.localeCompare(right.action));
}

function actionGroupLabel(action: string, candidates: PromptCandidateSummary[]): string {
  const source = {
    action,
    enabled: true,
    label: candidates[0]?.label ?? action,
    reason: ""
  } satisfies ActionPromptCandidateDto;
  return promptActionLabel(source);
}

function roleCount(candidates: PromptCandidateSummary[], role: PromptChoiceRole): number {
  return new Set(candidates.flatMap((candidate) =>
    candidate.choices
      .filter((choice) => choice.role === role)
      .map((choice) => choice.id))).size;
}

function groupReason(candidates: PromptCandidateSummary[]): string {
  const enabled = candidates.find((candidate) => candidate.enabled);
  if (enabled) {
    return enabled.reason;
  }

  return candidates[0]?.reason ?? "服务端未提供候选原因。";
}

function objectIndex(snapshot?: SnapshotDto): ObjectIndex {
  return Object.values(snapshot?.players ?? {}).reduce<ObjectIndex>((index, player) => {
    for (const [objectId, object] of Object.entries(player.objects ?? {})) {
      index[object.objectId ?? objectId] = object;
    }
    return index;
  }, {});
}

function objectLabel(objectId: string, objects: ObjectIndex): string {
  if (objectId === "HIDDEN") {
    return "隐藏对象";
  }

  const object = objects[objectId];
  return object?.cardNo ?? safeChoiceId(objectId);
}

function safeChoiceId(value: string): string {
  return /^[A-Z0-9_:-]+$/.test(value) || /^[a-z0-9]+(?:[-_:][a-z0-9]+)+$/.test(value)
    ? "服务端对象"
    : promptReasonLabel(value, "服务端对象");
}

function nonNegativeLimit(value: number): number {
  return Number.isFinite(value) ? Math.max(0, Math.floor(value)) : 0;
}

function uniqueStrings(values: string[]): string[] {
  return [...new Set(values.map((value) => value.trim()).filter(Boolean))];
}

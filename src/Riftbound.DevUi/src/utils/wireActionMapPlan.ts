import type { ActionPromptCandidateDto, ActionPromptContractDto, ActionPromptDto, CardObjectView, SnapshotDto } from "../types/protocol";
import type { CandidateSelectionDraft } from "./candidateSelectionDraft";
import type { ServerSubmissionGatePlan } from "./serverSubmissionGatePlan";
import {
  buildWireActionSubmissionGatePlan,
  buildWireActionWindowGatePlan,
  type WireActionSubmissionGatePlan,
  type WireActionWindowGatePlan
} from "./wireActionGates";
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
  commandFields: WireActionRouteFieldPlan[];
  draftActive: boolean;
  nextRequiredStep?: WireActionCandidateStepPlan;
  stepRows: WireActionCandidateStepPlan[];
};

export type WireActionRouteState = "blocked" | "incomplete" | "ready";

export type WireActionRouteFieldState = "covered" | "missing" | "optional" | "server";

export type WireActionRouteCheckState = "blocked" | "ready" | "waiting";

export type WireActionRouteCheckPlan = {
  key: string;
  label: string;
  reason: string;
  state: WireActionRouteCheckState;
  stateLabel: string;
};

export type WireActionRouteFieldPlan = WireActionCommandFieldPlan & {
  role?: PromptChoiceRole;
  state: WireActionRouteFieldState;
  stateLabel: string;
};

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
  checkRows: WireActionRouteCheckPlan[];
  checkSummary: string;
  commandType?: string;
  enabled: boolean;
  fields: WireActionRouteFieldPlan[];
  key: string;
  missingRequiredFieldCount: number;
  missingRequiredSelectionCount: number;
  nextStepLabel: string;
  selectedStepCount: number;
  serverInjectedFieldCount: number;
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

export type WireActionCoverageState = "blocked" | "empty" | "ready" | "warning";

export type WireActionCoverageMetricPlan = {
  key: string;
  label: string;
  state: WireActionCoverageState;
  value: string;
};

export type WireActionRoleCoveragePlan = {
  candidateCount: number;
  choiceCount: number;
  emptyRequiredCount: number;
  hiddenChoiceCount: number;
  key: string;
  label: string;
  objectCount: number;
  requiredCandidateCount: number;
  role: PromptChoiceRole;
  sampleLabel: string;
  state: WireActionCoverageState;
  summary: string;
  unknownObjectCount: number;
};

export type WireActionBlockerPlan = {
  actions: string[];
  count: number;
  key: string;
  reason: string;
};

export type WireActionCoveragePlan = {
  blockers: WireActionBlockerPlan[];
  commandRows: WireActionCoverageMetricPlan[];
  hiddenBoundaryLabel: string;
  metrics: WireActionCoverageMetricPlan[];
  roles: WireActionRoleCoveragePlan[];
  state: WireActionCoverageState;
  stateLabel: string;
  summary: string;
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
  coverage: WireActionCoveragePlan;
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
  submissionGate: WireActionSubmissionGatePlan;
  windowGate: WireActionWindowGatePlan;
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
  submissionGate?: ServerSubmissionGatePlan;
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
  snapshot,
  submissionGate
}: BuildWireActionMapPlanOptions): WireActionMapPlan {
  const model = buildPromptInteractionModel(prompt);
  const objects = objectIndex(snapshot);
  const gate = buildWireActionSubmissionGatePlan(submissionGate);
  const windowGate = buildWireActionWindowGatePlan({ playerId, prompt });
  const canAct = Boolean(gate.canSubmit && windowGate.canAct);
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
    canAct,
    blockedObjectEntries: knownDisabledOnlyObjects
      .slice(0, objectLimit)
      .map((objectId) => objectEntryPlan(objectId, objects, model, selectedObjectId)),
    blockedObjectEntryOverflowCount: Math.max(knownDisabledOnlyObjects.length - objectLimit, 0),
    candidatePlanTotalCount: candidatePlans.length,
    candidatePlans: candidatePlans.slice(0, nonNegativeLimit(maxCandidatePlans)),
    contract: contractPlan(prompt?.contract),
    coverage: coveragePlan(model, objects, gate, windowGate),
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
      { key: "gate", label: "提交门禁", value: gate.stateLabel },
      { key: "window", label: "行动窗口", value: windowGate.stateLabel },
      { key: "enabled", label: "可提交", value: `${enabledCandidates.length}` },
      { key: "total", label: "全部候选", value: `${model.candidates.length}` },
      { key: "entry", label: "对象入口", value: `${knownEnabledObjects.length}` },
      { key: "blocked", label: "不可提交关联", value: `${knownDisabledOnlyObjects.length}` }
    ],
    objectEntries: knownEnabledObjects
      .slice(0, objectLimit)
      .map((objectId) => objectEntryPlan(objectId, objects, model, selectedObjectId)),
    objectEntryOverflowCount: Math.max(knownEnabledObjects.length - objectLimit, 0),
    route: wireActionRoutePlan(candidatePlans, gate, windowGate),
    submissionGate: gate,
    windowGate
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
    commandFields: candidate?.command?.bindings.map((binding, index) =>
      routeFieldPlan(candidate, binding, index, stepRows)) ?? [],
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

function wireActionRoutePlan(
  candidatePlans: WireActionCandidatePlan[],
  submissionGate: WireActionSubmissionGatePlan,
  windowGate: WireActionWindowGatePlan
): WireActionRoutePlan | undefined {
  const candidatePlan = candidatePlans.find((candidate) => candidate.draftActive);
  if (!candidatePlan) {
    return undefined;
  }

  const missingRequiredSelectionCount = candidatePlan.stepRows.filter((step) => step.required && step.selectedCount <= 0).length;
  const missingRequiredFieldCount = candidatePlan.commandFields.filter((field) => field.state === "missing").length;
  const serverInjectedFieldCount = candidatePlan.commandFields.filter((field) => field.state === "server").length;
  const selectedStepCount = candidatePlan.stepRows.filter((step) => step.selectedCount > 0).length;
  const nextStep = candidatePlan.stepRows.find((step) => step.required && step.selectedCount <= 0)
    ?? candidatePlan.stepRows.find((step) => !step.required && step.count > 0 && step.selectedCount <= 0);
  const state: WireActionRouteState = !candidatePlan.enabled || !submissionGate.canSubmit || !windowGate.canAct
    ? "blocked"
    : missingRequiredSelectionCount > 0 || missingRequiredFieldCount > 0
      ? "incomplete"
      : "ready";
  const stateLabel = !submissionGate.canSubmit
    ? "提交门禁阻断"
    : !windowGate.canAct
      ? "行动窗口阻断"
      : routeStateLabel(state);
  const nextMissingField = candidatePlan.commandFields.find((field) => field.state === "missing");
  const nextStepLabel = !submissionGate.canSubmit
    ? submissionGate.reason
    : !windowGate.canAct
      ? windowGate.reason
    : nextStep
    ? `继续选择${nextStep.label}`
    : nextMissingField
      ? `补齐字段${nextMissingField.label}`
      : "可送服务端校验";
  const checkRows = wireActionRouteCheckRows({
    candidateEnabled: candidatePlan.enabled,
    missingRequiredFieldCount,
    missingRequiredSelectionCount,
    serverInjectedFieldCount,
    submissionGate,
    windowGate
  });

  return {
    candidateLabel: candidatePlan.candidateLabel,
    checkRows,
    checkSummary: routeCheckSummary(checkRows),
    commandType: candidatePlan.commandType,
    enabled: candidatePlan.enabled,
    fields: candidatePlan.commandFields,
    key: candidatePlan.key,
    missingRequiredFieldCount,
    missingRequiredSelectionCount,
    nextStepLabel,
    selectedStepCount,
    serverInjectedFieldCount,
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

function wireActionRouteCheckRows({
  candidateEnabled,
  missingRequiredFieldCount,
  missingRequiredSelectionCount,
  serverInjectedFieldCount,
  submissionGate,
  windowGate
}: {
  candidateEnabled: boolean;
  missingRequiredFieldCount: number;
  missingRequiredSelectionCount: number;
  serverInjectedFieldCount: number;
  submissionGate: WireActionSubmissionGatePlan;
  windowGate: WireActionWindowGatePlan;
}): WireActionRouteCheckPlan[] {
  return [
    {
      key: "server-candidate",
      label: "服务端候选",
      reason: candidateEnabled ? "候选仍由服务端标记为可提交。" : "服务端当前阻断该候选。",
      state: candidateEnabled ? "ready" : "blocked",
      stateLabel: candidateEnabled ? "开放" : "阻断"
    },
    {
      key: "submission-gate",
      label: "快照门禁",
      reason: submissionGate.reason,
      state: submissionGate.canSubmit ? "ready" : "blocked",
      stateLabel: submissionGate.stateLabel
    },
    {
      key: "action-window",
      label: "行动窗口",
      reason: windowGate.reason,
      state: windowGate.canAct ? "ready" : "blocked",
      stateLabel: windowGate.stateLabel
    },
    {
      key: "required-selections",
      label: "必选步骤",
      reason: missingRequiredSelectionCount > 0
        ? `仍缺少 ${missingRequiredSelectionCount} 个必选步骤。`
        : "来源、目标、位置等必选步骤已覆盖。",
      state: missingRequiredSelectionCount > 0 ? "waiting" : "ready",
      stateLabel: missingRequiredSelectionCount > 0 ? `缺少 ${missingRequiredSelectionCount}` : "齐备"
    },
    {
      key: "command-fields",
      label: "命令字段",
      reason: missingRequiredFieldCount > 0
        ? `仍缺少 ${missingRequiredFieldCount} 个服务端命令字段。`
        : "前端草稿已覆盖所有可选择命令字段。",
      state: missingRequiredFieldCount > 0 ? "blocked" : "ready",
      stateLabel: missingRequiredFieldCount > 0 ? `缺少 ${missingRequiredFieldCount}` : "齐备"
    },
    {
      key: "server-injected",
      label: "服务端注入",
      reason: serverInjectedFieldCount > 0
        ? `${serverInjectedFieldCount} 个字段由服务端注入，前端只显示不伪造。`
        : "当前候选没有额外服务端注入字段。",
      state: "ready",
      stateLabel: serverInjectedFieldCount > 0 ? `${serverInjectedFieldCount} 项` : "无"
    }
  ];
}

function coveragePlan(
  model: PromptInteractionModel,
  objects: ObjectIndex,
  submissionGate: WireActionSubmissionGatePlan,
  windowGate: WireActionWindowGatePlan
): WireActionCoveragePlan {
  const enabledCount = model.candidates.filter((candidate) => candidate.enabled).length;
  const disabledCount = model.candidates.length - enabledCount;
  const roles = promptChoiceRoleOrder.map((role) => roleCoveragePlan(role, model.candidates, objects));
  const emptyRequiredCount = roles.reduce((total, role) => total + role.emptyRequiredCount, 0);
  const templateCount = model.candidates.filter((candidate) => candidate.command).length;
  const composerCount = model.candidates.filter((candidate) => candidate.composer?.supported).length;
  const requiredFieldCount = model.candidates.reduce((total, candidate) =>
    total + (candidate.command?.bindings.filter((binding) => binding.required).length ?? 0), 0);
  const serverInjectedFieldCount = model.candidates.reduce((total, candidate) =>
    total + (candidate.command?.bindings.filter((binding) => binding.source === "requirementMetadata").length ?? 0), 0);
  const missingTemplateCount = model.candidates.filter((candidate) => !candidate.command).length;
  const state = coverageStateFor({
    disabledCount,
    emptyRequiredCount,
    enabledCount,
    missingTemplateCount,
    submissionGate,
    totalCount: model.candidates.length,
    windowGate
  });

  return {
    blockers: blockerRowsFor(model.candidates),
    commandRows: [
      coverageMetric("template", "命令模板", `${templateCount}/${model.candidates.length}`, missingTemplateCount > 0 ? "warning" : templateCount > 0 ? "ready" : "empty"),
      coverageMetric("composer", "组合声明", `${composerCount}/${model.candidates.length}`, composerCount > 0 ? "ready" : "empty"),
      coverageMetric("required-fields", "必填字段", `${requiredFieldCount}`, requiredFieldCount > 0 ? "ready" : "empty"),
      coverageMetric("server-fields", "服务端字段", `${serverInjectedFieldCount}`, serverInjectedFieldCount > 0 ? "warning" : "empty")
    ],
    hiddenBoundaryLabel: "只显示服务端公开的选择角色、对象引用与字段计数；隐藏 metadata 和隐藏区身份不在前端展开。",
    metrics: [
      coverageMetric("candidates", "候选覆盖", `${enabledCount} 可 / ${disabledCount} 阻`, model.candidates.length > 0 ? "ready" : "empty"),
      coverageMetric("gate", "提交门禁", submissionGate.stateLabel, submissionGate.canSubmit ? "ready" : "blocked"),
      coverageMetric("window", "行动窗口", windowGate.stateLabel, windowGate.canAct ? "ready" : "blocked"),
      coverageMetric("required-empty", "空必选角色", `${emptyRequiredCount}`, emptyRequiredCount > 0 ? "warning" : "ready")
    ],
    roles,
    state,
    stateLabel: coverageStateLabel(state),
    summary: coverageSummaryFor({
      disabledCount,
      emptyRequiredCount,
      enabledCount,
      missingTemplateCount,
      state
    })
  };
}

function roleCoveragePlan(
  role: PromptChoiceRole,
  candidates: PromptCandidateSummary[],
  objects: ObjectIndex
): WireActionRoleCoveragePlan {
  const choiceIds = new Set<string>();
  const objectIds = new Set<string>();
  const unknownObjectIds = new Set<string>();
  const sampleLabels: string[] = [];
  let candidateCount = 0;
  let emptyRequiredCount = 0;
  let hiddenChoiceCount = 0;
  let requiredCandidateCount = 0;

  for (const candidate of candidates) {
    const roleChoices = candidate.choices.filter((choice) => choice.role === role);
    const step = candidate.steps.find((candidateStep) => candidateStep.role === role);
    if (!step && roleChoices.length === 0) {
      continue;
    }

    candidateCount += 1;
    if (step?.required) {
      requiredCandidateCount += 1;
    }

    if (step?.required && roleChoices.length === 0) {
      emptyRequiredCount += 1;
    }

    for (const label of step?.sampleLabels ?? roleChoices.map((choice) => choice.label)) {
      if (sampleLabels.length < 3 && label.trim()) {
        sampleLabels.push(label);
      }
    }

    for (const choice of roleChoices) {
      choiceIds.add(choice.id);
      const refs = promptChoiceSummaryObjectIds(choice);
      if (refs.includes("HIDDEN")) {
        hiddenChoiceCount += 1;
      }

      for (const objectId of refs) {
        if (objectId === "HIDDEN") {
          continue;
        }

        if (objects[objectId]) {
          objectIds.add(objectId);
        } else {
          unknownObjectIds.add(objectId);
        }
      }
    }
  }

  const state = emptyRequiredCount > 0
    ? "warning"
    : candidateCount === 0
      ? "empty"
      : requiredCandidateCount > 0
        ? "ready"
        : "warning";

  return {
    candidateCount,
    choiceCount: choiceIds.size,
    emptyRequiredCount,
    hiddenChoiceCount,
    key: role,
    label: promptChoiceRoleLabel(role),
    objectCount: objectIds.size,
    requiredCandidateCount,
    role,
    sampleLabel: uniqueStrings(sampleLabels).join(" / ") || "无公开样例",
    state,
    summary: `${candidateCount} 候选 / ${requiredCandidateCount} 必选 / ${choiceIds.size} 选项 / ${objectIds.size} 对象`,
    unknownObjectCount: unknownObjectIds.size
  };
}

function coverageStateFor({
  disabledCount,
  emptyRequiredCount,
  enabledCount,
  missingTemplateCount,
  submissionGate,
  totalCount,
  windowGate
}: {
  disabledCount: number;
  emptyRequiredCount: number;
  enabledCount: number;
  missingTemplateCount: number;
  submissionGate: WireActionSubmissionGatePlan;
  totalCount: number;
  windowGate: WireActionWindowGatePlan;
}): WireActionCoverageState {
  if (totalCount === 0) {
    return "empty";
  }

  if (!submissionGate.canSubmit || !windowGate.canAct) {
    return "blocked";
  }

  if (enabledCount === 0 || emptyRequiredCount > 0 || missingTemplateCount > 0 || disabledCount > 0) {
    return "warning";
  }

  return "ready";
}

function coverageStateLabel(state: WireActionCoverageState): string {
  switch (state) {
    case "blocked":
      return "门禁阻断";
    case "empty":
      return "等待候选";
    case "ready":
      return "覆盖齐备";
    case "warning":
      return "需要关注";
  }
}

function coverageSummaryFor({
  disabledCount,
  emptyRequiredCount,
  enabledCount,
  missingTemplateCount,
  state
}: {
  disabledCount: number;
  emptyRequiredCount: number;
  enabledCount: number;
  missingTemplateCount: number;
  state: WireActionCoverageState;
}): string {
  if (state === "empty") {
    return "当前窗口没有服务端候选，前端保持只读。";
  }

  return `${enabledCount} 个可提交候选；${disabledCount} 个服务端阻断；${emptyRequiredCount} 个必选角色暂无公开选项；${missingTemplateCount} 个候选未公开命令模板。`;
}

function coverageMetric(
  key: string,
  label: string,
  value: string,
  state: WireActionCoverageState
): WireActionCoverageMetricPlan {
  return { key, label, state, value };
}

function blockerRowsFor(candidates: PromptCandidateSummary[]): WireActionBlockerPlan[] {
  const byReason = new Map<string, PromptCandidateSummary[]>();
  for (const candidate of candidates.filter((item) => !item.enabled)) {
    const reason = candidate.reason || "服务端阻断";
    byReason.set(reason, [...(byReason.get(reason) ?? []), candidate]);
  }

  return [...byReason.entries()]
    .map(([reason, rows], index) => ({
      actions: uniqueStrings(rows.map((row) => row.label)).slice(0, 3),
      count: rows.length,
      key: `blocker-${index}`,
      reason
    }))
    .sort((left, right) => right.count - left.count || left.reason.localeCompare(right.reason, "zh-Hans-CN"))
    .slice(0, 4);
}

function routeCheckSummary(rows: WireActionRouteCheckPlan[]): string {
  const readyCount = rows.filter((row) => row.state === "ready").length;
  const blockedCount = rows.filter((row) => row.state === "blocked").length;
  const waitingCount = rows.filter((row) => row.state === "waiting").length;
  return `${readyCount} 通过 / ${blockedCount} 阻断 / ${waitingCount} 等待`;
}

function routeFieldPlan(
  candidate: PromptCandidateSummary,
  binding: PromptCommandBindingSummary,
  index: number,
  stepRows: WireActionCandidateStepPlan[]
): WireActionRouteFieldPlan {
  const base = commandFieldPlan(candidate, binding, index);
  const state = routeFieldState(binding, stepRows);

  return {
    ...base,
    role: binding.role,
    state,
    stateLabel: routeFieldStateLabel(state)
  };
}

function routeFieldState(
  binding: PromptCommandBindingSummary,
  stepRows: WireActionCandidateStepPlan[]
): WireActionRouteFieldState {
  if (binding.source === "requirementMetadata") {
    return "server";
  }

  const selectedCount = binding.role
    ? stepRows.find((step) => step.role === binding.role)?.selectedCount ?? 0
    : 0;
  if (selectedCount > 0) {
    return "covered";
  }

  return binding.required ? "missing" : "optional";
}

function routeFieldStateLabel(state: WireActionRouteFieldState): string {
  switch (state) {
    case "covered":
      return "已覆盖";
    case "missing":
      return "缺少选择";
    case "optional":
      return "可选";
    case "server":
      return "服务端注入";
  }
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

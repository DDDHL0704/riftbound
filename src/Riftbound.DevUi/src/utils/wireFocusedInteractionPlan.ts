import type { ActionPromptCandidateDto, ActionPromptDto, SnapshotDto } from "../types/protocol";
import { sourceCandidatesForPrompt } from "./actionPromptCandidates";
import type { CandidateSelectionDraft } from "./candidateSelectionDraft";
import { candidateComposerKey } from "./candidateComposerModel";
import { buildFocusedActionModel, type FocusedActionModel } from "./focusedActionModel";
import { buildFocusedInteractionGrammarPlan, type FocusedInteractionGrammarPlan } from "./focusedInteractionGrammarPlan";
import {
  buildPromptInteractionModel,
  promptChoiceSummaryObjectIds,
  promptChoiceRoleLabel,
  type PromptCandidateSummary,
  type PromptInteractionModel,
  type PromptObjectSummary,
  type PromptChoiceRole
} from "./promptInteraction";
import { buildCardObjectIndex, type SnapshotObjectIndex } from "./snapshotObjectIndex";
import { buildSourceCandidateActionPlan, type SourceCandidateActionPlan } from "./sourceCandidateActionPlan";
import type { ServerSubmissionGatePlan } from "./serverSubmissionGatePlan";
import {
  buildWireActionSubmissionGatePlan,
  buildWireActionWindowGatePlan,
  type WireActionSubmissionGatePlan,
  type WireActionWindowGatePlan
} from "./wireActionGates";
import { buildWirePromptCandidateListPlan, type WirePromptCandidateListPlan, type WirePromptCandidateRowPlan } from "./wirePromptCandidatePlan";

export type WireFocusedActionEntryPlan = {
  actionPlan: SourceCandidateActionPlan;
  actionGateReason?: string;
  actionGateStateLabel?: string;
  candidate: ActionPromptCandidateDto;
  candidateDraft?: CandidateSelectionDraft;
  category: string;
  disabledByActionGate: boolean;
  intent: string;
  key: string;
  mode: "button" | "composer";
  priority: number;
  uiHint: string;
};

export type WireFocusedCandidatePathPlan = {
  key: string;
  label: string;
  steps: Array<{
    key: string;
    label: string;
    required: boolean;
    sampleLabel: string;
  }>;
};

export type WireFocusedSelectionDraftPlan = {
  destinationSelected: boolean;
  optionalCostCount: number;
  targetCount: number;
};

export type WireFocusedObjectPlan = {
  controllerLabel: string;
  objectId?: string;
  objectIdLabel: string;
  serverCandidateLabel: string;
  summary?: PromptObjectSummary;
};

export type WireFocusedReadinessState =
  | "needs-selection"
  | "no-focus"
  | "not-candidate"
  | "ready"
  | "server-blocked"
  | "submission-gate-blocked"
  | "window-blocked";

export type WireFocusedReadinessTone = "good" | "neutral" | "warn";

export type WireFocusedReadinessPlan = {
  blockedCount: number;
  candidateLabel: string;
  canSubmit: boolean;
  commandType?: string;
  enabledCount: number;
  missingRequiredCount: number;
  nextStepLabel: string;
  state: WireFocusedReadinessState;
  stateLabel: string;
  tone: WireFocusedReadinessTone;
};

export type WireFocusedLegalActionState =
  | "blocked"
  | "informational"
  | "needs-selection"
  | "ready";

export type WireFocusedLegalActionRowPlan = {
  action: string;
  category: string;
  commandType?: string;
  intent: string;
  key: string;
  label: string;
  missingRequiredLabels: string[];
  nextStepLabel: string;
  priority: number;
  reason: string;
  roleLabels: string[];
  state: WireFocusedLegalActionState;
  stateLabel: string;
  uiHint: string;
};

export type WireFocusedInteractionPlan = {
  actionEntries: WireFocusedActionEntryPlan[];
  draft?: WireFocusedSelectionDraftPlan;
  focusModel: FocusedActionModel;
  grammarPlan: FocusedInteractionGrammarPlan;
  legalActionRows: WireFocusedLegalActionRowPlan[];
  model: PromptInteractionModel;
  objectIndex: SnapshotObjectIndex;
  promptCandidateList: WirePromptCandidateListPlan;
  readiness: WireFocusedReadinessPlan;
  relatedCandidateRows: WirePromptCandidateRowPlan[];
  sourceCandidatePaths: WireFocusedCandidatePathPlan[];
  sourceCandidates: ActionPromptCandidateDto[];
  sourceObject: WireFocusedObjectPlan;
  sourceObjectId?: string;
  submissionGate: WireActionSubmissionGatePlan;
  windowGate: WireActionWindowGatePlan;
};

export type BuildWireFocusedInteractionPlanOptions = {
  canSubmitCommands: boolean;
  disabledByConnection: boolean;
  playerId: string;
  prompt?: ActionPromptDto;
  selectionDraft?: CandidateSelectionDraft;
  snapshot?: SnapshotDto;
  sourceControllerId?: string | null;
  sourceObjectId?: string;
  submissionGate?: ServerSubmissionGatePlan;
};

export function buildWireFocusedInteractionPlan({
  canSubmitCommands,
  disabledByConnection,
  playerId,
  prompt,
  selectionDraft,
  snapshot,
  sourceControllerId,
  sourceObjectId,
  submissionGate
}: BuildWireFocusedInteractionPlanOptions): WireFocusedInteractionPlan {
  const model = buildPromptInteractionModel(prompt);
  const objectIndex = buildCardObjectIndex(snapshot);
  const submitGate = buildWireActionSubmissionGatePlan(submissionGate, !disabledByConnection);
  const windowGate = buildWireActionWindowGatePlan({ playerId, prompt });
  const blockedByAnyGate = !submitGate.canSubmit || !windowGate.canAct;
  const gateBlockReason = !submitGate.canSubmit ? submitGate.reason : !windowGate.canAct ? windowGate.reason : undefined;
  const gateBlockStateLabel = !submitGate.canSubmit ? submitGate.stateLabel : !windowGate.canAct ? windowGate.stateLabel : undefined;
  const objectSummary = sourceObjectId ? model.objectById.get(sourceObjectId) : undefined;
  const focusModel = buildFocusedActionModel({
    interactionModel: model,
    prompt,
    selectionDraft,
    sourceObjectId
  });
  const relatedCandidates = sourceObjectId
    ? model.candidates.filter((candidate) =>
      candidate.choices.some((choice) => promptChoiceSummaryObjectIds(choice).includes(sourceObjectId)))
    : [];
  const relatedCandidateRows = candidateRowsFor(relatedCandidates, objectIndex);
  const sourceCandidates = sourceCandidatesForPrompt(prompt, sourceObjectId, { enabledOnly: false });
  const sourceCandidateSummaries = sourceObjectId
    ? model.candidates.filter((candidate) =>
      candidate.choices.some((choice) =>
        choice.role === "source"
        && promptChoiceSummaryObjectIds(choice).includes(sourceObjectId)))
    : [];
  const grammarPlan = buildFocusedInteractionGrammarPlan({
    candidates: sourceCandidateSummaries,
    disabledByConnection: blockedByAnyGate,
    selectionDraft,
    submitBlockedStateLabel: gateBlockStateLabel,
    sourceObjectId
  });
  const actionEntries = sourceCandidates
    .map((candidate) => actionEntryFor({
      actionGateReason: !windowGate.canAct ? windowGate.reason : undefined,
      canSubmitCommands,
      candidate,
      disabledByActionGate: !windowGate.canAct,
      disabledByConnection: !submitGate.canSubmit,
      selectionDraft,
      sourceObjectId
    }))
    .sort(actionEntrySort);
  const readiness = readinessPlanFor({
    focusModel,
    grammarPlan,
    gateBlockReason,
    sourceObjectId,
    submissionGate: submitGate,
    windowGate
  });

  return {
    actionEntries,
    draft: draftPlanFor(selectionDraft, sourceObjectId),
    focusModel,
    grammarPlan,
    legalActionRows: legalActionRowsFor({
      candidates: relatedCandidates,
      gateBlockReason,
      gateBlocked: blockedByAnyGate,
      selectionDraft,
      sourceObjectId
    }),
    model,
    objectIndex,
    promptCandidateList: buildWirePromptCandidateListPlan({
      model,
      objects: objectIndex,
      promptId: prompt?.promptId,
      promptMessage: prompt?.view?.message,
      promptReason: prompt?.reason,
      promptTitle: prompt?.view?.title,
      promptType: prompt?.view?.type,
      snapshotTick: prompt?.snapshotTick
    }),
    readiness,
    relatedCandidateRows,
    sourceCandidatePaths: candidatePathsFor(sourceCandidateSummaries),
    sourceCandidates,
    sourceObject: {
      controllerLabel: sourceControllerId?.trim() || "未知",
      objectId: sourceObjectId,
      objectIdLabel: sourceObjectId || "无对象 ID",
      serverCandidateLabel: objectSummary ? `${objectSummary.enabledCandidateCount} 可用 / ${objectSummary.disabledCandidateCount} 禁用` : "无候选",
      summary: objectSummary
    },
    sourceObjectId,
    submissionGate: submitGate,
    windowGate
  };
}

function readinessPlanFor({
  focusModel,
  gateBlockReason,
  grammarPlan,
  sourceObjectId,
  submissionGate,
  windowGate
}: {
  focusModel: FocusedActionModel;
  gateBlockReason?: string;
  grammarPlan: FocusedInteractionGrammarPlan;
  sourceObjectId?: string;
  submissionGate: WireActionSubmissionGatePlan;
  windowGate: WireActionWindowGatePlan;
}): WireFocusedReadinessPlan {
  const state = readinessStateFor({
    focusModel,
    grammarPlan,
    sourceObjectId,
    submissionGate,
    windowGate
  });

  return {
    blockedCount: focusModel.blockedCount,
    candidateLabel: grammarPlan.candidateLabel,
    canSubmit: state === "ready",
    commandType: grammarPlan.commandType,
    enabledCount: focusModel.enabledCount,
    missingRequiredCount: grammarPlan.missingRequiredCount,
    nextStepLabel: readinessNextStepLabel(state, focusModel, grammarPlan, gateBlockReason),
    state,
    stateLabel: readinessStateLabel(state),
    tone: readinessTone(state)
  };
}

function readinessStateFor({
  focusModel,
  grammarPlan,
  sourceObjectId,
  submissionGate,
  windowGate
}: {
  focusModel: FocusedActionModel;
  grammarPlan: FocusedInteractionGrammarPlan;
  sourceObjectId?: string;
  submissionGate: WireActionSubmissionGatePlan;
  windowGate: WireActionWindowGatePlan;
}): WireFocusedReadinessState {
  if (!sourceObjectId) {
    return "no-focus";
  }

  if (!focusModel.submittedByServer) {
    return "not-candidate";
  }

  if (!submissionGate.canSubmit) {
    return "submission-gate-blocked";
  }

  if (!windowGate.canAct) {
    return "window-blocked";
  }

  if (focusModel.enabledCount <= 0) {
    return "server-blocked";
  }

  if (grammarPlan.state === "ready") {
    return "ready";
  }

  if (grammarPlan.state === "incomplete") {
    return "needs-selection";
  }

  return "server-blocked";
}

function readinessNextStepLabel(
  state: WireFocusedReadinessState,
  focusModel: FocusedActionModel,
  grammarPlan: FocusedInteractionGrammarPlan,
  gateBlockReason?: string
): string {
  switch (state) {
    case "needs-selection":
      return grammarPlan.nextStepLabel;
    case "no-focus":
      return focusModel.nextStepLabel;
    case "not-candidate":
      return "该对象当前没有服务端候选。";
    case "ready":
      return "可以提交服务端候选。";
    case "server-blocked":
      return focusModel.blockingReasons[0] || focusModel.nextStepLabel;
    case "submission-gate-blocked":
    case "window-blocked":
      return gateBlockReason ?? "当前行动入口不能提交该候选。";
  }
}

function readinessStateLabel(state: WireFocusedReadinessState): string {
  switch (state) {
    case "needs-selection":
      return "待选择";
    case "no-focus":
      return "无焦点";
    case "not-candidate":
      return "非候选";
    case "ready":
      return "可提交";
    case "server-blocked":
      return "服务端阻断";
    case "submission-gate-blocked":
      return "提交门禁阻断";
    case "window-blocked":
      return "行动窗口阻断";
  }
}

function readinessTone(state: WireFocusedReadinessState): WireFocusedReadinessTone {
  switch (state) {
    case "ready":
      return "good";
    case "needs-selection":
    case "server-blocked":
    case "submission-gate-blocked":
    case "window-blocked":
      return "warn";
    case "no-focus":
    case "not-candidate":
      return "neutral";
  }
}

function actionEntryFor({
  actionGateReason,
  canSubmitCommands,
  candidate,
  disabledByActionGate,
  disabledByConnection,
  selectionDraft,
  sourceObjectId
}: {
  actionGateReason?: string;
  canSubmitCommands: boolean;
  candidate: ActionPromptCandidateDto;
  disabledByActionGate: boolean;
  disabledByConnection: boolean;
  selectionDraft?: CandidateSelectionDraft;
  sourceObjectId?: string;
}): WireFocusedActionEntryPlan {
  const actionPlan = buildSourceCandidateActionPlan({
    actionGateReason,
    canSubmitCommands,
    candidate,
    disabledByActionGate,
    disabledByConnection,
    sourceObjectId
  });
  const candidateDraft = selectionDraft?.candidateKey === candidateComposerKey(candidate)
    ? selectionDraft
    : undefined;
  const presentation = normalizedCandidatePresentation(candidate);

  return {
    actionPlan,
    actionGateReason,
    actionGateStateLabel: disabledByActionGate ? "行动窗口阻断" : undefined,
    candidate,
    candidateDraft,
    category: presentation.category,
    disabledByActionGate,
    intent: presentation.intent,
    key: `${candidate.action}-${candidate.label}`,
    mode: actionPlan.needsComposer && canSubmitCommands ? "composer" : "button",
    priority: presentation.priority,
    uiHint: presentation.uiHint
  };
}

function actionEntrySort(left: WireFocusedActionEntryPlan, right: WireFocusedActionEntryPlan): number {
  return Number(right.candidate.enabled) - Number(left.candidate.enabled)
    || left.priority - right.priority
    || left.actionPlan.label.localeCompare(right.actionPlan.label, "zh-Hans-CN");
}

function legalActionRowsFor({
  candidates,
  gateBlocked,
  gateBlockReason,
  selectionDraft,
  sourceObjectId
}: {
  candidates: PromptCandidateSummary[];
  gateBlocked: boolean;
  gateBlockReason?: string;
  selectionDraft?: CandidateSelectionDraft;
  sourceObjectId?: string;
}): WireFocusedLegalActionRowPlan[] {
  if (!sourceObjectId) {
    return [];
  }

  return candidates
    .map((candidate) => legalActionRowFor({
      candidate,
      gateBlocked,
      gateBlockReason,
      selectionDraft,
      sourceObjectId
    }))
    .sort((left, right) =>
      legalActionStateOrder(left.state) - legalActionStateOrder(right.state)
      || left.priority - right.priority
      || left.label.localeCompare(right.label, "zh-Hans-CN"));
}

function legalActionRowFor({
  candidate,
  gateBlocked,
  gateBlockReason,
  selectionDraft,
  sourceObjectId
}: {
  candidate: PromptCandidateSummary;
  gateBlocked: boolean;
  gateBlockReason?: string;
  selectionDraft?: CandidateSelectionDraft;
  sourceObjectId: string;
}): WireFocusedLegalActionRowPlan {
  const roleKeys = objectRolesForCandidate(candidate, sourceObjectId);
  const roleLabels = roleKeys.map(promptChoiceRoleLabel);
  const sourceRole = roleKeys.includes("source");
  const missingRequiredSteps = sourceRole
    ? candidate.steps.filter((step) => step.required && !isLegalActionStepSatisfied(step.role, sourceObjectId, selectionDraft))
    : [];
  const state = legalActionStateFor({
    candidate,
    gateBlocked,
    missingRequiredSteps,
    sourceRole
  });

  return {
    action: candidate.action,
    category: candidate.presentation.category,
    commandType: candidate.command?.cmdType,
    intent: candidate.presentation.intent,
    key: `${candidate.action}-${candidate.label}-${roleKeys.join(":")}`,
    label: candidate.label,
    missingRequiredLabels: missingRequiredSteps.map((step) => step.label),
    nextStepLabel: legalActionNextStepLabel({
      candidate,
      gateBlocked,
      gateBlockReason,
      missingRequiredSteps,
      roleLabels,
      sourceRole,
      state
    }),
    priority: candidate.presentation.priority,
    reason: candidate.reason,
    roleLabels,
    state,
    stateLabel: legalActionStateLabel(state, sourceRole, roleLabels),
    uiHint: candidate.presentation.uiHint
  };
}

function normalizedCandidatePresentation(candidate: ActionPromptCandidateDto): {
  category: string;
  intent: string;
  priority: number;
  uiHint: string;
} {
  return {
    category: normalizedPresentationText(candidate.presentation?.category, "custom"),
    intent: normalizedPresentationText(candidate.presentation?.intent, candidate.action.toLowerCase().replaceAll("_", "-")),
    priority: typeof candidate.presentation?.priority === "number" && Number.isFinite(candidate.presentation.priority)
      ? candidate.presentation.priority
      : 700,
    uiHint: normalizedPresentationText(candidate.presentation?.uiHint, "card-action")
  };
}

function normalizedPresentationText(value: string | null | undefined, fallback: string): string {
  const normalized = value?.trim();
  return normalized && normalized.length > 0 ? normalized : fallback;
}

function objectRolesForCandidate(candidate: PromptCandidateSummary, sourceObjectId: string): PromptChoiceRole[] {
  const roles = new Set<PromptChoiceRole>();
  for (const choice of candidate.choices) {
    if (promptChoiceSummaryObjectIds(choice).includes(sourceObjectId)) {
      roles.add(choice.role);
    }
  }

  return [...roles].sort((left, right) => roleSortIndex(left) - roleSortIndex(right));
}

function legalActionStateFor({
  candidate,
  gateBlocked,
  missingRequiredSteps,
  sourceRole
}: {
  candidate: PromptCandidateSummary;
  gateBlocked: boolean;
  missingRequiredSteps: PromptCandidateSummary["steps"];
  sourceRole: boolean;
}): WireFocusedLegalActionState {
  if (gateBlocked || !candidate.enabled) {
    return "blocked";
  }

  if (!sourceRole) {
    return "informational";
  }

  if (!candidate.command) {
    return "blocked";
  }

  return missingRequiredSteps.length > 0 ? "needs-selection" : "ready";
}

function legalActionStateLabel(
  state: WireFocusedLegalActionState,
  sourceRole: boolean,
  roleLabels: string[]
): string {
  switch (state) {
    case "blocked":
      return sourceRole ? "不可提交" : "关联阻断";
    case "informational":
      return roleLabels.length > 0 ? `可作为${roleLabels.join("/")}` : "关联候选";
    case "needs-selection":
      return "需选择";
    case "ready":
      return "可提交";
  }
}

function legalActionNextStepLabel({
  candidate,
  gateBlocked,
  gateBlockReason,
  missingRequiredSteps,
  roleLabels,
  sourceRole,
  state
}: {
  candidate: PromptCandidateSummary;
  gateBlocked: boolean;
  gateBlockReason?: string;
  missingRequiredSteps: PromptCandidateSummary["steps"];
  roleLabels: string[];
  sourceRole: boolean;
  state: WireFocusedLegalActionState;
}): string {
  if (gateBlocked) {
    return gateBlockReason ?? "当前行动入口不能提交该候选。";
  }

  if (!candidate.enabled) {
    return candidate.reason;
  }

  if (!sourceRole) {
    return roleLabels.length > 0
      ? `该对象当前作为${roleLabels.join("/")}出现在服务端候选中。`
      : "该对象与服务端候选有关联。";
  }

  if (!candidate.command) {
    return "候选未公开命令模板，前端不能组装提交。";
  }

  if (state === "needs-selection") {
    return `选择${missingRequiredSteps[0]?.label ?? "缺失项"}`;
  }

  return "可以提交服务端候选。";
}

function isLegalActionStepSatisfied(
  role: PromptChoiceRole,
  sourceObjectId: string,
  selectionDraft?: CandidateSelectionDraft
): boolean {
  switch (role) {
    case "source":
      return Boolean(sourceObjectId);
    case "target":
      return Boolean(selectionDraft?.targetChoiceIds.length);
    case "destination":
      return Boolean(selectionDraft?.destinationId);
    case "mode":
      return Boolean(selectionDraft?.mode);
    case "optionalCost":
      return Boolean(selectionDraft?.optionalCostIds.length);
  }
}

function roleSortIndex(role: PromptChoiceRole): number {
  switch (role) {
    case "source":
      return 0;
    case "mode":
      return 1;
    case "destination":
      return 2;
    case "target":
      return 3;
    case "optionalCost":
      return 4;
  }
}

function legalActionStateOrder(state: WireFocusedLegalActionState): number {
  switch (state) {
    case "ready":
      return 0;
    case "needs-selection":
      return 1;
    case "informational":
      return 2;
    case "blocked":
      return 3;
  }
}

function candidateRowsFor(
  candidates: PromptCandidateSummary[],
  objectIndex: SnapshotObjectIndex
): WirePromptCandidateRowPlan[] {
  const relatedCandidatePlan = buildWirePromptCandidateListPlan({
    model: {
      candidates,
      disabledObjectIds: new Set(),
      enabledObjectIds: new Set(),
      objectById: new Map()
    },
    objects: objectIndex
  });

  return [
    ...relatedCandidatePlan.enabledRows,
    ...relatedCandidatePlan.disabledRows
  ];
}

function candidatePathsFor(candidates: PromptCandidateSummary[]): WireFocusedCandidatePathPlan[] {
  return candidates.slice(0, 2).map((candidate) => ({
    key: `${candidate.action}-${candidate.label}`,
    label: candidate.label,
    steps: candidate.steps.map((step) => ({
      key: `${candidate.action}-${step.role}`,
      label: step.label,
      required: step.required,
      sampleLabel: step.sampleLabels.length > 0 ? step.sampleLabels.join(" / ") : "服务端候选"
    }))
  }));
}

function draftPlanFor(
  selectionDraft: CandidateSelectionDraft | undefined,
  sourceObjectId: string | undefined
): WireFocusedSelectionDraftPlan | undefined {
  if (!selectionDraft || !sourceObjectId || selectionDraft.sourceObjectId !== sourceObjectId) {
    return undefined;
  }

  return {
    destinationSelected: Boolean(selectionDraft?.destinationId),
    optionalCostCount: selectionDraft?.optionalCostIds.length ?? 0,
    targetCount: selectionDraft?.targetChoiceIds.length ?? 0
  };
}

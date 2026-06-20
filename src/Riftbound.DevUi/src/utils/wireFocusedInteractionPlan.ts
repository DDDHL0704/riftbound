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
import { buildWirePromptCandidateListPlan, type WirePromptCandidateListPlan, type WirePromptCandidateRowPlan } from "./wirePromptCandidatePlan";

export type WireFocusedActionEntryPlan = {
  actionPlan: SourceCandidateActionPlan;
  candidate: ActionPromptCandidateDto;
  candidateDraft?: CandidateSelectionDraft;
  key: string;
  mode: "button" | "composer";
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
  | "connection-blocked"
  | "needs-selection"
  | "no-focus"
  | "not-candidate"
  | "ready"
  | "server-blocked";

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
  commandType?: string;
  key: string;
  label: string;
  missingRequiredLabels: string[];
  nextStepLabel: string;
  reason: string;
  roleLabels: string[];
  state: WireFocusedLegalActionState;
  stateLabel: string;
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
};

export type BuildWireFocusedInteractionPlanOptions = {
  canSubmitCommands: boolean;
  disabledByConnection: boolean;
  prompt?: ActionPromptDto;
  selectionDraft?: CandidateSelectionDraft;
  snapshot?: SnapshotDto;
  sourceControllerId?: string | null;
  sourceObjectId?: string;
};

export function buildWireFocusedInteractionPlan({
  canSubmitCommands,
  disabledByConnection,
  prompt,
  selectionDraft,
  snapshot,
  sourceControllerId,
  sourceObjectId
}: BuildWireFocusedInteractionPlanOptions): WireFocusedInteractionPlan {
  const model = buildPromptInteractionModel(prompt);
  const objectIndex = buildCardObjectIndex(snapshot);
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
      candidate.enabled
      && candidate.choices.some((choice) =>
        choice.role === "source"
        && promptChoiceSummaryObjectIds(choice).includes(sourceObjectId)))
    : [];
  const grammarPlan = buildFocusedInteractionGrammarPlan({
    candidates: sourceCandidateSummaries,
    disabledByConnection,
    selectionDraft,
    sourceObjectId
  });
  const actionEntries = sourceCandidates.map((candidate) => actionEntryFor({
    canSubmitCommands,
    candidate,
    disabledByConnection,
    selectionDraft,
    sourceObjectId
  }));
  const readiness = readinessPlanFor({
    disabledByConnection,
    focusModel,
    grammarPlan,
    sourceObjectId
  });

  return {
    actionEntries,
    draft: draftPlanFor(selectionDraft, sourceObjectId),
    focusModel,
    grammarPlan,
    legalActionRows: legalActionRowsFor({
      candidates: relatedCandidates,
      disabledByConnection,
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
    sourceObjectId
  };
}

function readinessPlanFor({
  disabledByConnection,
  focusModel,
  grammarPlan,
  sourceObjectId
}: {
  disabledByConnection: boolean;
  focusModel: FocusedActionModel;
  grammarPlan: FocusedInteractionGrammarPlan;
  sourceObjectId?: string;
}): WireFocusedReadinessPlan {
  const state = readinessStateFor({
    disabledByConnection,
    focusModel,
    grammarPlan,
    sourceObjectId
  });

  return {
    blockedCount: focusModel.blockedCount,
    candidateLabel: grammarPlan.candidateLabel,
    canSubmit: state === "ready",
    commandType: grammarPlan.commandType,
    enabledCount: focusModel.enabledCount,
    missingRequiredCount: grammarPlan.missingRequiredCount,
    nextStepLabel: readinessNextStepLabel(state, focusModel, grammarPlan),
    state,
    stateLabel: readinessStateLabel(state),
    tone: readinessTone(state)
  };
}

function readinessStateFor({
  disabledByConnection,
  focusModel,
  grammarPlan,
  sourceObjectId
}: {
  disabledByConnection: boolean;
  focusModel: FocusedActionModel;
  grammarPlan: FocusedInteractionGrammarPlan;
  sourceObjectId?: string;
}): WireFocusedReadinessState {
  if (!sourceObjectId) {
    return "no-focus";
  }

  if (!focusModel.submittedByServer) {
    return "not-candidate";
  }

  if (disabledByConnection) {
    return "connection-blocked";
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
  grammarPlan: FocusedInteractionGrammarPlan
): string {
  switch (state) {
    case "connection-blocked":
      return "等待连接恢复后再提交服务端候选。";
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
  }
}

function readinessStateLabel(state: WireFocusedReadinessState): string {
  switch (state) {
    case "connection-blocked":
      return "连接阻断";
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
  }
}

function readinessTone(state: WireFocusedReadinessState): WireFocusedReadinessTone {
  switch (state) {
    case "ready":
      return "good";
    case "connection-blocked":
    case "needs-selection":
    case "server-blocked":
      return "warn";
    case "no-focus":
    case "not-candidate":
      return "neutral";
  }
}

function actionEntryFor({
  canSubmitCommands,
  candidate,
  disabledByConnection,
  selectionDraft,
  sourceObjectId
}: {
  canSubmitCommands: boolean;
  candidate: ActionPromptCandidateDto;
  disabledByConnection: boolean;
  selectionDraft?: CandidateSelectionDraft;
  sourceObjectId?: string;
}): WireFocusedActionEntryPlan {
  const actionPlan = buildSourceCandidateActionPlan({
    canSubmitCommands,
    candidate,
    disabledByConnection,
    sourceObjectId
  });
  const candidateDraft = selectionDraft?.candidateKey === candidateComposerKey(candidate)
    ? selectionDraft
    : undefined;

  return {
    actionPlan,
    candidate,
    candidateDraft,
    key: `${candidate.action}-${candidate.label}`,
    mode: actionPlan.needsComposer && canSubmitCommands ? "composer" : "button"
  };
}

function legalActionRowsFor({
  candidates,
  disabledByConnection,
  selectionDraft,
  sourceObjectId
}: {
  candidates: PromptCandidateSummary[];
  disabledByConnection: boolean;
  selectionDraft?: CandidateSelectionDraft;
  sourceObjectId?: string;
}): WireFocusedLegalActionRowPlan[] {
  if (!sourceObjectId) {
    return [];
  }

  return candidates
    .map((candidate) => legalActionRowFor({
      candidate,
      disabledByConnection,
      selectionDraft,
      sourceObjectId
    }))
    .sort((left, right) => legalActionStateOrder(left.state) - legalActionStateOrder(right.state));
}

function legalActionRowFor({
  candidate,
  disabledByConnection,
  selectionDraft,
  sourceObjectId
}: {
  candidate: PromptCandidateSummary;
  disabledByConnection: boolean;
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
    disabledByConnection,
    missingRequiredSteps,
    sourceRole
  });

  return {
    action: candidate.action,
    commandType: candidate.command?.cmdType,
    key: `${candidate.action}-${candidate.label}-${roleKeys.join(":")}`,
    label: candidate.label,
    missingRequiredLabels: missingRequiredSteps.map((step) => step.label),
    nextStepLabel: legalActionNextStepLabel({
      candidate,
      disabledByConnection,
      missingRequiredSteps,
      roleLabels,
      sourceRole,
      state
    }),
    reason: candidate.reason,
    roleLabels,
    state,
    stateLabel: legalActionStateLabel(state, sourceRole, roleLabels)
  };
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
  disabledByConnection,
  missingRequiredSteps,
  sourceRole
}: {
  candidate: PromptCandidateSummary;
  disabledByConnection: boolean;
  missingRequiredSteps: PromptCandidateSummary["steps"];
  sourceRole: boolean;
}): WireFocusedLegalActionState {
  if (disabledByConnection || !candidate.enabled) {
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
  disabledByConnection,
  missingRequiredSteps,
  roleLabels,
  sourceRole,
  state
}: {
  candidate: PromptCandidateSummary;
  disabledByConnection: boolean;
  missingRequiredSteps: PromptCandidateSummary["steps"];
  roleLabels: string[];
  sourceRole: boolean;
  state: WireFocusedLegalActionState;
}): string {
  if (disabledByConnection) {
    return "等待连接恢复后再提交。";
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

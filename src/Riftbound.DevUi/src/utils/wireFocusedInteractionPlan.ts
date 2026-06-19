import type { ActionPromptCandidateDto, ActionPromptDto, SnapshotDto } from "../types/protocol";
import { sourceCandidatesForPrompt } from "./actionPromptCandidates";
import type { CandidateSelectionDraft } from "./candidateSelectionDraft";
import { candidateComposerKey } from "./candidateComposerModel";
import { buildFocusedActionModel, type FocusedActionModel } from "./focusedActionModel";
import { buildFocusedInteractionGrammarPlan, type FocusedInteractionGrammarPlan } from "./focusedInteractionGrammarPlan";
import {
  buildPromptInteractionModel,
  promptChoiceSummaryObjectIds,
  type PromptCandidateSummary,
  type PromptInteractionModel,
  type PromptObjectSummary
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

export type WireFocusedInteractionPlan = {
  actionEntries: WireFocusedActionEntryPlan[];
  draft?: WireFocusedSelectionDraftPlan;
  focusModel: FocusedActionModel;
  grammarPlan: FocusedInteractionGrammarPlan;
  model: PromptInteractionModel;
  objectIndex: SnapshotObjectIndex;
  promptCandidateList: WirePromptCandidateListPlan;
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
  const sourceCandidates = sourceCandidatesForPrompt(prompt, sourceObjectId);
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

  return {
    actionEntries,
    draft: draftPlanFor(selectionDraft, sourceObjectId),
    focusModel,
    grammarPlan,
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

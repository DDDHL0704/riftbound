import type { ActionPromptDto } from "../types/protocol";
import { candidateMatchesSource } from "./actionPromptCandidates";
import type { CandidateSelectionDraft } from "./candidateSelectionDraft";
import {
  type PromptCandidateStep,
  type PromptCandidateSummary,
  type PromptChoiceRole,
  type PromptInteractionModel
} from "./promptInteraction";

export type FocusedActionCandidate = {
  candidate: PromptCandidateSummary;
  key: string;
  nextStep: PromptCandidateStep | undefined;
  stateLabel: string;
};

export type FocusedActionModel = {
  blockedCount: number;
  blockingReasons: string[];
  candidates: FocusedActionCandidate[];
  enabledCount: number;
  nextStepLabel: string;
  sourceObjectId?: string;
  stateLabel: string;
  submittedByServer: boolean;
  totalCount: number;
};

type BuildFocusedActionModelOptions = {
  interactionModel: PromptInteractionModel;
  prompt?: ActionPromptDto;
  selectionDraft?: CandidateSelectionDraft;
  sourceObjectId?: string;
};

export function buildFocusedActionModel({
  interactionModel,
  prompt,
  selectionDraft,
  sourceObjectId
}: BuildFocusedActionModelOptions): FocusedActionModel {
  if (!sourceObjectId) {
    return emptyFocusedActionModel("未选择卡牌");
  }

  const rawCandidates = (prompt?.candidates ?? [])
    .filter((candidate) => candidateMatchesSource(candidate, sourceObjectId));
  const focusedCandidates = rawCandidates
    .map((candidate) => {
      const summary = interactionModel.candidates.find((item) =>
        item.action === candidate.action
        && item.label === candidate.label
        && item.enabled === candidate.enabled);
      if (!summary) {
        return undefined;
      }

      return {
        candidate: summary,
        key: focusedActionCandidateKey(summary),
        nextStep: nextUnsatisfiedStep(summary, sourceObjectId, selectionDraft),
        stateLabel: summary.enabled ? "可提交候选" : "当前不可提交"
      } satisfies FocusedActionCandidate;
    })
    .filter((candidate): candidate is FocusedActionCandidate => Boolean(candidate));
  const enabledCount = focusedCandidates.filter((candidate) => candidate.candidate.enabled).length;
  const blockedCount = focusedCandidates.length - enabledCount;
  const primary = focusedCandidates.find((candidate) => candidate.candidate.enabled) ?? focusedCandidates[0];
  const nextStepLabel = nextStepCopy(primary, enabledCount);
  const blockingReasons = uniqueStrings(
    focusedCandidates
      .filter((candidate) => !candidate.candidate.enabled)
      .map((candidate) => candidate.candidate.reason)
  ).slice(0, 3);

  return {
    blockedCount,
    blockingReasons,
    candidates: focusedCandidates,
    enabledCount,
    nextStepLabel,
    sourceObjectId,
    stateLabel: focusedStateLabel(focusedCandidates.length, enabledCount),
    submittedByServer: focusedCandidates.length > 0,
    totalCount: focusedCandidates.length
  };
}

export function focusedActionCandidateKey(candidate: Pick<PromptCandidateSummary, "action" | "label">): string {
  return `${candidate.action}::${candidate.label}`;
}

function emptyFocusedActionModel(stateLabel: string): FocusedActionModel {
  return {
    blockedCount: 0,
    blockingReasons: [],
    candidates: [],
    enabledCount: 0,
    nextStepLabel: "点击桌面上的卡牌查看服务端候选。",
    stateLabel,
    submittedByServer: false,
    totalCount: 0
  };
}

function focusedStateLabel(totalCount: number, enabledCount: number): string {
  if (totalCount === 0) {
    return "该对象当前不在服务端候选中";
  }

  if (enabledCount > 0) {
    return `${enabledCount} 个可提交候选`;
  }

  return "候选存在但暂不可提交";
}

function nextStepCopy(candidate: FocusedActionCandidate | undefined, enabledCount: number): string {
  if (!candidate) {
    return "服务端未给该对象候选。";
  }

  if (candidate.nextStep) {
    return `下一步：${candidate.nextStep.label}`;
  }

  return enabledCount > 0
    ? "下一步：确认候选并提交给服务端校验。"
    : "等待服务端开放该候选。";
}

function nextUnsatisfiedStep(
  candidate: PromptCandidateSummary,
  sourceObjectId: string,
  selectionDraft?: CandidateSelectionDraft
): PromptCandidateStep | undefined {
  if (!candidate.enabled) {
    return candidate.steps.find((step) => step.required);
  }

  for (const step of candidate.steps) {
    if (!step.required) {
      continue;
    }

    if (!isStepSatisfied(step.role, sourceObjectId, selectionDraft)) {
      return step;
    }
  }

  return candidate.steps.find((step) =>
    step.count > 0
    && !isStepSatisfied(step.role, sourceObjectId, selectionDraft));
}

function isStepSatisfied(
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
    default:
      return false;
  }
}

function uniqueStrings(values: string[]): string[] {
  return [...new Set(values.filter((value) => value.trim().length > 0))];
}

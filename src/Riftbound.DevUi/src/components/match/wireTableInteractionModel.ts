import type { CandidateSelectionDraft } from "../../utils/candidateSelectionDraft";
import {
  promptChoiceSummaryObjectIds,
  type PromptCandidateSummary,
  type PromptChoiceRole,
  type PromptChoiceSummary,
  type PromptInteractionModel,
  type PromptObjectState
} from "../../utils/promptInteraction";
import { candidateComposerKey } from "../../utils/candidateComposerModel";
import type { WireTimelineDetail } from "./WireTimelineDetailPanel";
import type { WireTimelineObjectState } from "./wireCardFlow";

export function focusedCandidateSummaries(
  candidates: PromptCandidateSummary[],
  focusedObjectId?: string
): PromptCandidateSummary[] {
  if (!focusedObjectId) {
    return [];
  }

  return candidates.filter((candidate) =>
    candidate.enabled
    && candidate.choices.some((choice) =>
      choice.role === "source"
      && promptChoiceSummaryObjectIds(choice).includes(focusedObjectId)));
}

export function sourceCandidateForObject(
  candidates: PromptCandidateSummary[],
  objectId: string
): PromptCandidateSummary | undefined {
  return candidates.find((candidate) =>
    candidate.enabled
    && candidate.choices.some((choice) =>
      choice.role === "source"
      && promptChoiceSummaryObjectIds(choice).includes(objectId)));
}

export function candidateChoiceForObject(
  candidates: PromptCandidateSummary[],
  objectId: string
): { candidate: PromptCandidateSummary; choice: PromptChoiceSummary } | undefined {
  for (const candidate of candidates.filter((candidate) => candidate.enabled)) {
    const choice = candidate.choices.find((candidateChoice) =>
      candidateChoice.role !== "mode"
      && promptChoiceSummaryObjectIds(candidateChoice).includes(objectId));
    if (choice) {
      return { candidate, choice };
    }
  }

  return undefined;
}

export function emptySelectionDraft(sourceObjectId: string, candidate: PromptCandidateSummary): CandidateSelectionDraft {
  return {
    candidateKey: candidateComposerKey(candidate),
    optionalCostIds: [],
    sourceObjectId,
    targetChoiceIds: []
  };
}

export function updateSelectionDraft(
  current: CandidateSelectionDraft | undefined,
  sourceObjectId: string,
  candidate: PromptCandidateSummary,
  choice: PromptChoiceSummary
): CandidateSelectionDraft {
  const candidateKey = candidateComposerKey(candidate);
  const base = current?.candidateKey === candidateKey && current.sourceObjectId === sourceObjectId
    ? current
    : emptySelectionDraft(sourceObjectId, candidate);

  if (choice.role === "target") {
    return {
      ...base,
      targetChoiceIds: uniqueSelectionIds([choice.id, ...base.targetChoiceIds.filter((id) => id !== choice.id)]).slice(0, 8)
    };
  }

  if (choice.role === "destination") {
    return {
      ...base,
      destinationId: choice.id
    };
  }

  if (choice.role === "optionalCost") {
    const selected = base.optionalCostIds.includes(choice.id);
    return {
      ...base,
      optionalCostIds: selected
        ? base.optionalCostIds.filter((id) => id !== choice.id)
        : uniqueSelectionIds([...base.optionalCostIds, choice.id])
    };
  }

  return base;
}

export function buildWireInteractionMap(
  model: PromptInteractionModel,
  focusedCandidates: PromptCandidateSummary[],
  focusedObjectId?: string,
  selectionDraft?: CandidateSelectionDraft
): Record<string, PromptObjectState | undefined> {
  const states: Record<string, PromptObjectState | undefined> = Object.fromEntries([
    ...[...model.disabledObjectIds].map((objectId) => [objectId, "disabled" as const]),
    ...[...model.enabledObjectIds].map((objectId) => [objectId, "enabled" as const])
  ]);

  for (const candidate of focusedCandidates.filter((candidate) => candidate.enabled)) {
    for (const choice of candidate.choices) {
      const roleState = promptRoleState(choice.role);
      if (!roleState) {
        continue;
      }

      for (const objectId of promptChoiceSummaryObjectIds(choice)) {
        states[objectId] = mergePromptObjectState(states[objectId], roleState);
      }
    }
  }

  if (focusedObjectId && focusedCandidates.some((candidate) => candidate.enabled)) {
    states[focusedObjectId] = "source";
  }

  if (selectionDraft) {
    const selectedChoiceIds = new Set([
      ...selectionDraft.targetChoiceIds,
      selectionDraft.destinationId,
      selectionDraft.mode,
      ...selectionDraft.optionalCostIds
    ].filter((id): id is string => Boolean(id)));
    const draftCandidate = model.candidates.find((candidate) => candidateComposerKey(candidate) === selectionDraft.candidateKey);
    for (const choice of draftCandidate?.choices ?? []) {
      if (!selectedChoiceIds.has(choice.id)) {
        continue;
      }

      for (const objectId of promptChoiceSummaryObjectIds(choice)) {
        states[objectId] = mergePromptObjectState(states[objectId], "chosen");
      }
    }
  }

  return states;
}

export function buildWireTimelineMap(detail?: WireTimelineDetail): Record<string, WireTimelineObjectState | undefined> {
  if (!detail) {
    return {};
  }

  return Object.fromEntries(timelineDetailObjectIds(detail).map((objectId) => [objectId, detail.source]));
}

export function mergeWireTimelineMaps(
  ...maps: Array<Record<string, WireTimelineObjectState | undefined>>
): Record<string, WireTimelineObjectState | undefined> {
  const merged: Record<string, WireTimelineObjectState | undefined> = {};
  for (const map of maps) {
    for (const [objectId, state] of Object.entries(map)) {
      if (state) {
        merged[objectId] = state;
      }
    }
  }

  return merged;
}

function uniqueSelectionIds(ids: string[]): string[] {
  return Array.from(new Set(ids.filter((id) => id.trim().length > 0)));
}

function timelineDetailObjectIds(detail: WireTimelineDetail): string[] {
  return Array.from(new Set(detail.refs
    .map((ref) => ref.id.trim())
    .filter((id) => id.length > 0 && id !== "HIDDEN")));
}

function promptRoleState(role: PromptChoiceRole): PromptObjectState | undefined {
  return role === "mode" ? undefined : role;
}

function mergePromptObjectState(current: PromptObjectState | undefined, next: PromptObjectState): PromptObjectState {
  return promptStatePriority(next) >= promptStatePriority(current) ? next : current ?? next;
}

function promptStatePriority(state: PromptObjectState | undefined): number {
  switch (state) {
    case "chosen":
      return 7;
    case "source":
      return 6;
    case "target":
      return 5;
    case "destination":
      return 4;
    case "optionalCost":
      return 3;
    case "enabled":
      return 2;
    case "disabled":
      return 1;
    default:
      return 0;
  }
}

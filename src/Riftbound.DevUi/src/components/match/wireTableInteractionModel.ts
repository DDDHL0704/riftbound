import type { CandidateSelectionDraft } from "../../utils/candidateSelectionDraft";
import {
  promptChoiceRoleLabel,
  promptChoiceRoleOrder,
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

export type WireTableObjectHint = {
  candidateLabels: string[];
  choiceLabels: string[];
  dataLabel: string;
  disabledCandidateCount: number;
  enabledCandidateCount: number;
  nextClickLabel: string;
  objectId: string;
  roleLabels: string[];
  roles: PromptChoiceRole[];
  semanticSummary: string;
  state: PromptObjectState;
};

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

export function buildWireObjectHintMap(
  model: PromptInteractionModel,
  focusedCandidates: PromptCandidateSummary[],
  focusedObjectId?: string,
  selectionDraft?: CandidateSelectionDraft
): Record<string, WireTableObjectHint | undefined> {
  const states = buildWireInteractionMap(model, focusedCandidates, focusedObjectId, selectionDraft);
  const allObjectIds = new Set([
    ...Object.keys(states),
    ...Array.from(model.objectById.keys())
  ]);
  const selectedChoiceIds = selectionDraftChoiceIds(selectionDraft);
  const hints: Record<string, WireTableObjectHint | undefined> = {};

  for (const objectId of allObjectIds) {
    const summary = model.objectById.get(objectId);
    const state = states[objectId] ?? summary?.state;
    if (!state) {
      continue;
    }

    const objectChoices = uniqueChoicesByRoleAndId(
      (summary?.choices ?? [])
        .filter((choice) => choice.role !== "mode")
        .filter((choice) => promptChoiceSummaryObjectIds(choice).includes(objectId))
    );
    const focusedChoices = uniqueChoicesByRoleAndId(
      focusedCandidates
        .filter((candidate) => candidate.enabled)
        .flatMap((candidate) => candidate.choices)
        .filter((choice) => choice.role !== "mode")
        .filter((choice) => promptChoiceSummaryObjectIds(choice).includes(objectId))
    );
    if (objectChoices.length === 0 && focusedChoices.length === 0) {
      continue;
    }

    const roles = orderedRoles(uniqueRoles([
      ...objectChoices.map((choice) => choice.role),
      ...focusedChoices.map((choice) => choice.role)
    ]));
    const roleLabels = roles.map(promptChoiceRoleLabel);
    const candidateLabels = uniqueLabels(model.candidates
      .filter((candidate) => candidate.choices.some((choice) =>
        choice.role !== "mode"
        && promptChoiceSummaryObjectIds(choice).includes(objectId)))
      .map((candidate) => candidate.label));
    const choiceLabels = uniqueLabels([
      ...focusedChoices.map((choice) => selectedChoiceIds.has(choice.id) ? `${choice.label} 已选` : choice.label),
      ...objectChoices.map((choice) => choice.label)
    ]).slice(0, 4);
    const nextClickLabel = objectNextClickLabel(state, roles);
    const semanticSummary = [
      nextClickLabel,
      roleLabels.length ? roleLabels.join("/") : "",
      candidateLabels.length ? candidateLabels.slice(0, 2).join(" / ") : "",
      choiceLabels.length ? choiceLabels.slice(0, 2).join(" / ") : ""
    ].filter(Boolean).join(" · ");

    hints[objectId] = {
      candidateLabels: candidateLabels.slice(0, 4),
      choiceLabels,
      dataLabel: [state, ...roles].join(" "),
      disabledCandidateCount: summary?.disabledCandidateCount ?? 0,
      enabledCandidateCount: summary?.enabledCandidateCount ?? 0,
      nextClickLabel,
      objectId,
      roleLabels,
      roles,
      semanticSummary,
      state
    };
  }

  return hints;
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

function selectionDraftChoiceIds(selectionDraft?: CandidateSelectionDraft): Set<string> {
  if (!selectionDraft) {
    return new Set();
  }

  return new Set([
    ...selectionDraft.targetChoiceIds,
    selectionDraft.destinationId,
    selectionDraft.mode,
    ...selectionDraft.optionalCostIds
  ].filter((id): id is string => Boolean(id)));
}

function uniqueChoicesByRoleAndId(choices: PromptChoiceSummary[]): PromptChoiceSummary[] {
  const seen = new Set<string>();
  return choices.filter((choice) => {
    const key = `${choice.role}:${choice.id}`;
    if (seen.has(key)) {
      return false;
    }
    seen.add(key);
    return true;
  });
}

function uniqueRoles(roles: PromptChoiceRole[]): PromptChoiceRole[] {
  return Array.from(new Set(roles));
}

function orderedRoles(roles: PromptChoiceRole[]): PromptChoiceRole[] {
  return [...roles].sort((left, right) => promptChoiceRoleOrder.indexOf(left) - promptChoiceRoleOrder.indexOf(right));
}

function uniqueLabels(labels: string[]): string[] {
  return Array.from(new Set(labels.map((label) => label.trim()).filter(Boolean)));
}

function objectNextClickLabel(state: PromptObjectState, roles: PromptChoiceRole[]): string {
  if (state === "disabled") {
    return "暂不可用";
  }

  if (state === "chosen") {
    return "已选择";
  }

  if (state === "source") {
    return "已选来源";
  }

  if (roles.includes("target") || state === "target") {
    return "选择目标";
  }

  if (roles.includes("destination") || state === "destination") {
    return "选择位置";
  }

  if (roles.includes("optionalCost") || state === "optionalCost") {
    return "选择费用";
  }

  if (roles.includes("source") || state === "enabled") {
    return "选择来源";
  }

  return "暂不可用";
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

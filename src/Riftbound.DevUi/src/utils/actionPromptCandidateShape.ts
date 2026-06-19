import type { ActionPromptCandidateDto } from "../types/protocol";

export function candidateRequiresFurtherChoice(candidate: ActionPromptCandidateDto): boolean {
  const selectionSteps = candidate.selectionSteps ?? [];
  if (selectionSteps.length > 0) {
    return selectionSteps.some((step) =>
      step.role !== "source"
      && (step.required || step.choices.length > 0));
  }

  return Boolean(
    (candidate.targets?.length ?? 0) > 0
    || (candidate.destinations?.length ?? 0) > 0
    || (candidate.modes?.length ?? 0) > 0
    || (candidate.optionalCosts?.length ?? 0) > 0
  );
}

export function singlePromptChoiceId(choices?: Array<{ id: string }> | null): string | undefined {
  if (!Array.isArray(choices) || choices.length !== 1) {
    return undefined;
  }

  const id = choices[0]?.id;
  return typeof id === "string" && id.trim().length > 0 ? id : undefined;
}

import { commandFieldDisplayLabel } from "./commandFieldDisplay";
import type { TableObjectCandidateContext } from "./tableObjectContext";

export type PromptCandidateFieldSummary = {
  fields: string[];
  requiredFields: string[];
  secondaryFields: string[];
};

export type PromptCandidateSemanticSummary = {
  commandFieldLabels: string[];
  commandTypes: string[];
  reasonLabels: string[];
  requiredCommandFieldLabels: string[];
  secondaryCommandFieldLabels: string[];
  selectionRoleLabels: string[];
};

export function commandFieldLabelsForCandidate(candidate: TableObjectCandidateContext): PromptCandidateFieldSummary {
  const requiredFields = uniqueStrings(candidate.requiredCommandFields.map(commandFieldDisplayLabel));
  const fields = uniqueStrings(candidate.commandFields.map(commandFieldDisplayLabel));
  const secondaryFields = fields.filter((field) => !requiredFields.includes(field));

  return {
    fields,
    requiredFields,
    secondaryFields
  };
}

export function summarizePromptCandidateSemantics(
  candidates: TableObjectCandidateContext[],
  { disabledReasonsOnly = false }: { disabledReasonsOnly?: boolean } = {}
): PromptCandidateSemanticSummary {
  const fieldSummaries = candidates.map(commandFieldLabelsForCandidate);
  const reasonCandidates = disabledReasonsOnly
    ? candidates.filter((candidate) => !candidate.enabled)
    : candidates;

  return {
    commandFieldLabels: uniqueStrings(fieldSummaries.flatMap((summary) => summary.fields)),
    commandTypes: uniqueStrings(candidates.map((candidate) => candidate.commandType ?? candidate.label).filter(isNonEmptyString)),
    reasonLabels: uniqueStrings(reasonCandidates.map((candidate) => candidate.reason).filter(isNonEmptyString)),
    requiredCommandFieldLabels: uniqueStrings(fieldSummaries.flatMap((summary) => summary.requiredFields)),
    secondaryCommandFieldLabels: uniqueStrings(fieldSummaries.flatMap((summary) => summary.secondaryFields)),
    selectionRoleLabels: uniqueStrings(candidates.flatMap((candidate) => candidate.roles).filter(isNonEmptyString))
  };
}

function isNonEmptyString(value: string | undefined): value is string {
  return Boolean(value?.trim());
}

function uniqueStrings(values: string[]): string[] {
  return [...new Set(values.map((value) => value.trim()).filter(Boolean))];
}

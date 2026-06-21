import type { ActionPromptDto } from "../types/protocol";

export type PromptCandidateCountSource = "candidates" | "server-flow";

export type PromptCandidateCounts = {
  candidateCount: number;
  disabledCandidateCount: number;
  enabledCandidateCount: number;
  source: PromptCandidateCountSource;
};

export function promptCandidateCounts(prompt?: ActionPromptDto): PromptCandidateCounts {
  const candidates = prompt?.candidates ?? [];
  const fallbackCandidateCount = candidates.length;
  const fallbackEnabledCandidateCount = candidates.filter((candidate) => candidate.enabled).length;
  const fallbackDisabledCandidateCount = candidates.filter((candidate) => !candidate.enabled).length;
  const serverFlow = prompt?.serverFlow;
  const hasServerFlowCount = isFiniteCount(serverFlow?.candidateCount)
    || isFiniteCount(serverFlow?.enabledCandidateCount)
    || isFiniteCount(serverFlow?.disabledCandidateCount);
  const candidateCount = normalizedCount(serverFlow?.candidateCount, fallbackCandidateCount);
  const enabledCandidateCount = normalizedCount(serverFlow?.enabledCandidateCount, fallbackEnabledCandidateCount);
  const disabledCandidateCount = normalizedCount(
    serverFlow?.disabledCandidateCount,
    hasServerFlowCount ? Math.max(0, candidateCount - enabledCandidateCount) : fallbackDisabledCandidateCount
  );

  return {
    candidateCount,
    disabledCandidateCount,
    enabledCandidateCount,
    source: hasServerFlowCount ? "server-flow" : "candidates"
  };
}

function normalizedCount(value: number | null | undefined, fallback: number): number {
  return isFiniteCount(value) ? Math.max(0, Math.floor(value)) : fallback;
}

function isFiniteCount(value: number | null | undefined): value is number {
  return typeof value === "number" && Number.isFinite(value);
}

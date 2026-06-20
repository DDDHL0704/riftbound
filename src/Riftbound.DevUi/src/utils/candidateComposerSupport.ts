import type { ActionPromptCandidateDto } from "../types/protocol";

export function canComposeActionCandidate(candidate: ActionPromptCandidateDto): boolean {
  if (candidate.composer) {
    return candidate.composer.supported && Boolean(candidate.commandTemplate);
  }

  return Boolean(candidate.commandTemplate);
}

import type { ActionPromptCandidateDto } from "../types/protocol";

const composerCandidateActions = new Set([
  "PLAY_CARD",
  "HIDE_CARD",
  "REVEAL_CARD",
  "MOVE_UNIT",
  "ASSEMBLE_EQUIPMENT",
  "DECLARE_BATTLE",
  "ACTIVATE_ABILITY",
  "LEGEND_ACT"
]);

export function canComposeActionCandidate(candidate: ActionPromptCandidateDto): boolean {
  return Boolean(candidate.commandTemplate) || composerCandidateActions.has(candidate.action);
}

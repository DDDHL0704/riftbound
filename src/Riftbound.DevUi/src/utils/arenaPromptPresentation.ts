export type ArenaPromptPresentationMode = "hidden" | "context" | "modal";

export type ArenaPromptPresentationPlan = {
  mode: ArenaPromptPresentationMode;
  anchorObjectId?: string;
};

const MODAL_PROMPTS = new Set([
  "MULLIGAN",
  "ASSIGN_COMBAT_DAMAGE",
  "ORDER_TRIGGERS",
  "PAY_COST",
  "HAND_CHOICE"
]);

export function buildArenaPromptPresentation(input: {
  actionable: boolean;
  promptType?: string;
  selectedObjectId?: string;
}): ArenaPromptPresentationPlan {
  if (input.actionable && input.promptType && MODAL_PROMPTS.has(input.promptType)) {
    return { mode: "modal", anchorObjectId: undefined };
  }

  if (input.selectedObjectId) {
    return { mode: "context", anchorObjectId: input.selectedObjectId };
  }

  return input.actionable
    ? { mode: "context", anchorObjectId: undefined }
    : { mode: "hidden", anchorObjectId: undefined };
}

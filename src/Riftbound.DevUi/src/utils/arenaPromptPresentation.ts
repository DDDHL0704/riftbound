export type ArenaPromptPresentationMode = "hidden" | "context" | "modal";

export type ArenaPromptPresentationPlan = {
  mode: ArenaPromptPresentationMode;
  anchorObjectId?: string;
};

const MODAL_PROMPTS = new Set([
  "MULLIGAN",
  "ASSIGN_COMBAT_DAMAGE",
  "ORDER_TRIGGERS"
]);

export function buildArenaPromptPresentation(input: {
  actionable: boolean;
  promptType?: string;
  selectedObjectId?: string;
}): ArenaPromptPresentationPlan {
  if (!input.actionable) {
    return { mode: "hidden", anchorObjectId: undefined };
  }

  if (input.promptType && MODAL_PROMPTS.has(input.promptType)) {
    return { mode: "modal", anchorObjectId: undefined };
  }

  return { mode: "context", anchorObjectId: input.selectedObjectId };
}

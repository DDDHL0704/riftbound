import type { ActionPromptCandidateDto, ActionPromptDto } from "../types/protocol";

export type ActionPanelRenderEntryKind =
  | "candidate-button"
  | "damage-assignment"
  | "hand-choice"
  | "mulligan"
  | "order-triggers";

export type ActionPanelRenderState = "blocked" | "disabled" | "empty" | "ready" | "readonly";

export type ActionPanelRenderEntry = {
  canAct: boolean;
  candidate?: ActionPromptCandidateDto;
  key: string;
  kind: ActionPanelRenderEntryKind;
  readOnly: boolean;
};

export type ActionPanelRenderPlan = {
  emptyLabel: string;
  entries: ActionPanelRenderEntry[];
  promptType: string;
  state: ActionPanelRenderState;
};

export type BuildActionPanelRenderPlanOptions = {
  canAct: boolean;
  connected: boolean;
  prompt?: ActionPromptDto;
};

export function buildActionPanelRenderPlan({
  canAct,
  connected,
  prompt
}: BuildActionPanelRenderPlanOptions): ActionPanelRenderPlan {
  const allCandidates = prompt?.candidates ?? [];
  const enabledCandidates = allCandidates.filter((candidate) => candidate.enabled);
  const promptType = prompt?.view?.type?.trim() ?? "";
  const entries = [
    ...readonlyEntriesForPrompt(promptType, allCandidates, enabledCandidates),
    ...allCandidates.map((candidate, index) => entryForCandidate({
      canAct,
      candidate,
      connected,
      index,
      readOnly: false
    }))
  ];

  return {
    emptyLabel: "服务端暂未提供可提交候选。",
    entries,
    promptType: promptType || "无",
    state: renderStateFor(entries, connected, canAct)
  };
}

function readonlyEntriesForPrompt(
  promptType: string,
  allCandidates: ActionPromptCandidateDto[],
  enabledCandidates: ActionPromptCandidateDto[]
): ActionPanelRenderEntry[] {
  if (promptType === "ORDER_TRIGGERS" && !enabledCandidates.some((candidate) => candidate.action === "ORDER_TRIGGERS")) {
    if (allCandidates.some((candidate) => candidate.action === "ORDER_TRIGGERS")) {
      return [];
    }

    return [readonlyEntry("order-triggers", allCandidates.find((candidate) => candidate.action === "ORDER_TRIGGERS"))];
  }

  if (promptType === "HAND_CHOICE" && !enabledCandidates.some((candidate) => candidate.action === "CHOOSE_HAND_CARDS")) {
    if (allCandidates.some((candidate) => candidate.action === "CHOOSE_HAND_CARDS")) {
      return [];
    }

    return [readonlyEntry("hand-choice", allCandidates.find((candidate) => candidate.action === "CHOOSE_HAND_CARDS"))];
  }

  return [];
}

function readonlyEntry(
  kind: Extract<ActionPanelRenderEntryKind, "hand-choice" | "order-triggers">,
  candidate: ActionPromptCandidateDto | undefined
): ActionPanelRenderEntry {
  return {
    canAct: false,
    candidate,
    key: `readonly-${kind}-${candidate?.label ?? "prompt"}`,
    kind,
    readOnly: true
  };
}

function entryForCandidate({
  canAct,
  candidate,
  connected,
  index,
  readOnly
}: {
  canAct: boolean;
  candidate: ActionPromptCandidateDto;
  connected: boolean;
  index: number;
  readOnly: boolean;
}): ActionPanelRenderEntry {
  return {
    canAct: connected && canAct && !readOnly && candidate.enabled,
    candidate,
    key: `${candidate.action}-${candidate.label ?? "candidate"}-${index}`,
    kind: entryKindForAction(candidate.action),
    readOnly
  };
}

function entryKindForAction(action: string): ActionPanelRenderEntryKind {
  switch (action) {
    case "ASSIGN_COMBAT_DAMAGE":
      return "damage-assignment";
    case "CHOOSE_HAND_CARDS":
      return "hand-choice";
    case "MULLIGAN":
      return "mulligan";
    case "ORDER_TRIGGERS":
      return "order-triggers";
    default:
      return "candidate-button";
  }
}

function renderStateFor(
  entries: ActionPanelRenderEntry[],
  connected: boolean,
  canAct: boolean
): ActionPanelRenderState {
  if (entries.length === 0) {
    return "empty";
  }

  if (!connected) {
    return "disabled";
  }

  if (!canAct || entries.every((entry) => entry.readOnly)) {
    return "readonly";
  }

  if (!entries.some((entry) => entry.canAct)) {
    return "blocked";
  }

  return "ready";
}

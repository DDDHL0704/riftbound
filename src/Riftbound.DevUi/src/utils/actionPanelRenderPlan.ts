import type { ActionPromptCandidateDto, ActionPromptDto } from "../types/protocol";

export type ActionPanelRenderEntryKind =
  | "battle-declaration"
  | "candidate-button"
  | "damage-assignment"
  | "hand-choice"
  | "mulligan"
  | "order-triggers"
  | "pay-cost";

export type ActionPanelRenderState = "blocked" | "disabled" | "empty" | "ready" | "readonly";

export type ActionPanelSubmitGateState =
  | "ready"
  | "readonly"
  | "server-blocked"
  | "submission-gate-blocked"
  | "window-blocked";

export type ActionPanelSubmitGate = {
  canSubmit: boolean;
  reason: string;
  state: ActionPanelSubmitGateState;
  stateLabel: string;
  title?: string;
};

export type ActionPanelRenderEntry = {
  canAct: boolean;
  candidate?: ActionPromptCandidateDto;
  key: string;
  kind: ActionPanelRenderEntryKind;
  readOnly: boolean;
  submitGate: ActionPanelSubmitGate;
};

export type ActionPanelRenderPlan = {
  emptyLabel: string;
  entries: ActionPanelRenderEntry[];
  promptType: string;
  state: ActionPanelRenderState;
};

export type BuildActionPanelRenderPlanOptions = {
  canAct: boolean;
  connected?: boolean;
  prompt?: ActionPromptDto;
  submissionGate?: {
    canSubmit: boolean;
    reason: string;
    stateLabel: string;
  };
};

export function buildActionPanelRenderPlan({
  canAct,
  connected,
  prompt,
  submissionGate
}: BuildActionPanelRenderPlanOptions): ActionPanelRenderPlan {
  const allCandidates = prompt?.candidates ?? [];
  const enabledCandidates = allCandidates.filter((candidate) => candidate.enabled);
  const promptType = prompt?.view?.type?.trim() ?? "";
  const baseSubmitGate = submissionGate ?? fallbackSubmissionGate(connected ?? true);
  const entries = [
    ...readonlyEntriesForPrompt(promptType, allCandidates, enabledCandidates),
    ...[...allCandidates].sort(candidatePresentationSort).map((candidate, index) => entryForCandidate({
      canAct,
      candidate,
      index,
      readOnly: false,
      submissionGate: baseSubmitGate
    }))
  ];

  return {
    emptyLabel: "服务端暂未提供可提交候选。",
    entries,
    promptType: promptType || "无",
    state: renderStateFor(entries, baseSubmitGate.canSubmit, canAct)
  };
}

function candidatePresentationSort(left: ActionPromptCandidateDto, right: ActionPromptCandidateDto): number {
  const leftPriority = normalizedCandidatePriority(left);
  const rightPriority = normalizedCandidatePriority(right);
  return Number(right.enabled) - Number(left.enabled)
    || leftPriority - rightPriority
    || left.action.localeCompare(right.action);
}

function normalizedCandidatePriority(candidate: ActionPromptCandidateDto): number {
  const priority = candidate.presentation?.priority;
  return typeof priority === "number" && Number.isFinite(priority) ? priority : 700;
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
    readOnly: true,
    submitGate: readonlySubmitGate()
  };
}

function entryForCandidate({
  canAct,
  candidate,
  index,
  readOnly,
  submissionGate
}: {
  canAct: boolean;
  candidate: ActionPromptCandidateDto;
  index: number;
  readOnly: boolean;
  submissionGate: NonNullable<BuildActionPanelRenderPlanOptions["submissionGate"]>;
}): ActionPanelRenderEntry {
  const submitGate = submitGateForCandidate({ canAct, candidate, readOnly, submissionGate });

  return {
    canAct: submitGate.canSubmit,
    candidate,
    key: `${candidate.action}-${candidate.label ?? "candidate"}-${index}`,
    kind: entryKindForAction(candidate.action),
    readOnly,
    submitGate
  };
}

function submitGateForCandidate({
  canAct,
  candidate,
  readOnly,
  submissionGate
}: {
  canAct: boolean;
  candidate: ActionPromptCandidateDto;
  readOnly: boolean;
  submissionGate: NonNullable<BuildActionPanelRenderPlanOptions["submissionGate"]>;
}): ActionPanelSubmitGate {
  if (!submissionGate.canSubmit) {
    return {
      canSubmit: false,
      reason: submissionGate.reason,
      state: "submission-gate-blocked",
      stateLabel: submissionGate.stateLabel,
      title: submissionGate.reason
    };
  }

  if (readOnly) {
    return readonlySubmitGate();
  }

  if (!canAct) {
    return {
      canSubmit: false,
      reason: "当前行动窗口不能提交该候选。",
      state: "window-blocked",
      stateLabel: "窗口不可提交",
      title: "当前行动窗口不能提交该候选"
    };
  }

  if (!candidate.enabled) {
    const reason = candidate.reason?.trim() || "服务端候选暂不可提交。";
    return {
      canSubmit: false,
      reason,
      state: "server-blocked",
      stateLabel: "服务端阻断",
      title: reason
    };
  }

  return {
    canSubmit: true,
    reason: candidate.reason?.trim() || "服务端候选可提交。",
    state: "ready",
    stateLabel: "可提交",
    title: candidate.reason?.trim() || undefined
  };
}

function readonlySubmitGate(): ActionPanelSubmitGate {
  return {
    canSubmit: false,
    reason: "当前提示只读。",
    state: "readonly",
    stateLabel: "只读",
    title: "当前提示只读"
  };
}

function fallbackSubmissionGate(connected: boolean): NonNullable<BuildActionPanelRenderPlanOptions["submissionGate"]> {
  if (connected) {
    return {
      canSubmit: true,
      reason: "提交入口已就绪。",
      stateLabel: "可提交"
    };
  }

  return {
    canSubmit: false,
    reason: "行动入口未就绪，等待服务端窗口、连接或快照同步。",
    stateLabel: "入口未就绪"
  };
}

function entryKindForAction(action: string): ActionPanelRenderEntryKind {
  switch (action) {
    case "ASSIGN_COMBAT_DAMAGE":
      return "damage-assignment";
    case "DECLARE_BATTLE":
      return "battle-declaration";
    case "CHOOSE_HAND_CARDS":
      return "hand-choice";
    case "MULLIGAN":
      return "mulligan";
    case "ORDER_TRIGGERS":
      return "order-triggers";
    case "PAY_COST":
      return "pay-cost";
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

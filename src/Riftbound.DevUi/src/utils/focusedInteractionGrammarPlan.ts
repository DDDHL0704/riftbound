import type { CandidateSelectionDraft } from "./candidateSelectionDraft";
import {
  promptChoiceRoleLabel,
  promptChoiceRoleOrder,
  type PromptCandidateSummary,
  type PromptChoiceRole
} from "./promptInteraction";

export type FocusedInteractionGrammarState = "blocked" | "empty" | "incomplete" | "ready";

export type FocusedInteractionGrammarStepState =
  | "available"
  | "blocked"
  | "locked"
  | "missing"
  | "optional"
  | "ready"
  | "selected"
  | "skipped";

export type FocusedInteractionGrammarStep = {
  availableCount: number;
  key: string;
  label: string;
  required: boolean;
  role: PromptChoiceRole | "submit";
  sampleLabels: string[];
  selectedCount: number;
  state: FocusedInteractionGrammarStepState;
  stateLabel: string;
};

export type FocusedInteractionGrammarPlan = {
  candidateKey?: string;
  candidateLabel: string;
  commandFieldCount: number;
  commandType?: string;
  enabled: boolean;
  missingRequiredCount: number;
  nextStepLabel: string;
  state: FocusedInteractionGrammarState;
  stateLabel: string;
  steps: FocusedInteractionGrammarStep[];
  summary: string;
};

const roleOrder: Array<PromptChoiceRole | "submit"> = [...promptChoiceRoleOrder, "submit"];

const roleLabels: Record<"submit", string> = {
  submit: "提交",
};

export function buildFocusedInteractionGrammarPlan({
  candidates,
  disabledByConnection,
  selectionDraft,
  sourceObjectId
}: {
  candidates: PromptCandidateSummary[];
  disabledByConnection: boolean;
  selectionDraft?: CandidateSelectionDraft;
  sourceObjectId?: string;
}): FocusedInteractionGrammarPlan {
  const candidate = selectedCandidate(candidates, selectionDraft);
  if (!candidate) {
    return {
      candidateLabel: "无焦点候选",
      commandFieldCount: 0,
      enabled: false,
      missingRequiredCount: 0,
      nextStepLabel: "点击含服务端候选的卡牌",
      state: "empty",
      stateLabel: "无候选",
      steps: [],
      summary: "当前焦点没有服务端候选"
    };
  }

  const candidateKey = candidateGrammarKey(candidate);
  const draftApplies = selectionDraft?.candidateKey === candidateKey
    && (!sourceObjectId || selectionDraft.sourceObjectId === sourceObjectId);
  const steps = grammarSteps(candidate, draftApplies ? selectionDraft : undefined, sourceObjectId);
  const missingRequiredCount = steps.filter((step) => step.required && (step.state === "available" || step.state === "missing")).length;
  const submitBlocked = disabledByConnection || !candidate.enabled || missingRequiredCount > 0;
  const submitStep = submitGrammarStep({
    blocked: submitBlocked,
    commandFieldCount: candidate.command?.bindings.length ?? 0,
    disabledByConnection,
    enabled: candidate.enabled,
    missingRequiredCount
  });
  const allSteps = [...steps, submitStep].sort((left, right) => roleOrder.indexOf(left.role) - roleOrder.indexOf(right.role));
  const nextStepLabel = nextGrammarStepLabel(allSteps);
  const state = grammarState({
    disabledByConnection,
    enabled: candidate.enabled,
    missingRequiredCount
  });

  return {
    candidateKey,
    candidateLabel: candidate.label,
    commandFieldCount: candidate.command?.bindings.length ?? 0,
    commandType: candidate.command?.cmdType,
    enabled: candidate.enabled,
    missingRequiredCount,
    nextStepLabel,
    state,
    stateLabel: grammarStateLabel(state),
    steps: allSteps,
    summary: `${candidate.label} / ${grammarStateLabel(state)} / ${nextStepLabel}`
  };
}

export function candidateGrammarKey(candidate: Pick<PromptCandidateSummary, "action" | "label">): string {
  return `${candidate.action}::${candidate.label}`;
}

function selectedCandidate(
  candidates: PromptCandidateSummary[],
  selectionDraft?: CandidateSelectionDraft
): PromptCandidateSummary | undefined {
  if (selectionDraft) {
    const drafted = candidates.find((candidate) => candidateGrammarKey(candidate) === selectionDraft.candidateKey);
    if (drafted) {
      return drafted;
    }
  }

  return candidates.find((candidate) => candidate.enabled) ?? candidates[0];
}

function grammarSteps(
  candidate: PromptCandidateSummary,
  selectionDraft: CandidateSelectionDraft | undefined,
  sourceObjectId: string | undefined
): FocusedInteractionGrammarStep[] {
  const byRole = new Map<PromptChoiceRole, PromptCandidateSummary["steps"][number]>();
  for (const step of candidate.steps) {
    const existing = byRole.get(step.role);
    if (!existing || (Number(step.required) - Number(existing.required)) > 0 || step.count > existing.count) {
      byRole.set(step.role, step);
    }
  }

  if (sourceObjectId && !byRole.has("source")) {
    byRole.set("source", {
      count: 1,
      label: "来源",
      required: true,
      role: "source",
      sampleLabels: []
    });
  }

  return roleOrder
    .filter((role): role is PromptChoiceRole => role !== "submit")
    .map((role) => {
      const step = byRole.get(role);
      const selectedCount = selectedCountForRole(role, selectionDraft, sourceObjectId);
      const required = Boolean(step?.required || (role === "source" && sourceObjectId));
      const availableCount = step?.count ?? (sourceObjectId && role === "source" ? 1 : 0);
      const state = stepState({
        availableCount,
        required,
        role,
        selectedCount
      });

      return {
        availableCount,
        key: `${candidate.action}:${role}`,
        label: step?.label || promptChoiceRoleLabel(role),
        required,
        role,
        sampleLabels: step?.sampleLabels ?? [],
        selectedCount,
        state,
        stateLabel: stepStateLabel(state)
      } satisfies FocusedInteractionGrammarStep;
    })
    .filter((step) => step.required || step.availableCount > 0 || step.selectedCount > 0);
}

function selectedCountForRole(
  role: PromptChoiceRole,
  selectionDraft: CandidateSelectionDraft | undefined,
  sourceObjectId: string | undefined
): number {
  switch (role) {
    case "source":
      return sourceObjectId || selectionDraft?.sourceObjectId ? 1 : 0;
    case "mode":
      return selectionDraft?.mode ? 1 : 0;
    case "destination":
      return selectionDraft?.destinationId ? 1 : 0;
    case "target":
      return selectionDraft?.targetChoiceIds.length ?? 0;
    case "optionalCost":
      return selectionDraft?.optionalCostIds.length ?? 0;
  }
}

function stepState({
  availableCount,
  required,
  role,
  selectedCount
}: {
  availableCount: number;
  required: boolean;
  role: PromptChoiceRole;
  selectedCount: number;
}): FocusedInteractionGrammarStepState {
  if (selectedCount > 0) {
    return role === "source" ? "locked" : "selected";
  }

  if (required && availableCount <= 0) {
    return "missing";
  }

  if (required) {
    return "available";
  }

  if (availableCount > 0) {
    return "optional";
  }

  return "skipped";
}

function submitGrammarStep({
  blocked,
  commandFieldCount,
  disabledByConnection,
  enabled,
  missingRequiredCount
}: {
  blocked: boolean;
  commandFieldCount: number;
  disabledByConnection: boolean;
  enabled: boolean;
  missingRequiredCount: number;
}): FocusedInteractionGrammarStep {
  let state: FocusedInteractionGrammarStepState = "ready";
  let stateLabel = "可提交给服务端";
  if (blocked) {
    state = "blocked";
    stateLabel = disabledByConnection
      ? "等待连接恢复"
      : enabled
        ? `缺少 ${missingRequiredCount} 项`
        : "服务端阻断";
  }

  return {
    availableCount: commandFieldCount,
    key: "submit",
    label: "提交",
    required: true,
    role: "submit",
    sampleLabels: [],
    selectedCount: blocked ? 0 : 1,
    state,
    stateLabel
  };
}

function nextGrammarStepLabel(steps: FocusedInteractionGrammarStep[]): string {
  const missing = steps.find((step) => step.state === "missing");
  if (missing) {
    return `等待服务端提供${missing.label}`;
  }

  const available = steps.find((step) => step.required && step.state === "available");
  if (available) {
    return `选择${available.label}`;
  }

  const optional = steps.find((step) => step.state === "optional");
  if (optional) {
    return `可补充${optional.label}`;
  }

  const submit = steps.find((step) => step.role === "submit");
  return submit?.state === "ready" ? "提交服务端候选" : submit?.stateLabel ?? "等待服务端候选";
}

function grammarState({
  disabledByConnection,
  enabled,
  missingRequiredCount
}: {
  disabledByConnection: boolean;
  enabled: boolean;
  missingRequiredCount: number;
}): FocusedInteractionGrammarState {
  if (disabledByConnection || !enabled) {
    return "blocked";
  }

  if (missingRequiredCount > 0) {
    return "incomplete";
  }

  return "ready";
}

function grammarStateLabel(state: FocusedInteractionGrammarState): string {
  switch (state) {
    case "blocked":
      return "阻断";
    case "empty":
      return "无候选";
    case "incomplete":
      return "待选择";
    case "ready":
      return "可提交";
  }
}

function stepStateLabel(state: FocusedInteractionGrammarStepState): string {
  switch (state) {
    case "available":
      return "待选择";
    case "blocked":
      return "阻断";
    case "locked":
      return "已锁定";
    case "missing":
      return "缺少";
    case "optional":
      return "可选";
    case "ready":
      return "就绪";
    case "selected":
      return "已选择";
    case "skipped":
      return "无需";
  }
}

import type { PromptCandidateSummary, PromptChoiceRole } from "./promptInteraction";

export type CandidateInteractionStepState = "available" | "missing-required" | "optional" | "satisfied";

export type CandidateInteractionStepPlan = {
  count: number;
  key: string;
  label: string;
  required: boolean;
  role: PromptChoiceRole;
  sampleLabels: string[];
  state: CandidateInteractionStepState;
  stateLabel: string;
};

export type CandidateInteractionPlan = {
  action: string;
  candidateLabel: string;
  commandFieldCount: number;
  commandType?: string;
  enabled: boolean;
  key: string;
  missingRequiredStepCount: number;
  nextRequiredStep?: CandidateInteractionStepPlan;
  optionalStepCount: number;
  requiredStepCount: number;
  stepRows: CandidateInteractionStepPlan[];
  summary: string;
};

export function buildCandidateInteractionPlans(candidates: PromptCandidateSummary[]): CandidateInteractionPlan[] {
  return candidates.map(candidateInteractionPlan).sort(compareCandidateInteractionPlans);
}

const roleLabels: Record<PromptChoiceRole, string> = {
  destination: "位置",
  mode: "模式",
  optionalCost: "费用",
  source: "来源",
  target: "目标"
};

function candidateInteractionPlan(candidate: PromptCandidateSummary): CandidateInteractionPlan {
  const stepRows = candidate.steps.map((step) => {
    const state = stepState(step.required, step.count);
    return {
      count: step.count,
      key: `${candidate.action}:${step.role}:${step.label}`,
      label: step.label || roleLabels[step.role],
      required: step.required,
      role: step.role,
      sampleLabels: step.sampleLabels,
      state,
      stateLabel: stepStateLabel(state)
    } satisfies CandidateInteractionStepPlan;
  });
  const requiredSteps = stepRows.filter((step) => step.required);
  const missingRequiredSteps = requiredSteps.filter((step) => step.state === "missing-required");
  const commandFieldCount = candidate.command?.bindings.length ?? 0;

  return {
    action: candidate.action,
    candidateLabel: candidate.label,
    commandFieldCount,
    commandType: candidate.command?.cmdType,
    enabled: candidate.enabled,
    key: `${candidate.action}:${candidate.label}`,
    missingRequiredStepCount: missingRequiredSteps.length,
    nextRequiredStep: missingRequiredSteps[0] ?? requiredSteps.find((step) => step.state === "available"),
    optionalStepCount: stepRows.filter((step) => !step.required).length,
    requiredStepCount: requiredSteps.length,
    stepRows,
    summary: candidateSummary(candidate, missingRequiredSteps.length, commandFieldCount)
  };
}

function candidateSummary(candidate: PromptCandidateSummary, missingRequiredStepCount: number, commandFieldCount: number): string {
  const state = candidate.enabled ? "可提交" : "不可提交";
  const missing = missingRequiredStepCount > 0 ? `缺口 ${missingRequiredStepCount}` : "缺口 0";
  return `${state} / ${missing} / 命令字段 ${commandFieldCount}`;
}

function stepState(required: boolean, count: number): CandidateInteractionStepState {
  if (required && count <= 0) {
    return "missing-required";
  }

  if (required) {
    return "available";
  }

  if (count > 0) {
    return "optional";
  }

  return "satisfied";
}

function stepStateLabel(state: CandidateInteractionStepState): string {
  switch (state) {
    case "available":
      return "可选取";
    case "missing-required":
      return "缺少必需项";
    case "optional":
      return "可补充";
    case "satisfied":
      return "无需选择";
  }
}

function compareCandidateInteractionPlans(left: CandidateInteractionPlan, right: CandidateInteractionPlan): number {
  const enabledDelta = Number(right.enabled) - Number(left.enabled);
  if (enabledDelta !== 0) {
    return enabledDelta;
  }

  const missingDelta = left.missingRequiredStepCount - right.missingRequiredStepCount;
  if (missingDelta !== 0) {
    return missingDelta;
  }

  const requiredDelta = right.requiredStepCount - left.requiredStepCount;
  if (requiredDelta !== 0) {
    return requiredDelta;
  }

  return left.candidateLabel.localeCompare(right.candidateLabel) || left.action.localeCompare(right.action);
}

import type { ActionPromptCandidateDto, GameCommand } from "../types/protocol";
import { candidateRequiresFurtherChoice } from "./actionPromptCandidateShape";
import { commandForSourceCandidate } from "./actionPromptCandidates";
import { canComposeActionCandidate } from "./candidateComposerSupport";
import { promptActionLabel, promptReasonTitle } from "./formatters";

export type SourceCandidateActionButtonVariant = "ghost" | "primary";

export type SourceCandidateActionPlan = {
  command?: GameCommand;
  disabled: boolean;
  label: string;
  labelSuffix: string;
  needsComposer: boolean;
  title: string;
  variant: SourceCandidateActionButtonVariant;
};

type BuildSourceCandidateActionPlanOptions = {
  canSubmitCommands: boolean;
  candidate: ActionPromptCandidateDto;
  disabledByConnection: boolean;
  sourceObjectId?: string;
};

export function buildSourceCandidateActionPlan({
  canSubmitCommands,
  candidate,
  disabledByConnection,
  sourceObjectId
}: BuildSourceCandidateActionPlanOptions): SourceCandidateActionPlan {
  const command = candidateRequiresFurtherChoice(candidate)
    ? undefined
    : commandForSourceCandidate(candidate, sourceObjectId);
  const needsComposer = !command && canComposeActionCandidate(candidate);
  const actionable = Boolean(command) || needsComposer;

  return {
    command,
    disabled: disabledByConnection || !candidate.enabled || !actionable || !canSubmitCommands,
    label: promptActionLabel(candidate),
    labelSuffix: !actionable && candidate.action !== "WAIT" ? "（需选择）" : "",
    needsComposer,
    title: sourceCandidateActionTitle({
      actionable,
      canSubmitCommands,
      candidate,
      disabledByConnection
    }),
    variant: candidate.enabled && actionable ? "primary" : "ghost"
  };
}

function sourceCandidateActionTitle({
  actionable,
  canSubmitCommands,
  candidate,
  disabledByConnection
}: {
  actionable: boolean;
  canSubmitCommands: boolean;
  candidate: ActionPromptCandidateDto;
  disabledByConnection: boolean;
}): string {
  if (disabledByConnection) {
    return "连接恢复前不能提交行动";
  }

  if (!canSubmitCommands) {
    return "当前视图只能查看，不能提交行动";
  }

  if (actionable) {
    return promptReasonTitle(candidate.reason) ?? "服务端候选";
  }

  return "该候选还需要服务端提供完整选择后才能提交";
}

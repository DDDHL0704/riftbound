import type { ActionPromptCandidateDto, GameCommand } from "../types/protocol";
import { candidateRequiresFurtherChoice } from "./actionPromptCandidateShape";
import { commandPlanForSourceCandidate } from "./actionPromptCandidates";
import { commandSourceCopy, type ActionPanelCandidateCommandSource } from "./actionPanelCommandPlan";
import { canComposeActionCandidate } from "./candidateComposerSupport";
import { promptActionLabel, promptReasonTitle } from "./formatters";

export type SourceCandidateActionButtonVariant = "ghost" | "primary";

export type SourceCandidateActionPlan = {
  command?: GameCommand;
  commandSource: ActionPanelCandidateCommandSource;
  commandSourceDetail: string;
  commandSourceLabel: string;
  disabled: boolean;
  label: string;
  labelSuffix: string;
  needsComposer: boolean;
  title: string;
  variant: SourceCandidateActionButtonVariant;
};

type BuildSourceCandidateActionPlanOptions = {
  actionGateReason?: string;
  canSubmitCommands: boolean;
  candidate: ActionPromptCandidateDto;
  disabledByActionGate?: boolean;
  disabledByConnection: boolean;
  sourceObjectId?: string;
};

export function buildSourceCandidateActionPlan({
  actionGateReason,
  canSubmitCommands,
  candidate,
  disabledByActionGate = false,
  disabledByConnection,
  sourceObjectId
}: BuildSourceCandidateActionPlanOptions): SourceCandidateActionPlan {
  const sourceCommandPlan: {
    command?: GameCommand;
    commandSource: ActionPanelCandidateCommandSource;
  } = candidateRequiresFurtherChoice(candidate)
    ? { commandSource: "unavailable" as const }
    : commandPlanForSourceCandidate(candidate, sourceObjectId);
  const command = sourceCommandPlan.command;
  const needsComposer = !command && canComposeActionCandidate(candidate);
  const commandSource = command
    ? sourceCommandPlan.commandSource
    : needsComposer
      ? "composer"
      : "unavailable";
  const sourceCopy = commandSourceCopy(commandSource);
  const actionable = Boolean(command) || needsComposer;

  return {
    command,
    commandSource,
    commandSourceDetail: sourceCopy.detail,
    commandSourceLabel: sourceCopy.label,
    disabled: disabledByConnection || disabledByActionGate || !candidate.enabled || !actionable || !canSubmitCommands,
    label: promptActionLabel(candidate),
    labelSuffix: !actionable && candidate.action !== "WAIT" ? "（需选择）" : "",
    needsComposer,
    title: sourceCandidateActionTitle({
      actionable,
      actionGateReason,
      canSubmitCommands,
      candidate,
      disabledByActionGate,
      disabledByConnection
    }),
    variant: candidate.enabled && actionable ? "primary" : "ghost"
  };
}

function sourceCandidateActionTitle({
  actionable,
  actionGateReason,
  canSubmitCommands,
  candidate,
  disabledByActionGate,
  disabledByConnection
}: {
  actionable: boolean;
  actionGateReason?: string;
  canSubmitCommands: boolean;
  candidate: ActionPromptCandidateDto;
  disabledByActionGate: boolean;
  disabledByConnection: boolean;
}): string {
  if (disabledByConnection) {
    return "提交入口未就绪，暂不能提交行动";
  }

  if (disabledByActionGate) {
    return actionGateReason ?? "当前行动窗口不能提交该候选";
  }

  if (!canSubmitCommands) {
    return "当前视图只能查看，不能提交行动";
  }

  if (actionable) {
    return promptReasonTitle(candidate.reason) ?? "服务端候选";
  }

  return "该候选还需要服务端提供完整选择后才能提交";
}

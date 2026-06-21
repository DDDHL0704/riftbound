import type { ActionPromptCandidateDto, GameCommand } from "../types/protocol";
import { commandFromActionPromptTemplate } from "./actionPromptCommandTemplate";
import { candidateRequiresFurtherChoice, singlePromptChoiceId } from "./actionPromptCandidateShape";
import { sourceRequirementFor } from "./actionPromptCandidates";
import { canComposeActionCandidate } from "./candidateComposerSupport";

export type ActionPanelDirectActionKind = "ready" | "submitDeck";

export type ActionPanelCandidateButtonIcon = "check" | "flag" | "hourglass" | "play" | "send";

export type ActionPanelCandidateButtonVariant = "danger" | "ghost" | "primary";

export type ActionPanelCandidateCommandPlan = {
  command?: GameCommand;
  directAction?: ActionPanelDirectActionKind;
  disabled: boolean;
  icon: ActionPanelCandidateButtonIcon;
  labelSuffix: string;
  needsComposer: boolean;
  variant: ActionPanelCandidateButtonVariant;
};

type BuildActionPanelCandidateCommandPlanOptions = {
  disabledByActionGate?: boolean;
  candidate: ActionPromptCandidateDto;
  disabledByConnection: boolean;
};

export function buildActionPanelCandidateCommandPlan({
  candidate,
  disabledByActionGate = false,
  disabledByConnection
}: BuildActionPanelCandidateCommandPlanOptions): ActionPanelCandidateCommandPlan {
  const command = simpleCommand(candidate);
  const directAction = directCandidateAction(candidate);
  const needsComposer = !command && !directAction && canComposeActionCandidate(candidate);
  const executable = command || directAction;
  const disabled = disabledByConnection || disabledByActionGate || !candidate.enabled || (!executable && !needsComposer);

  return {
    command,
    directAction,
    disabled,
    icon: candidateIcon(candidate, executable),
    labelSuffix: !executable && !needsComposer && candidate.action !== "WAIT" ? "（需选择）" : "",
    needsComposer,
    variant: candidateVariant(candidate)
  };
}

function directCandidateAction(candidate: ActionPromptCandidateDto): ActionPanelDirectActionKind | undefined {
  if (candidate.action === "SUBMIT_DECK") {
    return "submitDeck";
  }
  if (candidate.action === "READY") {
    return "ready";
  }
  return undefined;
}

function candidateIcon(
  candidate: ActionPromptCandidateDto,
  executable: GameCommand | ActionPanelDirectActionKind | undefined
): ActionPanelCandidateButtonIcon {
  const uiHint = candidate.presentation?.uiHint;
  if (candidate.action === "SUBMIT_DECK") {
    return "send";
  }
  if (candidate.action === "READY") {
    return "check";
  }
  if (uiHint === "danger" || candidate.action === "SURRENDER") {
    return "flag";
  }
  return executable ? "play" : "hourglass";
}

function candidateVariant(candidate: ActionPromptCandidateDto): ActionPanelCandidateButtonVariant {
  if (!candidate.enabled) {
    return "ghost";
  }

  switch (candidate.presentation?.uiHint) {
    case "danger":
      return "danger";
    case "readonly":
    case "secondary":
      return "ghost";
    default:
      return candidate.action === "SURRENDER" ? "danger" : "primary";
  }
}

function simpleCommand(candidate: ActionPromptCandidateDto): GameCommand | undefined {
  if (candidate.commandTemplate && !candidateRequiresFurtherChoice(candidate)) {
    const source = singlePromptChoiceId(candidate.sources);
    const templatedCommand = commandFromActionPromptTemplate(
      candidate.commandTemplate,
      { sourceId: source },
      { candidateMetadata: candidate.metadata, requirement: sourceRequirementFor(candidate, source) }
    );
    if (templatedCommand) {
      return templatedCommand;
    }
  }

  switch (candidate.action) {
    case "PASS_PRIORITY":
      return { cmdType: "PASS_PRIORITY" };
    case "PASS_FOCUS":
      return { cmdType: "PASS_FOCUS" };
    case "PASS":
      return { cmdType: "PASS" };
    case "END_TURN":
      return { cmdType: "END_TURN" };
    case "SURRENDER":
      return { cmdType: "SURRENDER" };
    case "PAY_COST":
      return payCostCommand(candidate);
    case "WAIT":
      return undefined;
    default:
      return undefined;
  }
}

function payCostCommand(candidate: ActionPromptCandidateDto): GameCommand | undefined {
  const metadata = candidate.metadata ?? {};
  const paymentId = stringMetadata(metadata, "paymentId");
  const paymentWindow = stringMetadata(metadata, "paymentWindow");
  const paymentChoiceIds = stringArrayMetadata(metadata, "paymentChoiceIds");
  if (!paymentId || !paymentWindow || paymentChoiceIds == null) {
    return undefined;
  }

  return { cmdType: "PAY_COST", paymentId, paymentWindow, paymentChoiceIds };
}

function stringMetadata(metadata: Record<string, unknown>, key: string): string | undefined {
  const value = metadata[key];
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : undefined;
}

function stringArrayMetadata(metadata: Record<string, unknown>, key: string): string[] | undefined {
  const value = metadata[key];
  return stringArrayFromValue(value);
}

function stringArrayFromValue(value: unknown): string[] | undefined {
  if (!Array.isArray(value)) {
    return undefined;
  }

  const values = value.map((item) => typeof item === "string" ? item.trim() : "");
  return values.every((item) => item.length > 0) ? values : undefined;
}

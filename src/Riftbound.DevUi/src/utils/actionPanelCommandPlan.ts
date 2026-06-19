import type { ActionPromptCandidateDto, GameCommand, SnapshotDto } from "../types/protocol";
import { commandFromActionPromptTemplate } from "./actionPromptCommandTemplate";
import { candidateRequiresFurtherChoice, singlePromptChoiceId } from "./actionPromptCandidateShape";
import { sourceRequirementFor } from "./actionPromptCandidates";
import { findCardNo } from "./actionPanelChoiceModels";
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
  candidate: ActionPromptCandidateDto;
  disabledByConnection: boolean;
  snapshot?: SnapshotDto;
};

export function buildActionPanelCandidateCommandPlan({
  candidate,
  disabledByConnection,
  snapshot
}: BuildActionPanelCandidateCommandPlanOptions): ActionPanelCandidateCommandPlan {
  const command = simpleCommand(candidate, snapshot);
  const directAction = directCandidateAction(candidate);
  const needsComposer = !command && !directAction && canComposeActionCandidate(candidate);
  const executable = command || directAction;
  const disabled = disabledByConnection || !candidate.enabled || (!executable && !needsComposer);

  return {
    command,
    directAction,
    disabled,
    icon: candidateIcon(candidate, executable),
    labelSuffix: !executable && !needsComposer && candidate.action !== "WAIT" ? "（需选择）" : "",
    needsComposer,
    variant: candidate.action === "SURRENDER" && candidate.enabled ? "danger" : candidate.enabled ? "primary" : "ghost"
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
  if (candidate.action === "SUBMIT_DECK") {
    return "send";
  }
  if (candidate.action === "READY") {
    return "check";
  }
  if (candidate.action === "SURRENDER") {
    return "flag";
  }
  return executable ? "play" : "hourglass";
}

function simpleCommand(candidate: ActionPromptCandidateDto, snapshot?: SnapshotDto): GameCommand | undefined {
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
      if (candidate.commandTemplate && !candidateRequiresFurtherChoice(candidate)) {
        const source = singlePromptChoiceId(candidate.sources);
        if (!source) {
          return undefined;
        }
        const templatedCommand = commandFromActionPromptTemplate(
          candidate.commandTemplate,
          { sourceId: source },
          sourceRequirementFor(candidate, source)
        );
        if (templatedCommand) {
          return templatedCommand;
        }
      }
      if (candidate.action === "PLAY_CARD" && !candidate.commandTemplate) {
        const source = singlePromptChoiceId(candidate.sources);
        if (!source) {
          return undefined;
        }
        const cardNo = findCardNo(snapshot, source);
        return cardNo && !candidateRequiresFurtherChoice(candidate)
          ? { cmdType: "PLAY_CARD", sourceObjectId: source, cardNo, targetObjectIds: [] }
          : undefined;
      }
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

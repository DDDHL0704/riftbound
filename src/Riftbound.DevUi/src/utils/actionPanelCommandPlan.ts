import type { ActionPromptCandidateDto, GameCommand } from "../types/protocol";
import { commandFromActionPromptTemplate } from "./actionPromptCommandTemplate";
import { candidateRequiresFurtherChoice, singlePromptChoiceId } from "./actionPromptCandidateShape";
import { sourceRequirementFor } from "./actionPromptCandidates";
import { canComposeActionCandidate } from "./candidateComposerSupport";

export type ActionPanelDirectActionKind = "ready" | "submitDeck";

export type ActionPanelCandidateButtonIcon = "check" | "flag" | "hourglass" | "play" | "send";

export type ActionPanelCandidateCommandSource =
  | "client-fallback"
  | "composer"
  | "direct-action"
  | "server-template"
  | "unavailable";

export type ActionPanelCandidateButtonVariant = "danger" | "ghost" | "primary";

export type ActionPanelCandidateCommandPlan = {
  command?: GameCommand;
  commandSource: ActionPanelCandidateCommandSource;
  commandSourceDetail: string;
  commandSourceLabel: string;
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
  const simple = simpleCommand(candidate);
  const command = simple.command;
  const directAction = directCandidateAction(candidate);
  const needsComposer = !command && !directAction && canComposeActionCandidate(candidate);
  const executable = command || directAction;
  const disabled = disabledByConnection || disabledByActionGate || !candidate.enabled || (!executable && !needsComposer);
  const commandSource = directAction
    ? "direct-action"
    : command
      ? simple.source
      : needsComposer
        ? "composer"
        : "unavailable";
  const sourceCopy = commandSourceCopy(commandSource);

  return {
    command,
    commandSource,
    commandSourceDetail: sourceCopy.detail,
    commandSourceLabel: sourceCopy.label,
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

function simpleCommand(candidate: ActionPromptCandidateDto): { command?: GameCommand; source: ActionPanelCandidateCommandSource } {
  if (candidate.commandTemplate && !candidateRequiresFurtherChoice(candidate)) {
    const source = singlePromptChoiceId(candidate.sources);
    const templatedCommand = commandFromActionPromptTemplate(
      candidate.commandTemplate,
      { sourceId: source },
      { candidateMetadata: candidate.metadata, requirement: sourceRequirementFor(candidate, source) }
    );
    if (templatedCommand) {
      return { command: templatedCommand, source: "server-template" };
    }
  }

  const fallback = (command: GameCommand | undefined) => ({ command, source: "client-fallback" as const });
  switch (candidate.action) {
    case "PASS_PRIORITY":
      return fallback({ cmdType: "PASS_PRIORITY" });
    case "PASS_FOCUS":
      return fallback({ cmdType: "PASS_FOCUS" });
    case "PASS":
      return fallback({ cmdType: "PASS" });
    case "END_TURN":
      return fallback({ cmdType: "END_TURN" });
    case "SURRENDER":
      return fallback({ cmdType: "SURRENDER" });
    case "PAY_COST":
      return fallback(payCostCommand(candidate));
    case "WAIT":
      return { source: "unavailable" };
    default:
      return { source: "unavailable" };
  }
}

export function commandSourceCopy(source: ActionPanelCandidateCommandSource): { detail: string; label: string } {
  switch (source) {
    case "server-template":
      return {
        detail: "按服务端 commandTemplate 生成，提交后仍由规则引擎校验。",
        label: "服务端模板"
      };
    case "client-fallback":
      return {
        detail: "兼容旧候选的内置命令，提交后仍由规则引擎校验。",
        label: "前端内置"
      };
    case "direct-action":
      return {
        detail: "房间或准备类入口，不伪装成规则命令。",
        label: "本地入口"
      };
    case "composer":
      return {
        detail: "先选择来源、目标或模式，再按服务端模板提交。",
        label: "服务端组合"
      };
    case "unavailable":
      return {
        detail: "当前候选没有可提交命令或完整组合计划。",
        label: "等待服务端"
      };
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

import type { ActionPromptCandidateDto, ActionPromptDto, GameCommand, SnapshotDto } from "../types/protocol";
import { promptStampedCommand } from "./actionPromptCandidates";
import {
  buildActionPanelCandidateCommandPlan,
  type ActionPanelCandidateCommandSource,
  type ActionPanelDirectActionKind
} from "./actionPanelCommandPlan";
import type { CommandSubmissionUiSource } from "./commandSubmissionFollowupPlan";
import { buildServerSubmissionGatePlan, type ServerSubmissionGatePlan } from "./serverSubmissionGatePlan";

export type ServerQuickActionId = "endTurn" | "pass" | "ready" | "submitDeck" | "surrender";

export type ServerQuickActionState = "blocked" | "disconnected" | "missing" | "readonly" | "ready";

export type ServerQuickActionEntry = {
  candidateAction?: string;
  command?: GameCommand;
  commandSource: ActionPanelCandidateCommandSource;
  commandSourceDetail: string;
  commandSourceLabel: string;
  directAction?: ActionPanelDirectActionKind;
  disabled: boolean;
  id: ServerQuickActionId;
  label: string;
  state: ServerQuickActionState;
  title: string;
  variant: "danger" | "secondary";
};

export type ServerQuickActionPlan = {
  entries: ServerQuickActionEntry[];
};

type ServerQuickActionDefinition = {
  actions: string[];
  id: ServerQuickActionId;
  label: string;
  missingTitle: string;
  variant: "danger" | "secondary";
};

const quickActionDefinitions: ServerQuickActionDefinition[] = [
  {
    actions: ["READY"],
    id: "ready",
    label: "准备",
    missingTitle: "当前服务端没有提供准备候选。",
    variant: "secondary"
  },
  {
    actions: ["SUBMIT_DECK"],
    id: "submitDeck",
    label: "导入构筑",
    missingTitle: "当前服务端没有提供提交构筑候选。",
    variant: "secondary"
  },
  {
    actions: ["PASS_FOCUS", "PASS_PRIORITY", "PASS"],
    id: "pass",
    label: "跳过",
    missingTitle: "当前服务端没有提供跳过候选。",
    variant: "secondary"
  },
  {
    actions: ["END_TURN"],
    id: "endTurn",
    label: "结束回合",
    missingTitle: "当前服务端没有提供结束回合候选。",
    variant: "secondary"
  },
  {
    actions: ["SURRENDER"],
    id: "surrender",
    label: "投降",
    missingTitle: "当前服务端没有提供投降候选。",
    variant: "danger"
  }
];

export function buildServerQuickActionPlan({
  canAct,
  connected,
  ids,
  prompt,
  submissionGate,
  snapshot
}: {
  canAct: boolean;
  connected: boolean;
  ids?: readonly ServerQuickActionId[];
  prompt?: ActionPromptDto;
  submissionGate?: ServerSubmissionGatePlan;
  snapshot?: SnapshotDto;
}): ServerQuickActionPlan {
  const includedIds = ids ? new Set(ids) : undefined;
  const gate = submissionGate ?? buildServerSubmissionGatePlan({
    connectionStatus: connected ? "connected" : "disconnected",
    prompt,
    snapshot
  });
  return {
    entries: quickActionDefinitions
      .filter((definition) => !includedIds || includedIds.has(definition.id))
      .map((definition) => quickActionEntryForDefinition({
        canAct,
        candidate: candidateForDefinition(prompt?.candidates ?? [], definition),
        connected,
        definition,
        prompt,
        submissionGate: gate,
        snapshot
      }))
  };
}

export function quickActionCommandUiSource(entry: ServerQuickActionEntry): Partial<CommandSubmissionUiSource> {
  return {
    candidateAction: entry.candidateAction,
    candidateLabel: entry.label,
    commandSource: entry.commandSource,
    commandSourceDetail: entry.commandSourceDetail,
    commandSourceLabel: entry.commandSourceLabel,
    label: entry.label
  };
}

function quickActionEntryForDefinition({
  canAct,
  candidate,
  connected,
  definition,
  prompt,
  submissionGate,
  snapshot
}: {
  canAct: boolean;
  candidate?: ActionPromptCandidateDto;
  connected: boolean;
  definition: ServerQuickActionDefinition;
  prompt?: ActionPromptDto;
  submissionGate: ServerSubmissionGatePlan;
  snapshot?: SnapshotDto;
}): ServerQuickActionEntry {
  if (!candidate) {
    return {
      commandSource: "unavailable",
      commandSourceDetail: definition.missingTitle,
      commandSourceLabel: "等待服务端",
      disabled: true,
      id: definition.id,
      label: definition.label,
      state: "missing",
      title: definition.missingTitle,
      variant: definition.variant
    };
  }

  const commandPlan = buildActionPanelCandidateCommandPlan({
    candidate,
    disabledByConnection: !submissionGate.canSubmit || !canAct
  });
  const command = commandPlan.command ? promptStampedCommand(commandPlan.command, prompt) : undefined;
  const executable = Boolean(command || commandPlan.directAction);
  const disabled = commandPlan.disabled || !executable;

  return {
    candidateAction: candidate.action,
    command,
    commandSource: commandPlan.commandSource,
    commandSourceDetail: commandPlan.commandSourceDetail,
    commandSourceLabel: commandPlan.commandSourceLabel,
    directAction: commandPlan.directAction,
    disabled,
    id: definition.id,
    label: definition.label,
    state: quickActionState({ canAct, candidate, connected, disabled, executable, submissionGate }),
    title: quickActionTitle({ canAct, candidate, connected, executable, submissionGate }),
    variant: definition.variant
  };
}

function candidateForDefinition(
  candidates: ActionPromptCandidateDto[],
  definition: ServerQuickActionDefinition
): ActionPromptCandidateDto | undefined {
  for (const action of definition.actions) {
    const enabled = candidates.find((candidate) => candidate.action === action && candidate.enabled);
    if (enabled) {
      return enabled;
    }
  }

  return candidates.find((candidate) => definition.actions.includes(candidate.action));
}

function quickActionState({
  canAct,
  candidate,
  connected,
  disabled,
  executable,
  submissionGate
}: {
  canAct: boolean;
  candidate: ActionPromptCandidateDto;
  connected: boolean;
  disabled: boolean;
  executable: boolean;
  submissionGate: ServerSubmissionGatePlan;
}): ServerQuickActionState {
  if (!connected || submissionGate.state === "disconnected" || submissionGate.state === "resyncing") {
    return "disconnected";
  }

  if (!canAct) {
    return "readonly";
  }

  if (!submissionGate.canSubmit || disabled || !candidate.enabled || !executable) {
    return "blocked";
  }

  return "ready";
}

function quickActionTitle({
  canAct,
  candidate,
  connected,
  executable,
  submissionGate
}: {
  canAct: boolean;
  candidate: ActionPromptCandidateDto;
  connected: boolean;
  executable: boolean;
  submissionGate: ServerSubmissionGatePlan;
}): string {
  if (!connected || !submissionGate.canSubmit) {
    return submissionGate.reason;
  }

  if (!canAct) {
    return "当前不是你的服务端行动窗口。";
  }

  if (!candidate.enabled) {
    return candidate.reason || "服务端暂未开放该操作。";
  }

  if (!executable) {
    return "该候选需要在右侧操作区补充选择。";
  }

  return candidate.reason || "提交服务端候选操作。";
}

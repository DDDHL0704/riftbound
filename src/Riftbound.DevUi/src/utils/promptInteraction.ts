import type { ActionPromptCandidateDto, ActionPromptChoiceDto, ActionPromptDto } from "../types/protocol";
import { promptActionLabel, promptReasonLabel } from "./formatters";
import { redactInternalText } from "./redaction";

export type PromptChoiceRole = "source" | "target" | "destination" | "mode" | "optionalCost";
export type PromptObjectState = "enabled" | "disabled";

export type PromptChoiceSummary = {
  id: string;
  label: string;
  reason?: string;
  role: PromptChoiceRole;
};

export type PromptCandidateSummary = {
  action: string;
  enabled: boolean;
  label: string;
  reason: string;
  choices: PromptChoiceSummary[];
};

export type PromptObjectSummary = {
  choices: PromptChoiceSummary[];
  disabledCandidateCount: number;
  enabledCandidateCount: number;
  objectId: string;
  state: PromptObjectState;
};

export type PromptInteractionModel = {
  candidates: PromptCandidateSummary[];
  disabledObjectIds: Set<string>;
  enabledObjectIds: Set<string>;
  objectById: Map<string, PromptObjectSummary>;
};

const choiceGroups: Array<{ key: keyof ActionPromptCandidateDto; role: PromptChoiceRole }> = [
  { key: "sources", role: "source" },
  { key: "targets", role: "target" },
  { key: "destinations", role: "destination" },
  { key: "modes", role: "mode" },
  { key: "optionalCosts", role: "optionalCost" }
];

const roleLabels: Record<PromptChoiceRole, string> = {
  destination: "位置",
  mode: "模式",
  optionalCost: "费用",
  source: "来源",
  target: "目标"
};

export function buildPromptInteractionModel(prompt?: ActionPromptDto): PromptInteractionModel {
  const objectById = new Map<string, PromptObjectSummary>();
  const candidates = (prompt?.candidates ?? []).map((candidate) => {
    const choices = candidateChoices(candidate);
    for (const choice of choices) {
      for (const objectId of candidateChoiceObjectIds(choice.id)) {
        const existing = objectById.get(objectId) ?? {
          choices: [],
          disabledCandidateCount: 0,
          enabledCandidateCount: 0,
          objectId,
          state: "disabled" as const
        };
        existing.choices.push(choice);
        if (candidate.enabled) {
          existing.enabledCandidateCount += 1;
          existing.state = "enabled";
        } else {
          existing.disabledCandidateCount += 1;
        }
        objectById.set(objectId, existing);
      }
    }

    return {
      action: candidate.action,
      enabled: candidate.enabled,
      label: promptActionLabel(candidate),
      reason: promptReasonLabel(candidate.reason, candidate.enabled ? "可提交" : "暂不可提交"),
      choices
    };
  });

  const enabledObjectIds = new Set<string>();
  const disabledObjectIds = new Set<string>();
  for (const [objectId, summary] of objectById) {
    if (summary.enabledCandidateCount > 0) {
      enabledObjectIds.add(objectId);
    } else {
      disabledObjectIds.add(objectId);
    }
  }

  return {
    candidates,
    disabledObjectIds,
    enabledObjectIds,
    objectById
  };
}

export function promptObjectState(model: PromptInteractionModel, objectId?: string): PromptObjectState | undefined {
  if (!objectId) {
    return undefined;
  }

  return model.objectById.get(objectId)?.state;
}

export function promptChoiceRoleLabel(role: PromptChoiceRole): string {
  return roleLabels[role];
}

export function promptChoiceLabel(choice: ActionPromptChoiceDto): string {
  return redactInternalText(choice.label || choice.id || "服务端选项");
}

function candidateChoices(candidate: ActionPromptCandidateDto): PromptChoiceSummary[] {
  return choiceGroups.flatMap(({ key, role }) => {
    const choices = candidate[key] as ActionPromptChoiceDto[] | null | undefined;
    return (choices ?? []).map((choice) => ({
      id: choice.id,
      label: promptChoiceLabel(choice),
      reason: choice.reason ?? undefined,
      role
    }));
  });
}

function candidateChoiceObjectIds(choiceId: string): string[] {
  const cleaned = choiceId.trim();
  if (!cleaned) {
    return [];
  }

  const ids = new Set<string>([cleaned]);
  const lastSegment = cleaned.split(":").filter(Boolean).at(-1);
  if (lastSegment && lastSegment !== cleaned) {
    ids.add(lastSegment);
  }

  return [...ids];
}

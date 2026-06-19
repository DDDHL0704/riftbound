import type { CardObjectView } from "../types/protocol";
import {
  promptChoiceRoleLabel,
  promptChoiceSummaryObjectIds,
  type PromptCandidateSummary,
  type PromptInteractionModel
} from "./promptInteraction";

export type WirePromptCandidateChoiceGroup = {
  key: string;
  labels: string[];
  roleLabel: string;
  summary: string;
};

export type WirePromptCandidateObjectRef = {
  id: string;
  label?: string;
  role: string;
};

export type WirePromptCandidateRowPlan = {
  action: string;
  choiceGroups: WirePromptCandidateChoiceGroup[];
  enabled: boolean;
  key: string;
  label: string;
  objectRefs: WirePromptCandidateObjectRef[];
  reason: string;
};

export type WirePromptCandidateListPlan = {
  disabledRows: WirePromptCandidateRowPlan[];
  emptyLabel?: string;
  enabledRows: WirePromptCandidateRowPlan[];
  message: string;
  promptTitle: string;
  promptType: string;
  versionLabel: string;
};

type BuildWirePromptCandidateListPlanOptions = {
  maxChoiceGroups?: number;
  maxChoiceLabels?: number;
  maxDisabledRows?: number;
  maxEnabledRows?: number;
  model: PromptInteractionModel;
  objects: Record<string, CardObjectView>;
  promptId?: string | null;
  promptMessage?: string | null;
  promptReason?: string | null;
  promptTitle?: string | null;
  promptType?: string | null;
  snapshotTick?: number | null;
};

export function buildWirePromptCandidateListPlan({
  maxChoiceGroups = 5,
  maxChoiceLabels = 3,
  maxDisabledRows = 4,
  maxEnabledRows = 6,
  model,
  objects,
  promptId,
  promptMessage,
  promptReason,
  promptTitle,
  promptType,
  snapshotTick
}: BuildWirePromptCandidateListPlanOptions): WirePromptCandidateListPlan {
  const enabled = model.candidates.filter((candidate) => candidate.enabled);
  const disabled = model.candidates.filter((candidate) => !candidate.enabled);
  const title = promptTitle?.trim() || "当前行动窗口";
  const message = promptMessage?.trim() || promptReason?.trim() || "等待服务端提示";

  return {
    disabledRows: disabled
      .slice(0, nonNegativeLimit(maxDisabledRows))
      .map((candidate) => candidateRowPlan(candidate, objects, maxChoiceGroups, maxChoiceLabels, "disabled")),
    emptyLabel: model.candidates.length === 0 ? "服务端暂未提供候选行动。" : undefined,
    enabledRows: enabled
      .slice(0, nonNegativeLimit(maxEnabledRows))
      .map((candidate) => candidateRowPlan(candidate, objects, maxChoiceGroups, maxChoiceLabels, "enabled")),
    message,
    promptTitle: title,
    promptType: promptType?.trim() || "无",
    versionLabel: `版本：${promptId ?? "无"} / tick ${snapshotTick ?? "无"}`
  };
}

function candidateRowPlan(
  candidate: PromptCandidateSummary,
  objects: Record<string, CardObjectView>,
  maxChoiceGroups: number,
  maxChoiceLabels: number,
  prefix: "disabled" | "enabled"
): WirePromptCandidateRowPlan {
  return {
    action: candidate.action,
    choiceGroups: candidateChoiceGroups(candidate, maxChoiceGroups, maxChoiceLabels),
    enabled: candidate.enabled,
    key: `${prefix}-${candidate.action}-${candidate.label}`,
    label: candidate.label,
    objectRefs: candidateObjectRefs(candidate, objects),
    reason: candidate.reason
  };
}

function candidateChoiceGroups(
  candidate: PromptCandidateSummary,
  maxChoiceGroups: number,
  maxChoiceLabels: number
): WirePromptCandidateChoiceGroup[] {
  const groups = candidate.choices.reduce<Record<string, string[]>>((result, choice) => {
    const key = promptChoiceRoleLabel(choice.role);
    result[key] = [...(result[key] ?? []), choice.label];
    return result;
  }, {});

  return Object.entries(groups)
    .slice(0, nonNegativeLimit(maxChoiceGroups))
    .map(([roleLabel, labels]) => {
      const labelLimit = nonNegativeLimit(maxChoiceLabels);
      const visibleLabels = labels.slice(0, labelLimit);
      return {
        key: roleLabel,
        labels,
        roleLabel,
        summary: `${roleLabel}：${visibleLabels.join("、")}${labels.length > labelLimit ? ` 等 ${labels.length} 项` : ""}`
      };
    });
}

function candidateObjectRefs(
  candidate: PromptCandidateSummary,
  objects: Record<string, CardObjectView>
): WirePromptCandidateObjectRef[] {
  return candidate.choices.flatMap((choice) => {
    const role = promptChoiceRoleLabel(choice.role);
    return promptChoiceSummaryObjectIds(choice)
      .filter((objectId) => Boolean(objects[objectId]))
      .map((objectId) => ({ id: objectId, label: choice.label, role }));
  });
}

function nonNegativeLimit(value: number): number {
  return Number.isFinite(value) ? Math.max(0, Math.floor(value)) : 0;
}

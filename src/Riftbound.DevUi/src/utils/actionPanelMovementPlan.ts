import type { ActionPromptCandidateDto } from "../types/protocol";

export type ActionPanelMovementMetricKey = "sources" | "destinations" | "costs" | "origin" | "template";

export type ActionPanelMovementMetric = {
  detail: string;
  key: ActionPanelMovementMetricKey;
  label: string;
  value: string;
};

export type ActionPanelMovementPlan = {
  authorityLabel: string;
  commandFieldCount: number;
  destinationChoiceCount: number;
  metricRows: ActionPanelMovementMetric[];
  optionalCostChoiceCount: number;
  originCount: number;
  requirementCount: number;
  selectionStepCount: number;
  sourceChoiceCount: number;
  state: "blocked" | "ready";
  statusLabel: string;
};

export function buildActionPanelMovementPlan(candidate: ActionPromptCandidateDto): ActionPanelMovementPlan {
  const sourceRequirements = recordsFromUnknown(candidate.metadata?.sourceRequirements);
  const sourceChoiceCount = maxCount(
    candidate.sources?.length ?? 0,
    uniqueRecordValueCount(sourceRequirements, "sourceObjectId"),
    selectionStepChoiceCount(candidate, "source")
  );
  const destinationChoiceCount = maxCount(
    candidate.destinations?.length ?? 0,
    requirementChoiceCount(sourceRequirements, "destinationChoices"),
    selectionStepChoiceCount(candidate, "destination")
  );
  const optionalCostChoiceCount = maxCount(
    candidate.optionalCosts?.length ?? 0,
    requirementChoiceCount(sourceRequirements, "optionalCostChoices"),
    selectionStepChoiceCount(candidate, "optionalCost")
  );
  const originCount = uniqueRecordValueCount(sourceRequirements, "origin");
  const requiredOptionalCostCount = stringArrayRequirementCount(sourceRequirements, "requiredOptionalCosts");
  const commandFieldCount = candidate.commandTemplate?.bindings.length ?? 0;
  const selectionStepCount = candidate.selectionSteps?.length ?? candidate.composer?.selectionRoles.length ?? 0;

  return {
    authorityLabel: "来源、原位置、目标位置和费用由服务端候选与后续校验裁定。",
    commandFieldCount,
    destinationChoiceCount,
    metricRows: [
      {
        detail: `${sourceRequirements.length} 组来源约束`,
        key: "sources",
        label: "单位候选",
        value: String(sourceChoiceCount)
      },
      {
        detail: "来自服务端目标位置",
        key: "destinations",
        label: "位置候选",
        value: String(destinationChoiceCount)
      },
      {
        detail: requiredOptionalCostCount > 0 ? `${requiredOptionalCostCount} 个必需费用标记` : "无必需费用标记",
        key: "costs",
        label: "费用候选",
        value: String(optionalCostChoiceCount)
      },
      {
        detail: "来自 requirementMetadata",
        key: "origin",
        label: "原位置",
        value: originCount > 0 ? `${originCount} 项` : "服务端未公开"
      },
      {
        detail: selectionStepCount > 0 ? `${selectionStepCount} 步选择` : "服务端命令模板",
        key: "template",
        label: "命令字段",
        value: String(commandFieldCount)
      }
    ],
    optionalCostChoiceCount,
    originCount,
    requirementCount: sourceRequirements.length,
    selectionStepCount,
    sourceChoiceCount,
    state: candidate.enabled ? "ready" : "blocked",
    statusLabel: candidate.enabled ? "可移动" : "暂不可移动"
  };
}

function selectionStepChoiceCount(candidate: ActionPromptCandidateDto, role: string): number {
  return (candidate.selectionSteps ?? [])
    .filter((step) => step.role === role)
    .reduce((count, step) => count + uniqueChoiceIds(step.choices).length, 0);
}

function requirementChoiceCount(requirements: Record<string, unknown>[], key: string): number {
  const ids = new Set<string>();
  requirements.forEach((requirement) => {
    uniqueChoiceIds(requirement[key]).forEach((id) => ids.add(id));
  });
  return ids.size;
}

function stringArrayRequirementCount(requirements: Record<string, unknown>[], key: string): number {
  const ids = new Set<string>();
  requirements.forEach((requirement) => {
    const value = requirement[key];
    if (!Array.isArray(value)) {
      return;
    }

    value.forEach((item) => {
      if (typeof item === "string" && item.trim().length > 0) {
        ids.add(item);
      }
    });
  });
  return ids.size;
}

function uniqueRecordValueCount(records: Record<string, unknown>[], key: string): number {
  const values = new Set<string>();
  records.forEach((record) => {
    const value = record[key];
    if (typeof value === "string" && value.trim().length > 0) {
      values.add(value);
    }
  });
  return values.size;
}

function uniqueChoiceIds(value: unknown): string[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return [...new Set(value.map(choiceId).filter((id): id is string => id != null))];
}

function choiceId(value: unknown): string | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const id = value.id;
  if (typeof id === "string" && id.trim().length > 0) {
    return id;
  }

  const label = value.label;
  return typeof label === "string" && label.trim().length > 0 ? label : undefined;
}

function recordsFromUnknown(value: unknown): Record<string, unknown>[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value.filter(isRecord);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value != null && !Array.isArray(value);
}

function maxCount(...values: number[]): number {
  return Math.max(...values.filter((value) => Number.isFinite(value)), 0);
}

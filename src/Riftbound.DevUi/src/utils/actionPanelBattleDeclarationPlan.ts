import type { ActionPromptCandidateDto } from "../types/protocol";

export type ActionPanelBattleDeclarationMetricKey =
  | "attackers"
  | "battlefields"
  | "defenders"
  | "optional-costs"
  | "template";

export type ActionPanelBattleDeclarationMetric = {
  detail: string;
  key: ActionPanelBattleDeclarationMetricKey;
  label: string;
  value: number;
};

export type ActionPanelBattleDeclarationPlan = {
  authorityLabel: string;
  battlefieldChoiceCount: number;
  commandFieldCount: number;
  defenderChoiceCount: number;
  metricRows: ActionPanelBattleDeclarationMetric[];
  optionalCostChoiceCount: number;
  paymentResourceChoiceCount: number;
  requirementCount: number;
  selectionStepCount: number;
  sourceChoiceCount: number;
  state: "blocked" | "ready";
  statusLabel: string;
};

export function buildActionPanelBattleDeclarationPlan(
  candidate: ActionPromptCandidateDto
): ActionPanelBattleDeclarationPlan {
  const sourceRequirements = recordsFromUnknown(candidate.metadata?.sourceRequirements);
  const sourceChoiceCount = maxCount(
    candidate.sources?.length ?? 0,
    uniqueRecordValueCount(sourceRequirements, "sourceObjectId"),
    selectionStepChoiceCount(candidate, "source")
  );
  const battlefieldChoiceCount = maxCount(
    candidate.destinations?.length ?? 0,
    requirementChoiceCount(sourceRequirements, "battlefieldChoices"),
    selectionStepChoiceCount(candidate, "destination")
  );
  const defenderChoiceCount = maxCount(
    candidate.targets?.length ?? 0,
    requirementTargetChoiceCount(sourceRequirements),
    selectionStepChoiceCount(candidate, "target")
  );
  const optionalCostChoiceCount = maxCount(
    candidate.optionalCosts?.length ?? 0,
    requirementChoiceCount(sourceRequirements, "optionalCostChoices"),
    selectionStepChoiceCount(candidate, "optionalCost")
  );
  const paymentResourceChoiceCount = requirementChoiceCount(sourceRequirements, "paymentResourceChoices");
  const commandFieldCount = candidate.commandTemplate?.bindings.length ?? 0;
  const selectionStepCount = candidate.selectionSteps?.length ?? candidate.composer?.selectionRoles.length ?? 0;

  return {
    authorityLabel: "声明、战场、防守方和费用由服务端候选与后续校验裁定。",
    battlefieldChoiceCount,
    commandFieldCount,
    defenderChoiceCount,
    metricRows: [
      {
        detail: `${sourceRequirements.length} 组来源约束`,
        key: "attackers",
        label: "攻击候选",
        value: sourceChoiceCount
      },
      {
        detail: "来自服务端战场选择",
        key: "battlefields",
        label: "战场候选",
        value: battlefieldChoiceCount
      },
      {
        detail: "来自服务端防守选择",
        key: "defenders",
        label: "防守候选",
        value: defenderChoiceCount
      },
      {
        detail: paymentResourceChoiceCount > 0 ? `${paymentResourceChoiceCount} 个支付资源` : "无公开支付资源",
        key: "optional-costs",
        label: "费用候选",
        value: optionalCostChoiceCount
      },
      {
        detail: selectionStepCount > 0 ? `${selectionStepCount} 步选择` : "服务端命令模板",
        key: "template",
        label: "命令字段",
        value: commandFieldCount
      }
    ],
    optionalCostChoiceCount,
    paymentResourceChoiceCount,
    requirementCount: sourceRequirements.length,
    selectionStepCount,
    sourceChoiceCount,
    state: candidate.enabled ? "ready" : "blocked",
    statusLabel: candidate.enabled ? "可声明" : "暂不可声明"
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

function requirementTargetChoiceCount(requirements: Record<string, unknown>[]): number {
  const ids = new Set<string>();
  requirements.forEach((requirement) => {
    const choicesByIndex = requirement.targetChoicesByIndex;
    if (!isRecord(choicesByIndex)) {
      return;
    }

    Object.values(choicesByIndex).forEach((choices) => {
      uniqueChoiceIds(choices).forEach((id) => ids.add(id));
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

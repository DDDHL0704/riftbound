import type { ActionPromptCandidateDto, ActionPromptChoiceDto, ActionPromptDto } from "../types/protocol";
import { sourceRequirementRecords } from "./actionPromptCandidates";
import { promptActionLabel, promptReasonLabel } from "./formatters";
import { redactInternalText } from "./redaction";

export type PromptChoiceRole = "source" | "target" | "destination" | "mode" | "optionalCost";
export type PromptObjectState = "enabled" | "disabled" | "source" | "target" | "destination" | "optionalCost" | "chosen";

export type PromptChoiceSummary = {
  id: string;
  label: string;
  objectIds?: string[];
  reason?: string;
  role: PromptChoiceRole;
};

export type PromptCommandBindingSummary = {
  field: string;
  label?: string;
  required: boolean;
  role?: PromptChoiceRole;
  roleLabel?: string;
  source: string;
};

export type PromptCandidateCommandSummary = {
  bindings: PromptCommandBindingSummary[];
  cmdType: string;
};

export type PromptCandidateSummary = {
  action: string;
  command?: PromptCandidateCommandSummary;
  enabled: boolean;
  label: string;
  reason: string;
  choices: PromptChoiceSummary[];
  steps: PromptCandidateStep[];
};

export type PromptCandidateStep = {
  count: number;
  label: string;
  required: boolean;
  role: PromptChoiceRole;
  sampleLabels: string[];
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

const bindingSourceRoles: Record<string, PromptChoiceRole | undefined> = {
  selectedDestination: "destination",
  selectedMode: "mode",
  selectedOptionalCosts: "optionalCost",
  selectedSource: "source",
  selectedTarget: "target",
  selectedTargets: "target"
};

export function buildPromptInteractionModel(prompt?: ActionPromptDto): PromptInteractionModel {
  const objectById = new Map<string, PromptObjectSummary>();
  const candidates = (prompt?.candidates ?? []).map((candidate) => {
    const choices = candidateChoices(candidate);
    for (const choice of choices) {
      for (const objectId of promptChoiceSummaryObjectIds(choice)) {
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
      command: commandTemplateSummary(candidate),
      enabled: candidate.enabled,
      label: promptActionLabel(candidate),
      reason: promptReasonLabel(candidate.reason, candidate.enabled ? "可提交" : "暂不可提交"),
      choices,
      steps: candidateSteps(candidate, choices)
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

export function promptCommandBindingLabel(binding: PromptCommandBindingSummary): string {
  const label = binding.label ?? binding.roleLabel ?? (binding.source === "requirementMetadata" ? "服务端" : "");
  const prefix = label ? `${label}:` : "";
  return `${prefix}${binding.field}${binding.required ? "*" : ""}`;
}

export function promptCommandBindingSourceLabel(binding: PromptCommandBindingSummary): string {
  if (binding.source === "requirementMetadata") {
    return "服务端注入";
  }

  return binding.roleLabel ? "玩家选择" : binding.source;
}

export function promptChoiceLabel(choice: ActionPromptChoiceDto): string {
  return redactInternalText(choice.label || choice.id || "服务端选项");
}

function candidateChoices(candidate: ActionPromptCandidateDto): PromptChoiceSummary[] {
  const topLevelChoices = choiceGroups.flatMap(({ key, role }) => {
    const choices = candidate[key] as ActionPromptChoiceDto[] | null | undefined;
    return (choices ?? []).map((choice) => ({
      id: choice.id,
      label: promptChoiceLabel(choice),
      objectIds: normalizedObjectIds(choice.objectIds),
      reason: choice.reason ?? undefined,
      role
    }));
  });
  return uniqueChoiceSummaries([
    ...selectionStepChoices(candidate),
    ...topLevelChoices,
    ...sourceRequirementChoices(candidate)
  ]);
}

function commandTemplateSummary(candidate: ActionPromptCandidateDto): PromptCandidateCommandSummary | undefined {
  const template = candidate.commandTemplate;
  if (!template) {
    return undefined;
  }

  return {
    bindings: template.bindings.map((binding) => {
      const role = bindingSourceRoles[binding.source];
      return {
        field: binding.field,
        label: binding.label ?? undefined,
        required: Boolean(binding.required),
        role,
        roleLabel: role ? promptChoiceRoleLabel(role) : undefined,
        source: binding.source
      };
    }),
    cmdType: template.cmdType || candidate.action
  };
}

function candidateSteps(candidate: ActionPromptCandidateDto, choices: PromptChoiceSummary[]): PromptCandidateStep[] {
  const serverSteps = candidate.selectionSteps
    ?.map((step) => {
      const role = promptChoiceRoleFromString(step.role);
      if (!role) {
        return undefined;
      }
      const roleChoices = choices.filter((choice) => choice.role === role);
      const sampleLabels = uniqueLabels(roleChoices.map((choice) => choice.label));

      return {
        count: Math.max(
          new Set(step.choices.map((choice) => choice.id)).size,
          new Set(roleChoices.map((choice) => choice.id)).size
        ),
        label: step.label || promptChoiceRoleLabel(role),
        required: step.required,
        role,
        sampleLabels: sampleLabels.length > 0
          ? sampleLabels.slice(0, 3)
          : step.choices.map((choice) => redactInternalText(choice.label)).slice(0, 3)
      } satisfies PromptCandidateStep;
    })
    .filter((step): step is PromptCandidateStep => Boolean(step));
  if (serverSteps?.length) {
    return serverSteps;
  }

  return choiceGroups
    .map(({ role }) => {
      const roleChoices = choices.filter((choice) => choice.role === role);
      const uniqueLabels = roleChoices
        .filter((choice, index, all) => all.findIndex((candidate) => candidate.id === choice.id) === index)
        .map((choice) => choice.label);
      return {
        count: new Set(roleChoices.map((choice) => choice.id)).size,
        label: promptChoiceRoleLabel(role),
        required: role === "source" && requiresSourceStep(candidate.action),
        role,
        sampleLabels: uniqueLabels.slice(0, 3)
      };
    })
    .filter((step) => step.count > 0 || step.required);
}

function selectionStepChoices(candidate: ActionPromptCandidateDto): PromptChoiceSummary[] {
  return (candidate.selectionSteps ?? []).flatMap((step) => {
    const role = promptChoiceRoleFromString(step.role);
    if (!role) {
      return [];
    }

    return step.choices.map((choice) => ({
      id: choice.id,
      label: redactInternalText(choice.label),
      objectIds: normalizedObjectIds(choice.objectIds),
      reason: choice.reason ?? undefined,
      role
    }));
  });
}

function sourceRequirementChoices(candidate: ActionPromptCandidateDto): PromptChoiceSummary[] {
  return sourceRequirementRecords(candidate).flatMap((requirement) => [
    ...sourceChoicesForRequirement(requirement),
    ...choiceSummariesFromValue(requirement.targetChoices, "target"),
    ...choiceSummariesFromIndexedValue(requirement.targetChoicesByIndex, "target"),
    ...choiceSummariesFromIndexedValue(requirement.attackerChoicesByIndex, "source"),
    ...choiceSummariesFromValue(requirement.destinationChoices, "destination"),
    ...choiceSummariesFromValue(requirement.battlefieldChoices, "destination"),
    ...choiceSummariesFromValue(requirement.optionalCostChoices, "optionalCost"),
    ...choiceSummariesFromValue(requirement.additionalCostChoices, "optionalCost"),
    ...choiceSummariesFromValue(requirement.paymentResourceChoices, "optionalCost"),
    ...modeChoicesForRequirement(requirement)
  ]);
}

function sourceChoicesForRequirement(requirement: Record<string, unknown>): PromptChoiceSummary[] {
  const sourceObjectId = stringFromValue(requirement.sourceObjectId);
  if (!sourceObjectId) {
    return [];
  }

  const label = firstStringFromRecord(requirement, ["displayName", "cardNo", "equipmentCardNo", "sourceObjectId"]) ?? sourceObjectId;
  return [{
    id: sourceObjectId,
    label: redactInternalText(label),
    role: "source"
  }];
}

function modeChoicesForRequirement(requirement: Record<string, unknown>): PromptChoiceSummary[] {
  const mode = stringFromValue(requirement.mode ?? requirement.abilityId);
  if (!mode) {
    return [];
  }

  const label = firstStringFromRecord(requirement, ["modeLabel", "abilityLabel", "mode", "abilityId"]) ?? mode;
  return [{
    id: mode,
    label: redactInternalText(label),
    role: "mode"
  }];
}

function choiceSummariesFromIndexedValue(value: unknown, role: PromptChoiceRole): PromptChoiceSummary[] {
  if (!isRecord(value)) {
    return [];
  }

  return Object.values(value).flatMap((choices) => choiceSummariesFromValue(choices, role));
}

function choiceSummariesFromValue(value: unknown, role: PromptChoiceRole): PromptChoiceSummary[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value
    .filter((choice): choice is ActionPromptChoiceDto => isRecord(choice) && typeof choice.id === "string")
    .map((choice) => ({
      id: choice.id,
      label: promptChoiceLabel(choice),
      objectIds: normalizedObjectIds(choice.objectIds),
      reason: typeof choice.reason === "string" ? choice.reason : undefined,
      role
    }));
}

function uniqueChoiceSummaries(choices: PromptChoiceSummary[]): PromptChoiceSummary[] {
  const seen = new Set<string>();
  return choices.filter((choice) => {
    const key = `${choice.role}:${choice.id}`;
    if (seen.has(key)) {
      return false;
    }
    seen.add(key);
    return true;
  });
}

function uniqueLabels(labels: string[]): string[] {
  return Array.from(new Set(labels.map((label) => label.trim()).filter(Boolean)));
}

function promptChoiceRoleFromString(role: string): PromptChoiceRole | undefined {
  return choiceGroups.some((group) => group.role === role) ? role as PromptChoiceRole : undefined;
}

function normalizedObjectIds(objectIds: string[] | null | undefined): string[] | undefined {
  if (!Array.isArray(objectIds)) {
    return undefined;
  }

  const normalized = objectIds
    .map((objectId) => objectId.trim())
    .filter(Boolean);
  return normalized.length > 0 ? Array.from(new Set(normalized)) : undefined;
}

function requiresSourceStep(action: string): boolean {
  return sourceDrivenActions.has(action);
}

function firstStringFromRecord(record: Record<string, unknown>, keys: string[]): string | undefined {
  for (const key of keys) {
    const value = stringFromValue(record[key]);
    if (value) {
      return value;
    }
  }

  return undefined;
}

function stringFromValue(value: unknown): string | undefined {
  return typeof value === "string" && value.trim().length > 0 ? value : undefined;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

const sourceDrivenActions = new Set<string>([
  "MULLIGAN",
  "PLAY_CARD",
  "HIDE_CARD",
  "REVEAL_CARD",
  "TAP_RUNE",
  "RECYCLE_RUNE",
  "MOVE_UNIT",
  "ASSEMBLE_EQUIPMENT",
  "DECLARE_BATTLE",
  "ACTIVATE_ABILITY",
  "LEGEND_ACT"
]);

export function promptChoiceObjectIds(choiceId: string): string[] {
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

export function promptChoiceSummaryObjectIds(choice: PromptChoiceSummary): string[] {
  return choice.objectIds?.length ? choice.objectIds : promptChoiceObjectIds(choice.id);
}

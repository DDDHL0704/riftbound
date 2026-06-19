import type { ActionPromptCandidateDto, ActionPromptContractDto, ActionPromptDto, CardObjectView, SnapshotDto } from "../types/protocol";
import { buildCandidateInteractionPlans, type CandidateInteractionPlan } from "./candidateInteractionPlan";
import { commandBindingDisplayLabel, commandBindingFieldKey } from "./commandFieldDisplay";
import { promptActionLabel, promptReasonLabel } from "./formatters";
import {
  buildPromptInteractionModel,
  promptChoiceRoleLabel,
  promptCommandBindingLabel,
  promptCommandBindingSourceLabel,
  type PromptCandidateSummary,
  type PromptCommandBindingSummary,
  type PromptChoiceRole,
  type PromptInteractionModel
} from "./promptInteraction";

export type WireActionMapMetric = {
  key: string;
  label: string;
  value: string;
};

export type WireActionObjectEntry = {
  enabledCandidateCount: number;
  label: string;
  objectId: string;
  selected: boolean;
};

export type WireActionRoleCount = {
  count: number;
  label: string;
  role: PromptChoiceRole;
};

export type WireActionGroupPlan = {
  action: string;
  enabled: boolean;
  enabledCount: number;
  key: string;
  label: string;
  reason: string;
  roleCounts: WireActionRoleCount[];
  totalCount: number;
};

export type WireActionContractPlan = {
  candidateAction: string;
  hiddenMetadataCount: number;
  legalChoicesCount: number;
  promptKind: string;
  requiredPayloadCount: number;
  visibleMetadataCount: number;
};

export type WireActionGrammarStepPlan = {
  count: number;
  key: string;
  label: string;
  required: boolean;
  role: PromptChoiceRole;
  sampleLabel: string;
};

export type WireActionCommandFieldPlan = {
  field: string;
  key: string;
  label: string;
  required: boolean;
  sourceLabel: string;
};

export type WireActionGrammarCandidatePlan = {
  commandFieldCount: number;
  commandFields: WireActionCommandFieldPlan[];
  commandType?: string;
  key: string;
  label: string;
  stepCount: number;
  steps: WireActionGrammarStepPlan[];
};

export type WireActionMapPlan = {
  canAct: boolean;
  candidatePlanTotalCount: number;
  candidatePlans: CandidateInteractionPlan[];
  contract?: WireActionContractPlan;
  disabledOnlyObjectCount: number;
  grammarCandidateTotalCount: number;
  grammarCandidates: WireActionGrammarCandidatePlan[];
  groupTotalCount: number;
  groups: WireActionGroupPlan[];
  metrics: WireActionMapMetric[];
  objectEntries: WireActionObjectEntry[];
  objectEntryOverflowCount: number;
};

type BuildWireActionMapPlanOptions = {
  maxActionGroups?: number;
  maxCandidatePlans?: number;
  maxGrammarCandidates?: number;
  maxObjectEntries?: number;
  playerId: string;
  prompt?: ActionPromptDto;
  selectedObjectId?: string;
  snapshot?: SnapshotDto;
};

type ObjectIndex = Record<string, CardObjectView>;

type ActionGroup = {
  action: string;
  candidates: PromptCandidateSummary[];
  enabledCount: number;
};

const roleOrder: PromptChoiceRole[] = ["source", "target", "destination", "optionalCost", "mode"];

export function buildWireActionMapPlan({
  maxActionGroups = 5,
  maxCandidatePlans = 5,
  maxGrammarCandidates = 4,
  maxObjectEntries = 6,
  playerId,
  prompt,
  selectedObjectId,
  snapshot
}: BuildWireActionMapPlanOptions): WireActionMapPlan {
  const model = buildPromptInteractionModel(prompt);
  const objects = objectIndex(snapshot);
  const enabledCandidates = model.candidates.filter((candidate) => candidate.enabled);
  const enabledObjects = [...model.enabledObjectIds];
  const disabledOnlyObjects = [...model.disabledObjectIds];
  const knownEnabledObjects = enabledObjects.filter((objectId) => objects[objectId]);
  const knownDisabledOnlyObjects = disabledOnlyObjects.filter((objectId) => objects[objectId]);
  const groups = actionGroups(model);
  const candidatePlans = buildCandidateInteractionPlans(model.candidates);
  const grammarCandidates = model.candidates.filter((candidate) => candidate.enabled);
  const objectLimit = nonNegativeLimit(maxObjectEntries);

  return {
    canAct: Boolean(prompt?.actionable && prompt.playerId === playerId),
    candidatePlanTotalCount: candidatePlans.length,
    candidatePlans: candidatePlans.slice(0, nonNegativeLimit(maxCandidatePlans)),
    contract: contractPlan(prompt?.contract),
    disabledOnlyObjectCount: knownDisabledOnlyObjects.length,
    grammarCandidateTotalCount: grammarCandidates.length,
    grammarCandidates: grammarCandidates
      .slice(0, nonNegativeLimit(maxGrammarCandidates))
      .map(grammarCandidatePlan),
    groupTotalCount: groups.length,
    groups: groups
      .slice(0, nonNegativeLimit(maxActionGroups))
      .map(actionGroupPlan),
    metrics: [
      { key: "enabled", label: "可提交", value: `${enabledCandidates.length}` },
      { key: "total", label: "全部候选", value: `${model.candidates.length}` },
      { key: "entry", label: "对象入口", value: `${knownEnabledObjects.length}` },
      { key: "blocked", label: "不可提交关联", value: `${knownDisabledOnlyObjects.length}` }
    ],
    objectEntries: knownEnabledObjects
      .slice(0, objectLimit)
      .map((objectId) => objectEntryPlan(objectId, objects, model, selectedObjectId)),
    objectEntryOverflowCount: Math.max(knownEnabledObjects.length - objectLimit, 0)
  };
}

function objectEntryPlan(
  objectId: string,
  objects: ObjectIndex,
  model: PromptInteractionModel,
  selectedObjectId: string | undefined
): WireActionObjectEntry {
  const summary = model.objectById.get(objectId);
  return {
    enabledCandidateCount: summary?.enabledCandidateCount ?? 0,
    label: objectLabel(objectId, objects),
    objectId,
    selected: selectedObjectId === objectId
  };
}

function actionGroupPlan(group: ActionGroup): WireActionGroupPlan {
  return {
    action: group.action,
    enabled: group.enabledCount > 0,
    enabledCount: group.enabledCount,
    key: group.action,
    label: actionGroupLabel(group.action, group.candidates),
    reason: groupReason(group.candidates),
    roleCounts: roleOrder.map((role) => ({
      count: roleCount(group.candidates, role),
      label: promptChoiceRoleLabel(role),
      role
    })),
    totalCount: group.candidates.length
  };
}

function grammarCandidatePlan(candidate: PromptCandidateSummary): WireActionGrammarCandidatePlan {
  const commandFields = candidate.command?.bindings.map((binding, index) =>
    commandFieldPlan(candidate, binding, index)) ?? [];

  return {
    commandFieldCount: commandFields.length,
    commandFields,
    commandType: candidate.command?.cmdType,
    key: `${candidate.action}:${candidate.label}`,
    label: candidate.label,
    stepCount: candidate.steps.length,
    steps: candidate.steps.map((step) => ({
      count: step.count,
      key: `${candidate.action}:${step.role}:${step.label}`,
      label: step.label,
      required: step.required,
      role: step.role,
      sampleLabel: step.sampleLabels.length > 0
        ? step.sampleLabels.join(" / ")
        : "由服务端候选决定"
    }))
  };
}

function commandFieldPlan(
  candidate: PromptCandidateSummary,
  binding: PromptCommandBindingSummary,
  index: number
): WireActionCommandFieldPlan {
  return {
    field: commandBindingFieldKey(binding, index),
    key: `${candidate.action}:${commandBindingFieldKey(binding, index)}:${binding.source}:${index}`,
    label: commandBindingDisplayLabel(binding, promptCommandBindingLabel(binding)),
    required: binding.required,
    sourceLabel: promptCommandBindingSourceLabel(binding)
  };
}

function contractPlan(contract?: ActionPromptContractDto | null): WireActionContractPlan | undefined {
  if (!contract) {
    return undefined;
  }

  return {
    candidateAction: contract.candidateAction,
    hiddenMetadataCount: contract.hiddenMetadata.length,
    legalChoicesCount: contract.legalChoices.length,
    promptKind: contract.promptKind,
    requiredPayloadCount: contract.requiredPayload.length,
    visibleMetadataCount: contract.visibleMetadata.length
  };
}

function actionGroups(model: PromptInteractionModel): ActionGroup[] {
  const byAction = new Map<string, PromptCandidateSummary[]>();
  for (const candidate of model.candidates) {
    byAction.set(candidate.action, [...(byAction.get(candidate.action) ?? []), candidate]);
  }

  return [...byAction.entries()]
    .map(([action, candidates]) => ({
      action,
      candidates,
      enabledCount: candidates.filter((candidate) => candidate.enabled).length
    }))
    .sort((left, right) =>
      right.enabledCount - left.enabledCount
      || right.candidates.length - left.candidates.length
      || left.action.localeCompare(right.action));
}

function actionGroupLabel(action: string, candidates: PromptCandidateSummary[]): string {
  const source = {
    action,
    enabled: true,
    label: candidates[0]?.label ?? action,
    reason: ""
  } satisfies ActionPromptCandidateDto;
  return promptActionLabel(source);
}

function roleCount(candidates: PromptCandidateSummary[], role: PromptChoiceRole): number {
  return new Set(candidates.flatMap((candidate) =>
    candidate.choices
      .filter((choice) => choice.role === role)
      .map((choice) => choice.id))).size;
}

function groupReason(candidates: PromptCandidateSummary[]): string {
  const enabled = candidates.find((candidate) => candidate.enabled);
  if (enabled) {
    return enabled.reason;
  }

  return candidates[0]?.reason ?? "服务端未提供候选原因。";
}

function objectIndex(snapshot?: SnapshotDto): ObjectIndex {
  return Object.values(snapshot?.players ?? {}).reduce<ObjectIndex>((index, player) => {
    for (const [objectId, object] of Object.entries(player.objects ?? {})) {
      index[object.objectId ?? objectId] = object;
    }
    return index;
  }, {});
}

function objectLabel(objectId: string, objects: ObjectIndex): string {
  if (objectId === "HIDDEN") {
    return "隐藏对象";
  }

  const object = objects[objectId];
  return object?.cardNo ?? safeChoiceId(objectId);
}

function safeChoiceId(value: string): string {
  return /^[A-Z0-9_:-]+$/.test(value) || /^[a-z0-9]+(?:[-_:][a-z0-9]+)+$/.test(value)
    ? "服务端对象"
    : promptReasonLabel(value, "服务端对象");
}

function nonNegativeLimit(value: number): number {
  return Number.isFinite(value) ? Math.max(0, Math.floor(value)) : 0;
}

import type { ActionPromptContractDto } from "../types/protocol";
import type { FocusedActionModel } from "./focusedActionModel";
import { commandFieldLabelsForCandidate } from "./promptCandidateSemantics";
import {
  tableObjectContextSourceLabel,
  type TableObjectCandidateContext,
  type TableObjectContext,
  type TableObjectEventContext
} from "./tableObjectContext";

export type FocusedObjectStatusCard = {
  label: string;
  value: string;
};

export type FocusedObjectCommandRow = {
  category: string;
  commandType?: string;
  composerReason: string;
  composerState: TableObjectCandidateContext["composerState"];
  composerStateLabel: string;
  enabled: boolean;
  fields: string[];
  intent: string;
  key: string;
  label: string;
  priority: number;
  reason: string;
  requiredFields: string[];
  roles: string[];
  secondaryFields: string[];
  stepSummary: string;
  uiHint: string;
};

export type FocusedObjectNextStepRow = {
  candidateLabel: string;
  enabled: boolean;
  nextStepLabel?: string;
  stateLabel: string;
};

export type FocusedObjectContractSummary = {
  candidateAction: string;
  hiddenMetadataCount: number;
  legalChoicesCount: number;
  promptKind: string;
  requiredPayloadCount: number;
  validationErrorCount: number;
  visibleMetadataCount: number;
};

export type FocusedObjectAuthorityState = "derived" | "none" | "server" | "snapshot";

export type FocusedObjectEventRow = {
  description: string;
  kind: string;
  role: string;
};

export type FocusedObjectCommandPlan = {
  authorityLabel: string;
  authorityState: FocusedObjectAuthorityState;
  boundaryLabel: string;
  commandRows: FocusedObjectCommandRow[];
  contract?: FocusedObjectContractSummary;
  contextSourceLabel: string;
  eventRows: FocusedObjectEventRow[];
  nextStepRows: FocusedObjectNextStepRow[];
  stackRoles: string[];
  statusCards: FocusedObjectStatusCard[];
};

export function buildFocusedObjectCommandPlan({
  context,
  contract,
  focusModel
}: {
  context?: TableObjectContext;
  contract?: ActionPromptContractDto | null;
  focusModel?: FocusedActionModel;
}): FocusedObjectCommandPlan | undefined {
  if (!context) {
    return undefined;
  }

  const commandRows = context.candidateLinks
    .map(commandRowFromCandidate)
    .sort(commandRowSort);
  const nextStepRows = (focusModel?.candidates ?? [])
    .map(({ candidate, nextStep, stateLabel }) => ({
      candidateLabel: candidate.label,
      enabled: candidate.enabled,
      nextStepLabel: nextStep?.label,
      stateLabel
    }));
  const stateValue = context.stateLabels.length > 0
    ? context.stateLabels.slice(0, 3).join(" / ")
    : "无公开状态";
  const candidateValue = `${context.promptEnabledCount} 可用 / ${context.promptDisabledCount} 阻断`;
  const authority = focusedObjectAuthorityFor(context);

  return {
    authorityLabel: authority.label,
    authorityState: authority.state,
    boundaryLabel: context.contextBoundary,
    commandRows,
    contract: contractSummary(contract),
    contextSourceLabel: authority.sourceLabel,
    eventRows: context.eventLinks.slice(-3).reverse().map(eventRowFromContext),
    nextStepRows,
    stackRoles: context.stackRoles,
    statusCards: [
      { label: "位置", value: context.zone.label },
      { label: "状态", value: stateValue },
      { label: "候选", value: candidateValue },
      { label: "上下文", value: tableObjectContextSourceLabel(context) },
      { label: "下一步", value: focusModel?.nextStepLabel ?? "点击桌面对象查看服务端候选" }
    ]
  };
}

function focusedObjectAuthorityFor(context: TableObjectContext): {
  label: string;
  sourceLabel: string;
  state: FocusedObjectAuthorityState;
} {
  const sourceLabel = tableObjectContextSourceLabel(context);

  if (context.candidateSource === "server" || context.contextSource === "server-action-prompt") {
    return { label: "服务端对象上下文", sourceLabel, state: "server" };
  }

  if (context.candidateSource === "derived" || context.contextSource === "prompt-public-derived") {
    return { label: "公开候选只读派生", sourceLabel, state: "derived" };
  }

  if (context.candidateSource === "none" || context.contextSource === "snapshot-public-index") {
    return { label: "公开快照索引", sourceLabel, state: "snapshot" };
  }

  return { label: "无候选上下文", sourceLabel, state: "none" };
}

function commandRowFromCandidate(candidate: TableObjectCandidateContext, index: number): FocusedObjectCommandRow {
  const { fields, requiredFields, secondaryFields } = commandFieldLabelsForCandidate(candidate);

  return {
    category: candidate.category,
    commandType: candidate.commandType,
    composerReason: candidate.composerReason,
    composerState: candidate.composerState,
    composerStateLabel: candidate.composerStateLabel,
    enabled: candidate.enabled,
    fields,
    intent: candidate.intent,
    key: `${candidate.commandType ?? "NO_COMMAND"}:${candidate.label}:${index}`,
    label: candidate.label,
    priority: candidate.priority,
    reason: candidate.reason,
    requiredFields,
    roles: uniqueStrings(candidate.roles),
    secondaryFields,
    stepSummary: objectCandidateStepSummary(candidate),
    uiHint: candidate.uiHint
  };
}

function objectCandidateStepSummary(candidate: TableObjectCandidateContext): string {
  const steps = (candidate.selectionSteps ?? [])
    .filter((step) => step.required || step.objectChoiceCount > 0)
    .sort((left, right) => left.index - right.index || left.role.localeCompare(right.role));
  if (steps.length === 0) {
    return "";
  }

  return steps
    .slice(0, 4)
    .map((step) => `${step.label}${step.required ? "*" : ""} ${step.objectChoiceCount}/${step.choiceCount}`)
    .join(" / ");
}

function commandRowSort(left: FocusedObjectCommandRow, right: FocusedObjectCommandRow): number {
  if (left.enabled !== right.enabled) {
    return left.enabled ? -1 : 1;
  }

  if (left.priority !== right.priority) {
    return left.priority - right.priority;
  }

  const leftCommand = left.commandType ?? "";
  const rightCommand = right.commandType ?? "";
  if (leftCommand !== rightCommand) {
    return leftCommand.localeCompare(rightCommand);
  }

  return left.label.localeCompare(right.label);
}

function contractSummary(contract?: ActionPromptContractDto | null): FocusedObjectContractSummary | undefined {
  if (!contract) {
    return undefined;
  }

  return {
    candidateAction: contract.candidateAction,
    hiddenMetadataCount: contract.hiddenMetadata.length,
    legalChoicesCount: contract.legalChoices.length,
    promptKind: contract.promptKind,
    requiredPayloadCount: contract.requiredPayload.length,
    validationErrorCount: contract.validationErrors.length,
    visibleMetadataCount: contract.visibleMetadata.length
  };
}

function eventRowFromContext(event: TableObjectEventContext): FocusedObjectEventRow {
  return {
    description: event.description,
    kind: event.kind,
    role: event.role
  };
}

function uniqueStrings(values: string[]): string[] {
  return [...new Set(values.map((value) => value.trim()).filter(Boolean))];
}

import type { ActionPromptContractDto } from "../types/protocol";
import type { FocusedActionModel } from "./focusedActionModel";
import { commandFieldLabelsForCandidate } from "./promptCandidateSemantics";
import {
  tableObjectContextSourceLabel,
  type TableObjectCandidateContext,
  type TableObjectContext,
  type TableObjectEventContext
} from "./tableObjectContext";
import {
  buildWireActionSyntaxPlanFromTableContext,
  type WireActionSyntaxPlan
} from "./wireActionSyntaxPlan";

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

export type FocusedObjectSectionKey =
  | "authority"
  | "commands"
  | "contract"
  | "events"
  | "identity"
  | "relations"
  | "stack"
  | "syntax";

export type FocusedObjectSectionState = "derived" | "empty" | "ready" | "server" | "snapshot" | "warning";

export type FocusedObjectSectionRow = {
  count: number;
  key: FocusedObjectSectionKey;
  label: string;
  sourceLabel: string;
  state: FocusedObjectSectionState;
  stateLabel: string;
  summary: string;
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
  sectionRows: FocusedObjectSectionRow[];
  serverRelationRows: FocusedObjectServerRelationRow[];
  stackRoles: string[];
  statusCards: FocusedObjectStatusCard[];
  syntax: WireActionSyntaxPlan;
};

export type FocusedObjectServerRelationRow = {
  actionLabels: string[];
  candidateSummary: string;
  key: string;
  roles: string[];
  sourceLabel: string;
  stepSummary: string;
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
  const syntax = buildWireActionSyntaxPlanFromTableContext(context);
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
  const contractPlan = contractSummary(contract);
  const serverRelationRows = (context.serverRelations ?? []).map(serverRelationRowFromContext);
  const eventRows = context.eventLinks.slice(-3).reverse().map(eventRowFromContext);

  return {
    authorityLabel: authority.label,
    authorityState: authority.state,
    boundaryLabel: context.contextBoundary,
    commandRows,
    contract: contractPlan,
    contextSourceLabel: authority.sourceLabel,
    eventRows,
    nextStepRows,
    sectionRows: sectionRowsFor({
      authority,
      commandRows,
      context,
      contract: contractPlan,
      eventRows,
      serverRelationRows,
      syntax
    }),
    serverRelationRows,
    stackRoles: context.stackRoles,
    syntax,
    statusCards: [
      { label: "位置", value: context.zone.label },
      { label: "状态", value: stateValue },
      { label: "候选", value: candidateValue },
      { label: "上下文", value: tableObjectContextSourceLabel(context) },
      { label: "下一步", value: focusModel?.nextStepLabel ?? "点击桌面对象查看服务端候选" }
    ]
  };
}

function sectionRowsFor({
  authority,
  commandRows,
  context,
  contract,
  eventRows,
  serverRelationRows,
  syntax
}: {
  authority: ReturnType<typeof focusedObjectAuthorityFor>;
  commandRows: FocusedObjectCommandRow[];
  context: TableObjectContext;
  contract?: FocusedObjectContractSummary;
  eventRows: FocusedObjectEventRow[];
  serverRelationRows: FocusedObjectServerRelationRow[];
  syntax: WireActionSyntaxPlan;
}): FocusedObjectSectionRow[] {
  return [
    {
      count: 1,
      key: "identity",
      label: "对象身份",
      sourceLabel: context.zone.label,
      state: "ready",
      stateLabel: "已定位",
      summary: `${context.objectId} / ${context.cardNo ?? context.object?.cardNo ?? "未知卡号"}`
    },
    {
      count: context.promptEnabledCount + context.promptDisabledCount,
      key: "authority",
      label: "服务边界",
      sourceLabel: authority.sourceLabel,
      state: sectionAuthorityState(authority.state),
      stateLabel: authority.label,
      summary: context.contextBoundary
    },
    {
      count: syntax.rows.length,
      key: "syntax",
      label: "候选语法",
      sourceLabel: syntax.rows.length > 0 ? "对象候选" : "无候选",
      state: syntax.missingRequiredCount > 0 ? "warning" : syntax.rows.length > 0 ? "ready" : "empty",
      stateLabel: syntax.missingRequiredCount > 0 ? `缺少 ${syntax.missingRequiredCount}` : syntax.rows.length > 0 ? "已映射" : "无语法",
      summary: syntax.summary
    },
    {
      count: commandRows.length,
      key: "commands",
      label: "服务端命令",
      sourceLabel: commandRows.length > 0 ? "prompt.commandTemplate" : "无命令",
      state: commandRows.some((row) => row.enabled) ? "ready" : commandRows.length > 0 ? "warning" : "empty",
      stateLabel: commandRows.some((row) => row.enabled) ? "有可提交" : commandRows.length > 0 ? "全部阻断" : "无命令",
      summary: `${commandRows.filter((row) => row.enabled).length} 可用 / ${commandRows.filter((row) => !row.enabled).length} 阻断`
    },
    {
      count: serverRelationRows.length,
      key: "relations",
      label: "服务端关联",
      sourceLabel: serverRelationRows.length > 0 ? "serverFlow.relatedObjects" : "无关联",
      state: serverRelationRows.length > 0 ? "server" : "empty",
      stateLabel: serverRelationRows.length > 0 ? "已关联" : "无关联",
      summary: serverRelationRows.length > 0 ? serverRelationRows.map((row) => row.roles.join("/") || "关联").join(" / ") : "无服务端关联对象。"
    },
    {
      count: context.stackRoles.length,
      key: "stack",
      label: "结算链角色",
      sourceLabel: context.stackRoles.length > 0 ? "snapshot.stack" : "无结算链",
      state: context.stackRoles.length > 0 ? "ready" : "empty",
      stateLabel: context.stackRoles.length > 0 ? "有关联" : "无角色",
      summary: context.stackRoles.length > 0 ? context.stackRoles.join(" / ") : "当前对象不在公开结算链中。"
    },
    {
      count: eventRows.length,
      key: "events",
      label: "近期事件",
      sourceLabel: eventRows.length > 0 ? "event log" : "无事件",
      state: eventRows.length > 0 ? "ready" : "empty",
      stateLabel: eventRows.length > 0 ? "有记录" : "无记录",
      summary: eventRows.length > 0 ? eventRows.map((event) => `${event.role}:${event.kind}`).join(" / ") : "无公开关联事件。"
    },
    {
      count: contract ? 1 : 0,
      key: "contract",
      label: "提示契约",
      sourceLabel: contract ? contract.promptKind : "无契约",
      state: contract ? "server" : "empty",
      stateLabel: contract ? "服务端契约" : "无契约",
      summary: contract
        ? `${contract.candidateAction} / 提交 ${contract.requiredPayloadCount} / 合法 ${contract.legalChoicesCount}`
        : "当前提示未公开契约。"
    }
  ];
}

function sectionAuthorityState(state: FocusedObjectAuthorityState): FocusedObjectSectionState {
  switch (state) {
    case "derived":
      return "derived";
    case "server":
      return "server";
    case "snapshot":
      return "snapshot";
    case "none":
      return "empty";
  }
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

  if ((context.serverRelations ?? []).length > 0 || context.contextSource === "server-flow-related-object") {
    return { label: "服务端关联对象", sourceLabel, state: "server" };
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

function serverRelationRowFromContext(
  relation: TableObjectContext["serverRelations"][number],
  index: number
): FocusedObjectServerRelationRow {
  const enabled = relation.enabledCandidateCount ?? 0;
  const disabled = relation.disabledCandidateCount ?? 0;

  return {
    actionLabels: relation.candidateActions,
    candidateSummary: relation.enabledCandidateCount == null && relation.disabledCandidateCount == null
      ? "无候选计数"
      : `${enabled} 可用 / ${disabled} 阻断`,
    key: `${relation.roles.join("/") || "relation"}:${index}`,
    roles: relation.roles,
    sourceLabel: relation.source || "server-flow-related-object",
    stepSummary: relation.stepSummary
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

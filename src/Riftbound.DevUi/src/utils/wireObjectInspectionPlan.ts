import type { ActionPromptContractDto, ActionPromptObjectInspectionDto } from "../types/protocol";
import {
  tableObjectCandidateSourceLabel,
  tableObjectContextSourceLabel,
  type TableObjectCandidateContext,
  type TableObjectContext
} from "./tableObjectContext";

export type WireObjectInspectionAuthorityState = "derived" | "server" | "snapshot";
export type WireObjectInspectionRowState = "derived" | "empty" | "ready" | "server" | "snapshot" | "warning";

export type WireObjectInspectionMetric = {
  key: string;
  label: string;
  sourceLabel: string;
  state: WireObjectInspectionRowState;
  value: string;
};

export type WireObjectInspectionRoute = {
  key: string;
  label: string;
  sourceLabel: string;
  state: WireObjectInspectionRowState;
  stateLabel: string;
  summary: string;
};

export type WireObjectInspectionGroupRow = {
  key: string;
  label: string;
  tone?: string;
  value: string;
};

export type WireObjectInspectionGroup = {
  emptyLabel?: string;
  key: string;
  rows: WireObjectInspectionGroupRow[];
  sourceLabel: string;
  title: string;
};

export type WireObjectInspectionPlan = {
  authorityLabel: string;
  authorityState: WireObjectInspectionAuthorityState;
  boundaryLabel: string;
  contextSourceLabel: string;
  groups: WireObjectInspectionGroup[];
  metrics: WireObjectInspectionMetric[];
  objectId: string;
  routeRows: WireObjectInspectionRoute[];
};

export function buildWireObjectInspectionPlan({
  context,
  contract
}: {
  context?: TableObjectContext;
  contract?: ActionPromptContractDto | null;
}): WireObjectInspectionPlan | undefined {
  if (!context) {
    return undefined;
  }

  const authority = objectInspectionAuthority(context);
  const sourceLabel = tableObjectContextSourceLabel(context);
  const selectionSummary = selectionStepSummary(context.candidateLinks);
  const commandSummary = commandFieldSummary(context.candidateLinks);
  const relationCount = context.serverRelations.length;
  const eventCount = context.eventLinks.length;
  const stackCount = context.stackRoles.length;
  const candidateTotal = context.promptEnabledCount + context.promptDisabledCount;
  const serverInspection = context.serverInspection;

  return {
    authorityLabel: authority.label,
    authorityState: authority.state,
    boundaryLabel: serverInspection?.boundary || context.contextBoundary,
    contextSourceLabel: sourceLabel,
    groups: inspectionGroups({
      commandSummary,
      context,
      contract,
      selectionSummary,
      serverInspection
    }),
    metrics: [
      {
        key: "zone",
        label: "位置",
        sourceLabel: "snapshot.objects.location",
        state: context.zone.kind === "unknown" ? "warning" : "ready",
        value: context.zone.label
      },
      {
        key: "authority",
        label: "权威",
        sourceLabel,
        state: authority.state === "server" ? "server" : authority.state,
        value: authority.label
      },
      {
        key: "candidate",
        label: "候选",
        sourceLabel: tableObjectCandidateSourceLabel(context.candidateSource),
        state: context.promptEnabledCount > 0 ? "server" : context.promptDisabledCount > 0 ? "warning" : "empty",
        value: `${context.promptEnabledCount} 可用 / ${context.promptDisabledCount} 阻断`
      },
      {
        key: "syntax",
        label: "选择语法",
        sourceLabel: selectionSummary.total > 0 ? "objectContexts.candidates.selectionSteps" : "无选择步骤",
        state: selectionSummary.missingRequired > 0 ? "warning" : selectionSummary.total > 0 ? "ready" : "empty",
        value: selectionSummary.total > 0
          ? `${selectionSummary.total} 步 / 缺 ${selectionSummary.missingRequired}`
          : "无步骤"
      },
      {
        key: "commands",
        label: "命令字段",
        sourceLabel: commandSummary.total > 0 ? "objectContexts.candidates.commandFields" : "无命令字段",
        state: commandSummary.required > 0 ? "server" : commandSummary.total > 0 ? "ready" : "empty",
        value: `${commandSummary.required} 必填 / ${commandSummary.total} 公开`
      },
      {
        key: "links",
        label: "关联",
        sourceLabel: "stack / events / serverFlow",
        state: relationCount + eventCount + stackCount > 0 ? "ready" : "empty",
        value: `${relationCount} 流程 / ${stackCount} 结算 / ${eventCount} 事件`
      }
    ],
    objectId: context.objectId,
    routeRows: [
      {
        key: "identity",
        label: "对象身份",
        sourceLabel: context.object ? "snapshot.objects" : "objectContexts",
        state: context.object ? "ready" : "snapshot",
        stateLabel: context.object ? "已公开" : "仅上下文",
        summary: [context.objectId, context.cardNo ?? context.object?.cardNo ?? "未知卡号"].join(" / ")
      },
      {
        key: "zone",
        label: "区域映射",
        sourceLabel: "snapshot/public table projection",
        state: context.zone.kind === "unknown" ? "warning" : "ready",
        stateLabel: context.zone.kind,
        summary: [context.zone.label, context.zone.battlefieldObjectId ? `战场 ${context.zone.battlefieldObjectId}` : ""]
          .filter(Boolean)
          .join(" / ")
      },
      {
        key: "authority",
        label: "服务边界",
        sourceLabel,
        state: authority.state === "server" ? "server" : authority.state,
        stateLabel: authority.label,
        summary: serverInspection?.boundary || context.contextBoundary
      },
      {
        key: "candidate",
        label: "候选覆盖",
        sourceLabel: tableObjectCandidateSourceLabel(context.candidateSource),
        state: context.promptEnabledCount > 0 ? "server" : context.promptDisabledCount > 0 ? "warning" : "empty",
        stateLabel: `${context.promptEnabledCount} 可用 / ${context.promptDisabledCount} 阻断`,
        summary: candidateTotal > 0 ? "对象出现在当前行动提示候选中。" : "该对象当前没有服务端可提交候选。"
      },
      {
        key: "syntax",
        label: "选择语法",
        sourceLabel: selectionSummary.sourceLabel,
        state: selectionSummary.missingRequired > 0 ? "warning" : selectionSummary.total > 0 ? "ready" : "empty",
        stateLabel: selectionSummary.missingRequired > 0 ? `缺少 ${selectionSummary.missingRequired}` : selectionSummary.total > 0 ? "已映射" : "无步骤",
        summary: selectionSummary.label
      },
      {
        key: "commands",
        label: "命令字段",
        sourceLabel: commandSummary.sourceLabel,
        state: commandSummary.total > 0 ? "server" : "empty",
        stateLabel: `${commandSummary.required} 必填 / ${commandSummary.total} 公开`,
        summary: commandSummary.label
      },
      {
        key: "server-relations",
        label: "服务端流程",
        sourceLabel: relationCount > 0 ? "serverFlow.relatedObjects" : "无流程关联",
        state: relationCount > 0 ? "server" : "empty",
        stateLabel: relationCount > 0 ? "已关联" : "无关联",
        summary: relationCount > 0 ? relationRoleSummary(context) : "当前流程没有公开关联该对象。"
      },
      {
        key: "stack-events",
        label: "结算与事件",
        sourceLabel: "snapshot.stack / event log",
        state: eventCount + stackCount > 0 ? "ready" : "empty",
        stateLabel: `${stackCount} 结算 / ${eventCount} 事件`,
        summary: stackEventSummary(context)
      },
      {
        key: "contract",
        label: "提示契约",
        sourceLabel: contract ? "prompt.contract" : "无契约",
        state: contract ? "server" : "empty",
        stateLabel: contract ? contract.promptKind : "无契约",
        summary: contract
          ? `${contract.candidateAction} / 提交 ${contract.requiredPayload.length} / 合法 ${contract.legalChoices.length} / 隐藏 ${contract.hiddenMetadata.length}`
          : "当前提示没有公开契约。"
      }
    ]
  };
}

function objectInspectionAuthority(context: TableObjectContext): { label: string; state: WireObjectInspectionAuthorityState } {
  if (context.serverInspection || context.candidateSource === "server" || context.contextSource === "server-action-prompt") {
    return { label: context.serverInspection ? "服务端检查摘要" : "服务端对象上下文", state: "server" };
  }

  if (context.serverRelations.length > 0 || context.contextSource === "server-flow-related-object") {
    return { label: "服务端流程关联", state: "server" };
  }

  if (context.candidateSource === "derived" || context.contextSource === "prompt-public-derived") {
    return { label: "公开候选只读派生", state: "derived" };
  }

  return { label: "公开快照索引", state: "snapshot" };
}

function inspectionGroups({
  commandSummary,
  context,
  contract,
  selectionSummary,
  serverInspection
}: {
  commandSummary: ReturnType<typeof commandFieldSummary>;
  context: TableObjectContext;
  contract?: ActionPromptContractDto | null;
  selectionSummary: ReturnType<typeof selectionStepSummary>;
  serverInspection?: ActionPromptObjectInspectionDto | null;
}): WireObjectInspectionGroup[] {
  const serverGroups = serverInspection?.groups.map((group) => ({
    emptyLabel: group.emptyLabel ?? undefined,
    key: `server-${group.key}`,
    rows: group.rows.map((row) => ({
      key: row.key,
      label: row.label,
      tone: row.tone ?? undefined,
      value: row.value
    })),
    sourceLabel: serverInspection.source,
    title: group.title
  })) ?? [];

  return [
    {
      key: "identity",
      rows: [
        { key: "object", label: "对象", value: context.objectId },
        { key: "card", label: "卡号", value: context.cardNo ?? context.object?.cardNo ?? "未知" },
        { key: "owner", label: "所属", value: context.ownerId ?? context.object?.ownerId ?? "未知" },
        { key: "controller", label: "控制", value: context.controllerId ?? context.object?.controllerId ?? "未知" },
        { key: "zone", label: "区域", value: context.zone.label }
      ],
      sourceLabel: "snapshot.objects",
      title: "对象定位"
    },
    {
      emptyLabel: "当前对象没有服务端候选。",
      key: "candidates",
      rows: context.candidateLinks.map((candidate, index) => ({
        key: `candidate-${index}`,
        label: candidate.enabled ? "可提交" : "阻断",
        tone: candidate.enabled ? "good" : "warn",
        value: candidateRouteLabel(candidate)
      })),
      sourceLabel: tableObjectCandidateSourceLabel(context.candidateSource),
      title: "候选与提交"
    },
    {
      emptyLabel: "当前候选没有公开选择步骤。",
      key: "syntax",
      rows: selectionSummary.rows,
      sourceLabel: selectionSummary.sourceLabel,
      title: "选择语法"
    },
    {
      emptyLabel: "当前候选没有公开命令字段。",
      key: "commands",
      rows: commandSummary.rows,
      sourceLabel: commandSummary.sourceLabel,
      title: "命令字段"
    },
    ...serverGroups,
    {
      emptyLabel: "当前流程没有公开关联该对象。",
      key: "relations",
      rows: context.serverRelations.map((relation, index) => ({
        key: `relation-${index}`,
        label: relation.roles.join(" / ") || "关联",
        value: [
          relation.enabledCandidateCount != null || relation.disabledCandidateCount != null
            ? `${relation.enabledCandidateCount ?? 0} 可用 / ${relation.disabledCandidateCount ?? 0} 阻断`
            : "无候选计数",
          relation.candidateActions.length > 0 ? relation.candidateActions.join(" / ") : "",
          relation.stepSummary
        ].filter(Boolean).join(" / ")
      })),
      sourceLabel: "serverFlow.relatedObjects",
      title: "服务端流程"
    },
    {
      emptyLabel: "当前对象没有公开结算链或近期事件。",
      key: "events",
      rows: [
        ...context.stackRoles.map((role, index) => ({
          key: `stack-${index}`,
          label: "结算链",
          value: role
        })),
        ...context.eventLinks.map((event, index) => ({
          key: `event-${index}`,
          label: event.role,
          value: `${event.kind}：${event.description}`
        }))
      ],
      sourceLabel: "snapshot.stack / event log",
      title: "结算与事件"
    },
    {
      key: "boundary",
      rows: [
        { key: "source", label: "来源", value: tableObjectContextSourceLabel(context) },
        { key: "candidate-source", label: "候选", value: tableObjectCandidateSourceLabel(context.candidateSource) },
        { key: "rule-boundary", label: "规则裁定", value: "合法性、费用、时机与隐藏信息均由服务端候选和后续校验裁定。" },
        {
          key: "contract",
          label: "契约",
          value: contract
            ? `${contract.promptKind} / ${contract.candidateAction} / 隐藏 metadata ${contract.hiddenMetadata.length}`
            : "未公开 prompt contract"
        }
      ],
      sourceLabel: "authority boundary",
      title: "安全边界"
    }
  ];
}

function selectionStepSummary(candidates: TableObjectCandidateContext[]): {
  label: string;
  missingRequired: number;
  rows: WireObjectInspectionGroupRow[];
  sourceLabel: string;
  total: number;
} {
  const steps = candidates.flatMap((candidate, candidateIndex) =>
    candidate.selectionSteps.map((step, stepIndex) => ({
      candidate,
      key: `step-${candidateIndex}-${stepIndex}`,
      step
    })));
  const visibleSteps = steps.filter(({ step }) => step.required || step.objectChoiceCount > 0);
  const missingRequired = visibleSteps.filter(({ step }) => step.required && step.objectChoiceCount <= 0).length;
  const rows = visibleSteps
    .sort((left, right) => left.step.index - right.step.index || left.step.role.localeCompare(right.step.role))
    .map(({ candidate, key, step }) => ({
      key,
      label: step.required ? "必选" : "可选",
      tone: step.required && step.objectChoiceCount <= 0 ? "warn" : "neutral",
      value: `${candidate.label} / ${step.label} / ${step.objectChoiceCount}/${step.choiceCount}`
    }));
  const sourceLabel = rows.length > 0 ? "objectContexts.candidates.selectionSteps" : "无选择步骤";

  return {
    label: rows.length > 0
      ? `${rows.length} 个公开步骤，${missingRequired} 个必填未覆盖。`
      : "没有需要展示的选择步骤。",
    missingRequired,
    rows,
    sourceLabel,
    total: rows.length
  };
}

function commandFieldSummary(candidates: TableObjectCandidateContext[]): {
  label: string;
  required: number;
  rows: WireObjectInspectionGroupRow[];
  sourceLabel: string;
  total: number;
} {
  const requiredFields = uniqueStrings(candidates.flatMap((candidate) => candidate.requiredCommandFields));
  const commandFields = uniqueStrings(candidates.flatMap((candidate) => candidate.commandFields));
  const rows = [
    ...requiredFields.map((field, index) => ({
      key: `required-${index}`,
      label: "必填",
      tone: "warn",
      value: field
    })),
    ...commandFields
      .filter((field) => !requiredFields.includes(field))
      .map((field, index) => ({
        key: `field-${index}`,
        label: "公开",
        tone: "neutral",
        value: field
      }))
  ];

  return {
    label: rows.length > 0
      ? `${requiredFields.length} 个必填字段，${commandFields.length} 个公开字段。`
      : "没有公开命令字段。",
    required: requiredFields.length,
    rows,
    sourceLabel: rows.length > 0 ? "objectContexts.candidates.commandFields" : "无命令字段",
    total: commandFields.length
  };
}

function candidateRouteLabel(candidate: TableObjectCandidateContext): string {
  return [
    candidate.commandType ?? candidate.label,
    candidate.roles.length > 0 ? candidate.roles.join("/") : "",
    candidate.requiredCommandFields.length > 0 ? `需 ${candidate.requiredCommandFields.join("/")}` : "",
    candidate.selectionSteps.length > 0 ? `步骤 ${candidate.selectionSteps.length}` : "",
    candidate.composerStateLabel,
    candidate.enabled ? "" : candidate.reason
  ].filter(Boolean).join(" / ");
}

function relationRoleSummary(context: TableObjectContext): string {
  return context.serverRelations
    .slice(0, 4)
    .map((relation) => relation.roles.join("/") || relation.source || "关联")
    .join(" / ");
}

function stackEventSummary(context: TableObjectContext): string {
  const parts = [
    context.stackRoles.length > 0 ? `结算链 ${context.stackRoles.join("/")}` : "",
    context.eventLinks.length > 0 ? `事件 ${context.eventLinks.slice(-2).map((event) => event.kind).join("/")}` : ""
  ].filter(Boolean);
  return parts.length > 0 ? parts.join(" / ") : "无公开结算链或事件关联。";
}

function uniqueStrings(values: string[]): string[] {
  return [...new Set(values.map((value) => value.trim()).filter(Boolean))];
}

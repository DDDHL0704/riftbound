import type {
  ActionPromptDto,
  ActionPromptObjectCandidateDto,
  ActionPromptObjectCandidateStepDto,
  ActionPromptObjectContextDto,
  ActionPromptObjectInspectionDto,
  BattlefieldSnapshotView,
  CardObjectView,
  GameEvent,
  SnapshotDto,
  StackItemView
} from "../types/protocol";
import { asArray, asRecord, asString } from "./collections";
import { promptReasonLabel } from "./formatters";
import { gameEventObjectRefPlan } from "./gameEventObjectRefs";
import {
  buildPromptInteractionModel,
  promptCommandBindingLabel,
  promptChoiceRoleLabel,
  promptChoiceSummaryObjectIds,
  type PromptCandidateComposerSummary,
  type PromptCandidateSummary,
  type PromptChoiceRole,
  type PromptCommandBindingSummary
} from "./promptInteraction";
import { redactInternalText } from "./redaction";
import { buildCardObjectIndex } from "./snapshotObjectIndex";

export type TableObjectZoneKind =
  | "banished"
  | "base"
  | "battlefield"
  | "battlefield-site"
  | "champion"
  | "graveyard"
  | "hand"
  | "legend"
  | "rune"
  | "stack"
  | "unknown";

export type TableObjectZoneContext = {
  battlefieldObjectId?: string;
  kind: TableObjectZoneKind;
  label: string;
  playerId?: string;
};

export type TableObjectEventContext = {
  description: string;
  kind: string;
  role: string;
};

export type TableObjectCandidateContext = {
  commandFields: string[];
  commandType?: string;
  composerReason: string;
  composerState: PromptCandidateComposerSummary["state"];
  composerStateLabel: string;
  enabled: boolean;
  label: string;
  reason: string;
  requiredCommandFields: string[];
  roles: string[];
  selectionSteps: TableObjectCandidateStepContext[];
};

export type TableObjectCandidateStepContext = {
  choiceCount: number;
  index: number;
  label: string;
  objectChoiceCount: number;
  required: boolean;
  role: string;
};

export type TableObjectCandidateSource = "derived" | "none" | "server";

export type TableObjectContext = {
  candidateLinks: TableObjectCandidateContext[];
  candidateSource: TableObjectCandidateSource;
  cardNo?: string | null;
  controllerId?: string | null;
  contextBoundary: string;
  contextSource: string;
  eventLinks: TableObjectEventContext[];
  object?: CardObjectView;
  objectId: string;
  ownerId?: string | null;
  promptDisabledCount: number;
  promptEnabledCount: number;
  serverInspection?: ActionPromptObjectInspectionDto | null;
  stackRoles: string[];
  stateLabels: string[];
  zone: TableObjectZoneContext;
};

export type TableObjectContextModel = {
  byId: Record<string, TableObjectContext>;
};

type BuildTableObjectContextModelOptions = {
  events?: GameEvent[];
  perspectivePlayerId: string;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
};

const zoneKeyLabels: Array<{ key: keyof NonNullable<SnapshotDto["players"][string]["zones"]>; kind: TableObjectZoneKind; label: string }> = [
  { key: "hand", kind: "hand", label: "手牌" },
  { key: "base", kind: "base", label: "基地" },
  { key: "graveyard", kind: "graveyard", label: "已打出牌堆" },
  { key: "banished", kind: "banished", label: "放逐区" },
  { key: "legendZone", kind: "legend", label: "传奇区" },
  { key: "championZone", kind: "champion", label: "英雄区" },
  { key: "battlefields", kind: "battlefield-site", label: "战场牌区" }
];

export function buildTableObjectContextModel({
  events = [],
  perspectivePlayerId,
  prompt,
  snapshot
}: BuildTableObjectContextModelOptions): TableObjectContextModel {
  const objects = buildCardObjectIndex(snapshot);
  const zoneById = buildZoneIndex(snapshot, objects, perspectivePlayerId);
  const promptModel = buildPromptInteractionModel(prompt);
  const serverObjectContextById = buildServerObjectContextIndex(prompt?.objectContexts);
  const eventLinksById = buildEventLinks(events);
  const stackRolesById = buildStackRoles(snapshot?.stack ?? []);
  const objectIds = new Set<string>([
    ...Object.keys(objects),
    ...Object.keys(zoneById),
    ...promptModel.objectById.keys(),
    ...serverObjectContextById.keys(),
    ...Object.keys(eventLinksById),
    ...Object.keys(stackRolesById)
  ]);
  const byId: Record<string, TableObjectContext> = {};

  for (const objectId of objectIds) {
    const object = objects[objectId];
    const promptSummary = promptModel.objectById.get(objectId);
    const serverContext = serverObjectContextById.get(objectId);
    const derivedCandidateLinks = promptModel.candidates
      .filter((candidate) => candidate.choices.some((choice) => promptChoiceSummaryObjectIds(choice).includes(objectId)))
      .map((candidate) => candidateContextForObject(candidate, objectId));
    const candidateLinks = serverContext
      ? serverContext.candidates.map(candidateContextFromServerObjectCandidate)
      : derivedCandidateLinks;
    const candidateSource: TableObjectCandidateSource = serverContext
      ? "server"
      : candidateLinks.length > 0 ? "derived" : "none";
    byId[objectId] = {
      candidateLinks,
      candidateSource,
      cardNo: object?.cardNo,
      controllerId: object?.controllerId,
      contextBoundary: objectContextBoundary(serverContext, candidateSource),
      contextSource: objectContextSource(serverContext, candidateSource),
      eventLinks: eventLinksById[objectId] ?? [],
      object,
      objectId,
      ownerId: object?.ownerId,
      promptDisabledCount: serverContext?.disabledCandidateCount ?? promptSummary?.disabledCandidateCount ?? 0,
      promptEnabledCount: serverContext?.enabledCandidateCount ?? promptSummary?.enabledCandidateCount ?? 0,
      serverInspection: serverContext?.inspection,
      stackRoles: stackRolesById[objectId] ?? [],
      stateLabels: objectStateLabels(object),
      zone: zoneById[objectId] ?? { kind: "unknown", label: "未定位区域" }
    };
  }

  return { byId };
}

export function tableObjectCandidateSourceLabel(source: TableObjectCandidateSource | undefined): string {
  switch (source) {
    case "server":
      return "服务端对象上下文";
    case "derived":
      return "公开候选只读派生";
    case "none":
      return "无候选上下文";
    default:
      return "未建立上下文";
  }
}

export function tableObjectContextSourceLabel(context: Pick<TableObjectContext, "candidateSource" | "contextSource"> | undefined): string {
  const source = context?.contextSource?.trim();
  if (!source) {
    return tableObjectCandidateSourceLabel(context?.candidateSource);
  }

  switch (source) {
    case "server-action-prompt":
      return "服务端对象上下文";
    case "prompt-public-derived":
      return "公开候选只读派生";
    case "snapshot-public-index":
      return "公开快照索引";
    default:
      return source;
  }
}

function buildServerObjectContextIndex(
  contexts: ActionPromptObjectContextDto[] | null | undefined
): Map<string, ActionPromptObjectContextDto> {
  const byId = new Map<string, ActionPromptObjectContextDto>();
  for (const context of contexts ?? []) {
    const objectId = context.objectId?.trim();
    if (objectId) {
      byId.set(objectId, context);
    }
  }
  return byId;
}

function objectContextSource(
  context: ActionPromptObjectContextDto | undefined,
  candidateSource: TableObjectCandidateSource
): string {
  const serverSource = context?.source?.trim() || context?.inspection?.source?.trim();
  if (serverSource) {
    return serverSource;
  }

  switch (candidateSource) {
    case "derived":
      return "prompt-public-derived";
    case "server":
      return "server-action-prompt";
    case "none":
      return "snapshot-public-index";
  }
}

function objectContextBoundary(
  context: ActionPromptObjectContextDto | undefined,
  candidateSource: TableObjectCandidateSource
): string {
  const serverBoundary = context?.boundary?.trim() || context?.inspection?.boundary?.trim();
  if (serverBoundary) {
    return serverBoundary;
  }

  switch (candidateSource) {
    case "derived":
      return "当前对象上下文由公开 prompt 候选派生，只用于只读定位；合法性和提交仍以服务端候选与校验为准。";
    case "server":
      return "服务端对象上下文只公开当前行动提示中的对象候选、选择角色和命令字段；隐藏 metadata、隐藏区内容和未公开卡牌身份不进入对象上下文。";
    case "none":
      return "当前对象只有公开快照索引，没有服务端候选上下文；前端不会从该对象推断可提交操作。";
  }
}

function candidateContextFromServerObjectCandidate(candidate: ActionPromptObjectCandidateDto): TableObjectCandidateContext {
  const composer = composerContextFromServerObjectCandidate(candidate);

  return {
    commandFields: candidate.commandFields ?? [],
    commandType: candidate.commandType ?? candidate.action,
    composerReason: composer.reason,
    composerState: composer.state,
    composerStateLabel: composer.stateLabel,
    enabled: candidate.enabled,
    label: candidate.label || candidate.action,
    reason: promptReasonLabel(candidate.reason, candidate.enabled ? "可提交" : "暂不可提交"),
    requiredCommandFields: candidate.requiredCommandFields ?? [],
    roles: candidate.roles ?? [],
    selectionSteps: objectCandidateStepsFromServer(candidate.selectionSteps)
  };
}

function candidateContextForObject(candidate: PromptCandidateSummary, objectId: string): TableObjectCandidateContext {
  const linkedChoices = candidate.choices.filter((choice) => promptChoiceSummaryObjectIds(choice).includes(objectId));
  const roleKeys = uniquePromptRoles(linkedChoices.map((choice) => choice.role));
  const commandBindings = commandBindingsForRoles(candidate.command?.bindings, roleKeys);
  const composer = candidate.composer ?? composerContextFromCommand(Boolean(candidate.command));

  return {
    commandFields: commandBindings.map(promptCommandBindingLabel),
    commandType: candidate.command?.cmdType,
    composerReason: composer.reason,
    composerState: composer.state,
    composerStateLabel: composer.stateLabel,
    enabled: candidate.enabled,
    label: candidate.label,
    reason: candidate.reason,
    requiredCommandFields: commandBindings.filter((binding) => binding.required).map(promptCommandBindingLabel),
    roles: uniqueStrings(linkedChoices.map((choice) => promptChoiceRoleLabel(choice.role))),
    selectionSteps: candidate.steps.map((step, index) => {
      const objectChoiceCount = linkedChoices.filter((choice) => choice.role === step.role).length;
      return {
        choiceCount: step.count,
        index,
        label: step.label,
        objectChoiceCount,
        required: step.required,
        role: step.role
      };
    })
  };
}

function objectCandidateStepsFromServer(
  steps: ActionPromptObjectCandidateStepDto[] | null | undefined
): TableObjectCandidateStepContext[] {
  return (steps ?? [])
    .map((step) => ({
      choiceCount: numberOrZero(step.choiceCount),
      index: numberOrZero(step.index),
      label: step.label || step.role || "步骤",
      objectChoiceCount: numberOrZero(step.objectChoiceCount),
      required: Boolean(step.required),
      role: step.role || step.label || "step"
    }))
    .sort((left, right) => left.index - right.index || left.role.localeCompare(right.role));
}

function composerContextFromServerObjectCandidate(candidate: ActionPromptObjectCandidateDto): PromptCandidateComposerSummary {
  if (candidate.composer) {
    if (candidate.composer.supported) {
      return {
        reason: redactInternalText(candidate.composer.reason || "服务端已公开组合提交。"),
        state: "server",
        stateLabel: "服务端声明",
        supported: true
      };
    }

    return {
      reason: redactInternalText(candidate.composer.reason || "服务端暂未开放组合提交。"),
      state: "blocked",
      stateLabel: "服务端阻断",
      supported: false
    };
  }

  return composerContextFromCommand(Boolean(candidate.commandType));
}

function composerContextFromCommand(hasCommand: boolean): PromptCandidateComposerSummary {
  if (hasCommand) {
    return {
      reason: "候选只有命令模板，缺少服务端 composer 支持声明。",
      state: "fallback",
      stateLabel: "仅有模板",
      supported: true
    };
  }

  return {
    reason: "候选未公开组合提交协议。",
    state: "missing",
    stateLabel: "未公开",
    supported: false
  };
}

function commandBindingsForRoles(
  bindings: PromptCommandBindingSummary[] | undefined,
  roles: PromptChoiceRole[]
): PromptCommandBindingSummary[] {
  if (!bindings?.length) {
    return [];
  }

  return bindings.filter((binding) =>
    binding.source === "requirementMetadata"
    || (binding.role && roles.includes(binding.role)));
}

function buildZoneIndex(
  snapshot: SnapshotDto | undefined,
  objects: Record<string, CardObjectView>,
  perspectivePlayerId: string
): Record<string, TableObjectZoneContext> {
  const zones: Record<string, TableObjectZoneContext> = {};
  for (const [playerId, player] of Object.entries(snapshot?.players ?? {})) {
    const sideLabel = playerSideLabel(playerId, perspectivePlayerId);
    for (const { key, kind, label } of zoneKeyLabels) {
      for (const objectId of asArray<string>(player.zones?.[key])) {
        const object = objects[objectId];
        zones[objectId] = {
          kind: kind === "base" && isRuneObject(object) ? "rune" : kind,
          label: `${sideLabel}${kind === "base" && isRuneObject(object) ? "已抽出符文" : label}`,
          playerId
        };
      }
    }
  }

  for (const [index, battlefield] of asArray<BattlefieldSnapshotView>(asRecord(snapshot?.lanes).battlefields).entries()) {
    const laneLabel = index === 0 ? "左战场" : "右战场";
    const battlefieldId = asString(battlefield.battlefieldObjectId, "");
    if (battlefieldId) {
      zones[battlefieldId] = {
        battlefieldObjectId: battlefieldId,
        kind: "battlefield-site",
        label: `${laneLabel}牌`,
        playerId: battlefield.zonePlayerId
      };
    }

    for (const objectId of asArray<string>(battlefield.occupantObjectIds)) {
      const object = objects[objectId];
      const sideLabel = playerSideLabel(object?.controllerId ?? object?.ownerId ?? "", perspectivePlayerId);
      zones[objectId] = {
        battlefieldObjectId: battlefieldId || undefined,
        kind: "battlefield",
        label: `${laneLabel} / ${sideLabel}单位`,
        playerId: object?.controllerId ?? object?.ownerId ?? battlefield.zonePlayerId
      };
    }
  }

  return zones;
}

function buildStackRoles(stack: StackItemView[]): Record<string, string[]> {
  const roles: Record<string, string[]> = {};
  for (const item of stack) {
    addRole(roles, item.sourceObjectId, "结算链来源");
    for (const targetObjectId of item.targetObjectIds ?? []) {
      addRole(roles, targetObjectId, "结算链目标");
    }
  }
  return roles;
}

function buildEventLinks(events: GameEvent[]): Record<string, TableObjectEventContext[]> {
  const links: Record<string, TableObjectEventContext[]> = {};
  for (const event of events) {
    for (const ref of gameEventObjectRefPlan(event).refs) {
      if (!ref.objectId || ref.isHidden) {
        continue;
      }

      const existing = links[ref.objectId] ?? [];
      existing.push({
        description: event.description?.trim() || event.kind,
        kind: event.kind,
        role: ref.role || "对象"
      });
      links[ref.objectId] = existing.slice(-4);
    }
  }
  return links;
}

function objectStateLabels(object?: CardObjectView): string[] {
  if (!object) {
    return ["服务端对象未公开"];
  }

  const labels: string[] = [];
  if (object.isFaceDown) labels.push("隐藏");
  if (object.isExhausted) labels.push("横置");
  if (object.isAttacking) labels.push("攻击中");
  if (object.isDefending) labels.push("防守中");
  if ((object.damage ?? 0) > 0) labels.push(`伤害 ${object.damage}`);
  const power = object.effectivePower ?? object.power;
  if (power != null) labels.push(`战力 ${power}`);
  if (object.untilEndOfTurnPowerModifier) labels.push(`本回合战力 ${object.untilEndOfTurnPowerModifier > 0 ? "+" : ""}${object.untilEndOfTurnPowerModifier}`);
  return labels.length > 0 ? labels : ["常规状态"];
}

function isRuneObject(object?: CardObjectView): boolean {
  return Boolean(object?.tags?.some((tag) => tag === "CARD_TYPE:RUNE") || object?.cardNo?.includes("-R"));
}

function addRole(index: Record<string, string[]>, objectId: string | null | undefined, role: string): void {
  if (!objectId?.trim()) {
    return;
  }

  index[objectId] = uniqueStrings([...(index[objectId] ?? []), role]);
}

function playerSideLabel(playerId: string | null | undefined, perspectivePlayerId: string): string {
  if (!playerId) {
    return "未知";
  }
  return playerId === perspectivePlayerId ? "我方" : "对方";
}

function numberOrZero(value: number | null | undefined): number {
  return Number.isFinite(value) ? Number(value) : 0;
}

function uniqueStrings(values: string[]): string[] {
  return [...new Set(values.map((value) => value.trim()).filter(Boolean))];
}

function uniquePromptRoles(values: PromptChoiceRole[]): PromptChoiceRole[] {
  return [...new Set(values)];
}

import type {
  ActionPromptDto,
  ActionPromptObjectCandidateDto,
  ActionPromptObjectCandidateStepDto,
  ActionPromptObjectContextDto,
  ActionPromptObjectInspectionDto,
  ActionPromptServerFlowObjectRefDto,
  BattlefieldSnapshotView,
  CardObjectView,
  GameEvent,
  SnapshotDto,
  StackItemView
} from "../types/protocol";
import type { WireTimelineDetail } from "../components/match/WireTimelineDetailPanel";
import { asArray, asRecord, asString } from "./collections";
import { promptReasonLabel } from "./formatters";
import { gameEventObjectRefPlan } from "./gameEventObjectRefs";
import {
  buildPromptInteractionModel,
  promptCommandBindingLabel,
  promptChoiceRoleLabel,
  promptChoiceSummaryObjectIds,
  type PromptCandidateComposerSummary,
  type PromptCandidatePresentationSummary,
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
  | "main-deck"
  | "rune"
  | "rune-deck"
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
  detail?: WireTimelineDetail;
  kind: string;
  role: string;
};

export type TableObjectServerRelationContext = {
  boundary?: string;
  candidateActions: string[];
  disabledCandidateCount?: number;
  enabledCandidateCount?: number;
  roles: string[];
  source?: string;
  stepSummary: string;
};

export type TableObjectCandidateContext = {
  category: string;
  commandFields: string[];
  commandType?: string;
  composerReason: string;
  composerState: PromptCandidateComposerSummary["state"];
  composerStateLabel: string;
  enabled: boolean;
  intent: string;
  label: string;
  priority: number;
  reason: string;
  requiredCommandFields: string[];
  roles: string[];
  selectionSteps: TableObjectCandidateStepContext[];
  uiHint: string;
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
  serverRelations: TableObjectServerRelationContext[];
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
  const serverRelationsById = buildServerRelationIndex(prompt?.serverFlow?.relatedObjects);
  const eventLinksById = buildEventLinks(events, objects);
  const stackRolesById = buildStackRoles(snapshot?.stack ?? []);
  const objectIds = new Set<string>([
    ...Object.keys(objects),
    ...Object.keys(zoneById),
    ...promptModel.objectById.keys(),
    ...serverObjectContextById.keys(),
    ...serverRelationsById.keys(),
    ...Object.keys(eventLinksById),
    ...Object.keys(stackRolesById)
  ]);
  const byId: Record<string, TableObjectContext> = {};

  for (const objectId of objectIds) {
    const object = objects[objectId];
    const promptSummary = promptModel.objectById.get(objectId);
    const serverContext = serverObjectContextById.get(objectId);
    const serverRelations = serverRelationsById.get(objectId) ?? [];
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
      contextBoundary: objectContextBoundary(serverContext, candidateSource, serverRelations),
      contextSource: objectContextSource(serverContext, candidateSource, serverRelations),
      eventLinks: eventLinksById[objectId] ?? [],
      object,
      objectId,
      ownerId: object?.ownerId,
      promptDisabledCount: serverContext?.disabledCandidateCount
        ?? relationCandidateCount(serverRelations, "disabled")
        ?? promptSummary?.disabledCandidateCount
        ?? 0,
      promptEnabledCount: serverContext?.enabledCandidateCount
        ?? relationCandidateCount(serverRelations, "enabled")
        ?? promptSummary?.enabledCandidateCount
        ?? 0,
      serverRelations,
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
    case "server-flow-related-object":
      return "服务端关联对象";
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

function buildServerRelationIndex(
  relatedObjects: ActionPromptServerFlowObjectRefDto[] | null | undefined
): Map<string, TableObjectServerRelationContext[]> {
  const byId = new Map<string, TableObjectServerRelationContext[]>();
  for (const ref of relatedObjects ?? []) {
    const objectId = ref.objectId?.trim();
    if (!objectId) {
      continue;
    }

    const relation: TableObjectServerRelationContext = {
      boundary: ref.candidateBoundary?.trim() || undefined,
      candidateActions: uniqueStrings(ref.candidateActions ?? []),
      disabledCandidateCount: numberOrUndefined(ref.disabledCandidateCount),
      enabledCandidateCount: numberOrUndefined(ref.enabledCandidateCount),
      roles: uniqueStrings([
        ref.role,
        ...(ref.candidateRoles ?? [])
      ]),
      source: ref.candidateSource?.trim() || "server-flow-related-object",
      stepSummary: serverRelationStepSummary(ref.candidateSteps)
    };
    byId.set(objectId, [...(byId.get(objectId) ?? []), relation]);
  }

  return byId;
}

function objectContextSource(
  context: ActionPromptObjectContextDto | undefined,
  candidateSource: TableObjectCandidateSource,
  serverRelations: TableObjectServerRelationContext[]
): string {
  const serverSource = context?.source?.trim() || context?.inspection?.source?.trim();
  if (serverSource) {
    return serverSource;
  }

  const relationSource = serverRelations.find((relation) => relation.source)?.source?.trim();
  if (relationSource) {
    return relationSource;
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
  candidateSource: TableObjectCandidateSource,
  serverRelations: TableObjectServerRelationContext[]
): string {
  const serverBoundary = context?.boundary?.trim() || context?.inspection?.boundary?.trim();
  if (serverBoundary) {
    return serverBoundary;
  }

  const relationBoundary = serverRelations.find((relation) => relation.boundary)?.boundary?.trim();
  if (relationBoundary) {
    return relationBoundary;
  }

  if (serverRelations.length > 0) {
    return "服务端流程只公开该对象与当前规则队列、结算链、费用或提示窗口的关联角色；没有候选命令时前端只做定位和检查，不推断可提交操作。";
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
  const presentation = candidatePresentationFromServer(candidate);

  return {
    category: presentation.category,
    commandFields: candidate.commandFields ?? [],
    commandType: candidate.commandType ?? candidate.action,
    composerReason: composer.reason,
    composerState: composer.state,
    composerStateLabel: composer.stateLabel,
    enabled: candidate.enabled,
    intent: presentation.intent,
    label: candidate.label || candidate.action,
    priority: presentation.priority,
    reason: promptReasonLabel(candidate.reason, candidate.enabled ? "可提交" : "暂不可提交"),
    requiredCommandFields: candidate.requiredCommandFields ?? [],
    roles: candidate.roles ?? [],
    selectionSteps: objectCandidateStepsFromServer(candidate.selectionSteps),
    uiHint: presentation.uiHint
  };
}

function candidateContextForObject(candidate: PromptCandidateSummary, objectId: string): TableObjectCandidateContext {
  const linkedChoices = candidate.choices.filter((choice) => promptChoiceSummaryObjectIds(choice).includes(objectId));
  const roleKeys = uniquePromptRoles(linkedChoices.map((choice) => choice.role));
  const commandBindings = commandBindingsForRoles(candidate.command?.bindings, roleKeys);
  const composer = candidate.composer ?? composerContextFromCommand(Boolean(candidate.command));

  return {
    category: candidate.presentation.category,
    commandFields: commandBindings.map(promptCommandBindingLabel),
    commandType: candidate.command?.cmdType,
    composerReason: composer.reason,
    composerState: composer.state,
    composerStateLabel: composer.stateLabel,
    enabled: candidate.enabled,
    intent: candidate.presentation.intent,
    label: candidate.label,
    priority: candidate.presentation.priority,
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
    }),
    uiHint: candidate.presentation.uiHint
  };
}

function candidatePresentationFromServer(candidate: ActionPromptObjectCandidateDto): PromptCandidatePresentationSummary {
  const presentation = candidate.presentation;
  return {
    category: normalizedPresentationText(presentation?.category, "custom"),
    intent: normalizedPresentationText(presentation?.intent, candidate.action.toLowerCase().replaceAll("_", "-")),
    priority: typeof presentation?.priority === "number" && Number.isFinite(presentation.priority)
      ? presentation.priority
      : 700,
    uiHint: normalizedPresentationText(presentation?.uiHint, "card-action")
  };
}

function normalizedPresentationText(value: string | null | undefined, fallback: string): string {
  const trimmed = value?.trim();
  return trimmed || fallback;
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

function serverRelationStepSummary(steps: ActionPromptObjectCandidateStepDto[] | null | undefined): string {
  const publicSteps = objectCandidateStepsFromServer(steps)
    .filter((step) => step.required || step.objectChoiceCount > 0)
    .sort((left, right) => {
      if (left.objectChoiceCount !== right.objectChoiceCount) {
        return right.objectChoiceCount - left.objectChoiceCount;
      }

      return left.index - right.index || left.role.localeCompare(right.role);
    });
  if (publicSteps.length === 0) {
    return "无候选步骤";
  }

  const visible = publicSteps.slice(0, 3).map((step) =>
    `${step.label}${step.required ? "*" : ""} ${step.objectChoiceCount}/${step.choiceCount}`);
  return publicSteps.length > 3 ? `${visible.join(" / ")} +${publicSteps.length - 3}` : visible.join(" / ");
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
  const laneLabelByBattlefieldId: Record<string, string> = {};
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
      laneLabelByBattlefieldId[battlefieldId] = laneLabel;
    }

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

  for (const [objectId, object] of Object.entries(objects)) {
    const locationZone = zoneContextFromObjectLocation(object, objectId, laneLabelByBattlefieldId, perspectivePlayerId);
    if (locationZone) {
      zones[objectId] = locationZone;
    }
  }

  return zones;
}

function zoneContextFromObjectLocation(
  object: CardObjectView | undefined,
  objectId: string,
  laneLabelByBattlefieldId: Record<string, string>,
  perspectivePlayerId: string
): TableObjectZoneContext | undefined {
  const location = asRecord(object?.location);
  const zone = asString(location.zone, "").trim().toUpperCase();
  if (!zone) {
    return undefined;
  }

  const battlefieldObjectId = asString(location.battlefieldObjectId, "").trim();
  const serverZoneKind = tableObjectZoneKindFromServer(asString(location.zoneKind, "").trim());
  const serverZoneLabel = asString(location.zoneLabel, "").trim();
  const playerId = asString(location.playerId, "").trim() || object?.controllerId || object?.ownerId || undefined;
  const sideLabel = playerSideLabel(playerId, perspectivePlayerId);
  const battlefieldLabel = laneLabelByBattlefieldId[battlefieldObjectId] ?? laneLabelByBattlefieldId[objectId] ?? "战场";
  const shared: Pick<TableObjectZoneContext, "battlefieldObjectId" | "playerId"> = {
    battlefieldObjectId: battlefieldObjectId || undefined,
    playerId
  };

  switch (zone) {
    case "BANISHED":
      return { ...shared, kind: serverZoneKind ?? "banished", label: `${sideLabel}${serverZoneLabel || "放逐区"}` };
    case "BASE":
      return serverZoneKind === "rune" || (!serverZoneKind && isRuneObject(object))
        ? { ...shared, kind: "rune", label: `${sideLabel}${serverZoneLabel || "已抽出符文"}` }
        : { ...shared, kind: serverZoneKind ?? "base", label: `${sideLabel}${serverZoneLabel || "基地"}` };
    case "BATTLEFIELD":
      if (serverZoneKind === "battlefield-site" || (!serverZoneKind && isBattlefieldSiteObject(object, objectId, laneLabelByBattlefieldId))) {
        return { ...shared, battlefieldObjectId: objectId, kind: "battlefield-site", label: `${battlefieldLabel}牌` };
      }

      return { ...shared, kind: "battlefield", label: `${battlefieldLabel} / ${sideLabel}单位` };
    case "CHAMPION":
      return { ...shared, kind: serverZoneKind ?? "champion", label: `${sideLabel}${serverZoneLabel || "英雄区"}` };
    case "GRAVEYARD":
      return { ...shared, kind: serverZoneKind ?? "graveyard", label: `${sideLabel}${serverZoneLabel || "已打出牌堆"}` };
    case "HAND":
      return { ...shared, kind: serverZoneKind ?? "hand", label: `${sideLabel}${serverZoneLabel || "手牌"}` };
    case "LEGEND":
      return { ...shared, kind: serverZoneKind ?? "legend", label: `${sideLabel}${serverZoneLabel || "传奇区"}` };
    case "MAIN_DECK":
      return { ...shared, kind: serverZoneKind ?? "main-deck", label: `${sideLabel}${serverZoneLabel || "主牌库"}` };
    case "RUNE_DECK":
      return { ...shared, kind: serverZoneKind ?? "rune-deck", label: `${sideLabel}${serverZoneLabel || "符文牌堆"}` };
    case "STACK":
      return { ...shared, kind: serverZoneKind ?? "stack", label: serverZoneLabel || "结算链" };
    default:
      return { ...shared, kind: serverZoneKind ?? "unknown", label: `${sideLabel}${serverZoneLabel || "服务端区域"}` };
  }
}

function tableObjectZoneKindFromServer(value: string): TableObjectZoneKind | undefined {
  switch (value) {
    case "banished":
    case "base":
    case "battlefield":
    case "battlefield-site":
    case "champion":
    case "graveyard":
    case "hand":
    case "legend":
    case "main-deck":
    case "rune":
    case "rune-deck":
    case "stack":
    case "unknown":
      return value;
    default:
      return undefined;
  }
}

function isBattlefieldSiteObject(
  object: CardObjectView | undefined,
  objectId: string,
  laneLabelByBattlefieldId: Record<string, string>
): boolean {
  return Boolean(laneLabelByBattlefieldId[objectId] || object?.tags?.some((tag) => tag === "CARD_TYPE:BATTLEFIELD"));
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

function buildEventLinks(events: GameEvent[], objects: Record<string, CardObjectView>): Record<string, TableObjectEventContext[]> {
  const links: Record<string, TableObjectEventContext[]> = {};
  for (const [index, event] of events.entries()) {
    const refPlan = gameEventObjectRefPlan(event);
    const detail = objectEventDetail(event, index, refPlan.source, refPlan.refs, objects);
    for (const ref of refPlan.refs) {
      if (!ref.objectId || ref.isHidden) {
        continue;
      }

      const existing = links[ref.objectId] ?? [];
      existing.push({
        description: event.description?.trim() || event.kind,
        detail,
        kind: event.kind,
        role: ref.role || "对象"
      });
      links[ref.objectId] = existing.slice(-4);
    }
  }
  return links;
}

function objectEventDetail(
  event: GameEvent,
  index: number,
  source: ReturnType<typeof gameEventObjectRefPlan>["source"],
  refs: NonNullable<GameEvent["objectRefs"]>,
  objects: Record<string, CardObjectView>
): WireTimelineDetail {
  const detailRefs = refs
    .map((ref) => objectEventDetailRef(ref, objects))
    .filter((ref): ref is WireTimelineDetail["refs"][number] => Boolean(ref));
  const description = event.description?.trim() || event.kind;

  return {
    id: `object-event:${event.kind}:${index}`,
    lines: [
      { label: "类型", value: event.kind },
      { label: "描述", value: description },
      { label: "对象", value: detailRefs.length > 0 ? `${detailRefs.length} 项` : "无" },
      { label: "对象来源", value: objectEventRefSourceLabel(source) },
      { label: "引用边界", value: objectEventRefBoundaryLabel(detailRefs) }
    ],
    refs: detailRefs,
    source: "event",
    subtitle: description,
    title: event.kind
  };
}

function objectEventDetailRef(
  ref: NonNullable<GameEvent["objectRefs"]>[number],
  objects: Record<string, CardObjectView>
): WireTimelineDetail["refs"][number] | undefined {
  const objectId = ref.objectId?.trim();
  if (!objectId) {
    return undefined;
  }

  const object = ref.isHidden ? undefined : objects[objectId];
  const visibility = ref.isHidden ? "hidden" : object ? "visible" : "missing";
  return {
    battlefieldObjectId: visibility === "hidden" ? undefined : ref.battlefieldObjectId ?? object?.location?.battlefieldObjectId,
    id: objectId,
    label: visibility === "hidden" ? "隐藏对象" : ref.cardNo?.trim() || object?.cardNo || undefined,
    role: ref.role?.trim() || "对象",
    visibility,
    zone: visibility === "hidden" ? undefined : ref.zone ?? object?.location?.zone
  };
}

function objectEventRefSourceLabel(source: ReturnType<typeof gameEventObjectRefPlan>["source"]): string {
  switch (source) {
    case "server":
      return "服务端摘要";
    case "payload":
      return "事件字段";
    case "none":
      return "无对象引用";
  }
}

function objectEventRefBoundaryLabel(refs: WireTimelineDetail["refs"]): string {
  if (refs.some((ref) => ref.visibility === "hidden")) {
    return "包含隐藏对象引用，仅显示服务端允许的占位。";
  }

  if (refs.some((ref) => ref.visibility === "missing")) {
    return "存在当前公开快照未定位对象。";
  }

  return "全部引用可由当前公开快照定位。";
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

function numberOrUndefined(value: number | null | undefined): number | undefined {
  return typeof value === "number" && Number.isFinite(value) ? Math.max(0, Math.floor(value)) : undefined;
}

function relationCandidateCount(
  relations: TableObjectServerRelationContext[],
  kind: "disabled" | "enabled"
): number | undefined {
  const values = relations
    .map((relation) => kind === "enabled" ? relation.enabledCandidateCount : relation.disabledCandidateCount)
    .filter((value): value is number => typeof value === "number" && Number.isFinite(value));
  if (values.length === 0) {
    return undefined;
  }

  return values.reduce((total, value) => total + value, 0);
}

function uniqueStrings(values: string[]): string[] {
  return [...new Set(values.map((value) => value.trim()).filter(Boolean))];
}

function uniquePromptRoles(values: PromptChoiceRole[]): PromptChoiceRole[] {
  return [...new Set(values)];
}

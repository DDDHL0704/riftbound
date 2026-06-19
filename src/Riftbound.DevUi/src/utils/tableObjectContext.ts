import type { ActionPromptDto, BattlefieldSnapshotView, CardObjectView, GameEvent, GameEventObjectRef, SnapshotDto, StackItemView } from "../types/protocol";
import { asArray, asRecord, asString } from "./collections";
import { buildPromptInteractionModel, promptChoiceRoleLabel, promptChoiceSummaryObjectIds } from "./promptInteraction";
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
  enabled: boolean;
  label: string;
  reason: string;
  roles: string[];
};

export type TableObjectContext = {
  candidateLinks: TableObjectCandidateContext[];
  cardNo?: string | null;
  controllerId?: string | null;
  eventLinks: TableObjectEventContext[];
  object?: CardObjectView;
  objectId: string;
  ownerId?: string | null;
  promptDisabledCount: number;
  promptEnabledCount: number;
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

const singularObjectKeyRoles: Record<string, string> = {
  attachedToObjectId: "贴附",
  attackerObjectId: "攻击",
  battlefieldId: "战场",
  battlefieldObjectId: "战场",
  cardObjectId: "卡牌",
  defenderObjectId: "防守",
  destroyedObjectId: "被摧毁",
  equipmentObjectId: "装备",
  hostObjectId: "贴附",
  objectId: "对象",
  runeObjectId: "符文",
  sourceObjectId: "来源",
  targetObjectId: "目标",
  unitObjectId: "单位"
};

const arrayObjectKeyRoles: Record<string, string> = {
  attackerObjectIds: "攻击",
  banishedObjectIds: "放逐",
  cardObjectIds: "卡牌",
  chosenObjectIds: "已选",
  defenderObjectIds: "防守",
  destroyedObjectIds: "被摧毁",
  discardedObjectIds: "弃置",
  exhaustedObjectIds: "横置",
  objectIds: "对象",
  participantObjectIds: "参与",
  paymentObjectIds: "费用",
  readyObjectIds: "重置",
  revealedObjectIds: "展示",
  runeObjectIds: "符文",
  sourceObjectIds: "来源",
  targetObjectIds: "目标",
  unitObjectIds: "单位"
};

export function buildTableObjectContextModel({
  events = [],
  perspectivePlayerId,
  prompt,
  snapshot
}: BuildTableObjectContextModelOptions): TableObjectContextModel {
  const objects = buildCardObjectIndex(snapshot);
  const zoneById = buildZoneIndex(snapshot, objects, perspectivePlayerId);
  const promptModel = buildPromptInteractionModel(prompt);
  const eventLinksById = buildEventLinks(events);
  const stackRolesById = buildStackRoles(snapshot?.stack ?? []);
  const objectIds = new Set<string>([
    ...Object.keys(objects),
    ...Object.keys(zoneById),
    ...promptModel.objectById.keys(),
    ...Object.keys(eventLinksById),
    ...Object.keys(stackRolesById)
  ]);
  const byId: Record<string, TableObjectContext> = {};

  for (const objectId of objectIds) {
    const object = objects[objectId];
    const promptSummary = promptModel.objectById.get(objectId);
    byId[objectId] = {
      candidateLinks: promptModel.candidates
        .filter((candidate) => candidate.choices.some((choice) => promptChoiceSummaryObjectIds(choice).includes(objectId)))
        .map((candidate) => ({
          enabled: candidate.enabled,
          label: candidate.label,
          reason: candidate.reason,
          roles: uniqueStrings(candidate.choices
            .filter((choice) => promptChoiceSummaryObjectIds(choice).includes(objectId))
            .map((choice) => promptChoiceRoleLabel(choice.role)))
        })),
      cardNo: object?.cardNo,
      controllerId: object?.controllerId,
      eventLinks: eventLinksById[objectId] ?? [],
      object,
      objectId,
      ownerId: object?.ownerId,
      promptDisabledCount: promptSummary?.disabledCandidateCount ?? 0,
      promptEnabledCount: promptSummary?.enabledCandidateCount ?? 0,
      stackRoles: stackRolesById[objectId] ?? [],
      stateLabels: objectStateLabels(object),
      zone: zoneById[objectId] ?? { kind: "unknown", label: "未定位区域" }
    };
  }

  return { byId };
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
    for (const ref of eventObjectRefs(event)) {
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

function eventObjectRefs(event: GameEvent): GameEventObjectRef[] {
  if (event.objectRefs?.length) {
    return event.objectRefs;
  }

  return collectPayloadObjectRefs(event.payload).map(({ objectId, role }) => ({ objectId, role }));
}

function collectPayloadObjectRefs(payload: Record<string, unknown>, depth = 0): Array<{ objectId: string; role: string }> {
  if (depth > 2) {
    return [];
  }

  const refs: Array<{ objectId: string; role: string }> = [];
  for (const [key, value] of Object.entries(payload)) {
    const singularRole = singularObjectKeyRoles[key];
    if (singularRole && typeof value === "string" && value.trim()) {
      refs.push({ objectId: value.trim(), role: singularRole });
      continue;
    }

    const arrayRole = arrayObjectKeyRoles[key];
    if (arrayRole && Array.isArray(value)) {
      refs.push(...value
        .filter((item): item is string => typeof item === "string" && item.trim().length > 0)
        .map((objectId) => ({ objectId: objectId.trim(), role: arrayRole })));
      continue;
    }

    if (isRecord(value)) {
      refs.push(...collectPayloadObjectRefs(value, depth + 1));
    } else if (Array.isArray(value)) {
      for (const item of value) {
        if (isRecord(item)) {
          refs.push(...collectPayloadObjectRefs(item, depth + 1));
        }
      }
    }
  }

  const seen = new Set<string>();
  return refs.filter((ref) => {
    const key = `${ref.role}:${ref.objectId}`;
    if (seen.has(key)) {
      return false;
    }
    seen.add(key);
    return true;
  });
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

function uniqueStrings(values: string[]): string[] {
  return [...new Set(values.map((value) => value.trim()).filter(Boolean))];
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

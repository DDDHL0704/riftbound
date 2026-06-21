import type { GameEvent, GameEventObjectRef } from "../types/protocol";
import { asArray, asRecord } from "./collections";

export type GameEventObjectRefSource = "none" | "payload" | "server";

export type GameEventObjectRefPlan = {
  refs: GameEventObjectRef[];
  source: GameEventObjectRefSource;
};

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

const singularObjectIdSuffix = "ObjectId";
const arrayObjectIdsSuffix = "ObjectIds";

export function gameEventObjectRefPlan(event: GameEvent): GameEventObjectRefPlan {
  const serverRefs = compactRefs(asArray<GameEventObjectRef>(event.objectRefs));
  if (serverRefs.length > 0) {
    return { refs: serverRefs, source: "server" };
  }

  const payloadRefs = collectPayloadObjectRefs(event.payload).map(({ objectId, role }) => ({ objectId, role }));
  if (payloadRefs.length > 0) {
    return { refs: payloadRefs, source: "payload" };
  }

  return { refs: [], source: "none" };
}

export function gameEventObjectRefPlans(events: GameEvent[]): GameEventObjectRefPlan[] {
  return events.map(gameEventObjectRefPlan);
}

export function gameEventObjectRefSourceLabel(source: GameEventObjectRefSource): string {
  switch (source) {
    case "server":
      return "服务端摘要";
    case "payload":
      return "事件字段";
    case "none":
      return "无对象引用";
  }
}

export function visibleGameEventObjectRefCount(plan: GameEventObjectRefPlan): number {
  return plan.refs.filter((ref) => ref.objectId?.trim() && !ref.isHidden).length;
}

function compactRefs(refs: GameEventObjectRef[]): GameEventObjectRef[] {
  const seen = new Set<string>();
  return refs
    .map((ref) => ({
      ...ref,
      objectId: ref.objectId?.trim() ?? "",
      role: ref.role?.trim() || "对象"
    }))
    .filter((ref) => {
      if (!ref.objectId) {
        return false;
      }
      const key = `${ref.role}:${ref.objectId}`;
      if (seen.has(key)) {
        return false;
      }
      seen.add(key);
      return true;
    });
}

function collectPayloadObjectRefs(payload: Record<string, unknown>, depth = 0): Array<{ objectId: string; role: string }> {
  if (depth > 2) {
    return [];
  }

  const refs: Array<{ objectId: string; role: string }> = [];
  for (const [key, value] of Object.entries(asRecord(payload))) {
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

    const inferredSingularRole = inferSingularObjectRefRole(key);
    if (inferredSingularRole && typeof value === "string" && value.trim()) {
      refs.push({ objectId: value.trim(), role: inferredSingularRole });
      continue;
    }

    const inferredArrayRole = inferArrayObjectRefRole(key);
    if (inferredArrayRole && Array.isArray(value)) {
      refs.push(...value
        .filter((item): item is string => typeof item === "string" && item.trim().length > 0)
        .map((objectId) => ({ objectId: objectId.trim(), role: inferredArrayRole })));
      continue;
    }

    if (Array.isArray(value)) {
      for (const item of value) {
        refs.push(...collectPayloadObjectRefs(asRecord(item), depth + 1));
      }
      continue;
    }

    refs.push(...collectPayloadObjectRefs(asRecord(value), depth + 1));
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

function inferSingularObjectRefRole(key: string): string | undefined {
  if (!key.endsWith(singularObjectIdSuffix) || key.length <= singularObjectIdSuffix.length) {
    return undefined;
  }

  return inferObjectRefRole(key.slice(0, -singularObjectIdSuffix.length));
}

function inferArrayObjectRefRole(key: string): string | undefined {
  if (!key.endsWith(arrayObjectIdsSuffix) || key.length <= arrayObjectIdsSuffix.length) {
    return undefined;
  }

  return inferObjectRefRole(key.slice(0, -arrayObjectIdsSuffix.length));
}

function inferObjectRefRole(rawPrefix: string): string {
  const prefix = rawPrefix.toLowerCase();
  if (prefix.includes("battlefield")) {
    return "战场";
  }

  if (prefix.includes("source") || prefix.includes("triggeredby")) {
    return "来源";
  }

  if (prefix.includes("target") || prefix.includes("chosen") || prefix.includes("selected")) {
    return "目标";
  }

  if (prefix.includes("attacker")) {
    return "攻击";
  }

  if (prefix.includes("defender")) {
    return "防守";
  }

  if (prefix.includes("destroy") || prefix.includes("defeated") || prefix.includes("removed") || prefix.includes("cleared")) {
    return "被移除";
  }

  if (prefix.includes("discard")) {
    return "弃置";
  }

  if (prefix.includes("returned") || prefix.includes("recalled")) {
    return "返回";
  }

  if (prefix.includes("recycled")) {
    return "回收";
  }

  if (prefix.includes("revealed")) {
    return "展示";
  }

  if (prefix.includes("rune")) {
    return "符文";
  }

  if (prefix.includes("equipment") || prefix.includes("armament")) {
    return "装备";
  }

  if (prefix.includes("token")) {
    return "衍生";
  }

  if (prefix.includes("played")) {
    return "打出";
  }

  if (prefix.includes("activated")) {
    return "激活";
  }

  if (prefix.includes("ready") || prefix.includes("readied")) {
    return "重置";
  }

  if (prefix.includes("moved") || prefix.includes("destination")) {
    return "移动";
  }

  if (prefix.includes("unit") || prefix.includes("participant")) {
    return "单位";
  }

  if (prefix.includes("hidden")) {
    return "隐藏";
  }

  return "对象";
}

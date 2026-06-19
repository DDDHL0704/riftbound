import type { CardObjectView, ErrorDto, GameEvent, GameEventObjectRef } from "../types/protocol";
import type { WireTimelineDetail } from "../components/match/WireTimelineDetailPanel";
import { asArray, asRecord } from "./collections";
import { errorCodeLabel, errorMessageLabel } from "./errors";
import { redactInternalText } from "./redaction";

export type LogDensity = "compact" | "standard" | "detailed";

export type EventLogObjectRef = {
  id: string;
  label?: string;
  role: string;
};

export type EventLogErrorRowPlan = {
  key: string;
  message: string;
  title: string;
};

export type EventLogEventRowPlan = {
  description: string;
  detail: WireTimelineDetail;
  key: string;
  kind: string;
  refs: EventLogObjectRef[];
  title: string;
};

export type EventLogPlan = {
  density: LogDensity;
  emptyLabel?: string;
  errorCount: number;
  errors: EventLogErrorRowPlan[];
  eventCount: number;
  events: EventLogEventRowPlan[];
  hiddenEventCount: number;
  state: "empty" | "errors" | "events" | "mixed";
  visibleEventCount: number;
};

export type BuildEventLogPlanInput = {
  density?: LogDensity;
  errors: ErrorDto[];
  events: GameEvent[];
  objectIndex?: Record<string, CardObjectView>;
};

const eventKindLabels: Record<string, string> = {
  ABILITY_ACTIVATED: "激活能力",
  ABILITY_NO_EFFECT: "能力未生效",
  ABILITY_RESOLVED: "能力结算",
  BATTLE_CLOSED: "战斗结束",
  BATTLE_DAMAGE_ASSIGNMENT_OPENED: "战斗伤害分配开启",
  BATTLE_DAMAGE_STEP_STARTED: "战斗伤害步骤",
  BATTLE_DECLARED: "声明战斗",
  BATTLEFIELD_CONTESTED: "战场争夺",
  BATTLEFIELD_CONTROL_RESOLVED: "战场控制结算",
  BATTLEFIELD_CONQUERED: "征服战场",
  BATTLEFIELD_HELD: "据守战场",
  BATTLEFIELD_REPLACED: "战场替换",
  BATTLEFIELD_REPLACEMENT_APPLIED: "战场替换已应用",
  BATTLEFIELD_SCORE_PREVENTED: "战场得分被阻止",
  BATTLEFIELD_STANDBY_REMOVED: "待命清理",
  BATTLEFIELD_TOKEN_CREATED: "战场指示物",
  BATTLEFIELD_TRIGGER_RESOLVED: "战场触发结算",
  BATTLE_NO_RESULT: "战斗无结果",
  BATTLE_RESPONSE_PRIORITY_CLOSED: "战斗回应优先权关闭",
  BATTLE_RESPONSE_PRIORITY_OPENED: "战斗回应优先权开启",
  BOON_CONSUMED: "消耗增益",
  BOON_GRANTED: "获得增益",
  BURNOUT_APPLIED: "燃尽",
  CARDS_BANISHED: "放逐卡牌",
  CARDS_DISCARDED: "弃置卡牌",
  CARDS_MILLED: "磨牌",
  CARDS_RECYCLED: "回收卡牌",
  CARDS_REVEALED: "展示卡牌",
  CARD_DISCARDED: "弃置卡牌",
  CARD_DRAWN: "抽牌",
  CARD_HIDDEN: "布置待命",
  CARD_PLAYED: "打出卡牌",
  CARD_RETURNED_TO_HAND: "卡牌回手",
  CARD_REVEALED: "翻开待命",
  CLEANUP_REPEATED: "清理循环",
  COMBAT_DAMAGE_ASSIGNED: "战斗伤害分配",
  COMBAT_DAMAGE_ASSIGNMENT_SUBMITTED: "提交战斗伤害分配",
  COST_PAID: "支付费用",
  DAMAGE_APPLIED: "造成伤害",
  DAMAGE_REMOVED: "移除伤害",
  DECK_SUBMITTED: "提交卡组",
  DESTROY_REPLACEMENT_EFFECT_APPLIED: "摧毁替代效果",
  DEV_SCENARIO_SEEDED: "载入测试状态",
  EQUIPMENT_ATTACHED: "装备装配",
  EQUIPMENT_CONTROL_CHANGED: "装备控制权变更",
  EQUIPMENT_CONTROL_RETURNED: "装备控制权返还",
  EQUIPMENT_DESTROYED: "装备摧毁",
  EQUIPMENT_DETACHED: "装备脱离",
  EQUIPMENT_EXHAUSTED: "装备横置",
  EQUIPMENT_MOVED_WITH_UNIT: "装备随单位移动",
  EQUIPMENT_PLAYED_TO_BASE: "装备进入基地",
  EQUIPMENT_READIED: "装备重置",
  EQUIPMENT_REATTACHED: "装备重新贴附",
  EQUIPMENT_RECALLED_TO_BASE: "装备召回基地",
  EQUIPMENT_RETURNED_TO_HAND: "装备回手",
  EQUIPMENT_TOKEN_CREATED: "装备指示物",
  EXPERIENCE_GAINED: "获得经验",
  EXPERIENCE_SPENT: "花费经验",
  EXTRA_TURN_SCHEDULED: "追加回合",
  FOCUS_PASSED: "让过焦点",
  HAND_CHOICE_REQUESTED: "请求手牌选择",
  HAND_CHOICE_RESOLVED: "手牌选择结算",
  HAND_CHOICE_SKIPPED: "跳过手牌选择",
  LEGEND_ABILITY_ACTIVATED: "传奇行动",
  LEGEND_EXHAUSTED: "传奇横置",
  LEGEND_READIED: "传奇重置",
  LEGEND_TRIGGER_RESOLVED: "传奇触发结算",
  MAIN_PHASE_BEGAN: "进入主阶段",
  MANA_GAINED: "获得法力",
  MATCH_STARTED: "对局开始",
  MATCH_WON: "对局胜利",
  MULLIGAN_COMPLETED: "完成起手调度",
  MULLIGAN_PHASE_COMPLETED: "起手调度结束",
  OBJECTS_READIED: "对象重置",
  OBJECT_TAG_ADDED: "添加对象标签",
  OFFICIAL_BATTLEFIELD_SELECTED: "选择官方战场",
  OFFICIAL_OPENING_STARTED: "正式开局开始",
  OPENING_HAND_DRAWN: "抽取起手牌",
  PAYMENT_WINDOW_CLOSED: "支付窗口关闭",
  PAYMENT_WINDOW_OPENED: "支付窗口开启",
  PLAYER_READY: "玩家准备",
  POWER_GAINED: "获得符能",
  POWER_MODIFIED_UNTIL_END_OF_TURN: "临时战力修正",
  POWER_MODIFIER_EXPIRED: "战力修正过期",
  PRIORITY_PASSED: "让过优先权",
  ROAM_GRANTED: "获得游走",
  RUNES_CALLED: "召出符文",
  RUNE_CHANNELLED: "引导符文",
  RUNE_POOL_CLEARED: "清空符文池",
  RUNE_READIED: "符文重置",
  RUNE_READY_SCHEDULED: "安排符文重置",
  RUNE_RECYCLED: "回收符文",
  RUNE_TAPPED: "横置符文",
  SCORE_GAINED: "获得分数",
  SPELL_DUEL_CLOSED: "法术对决关闭",
  SPELL_DUEL_STARTED: "法术对决开始",
  STACK_ITEM_ADDED: "加入结算链",
  STACK_ITEM_COUNTERED: "无效化法术",
  STACK_ITEM_CONTROL_GAINED: "获得结算链控制",
  STACK_ITEM_RESOLVED: "结算链项目结算",
  STANDBY_HIDE_PERMISSION_GRANTED: "获得待命布置许可",
  STATUS_EFFECT_APPLIED: "状态效果",
  TEMPORARY_PAYMENT_RESOURCE_CLEARED: "临时支付资源清除",
  TEMPORARY_PAYMENT_RESOURCE_SPENT: "临时支付资源消耗",
  TRIGGER_EXPIRED: "触发已过期",
  TRIGGER_PAYMENT_DECLINED: "拒绝触发支付",
  TRIGGER_QUEUED: "触发排队",
  TRIGGER_RESOLVED: "触发结算",
  TRIGGERS_MOVED_TO_STACK: "触发进入结算链",
  TRIGGERS_ORDERED: "触发排序完成",
  TURN_BEGAN: "回合开始",
  TURN_ENDED: "回合结束",
  TURN_END_CLEANUP_STARTED: "回合结束清理",
  TURN_END_DECLARED: "宣告结束回合",
  TURN_PLAYER_ADVANCED: "回合玩家推进",
  TURN_START_BEGAN: "回合开始",
  UNIT_BANISHED: "单位放逐",
  UNIT_CONQUEST_EFFECT_ACTIVATED: "单位征服效果",
  UNIT_CONTROL_GAINED: "获得单位控制",
  UNIT_CONTROL_RETURNED: "返还单位控制",
  UNIT_DESTROYED: "单位摧毁",
  UNIT_EXHAUSTED: "单位横置",
  UNIT_LOCATIONS_SWAPPED: "交换单位位置",
  UNIT_MOVED_TO_BASE: "单位移至基地",
  UNIT_MOVED_TO_BATTLEFIELD: "单位进入战场",
  UNIT_MOVED_TO_UNIT_LOCATION: "单位移至单位处",
  UNIT_PLAYED_TO_BASE: "单位进入基地",
  UNIT_PLAYED_TO_BATTLEFIELD: "单位进入战场",
  UNIT_READIED: "单位重置",
  UNIT_RECALLED_TO_BASE: "单位召回基地",
  UNIT_RECALLED_TO_OWNER_BASE: "单位召回所属者基地",
  UNIT_RETURNED_TO_CHAMPION_ZONE: "单位返回英雄区",
  UNIT_RETURNED_TO_DECK: "单位回到牌堆",
  UNIT_RETURNED_TO_HAND: "单位回手",
  UNIT_TOKEN_CREATED: "单位指示物",
  UNTIL_END_OF_TURN_EXPIRED: "回合结束效果过期"
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

export function buildEventLogPlan({
  density = "standard",
  errors,
  events,
  objectIndex = {}
}: BuildEventLogPlanInput): EventLogPlan {
  const visibleEvents = density === "compact" ? events.slice(-12) : events;
  const errorRows = errors.map((error, index) => ({
    key: `error-${index}`,
    message: errorMessageLabel(error),
    title: errorCodeLabel(error.code)
  }));
  const eventRows = visibleEvents.map((event, index) => eventRowPlan(event, events.length - visibleEvents.length + index, objectIndex));

  return {
    density,
    emptyLabel: events.length === 0 && errors.length === 0 ? "暂无服务端事件。" : undefined,
    errorCount: errors.length,
    errors: errorRows,
    eventCount: events.length,
    events: eventRows,
    hiddenEventCount: events.length - visibleEvents.length,
    state: eventLogState(errors.length, events.length),
    visibleEventCount: visibleEvents.length
  };
}

export function eventKindLabel(kind: string) {
  return eventKindLabels[kind] ?? "服务端事件";
}

export function eventDescriptionLabel(event: GameEvent) {
  if (event.kind === "DEV_SCENARIO_SEEDED") {
    return "测试状态已载入";
  }

  const description = event.description?.trim() || eventKindLabel(event.kind);
  return redactInternalText(description);
}

function eventLogState(errorCount: number, eventCount: number): EventLogPlan["state"] {
  if (errorCount > 0 && eventCount > 0) {
    return "mixed";
  }

  if (errorCount > 0) {
    return "errors";
  }

  if (eventCount > 0) {
    return "events";
  }

  return "empty";
}

function eventRowPlan(event: GameEvent, index: number, objects: Record<string, CardObjectView>): EventLogEventRowPlan {
  const refs = eventObjectRefs(event, objects);
  return {
    description: eventDescriptionLabel(event),
    detail: eventDetail(event, index, refs),
    key: `${event.kind}-${index}`,
    kind: event.kind,
    refs,
    title: eventKindLabel(event.kind)
  };
}

function eventObjectRefs(event: GameEvent, objects: Record<string, CardObjectView>): EventLogObjectRef[] {
  const serverRefs = event.objectRefs
    ?.map(serverObjectRef)
    .filter((ref): ref is EventLogObjectRef => Boolean(ref));
  return serverRefs?.length ? serverRefs : collectEventObjectRefs(event.payload, objects, 0);
}

function eventDetail(event: GameEvent, index: number, refs: EventLogObjectRef[]): WireTimelineDetail {
  return {
    id: `event:${event.kind}:${index}`,
    lines: [
      { label: "类型", value: eventKindLabel(event.kind) },
      { label: "描述", value: eventDescriptionLabel(event) },
      { label: "对象", value: refs.length > 0 ? `${refs.length} 项` : "无" },
      { label: "对象来源", value: event.objectRefs?.length ? "服务端摘要" : "事件字段" }
    ],
    refs,
    source: "event",
    subtitle: eventDescriptionLabel(event),
    title: eventKindLabel(event.kind)
  };
}

function serverObjectRef(ref: GameEventObjectRef): EventLogObjectRef | undefined {
  const objectId = ref.objectId?.trim();
  if (!objectId) {
    return undefined;
  }

  return {
    id: objectId,
    label: ref.isHidden ? "隐藏对象" : ref.cardNo?.trim() || undefined,
    role: ref.role?.trim() || "对象"
  };
}

function collectEventObjectRefs(record: Record<string, unknown>, objects: Record<string, CardObjectView>, depth: number): EventLogObjectRef[] {
  if (depth > 2) {
    return [];
  }

  const refs: EventLogObjectRef[] = [];
  for (const [key, value] of Object.entries(record)) {
    const singularRole = singularObjectKeyRoles[key];
    if (singularRole && typeof value === "string" && isVisibleObjectRef(value, objects)) {
      refs.push({ id: value, role: singularRole });
      continue;
    }

    const arrayRole = arrayObjectKeyRoles[key];
    if (arrayRole) {
      refs.push(...asArray<unknown>(value)
        .filter((item): item is string => typeof item === "string" && isVisibleObjectRef(item, objects))
        .map((objectId) => ({ id: objectId, role: arrayRole })));
      continue;
    }

    if (Array.isArray(value)) {
      for (const item of value) {
        if (item && typeof item === "object") {
          refs.push(...collectEventObjectRefs(asRecord(item), objects, depth + 1));
        }
      }
      continue;
    }

    if (value && typeof value === "object") {
      refs.push(...collectEventObjectRefs(asRecord(value), objects, depth + 1));
    }
  }

  return refs;
}

function isVisibleObjectRef(objectId: string, objects: Record<string, CardObjectView>): boolean {
  return objectId === "HIDDEN" || Boolean(objects[objectId]);
}

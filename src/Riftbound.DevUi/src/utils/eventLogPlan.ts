import type { CardObjectView, ErrorDto, GameEvent, GameEventObjectRef } from "../types/protocol";
import type { WireTimelineDetail } from "../components/match/WireTimelineDetailPanel";
import { errorCodeLabel, errorMessageLabel } from "./errors";
import { gameEventObjectRefPlan, gameEventObjectRefSourceLabel, type GameEventObjectRefSource } from "./gameEventObjectRefs";
import { redactInternalText } from "./redaction";

export type LogDensity = "compact" | "standard" | "detailed";

export type EventLogObjectRef = {
  battlefieldObjectId?: string | null;
  id: string;
  label?: string;
  role: string;
  visibility: "hidden" | "missing" | "visible";
  zone?: string | null;
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

export type EventLogCollapsedSummaryPlan = {
  eventCount: number;
  hiddenRefCount: number;
  missingRefCount: number;
  refCount: number;
  serverRefCount: number;
  visibleRefCount: number;
};

export type EventLogPlan = {
  collapsedSummary?: EventLogCollapsedSummaryPlan;
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
  BATTLE_SKIPPED: "跳过战斗",
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
  MAIN_DECK_CARDS_REVEALED: "展示主牌堆",
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

export function buildEventLogPlan({
  density = "standard",
  errors,
  events,
  objectIndex = {}
}: BuildEventLogPlanInput): EventLogPlan {
  const visibleEvents = density === "compact" ? events.slice(-12) : events;
  const collapsedEvents = density === "compact" ? events.slice(0, Math.max(0, events.length - visibleEvents.length)) : [];
  const errorRows = errors.map((error, index) => ({
    key: `error-${index}`,
    message: errorMessageLabel(error),
    title: errorCodeLabel(error.code)
  }));
  const eventRows = visibleEvents.map((event, index) => eventRowPlan(event, events.length - visibleEvents.length + index, objectIndex));

  return {
    collapsedSummary: collapsedEventSummary(collapsedEvents, objectIndex),
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
  const refPlan = gameEventObjectRefPlan(event);
  const refs = eventObjectRefs(refPlan.refs, objects, refPlan.source);
  return {
    description: eventDescriptionLabel(event),
    detail: eventDetail(event, index, refs, refPlan.source),
    key: `${event.kind}-${index}`,
    kind: event.kind,
    refs,
    title: eventKindLabel(event.kind)
  };
}

function eventObjectRefs(
  refs: GameEventObjectRef[],
  objects: Record<string, CardObjectView>,
  source: GameEventObjectRefSource
): EventLogObjectRef[] {
  const eventRefs = refs
    .map((ref) => serverObjectRef(ref, objects))
    .filter((ref): ref is EventLogObjectRef => Boolean(ref));
  return source === "payload"
    ? eventRefs.filter((ref) => ref.visibility !== "missing")
    : eventRefs;
}

function collapsedEventSummary(
  events: GameEvent[],
  objects: Record<string, CardObjectView>
): EventLogCollapsedSummaryPlan | undefined {
  if (events.length === 0) {
    return undefined;
  }

  const summary: EventLogCollapsedSummaryPlan = {
    eventCount: events.length,
    hiddenRefCount: 0,
    missingRefCount: 0,
    refCount: 0,
    serverRefCount: 0,
    visibleRefCount: 0
  };

  for (const event of events) {
    const refPlan = gameEventObjectRefPlan(event);
    const refs = eventObjectRefs(refPlan.refs, objects, refPlan.source);
    summary.refCount += refs.length;
    if (refPlan.source === "server") {
      summary.serverRefCount += refs.length;
    }
    for (const ref of refs) {
      if (ref.visibility === "hidden") {
        summary.hiddenRefCount += 1;
      } else if (ref.visibility === "missing") {
        summary.missingRefCount += 1;
      } else {
        summary.visibleRefCount += 1;
      }
    }
  }

  return summary;
}

function eventDetail(
  event: GameEvent,
  index: number,
  refs: EventLogObjectRef[],
  source: GameEventObjectRefSource
): WireTimelineDetail {
  return {
    id: `event:${event.kind}:${index}`,
    lines: [
      { label: "类型", value: eventKindLabel(event.kind) },
      { label: "描述", value: eventDescriptionLabel(event) },
      { label: "对象", value: refs.length > 0 ? `${refs.length} 项` : "无" },
      { label: "对象来源", value: gameEventObjectRefSourceLabel(source) },
      { label: "引用边界", value: objectRefBoundaryLabel(refs) }
    ],
    refs,
    source: "event",
    subtitle: eventDescriptionLabel(event),
    title: eventKindLabel(event.kind)
  };
}

function serverObjectRef(ref: GameEventObjectRef, objects: Record<string, CardObjectView>): EventLogObjectRef | undefined {
  const objectId = ref.objectId?.trim();
  if (!objectId) {
    return undefined;
  }
  const visibility = eventRefVisibility(ref, objects);
  const object = visibility === "hidden" ? undefined : objects[objectId];

  return {
    battlefieldObjectId: visibility === "hidden" ? undefined : ref.battlefieldObjectId ?? object?.location?.battlefieldObjectId,
    id: objectId,
    label: visibility === "hidden" ? "隐藏对象" : ref.cardNo?.trim() || undefined,
    role: ref.role?.trim() || "对象",
    visibility,
    zone: visibility === "hidden" ? undefined : ref.zone ?? object?.location?.zone
  };
}

function eventRefVisibility(ref: GameEventObjectRef, objects: Record<string, CardObjectView>): EventLogObjectRef["visibility"] {
  if (ref.isHidden || ref.objectId === "HIDDEN") {
    return "hidden";
  }

  return ref.objectId && objects[ref.objectId] ? "visible" : "missing";
}

function objectRefBoundaryLabel(refs: EventLogObjectRef[]): string {
  if (refs.length === 0) {
    return "无引用";
  }

  const hidden = refs.filter((ref) => ref.visibility === "hidden").length;
  const missing = refs.filter((ref) => ref.visibility === "missing").length;
  const visible = refs.length - hidden - missing;
  if (missing > 0) {
    return `可见 ${visible} / 隐藏 ${hidden} / 缺失 ${missing}`;
  }

  if (hidden > 0) {
    return `可见 ${visible} / 隐藏 ${hidden}`;
  }

  return `可见 ${visible}`;
}

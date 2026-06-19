import type {
  ActionPromptDto,
  BattleResolutionView,
  BattlefieldResolutionView,
  CardObjectView,
  PendingTaskView,
  SnapshotDto,
  StackItemView,
  TriggerQueueItemView
} from "../../types/protocol";
import { Children, type ReactNode } from "react";
import { asArray, asNumber, asRecord, asString } from "../../utils/collections";
import { matchPhaseLabel, timingStateLabel } from "../../utils/formatters";
import { redactInternalText } from "../../utils/redaction";
import { buildCardObjectIndex } from "../../utils/snapshotObjectIndex";
import { StatusPill } from "../ui/StatusPill";

type WireRuleQueuePanelProps = {
  onInspectObject?: (objectId: string) => void;
  playerId: string;
  prompt?: ActionPromptDto;
  selectedObjectId?: string;
  snapshot?: SnapshotDto;
};

type ObjectIndex = Record<string, CardObjectView>;
type RuleObjectRef = {
  id: string;
  role: string;
};

const stackEffectLabels: Record<string, string> = {
  ABILITY: "技能",
  HEXTECH_RAY_DAMAGE_3: "海克斯射线伤害",
  LEGEND_ABILITY: "传奇技能",
  REVEAL_CARD: "翻开待命",
  SPELL: "法术",
  TRIGGER: "触发"
};

const taskKindLabels: Record<string, string> = {
  BATTLEFIELD_CONTESTED: "战场控制检查",
  DESTROY_LETHAL_UNIT: "致命伤害清理",
  DESTROY_ZERO_POWER_UNIT: "0 战力清理",
  REMOVE_ILLEGAL_STANDBY: "待命清理",
  RECALL_UNATTACHED_EQUIPMENT: "装备清理",
  START_BATTLE: "开始战斗",
  START_SPELL_DUEL: "开始法术对决"
};

const taskReasonLabels: Record<string, string> = {
  ADDITIONAL_COST: "额外费用",
  BATTLE_CLEANUP: "战斗清理",
  BATTLE_CLEANUP_ATTACKER_RECALL: "战斗后召回攻击者",
  BATTLEFIELD_CONTROL_CLEANUP: "战场控制清理",
  BATTLEFIELD_CONTESTED: "战场争夺",
  LETHAL_DAMAGE: "致命伤害",
  UNATTACHED_EQUIPMENT_CLEANUP: "装备脱离清理",
  ZERO_POWER: "0 战力"
};

const taskPhaseLabels: Record<string, string> = {
  BATTLE_TASKS: "战斗任务",
  BATTLEFIELD_TASKS: "战场任务",
  IDLE: "空闲",
  SPELL_DUEL_TASKS: "法术对决任务",
  STATE_BASED_CLEANUP: "状态清理"
};

const battlefieldResolutionLabels: Record<string, string> = {
  CONQUERED: "征服",
  CONTROL_RESOLVED: "控制结算",
  HELD: "据守"
};

const battleResolutionLabels: Record<string, string> = {
  CLOSED: "战斗结束",
  NO_RESULT: "战斗无结果"
};

export function WireRuleQueuePanel({ onInspectObject, playerId, prompt, selectedObjectId, snapshot }: WireRuleQueuePanelProps) {
  const timing = asRecord(snapshot?.timing);
  const queue = asRecord(timing.pendingTaskQueue);
  const turnWindow = asRecord(timing.turnWindow);
  const stack = asArray<Record<string, unknown>>(snapshot?.stack).map(stackItemFromRecord);
  const pendingTasks = asArray<Record<string, unknown>>(queue.tasks).map(taskFromRecord);
  const battlefieldTasks = asArray<Record<string, unknown>>(timing.battlefieldTasks).map(taskFromRecord);
  const triggerQueue = asArray<Record<string, unknown>>(timing.triggerQueue).map(triggerFromRecord);
  const battlefieldResolutions = asArray<Record<string, unknown>>(timing.battlefieldResolutions).map(battlefieldResolutionFromRecord);
  const battleResolutions = asArray<Record<string, unknown>>(timing.battleResolutions).map(battleResolutionFromRecord);
  const objects = buildCardObjectIndex(snapshot);
  const phase = asString(timing.phase, snapshot?.turnState ?? "");
  const timingState = asString(timing.timingState, snapshot?.turnState ?? "");
  const actingPlayerId = asString(turnWindow.actingPlayerId, asString(timing.priorityPlayerId, ""));
  const promptOwner = prompt?.playerId ?? asString(timing.promptPlayerId, "");
  const activeTaskId = asString(queue.activeTaskId, "");
  const isBlocking = Boolean(queue.isBlocking);

  return (
    <section className="wire-rule-queue" aria-label="服务端规则队列">
      <header className="wire-rule-queue-header">
        <div>
          <strong>结算链 / 规则事件</strong>
          <span>tick {snapshot?.tick ?? "无"} / prompt {prompt?.promptId ? "已提供" : "无"}</span>
        </div>
        <StatusPill tone={isBlocking ? "warn" : stack.length > 0 ? "info" : "neutral"}>
          {isBlocking ? "规则阻塞" : stack.length > 0 ? "等待响应" : "空闲"}
        </StatusPill>
      </header>

      <div className="wire-rule-state-grid">
        <RuleMetric label="阶段" value={matchPhaseLabel(phase)} />
        <RuleMetric label="窗口" value={timingStateLabel(timingState)} />
        <RuleMetric label="行动权" mine={actingPlayerId === playerId} value={actingPlayerId || "无"} />
        <RuleMetric label="提示归属" mine={promptOwner === playerId} value={promptOwner || "无"} />
        <RuleMetric label="结算链" value={`${stack.length} 项`} />
        <RuleMetric label="任务" value={`${pendingTasks.length + battlefieldTasks.length} 项`} />
        <RuleMetric label="触发" value={`${triggerQueue.length} 项`} />
        <RuleMetric label="近期事件" value={`${battlefieldResolutions.length + battleResolutions.length} 项`} />
      </div>

      <RuleSection title="结算链" emptyLabel="当前无结算链项目。">
        {stack.map((item, index) => (
          <article className="wire-rule-item" key={item.stackItemId ?? `stack-${index}`}>
            <div>
              <strong>项目 {stack.length - index}</strong>
              <span>{stackEffectLabel(item.effectKind)}</span>
            </div>
            <RuleLine label="控制者" mine={item.controllerId === playerId} value={item.controllerId ?? "未知"} />
            <RuleLine label="来源" value={sourceLabel(item.sourceObjectId, item.cardNo, objects)} />
            <RuleLine label="目标" value={objectListLabel(item.targetObjectIds, objects)} />
            {item.damageAmount != null && item.damageAmount > 0 && <RuleLine label="伤害" value={String(item.damageAmount)} />}
            {item.destination && <RuleLine label="去向" value={zoneLabel(item.destination)} />}
            <RuleObjectRefs objects={objects} onInspectObject={onInspectObject} refs={stackObjectRefs(item)} selectedObjectId={selectedObjectId} />
          </article>
        ))}
      </RuleSection>

      <RuleSection title="规则任务" emptyLabel="当前无待处理规则任务。">
        {(pendingTasks.length > 0 || battlefieldTasks.length > 0) && (
          <div className="wire-rule-note">
            <span>队列阶段：{taskPhaseLabel(asString(queue.phase, ""))}</span>
            <span>活动任务：{activeTaskId ? "处理中" : "无"}</span>
            <span>{isBlocking ? "阻塞普通行动" : "不阻塞普通行动"}</span>
          </div>
        )}
        {[...pendingTasks, ...battlefieldTasks].map((task, index) => (
          <article className="wire-rule-item" key={task.taskId ?? `task-${index}`}>
            <div>
              <strong>{taskKindLabel(task.kind)}</strong>
              <span>{task.status ? protocolValue(task.status, "服务端状态") : "状态未提供"}</span>
            </div>
            <RuleLine label="原因" value={taskReasonLabel(task.reason)} />
            <RuleLine label="战场" value={task.battlefieldObjectId ? objectLabel(task.battlefieldObjectId, objects) : "无"} />
            <RuleLine label="行动玩家" mine={task.actingPlayerId === playerId} value={task.actingPlayerId ?? "无"} />
            <RuleLine label="参与对象" value={objectListLabel(task.participantObjectIds, objects)} />
            {task.stackItemIds && task.stackItemIds.length > 0 && <RuleLine label="关联结算链" value={`${task.stackItemIds.length} 项`} />}
            {task.spellDuelId && <RuleLine label="法术对决" value="服务端已创建" />}
            {task.battleId && <RuleLine label="战斗" value="服务端已创建" />}
            <RuleObjectRefs objects={objects} onInspectObject={onInspectObject} refs={taskObjectRefs(task)} selectedObjectId={selectedObjectId} />
          </article>
        ))}
      </RuleSection>

      <RuleSection title="触发队列" emptyLabel="当前无待排序或待结算触发。">
        {triggerQueue.map((trigger, index) => (
          <article className="wire-rule-item" key={trigger.triggerId ?? `trigger-${index}`}>
            <div>
              <strong>触发 {index + 1}</strong>
              <span>{stackEffectLabel(trigger.effectKind)}</span>
            </div>
            <RuleLine label="控制者" mine={trigger.controllerId === playerId} value={trigger.controllerId ?? "无"} />
            <RuleLine label="来源" value={trigger.sourceVisibility === "HIDDEN" ? "隐藏来源" : objectLabel(trigger.sourceObjectId, objects)} />
            <RuleLine label="来源事件" value={eventLabel(trigger.triggeredByEventKind)} />
            <RuleObjectRefs objects={objects} onInspectObject={onInspectObject} refs={triggerObjectRefs(trigger)} selectedObjectId={selectedObjectId} />
          </article>
        ))}
      </RuleSection>

      <RuleSection title="近期规则事件" emptyLabel="暂无近期战场或战斗结算。">
        {battlefieldResolutions.map((resolution, index) => (
          <article className="wire-rule-item" key={resolution.resolutionId ?? `battlefield-resolution-${index}`}>
            <div>
              <strong>{battlefieldResolutionLabel(resolution.kind)}</strong>
              <span>tick {resolution.tick ?? "无"}</span>
            </div>
            <RuleLine label="战场" value={objectLabel(resolution.battlefieldObjectId, objects)} />
            <RuleLine label="控制者" mine={(resolution.playerId ?? resolution.controllerId) === playerId} value={resolution.playerId ?? resolution.controllerId ?? "无"} />
            <RuleLine label="参与对象" value={objectListLabel(resolution.participantObjectIds, objects)} />
            <RuleLine label="事件" value={eventListLabel(resolution.relatedEventKinds)} />
            <RuleObjectRefs objects={objects} onInspectObject={onInspectObject} refs={battlefieldResolutionObjectRefs(resolution)} selectedObjectId={selectedObjectId} />
          </article>
        ))}
        {battleResolutions.map((resolution, index) => (
          <article className="wire-rule-item" key={resolution.resolutionId ?? `battle-resolution-${index}`}>
            <div>
              <strong>{battleResolutionLabel(resolution.kind)}</strong>
              <span>tick {resolution.tick ?? "无"}</span>
            </div>
            <RuleLine label="战场" value={objectLabel(resolution.battlefieldId, objects)} />
            <RuleLine label="胜者" mine={resolution.winnerPlayerId === playerId} value={resolution.winnerPlayerId ?? "无"} />
            <RuleLine label="被摧毁" value={objectListLabel(resolution.destroyedObjectIds, objects)} />
            <RuleLine label="事件" value={eventListLabel(resolution.relatedEventKinds)} />
            <RuleObjectRefs objects={objects} onInspectObject={onInspectObject} refs={battleResolutionObjectRefs(resolution)} selectedObjectId={selectedObjectId} />
          </article>
        ))}
      </RuleSection>
    </section>
  );
}

function RuleMetric({ label, mine, value }: { label: string; mine?: boolean; value: string }) {
  return (
    <div className={mine ? "wire-rule-metric is-mine" : "wire-rule-metric"}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function RuleSection({
  children,
  emptyLabel,
  title
}: {
  children: ReactNode;
  emptyLabel: string;
  title: string;
}) {
  const childArray = Children.toArray(children).filter(Boolean);
  return (
    <section className="wire-rule-section">
      <h3>{title}</h3>
      {childArray.length === 0 ? <span className="empty-hint">{emptyLabel}</span> : childArray}
    </section>
  );
}

function RuleLine({ label, mine, value }: { label: string; mine?: boolean; value: string }) {
  return (
    <span className={mine ? "wire-rule-line is-mine" : "wire-rule-line"}>
      <span>{label}</span>
      <strong>{value || "无"}</strong>
    </span>
  );
}

function RuleObjectRefs({
  objects,
  onInspectObject,
  refs,
  selectedObjectId
}: {
  objects: ObjectIndex;
  onInspectObject?: (objectId: string) => void;
  refs: RuleObjectRef[];
  selectedObjectId?: string;
}) {
  const visibleRefs = uniqueRuleObjectRefs(refs);
  if (visibleRefs.length === 0) {
    return null;
  }

  return (
    <div className="wire-rule-object-refs" aria-label="关联桌面对象">
      {visibleRefs.map((ref) => {
        const object = objects[ref.id];
        const hidden = ref.id === "HIDDEN";
        const canInspect = Boolean(object && onInspectObject && !hidden);
        const label = `${ref.role} ${objectLabel(ref.id, objects)}`;
        if (!canInspect) {
          return (
            <span className="wire-rule-object-ref is-disabled" data-rule-object-ref={ref.id} data-rule-object-role={ref.role} key={`${ref.role}-${ref.id}`}>
              {label}
            </span>
          );
        }

        return (
          <button
            className={selectedObjectId === ref.id ? "wire-rule-object-ref is-selected" : "wire-rule-object-ref"}
            data-rule-object-ref={ref.id}
            data-rule-object-role={ref.role}
            data-selected={selectedObjectId === ref.id ? "true" : undefined}
            key={`${ref.role}-${ref.id}`}
            onClick={() => onInspectObject?.(ref.id)}
            type="button"
          >
            {label}
          </button>
        );
      })}
    </div>
  );
}

function stackItemFromRecord(record: Record<string, unknown>): StackItemView {
  const damageAmount = asNumber(record.damageAmount, -1);
  return {
    cardNo: nullableString(record.cardNo),
    controllerId: nullableString(record.controllerId) ?? undefined,
    damageAmount: damageAmount >= 0 ? damageAmount : undefined,
    destination: nullableString(record.destination) ?? undefined,
    effectKind: nullableString(record.effectKind) ?? undefined,
    sourceObjectId: nullableString(record.sourceObjectId) ?? undefined,
    stackItemId: nullableString(record.stackItemId) ?? undefined,
    targetObjectIds: stringArray(record.targetObjectIds)
  };
}

function taskFromRecord(record: Record<string, unknown>): PendingTaskView {
  return {
    actingPlayerId: nullableString(record.actingPlayerId),
    battleId: nullableString(record.battleId) ?? undefined,
    battlefieldObjectId: nullableString(record.battlefieldObjectId) ?? undefined,
    kind: nullableString(record.kind) ?? undefined,
    participantControllerIds: stringArray(record.participantControllerIds),
    participantObjectIds: stringArray(record.participantObjectIds),
    reason: nullableString(record.reason) ?? undefined,
    spellDuelId: nullableString(record.spellDuelId) ?? undefined,
    stackItemIds: stringArray(record.stackItemIds),
    status: nullableString(record.status) ?? undefined,
    taskId: nullableString(record.taskId) ?? undefined
  };
}

function triggerFromRecord(record: Record<string, unknown>): TriggerQueueItemView {
  return {
    controllerId: nullableString(record.controllerId) ?? undefined,
    effectKind: nullableString(record.effectKind) ?? undefined,
    sourceObjectId: nullableString(record.sourceObjectId) ?? undefined,
    sourceVisibility: nullableString(record.sourceVisibility) ?? undefined,
    triggeredByEventKind: nullableString(record.triggeredByEventKind) ?? undefined,
    triggerId: nullableString(record.triggerId) ?? undefined
  };
}

function battlefieldResolutionFromRecord(record: Record<string, unknown>): BattlefieldResolutionView {
  return {
    battlefieldObjectId: nullableString(record.battlefieldObjectId) ?? undefined,
    controllerId: nullableString(record.controllerId),
    kind: nullableString(record.kind) ?? undefined,
    participantObjectIds: stringArray(record.participantObjectIds),
    playerId: nullableString(record.playerId),
    previousControllerId: nullableString(record.previousControllerId),
    reason: nullableString(record.reason) ?? undefined,
    relatedEventKinds: stringArray(record.relatedEventKinds),
    resolutionId: nullableString(record.resolutionId) ?? undefined,
    sourceObjectId: nullableString(record.sourceObjectId),
    tick: asNumber(record.tick, 0)
  };
}

function battleResolutionFromRecord(record: Record<string, unknown>): BattleResolutionView {
  return {
    attackerObjectIds: stringArray(record.attackerObjectIds),
    attackingPlayerId: nullableString(record.attackingPlayerId),
    battlefieldId: nullableString(record.battlefieldId) ?? undefined,
    defenderObjectIds: stringArray(record.defenderObjectIds),
    defendingPlayerId: nullableString(record.defendingPlayerId),
    destroyedObjectIds: stringArray(record.destroyedObjectIds),
    kind: nullableString(record.kind) ?? undefined,
    reason: nullableString(record.reason) ?? undefined,
    relatedEventKinds: stringArray(record.relatedEventKinds),
    resolutionId: nullableString(record.resolutionId) ?? undefined,
    survivingAttackerObjectIds: stringArray(record.survivingAttackerObjectIds),
    survivingDefenderObjectIds: stringArray(record.survivingDefenderObjectIds),
    tick: asNumber(record.tick, 0),
    winnerPlayerId: nullableString(record.winnerPlayerId)
  };
}

function stackObjectRefs(item: StackItemView): RuleObjectRef[] {
  return [
    ref("来源", item.sourceObjectId),
    ...refs("目标", item.targetObjectIds)
  ];
}

function taskObjectRefs(task: PendingTaskView): RuleObjectRef[] {
  return [
    ref("战场", task.battlefieldObjectId),
    ...refs("参与", task.participantObjectIds)
  ];
}

function triggerObjectRefs(trigger: TriggerQueueItemView): RuleObjectRef[] {
  return trigger.sourceVisibility === "HIDDEN" ? [ref("来源", "HIDDEN")] : [ref("来源", trigger.sourceObjectId)];
}

function battlefieldResolutionObjectRefs(resolution: BattlefieldResolutionView): RuleObjectRef[] {
  return [
    ref("战场", resolution.battlefieldObjectId),
    ref("来源", resolution.sourceObjectId),
    ...refs("参与", resolution.participantObjectIds)
  ];
}

function battleResolutionObjectRefs(resolution: BattleResolutionView): RuleObjectRef[] {
  return [
    ref("战场", resolution.battlefieldId),
    ...refs("攻击", resolution.attackerObjectIds),
    ...refs("防守", resolution.defenderObjectIds),
    ...refs("被摧毁", resolution.destroyedObjectIds)
  ];
}

function ref(role: string, id: string | null | undefined): RuleObjectRef {
  return { id: id?.trim() ?? "", role };
}

function refs(role: string, ids: string[] | undefined): RuleObjectRef[] {
  return (ids ?? []).map((id) => ref(role, id));
}

function uniqueRuleObjectRefs(refs: RuleObjectRef[]): RuleObjectRef[] {
  const seen = new Set<string>();
  const unique: RuleObjectRef[] = [];
  for (const item of refs) {
    if (!item.id || seen.has(item.id)) {
      continue;
    }

    seen.add(item.id);
    unique.push(item);
  }

  return unique;
}

function objectLabel(objectId: string | null | undefined, objects: ObjectIndex): string {
  if (!objectId) {
    return "无";
  }

  if (objectId === "HIDDEN") {
    return "隐藏对象";
  }

  const object = objects[objectId];
  return object?.cardNo ? `${object.cardNo}` : idLabel(objectId);
}

function sourceLabel(sourceObjectId: string | null | undefined, cardNo: string | null | undefined, objects: ObjectIndex): string {
  if (cardNo) {
    return sourceObjectId ? `${cardNo} / ${objectLabel(sourceObjectId, objects)}` : cardNo;
  }

  return objectLabel(sourceObjectId, objects);
}

function objectListLabel(objectIds: string[] | undefined, objects: ObjectIndex): string {
  if (!objectIds || objectIds.length === 0) {
    return "无";
  }

  const visible = objectIds.slice(0, 3).map((objectId) => objectLabel(objectId, objects)).join("、");
  return objectIds.length > 3 ? `${visible} 等 ${objectIds.length} 项` : visible;
}

function eventListLabel(eventKinds: string[] | undefined): string {
  if (!eventKinds || eventKinds.length === 0) {
    return "无";
  }

  const visible = eventKinds.slice(0, 3).map(eventLabel).join("、");
  return eventKinds.length > 3 ? `${visible} 等 ${eventKinds.length} 项` : visible;
}

function stackEffectLabel(value: string | undefined): string {
  return labelFor(stackEffectLabels, value, "服务端效果", "效果");
}

function taskKindLabel(value: string | undefined): string {
  return labelFor(taskKindLabels, value, "服务端任务", "任务");
}

function taskReasonLabel(value: string | undefined): string {
  return labelFor(taskReasonLabels, value, "服务端规则", "服务端规则");
}

function taskPhaseLabel(value: string | undefined): string {
  return labelFor(taskPhaseLabels, value, "服务端阶段", "无");
}

function battlefieldResolutionLabel(value: string | undefined): string {
  return labelFor(battlefieldResolutionLabels, value, "战场结果", "战场结果");
}

function battleResolutionLabel(value: string | undefined): string {
  return labelFor(battleResolutionLabels, value, "战斗结果", "战斗结果");
}

function eventLabel(value: string | undefined): string {
  const labels: Record<string, string> = {
    BATTLEFIELD_CONQUERED: "战场被征服",
    BATTLEFIELD_CONTROL_RESOLVED: "战场控制结算",
    BATTLEFIELD_HELD: "战场被据守",
    BATTLE_CLOSED: "战斗关闭",
    DAMAGE_ASSIGNED: "伤害分配"
  };
  return labelFor(labels, value, "服务端事件", "无");
}

function zoneLabel(value: string): string {
  if (value.startsWith("BATTLEFIELD:")) {
    return "战场";
  }

  const labels: Record<string, string> = {
    BANISHED: "放逐区",
    BASE: "基地",
    GRAVEYARD: "废牌堆",
    STACK: "结算链",
    STANDBY: "待命"
  };
  return labelFor(labels, value, "服务端区域", "无");
}

function labelFor(map: Record<string, string>, value: string | undefined, protocolFallback: string, emptyFallback: string): string {
  const raw = value?.trim() ?? "";
  if (!raw) {
    return emptyFallback;
  }

  return map[raw] ?? protocolValue(raw, protocolFallback);
}

function protocolValue(value: string, fallback: string): string {
  if (isProtocolToken(value)) {
    return fallback;
  }

  return redactInternalText(value);
}

function nullableString(value: unknown): string | null {
  return typeof value === "string" && value.trim().length > 0 ? value : null;
}

function stringArray(value: unknown): string[] {
  return asArray<unknown>(value).filter((item): item is string => typeof item === "string" && item.trim().length > 0);
}

function idLabel(value: string): string {
  return isProtocolToken(value) ? "服务端对象" : redactInternalText(value);
}

function isProtocolToken(value: string): boolean {
  return /^[A-Z0-9_:-]+$/.test(value) || /^[a-z0-9]+(?:[-_:][a-z0-9]+)+$/.test(value);
}

import type {
  ActionPromptDto,
  BattleResolutionView,
  BattlefieldResolutionView,
  PendingTaskView,
  SnapshotDto,
  StackItemView,
  TriggerQueueItemView
} from "../../types/protocol";
import { Children, type ReactNode, useState } from "react";
import { asArray, asNumber, asRecord, asString } from "../../utils/collections";
import { redactInternalText } from "../../utils/redaction";
import { buildWireRuleQueuePlan, type WireRuleQueueInspectorPlan, type WireRuleQueueLane, type WireRuleQueueSequenceItem } from "../../utils/wireRuleQueuePlan";
import { buildCardObjectIndex } from "../../utils/snapshotObjectIndex";
import { StatusPill } from "../ui/StatusPill";
import { WireDetailTrigger } from "./WireDetailTrigger";
import {
  WireObjectRefChips,
  type WireObjectIndex,
  type WireObjectRef,
  wireObjectLabel,
  wireObjectRef,
  wireObjectRefs
} from "./WireObjectRefChips";
import type { WireTimelineDetail, WireTimelineDetailLine } from "./WireTimelineDetailPanel";

type WireRuleQueuePanelProps = {
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  onInspectObject?: (objectId: string) => void;
  playerId: string;
  prompt?: ActionPromptDto;
  selectedDetailId?: string;
  selectedObjectId?: string;
  snapshot?: SnapshotDto;
};

type ObjectIndex = WireObjectIndex;

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

export function WireRuleQueuePanel({ onInspectObject, onSelectDetail, playerId, prompt, selectedDetailId, selectedObjectId, snapshot }: WireRuleQueuePanelProps) {
  const [inspectorOpen, setInspectorOpen] = useState(false);
  const timing = asRecord(snapshot?.timing);
  const queue = asRecord(timing.pendingTaskQueue);
  const plan = buildWireRuleQueuePlan({ playerId, prompt, snapshot });
  const stack = asArray<Record<string, unknown>>(snapshot?.stack).map(stackItemFromRecord);
  const pendingTasks = asArray<Record<string, unknown>>(queue.tasks).map(taskFromRecord);
  const battlefieldTasks = asArray<Record<string, unknown>>(timing.battlefieldTasks).map(taskFromRecord);
  const triggerQueue = asArray<Record<string, unknown>>(timing.triggerQueue).map(triggerFromRecord);
  const battlefieldResolutions = asArray<Record<string, unknown>>(timing.battlefieldResolutions).map(battlefieldResolutionFromRecord);
  const battleResolutions = asArray<Record<string, unknown>>(timing.battleResolutions).map(battleResolutionFromRecord);
  const objects = buildCardObjectIndex(snapshot);
  const activeTaskId = asString(queue.activeTaskId, "");
  const isBlocking = Boolean(queue.isBlocking);

  return (
    <section className="wire-rule-queue" aria-label="服务端规则队列" data-wire-rule-queue-state={plan.state}>
      <header className="wire-rule-queue-header">
        <div>
          <strong>结算链 / 规则事件</strong>
          <span>tick {snapshot?.tick ?? "无"} / prompt {prompt?.promptId ? "已提供" : "无"}</span>
        </div>
        <StatusPill tone={isBlocking ? "warn" : stack.length > 0 ? "info" : "neutral"}>
          {isBlocking ? "规则阻塞" : stack.length > 0 ? "等待响应" : "空闲"}
        </StatusPill>
      </header>

      <section className="wire-rule-flow" aria-label="服务端规则队列地图">
        <div className="wire-rule-flow-heading">
          <strong>规则队列地图</strong>
          <span>{plan.stateLabel}</span>
        </div>
        <ol className="wire-rule-lanes">
          {plan.lanes.map((lane) => (
            <RuleLaneCard key={lane.key} lane={lane} />
          ))}
        </ol>
        <div className="wire-rule-flow-next" data-wire-rule-next-lane={plan.activeLaneKey}>
          下一步：{plan.nextStepLabel}
        </div>
        {plan.sequence.length > 0 && (
          <ol className="wire-rule-sequence" aria-label="服务端规则队列顺序">
            {plan.sequence.map((item) => (
              <RuleSequenceItem item={item} key={item.key} />
            ))}
          </ol>
        )}
        <button
          aria-expanded={inspectorOpen}
          className="wire-rule-inspector-toggle"
          data-rule-inspector-toggle="true"
          onClick={() => setInspectorOpen((open) => !open)}
          type="button"
        >
          {inspectorOpen ? "收起规则检查" : "展开规则检查"}
        </button>
        <RuleQueueInspector open={inspectorOpen} plan={plan.inspector} />
      </section>

      <div className="wire-rule-state-grid">
        {plan.metrics.map((metric) => (
          <RuleMetric key={metric.key} label={metric.label} mine={metric.mine} value={metric.value} />
        ))}
      </div>

      <RuleSection title="结算链" emptyLabel="当前无结算链项目。">
        {stack.map((item, index) => (
          <article className={isSelectedRuleDetail(selectedDetailId, "stack", item.stackItemId ?? String(index)) ? "wire-rule-item is-detail-selected" : "wire-rule-item"} key={item.stackItemId ?? `stack-${index}`}>
            <div>
              <strong>项目 {stack.length - index}</strong>
              <span>{stackEffectLabel(item.effectKind)}</span>
              <RuleDetailButton detail={stackDetail(item, index, stack.length, playerId, objects)} onSelectDetail={onSelectDetail} selectedDetailId={selectedDetailId} />
            </div>
            <RuleLine label="控制者" mine={item.controllerId === playerId} value={item.controllerId ?? "未知"} />
            <RuleLine label="来源" value={sourceLabel(item.sourceObjectId, item.cardNo, objects)} />
            <RuleLine label="目标" value={objectListLabel(item.targetObjectIds, objects)} />
            {item.damageAmount != null && item.damageAmount > 0 && <RuleLine label="伤害" value={String(item.damageAmount)} />}
            {item.destination && <RuleLine label="去向" value={zoneLabel(item.destination)} />}
            <WireObjectRefChips objects={objects} onInspectObject={onInspectObject} refs={stackObjectRefs(item)} selectedObjectId={selectedObjectId} source="rule" />
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
          <article className={isSelectedRuleDetail(selectedDetailId, "task", task.taskId ?? String(index)) ? "wire-rule-item is-detail-selected" : "wire-rule-item"} key={task.taskId ?? `task-${index}`}>
            <div>
              <strong>{taskKindLabel(task.kind)}</strong>
              <span>{task.status ? protocolValue(task.status, "服务端状态") : "状态未提供"}</span>
              <RuleDetailButton detail={taskDetail(task, index, playerId, objects)} onSelectDetail={onSelectDetail} selectedDetailId={selectedDetailId} />
            </div>
            <RuleLine label="原因" value={taskReasonLabel(task.reason)} />
            <RuleLine label="战场" value={task.battlefieldObjectId ? objectLabel(task.battlefieldObjectId, objects) : "无"} />
            <RuleLine label="行动玩家" mine={task.actingPlayerId === playerId} value={task.actingPlayerId ?? "无"} />
            <RuleLine label="参与对象" value={objectListLabel(task.participantObjectIds, objects)} />
            {task.stackItemIds && task.stackItemIds.length > 0 && <RuleLine label="关联结算链" value={`${task.stackItemIds.length} 项`} />}
            {task.spellDuelId && <RuleLine label="法术对决" value="服务端已创建" />}
            {task.battleId && <RuleLine label="战斗" value="服务端已创建" />}
            <WireObjectRefChips objects={objects} onInspectObject={onInspectObject} refs={taskObjectRefs(task)} selectedObjectId={selectedObjectId} source="rule" />
          </article>
        ))}
      </RuleSection>

      <RuleSection title="触发队列" emptyLabel="当前无待排序或待结算触发。">
        {triggerQueue.map((trigger, index) => (
          <article className={isSelectedRuleDetail(selectedDetailId, "trigger", trigger.triggerId ?? String(index)) ? "wire-rule-item is-detail-selected" : "wire-rule-item"} key={trigger.triggerId ?? `trigger-${index}`}>
            <div>
              <strong>触发 {index + 1}</strong>
              <span>{stackEffectLabel(trigger.effectKind)}</span>
              <RuleDetailButton detail={triggerDetail(trigger, index, playerId, objects)} onSelectDetail={onSelectDetail} selectedDetailId={selectedDetailId} />
            </div>
            <RuleLine label="控制者" mine={trigger.controllerId === playerId} value={trigger.controllerId ?? "无"} />
            <RuleLine label="来源" value={trigger.sourceVisibility === "HIDDEN" ? "隐藏来源" : objectLabel(trigger.sourceObjectId, objects)} />
            <RuleLine label="来源事件" value={eventLabel(trigger.triggeredByEventKind)} />
            <WireObjectRefChips objects={objects} onInspectObject={onInspectObject} refs={triggerObjectRefs(trigger)} selectedObjectId={selectedObjectId} source="rule" />
          </article>
        ))}
      </RuleSection>

      <RuleSection title="近期规则事件" emptyLabel="暂无近期战场或战斗结算。">
        {battlefieldResolutions.map((resolution, index) => (
          <article className={isSelectedRuleDetail(selectedDetailId, "battlefield-resolution", resolution.resolutionId ?? String(index)) ? "wire-rule-item is-detail-selected" : "wire-rule-item"} key={resolution.resolutionId ?? `battlefield-resolution-${index}`}>
            <div>
              <strong>{battlefieldResolutionLabel(resolution.kind)}</strong>
              <span>tick {resolution.tick ?? "无"}</span>
              <RuleDetailButton detail={battlefieldResolutionDetail(resolution, index, playerId, objects)} onSelectDetail={onSelectDetail} selectedDetailId={selectedDetailId} />
            </div>
            <RuleLine label="战场" value={objectLabel(resolution.battlefieldObjectId, objects)} />
            <RuleLine label="控制者" mine={(resolution.playerId ?? resolution.controllerId) === playerId} value={resolution.playerId ?? resolution.controllerId ?? "无"} />
            <RuleLine label="参与对象" value={objectListLabel(resolution.participantObjectIds, objects)} />
            <RuleLine label="事件" value={eventListLabel(resolution.relatedEventKinds)} />
            <WireObjectRefChips objects={objects} onInspectObject={onInspectObject} refs={battlefieldResolutionObjectRefs(resolution)} selectedObjectId={selectedObjectId} source="rule" />
          </article>
        ))}
        {battleResolutions.map((resolution, index) => (
          <article className={isSelectedRuleDetail(selectedDetailId, "battle-resolution", resolution.resolutionId ?? String(index)) ? "wire-rule-item is-detail-selected" : "wire-rule-item"} key={resolution.resolutionId ?? `battle-resolution-${index}`}>
            <div>
              <strong>{battleResolutionLabel(resolution.kind)}</strong>
              <span>tick {resolution.tick ?? "无"}</span>
              <RuleDetailButton detail={battleResolutionDetail(resolution, index, playerId, objects)} onSelectDetail={onSelectDetail} selectedDetailId={selectedDetailId} />
            </div>
            <RuleLine label="战场" value={objectLabel(resolution.battlefieldId, objects)} />
            <RuleLine label="胜者" mine={resolution.winnerPlayerId === playerId} value={resolution.winnerPlayerId ?? "无"} />
            <RuleLine label="被摧毁" value={objectListLabel(resolution.destroyedObjectIds, objects)} />
            <RuleLine label="事件" value={eventListLabel(resolution.relatedEventKinds)} />
            <WireObjectRefChips objects={objects} onInspectObject={onInspectObject} refs={battleResolutionObjectRefs(resolution)} selectedObjectId={selectedObjectId} source="rule" />
          </article>
        ))}
      </RuleSection>
    </section>
  );
}

function RuleQueueInspector({ open, plan }: { open: boolean; plan: WireRuleQueueInspectorPlan }) {
  return (
    <aside
      aria-label="规则队列检查器"
      className="wire-rule-inspector"
      data-rule-inspector-state={open ? "open" : "closed"}
      hidden={!open}
    >
      <header>
        <strong>规则检查</strong>
        <span>{plan.summary}</span>
      </header>
      <section>
        <strong>通道</strong>
        <ol className="wire-rule-inspector-lanes">
          {plan.lanes.map((lane) => (
            <li data-rule-inspector-lane={lane.key} data-rule-inspector-lane-state={lane.state} key={lane.key}>
              <span>{lane.label}</span>
              <strong>{lane.count} 项 / {lane.stateLabel}</strong>
              <small>{lane.headline}</small>
              <small>{lane.hint}</small>
            </li>
          ))}
        </ol>
      </section>
      <section>
        <strong>顺序</strong>
        {plan.sequence.length > 0 ? (
          <ol className="wire-rule-inspector-sequence">
            {plan.sequence.map((item) => (
              <li data-rule-inspector-sequence-lane={item.laneLabel} key={item.key}>
                <span>{item.label}</span>
                <strong>{item.laneLabel} / {item.detailLabel}</strong>
                <small>{item.stateLabel} / {item.tickLabel ?? `${item.objectCount} 对象`}</small>
              </li>
            ))}
          </ol>
        ) : (
          <span className="empty-hint">当前无服务端队列顺序。</span>
        )}
      </section>
      <footer>
        <span>状态 {plan.stateLabel}</span>
        <span>活动 {plan.activeLaneLabel}</span>
        <span>下一步 {plan.nextStepLabel}</span>
      </footer>
    </aside>
  );
}

function RuleLaneCard({ lane }: { lane: WireRuleQueueLane }) {
  return (
    <li data-rule-lane={lane.key} data-rule-lane-state={lane.state}>
      <small>{lane.label}</small>
      <strong>{lane.count} 项</strong>
      <span>{lane.headline}</span>
      <em>{lane.hint}</em>
    </li>
  );
}

function RuleSequenceItem({ item }: { item: WireRuleQueueSequenceItem }) {
  return (
    <li data-rule-sequence-lane={item.lane}>
      <small>{item.label}</small>
      <strong>{item.detailLabel}</strong>
      <span>{item.stateLabel}</span>
      <em>{item.tickLabel ?? `${item.objectCount} 对象`}</em>
    </li>
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

function RuleDetailButton({
  detail,
  onSelectDetail,
  selectedDetailId
}: {
  detail: WireTimelineDetail;
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  selectedDetailId?: string;
}) {
  return <WireDetailTrigger detail={detail} onSelectDetail={onSelectDetail} selectedDetailId={selectedDetailId} />;
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

function stackDetail(
  item: StackItemView,
  index: number,
  stackLength: number,
  playerId: string,
  objects: ObjectIndex
): WireTimelineDetail {
  const idSeed = item.stackItemId ?? String(index);
  return {
    id: ruleDetailId("stack", idSeed),
    lines: compactDetailLines([
      line("控制者", item.controllerId ?? "未知", item.controllerId === playerId),
      line("来源", sourceLabel(item.sourceObjectId, item.cardNo, objects)),
      line("目标", objectListLabel(item.targetObjectIds, objects)),
      item.damageAmount != null && item.damageAmount > 0 ? line("伤害", String(item.damageAmount)) : undefined,
      item.destination ? line("去向", zoneLabel(item.destination)) : undefined,
      item.stackItemId ? line("服务端编号", idLabel(item.stackItemId)) : undefined
    ]),
    refs: stackObjectRefs(item),
    source: "rule",
    subtitle: `${stackEffectLabel(item.effectKind)} / 项目 ${stackLength - index}`,
    title: "结算链项目"
  };
}

function taskDetail(task: PendingTaskView, index: number, playerId: string, objects: ObjectIndex): WireTimelineDetail {
  const idSeed = task.taskId ?? String(index);
  return {
    id: ruleDetailId("task", idSeed),
    lines: compactDetailLines([
      line("状态", task.status ? protocolValue(task.status, "服务端状态") : "状态未提供"),
      line("原因", taskReasonLabel(task.reason)),
      line("战场", task.battlefieldObjectId ? objectLabel(task.battlefieldObjectId, objects) : "无"),
      line("行动玩家", task.actingPlayerId ?? "无", task.actingPlayerId === playerId),
      line("参与对象", objectListLabel(task.participantObjectIds, objects)),
      task.stackItemIds && task.stackItemIds.length > 0 ? line("关联结算链", `${task.stackItemIds.length} 项`) : undefined,
      task.spellDuelId ? line("法术对决", "服务端已创建") : undefined,
      task.battleId ? line("战斗", "服务端已创建") : undefined
    ]),
    refs: taskObjectRefs(task),
    source: "rule",
    subtitle: task.status ? protocolValue(task.status, "服务端状态") : "状态未提供",
    title: taskKindLabel(task.kind)
  };
}

function triggerDetail(trigger: TriggerQueueItemView, index: number, playerId: string, objects: ObjectIndex): WireTimelineDetail {
  const idSeed = trigger.triggerId ?? String(index);
  return {
    id: ruleDetailId("trigger", idSeed),
    lines: compactDetailLines([
      line("控制者", trigger.controllerId ?? "无", trigger.controllerId === playerId),
      line("来源", trigger.sourceVisibility === "HIDDEN" ? "隐藏来源" : objectLabel(trigger.sourceObjectId, objects)),
      line("来源事件", eventLabel(trigger.triggeredByEventKind)),
      trigger.triggerId ? line("服务端编号", idLabel(trigger.triggerId)) : undefined
    ]),
    refs: triggerObjectRefs(trigger),
    source: "rule",
    subtitle: stackEffectLabel(trigger.effectKind),
    title: `触发 ${index + 1}`
  };
}

function battlefieldResolutionDetail(
  resolution: BattlefieldResolutionView,
  index: number,
  playerId: string,
  objects: ObjectIndex
): WireTimelineDetail {
  const idSeed = resolution.resolutionId ?? String(index);
  return {
    id: ruleDetailId("battlefield-resolution", idSeed),
    lines: compactDetailLines([
      line("战场", objectLabel(resolution.battlefieldObjectId, objects)),
      line("控制者", resolution.playerId ?? resolution.controllerId ?? "无", (resolution.playerId ?? resolution.controllerId) === playerId),
      line("之前控制者", resolution.previousControllerId ?? "无", resolution.previousControllerId === playerId),
      line("参与对象", objectListLabel(resolution.participantObjectIds, objects)),
      line("事件", eventListLabel(resolution.relatedEventKinds)),
      line("tick", String(resolution.tick ?? "无"))
    ]),
    refs: battlefieldResolutionObjectRefs(resolution),
    source: "rule",
    subtitle: `tick ${resolution.tick ?? "无"}`,
    title: battlefieldResolutionLabel(resolution.kind)
  };
}

function battleResolutionDetail(
  resolution: BattleResolutionView,
  index: number,
  playerId: string,
  objects: ObjectIndex
): WireTimelineDetail {
  const idSeed = resolution.resolutionId ?? String(index);
  return {
    id: ruleDetailId("battle-resolution", idSeed),
    lines: compactDetailLines([
      line("战场", objectLabel(resolution.battlefieldId, objects)),
      line("攻击方", resolution.attackingPlayerId ?? "无", resolution.attackingPlayerId === playerId),
      line("防守方", resolution.defendingPlayerId ?? "无", resolution.defendingPlayerId === playerId),
      line("胜者", resolution.winnerPlayerId ?? "无", resolution.winnerPlayerId === playerId),
      line("被摧毁", objectListLabel(resolution.destroyedObjectIds, objects)),
      line("事件", eventListLabel(resolution.relatedEventKinds)),
      line("tick", String(resolution.tick ?? "无"))
    ]),
    refs: battleResolutionObjectRefs(resolution),
    source: "rule",
    subtitle: `tick ${resolution.tick ?? "无"}`,
    title: battleResolutionLabel(resolution.kind)
  };
}

function ruleDetailId(kind: string, id: string): string {
  return `rule:${kind}:${id}`;
}

function isSelectedRuleDetail(selectedDetailId: string | undefined, kind: string, id: string): boolean {
  return selectedDetailId === ruleDetailId(kind, id);
}

function line(label: string, value: string, mine?: boolean): WireTimelineDetailLine {
  return { label, mine, value };
}

function compactDetailLines(lines: Array<WireTimelineDetailLine | undefined>): WireTimelineDetailLine[] {
  return lines.filter((item): item is WireTimelineDetailLine => Boolean(item));
}

function stackObjectRefs(item: StackItemView): WireObjectRef[] {
  return [
    wireObjectRef("来源", item.sourceObjectId),
    ...wireObjectRefs("目标", item.targetObjectIds)
  ];
}

function taskObjectRefs(task: PendingTaskView): WireObjectRef[] {
  return [
    wireObjectRef("战场", task.battlefieldObjectId),
    ...wireObjectRefs("参与", task.participantObjectIds)
  ];
}

function triggerObjectRefs(trigger: TriggerQueueItemView): WireObjectRef[] {
  return trigger.sourceVisibility === "HIDDEN" ? [wireObjectRef("来源", "HIDDEN")] : [wireObjectRef("来源", trigger.sourceObjectId)];
}

function battlefieldResolutionObjectRefs(resolution: BattlefieldResolutionView): WireObjectRef[] {
  return [
    wireObjectRef("战场", resolution.battlefieldObjectId),
    wireObjectRef("来源", resolution.sourceObjectId),
    ...wireObjectRefs("参与", resolution.participantObjectIds)
  ];
}

function battleResolutionObjectRefs(resolution: BattleResolutionView): WireObjectRef[] {
  return [
    wireObjectRef("战场", resolution.battlefieldId),
    ...wireObjectRefs("攻击", resolution.attackerObjectIds),
    ...wireObjectRefs("防守", resolution.defenderObjectIds),
    ...wireObjectRefs("被摧毁", resolution.destroyedObjectIds)
  ];
}

function objectLabel(objectId: string | null | undefined, objects: ObjectIndex): string {
  return wireObjectLabel(objectId, objects);
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

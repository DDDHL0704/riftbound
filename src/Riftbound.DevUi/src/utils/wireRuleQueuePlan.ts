import type { ActionPromptDto, GameEvent, SnapshotDto } from "../types/protocol";
import { asArray, asNumber, asRecord, asString } from "./collections";
import { eventDescriptionLabel, eventKindLabel } from "./eventLogPlan";
import { matchPhaseLabel, timingStateLabel } from "./formatters";
import { gameEventObjectRefPlan, gameEventObjectRefSourceLabel } from "./gameEventObjectRefs";
import { promptCandidateCounts } from "./promptCandidateCounts";
import {
  buildPromptInteractionModel,
  promptChoiceRoleLabel,
  promptChoiceSummaryObjectIds,
  type PromptCandidateSummary,
  type PromptInteractionModel
} from "./promptInteraction";
import { redactInternalText } from "./redaction";
import { buildCardObjectIndex, type SnapshotObjectIndex } from "./snapshotObjectIndex";

export type WireRuleQueueLaneKey = "resolution" | "stack" | "task" | "trigger";

export type WireRuleQueueState =
  | "idle"
  | "resolution-history"
  | "stack-response"
  | "task-blocked"
  | "task-open"
  | "trigger-pending";

export type WireRuleQueueLaneState = "active" | "blocked" | "empty" | "waiting";

export type WireRuleQueueMetric = {
  key: string;
  label: string;
  mine?: boolean;
  value: string;
};

export type WireRuleQueueLane = {
  count: number;
  headline: string;
  hint: string;
  key: WireRuleQueueLaneKey;
  label: string;
  state: WireRuleQueueLaneState;
};

export type WireRuleQueueSequenceItem = {
  detailLabel: string;
  key: string;
  lane: WireRuleQueueLaneKey;
  label: string;
  objectCount: number;
  refs: WireRuleQueueObjectRef[];
  stateLabel: string;
  tickLabel?: string;
};

export type WireRuleQueueResponsibilityState =
  | "blocked"
  | "history"
  | "respond"
  | "waiting"
  | "watch";

export type WireRuleQueueResponsibilitySubmitState =
  | "history"
  | "no-candidates"
  | "ready"
  | "readonly"
  | "waiting-lane"
  | "waiting-prompt"
  | "wrong-player";

export type WireRuleQueueResponsibilitySubmitSemanticRow = {
  category: string;
  count: number;
  enabledCount: number;
  intent: string;
  key: string;
  priority: number;
  uiHint: string;
};

export type WireRuleQueueResponsibilitySubmitPlan = {
  canSubmit: boolean;
  candidateCount: number;
  enabledCandidateCount: number;
  promptType: string;
  reason: string;
  semanticRows: WireRuleQueueResponsibilitySubmitSemanticRow[];
  semanticSummary: string;
  state: WireRuleQueueResponsibilitySubmitState;
  stateLabel: string;
};

export type WireRuleQueueResponsibilityItem = {
  actorLabel: string;
  actionLabel: string;
  detailLabel: string;
  key: string;
  label: string;
  lane: WireRuleQueueLaneKey;
  objectCount: number;
  reason: string;
  refs: WireRuleQueueObjectRef[];
  state: WireRuleQueueResponsibilityState;
  stateLabel: string;
  submit: WireRuleQueueResponsibilitySubmitPlan;
};

export type WireRuleQueueResponsibilityPlan = {
  activeCount: number;
  items: WireRuleQueueResponsibilityItem[];
  stateLabel: string;
  submitReadyCount: number;
  summary: string;
};

export type WireRuleQueueInspectorLane = {
  count: number;
  headline: string;
  hint: string;
  key: WireRuleQueueLaneKey;
  label: string;
  state: WireRuleQueueLaneState;
  stateLabel: string;
};

export type WireRuleQueueInspectorSequence = {
  detailLabel: string;
  key: string;
  laneLabel: string;
  label: string;
  objectCount: number;
  refs: WireRuleQueueObjectRef[];
  stateLabel: string;
  tickLabel?: string;
};

export type WireRuleQueueCoverageKey = "battle" | "payment" | "stack" | "trigger" | "window";

export type WireRuleQueueCoverageState = "empty" | "history" | "live" | "mixed";

export type WireRuleQueueCoverageRow = {
  eventCount: number;
  hint: string;
  key: WireRuleQueueCoverageKey;
  label: string;
  liveCount: number;
  objectRefCount: number;
  state: WireRuleQueueCoverageState;
  stateLabel: string;
};

type ServerRuleQueueCoverageRow = {
  evidenceKeys: string[];
  liveCount: number;
};

type ServerRuleQueueCoverageMap = Partial<Record<WireRuleQueueCoverageKey, ServerRuleQueueCoverageRow>>;

export type WireRuleQueueInspectorPlan = {
  activeLaneKey: WireRuleQueueLaneKey | "none";
  activeLaneLabel: string;
  coverage: WireRuleQueueCoverageRow[];
  lanes: WireRuleQueueInspectorLane[];
  nextStepLabel: string;
  sequence: WireRuleQueueInspectorSequence[];
  state: WireRuleQueueState;
  stateLabel: string;
  summary: string;
};

export type WireRuleQueueStatusTone = "bad" | "good" | "info" | "neutral" | "warn";

export type WireRuleQueueHeaderPlan = {
  statusLabel: string;
  statusTone: WireRuleQueueStatusTone;
  subtitle: string;
  title: string;
};

export type WireRuleQueueDetailLine = {
  label: string;
  mine?: boolean;
  value: string;
};

export type WireRuleQueueObjectRef = {
  id: string;
  label?: string;
  role: string;
  visibility?: "hidden" | "missing" | "visible";
};

export type WireRuleQueueDetailPlan = {
  id: string;
  lines: WireRuleQueueDetailLine[];
  refs: WireRuleQueueObjectRef[];
  source: "rule";
  subtitle?: string;
  title: string;
};

export type WireRuleQueueItemPlan = {
  detail: WireRuleQueueDetailPlan;
  key: string;
  lines: WireRuleQueueDetailLine[];
  refs: WireRuleQueueObjectRef[];
  subtitle: string;
  title: string;
};

export type WireRuleQueueSectionPlan = {
  emptyLabel: string;
  items: WireRuleQueueItemPlan[];
  key: WireRuleQueueLaneKey;
  notes: string[];
  title: string;
};

export type WireRuleQueueFocusPlan = {
  actionRows: WireRuleQueueFocusActionRow[];
  detail?: WireRuleQueueDetailPlan;
  emptyLabel: string;
  laneKey: WireRuleQueueLaneKey | "none";
  laneLabel: string;
  reasonLabel: string;
};

export type WireRuleQueueFocusActionState = "blocked" | "ready" | "referenced";

export type WireRuleQueueFocusActionRow = {
  actionRoleLabels: string[];
  candidateCount: number;
  disabledCandidateCount: number;
  enabledCandidateCount: number;
  key: string;
  nextStepLabel: string;
  objectId: string;
  semanticSummary: string;
  serverRoleLabel: string;
  state: WireRuleQueueFocusActionState;
  stateLabel: string;
};

export type WireRuleQueuePlan = {
  activeLaneKey: WireRuleQueueLaneKey | "none";
  coverage: WireRuleQueueCoverageRow[];
  focus: WireRuleQueueFocusPlan;
  header: WireRuleQueueHeaderPlan;
  inspector: WireRuleQueueInspectorPlan;
  lanes: WireRuleQueueLane[];
  metrics: WireRuleQueueMetric[];
  nextStepLabel: string;
  responsibility: WireRuleQueueResponsibilityPlan;
  sections: WireRuleQueueSectionPlan[];
  sequence: WireRuleQueueSequenceItem[];
  state: WireRuleQueueState;
  stateLabel: string;
};

type BuildWireRuleQueuePlanInput = {
  events?: GameEvent[];
  playerId: string;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
};

type RuleQueueCounts = {
  battlefieldResolutionCount: number;
  battleResolutionCount: number;
  pendingTaskCount: number;
  ruleEventCount: number;
  stackCount: number;
  taskCount: number;
  triggerCount: number;
};

export function buildWireRuleQueuePlan({
  events = [],
  playerId,
  prompt,
  snapshot
}: BuildWireRuleQueuePlanInput): WireRuleQueuePlan {
  const timing = asRecord(snapshot?.timing);
  const queue = asRecord(timing.pendingTaskQueue);
  const turnWindow = asRecord(timing.turnWindow);
  const stack = asArray<Record<string, unknown>>(snapshot?.stack);
  const pendingTasks = asArray<Record<string, unknown>>(queue.tasks);
  const battlefieldTasks = asArray<Record<string, unknown>>(timing.battlefieldTasks);
  const tasks = [...pendingTasks, ...battlefieldTasks];
  const triggers = asArray<Record<string, unknown>>(timing.triggerQueue);
  const battlefieldResolutions = asArray<Record<string, unknown>>(timing.battlefieldResolutions);
  const battleResolutions = asArray<Record<string, unknown>>(timing.battleResolutions);
  const ruleEvents = ruleQueueEvents(events);
  const coverage = ruleCoverageRows({ battleResolutions, battlefieldResolutions, prompt, ruleEvents, stack, tasks, timing, triggers });
  const counts: RuleQueueCounts = {
    battlefieldResolutionCount: battlefieldResolutions.length,
    battleResolutionCount: battleResolutions.length,
    pendingTaskCount: pendingTasks.length,
    ruleEventCount: ruleEvents.length,
    stackCount: stack.length,
    taskCount: tasks.length,
    triggerCount: triggers.length
  };
  const isBlocking = Boolean(queue.isBlocking);
  const state = queueState({ counts, isBlocking });
  const activeLaneKey = activeLaneForState(state);
  const phase = asString(timing.phase, snapshot?.turnState ?? "");
  const windowState = asString(asRecord(timing.turnWindow).state, asString(timing.timingState, ""));
  const actingPlayerId = asString(turnWindow.actingPlayerId, asString(timing.priorityPlayerId, ""));
  const promptOwner = prompt?.playerId ?? asString(timing.promptPlayerId, "");
  const resolutionCount = counts.battlefieldResolutionCount + counts.battleResolutionCount + counts.ruleEventCount;

  const lanes = [
    lane({
      count: stack.length,
      headline: stack.length > 0 ? topStackHeadline(stack[0], stack.length) : "空",
      hint: stack.length > 0 ? "等待响应或继续按 LIFO 结算" : "当前无待响应结算项目",
      key: "stack",
      label: "结算链",
      state: laneState(activeLaneKey, "stack", stack.length, false)
    }),
    lane({
      count: tasks.length,
      headline: tasks.length > 0 ? topTaskHeadline(tasks[0], isBlocking) : "空",
      hint: tasks.length > 0 ? taskQueueHint(queue, isBlocking) : "当前无服务端规则任务",
      key: "task",
      label: "规则任务",
      state: laneState(activeLaneKey, "task", tasks.length, isBlocking)
    }),
    lane({
      count: triggers.length,
      headline: triggers.length > 0 ? topTriggerHeadline(triggers[0]) : "空",
      hint: triggers.length > 0 ? "等待触发排序或触发结算" : "当前无待排序触发",
      key: "trigger",
      label: "触发队列",
      state: laneState(activeLaneKey, "trigger", triggers.length, false)
    }),
    lane({
      count: resolutionCount,
      headline: resolutionHeadline(battlefieldResolutions, battleResolutions, ruleEvents),
      hint: resolutionCount > 0 ? "可选择近期规则事件查看桌面投影" : "当前无近期战场、战斗结算或服务端规则事件",
      key: "resolution",
      label: "近期事件",
      state: laneState(activeLaneKey, "resolution", resolutionCount, false)
    })
  ];
  const metrics = [
    { key: "phase", label: "阶段", value: matchPhaseLabel(phase) },
    { key: "window", label: "窗口", value: timingStateLabel(windowState) },
    { key: "acting-player", label: "行动权", mine: actingPlayerId === playerId, value: actingPlayerId || "无" },
    { key: "prompt-owner", label: "提示归属", mine: promptOwner === playerId, value: promptOwner || "无" },
    { key: "stack", label: "结算链", value: `${counts.stackCount} 项` },
    { key: "task", label: "任务", value: `${counts.taskCount} 项` },
    { key: "trigger", label: "触发", value: `${counts.triggerCount} 项` },
    { key: "resolution", label: "近期事件", value: `${resolutionCount} 项` },
    { key: "coverage", label: "事件覆盖", value: `${coverage.filter((row) => row.state !== "empty").length} 类` }
  ];
  const sequence = queueSequence({ battleResolutions, battlefieldResolutions, ruleEvents, stack, tasks, triggers });
  const nextStep = nextStepLabel(state);
  const objectIndex = buildCardObjectIndex(snapshot);
  const interactionModel = buildPromptInteractionModel(prompt);
  const sections = ruleQueueSections({
    battleResolutions,
    battlefieldResolutions,
    objects: objectIndex,
    playerId,
    queue,
    ruleEvents,
    stack,
    tasks,
    triggers
  });
  const focus = focusPlanFor({ activeLaneKey, interactionModel, sections, state });
  const responsibility = responsibilityPlanFor({ activeLaneKey, playerId, prompt, sequence, state });

  return {
    activeLaneKey,
    coverage,
    focus,
    header: headerPlanFor({
      promptId: prompt?.promptId,
      snapshotTick: snapshot?.tick,
      state
    }),
    inspector: inspectorPlan({ activeLaneKey, coverage, lanes, nextStepLabel: nextStep, sequence, state }),
    lanes,
    metrics,
    nextStepLabel: nextStep,
    responsibility,
    sections,
    sequence,
    state,
    stateLabel: queueStateLabel(state)
  };
}

function responsibilityPlanFor({
  activeLaneKey,
  playerId,
  prompt,
  sequence,
  state
}: {
  activeLaneKey: WireRuleQueueLaneKey | "none";
  playerId: string;
  prompt?: ActionPromptDto;
  sequence: WireRuleQueueSequenceItem[];
  state: WireRuleQueueState;
}): WireRuleQueueResponsibilityPlan {
  const laneCounters = new Map<WireRuleQueueLaneKey, number>();
  const items = sequence.map((item) => {
    const laneIndex = laneCounters.get(item.lane) ?? 0;
    laneCounters.set(item.lane, laneIndex + 1);
    return responsibilityItemFor({
      activeLaneKey,
      item,
      laneIndex,
      playerId,
      prompt,
      state
    });
  });
  const activeCount = items.filter((item) => item.state === "respond" || item.state === "blocked" || item.state === "watch").length;
  const submitReadyCount = items.filter((item) => item.submit.canSubmit).length;
  const stateLabel = responsibilityStateLabel(state);

  return {
    activeCount,
    items,
    stateLabel,
    submitReadyCount,
    summary: responsibilitySummaryFor(state, activeCount, items.length, submitReadyCount)
  };
}

function responsibilityItemFor({
  activeLaneKey,
  item,
  laneIndex,
  playerId,
  prompt,
  state
}: {
  activeLaneKey: WireRuleQueueLaneKey | "none";
  item: WireRuleQueueSequenceItem;
  laneIndex: number;
  playerId: string;
  prompt?: ActionPromptDto;
  state: WireRuleQueueState;
}): WireRuleQueueResponsibilityItem {
  const activeForLane = activeLaneKey === item.lane && laneIndex === 0;
  const responsibilityState = responsibilityItemState(item.lane, state, activeForLane);
  const submit = responsibilitySubmitPlanFor({ item, playerId, prompt, state: responsibilityState });

  return {
    actorLabel: responsibilityActorLabel(item),
    actionLabel: responsibilityActionLabel(item.lane, responsibilityState),
    detailLabel: item.detailLabel,
    key: `responsibility:${item.key}`,
    label: item.label,
    lane: item.lane,
    objectCount: item.objectCount,
    reason: responsibilityReason(item, responsibilityState),
    refs: item.refs,
    state: responsibilityState,
    stateLabel: responsibilityItemStateLabel(responsibilityState),
    submit
  };
}

function responsibilitySubmitPlanFor({
  item,
  playerId,
  prompt,
  state
}: {
  item: WireRuleQueueSequenceItem;
  playerId: string;
  prompt?: ActionPromptDto;
  state: WireRuleQueueResponsibilityState;
}): WireRuleQueueResponsibilitySubmitPlan {
  const counts = promptCandidateCounts(prompt);
  const candidateCount = counts.candidateCount;
  const enabledCandidateCount = counts.enabledCandidateCount;
  const promptType = prompt?.view?.type ?? prompt?.serverFlow?.promptType ?? "无";
  const semanticRows = responsibilitySubmitSemanticRows(prompt);

  if (state === "history" || item.lane === "resolution") {
    return responsibilitySubmitPlan({
      candidateCount,
      enabledCandidateCount,
      promptType,
      reason: "历史规则事件只用于回看，不提供提交入口。",
      semanticRows,
      state: "history"
    });
  }

  if (state === "waiting") {
    return responsibilitySubmitPlan({
      candidateCount,
      enabledCandidateCount,
      promptType,
      reason: "等待前序结算、任务或触发先处理。",
      semanticRows,
      state: "waiting-lane"
    });
  }

  if (!prompt) {
    return responsibilitySubmitPlan({
      candidateCount,
      enabledCandidateCount,
      promptType,
      reason: "等待服务端 prompt 提供可提交候选。",
      semanticRows,
      state: "waiting-prompt"
    });
  }

  if (prompt.playerId !== playerId) {
    return responsibilitySubmitPlan({
      candidateCount,
      enabledCandidateCount,
      promptType,
      reason: `当前行动窗口属于 ${prompt.playerId || "未知玩家"}，本地玩家 ${playerId} 只读观察。`,
      semanticRows,
      state: "wrong-player"
    });
  }

  if (!prompt.actionable) {
    return responsibilitySubmitPlan({
      candidateCount,
      enabledCandidateCount,
      promptType,
      reason: prompt.reason?.trim() || "服务端提示当前只读，不能提交行动。",
      semanticRows,
      state: "readonly"
    });
  }

  if (enabledCandidateCount <= 0) {
    return responsibilitySubmitPlan({
      candidateCount,
      enabledCandidateCount,
      promptType,
      reason: "服务端 prompt 已到达，但没有可用候选。",
      semanticRows,
      state: "no-candidates"
    });
  }

  return responsibilitySubmitPlan({
    candidateCount,
    enabledCandidateCount,
    promptType,
    reason: `服务端 prompt 提供 ${enabledCandidateCount}/${candidateCount} 个可用候选。`,
    semanticRows,
    state: "ready"
  });
}

function responsibilitySubmitPlan({
  candidateCount,
  enabledCandidateCount,
  promptType,
  reason,
  semanticRows,
  state
}: {
  candidateCount: number;
  enabledCandidateCount: number;
  promptType: string;
  reason: string;
  semanticRows: WireRuleQueueResponsibilitySubmitSemanticRow[];
  state: WireRuleQueueResponsibilitySubmitState;
}): WireRuleQueueResponsibilitySubmitPlan {
  return {
    canSubmit: state === "ready",
    candidateCount,
    enabledCandidateCount,
    promptType,
    reason,
    semanticRows,
    semanticSummary: responsibilitySubmitSemanticSummary(semanticRows),
    state,
    stateLabel: responsibilitySubmitStateLabel(state)
  };
}

function responsibilitySubmitSemanticRows(prompt?: ActionPromptDto): WireRuleQueueResponsibilitySubmitSemanticRow[] {
  const byKey = new Map<string, WireRuleQueueResponsibilitySubmitSemanticRow>();
  for (const candidate of prompt?.candidates ?? []) {
    const category = normalizedPresentationText(candidate.presentation?.category, "custom");
    const intent = normalizedPresentationText(candidate.presentation?.intent, candidate.action.toLowerCase().replaceAll("_", "-"));
    const priority = typeof candidate.presentation?.priority === "number" && Number.isFinite(candidate.presentation.priority)
      ? candidate.presentation.priority
      : 700;
    const uiHint = normalizedPresentationText(candidate.presentation?.uiHint, "card-action");
    const key = `${category}:${intent}:${uiHint}`;
    const existing = byKey.get(key);
    if (existing) {
      existing.count += 1;
      existing.enabledCount += candidate.enabled ? 1 : 0;
      existing.priority = Math.min(existing.priority, priority);
      continue;
    }

    byKey.set(key, {
      category,
      count: 1,
      enabledCount: candidate.enabled ? 1 : 0,
      intent,
      key,
      priority,
      uiHint
    });
  }

  return [...byKey.values()].sort((left, right) =>
    left.priority - right.priority
    || left.category.localeCompare(right.category)
    || left.intent.localeCompare(right.intent));
}

function responsibilitySubmitSemanticSummary(rows: WireRuleQueueResponsibilitySubmitSemanticRow[]): string {
  if (rows.length === 0) {
    return "无动作语义";
  }

  const enabledRows = rows.filter((row) => row.enabledCount > 0);
  const summaryRows = enabledRows.length > 0 ? enabledRows : rows;
  const values = summaryRows.map((row) =>
    `${row.category}/${row.intent}${row.enabledCount > 0 && row.enabledCount !== row.count ? ` ${row.enabledCount}/${row.count}` : row.count > 1 ? ` x${row.count}` : ""}`);
  const visible = values.slice(0, 2);
  return values.length > 2 ? `${visible.join(" / ")} +${values.length - 2}` : visible.join(" / ");
}

function normalizedPresentationText(value: string | null | undefined, fallback: string): string {
  const trimmed = value?.trim();
  return trimmed || fallback;
}

function responsibilitySubmitStateLabel(state: WireRuleQueueResponsibilitySubmitState): string {
  switch (state) {
    case "history":
      return "历史回看";
    case "no-candidates":
      return "无可用候选";
    case "ready":
      return "可提交";
    case "readonly":
      return "只读窗口";
    case "waiting-lane":
      return "等待前序";
    case "waiting-prompt":
      return "等待提示";
    case "wrong-player":
      return "非当前玩家";
  }
}

function responsibilityItemState(
  lane: WireRuleQueueLaneKey,
  state: WireRuleQueueState,
  activeForLane: boolean
): WireRuleQueueResponsibilityState {
  if (lane === "resolution") {
    return "history";
  }

  if (!activeForLane) {
    return "waiting";
  }

  switch (state) {
    case "stack-response":
      return lane === "stack" ? "respond" : "waiting";
    case "task-blocked":
      return lane === "task" ? "blocked" : "waiting";
    case "task-open":
    case "trigger-pending":
      return "watch";
    case "idle":
    case "resolution-history":
      return "waiting";
  }
}

function responsibilityStateLabel(state: WireRuleQueueState): string {
  switch (state) {
    case "idle":
      return "无待响应";
    case "resolution-history":
      return "可回看";
    case "stack-response":
      return "响应窗口";
    case "task-blocked":
      return "规则阻塞";
    case "task-open":
      return "任务观察";
    case "trigger-pending":
      return "触发观察";
  }
}

function responsibilitySummaryFor(
  state: WireRuleQueueState,
  activeCount: number,
  totalCount: number,
  submitReadyCount: number
): string {
  if (totalCount === 0) {
    return "当前没有服务端结算链、规则任务、触发或近期规则事件。";
  }

  switch (state) {
    case "stack-response":
      return `${activeCount} 个当前响应入口，${submitReadyCount} 个可提交入口；提交动作仍以服务端 prompt 候选为准。`;
    case "task-blocked":
      return `${activeCount} 个阻塞规则任务，${submitReadyCount} 个可提交入口；普通行动保持只读直到服务端推进。`;
    case "task-open":
      return `${activeCount} 个规则任务可观察，${submitReadyCount} 个可提交入口；等待服务端任务队列推进。`;
    case "trigger-pending":
      return `${activeCount} 个触发队列项可观察，${submitReadyCount} 个可提交入口；排序或结算由服务端窗口裁定。`;
    case "resolution-history":
      return `${totalCount} 个近期规则事件可回看。`;
    case "idle":
      return "当前没有活动响应责任。";
  }
}

function responsibilityActorLabel(item: WireRuleQueueSequenceItem): string {
  if (item.lane === "stack") {
    return `控制者 ${item.stateLabel}`;
  }

  if (item.lane === "task") {
    return item.stateLabel;
  }

  if (item.lane === "trigger") {
    return `控制者 ${item.stateLabel}`;
  }

  return item.tickLabel ?? item.stateLabel;
}

function responsibilityActionLabel(
  lane: WireRuleQueueLaneKey,
  state: WireRuleQueueResponsibilityState
): string {
  if (state === "blocked") {
    return "等待规则任务";
  }

  if (state === "respond") {
    return "响应结算链";
  }

  if (state === "history") {
    return "查看事件";
  }

  if (state === "watch") {
    return lane === "trigger" ? "查看触发" : "查看任务";
  }

  return "等待前序";
}

function responsibilityReason(
  item: WireRuleQueueSequenceItem,
  state: WireRuleQueueResponsibilityState
): string {
  switch (state) {
    case "blocked":
      return "该规则任务阻塞普通行动，前端只展示服务端队列并等待推进。";
    case "history":
      return "已完成的规则事件，可打开详情核对对象投影。";
    case "respond":
      return "结算链顶部项目；是否能响应、响应方式和提交字段都以服务端 prompt 为准。";
    case "waiting":
      return "等待前序结算、任务或触发先处理。";
    case "watch":
      return item.lane === "trigger"
        ? "触发队列等待排序或结算，前端不自行决定触发顺序。"
        : "规则任务等待服务端推进，前端不重算状态动作。";
  }
}

function responsibilityItemStateLabel(state: WireRuleQueueResponsibilityState): string {
  switch (state) {
    case "blocked":
      return "阻塞";
    case "history":
      return "历史";
    case "respond":
      return "可响应";
    case "waiting":
      return "等待";
    case "watch":
      return "观察";
  }
}

function headerPlanFor({
  promptId,
  snapshotTick,
  state
}: {
  promptId?: string | null;
  snapshotTick?: number;
  state: WireRuleQueueState;
}): WireRuleQueueHeaderPlan {
  return {
    statusLabel: queueStateLabel(state),
    statusTone: statusToneForState(state),
    subtitle: `tick ${snapshotTick ?? "无"} / prompt ${promptId ? "已提供" : "无"}`,
    title: "结算链 / 规则事件"
  };
}

function statusToneForState(state: WireRuleQueueState): WireRuleQueueStatusTone {
  switch (state) {
    case "task-blocked":
      return "warn";
    case "stack-response":
    case "trigger-pending":
      return "info";
    case "task-open":
    case "resolution-history":
      return "neutral";
    case "idle":
      return "neutral";
  }
}

function focusPlanFor({
  activeLaneKey,
  interactionModel,
  sections,
  state
}: {
  activeLaneKey: WireRuleQueueLaneKey | "none";
  interactionModel: PromptInteractionModel;
  sections: WireRuleQueueSectionPlan[];
  state: WireRuleQueueState;
}): WireRuleQueueFocusPlan {
  const activeSection = sections.find((section) => section.key === activeLaneKey);
  const detail = activeSection?.items[0]?.detail;
  return {
    actionRows: ruleFocusActionRows(detail?.refs ?? [], interactionModel),
    detail,
    emptyLabel: "当前没有活动规则对象。",
    laneKey: activeLaneKey,
    laneLabel: activeSection?.title ?? "无活动通道",
    reasonLabel: focusReasonLabel(state, activeSection?.title)
  };
}

function ruleFocusActionRows(
  refs: WireRuleQueueObjectRef[],
  interactionModel: PromptInteractionModel
): WireRuleQueueFocusActionRow[] {
  const grouped = new Map<string, Set<string>>();
  for (const ref of refs) {
    if (!ref.id || ref.id === "HIDDEN" || ref.visibility === "hidden") {
      continue;
    }

    const roles = grouped.get(ref.id) ?? new Set<string>();
    roles.add(ref.role);
    grouped.set(ref.id, roles);
  }

  return Array.from(grouped.entries()).map(([objectId, serverRoles]) => {
    const summary = interactionModel.objectById.get(objectId);
    const candidates = candidatesForObject(interactionModel.candidates, objectId);
    const actionRoleLabels = uniqueStrings((summary?.choices ?? [])
      .filter((choice) => choice.role !== "mode")
      .filter((choice) => promptChoiceSummaryObjectIds(choice).includes(objectId))
      .map((choice) => promptChoiceRoleLabel(choice.role)));
    const enabledCandidateCount = summary?.enabledCandidateCount ?? candidates.filter((candidate) => candidate.enabled).length;
    const disabledCandidateCount = summary?.disabledCandidateCount ?? candidates.filter((candidate) => !candidate.enabled).length;
    const state = focusActionState(enabledCandidateCount, disabledCandidateCount);
    const semanticSummary = focusActionSemanticSummary(candidates);

    return {
      actionRoleLabels,
      candidateCount: enabledCandidateCount + disabledCandidateCount,
      disabledCandidateCount,
      enabledCandidateCount,
      key: `focus-action:${objectId}`,
      nextStepLabel: focusActionNextStepLabel(state, actionRoleLabels, candidates),
      objectId,
      semanticSummary,
      serverRoleLabel: Array.from(serverRoles).join(" / "),
      state,
      stateLabel: focusActionStateLabel(state)
    };
  });
}

function candidatesForObject(candidates: PromptCandidateSummary[], objectId: string): PromptCandidateSummary[] {
  return candidates.filter((candidate) =>
    candidate.choices.some((choice) =>
      choice.role !== "mode"
      && promptChoiceSummaryObjectIds(choice).includes(objectId)));
}

function focusActionState(enabledCandidateCount: number, disabledCandidateCount: number): WireRuleQueueFocusActionState {
  if (enabledCandidateCount > 0) {
    return "ready";
  }

  if (disabledCandidateCount > 0) {
    return "blocked";
  }

  return "referenced";
}

function focusActionStateLabel(state: WireRuleQueueFocusActionState): string {
  switch (state) {
    case "blocked":
      return "候选阻断";
    case "ready":
      return "候选可用";
    case "referenced":
      return "仅规则引用";
  }
}

function focusActionNextStepLabel(
  state: WireRuleQueueFocusActionState,
  actionRoleLabels: string[],
  candidates: PromptCandidateSummary[]
): string {
  if (state === "ready") {
    return actionRoleLabels.length > 0
      ? `该对象可作为${actionRoleLabels.join("/")}参与当前服务端候选。`
      : "该对象关联当前可用服务端候选。";
  }

  if (state === "blocked") {
    return candidates.find((candidate) => !candidate.enabled)?.reason || "该对象存在服务端候选，但当前不可提交。";
  }

  return "该对象来自当前规则焦点，当前 prompt 未给它行动候选。";
}

function focusActionSemanticSummary(candidates: PromptCandidateSummary[]): string {
  const values = uniqueStrings(candidates.map((candidate) =>
    `${candidate.presentation.category}/${candidate.presentation.intent}`));
  if (values.length === 0) {
    return "无候选语义";
  }

  const visible = values.slice(0, 2);
  return values.length > 2 ? `${visible.join(" / ")} +${values.length - 2}` : visible.join(" / ");
}

function uniqueStrings(values: string[]): string[] {
  return Array.from(new Set(values.map((value) => value.trim()).filter(Boolean)));
}

function focusReasonLabel(state: WireRuleQueueState, laneTitle: string | undefined): string {
  switch (state) {
    case "idle":
      return "等待服务端下一条规则事件。";
    case "resolution-history":
      return "最近完成的规则事件可用于回看桌面投影。";
    case "stack-response":
      return "当前优先查看结算链顶部项目。";
    case "task-blocked":
      return "阻塞普通行动的规则任务优先展示。";
    case "task-open":
      return "当前服务端规则任务可检查。";
    case "trigger-pending":
      return "当前触发队列等待服务端处理。";
    default:
      return laneTitle ? `${laneTitle} 可检查。` : "当前规则焦点可检查。";
  }
}

function queueState({ counts, isBlocking }: { counts: RuleQueueCounts; isBlocking: boolean }): WireRuleQueueState {
  if (counts.taskCount > 0) {
    return isBlocking ? "task-blocked" : "task-open";
  }

  if (counts.stackCount > 0) {
    return "stack-response";
  }

  if (counts.triggerCount > 0) {
    return "trigger-pending";
  }

  if (counts.battlefieldResolutionCount + counts.battleResolutionCount + counts.ruleEventCount > 0) {
    return "resolution-history";
  }

  return "idle";
}

function activeLaneForState(state: WireRuleQueueState): WireRuleQueueLaneKey | "none" {
  switch (state) {
    case "resolution-history":
      return "resolution";
    case "stack-response":
      return "stack";
    case "task-blocked":
    case "task-open":
      return "task";
    case "trigger-pending":
      return "trigger";
    case "idle":
      return "none";
  }
}

function queueStateLabel(state: WireRuleQueueState): string {
  switch (state) {
    case "idle":
      return "空闲";
    case "resolution-history":
      return "近期规则事件";
    case "stack-response":
      return "等待响应";
    case "task-blocked":
      return "规则阻塞";
    case "task-open":
      return "规则任务";
    case "trigger-pending":
      return "触发待处理";
  }
}

function nextStepLabel(state: WireRuleQueueState): string {
  switch (state) {
    case "idle":
      return "等待服务端下一条规则事件。";
    case "resolution-history":
      return "选择近期规则事件，查看对象投影与关联候选。";
    case "stack-response":
      return "等待双方响应，或由服务端继续结算链。";
    case "task-blocked":
      return "等待服务端处理阻塞规则任务，普通行动保持只读。";
    case "task-open":
      return "查看规则任务详情，等待服务端推进任务队列。";
    case "trigger-pending":
      return "等待触发排序或触发结算。";
  }
}

function lane({
  count,
  headline,
  hint,
  key,
  label,
  state
}: WireRuleQueueLane): WireRuleQueueLane {
  return { count, headline, hint, key, label, state };
}

function laneState(
  activeLaneKey: WireRuleQueueLaneKey | "none",
  key: WireRuleQueueLaneKey,
  count: number,
  blocked: boolean
): WireRuleQueueLaneState {
  if (count <= 0) {
    return "empty";
  }

  if (blocked) {
    return "blocked";
  }

  return activeLaneKey === key ? "active" : "waiting";
}

function inspectorPlan({
  activeLaneKey,
  coverage,
  lanes,
  nextStepLabel,
  sequence,
  state
}: {
  activeLaneKey: WireRuleQueueLaneKey | "none";
  coverage: WireRuleQueueCoverageRow[];
  lanes: WireRuleQueueLane[];
  nextStepLabel: string;
  sequence: WireRuleQueueSequenceItem[];
  state: WireRuleQueueState;
}): WireRuleQueueInspectorPlan {
  const activeLane = lanes.find((lane) => lane.key === activeLaneKey);
  const stateLabel = queueStateLabel(state);
  const activeLaneLabel = activeLane?.label ?? "无活动通道";

  return {
    activeLaneKey,
    activeLaneLabel,
    coverage,
    lanes: lanes.map((laneItem) => ({
      ...laneItem,
      stateLabel: laneStateLabel(laneItem.state)
    })),
    nextStepLabel,
    sequence: sequence.map((item) => ({
      detailLabel: item.detailLabel,
      key: item.key,
      label: item.label,
      laneLabel: laneLabel(item.lane),
      objectCount: item.objectCount,
      refs: item.refs,
      stateLabel: item.stateLabel,
      tickLabel: item.tickLabel
    })),
    state,
    stateLabel,
    summary: `${stateLabel} / ${activeLaneLabel} / ${nextStepLabel}`
  };
}

function ruleCoverageRows({
  battleResolutions,
  battlefieldResolutions,
  prompt,
  ruleEvents,
  stack,
  tasks,
  timing,
  triggers
}: {
  battleResolutions: Array<Record<string, unknown>>;
  battlefieldResolutions: Array<Record<string, unknown>>;
  prompt?: ActionPromptDto;
  ruleEvents: GameEvent[];
  stack: Array<Record<string, unknown>>;
  tasks: Array<Record<string, unknown>>;
  timing: Record<string, unknown>;
  triggers: Array<Record<string, unknown>>;
}): WireRuleQueueCoverageRow[] {
  const eventCounts = coverageEventCounts(ruleEvents);
  const objectRefCounts = coverageObjectRefCounts(ruleEvents);
  const serverCoverage = serverRuleQueueCoverage(timing);
  const liveCounts = fallbackLiveCoverageCounts({ battleResolutions, battlefieldResolutions, prompt, stack, tasks, timing, triggers });

  return coverageKeys.map((key) => {
    const serverRow = serverCoverage[key];
    const liveCount = serverRow?.liveCount ?? liveCounts[key] ?? 0;
    const eventCount = eventCounts[key] ?? 0;
    const objectRefCount = objectRefCounts[key] ?? 0;
    const state = coverageState(liveCount, eventCount);
    return {
      eventCount,
      hint: coverageHint(key, state, liveCount, eventCount, objectRefCount, serverRow?.evidenceKeys ?? []),
      key,
      label: coverageLabel(key),
      liveCount,
      objectRefCount,
      state,
      stateLabel: coverageStateLabel(state)
    };
  });
}

function fallbackLiveCoverageCounts({
  battleResolutions,
  battlefieldResolutions,
  prompt,
  stack,
  tasks,
  timing,
  triggers
}: {
  battleResolutions: Array<Record<string, unknown>>;
  battlefieldResolutions: Array<Record<string, unknown>>;
  prompt?: ActionPromptDto;
  stack: Array<Record<string, unknown>>;
  tasks: Array<Record<string, unknown>>;
  timing: Record<string, unknown>;
  triggers: Array<Record<string, unknown>>;
}): Record<WireRuleQueueCoverageKey, number> {
  const battle = asRecord(timing.battle);
  const pendingPayment = asRecord(timing.pendingPayment);
  const turnWindowState = asString(asRecord(timing.turnWindow).state, "");

  return {
    battle: battleResolutions.length
      + battlefieldResolutions.length
      + (battle.isActive === true ? 1 : 0)
      + tasks.filter((task) => isBattleLiveTaskKind(asString(task.kind, ""))).length,
    payment: (Object.keys(pendingPayment).length > 0 ? 1 : 0)
      + tasks.filter((task) => isPaymentLiveTaskKind(asString(task.kind, ""))).length,
    stack: stack.length,
    trigger: triggers.length,
    window: prompt !== undefined || turnWindowState.length > 0 ? 1 : 0
  };
}

function serverRuleQueueCoverage(timing: Record<string, unknown>): ServerRuleQueueCoverageMap {
  const coverage: ServerRuleQueueCoverageMap = {};
  for (const row of asArray<Record<string, unknown>>(timing.ruleQueueCoverage)) {
    const key = normalizeCoverageKey(row.key);
    if (!key) {
      continue;
    }

    coverage[key] = {
      evidenceKeys: asArray<string>(row.evidenceKeys).filter((value) => typeof value === "string" && value.trim().length > 0),
      liveCount: Math.max(0, Math.floor(asNumber(row.liveCount, 0)))
    };
  }

  return coverage;
}

function normalizeCoverageKey(value: unknown): WireRuleQueueCoverageKey | undefined {
  if (typeof value !== "string") {
    return undefined;
  }

  return (coverageKeys as readonly string[]).includes(value) ? value as WireRuleQueueCoverageKey : undefined;
}

const coverageKeys: WireRuleQueueCoverageKey[] = ["stack", "trigger", "battle", "window", "payment"];

function coverageEventCounts(events: GameEvent[]): Record<WireRuleQueueCoverageKey, number> {
  const counts = emptyCoverageRecord();
  for (const event of events) {
    for (const key of coverageKeysForEventKind(event.kind)) {
      counts[key] += 1;
    }
  }

  return counts;
}

function coverageObjectRefCounts(events: GameEvent[]): Record<WireRuleQueueCoverageKey, number> {
  const counts = emptyCoverageRecord();
  for (const event of events) {
    const refCount = ruleEventObjectRefs(event).filter((ref) => ref.id !== "HIDDEN").length;
    for (const key of coverageKeysForEventKind(event.kind)) {
      counts[key] += refCount;
    }
  }

  return counts;
}

function emptyCoverageRecord(): Record<WireRuleQueueCoverageKey, number> {
  return {
    battle: 0,
    payment: 0,
    stack: 0,
    trigger: 0,
    window: 0
  };
}

function coverageKeysForEventKind(kind: string): WireRuleQueueCoverageKey[] {
  const keys: WireRuleQueueCoverageKey[] = [];
  if (kind.startsWith("STACK") || kind.startsWith("SPELL_DUEL")) {
    keys.push("stack");
  }
  if (kind.startsWith("TRIGGER") || kind.endsWith("_TRIGGER_RESOLVED")) {
    keys.push("trigger");
  }
  if (kind.startsWith("BATTLE") || kind.startsWith("COMBAT") || kind === "DAMAGE_APPLIED" || kind.includes("CONQUEST")) {
    keys.push("battle");
  }
  if (kind === "PRIORITY_PASSED" || kind === "FOCUS_PASSED") {
    keys.push("window");
  }
  if (kind.startsWith("PAYMENT") || kind === "COST_PAID") {
    keys.push("payment");
  }

  return keys;
}

function isBattleLiveTaskKind(kind: string): boolean {
  return kind.startsWith("BATTLE") || kind === "START_BATTLE";
}

function isPaymentLiveTaskKind(kind: string): boolean {
  return kind.startsWith("PAYMENT") || kind === "COST_PAID";
}

function coverageState(liveCount: number, eventCount: number): WireRuleQueueCoverageState {
  if (liveCount > 0 && eventCount > 0) {
    return "mixed";
  }

  if (liveCount > 0) {
    return "live";
  }

  if (eventCount > 0) {
    return "history";
  }

  return "empty";
}

function coverageLabel(key: WireRuleQueueCoverageKey): string {
  switch (key) {
    case "battle":
      return "战斗/战场";
    case "payment":
      return "费用/支付";
    case "stack":
      return "结算链";
    case "trigger":
      return "触发";
    case "window":
      return "窗口/优先权";
  }
}

function coverageStateLabel(state: WireRuleQueueCoverageState): string {
  switch (state) {
    case "empty":
      return "未出现";
    case "history":
      return "近期事件";
    case "live":
      return "实时快照";
    case "mixed":
      return "快照+事件";
  }
}

function coverageHint(
  key: WireRuleQueueCoverageKey,
  state: WireRuleQueueCoverageState,
  liveCount: number,
  eventCount: number,
  objectRefCount: number,
  evidenceKeys: string[]
): string {
  if (state === "empty") {
    return `${coverageLabel(key)}当前没有服务端快照或近期事件。`;
  }

  const evidence = evidenceKeys.length > 0 ? ` / 依据 ${evidenceKeys.join(", ")}` : "";
  return `${coverageLabel(key)}：实时 ${liveCount} / 事件 ${eventCount} / 对象引用 ${objectRefCount}${evidence}`;
}

function laneLabel(key: WireRuleQueueLaneKey): string {
  switch (key) {
    case "resolution":
      return "近期事件";
    case "stack":
      return "结算链";
    case "task":
      return "规则任务";
    case "trigger":
      return "触发队列";
  }
}

function laneStateLabel(state: WireRuleQueueLaneState): string {
  switch (state) {
    case "active":
      return "活动";
    case "blocked":
      return "阻塞";
    case "empty":
      return "空";
    case "waiting":
      return "等待";
  }
}

function queueSequence({
  battleResolutions,
  battlefieldResolutions,
  ruleEvents,
  stack,
  tasks,
  triggers
}: {
  battleResolutions: Array<Record<string, unknown>>;
  battlefieldResolutions: Array<Record<string, unknown>>;
  ruleEvents: GameEvent[];
  stack: Array<Record<string, unknown>>;
  tasks: Array<Record<string, unknown>>;
  triggers: Array<Record<string, unknown>>;
}): WireRuleQueueSequenceItem[] {
  return [
    ...stack.map((item, index) => ({
      detailLabel: stackEffectLabel(asString(item.effectKind, "")),
      key: `stack:${asString(item.stackItemId, String(index))}`,
      label: `结算链 ${stack.length - index}`,
      lane: "stack" as const,
      objectCount: objectCount([item.sourceObjectId, ...asArray(item.targetObjectIds)]),
      refs: compactObjectRefs([
        objectRef("来源", optionalString(item.sourceObjectId)),
        ...objectRefs("目标", stringArray(item.targetObjectIds))
      ]),
      stateLabel: asString(item.controllerId, "未知控制者")
    })),
    ...tasks.map((task, index) => ({
      detailLabel: taskKindLabel(asString(task.kind, "")),
      key: `task:${asString(task.taskId, String(index))}`,
      label: `任务 ${index + 1}`,
      lane: "task" as const,
      objectCount: objectCount([task.battlefieldObjectId, ...asArray(task.participantObjectIds)]),
      refs: compactObjectRefs([
        objectRef("战场", optionalString(task.battlefieldObjectId)),
        ...objectRefs("参与", stringArray(task.participantObjectIds))
      ]),
      stateLabel: asString(task.status, "状态未提供")
    })),
    ...triggers.map((trigger, index) => ({
      detailLabel: stackEffectLabel(asString(trigger.effectKind, "")),
      key: `trigger:${asString(trigger.triggerId, String(index))}`,
      label: `触发 ${index + 1}`,
      lane: "trigger" as const,
      objectCount: objectCount([trigger.sourceObjectId]),
      refs: compactObjectRefs([
        objectRef("来源", optionalString(trigger.sourceVisibility) === "HIDDEN" ? "HIDDEN" : optionalString(trigger.sourceObjectId))
      ]),
      stateLabel: asString(trigger.controllerId, "无控制者")
    })),
    ...battlefieldResolutions.map((resolution, index) => ({
      detailLabel: battlefieldResolutionLabel(asString(resolution.kind, "")),
      key: `battlefield-resolution:${asString(resolution.resolutionId, String(index))}`,
      label: `战场事件 ${index + 1}`,
      lane: "resolution" as const,
      objectCount: objectCount([resolution.battlefieldObjectId, resolution.sourceObjectId, ...asArray(resolution.participantObjectIds)]),
      refs: compactObjectRefs([
        objectRef("战场", optionalString(resolution.battlefieldObjectId)),
        objectRef("来源", nullableString(resolution.sourceObjectId)),
        ...objectRefs("参与", stringArray(resolution.participantObjectIds))
      ]),
      stateLabel: asString(resolution.playerId, asString(resolution.controllerId, "无控制者")),
      tickLabel: tickLabel(resolution.tick)
    })),
    ...battleResolutions.map((resolution, index) => ({
      detailLabel: battleResolutionLabel(asString(resolution.kind, "")),
      key: `battle-resolution:${asString(resolution.resolutionId, String(index))}`,
      label: `战斗事件 ${index + 1}`,
      lane: "resolution" as const,
      objectCount: objectCount([
        resolution.battlefieldId,
        ...asArray(resolution.attackerObjectIds),
        ...asArray(resolution.defenderObjectIds),
        ...asArray(resolution.destroyedObjectIds)
      ]),
      refs: compactObjectRefs([
        objectRef("战场", optionalString(resolution.battlefieldId)),
        ...objectRefs("攻击", stringArray(resolution.attackerObjectIds)),
        ...objectRefs("防守", stringArray(resolution.defenderObjectIds)),
        ...objectRefs("被摧毁", stringArray(resolution.destroyedObjectIds))
      ]),
      stateLabel: asString(resolution.winnerPlayerId, "无胜者"),
      tickLabel: tickLabel(resolution.tick)
    })),
    ...ruleEvents.map((event, index) => {
      const refs = ruleEventObjectRefs(event);
      return {
        detailLabel: eventKindLabel(event.kind),
        key: `rule-event:${event.kind}:${index}`,
        label: `服务端事件 ${index + 1}`,
        lane: "resolution" as const,
        objectCount: refs.filter((ref) => ref.id !== "HIDDEN").length,
        refs,
        stateLabel: eventDescriptionLabel(event)
      };
    })
  ].slice(0, 8);
}

function ruleQueueSections({
  battleResolutions,
  battlefieldResolutions,
  objects,
  playerId,
  queue,
  ruleEvents,
  stack,
  tasks,
  triggers
}: {
  battleResolutions: Array<Record<string, unknown>>;
  battlefieldResolutions: Array<Record<string, unknown>>;
  objects: SnapshotObjectIndex;
  playerId: string;
  queue: Record<string, unknown>;
  ruleEvents: GameEvent[];
  stack: Array<Record<string, unknown>>;
  tasks: Array<Record<string, unknown>>;
  triggers: Array<Record<string, unknown>>;
}): WireRuleQueueSectionPlan[] {
  return [
    {
      emptyLabel: "当前无结算链项目。",
      items: stack.map((item, index) => stackItemPlan(item, index, stack.length, playerId, objects)),
      key: "stack",
      notes: [],
      title: "结算链"
    },
    {
      emptyLabel: "当前无待处理规则任务。",
      items: tasks.map((task, index) => taskItemPlan(task, index, playerId, objects)),
      key: "task",
      notes: tasks.length > 0 ? taskQueueNotes(queue) : [],
      title: "规则任务"
    },
    {
      emptyLabel: "当前无待排序或待结算触发。",
      items: triggers.map((trigger, index) => triggerItemPlan(trigger, index, playerId, objects)),
      key: "trigger",
      notes: [],
      title: "触发队列"
    },
    {
      emptyLabel: "暂无近期战场、战斗结算或服务端规则事件。",
      items: [
        ...battlefieldResolutions.map((resolution, index) => battlefieldResolutionItemPlan(resolution, index, playerId, objects)),
        ...battleResolutions.map((resolution, index) => battleResolutionItemPlan(resolution, index, playerId, objects)),
        ...ruleEvents.map((event, index) => ruleEventItemPlan(event, index, objects))
      ],
      key: "resolution",
      notes: [],
      title: "近期规则事件"
    }
  ];
}

function stackItemPlan(
  item: Record<string, unknown>,
  index: number,
  stackLength: number,
  playerId: string,
  objects: SnapshotObjectIndex
): WireRuleQueueItemPlan {
  const stackItemId = asString(item.stackItemId, String(index));
  const controllerId = optionalString(item.controllerId);
  const sourceObjectId = optionalString(item.sourceObjectId);
  const cardNo = nullableString(item.cardNo);
  const targetObjectIds = stringArray(item.targetObjectIds);
  const effectKind = asString(item.effectKind, "");
  const damageAmount = numberValue(item.damageAmount);
  const destination = optionalString(item.destination);
  const orderLine = detailLine("顺序", stackOrderLabel(index));
  const responseLine = detailLine("响应", stackResponseLabel(index));
  const lines = compactLines([
    detailLine("控制者", controllerId ?? "未知", controllerId === playerId),
    detailLine("来源", sourceLabel(sourceObjectId, cardNo, objects)),
    detailLine("目标", objectListLabel(targetObjectIds, objects)),
    damageAmount != null && damageAmount > 0 ? detailLine("伤害", String(damageAmount)) : undefined,
    destination ? detailLine("去向", zoneLabel(destination)) : undefined,
    orderLine,
    responseLine
  ]);
  const refs = compactObjectRefs([
    objectRef("来源", sourceObjectId),
    ...objectRefs("目标", targetObjectIds)
  ]);
  const detail = detailPlan({
    id: ruleDetailId("stack", stackItemId),
    lines: compactLines([
      ...lines,
      detailLine("权威", "服务端结算链快照；前端不重算优先权"),
      detailLine("边界", "公开结算链项；对象身份以服务端快照和公开引用为准"),
      detailLine("服务端编号", idLabel(stackItemId))
    ]),
    refs,
    subtitle: `${stackEffectLabel(effectKind)} / 项目 ${stackLength - index}`,
    title: "结算链项目"
  });

  return {
    detail,
    key: `stack:${stackItemId}`,
    lines,
    refs,
    subtitle: stackEffectLabel(effectKind),
    title: `项目 ${stackLength - index}`
  };
}

function taskItemPlan(
  task: Record<string, unknown>,
  index: number,
  playerId: string,
  objects: SnapshotObjectIndex
): WireRuleQueueItemPlan {
  const taskId = asString(task.taskId, String(index));
  const actingPlayerId = nullableString(task.actingPlayerId);
  const battlefieldObjectId = optionalString(task.battlefieldObjectId);
  const participantObjectIds = stringArray(task.participantObjectIds);
  const stackItemIds = stringArray(task.stackItemIds);
  const spellDuelId = optionalString(task.spellDuelId);
  const battleId = optionalString(task.battleId);
  const status = optionalString(task.status);
  const lines = compactLines([
    detailLine("原因", taskReasonLabel(optionalString(task.reason))),
    detailLine("战场", battlefieldObjectId ? objectLabel(battlefieldObjectId, objects) : "无"),
    detailLine("行动玩家", actingPlayerId ?? "无", actingPlayerId === playerId),
    detailLine("参与对象", objectListLabel(participantObjectIds, objects)),
    stackItemIds.length > 0 ? detailLine("关联结算链", `${stackItemIds.length} 项`) : undefined,
    spellDuelId ? detailLine("法术对决", "服务端已创建") : undefined,
    battleId ? detailLine("战斗", "服务端已创建") : undefined
  ]);
  const refs = compactObjectRefs([
    objectRef("战场", battlefieldObjectId),
    ...objectRefs("参与", participantObjectIds)
  ]);
  const detail = detailPlan({
    id: ruleDetailId("task", taskId),
    lines: compactLines([detailLine("状态", status ? protocolValue(status, "服务端状态") : "状态未提供"), ...lines]),
    refs,
    subtitle: status ? protocolValue(status, "服务端状态") : "状态未提供",
    title: taskKindLabel(optionalString(task.kind))
  });

  return {
    detail,
    key: `task:${taskId}`,
    lines: compactLines([detailLine("状态", status ? protocolValue(status, "服务端状态") : "状态未提供"), ...lines]),
    refs,
    subtitle: status ? protocolValue(status, "服务端状态") : "状态未提供",
    title: taskKindLabel(optionalString(task.kind))
  };
}

function triggerItemPlan(
  trigger: Record<string, unknown>,
  index: number,
  playerId: string,
  objects: SnapshotObjectIndex
): WireRuleQueueItemPlan {
  const triggerId = asString(trigger.triggerId, String(index));
  const controllerId = optionalString(trigger.controllerId);
  const sourceObjectId = optionalString(trigger.sourceObjectId);
  const sourceVisibility = optionalString(trigger.sourceVisibility);
  const refs = compactObjectRefs(sourceVisibility === "HIDDEN" ? [objectRef("来源", "HIDDEN")] : [objectRef("来源", sourceObjectId)]);
  const lines = compactLines([
    detailLine("控制者", controllerId ?? "无", controllerId === playerId),
    detailLine("来源", sourceVisibility === "HIDDEN" ? "隐藏来源" : objectLabel(sourceObjectId, objects)),
    detailLine("来源事件", eventLabel(optionalString(trigger.triggeredByEventKind)))
  ]);
  const detail = detailPlan({
    id: ruleDetailId("trigger", triggerId),
    lines: compactLines([...lines, detailLine("服务端编号", idLabel(triggerId))]),
    refs,
    subtitle: stackEffectLabel(optionalString(trigger.effectKind)),
    title: `触发 ${index + 1}`
  });

  return {
    detail,
    key: `trigger:${triggerId}`,
    lines,
    refs,
    subtitle: stackEffectLabel(optionalString(trigger.effectKind)),
    title: `触发 ${index + 1}`
  };
}

function battlefieldResolutionItemPlan(
  resolution: Record<string, unknown>,
  index: number,
  playerId: string,
  objects: SnapshotObjectIndex
): WireRuleQueueItemPlan {
  const resolutionId = asString(resolution.resolutionId, String(index));
  const battlefieldObjectId = optionalString(resolution.battlefieldObjectId);
  const playerOrController = nullableString(resolution.playerId) ?? nullableString(resolution.controllerId);
  const participantObjectIds = stringArray(resolution.participantObjectIds);
  const relatedEventKinds = stringArray(resolution.relatedEventKinds);
  const tick = numberValue(resolution.tick);
  const lines = compactLines([
    detailLine("战场", objectLabel(battlefieldObjectId, objects)),
    detailLine("控制者", playerOrController ?? "无", playerOrController === playerId),
    detailLine("参与对象", objectListLabel(participantObjectIds, objects)),
    detailLine("事件", eventListLabel(relatedEventKinds))
  ]);
  const refs = compactObjectRefs([
    objectRef("战场", battlefieldObjectId),
    objectRef("来源", nullableString(resolution.sourceObjectId)),
    ...objectRefs("参与", participantObjectIds)
  ]);
  const detail = detailPlan({
    id: ruleDetailId("battlefield-resolution", resolutionId),
    lines: compactLines([
      ...lines,
      detailLine("之前控制者", nullableString(resolution.previousControllerId) ?? "无", nullableString(resolution.previousControllerId) === playerId),
      detailLine("tick", tick == null ? "无" : String(tick))
    ]),
    refs,
    subtitle: `tick ${tick ?? "无"}`,
    title: battlefieldResolutionLabel(optionalString(resolution.kind))
  });

  return {
    detail,
    key: `battlefield-resolution:${resolutionId}`,
    lines: compactLines([...lines, detailLine("tick", tick == null ? "无" : String(tick))]),
    refs,
    subtitle: `tick ${tick ?? "无"}`,
    title: battlefieldResolutionLabel(optionalString(resolution.kind))
  };
}

function battleResolutionItemPlan(
  resolution: Record<string, unknown>,
  index: number,
  playerId: string,
  objects: SnapshotObjectIndex
): WireRuleQueueItemPlan {
  const resolutionId = asString(resolution.resolutionId, String(index));
  const battlefieldId = optionalString(resolution.battlefieldId);
  const destroyedObjectIds = stringArray(resolution.destroyedObjectIds);
  const relatedEventKinds = stringArray(resolution.relatedEventKinds);
  const winnerPlayerId = nullableString(resolution.winnerPlayerId);
  const tick = numberValue(resolution.tick);
  const lines = compactLines([
    detailLine("战场", objectLabel(battlefieldId, objects)),
    detailLine("胜者", winnerPlayerId ?? "无", winnerPlayerId === playerId),
    detailLine("被摧毁", objectListLabel(destroyedObjectIds, objects)),
    detailLine("事件", eventListLabel(relatedEventKinds))
  ]);
  const refs = compactObjectRefs([
    objectRef("战场", battlefieldId),
    ...objectRefs("攻击", stringArray(resolution.attackerObjectIds)),
    ...objectRefs("防守", stringArray(resolution.defenderObjectIds)),
    ...objectRefs("被摧毁", destroyedObjectIds)
  ]);
  const detail = detailPlan({
    id: ruleDetailId("battle-resolution", resolutionId),
    lines: compactLines([
      detailLine("攻击方", nullableString(resolution.attackingPlayerId) ?? "无", nullableString(resolution.attackingPlayerId) === playerId),
      detailLine("防守方", nullableString(resolution.defendingPlayerId) ?? "无", nullableString(resolution.defendingPlayerId) === playerId),
      ...lines,
      detailLine("tick", tick == null ? "无" : String(tick))
    ]),
    refs,
    subtitle: `tick ${tick ?? "无"}`,
    title: battleResolutionLabel(optionalString(resolution.kind))
  });

  return {
    detail,
    key: `battle-resolution:${resolutionId}`,
    lines: compactLines([...lines, detailLine("tick", tick == null ? "无" : String(tick))]),
    refs,
    subtitle: `tick ${tick ?? "无"}`,
    title: battleResolutionLabel(optionalString(resolution.kind))
  };
}

function ruleEventItemPlan(
  event: GameEvent,
  index: number,
  objects: SnapshotObjectIndex
): WireRuleQueueItemPlan {
  const refs = ruleEventObjectRefs(event);
  const refSource = gameEventObjectRefPlan(event).source;
  const lines = compactLines([
    detailLine("类型", eventKindLabel(event.kind)),
    detailLine("描述", eventDescriptionLabel(event)),
    detailLine("对象", refs.length > 0 ? `${refs.length} 项` : "无"),
    detailLine("引用", gameEventObjectRefSourceLabel(refSource))
  ]);
  const detail = detailPlan({
    id: ruleDetailId("event", `${event.kind}:${index}`),
    lines: compactLines([
      ...lines,
      detailLine("边界", "服务端事件；前端只展示公开对象引用和描述"),
      detailLine("对象可见性", ruleEventVisibilityLabel(refs, objects))
    ]),
    refs,
    subtitle: eventDescriptionLabel(event),
    title: eventKindLabel(event.kind)
  });

  return {
    detail,
    key: `event:${event.kind}:${index}`,
    lines,
    refs,
    subtitle: eventDescriptionLabel(event),
    title: eventKindLabel(event.kind)
  };
}

function taskQueueNotes(queue: Record<string, unknown>): string[] {
  const phase = taskPhaseLabel(asString(queue.phase, ""));
  const active = asString(queue.activeTaskId, "");
  const blocking = Boolean(queue.isBlocking);
  return [
    `队列阶段：${phase}`,
    `活动任务：${active ? "处理中" : "无"}`,
    blocking ? "阻塞普通行动" : "不阻塞普通行动"
  ];
}

function detailPlan({
  id,
  lines,
  refs,
  subtitle,
  title
}: {
  id: string;
  lines: WireRuleQueueDetailLine[];
  refs: WireRuleQueueObjectRef[];
  subtitle?: string;
  title: string;
}): WireRuleQueueDetailPlan {
  return {
    id,
    lines,
    refs,
    source: "rule",
    subtitle,
    title
  };
}

function detailLine(label: string, value: string | null | undefined, mine?: boolean): WireRuleQueueDetailLine {
  return { label, mine, value: value || "无" };
}

function compactLines(lines: Array<WireRuleQueueDetailLine | undefined>): WireRuleQueueDetailLine[] {
  return lines.filter((item): item is WireRuleQueueDetailLine => Boolean(item));
}

function objectRef(role: string, id: string | null | undefined): WireRuleQueueObjectRef {
  return { id: id?.trim() ?? "", role };
}

function objectRefs(role: string, ids: string[] | undefined): WireRuleQueueObjectRef[] {
  return (ids ?? []).map((id) => objectRef(role, id));
}

function compactObjectRefs(refs: WireRuleQueueObjectRef[]): WireRuleQueueObjectRef[] {
  return refs.filter((ref) => ref.id.trim().length > 0);
}

function ruleDetailId(kind: string, id: string): string {
  return `rule:${kind}:${id}`;
}

function objectLabel(objectId: string | null | undefined, objects: SnapshotObjectIndex): string {
  if (!objectId) {
    return "无";
  }

  if (objectId === "HIDDEN") {
    return "隐藏对象";
  }

  const object = objects[objectId];
  return object?.cardNo ? `${object.cardNo}` : idLabel(objectId);
}

function sourceLabel(sourceObjectId: string | null | undefined, cardNo: string | null | undefined, objects: SnapshotObjectIndex): string {
  if (cardNo) {
    return sourceObjectId ? `${cardNo} / ${objectLabel(sourceObjectId, objects)}` : cardNo;
  }

  return objectLabel(sourceObjectId, objects);
}

function objectListLabel(objectIds: string[] | undefined, objects: SnapshotObjectIndex): string {
  const ids = objectIds ?? [];
  if (ids.length === 0) {
    return "无";
  }

  if (ids.length <= 3) {
    return ids.map((id) => objectLabel(id, objects)).join(" / ");
  }

  return `${ids.length} 个对象`;
}

function eventLabel(kind: string | null | undefined): string {
  return kind && kind.trim().length > 0 ? labelFor({}, kind, "服务端事件", "无") : "无";
}

function eventListLabel(kinds: string[] | undefined): string {
  const values = kinds ?? [];
  if (values.length === 0) {
    return "无";
  }

  if (values.length <= 2) {
    return values.map(eventLabel).join(" / ");
  }

  return `${values.length} 个事件`;
}

function zoneLabel(value: string): string {
  const labels: Record<string, string> = {
    BASE: "基地",
    GRAVEYARD: "已打出",
    HAND: "手牌",
    STACK: "结算链"
  };
  return labelFor(labels, value, "服务端区域", "无");
}

function protocolValue(value: string | null | undefined, fallback: string): string {
  const raw = value?.trim() ?? "";
  if (!raw) {
    return "无";
  }

  return isProtocolToken(raw) ? fallback : raw;
}

function idLabel(value: string): string {
  return isProtocolToken(value) ? "服务端对象" : redactInternalText(value);
}

function stringArray(value: unknown): string[] {
  return asArray(value).filter((item): item is string => typeof item === "string" && item.trim().length > 0);
}

function optionalString(value: unknown): string {
  return typeof value === "string" && value.trim().length > 0 ? value : "";
}

function nullableString(value: unknown): string | null {
  return typeof value === "string" && value.trim().length > 0 ? value : null;
}

function numberValue(value: unknown): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function topStackHeadline(item: Record<string, unknown>, stackLength: number): string {
  return `${stackEffectLabel(asString(item.effectKind, ""))} / 顶部 ${stackLength}`;
}

function stackOrderLabel(index: number): string {
  if (index === 0) {
    return "顶部；下一个结算";
  }

  return `等待上方 ${index} 项`;
}

function stackResponseLabel(index: number): string {
  if (index === 0) {
    return "响应窗口由服务端 prompt 裁定";
  }

  return "先等待上方结算链项目";
}

function topTaskHeadline(task: Record<string, unknown>, isBlocking: boolean): string {
  return `${isBlocking ? "阻塞" : "待处理"} / ${taskKindLabel(asString(task.kind, ""))}`;
}

function topTriggerHeadline(trigger: Record<string, unknown>): string {
  return `${stackEffectLabel(asString(trigger.effectKind, ""))} / ${asString(trigger.controllerId, "无控制者")}`;
}

function taskQueueHint(queue: Record<string, unknown>, isBlocking: boolean): string {
  const phase = taskPhaseLabel(asString(queue.phase, ""));
  const active = asString(queue.activeTaskId, "");
  return `${phase} / ${active ? "活动任务处理中" : "无活动任务"} / ${isBlocking ? "阻塞行动" : "不阻塞普通行动"}`;
}

function resolutionHeadline(
  battlefieldResolutions: Array<Record<string, unknown>>,
  battleResolutions: Array<Record<string, unknown>>,
  ruleEvents: GameEvent[]
): string {
  const firstBattlefield = battlefieldResolutions[0];
  if (firstBattlefield) {
    return battlefieldResolutionLabel(asString(firstBattlefield.kind, ""));
  }

  const firstBattle = battleResolutions[0];
  if (firstBattle) {
    return battleResolutionLabel(asString(firstBattle.kind, ""));
  }

  const firstEvent = ruleEvents[0];
  if (firstEvent) {
    return eventKindLabel(firstEvent.kind);
  }

  return "空";
}

function objectCount(values: unknown[]): number {
  return new Set(values.filter((value): value is string => typeof value === "string" && value.trim().length > 0 && value !== "HIDDEN")).size;
}

function tickLabel(value: unknown): string | undefined {
  return typeof value === "number" && Number.isFinite(value) ? `tick ${value}` : undefined;
}

function ruleQueueEvents(events: GameEvent[]): GameEvent[] {
  return events
    .filter((event) => isRuleQueueEventKind(event.kind))
    .slice(-6)
    .reverse();
}

function isRuleQueueEventKind(kind: string): boolean {
  return kind.startsWith("BATTLE")
    || kind.startsWith("STACK")
    || kind.startsWith("TRIGGER")
    || kind.startsWith("SPELL_DUEL")
    || kind.startsWith("COMBAT")
    || kind.startsWith("PAYMENT")
    || kind === "COST_PAID"
    || kind === "DAMAGE_APPLIED"
    || kind === "PRIORITY_PASSED"
    || kind === "FOCUS_PASSED"
    || kind.endsWith("_TRIGGER_RESOLVED")
    || kind.includes("CONQUEST");
}

function ruleEventObjectRefs(event: GameEvent): WireRuleQueueObjectRef[] {
  return compactObjectRefs(gameEventObjectRefPlan(event).refs.map((ref) => ({
    id: ref.isHidden ? "HIDDEN" : ref.objectId,
    label: ref.isHidden ? "隐藏对象" : ref.cardNo ?? undefined,
    role: ref.role || "对象",
    visibility: ref.isHidden || ref.objectId === "HIDDEN" ? "hidden" : undefined
  })));
}

function ruleEventVisibilityLabel(refs: WireRuleQueueObjectRef[], objects: SnapshotObjectIndex): string {
  if (refs.length === 0) {
    return "无引用";
  }

  const hidden = refs.filter((ref) => ref.visibility === "hidden" || ref.id === "HIDDEN").length;
  const visible = refs.filter((ref) => ref.id !== "HIDDEN" && Boolean(objects[ref.id])).length;
  const missing = refs.length - hidden - visible;
  return `可见 ${visible} / 隐藏 ${hidden} / 缺失 ${missing}`;
}

function stackEffectLabel(value: string): string {
  const labels: Record<string, string> = {
    ABILITY: "技能",
    HEXTECH_RAY_DAMAGE_3: "海克斯射线伤害",
    LEGEND_ABILITY: "传奇技能",
    REVEAL_CARD: "翻开待命",
    SPELL: "法术",
    TRIGGER: "触发"
  };
  return labelFor(labels, value, "服务端效果", "效果");
}

function taskKindLabel(value: string): string {
  const labels: Record<string, string> = {
    BATTLEFIELD_CONTESTED: "战场控制检查",
    DESTROY_LETHAL_UNIT: "致命伤害清理",
    DESTROY_ZERO_POWER_UNIT: "0 战力清理",
    REMOVE_ILLEGAL_STANDBY: "待命清理",
    RECALL_UNATTACHED_EQUIPMENT: "装备清理",
    START_BATTLE: "开始战斗",
    START_SPELL_DUEL: "开始法术对决"
  };
  return labelFor(labels, value, "服务端任务", "任务");
}

function taskPhaseLabel(value: string): string {
  const labels: Record<string, string> = {
    BATTLE_TASKS: "战斗任务",
    BATTLEFIELD_TASKS: "战场任务",
    IDLE: "空闲",
    SPELL_DUEL_TASKS: "法术对决任务",
    STATE_BASED_CLEANUP: "状态清理"
  };
  return labelFor(labels, value, "服务端阶段", "无");
}

function taskReasonLabel(value: string): string {
  const labels: Record<string, string> = {
    ADDITIONAL_COST: "额外费用",
    BATTLE_CLEANUP: "战斗清理",
    BATTLE_CLEANUP_ATTACKER_RECALL: "战斗后召回攻击者",
    BATTLEFIELD_CONTROL_CLEANUP: "战场控制清理",
    BATTLEFIELD_CONTESTED: "战场争夺",
    LETHAL_DAMAGE: "致命伤害",
    UNATTACHED_EQUIPMENT_CLEANUP: "装备脱离清理",
    ZERO_POWER: "0 战力"
  };
  return labelFor(labels, value, "服务端原因", "无");
}

function battlefieldResolutionLabel(value: string): string {
  const labels: Record<string, string> = {
    CONQUERED: "征服",
    CONTROL_RESOLVED: "控制结算",
    HELD: "据守"
  };
  return labelFor(labels, value, "战场结果", "战场结果");
}

function battleResolutionLabel(value: string): string {
  const labels: Record<string, string> = {
    CLOSED: "战斗结束",
    NO_RESULT: "战斗无结果"
  };
  return labelFor(labels, value, "战斗结果", "战斗结果");
}

function labelFor(map: Record<string, string>, value: string, protocolFallback: string, emptyFallback: string): string {
  const raw = value.trim();
  if (!raw) {
    return emptyFallback;
  }

  return map[raw] ?? (isProtocolToken(raw) ? protocolFallback : raw);
}

function isProtocolToken(value: string): boolean {
  return /^[A-Z0-9_:-]+$/.test(value) || /^[a-z0-9]+(?:[-_:][a-z0-9]+)+$/.test(value);
}

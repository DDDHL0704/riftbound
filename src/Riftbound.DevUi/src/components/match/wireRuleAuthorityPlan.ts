import type { GameEvent, SnapshotDto } from "../../types/protocol";
import { asArray, asRecord, asString } from "../../utils/collections";

export type WireRuleAuthorityState = "fallback" | "missing" | "mixed" | "server";

export type WireRuleAuthorityMetric = {
  key: string;
  label: string;
  state: WireRuleAuthorityState;
  value: string;
};

export type WireRuleAuthorityRow = {
  key: string;
  label: string;
  state: WireRuleAuthorityState;
  stateLabel: string;
  value: string;
};

export type WireRuleAuthorityPlan = {
  issueCount: number;
  metrics: WireRuleAuthorityMetric[];
  rows: WireRuleAuthorityRow[];
  state: WireRuleAuthorityState;
  stateLabel: string;
  summary: string;
};

export function buildWireRuleAuthorityPlan({
  events = [],
  snapshot
}: {
  events?: GameEvent[];
  snapshot?: SnapshotDto;
}): WireRuleAuthorityPlan {
  const timing = asRecord(snapshot?.timing);
  const queue = asRecord(timing.pendingTaskQueue);
  const stack = asArray<Record<string, unknown>>(snapshot?.stack);
  const pendingTasks = asArray<Record<string, unknown>>(queue.tasks);
  const battlefieldTasks = asArray<Record<string, unknown>>(timing.battlefieldTasks);
  const tasks = [...pendingTasks, ...battlefieldTasks];
  const triggers = asArray<Record<string, unknown>>(timing.triggerQueue);
  const battlefieldResolutions = asArray<Record<string, unknown>>(timing.battlefieldResolutions);
  const battleResolutions = asArray<Record<string, unknown>>(timing.battleResolutions);
  const resolutions = [...battlefieldResolutions, ...battleResolutions];
  const stackAuthority = stackRow(snapshot, stack);
  const taskAuthority = taskRow(snapshot, queue, tasks);
  const triggerAuthority = triggerRow(snapshot, triggers);
  const resolutionAuthority = resolutionRow(snapshot, resolutions);
  const eventRefAuthority = eventRefRow(events);
  const rows = [
    snapshotRow(snapshot),
    stackAuthority,
    taskAuthority,
    triggerAuthority,
    resolutionAuthority,
    eventRefAuthority
  ];
  const issueCount = rows.filter((row) => row.state !== "server").length;
  const state = authorityState(rows.map((row) => row.state));

  return {
    issueCount,
    metrics: [
      { key: "stack", label: "结算链", state: stackAuthority.state, value: `${stack.length}` },
      { key: "task", label: "规则任务", state: taskAuthority.state, value: `${tasks.length}` },
      { key: "trigger", label: "触发", state: triggerAuthority.state, value: `${triggers.length}` },
      { key: "resolution", label: "近期事件", state: resolutionAuthority.state, value: `${resolutions.length}` },
      { key: "eventRefs", label: "事件引用", state: eventRefAuthority.state, value: `${eventObjectRefCount(events)}` },
      { key: "issues", label: "待补齐", state: issueCount === 0 ? "server" : state, value: String(issueCount) }
    ],
    rows,
    state,
    stateLabel: authorityStateLabel(state),
    summary: authoritySummary(state, issueCount)
  };
}

function snapshotRow(snapshot: SnapshotDto | undefined): WireRuleAuthorityRow {
  if (!snapshot) {
    return row("snapshot", "服务端快照", "missing", "缺少 snapshot", "等待服务端同步");
  }

  return row("snapshot", "服务端快照", "server", "已同步", `tick ${snapshot.tick}`);
}

function stackRow(
  snapshot: SnapshotDto | undefined,
  stack: Array<Record<string, unknown>>
): WireRuleAuthorityRow {
  if (!snapshot) {
    return row("stack", "结算链", "missing", "缺少快照", "0");
  }

  if (stack.length === 0) {
    return row("stack", "结算链", "server", "服务端空链", "0 项");
  }

  const complete = stack.every((item) =>
    hasText(item.stackItemId)
    && hasText(item.effectKind)
    && (hasText(item.sourceObjectId) || hasText(item.cardNo)));
  return row("stack", "结算链", complete ? "server" : "mixed", complete ? "结构完整" : "缺少项目字段", `${stack.length} 项`);
}

function taskRow(
  snapshot: SnapshotDto | undefined,
  queue: Record<string, unknown>,
  tasks: Array<Record<string, unknown>>
): WireRuleAuthorityRow {
  if (!snapshot) {
    return row("task", "规则任务", "missing", "缺少快照", "0");
  }

  if (tasks.length === 0) {
    return row("task", "规则任务", "server", "服务端空队列", "0 项");
  }

  const queueBlocking = Boolean(queue.isBlocking);
  const queueHasActiveTask = !queueBlocking || hasText(queue.activeTaskId);
  const complete = queueHasActiveTask && tasks.every((task) =>
    hasText(task.taskId)
    && hasText(task.kind)
    && hasText(task.status));
  return row(
    "task",
    "规则任务",
    complete ? "server" : "mixed",
    complete ? "结构完整" : "缺少任务字段",
    `${tasks.length} 项 / ${queueBlocking ? "阻塞" : "非阻塞"}`
  );
}

function triggerRow(
  snapshot: SnapshotDto | undefined,
  triggers: Array<Record<string, unknown>>
): WireRuleAuthorityRow {
  if (!snapshot) {
    return row("trigger", "触发队列", "missing", "缺少快照", "0");
  }

  if (triggers.length === 0) {
    return row("trigger", "触发队列", "server", "服务端空队列", "0 项");
  }

  const complete = triggers.every((trigger) =>
    hasText(trigger.triggerId)
    && hasText(trigger.effectKind)
    && (hasText(trigger.sourceObjectId) || asString(trigger.sourceVisibility, "") === "HIDDEN"));
  return row("trigger", "触发队列", complete ? "server" : "mixed", complete ? "结构完整" : "缺少触发字段", `${triggers.length} 项`);
}

function resolutionRow(
  snapshot: SnapshotDto | undefined,
  resolutions: Array<Record<string, unknown>>
): WireRuleAuthorityRow {
  if (!snapshot) {
    return row("resolution", "近期规则事件", "missing", "缺少快照", "0");
  }

  if (resolutions.length === 0) {
    return row("resolution", "近期规则事件", "server", "服务端无历史", "0 项");
  }

  const complete = resolutions.every((resolution) =>
    hasText(resolution.resolutionId)
    && hasText(resolution.kind)
    && Number.isFinite(Number(resolution.tick))
    && (hasText(resolution.battlefieldObjectId) || hasText(resolution.battlefieldId)));
  return row("resolution", "近期规则事件", complete ? "server" : "mixed", complete ? "结构完整" : "缺少事件字段", `${resolutions.length} 项`);
}

function eventRefRow(events: GameEvent[]): WireRuleAuthorityRow {
  if (events.length === 0) {
    return row("eventRefs", "事件对象引用", "server", "暂无事件", "0 个引用");
  }

  const explicitRefCount = eventObjectRefCount(events);
  if (events.every((event) => (event.objectRefs?.length ?? 0) > 0)) {
    return row("eventRefs", "事件对象引用", "server", "服务端 objectRefs", `${explicitRefCount} 个引用`);
  }

  const derivedRefCount = events.reduce((count, event) => count + payloadObjectRefCount(event.payload), 0);
  if (derivedRefCount > 0) {
    return row("eventRefs", "事件对象引用", "mixed", "payload 派生", `${explicitRefCount} 显式 / ${derivedRefCount} 派生`);
  }

  return row("eventRefs", "事件对象引用", "fallback", "无对象引用", `${events.length} 个事件`);
}

function authorityState(states: WireRuleAuthorityState[]): WireRuleAuthorityState {
  if (states.length === 0 || states.includes("missing")) {
    return "missing";
  }

  if (states.every((state) => state === "server")) {
    return "server";
  }

  if (states.some((state) => state === "fallback")) {
    return states.some((state) => state === "server" || state === "mixed") ? "mixed" : "fallback";
  }

  return "mixed";
}

function authorityStateLabel(state: WireRuleAuthorityState): string {
  switch (state) {
    case "server":
      return "服务端权威";
    case "mixed":
      return "部分兜底";
    case "fallback":
      return "前端兜底";
    case "missing":
      return "材料缺失";
  }
}

function authoritySummary(state: WireRuleAuthorityState, issueCount: number): string {
  switch (state) {
    case "server":
      return "结算链、规则任务、触发和事件引用均有可解释的服务端材料。";
    case "mixed":
      return `仍有 ${issueCount} 项规则材料不完整，需要继续补齐服务端投影。`;
    case "fallback":
      return "规则事件主要依赖前端从 payload 猜测，不适合作为最终规则视图。";
    case "missing":
      return "缺少服务端规则快照材料，当前只能等待同步。";
  }
}

function row(
  key: string,
  label: string,
  state: WireRuleAuthorityState,
  stateLabel: string,
  value: string
): WireRuleAuthorityRow {
  return { key, label, state, stateLabel, value };
}

function hasText(value: unknown): boolean {
  return typeof value === "string" && value.trim().length > 0;
}

function eventObjectRefCount(events: GameEvent[]): number {
  return events.reduce((count, event) => count + (event.objectRefs?.filter((ref) => ref.objectId && !ref.isHidden).length ?? 0), 0);
}

function payloadObjectRefCount(payload: Record<string, unknown>, depth = 0): number {
  if (depth > 2) {
    return 0;
  }

  let count = 0;
  for (const [key, value] of Object.entries(payload ?? {})) {
    if ((key.endsWith("ObjectId") || key.endsWith("Id")) && hasText(value)) {
      count += 1;
      continue;
    }

    if ((key.endsWith("ObjectIds") || key.endsWith("Ids")) && Array.isArray(value)) {
      count += value.filter(hasText).length;
      continue;
    }

    if (value && typeof value === "object" && !Array.isArray(value)) {
      count += payloadObjectRefCount(value as Record<string, unknown>, depth + 1);
    }
  }

  return count;
}

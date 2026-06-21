import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/commandSubmissionFollowupPlan.ts");
const source = readFileSync(sourcePath, "utf8");
const output = ts.transpileModule(source, {
  compilerOptions: {
    esModuleInterop: true,
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

function requireShim(id) {
  if (id === "./eventLogPlan") {
    return {
      eventDescriptionLabel: (event) => event.description || `event:${event.kind}`,
      eventKindLabel: (kind) => `label:${kind}`
    };
  }

  throw new Error(`Unexpected command submission followup plan import: ${id}`);
}

new Function("exports", "module", "require", output)(moduleShim.exports, moduleShim, requireShim);

const { buildCommandSubmissionFollowupPlan } = moduleShim.exports;

assert.deepEqual(
  buildCommandSubmissionFollowupPlan({}).state,
  "empty"
);
assert.equal(buildCommandSubmissionFollowupPlan({}).serverFollowupState, "none");
assert.equal(buildCommandSubmissionFollowupPlan({}).metrics.find((metric) => metric.key === "serverState").value, "无");
assert.equal(buildCommandSubmissionFollowupPlan({}).bridge.state, "empty");
assert.equal(buildCommandSubmissionFollowupPlan({}).bridge.headline, "等待提交");
assert.equal(buildCommandSubmissionFollowupPlan({}).bridge.nextStepLabel, "先提交服务端候选路线。");
assert.deepEqual(
  buildCommandSubmissionFollowupPlan({}).bridge.rows.map((row) => `${row.key}:${row.state}:${row.value}`),
  ["serverState:empty:无", "tick:waiting:无", "events:empty:0", "snapshot:empty:0", "prompt:empty:0"]
);

const pendingPlan = buildCommandSubmissionFollowupPlan({
  feedback: {
    clientIntentId: "client-1",
    cmdType: "END_TURN",
    message: "提交中",
    state: "submitting",
    stateLabel: "提交中"
  },
  snapshot: { tick: 11 }
});
assert.equal(pendingPlan.state, "pending");
assert.equal(pendingPlan.metrics.find((metric) => metric.key === "events").state, "empty");
assert.equal(pendingPlan.serverFollowupState, "pending");
assert.equal(pendingPlan.metrics.find((metric) => metric.key === "serverState").state, "waiting");
assert.equal(pendingPlan.bridge.state, "waiting");
assert.equal(pendingPlan.bridge.headline, "等待服务端回执");
assert.equal(pendingPlan.bridge.rows.find((row) => row.key === "serverState").state, "waiting");

const eventPlan = buildCommandSubmissionFollowupPlan({
  events: [
    { description: "进入主阶段", kind: "MAIN_PHASE_BEGAN", receivedBatchIndex: 0, receivedMessageType: "EVENTS", receivedServerTick: 12 },
    {
      description: "抽牌",
      kind: "CARD_DRAWN",
      objectRefs: [
        { objectId: "card-1", role: "来源" },
        { isHidden: true, objectId: "secret-1", role: "隐藏" }
      ],
      receivedBatchIndex: 1,
      receivedMessageType: "EVENTS",
      receivedServerTick: 12
    },
    { description: "其他 tick", kind: "TURN_ENDED", receivedBatchIndex: 2, receivedMessageType: "EVENTS", receivedServerTick: 13 }
  ],
  feedback: {
    clientIntentId: "client-2",
    cmdType: "END_TURN",
    followup: {
      eventCount: 2,
      eventKinds: ["MAIN_PHASE_BEGAN", "CARD_DRAWN", "CARD_DRAWN"],
      promptCount: 0,
      serverTick: 12,
      snapshotCount: 1,
      state: "events",
      summary: "tick 12 已生成 2 条公开事件、1 个快照、0 个提示。"
    },
    message: "服务端已接受",
    receiptState: "ACCEPTED",
    serverTick: 12,
    state: "sent",
    stateLabel: "服务端已接受",
    uiSource: {
      detailId: "rule:stack:1",
      label: "规则与事件详情",
      objectId: "card-1",
      surface: "timeline-detail"
    }
  },
  snapshot: { tick: 12 }
});
assert.equal(eventPlan.state, "accepted-events");
assert.equal(eventPlan.uiSource?.surface, "timeline-detail");
assert.equal(eventPlan.uiSource?.detailId, "rule:stack:1");
assert.equal(eventPlan.uiSource?.objectId, "card-1");
assert.deepEqual(
  eventPlan.serverEventKinds.map((row) => `${row.kind}:${row.label}`),
  ["MAIN_PHASE_BEGAN:label:MAIN_PHASE_BEGAN", "CARD_DRAWN:label:CARD_DRAWN"]
);
assert.equal(eventPlan.events.length, 2);
assert.equal(eventPlan.events[0].title, "label:MAIN_PHASE_BEGAN");
assert.equal(eventPlan.events[1].refCount, 2);
assert.equal(eventPlan.events[1].refs[0].objectId, "card-1");
assert.equal(eventPlan.events[1].refs[0].label, "来源：card-1");
assert.equal(eventPlan.events[1].refs[1].hidden, true);
assert.equal(eventPlan.events[1].refs[1].objectId, undefined);
assert.equal(eventPlan.events[1].refs[1].label, "隐藏：隐藏对象");
assert.equal(eventPlan.metrics.find((metric) => metric.key === "events").value, "2");
assert.equal(eventPlan.metrics.find((metric) => metric.key === "snapshot").state, "ready");
assert.equal(eventPlan.metrics.find((metric) => metric.key === "prompt").value, "0");
assert.equal(eventPlan.serverFollowupState, "events");
assert.equal(eventPlan.metrics.find((metric) => metric.key === "serverState").value, "事件");
assert.equal(eventPlan.bridge.state, "ready");
assert.equal(eventPlan.bridge.headline, "已收到同 tick 事件");
assert.equal(eventPlan.bridge.nextStepLabel, "查看事件引用，必要时选择对象检查规则上下文。");
assert.equal(eventPlan.bridge.rows.find((row) => row.key === "events").state, "ready");

const receiptAwaitingPlan = buildCommandSubmissionFollowupPlan({
  events: [],
  feedback: {
    clientIntentId: "client-2b",
    cmdType: "END_TURN",
    followup: {
      eventCount: 2,
      promptCount: 2,
      serverTick: 15,
      snapshotCount: 2,
      state: "events",
      summary: "tick 15 已生成 2 条公开事件、2 个快照、2 个提示。"
    },
    message: "服务端已接受",
    receiptState: "ACCEPTED",
    serverTick: 15,
    state: "sent",
    stateLabel: "服务端已接受"
  },
  snapshot: { tick: 14 }
});
assert.equal(receiptAwaitingPlan.state, "accepted-awaiting");
assert.equal(receiptAwaitingPlan.hiddenEventCount, 2);
assert.equal(receiptAwaitingPlan.metrics.find((metric) => metric.key === "events").value, "2");
assert.equal(receiptAwaitingPlan.metrics.find((metric) => metric.key === "prompt").state, "ready");
assert.equal(receiptAwaitingPlan.serverFollowupState, "events");
assert.equal(receiptAwaitingPlan.serverFollowupStateLabel, "事件");
assert.equal(receiptAwaitingPlan.metrics.find((metric) => metric.key === "serverState").value, "事件");
assert.equal(receiptAwaitingPlan.bridge.state, "waiting");
assert.equal(receiptAwaitingPlan.bridge.headline, "等待同 tick 广播");
assert.equal(receiptAwaitingPlan.bridge.rows.find((row) => row.key === "snapshot").state, "ready");

const hiddenPlan = buildCommandSubmissionFollowupPlan({
  events: [
    { kind: "A", receivedBatchIndex: 0, receivedServerTick: 2 },
    { kind: "B", receivedBatchIndex: 1, receivedServerTick: 2 },
    { kind: "C", receivedBatchIndex: 2, receivedServerTick: 2 }
  ],
  feedback: {
    clientIntentId: "client-3",
    cmdType: "READY",
    message: "服务端已接受",
    receiptState: "ACCEPTED",
    serverTick: 2,
    state: "sent",
    stateLabel: "服务端已接受"
  },
  limit: 2,
  snapshot: { tick: 2 }
});
assert.equal(hiddenPlan.state, "accepted-events");
assert.equal(hiddenPlan.events.length, 2);
assert.equal(hiddenPlan.hiddenEventCount, 1);

const snapshotPlan = buildCommandSubmissionFollowupPlan({
  events: [],
  feedback: {
    clientIntentId: "client-4",
    cmdType: "PASS_PRIORITY",
    message: "服务端已接受",
    receiptState: "ACCEPTED",
    serverTick: 20,
    state: "sent",
    stateLabel: "服务端已接受"
  },
  snapshot: { tick: 21 }
});
assert.equal(snapshotPlan.state, "accepted-snapshot");
assert.equal(snapshotPlan.metrics.find((metric) => metric.key === "snapshot").state, "ready");

const receiptSnapshotPlan = buildCommandSubmissionFollowupPlan({
  events: [],
  feedback: {
    clientIntentId: "client-4b",
    cmdType: "PASS_PRIORITY",
    followup: {
      eventCount: 0,
      promptCount: 2,
      serverTick: 22,
      snapshotCount: 2,
      state: "snapshot-prompt",
      summary: "tick 22 无公开事件，但已生成 2 个快照、2 个提示。"
    },
    message: "服务端已接受",
    receiptState: "ACCEPTED",
    state: "sent",
    stateLabel: "服务端已接受"
  },
  snapshot: { tick: 21 }
});
assert.equal(receiptSnapshotPlan.state, "accepted-snapshot");
assert.equal(receiptSnapshotPlan.summary, "tick 22 无公开事件，但已生成 2 个快照、2 个提示。");
assert.equal(receiptSnapshotPlan.metrics.find((metric) => metric.key === "events").state, "empty");
assert.equal(receiptSnapshotPlan.metrics.find((metric) => metric.key === "prompt").value, "2");
assert.equal(receiptSnapshotPlan.serverFollowupState, "snapshot-prompt");
assert.equal(receiptSnapshotPlan.metrics.find((metric) => metric.key === "serverState").value, "快照/提示");
assert.equal(receiptSnapshotPlan.bridge.state, "ready");
assert.equal(receiptSnapshotPlan.bridge.headline, "快照/提示已同步");

const awaitingPlan = buildCommandSubmissionFollowupPlan({
  events: [],
  feedback: {
    clientIntentId: "client-5",
    cmdType: "PASS_PRIORITY",
    message: "服务端已接受",
    receiptState: "ACCEPTED",
    serverTick: 30,
    state: "sent",
    stateLabel: "服务端已接受"
  },
  snapshot: { tick: 29 }
});
assert.equal(awaitingPlan.state, "accepted-awaiting");
assert.equal(awaitingPlan.metrics.find((metric) => metric.key === "snapshot").state, "waiting");
assert.equal(awaitingPlan.bridge.state, "waiting");

const unknownTickPlan = buildCommandSubmissionFollowupPlan({
  feedback: {
    clientIntentId: "client-6",
    cmdType: "PASS_PRIORITY",
    message: "服务端已接受",
    receiptState: "ACCEPTED",
    state: "sent",
    stateLabel: "服务端已接受"
  },
  snapshot: { tick: 1 }
});
assert.equal(unknownTickPlan.state, "unknown-tick");
assert.equal(unknownTickPlan.bridge.state, "unknown");
assert.equal(unknownTickPlan.bridge.headline, "缺少回执 tick");

const failedPlan = buildCommandSubmissionFollowupPlan({
  feedback: {
    clientIntentId: "client-7",
    cmdType: "PASS_PRIORITY",
    message: "失败",
    state: "failed",
    stateLabel: "失败"
  },
  snapshot: { tick: 1 }
});
assert.equal(failedPlan.state, "failed");
assert.equal(failedPlan.serverFollowupState, "client-failed");
assert.equal(failedPlan.metrics.find((metric) => metric.key === "serverState").value, "本地失败");
assert.equal(failedPlan.bridge.state, "failed");
assert.equal(failedPlan.bridge.rows.find((row) => row.key === "serverState").state, "blocked");

console.log("Command submission followup plan check passed.");

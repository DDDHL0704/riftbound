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

const eventPlan = buildCommandSubmissionFollowupPlan({
  events: [
    { description: "进入主阶段", kind: "MAIN_PHASE_BEGAN", receivedBatchIndex: 0, receivedMessageType: "EVENTS", receivedServerTick: 12 },
    { description: "抽牌", kind: "CARD_DRAWN", objectRefs: [{ objectId: "card-1", role: "来源" }], receivedBatchIndex: 1, receivedMessageType: "EVENTS", receivedServerTick: 12 },
    { description: "其他 tick", kind: "TURN_ENDED", receivedBatchIndex: 2, receivedMessageType: "EVENTS", receivedServerTick: 13 }
  ],
  feedback: {
    clientIntentId: "client-2",
    cmdType: "END_TURN",
    message: "服务端已接受",
    receiptState: "ACCEPTED",
    serverTick: 12,
    state: "sent",
    stateLabel: "服务端已接受"
  },
  snapshot: { tick: 12 }
});
assert.equal(eventPlan.state, "accepted-events");
assert.equal(eventPlan.events.length, 2);
assert.equal(eventPlan.events[0].title, "label:MAIN_PHASE_BEGAN");
assert.equal(eventPlan.events[1].refCount, 1);
assert.equal(eventPlan.metrics.find((metric) => metric.key === "events").value, "2");
assert.equal(eventPlan.metrics.find((metric) => metric.key === "snapshot").state, "ready");

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

console.log("Command submission followup plan check passed.");

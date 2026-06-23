import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/wireSidePanelReceiptPlan.ts");
const source = readFileSync(sourcePath, "utf8");
const matchPageSource = readFileSync(resolve(scriptDir, "../src/pages/MatchPage.tsx"), "utf8");
const actionMapSource = readFileSync(resolve(scriptDir, "../src/components/match/WireActionMapPanel.tsx"), "utf8");
const output = ts.transpileModule(source, {
  compilerOptions: {
    esModuleInterop: true,
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

function requireShim(id) {
  throw new Error(`Unexpected wire side panel receipt plan import: ${id}`);
}

new Function("exports", "module", "require", output)(moduleShim.exports, moduleShim, requireShim);

const { buildWireSidePanelReceiptPlan } = moduleShim.exports;

const emptyFollowup = {
  bridge: {
    headline: "等待提交",
    nextStepLabel: "先提交服务端候选路线。",
    rows: [
      { key: "serverState", label: "服务端后续", state: "empty", stateLabel: "无", value: "无" },
      { key: "tick", label: "回执 tick", state: "waiting", stateLabel: "等待", value: "无" },
      { key: "events", label: "事件", state: "empty", stateLabel: "无", value: "0" },
      { key: "snapshot", label: "快照", state: "empty", stateLabel: "无", value: "0" }
    ],
    serverStateLabel: "无",
    state: "empty",
    stateLabel: "未提交",
    summary: "尚未提交命令，等待服务端回执。"
  },
  events: [],
  hiddenEventCount: 0,
  metrics: [
    { key: "serverState", label: "服务端后续", state: "empty", value: "无" },
    { key: "tick", label: "回执 tick", state: "waiting", value: "无" },
    { key: "events", label: "事件", state: "empty", value: "0" }
  ],
  serverEventKinds: [],
  serverFollowupState: "none",
  serverFollowupStateLabel: "无",
  sourceRows: [],
  state: "empty",
  summary: "尚未提交命令，等待服务端回执。"
};

const emptyPlan = buildWireSidePanelReceiptPlan({ followup: emptyFollowup });
assert.equal(emptyPlan.mode, "empty");
assert.equal(emptyPlan.state, "empty");
assert.equal(emptyPlan.stateLabel, "尚未提交");
assert.equal(emptyPlan.canOpenLayer, false);
assert.equal(emptyPlan.detailButtonLabel, "打开回执检查层");
assert.equal(emptyPlan.bridge.rows.length, 3);
assert.equal(emptyPlan.bridge.hiddenRowCount, 1);
assert.deepEqual(
  emptyPlan.metrics.map((metric) => `${metric.key}:${metric.state}:${metric.value}`),
  ["command:empty:无", "receipt:empty:无", "followup:empty:无", "events:empty:0"]
);

const acceptedFollowup = {
  ...emptyFollowup,
  bridge: {
    ...emptyFollowup.bridge,
    headline: "已收到同 tick 事件",
    rows: [
      { key: "serverState", label: "服务端后续", state: "ready", stateLabel: "就绪", value: "事件" },
      { key: "tick", label: "回执 tick", state: "ready", stateLabel: "就绪", value: "12" },
      { key: "events", label: "事件", state: "ready", stateLabel: "就绪", value: "2" }
    ],
    serverStateLabel: "事件",
    state: "ready",
    stateLabel: "已同步",
    summary: "tick 12 已生成 2 条公开事件。"
  },
  events: [
    { description: "进入结算链", key: "12:STACK_ITEM_ADDED:0", kind: "STACK_ITEM_ADDED", refCount: 1, refs: [], title: "结算链加入" },
    { description: "据守结算", key: "12:BATTLEFIELD_CONTROL_RESOLVED:1", kind: "BATTLEFIELD_CONTROL_RESOLVED", refCount: 2, refs: [], title: "战场控制结算" },
    { description: "额外事件", key: "12:EXTRA:2", kind: "EXTRA", refCount: 0, refs: [], title: "额外事件" }
  ],
  hiddenEventCount: 1,
  metrics: [
    { key: "serverState", label: "服务端后续", state: "ready", value: "事件" },
    { key: "tick", label: "回执 tick", state: "ready", value: "12" },
    { key: "events", label: "事件", state: "ready", value: "3" }
  ],
  serverFollowupState: "events",
  serverFollowupStateLabel: "事件",
  sourceRows: [
    { key: "surface", label: "入口", state: "ready", value: "规则与事件详情" }
  ],
  state: "accepted-events",
  summary: "tick 12 已生成 2 条公开事件。"
};
const acceptedPlan = buildWireSidePanelReceiptPlan({
  feedback: {
    clientIntentId: "p1-PLAY_CARD-123456789",
    cmdType: "PLAY_CARD",
    message: "服务端已接受",
    receiptState: "ACCEPTED",
    serverTick: 12,
    state: "sent",
    stateLabel: "服务端已接受"
  },
  followup: acceptedFollowup
});
assert.equal(acceptedPlan.mode, "accepted");
assert.equal(acceptedPlan.canOpenLayer, true);
assert.equal(acceptedPlan.detailButtonLabel, "打开回执检查层");
assert.equal(acceptedPlan.eventRows.length, 2);
assert.equal(acceptedPlan.hiddenEventCount, 2);
assert.equal(acceptedPlan.metrics.find((metric) => metric.key === "command").value, "PLAY_CARD");
assert.equal(acceptedPlan.metrics.find((metric) => metric.key === "receipt").value, "ACCEPTED");
assert.equal(acceptedPlan.metrics.find((metric) => metric.key === "followup").value, "事件");

const failedPlan = buildWireSidePanelReceiptPlan({
  feedback: {
    cmdType: "PLAY_CARD",
    errorCode: "RULE_REJECTED",
    message: "命令被服务端规则拒绝",
    receiptState: "REJECTED",
    state: "failed",
    stateLabel: "提交失败"
  },
  followup: {
    ...emptyFollowup,
    bridge: {
      ...emptyFollowup.bridge,
      state: "failed",
      stateLabel: "失败",
      summary: "命令被服务端规则拒绝。"
    },
    serverFollowupState: "rejected",
    serverFollowupStateLabel: "已拒绝",
    state: "failed",
    summary: "命令被服务端规则拒绝。"
  }
});
assert.equal(failedPlan.mode, "failed");
assert.equal(failedPlan.state, "failed");
assert.equal(failedPlan.metrics.find((metric) => metric.key === "receipt").state, "failed");
assert.equal(failedPlan.subtitle, "命令被服务端规则拒绝");

for (const snippet of [
  "data-wire-side-panel-receipt",
  "data-wire-side-panel-receipt-mode",
  "data-wire-side-panel-receipt-state",
  "data-wire-side-panel-receipt-bridge-state",
  "data-wire-side-panel-receipt-can-open-layer",
  "data-wire-side-panel-receipt-event-count",
  "data-wire-side-panel-receipt-hidden-count"
]) {
  assert.ok(actionMapSource.includes(snippet), `CommandSubmissionFeedbackCompactPanel must expose ${snippet}.`);
}

for (const snippet of [
  "data-wire-side-panel-receipt-shell",
  "aria-label=\"服务端提交回执常驻区\"",
  "variant=\"compact\""
]) {
  assert.ok(matchPageSource.includes(snippet), `MatchPage persistent receipt rail must include ${snippet}.`);
}

console.log("wire side panel receipt plan checks passed");

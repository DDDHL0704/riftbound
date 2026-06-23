import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(resolve(scriptDir, "../src/utils/wireSidePanelStackPlan.ts"), "utf8");
const output = ts.transpileModule(source, {
  compilerOptions: {
    esModuleInterop: true,
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

function requireShim(id) {
  throw new Error(`Unexpected wire side panel stack plan import: ${id}`);
}

new Function("exports", "module", "require", output)(moduleShim.exports, moduleShim, requireShim);

const { buildWireSidePanelStackPlan } = moduleShim.exports;

const orchestration = {
  activeCount: 2,
  entries: [
    entry("commandCenter", "指挥中心", "ready", "可提交", 2),
    entry("interaction", "焦点", "review", "焦点", 1),
    entry("ruleQueue", "规则队列", "active", "规则", 3),
    entry("serverFlow", "结算链", "active", "服务端", 3),
    entry("timelineDetail", "详情", "review", "详情", 1)
  ],
  nextStepLabel: "提交服务端候选。",
  primarySlot: "commandCenter",
  state: "ready",
  stateLabel: "可提交",
  summary: "指挥中心 / 可提交",
  urgentCount: 1
};

const hiddenFocusPlan = focusPlan({ visible: false });
const visibleFocusPlan = focusPlan({ visible: true });
const idleRulePlan = rulePlan({ state: "idle" });
const activeRulePlan = rulePlan({ state: "task-blocked" });

const quietPlan = buildWireSidePanelStackPlan({
  activeSlot: "commandCenter",
  focusPlan: hiddenFocusPlan,
  orchestration,
  ruleChainPlan: idleRulePlan
});
assert.equal(quietPlan.byRail.status.mode, "summary");
assert.equal(quietPlan.byRail.status.actionLabel, "总览");
assert.equal(quietPlan.byRail.status.actionSlot, "overview");
assert.equal(quietPlan.byRail.focus.mode, "hidden");
assert.equal(quietPlan.byRail.focus.actionSlot, undefined);
assert.equal(quietPlan.byRail.rules.mode, "hidden");
assert.equal(quietPlan.byRail.receipt.mode, "summary");
assert.equal(quietPlan.byRail.receipt.state, "empty");
assert.equal(quietPlan.byRail.receipt.bodyMode, "compact");
assert.equal(quietPlan.byRail.main.mode, "expanded");
assert.equal(quietPlan.byRail.main.actionSlot, "commandCenter");
assert.equal(quietPlan.density, "balanced");
assert.equal(quietPlan.capacityOverflow, false);
assert.equal(quietPlan.renderedBodyCount, 3);
assert.equal(quietPlan.byRail.status.bodyMode, "compact");
assert.equal(quietPlan.byRail.status.priority, "urgent");
assert.equal(quietPlan.byRail.main.bodyMode, "full");
assert.equal(quietPlan.byRail.main.capacityWeight, 4);
assert.deepEqual(quietPlan.visibleEntries.map((rail) => rail.key), ["status", "receipt", "main"]);

const focusedPlan = buildWireSidePanelStackPlan({
  activeSlot: "commandCenter",
  focusPlan: visibleFocusPlan,
  orchestration,
  ruleChainPlan: idleRulePlan
});
assert.equal(focusedPlan.byRail.focus.mode, "summary");
assert.equal(focusedPlan.byRail.focus.state, "normal");
assert.equal(focusedPlan.byRail.focus.actionSlot, "interaction");
assert.equal(focusedPlan.byRail.focus.actionLabel, "焦点");
assert.equal(focusedPlan.density, "crowded");
assert.equal(focusedPlan.byRail.status.bodyMode, "collapsed");
assert.equal(focusedPlan.byRail.focus.bodyMode, "collapsed");
assert.equal(focusedPlan.renderedBodyCount, 2);

const actionMapEmptyReceiptPlan = buildWireSidePanelStackPlan({
  activeSlot: "actionMap",
  focusPlan: visibleFocusPlan,
  orchestration,
  ruleChainPlan: idleRulePlan
});
assert.equal(actionMapEmptyReceiptPlan.byRail.focus.mode, "summary");
assert.equal(actionMapEmptyReceiptPlan.byRail.focus.bodyMode, "compact");
assert.equal(actionMapEmptyReceiptPlan.byRail.receipt.mode, "summary");
assert.equal(actionMapEmptyReceiptPlan.byRail.receipt.state, "empty");
assert.equal(actionMapEmptyReceiptPlan.byRail.receipt.bodyMode, "compact");
assert.equal(actionMapEmptyReceiptPlan.byRail.receipt.reason, "尚未提交命令。");
assert.equal(actionMapEmptyReceiptPlan.capacityOverflow, false);

const timelineDetailReceiptPlan = buildWireSidePanelStackPlan({
  activeSlot: "timelineDetail",
  focusPlan: hiddenFocusPlan,
  orchestration,
  ruleChainPlan: activeRulePlan
});
assert.equal(timelineDetailReceiptPlan.byRail.rules.mode, "summary");
assert.equal(timelineDetailReceiptPlan.byRail.rules.bodyMode, "compact");
assert.equal(timelineDetailReceiptPlan.byRail.receipt.mode, "summary");
assert.equal(timelineDetailReceiptPlan.byRail.receipt.bodyMode, "compact");
assert.equal(timelineDetailReceiptPlan.byRail.main.actionSlot, "timelineDetail");
assert.equal(timelineDetailReceiptPlan.capacityOverflow, false);

const expandedFocusPlan = buildWireSidePanelStackPlan({
  activeSlot: "interaction",
  focusPlan: visibleFocusPlan,
  orchestration,
  ruleChainPlan: idleRulePlan
});
assert.equal(expandedFocusPlan.byRail.focus.mode, "expanded");
assert.equal(expandedFocusPlan.byRail.focus.state, "primary");
assert.equal(expandedFocusPlan.byRail.focus.bodyMode, "full");
assert.equal(expandedFocusPlan.byRail.focus.priority, "primary");

const rulesSummaryPlan = buildWireSidePanelStackPlan({
  activeSlot: "commandCenter",
  focusPlan: hiddenFocusPlan,
  orchestration,
  ruleChainPlan: activeRulePlan
});
assert.equal(rulesSummaryPlan.byRail.rules.mode, "summary");
assert.equal(rulesSummaryPlan.byRail.rules.state, "urgent");
assert.equal(rulesSummaryPlan.byRail.rules.actionSlot, "ruleQueue");
assert.equal(rulesSummaryPlan.density, "urgent");
assert.equal(rulesSummaryPlan.byRail.rules.bodyMode, "compact");
assert.equal(rulesSummaryPlan.byRail.rules.priority, "urgent");
assert.equal(rulesSummaryPlan.capacityOverflow, false);

const rulesExpandedPlan = buildWireSidePanelStackPlan({
  activeSlot: "ruleQueue",
  focusPlan: hiddenFocusPlan,
  orchestration,
  ruleChainPlan: activeRulePlan
});
assert.equal(rulesExpandedPlan.byRail.rules.mode, "expanded");
assert.equal(rulesExpandedPlan.byRail.rules.state, "primary");
assert.equal(rulesExpandedPlan.byRail.rules.bodyMode, "full");

const receiptSummaryPlan = buildWireSidePanelStackPlan({
  activeSlot: "ruleQueue",
  focusPlan: hiddenFocusPlan,
  orchestration,
  ruleChainPlan: activeRulePlan,
  submissionFeedback: receipt("sent")
});
assert.equal(receiptSummaryPlan.byRail.receipt.mode, "summary");
assert.equal(receiptSummaryPlan.byRail.receipt.state, "normal");
assert.equal(receiptSummaryPlan.byRail.receipt.actionSlot, "commandCenter");
assert.equal(receiptSummaryPlan.density, "crowded");
assert.equal(receiptSummaryPlan.byRail.receipt.bodyMode, "collapsed");
assert.equal(receiptSummaryPlan.byRail.status.bodyMode, "collapsed");
assert.equal(receiptSummaryPlan.capacityOverflow, false);

const receiptExpandedPlan = buildWireSidePanelStackPlan({
  activeSlot: "commandCenter",
  focusPlan: hiddenFocusPlan,
  orchestration,
  ruleChainPlan: activeRulePlan,
  submissionFeedback: receipt("failed")
});
assert.equal(receiptExpandedPlan.byRail.receipt.mode, "expanded");
assert.equal(receiptExpandedPlan.byRail.receipt.state, "urgent");
assert.equal(receiptExpandedPlan.byRail.receipt.bodyMode, "compact");
assert.equal(receiptExpandedPlan.byRail.receipt.priority, "urgent");
assert.equal(receiptExpandedPlan.byRail.receipt.capacityWeight, 2);
assert.equal(receiptExpandedPlan.capacityOverflow, false);
assert.ok(receiptExpandedPlan.summary.includes("摘要"));

console.log("Wire side panel stack plan check passed.");

function entry(slot, label, state, stateLabel, count) {
  return {
    count,
    detail: `${label} detail`,
    groupLabel: "测试",
    href: `#${slot}`,
    label,
    order: count,
    slot,
    state,
    stateLabel,
    tone: "neutral"
  };
}

function focusPlan({ visible }) {
  return {
    contextMetrics: [],
    eventCount: visible ? 2 : 0,
    metrics: [],
    nextStepLabel: visible ? "检查焦点对象。" : "选择一个对象。",
    objectId: visible ? "obj-1" : undefined,
    relationCount: visible ? 1 : 0,
    relations: [],
    routes: [],
    state: visible ? "ready" : "empty",
    stateLabel: visible ? "可操作" : "空",
    subtitle: visible ? "焦点对象" : "未选择",
    title: visible ? "已选对象" : "无焦点",
    tone: "neutral",
    visible
  };
}

function rulePlan({ state }) {
  return {
    activeLaneKey: state === "idle" ? "none" : "tasks",
    activeLaneLabel: state === "idle" ? "无" : "任务",
    lanes: [],
    metrics: [],
    nextStepLabel: state === "idle" ? "暂无规则队列。" : "处理规则队列。",
    routes: [],
    state,
    stateLabel: state === "idle" ? "空" : "阻塞",
    subtitle: "规则链测试",
    title: "规则链"
  };
}

function receipt(state) {
  return {
    clientIntentId: "intent-1",
    cmdType: "PASS",
    message: state === "failed" ? "服务端拒绝" : "服务端接受",
    state,
    stateLabel: state === "failed" ? "失败" : "已提交",
    submittedAt: 1
  };
}

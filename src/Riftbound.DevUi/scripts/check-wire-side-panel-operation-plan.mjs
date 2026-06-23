import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/wireSidePanelOperationPlan.ts");
const panelSourcePath = resolve(scriptDir, "../src/components/match/WireSidePanelOperationPanel.tsx");
const styleSourcePath = resolve(scriptDir, "../src/styles/globals.css");
const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };
new Function("exports", "module", output)(moduleShim.exports, moduleShim);
const { buildWireSidePanelOperationPlan } = moduleShim.exports;
const panelSource = readFileSync(panelSourcePath, "utf8");
const styleSource = readFileSync(styleSourcePath, "utf8");

const readyPlan = buildWireSidePanelOperationPlan({
  activeSlot: "commandCenter",
  focusPlan: focusPlan({ visible: true }),
  orchestration: orchestration(),
  prompt: {
    actionable: true,
    candidates: [{ action: "PASS", enabled: true }],
    serverFlow: { candidateCount: 2, enabledCandidateCount: 1 },
    view: { title: "主行动", type: "MAIN_ACTION" }
  },
  ruleChainPlan: ruleChainPlan({ state: "task-open" }),
  submissionGate: { canSubmit: true }
});
assert.equal(readyPlan.sections.length, 4);
assert.deepEqual(readyPlan.sections.map((section) => section.key), ["focus", "prompt", "rules", "commands"]);
assert.equal(readyPlan.activeSectionKey, "commands");
assert.equal(section(readyPlan, "focus").primarySlot, "interaction");
assert.equal(section(readyPlan, "prompt").primarySlot, "actionPrompt");
assert.equal(section(readyPlan, "rules").primarySlot, "ruleQueue");
assert.equal(section(readyPlan, "commands").primarySlot, "commandCenter");
assert.equal(section(readyPlan, "focus").routes.find((route) => route.key === "map").state, "available");
assert.equal(section(readyPlan, "prompt").state, "ready");
assert.equal(section(readyPlan, "rules").routes.find((route) => route.key === "log").state, "available");
assert.equal(section(readyPlan, "rules").routes.find((route) => route.key === "flow").slot, "serverFlow");
assert.equal(section(readyPlan, "commands").routes.find((route) => route.key === "center").slot, "commandCenter");
assert.deepEqual(
  readyPlan.sections.map((item) => item.routes.map((route) => route.slot)).flat().filter((slot, index, slots) => slots.indexOf(slot) === index).sort(),
  ["actionMap", "actionPrompt", "commandCenter", "interaction", "log", "responseCoach", "ruleQueue", "serverFlow", "timelineDetail", "turnWindow"].sort()
);
assert.match(readyPlan.summary, /可用入口/);

const blockedPromptPlan = buildWireSidePanelOperationPlan({
  activeSlot: "actionPrompt",
  focusPlan: focusPlan({ visible: false }),
  orchestration: orchestration(),
  prompt: {
    actionable: true,
    candidates: [{ action: "PLAY_CARD", enabled: false }],
    view: { title: "打出", type: "MAIN_ACTION" }
  },
  ruleChainPlan: ruleChainPlan({ state: "idle" }),
  submissionGate: { canSubmit: false }
});
assert.equal(blockedPromptPlan.state, "blocked");
assert.equal(section(blockedPromptPlan, "focus").state, "empty");
assert.equal(section(blockedPromptPlan, "prompt").state, "blocked");
assert.equal(section(blockedPromptPlan, "rules").state, "active", "event log remains a rule/event route even when rule queue is idle");

assert.match(panelSource, /data-wire-side-panel-operation-section/);
assert.match(panelSource, /data-wire-side-panel-operation-route/);
assert.match(panelSource, /wire-side-panel-operation-sections/);
assert.equal(panelSource.includes("cardNo"), false, "operation panel must not render cardNo directly");
assert.equal(panelSource.includes("effect"), false, "operation panel must not render effect text directly");
assert.match(styleSource, /\.wire-side-panel-operation-sections[\s\S]*overflow: auto/);
assert.match(styleSource, /\.wire-side-panel-operation-section-main[\s\S]*text-align: left/);

console.log("Wire side panel operation plan check passed.");

function section(plan, key) {
  return plan.sections.find((item) => item.key === key);
}

function orchestration() {
  const entries = [
    entry("commandCenter", "指挥中心", "ready", "可提交", 1, "服务端候选可提交。"),
    entry("actionMap", "操作地图", "ready", "可提交", 1, "合法操作地图只展示服务端当前候选。"),
    entry("interaction", "焦点行动", "review", "焦点", 1, "已选中公开对象。"),
    entry("actionPrompt", "行动提示", "ready", "可提交", 2, "服务端行动提示保留原始候选与提交入口。"),
    entry("responseCoach", "响应导航", "ready", "可响应", 1, "响应导航可提交服务端候选。"),
    entry("turnWindow", "窗口总览", "active", "窗口", 2, "服务端已公开当前行动窗口。"),
    entry("ruleQueue", "规则队列", "active", "规则", 2, "存在结算链、任务或触发队列。"),
    entry("serverFlow", "服务端流", "active", "服务端", 2, "使用服务端 serverFlow 作为结算与行动流程来源。"),
    entry("timelineDetail", "事件详情", "review", "详情", 1, "正在查看规则或事件详情。"),
    entry("log", "日志", "history", "日志", 3, "显示服务端事件历史。")
  ];
  return {
    activeCount: 5,
    entries,
    nextStepLabel: "提交服务端候选。",
    primarySlot: "commandCenter",
    state: "ready",
    stateLabel: "可提交",
    summary: "指挥中心 / 可提交",
    urgentCount: 1
  };
}

function entry(slot, label, state, stateLabel, count, detail) {
  return {
    count,
    detail,
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
    eventCount: visible ? 1 : 0,
    metrics: [],
    nextStepLabel: visible ? "检查服务端候选。" : "选择公开对象。",
    objectId: visible ? "unit-1" : undefined,
    relationCount: visible ? 1 : 0,
    relations: [],
    routes: [
      { key: "actions", state: visible ? "available" : "disabled" },
      { key: "rules", state: visible ? "available" : "disabled" },
      { key: "map", state: visible ? "available" : "disabled" }
    ],
    state: visible ? "ready" : "empty",
    stateLabel: visible ? "可操作" : "空",
    subtitle: visible ? "公开对象" : "未选择",
    title: visible ? "已选对象" : "无焦点",
    tone: "neutral",
    visible
  };
}

function ruleChainPlan({ state }) {
  return {
    activeLaneKey: state === "idle" ? "none" : "stack",
    activeLaneLabel: state === "idle" ? "无" : "结算链",
    detail: state === "idle" ? undefined : { id: "rule:stack:1" },
    lanes: [
      { count: state === "idle" ? 0 : 1, key: "stack", label: "结算链", state: state === "idle" ? "empty" : "active", stateLabel: state === "idle" ? "空" : "当前" },
      { count: state === "idle" ? 0 : 1, key: "tasks", label: "任务", state: state === "idle" ? "empty" : "waiting", stateLabel: state === "idle" ? "空" : "等待" }
    ],
    metrics: [],
    nextStepLabel: state === "idle" ? "暂无规则队列。" : "处理服务端规则队列。",
    routes: [],
    state,
    stateLabel: state === "idle" ? "空" : "进行中",
    subtitle: "规则链测试",
    title: "规则链"
  };
}

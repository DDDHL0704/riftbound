import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const planSource = readFileSync(resolve(scriptDir, "../src/utils/wireSidePanelStateRailPlan.ts"), "utf8");
const matchPageSource = readFileSync(resolve(scriptDir, "../src/pages/MatchPage.tsx"), "utf8");
const styleSource = readFileSync(resolve(scriptDir, "../src/styles/globals.css"), "utf8");

const output = ts.transpileModule(planSource, {
  compilerOptions: {
    esModuleInterop: true,
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

function requireShim(id) {
  throw new Error(`Unexpected wire side panel state rail plan import: ${id}`);
}

new Function("exports", "module", "require", output)(moduleShim.exports, moduleShim, requireShim);

const { buildWireSidePanelStateRailPlan } = moduleShim.exports;

const plan = buildWireSidePanelStateRailPlan({
  activeSlot: "commandCenter",
  connectionStatus: "connected",
  events: [
    { kind: "CARD_DRAWN", description: "P1 drew a card" },
    { kind: "SPELL_RESOLVED", description: "Spell resolved" }
  ],
  orchestration: {
    activeCount: 4,
    entries: [],
    nextStepLabel: "提交服务端候选。",
    primarySlot: "commandCenter",
    state: "ready",
    stateLabel: "可提交",
    summary: "指挥中心 / 可提交",
    urgentCount: 1
  },
  prompt: {
    actionable: true,
    candidates: [
      { enabled: true },
      { enabled: false }
    ],
    serverFlow: {
      candidateCount: 3,
      disabledCandidateCount: 1,
      enabledCandidateCount: 2,
      promptType: "MAIN_ACTION"
    },
    snapshotTick: 41
  },
  ruleChainPlan: {
    activeLaneKey: "tasks",
    activeLaneLabel: "任务",
    lanes: [],
    metrics: [],
    nextStepLabel: "处理任务。",
    routes: [],
    state: "task-open",
    stateLabel: "任务",
    subtitle: "规则链",
    title: "规则链"
  },
  snapshot: {
    stack: [{ id: "stack-1" }],
    tick: 42,
    timing: {
      battlefieldTasks: [{ id: "battlefield-task" }],
      pendingTaskQueue: {
        tasks: [{ id: "pending-task" }]
      },
      triggerQueue: [{ id: "trigger-1" }]
    }
  },
  submissionFeedback: {
    clientIntentId: "intent-1",
    cmdType: "PASS",
    message: "服务端接受",
    state: "sent",
    stateLabel: "已提交",
    submittedAt: 1
  },
  submissionGate: {
    canSubmit: true,
    reason: "服务端允许提交。",
    state: "ready",
    stateLabel: "可提交"
  }
});

assert.equal(plan.state, "ready");
assert.equal(plan.activeSlot, "commandCenter");
assert.equal(plan.entries.length, 10);
assert.deepEqual(plan.entries.map((entry) => entry.key), [
  "connection",
  "snapshot",
  "prompt",
  "candidates",
  "stack",
  "tasks",
  "triggers",
  "events",
  "submission",
  "receipt"
]);
assert.equal(plan.byKey.snapshot.value, "42");
assert.equal(plan.byKey.prompt.value, "MAIN_ACTION");
assert.equal(plan.byKey.prompt.detail, "prompt tick 41 / 当前可操作");
assert.equal(plan.byKey.candidates.value, "2/3");
assert.equal(plan.byKey.stack.value, "1");
assert.equal(plan.byKey.tasks.value, "2");
assert.equal(plan.byKey.triggers.value, "1");
assert.equal(plan.byKey.events.value, "2");
assert.equal(plan.byKey.submission.value, "可提交");
assert.equal(plan.byKey.receipt.value, "已提交");
assert.match(plan.summary, /tick 42/);
assert.match(plan.summary, /候选 2\/3/);
assert.match(plan.summary, /队列 4/);

const offlinePlan = buildWireSidePanelStateRailPlan({
  activeSlot: "log",
  connectionStatus: "disconnected",
  events: [],
  orchestration: {
    activeCount: 0,
    entries: [],
    nextStepLabel: "重新连接。",
    primarySlot: "overview",
    state: "offline",
    stateLabel: "离线",
    summary: "离线",
    urgentCount: 1
  },
  ruleChainPlan: {
    activeLaneKey: "none",
    activeLaneLabel: "无",
    lanes: [],
    metrics: [],
    nextStepLabel: "无规则链。",
    routes: [],
    state: "idle",
    stateLabel: "空",
    subtitle: "规则链",
    title: "规则链"
  },
  submissionGate: {
    canSubmit: false,
    reason: "连接未就绪。",
    state: "blocked",
    stateLabel: "阻断"
  }
});
assert.equal(offlinePlan.state, "offline");
assert.equal(offlinePlan.byKey.connection.state, "offline");
assert.equal(offlinePlan.byKey.snapshot.value, "无");
assert.equal(offlinePlan.byKey.prompt.value, "无");
assert.equal(offlinePlan.byKey.submission.value, "阻断");
assert.equal(offlinePlan.byKey.receipt.value, "无");

for (const snippet of [
  "buildWireSidePanelStateRailPlan",
  "const sidePanelStateRailPlan = useMemo",
  "data-wire-side-panel-state-rail",
  "data-wire-side-panel-state-summary",
  "data-wire-side-panel-state-metric",
  "data-wire-side-panel-state-key",
  "data-wire-side-panel-state-source"
]) {
  assert.ok(matchPageSource.includes(snippet), `MatchPage must include ${snippet}`);
}

assertStyleBlock(".wire-side-panel-state-rail", [
  /display: grid/,
  /grid-template-columns: repeat\(5, minmax\(0, 1fr\)\)/,
  /border: 1px solid #000/
]);
assertStyleBlock(".wire-side-panel-state-metric", [
  /min-width: 0/,
  /border: 1px solid #000/
]);

console.log("Wire side panel state rail plan check passed.");

function assertStyleBlock(selector, patterns) {
  for (const pattern of patterns) {
    assert.ok(styleBlocks(selector).some((block) => pattern.test(block)), `${selector} must satisfy ${pattern}`);
  }
}

function styleBlocks(selector) {
  const escaped = selector.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  return Array.from(styleSource.matchAll(new RegExp(`${escaped}\\s*\\{[\\s\\S]*?\\n\\}`, "g")), (match) => match[0]);
}

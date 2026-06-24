import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const layout = JSON.parse(readFileSync(resolve(scriptDir, "../src/components/match/wireTableLayoutData.json"), "utf8"));
const { buildWireSidePanelDirectoryPlan } = loadTsModule(resolve(scriptDir, "../src/utils/wireSidePanelDirectoryPlan.ts"));
const { buildWireSidePanelOrchestrationPlan } = loadTsModule(resolve(scriptDir, "../src/utils/wireSidePanelOrchestrationPlan.ts"));
const directory = buildWireSidePanelDirectoryPlan(layout.sidePanel.slots);

const stalePlan = buildWireSidePanelOrchestrationPlan({
  connectionStatus: "connected",
  directory,
  events: [],
  prompt: {
    actionable: true,
    candidates: [{ action: "PLAY_CARD", enabled: true }],
    playerId: "P1",
    snapshotTick: 8,
    view: { title: "主行动", type: "MAIN_ACTION" }
  },
  snapshot: {
    activePlayerId: "P1",
    lanes: {},
    players: {},
    stack: [],
    table: { source: "server-snapshot" },
    tick: 7,
    timing: {},
    turnNumber: 1,
    turnState: "MAIN"
  },
  submissionGate: {
    canSubmit: false,
    reason: "行动提示属于 tick 8，当前桌面快照是 tick 7。",
    state: "stale-snapshot",
    stateLabel: "等待同步"
  }
});
assert.equal(stalePlan.primarySlot, "commandCenter");
assert.equal(entry(stalePlan, "commandCenter").state, "blocked");
assert.equal(entry(stalePlan, "commandCenter").stateLabel, "等待同步");
assert.equal(entry(stalePlan, "commandCenter").detail, "行动提示属于 tick 8，当前桌面快照是 tick 7。");
assert.equal(entry(stalePlan, "actionMap").stateLabel, "等待同步");
assert.equal(entry(stalePlan, "actionPrompt").detail, "行动提示属于 tick 8，当前桌面快照是 tick 7。");

const readOnlyPromptPlan = buildWireSidePanelOrchestrationPlan({
  connectionStatus: "connected",
  directory,
  events: [],
  prompt: {
    actionable: false,
    candidates: [{ action: "PASS_PRIORITY", enabled: true, reason: "只读观察" }],
    playerId: "P2",
    snapshotTick: 11,
    view: { title: "响应观察", type: "RESPONSE" }
  },
  snapshot: {
    activePlayerId: "P1",
    lanes: {},
    players: {},
    stack: [],
    table: { source: "server-snapshot" },
    tick: 11,
    timing: {},
    turnNumber: 2,
    turnState: "ACTION"
  },
  submissionGate: {
    canSubmit: false,
    reason: "当前服务端提示为只读状态，暂不提交行动。",
    state: "read-only-prompt",
    stateLabel: "只读提示"
  }
});
assert.equal(entry(readOnlyPromptPlan, "commandCenter").state, "review");
assert.equal(entry(readOnlyPromptPlan, "commandCenter").stateLabel, "只读提示");
assert.equal(entry(readOnlyPromptPlan, "commandCenter").count, 1);
assert.equal(entry(readOnlyPromptPlan, "commandCenter").detail, "当前服务端提示为只读状态，暂不提交行动。");
assert.equal(entry(readOnlyPromptPlan, "actionMap").state, "review");
assert.equal(entry(readOnlyPromptPlan, "actionPrompt").stateLabel, "只读提示");
assert.equal(entry(readOnlyPromptPlan, "responseCoach").state, "review");
assert.equal(entry(readOnlyPromptPlan, "responseCoach").count, 1);

const readyPlan = buildWireSidePanelOrchestrationPlan({
  connectionStatus: "connected",
  directory,
  events: [{ kind: "STACK_ITEM_ADDED", description: "加入结算链", payload: {} }],
  prompt: {
    actionable: true,
    candidates: [{ action: "PASS", enabled: true }],
    playerId: "P1",
    serverFlow: { candidateCount: 1, enabledCandidateCount: 1 },
    view: { title: "主行动", type: "MAIN_ACTION" }
  },
  selectedObjectId: "unit-1",
  snapshot: {
    activePlayerId: "P1",
    lanes: {},
    players: {},
    stack: [{ effectKind: "SPELL", stackItemId: "stack-1" }],
    table: { source: "server-snapshot" },
    tick: 9,
    timing: { pendingTaskQueue: { tasks: [{ kind: "SPELL_DUEL" }] } },
    turnNumber: 1,
    turnState: "MAIN"
  },
  submissionGate: { canSubmit: true, reason: "行动提示和桌面快照同属 tick 9。", state: "connected", stateLabel: "可提交" },
  timelineDetail: { id: "rule:stack:1", source: "rule" }
});
assert.equal(readyPlan.primarySlot, "commandCenter");
assert.equal(entry(readyPlan, "serverFlow").state, "active");
assert.equal(entry(readyPlan, "ruleQueue").state, "active");
assert.equal(entry(readyPlan, "timelineDetail").state, "review");
assert.equal(entry(readyPlan, "log").state, "history");
assert.equal(readyPlan.activeCount >= 5, true);

const offlineHistoryPlan = buildWireSidePanelOrchestrationPlan({
  connectionStatus: "disconnected",
  directory,
  events: [{ kind: "PLAYER_JOINED", description: "已入座", payload: {} }],
  prompt: undefined,
  snapshot: undefined,
  submissionGate: { canSubmit: false, reason: "连接未就绪。", state: "disconnected", stateLabel: "未连接" },
  timelineDetail: { id: "event:1", source: "event" }
});
assert.equal(entry(offlineHistoryPlan, "commandCenter").state, "offline");
assert.equal(entry(offlineHistoryPlan, "log").state, "history");
assert.equal(entry(offlineHistoryPlan, "timelineDetail").state, "review");

console.log("Wire side panel orchestration plan check passed.");

function entry(plan, slot) {
  return plan.entries.find((item) => item.slot === slot);
}

function loadTsModule(sourcePath) {
  const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
  const output = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022
    }
  }).outputText;
  const moduleShim = { exports: {} };
  new Function("exports", "module", output)(moduleShim.exports, moduleShim);
  return moduleShim.exports;
}

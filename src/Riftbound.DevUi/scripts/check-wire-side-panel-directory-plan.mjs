import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const layout = JSON.parse(readFileSync(resolve(scriptDir, "../src/components/match/wireTableLayoutData.json"), "utf8"));
const { buildWireSidePanelDirectoryPlan, wireSidePanelAnchorId } = loadTsModule(resolve(scriptDir, "../src/utils/wireSidePanelDirectoryPlan.ts"));
const { buildWireSidePanelDirectoryViewPlan } = loadTsModule(resolve(scriptDir, "../src/utils/wireSidePanelDirectoryViewPlan.ts"));
const { buildWireSidePanelFramePlan } = loadTsModule(resolve(scriptDir, "../src/utils/wireSidePanelFramePlan.ts"));
const { buildWireSidePanelOrchestrationPlan } = loadTsModule(resolve(scriptDir, "../src/utils/wireSidePanelOrchestrationPlan.ts"));
const { WIRE_SIDE_PANEL_TABS } = loadTsModule(resolve(scriptDir, "../src/utils/wireSidePanelTabPlan.ts"));

const expectedSlots = [
  "overview",
  "turnWindow",
  "commandCenter",
  "serverFlow",
  "responseCoach",
  "tableAuthority",
  "informationBoundary",
  "promptAuthority",
  "actionMap",
  "interaction",
  "ruleQueue",
  "timelineDetail",
  "actionPrompt",
  "log"
];
const plan = buildWireSidePanelDirectoryPlan(layout.sidePanel.slots);

assert.deepEqual(plan.entries.map((entry) => entry.slot), expectedSlots);
assert.deepEqual(plan.entries.map((entry) => entry.order), expectedSlots.map((_, index) => index + 1));
assert.equal(new Set(plan.entries.map((entry) => entry.anchorId)).size, expectedSlots.length);
assert.equal(plan.bySlot.turnWindow.anchorId, "wire-side-panel-turnWindow");
assert.equal(plan.bySlot.log.label, "日志");
assert.deepEqual(plan.groups.map((group) => group.group), ["window", "command", "authority", "rules", "history"]);

for (const entry of plan.entries) {
  assert.equal(entry.anchorId, wireSidePanelAnchorId(entry.slot));
  assert.ok(entry.label.length > 0, `${entry.slot} must expose a label`);
  assert.ok(entry.groupLabel.length > 0, `${entry.slot} must expose a group label`);
}

assert.throws(
  () => buildWireSidePanelDirectoryPlan(["turnWindow", "turnWindow"]),
  /Duplicate wire side panel slot/
);

const readyOrchestration = buildWireSidePanelOrchestrationPlan({
  connectionStatus: "connected",
  directory: plan,
  events: [{ kind: "STACK_ITEM_ADDED", description: "加入结算链", payload: {} }],
  prompt: {
    actionable: true,
    candidates: [{ action: "PASS", enabled: true }],
    playerId: "P1",
    reason: "test",
    serverFlow: {
      actionableForPromptPlayer: true,
      candidateCount: 1,
      disabledCandidateCount: 0,
      enabledCandidateCount: 1,
      isResponsiblePlayer: true,
      lanes: [],
      nextStep: "提交 PASS",
      primaryLabel: "可行动",
      promptPlayerId: "P1",
      promptType: "MAIN_ACTION",
      queueCounts: {},
      reason: "服务端候选",
      relatedObjectIds: [],
      relatedObjects: [],
      state: "ready",
      stateLabel: "可行动",
      steps: [],
      summary: "可行动",
      tone: "good"
    },
    view: { title: "主行动", type: "MAIN_ACTION" }
  },
  selectedObjectId: "unit-1",
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
  submissionGate: { canSubmit: true, reason: "ok", state: "connected", stateLabel: "可提交" },
  timelineDetail: { id: "event:1", source: "event" }
});
assert.equal(readyOrchestration.entries.length, expectedSlots.length);
assert.equal(readyOrchestration.primarySlot, "commandCenter");
assert.equal(entry(readyOrchestration, "commandCenter").state, "ready");
assert.equal(entry(readyOrchestration, "actionMap").state, "ready");
assert.equal(entry(readyOrchestration, "serverFlow").state, "active");
assert.equal(entry(readyOrchestration, "tableAuthority").state, "audit");
assert.equal(entry(readyOrchestration, "timelineDetail").state, "review");
assert.equal(entry(readyOrchestration, "log").state, "history");
assert.ok(readyOrchestration.urgentCount >= 3);

const actionDirectoryView = buildWireSidePanelDirectoryViewPlan({
  activeSlot: "commandCenter",
  activeTab: "action",
  entries: readyOrchestration.entries,
  tabs: WIRE_SIDE_PANEL_TABS
});
assert.equal(actionDirectoryView.activeEntry.slot, "commandCenter");
assert.equal(actionDirectoryView.primaryEntry.slot, "commandCenter");
assert.deepEqual(actionDirectoryView.visibleEntries.map((item) => item.slot), ["commandCenter", "actionMap", "interaction", "actionPrompt"]);
assert.equal(actionDirectoryView.visibleEntries.find((item) => item.slot === "commandCenter").active, true);
assert.equal(actionDirectoryView.visibleEntries.find((item) => item.slot === "commandCenter").primary, true);
assert.equal(actionDirectoryView.hiddenCount, expectedSlots.length - 4);
assert.deepEqual(actionDirectoryView.tabs.map((item) => item.id), ["action", "response", "rules", "log", "detail"]);
assert.equal(actionDirectoryView.currentTab.id, "action");
assert.equal(actionDirectoryView.currentTab.active, true);
assert.ok(actionDirectoryView.tabs.find((item) => item.id === "rules").count >= 1);

const detailDirectoryView = buildWireSidePanelDirectoryViewPlan({
  activeSlot: "timelineDetail",
  activeTab: "detail",
  entries: readyOrchestration.entries,
  tabs: WIRE_SIDE_PANEL_TABS
});
assert.deepEqual(detailDirectoryView.visibleEntries.map((item) => item.slot), [
  "timelineDetail",
  "overview",
  "tableAuthority",
  "informationBoundary",
  "promptAuthority"
]);
assert.equal(detailDirectoryView.activeEntry.slot, "timelineDetail");
assert.equal(detailDirectoryView.primaryEntry.slot, "timelineDetail");

assert.throws(
  () => buildWireSidePanelDirectoryViewPlan({
    activeSlot: "commandCenter",
    activeTab: "action",
    entries: readyOrchestration.entries,
    tabs: [
      ...WIRE_SIDE_PANEL_TABS,
      { id: "duplicate", label: "重复", primarySlot: "commandCenter", slots: ["commandCenter"] }
    ]
  }),
  /Wire side panel slot appears in multiple tabs/
);
assert.throws(
  () => buildWireSidePanelDirectoryViewPlan({
    activeSlot: "commandCenter",
    activeTab: "missing",
    entries: readyOrchestration.entries,
    tabs: WIRE_SIDE_PANEL_TABS
  }),
  /Active wire side panel tab is not registered/
);

const commandFrame = buildWireSidePanelFramePlan({
  activeSlot: "commandCenter",
  slots: layout.sidePanel.slots
});
assert.deepEqual(commandFrame.entries.map((item) => item.slot), expectedSlots);
assert.deepEqual(commandFrame.persistentSlots, ["serverFlow"]);
assert.ok(commandFrame.mainSlots.includes("commandCenter"));
assert.ok(!commandFrame.mainSlots.includes("serverFlow"));
assert.deepEqual(commandFrame.visibleSlots, ["commandCenter", "serverFlow"]);
assert.equal(frameEntry(commandFrame, "commandCenter").active, true);
assert.equal(frameEntry(commandFrame, "commandCenter").region, "main");
assert.equal(frameEntry(commandFrame, "commandCenter").ariaHidden, false);
assert.equal(frameEntry(commandFrame, "serverFlow").active, false);
assert.equal(frameEntry(commandFrame, "serverFlow").region, "persistent");
assert.equal(frameEntry(commandFrame, "serverFlow").ariaHidden, false);
assert.equal(frameEntry(commandFrame, "log").ariaHidden, true);

const serverFlowFrame = buildWireSidePanelFramePlan({
  activeSlot: "serverFlow",
  slots: layout.sidePanel.slots
});
assert.deepEqual(serverFlowFrame.visibleSlots, ["serverFlow"]);
assert.equal(frameEntry(serverFlowFrame, "serverFlow").active, true);
assert.equal(frameEntry(serverFlowFrame, "serverFlow").ariaHidden, false);

assert.throws(
  () => buildWireSidePanelFramePlan({
    activeSlot: "commandCenter",
    persistentSlots: ["serverFlow", "serverFlow"],
    slots: layout.sidePanel.slots
  }),
  /Duplicate persistent side panel slot/
);
assert.throws(
  () => buildWireSidePanelFramePlan({
    activeSlot: "commandCenter",
    persistentSlots: ["missingSlot"],
    slots: layout.sidePanel.slots
  }),
  /Persistent side panel slot is not in layout/
);

const offlineOrchestration = buildWireSidePanelOrchestrationPlan({
  connectionStatus: "disconnected",
  directory: plan,
  events: [],
  prompt: undefined,
  snapshot: undefined,
  submissionGate: { canSubmit: false, reason: "offline", state: "disconnected", stateLabel: "未连接" }
});
assert.equal(offlineOrchestration.state, "offline");
assert.equal(entry(offlineOrchestration, "commandCenter").state, "offline");
assert.equal(entry(offlineOrchestration, "log").state, "offline");

console.log("Wire side panel directory plan check passed.");

function entry(orchestration, slot) {
  return orchestration.entries.find((item) => item.slot === slot);
}

function frameEntry(plan, slot) {
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

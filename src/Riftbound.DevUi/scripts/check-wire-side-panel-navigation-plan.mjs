import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const { WIRE_SIDE_PANEL_TABS } = loadTsModule(resolve(scriptDir, "../src/utils/wireSidePanelTabPlan.ts"));
const {
  buildWireSidePanelTransitionPlan,
  isStickyWireSidePanelState,
  preferredWireSidePanelSlotForTab
} = loadTsModule(resolve(scriptDir, "../src/utils/wireSidePanelNavigationPlan.ts"));

const slots = [
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
const entries = slots.map((slot, index) => ({
  count: slot === "actionMap" ? 2 : slot === "ruleQueue" ? 4 : 0,
  detail: `${slot} detail`,
  groupLabel: "测试",
  href: `#wire-side-panel-${slot}`,
  label: slot,
  order: index + 1,
  slot,
  state: stateFor(slot),
  stateLabel: stateFor(slot),
  tone: "neutral"
}));
const bySlot = Object.fromEntries(entries.map((entry) => [entry.slot, entry]));

assert.equal(isStickyWireSidePanelState("ready"), true);
assert.equal(isStickyWireSidePanelState("review"), true);
assert.equal(isStickyWireSidePanelState("waiting"), false);

const actionTab = WIRE_SIDE_PANEL_TABS.find((tab) => tab.id === "action");
const detailTab = WIRE_SIDE_PANEL_TABS.find((tab) => tab.id === "detail");
assert.equal(preferredWireSidePanelSlotForTab(actionTab, bySlot, "ruleQueue"), "actionMap");
assert.equal(preferredWireSidePanelSlotForTab(detailTab, bySlot, "commandCenter"), "timelineDetail");
assert.equal(preferredWireSidePanelSlotForTab(actionTab, bySlot, "commandCenter"), "commandCenter");

const tabTransition = transition({ activeSlot: "commandCenter", source: "tab", targetTab: "rules" });
assert.equal(tabTransition.fromSlot, "commandCenter");
assert.equal(tabTransition.fromTab, "action");
assert.equal(tabTransition.targetSlot, "ruleQueue");
assert.equal(tabTransition.targetTab, "rules");
assert.equal(tabTransition.tabChanges, true);
assert.equal(tabTransition.selectable, true);
assert.equal(tabTransition.actionLabel, "切换");
assert.ok(tabTransition.reason.includes("行动 -> 规则"));

const sameTabTransition = transition({ activeSlot: "commandCenter", source: "directory", targetSlot: "actionMap" });
assert.equal(sameTabTransition.targetTab, "action");
assert.equal(sameTabTransition.tabChanges, false);
assert.equal(sameTabTransition.selectable, true);
assert.equal(sameTabTransition.actionLabel, "转到");

const currentTransition = transition({ activeSlot: "commandCenter", source: "control-route", targetSlot: "commandCenter" });
assert.equal(currentTransition.alreadyActive, true);
assert.equal(currentTransition.selectable, false);
assert.equal(currentTransition.actionLabel, "当前");

const railTransition = transition({ activeSlot: "commandCenter", source: "rail", targetSlot: "ruleQueue" });
assert.equal(railTransition.source, "rail");
assert.equal(railTransition.targetTab, "rules");
assert.equal(railTransition.tabChanges, true);

assert.throws(
  () => transition({ activeSlot: "commandCenter", source: "tab", targetTab: "missing" }),
  /navigation tab is not registered/
);
assert.throws(
  () => transition({ activeSlot: "commandCenter", source: "tab", targetTab: "rules", targetSlot: "actionMap" }),
  /does not belong to requested tab/
);
assert.throws(
  () => buildWireSidePanelTransitionPlan({
    activeSlot: "commandCenter",
    entries,
    primarySlot: "commandCenter",
    source: "directory",
    tabs: [...WIRE_SIDE_PANEL_TABS, { id: "dup", label: "重复", primarySlot: "commandCenter", slots: ["commandCenter"] }],
    targetSlot: "actionMap"
  }),
  /navigation slot appears in multiple tabs/
);

console.log("Wire side panel navigation plan check passed.");

function transition({ activeSlot, source, targetSlot, targetTab }) {
  return buildWireSidePanelTransitionPlan({
    activeSlot,
    entries,
    primarySlot: "commandCenter",
    source,
    tabs: WIRE_SIDE_PANEL_TABS,
    targetSlot,
    targetTab
  });
}

function stateFor(slot) {
  switch (slot) {
    case "actionMap":
    case "ruleQueue":
      return "active";
    case "timelineDetail":
      return "review";
    default:
      return "waiting";
  }
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

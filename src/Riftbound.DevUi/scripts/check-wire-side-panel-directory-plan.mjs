import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const layout = JSON.parse(readFileSync(resolve(scriptDir, "../src/components/match/wireTableLayoutData.json"), "utf8"));
const { buildWireSidePanelDirectoryPlan, wireSidePanelAnchorId } = loadTsModule(resolve(scriptDir, "../src/utils/wireSidePanelDirectoryPlan.ts"));

const expectedSlots = [
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

console.log("Wire side panel directory plan check passed.");

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

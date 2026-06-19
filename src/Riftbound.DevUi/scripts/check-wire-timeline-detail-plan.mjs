import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const stripImports = (value) => value.replace(/^import[\s\S]*?;\n/gm, "");
const transpile = (value) => ts.transpileModule(stripImports(value), {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;

const commandFieldDisplayPath = resolve(scriptDir, "../src/utils/commandFieldDisplay.ts");
const commandFieldDisplayOutput = transpile(readFileSync(commandFieldDisplayPath, "utf8"));
const commandFieldModuleShim = { exports: {} };
new Function("exports", "module", commandFieldDisplayOutput)(commandFieldModuleShim.exports, commandFieldModuleShim);

const candidateSemanticsPath = resolve(scriptDir, "../src/utils/promptCandidateSemantics.ts");
const candidateSemanticsOutput = transpile(readFileSync(candidateSemanticsPath, "utf8"));
const candidateSemanticsModuleShim = { exports: {} };
new Function(
  "exports",
  "module",
  "commandFieldDisplayLabel",
  candidateSemanticsOutput
)(
  candidateSemanticsModuleShim.exports,
  candidateSemanticsModuleShim,
  commandFieldModuleShim.exports.commandFieldDisplayLabel
);

const sourcePath = resolve(scriptDir, "../src/utils/wireTimelineDetailPlan.ts");
const source = readFileSync(sourcePath, "utf8");
const output = transpile(source);
const moduleShim = { exports: {} };

new Function(
  "exports",
  "module",
  "summarizePromptCandidateSemantics",
  output
)(
  moduleShim.exports,
  moduleShim,
  candidateSemanticsModuleShim.exports.summarizePromptCandidateSemantics
);

const { buildWireTimelineDetailPlan } = moduleShim.exports;
const plan = buildWireTimelineDetailPlan({
  detail: {
    id: "rule:stack:fixture-stack-1",
    lines: [{ label: "来源", value: "闪电" }],
    refs: [
      { id: "source-1", role: "来源" },
      { id: "target-1", label: "目标牌", role: "目标" },
      { id: "missing-1", role: "目标" },
      { id: "HIDDEN", role: "隐藏来源" }
    ],
    source: "rule",
    subtitle: "法术",
    title: "结算链项目"
  },
  objectIndex: {
    "source-1": { objectId: "source-1", cardNo: "OGN-001/298" },
    "target-1": { objectId: "target-1", cardNo: "SFD-001/221" }
  },
  objectContextById: {
    "source-1": {
      candidateLinks: [
        {
          commandFields: ["来源:sourceObjectId*"],
          commandType: "PLAY_CARD",
          enabled: true,
          label: "打出卡牌",
          reason: "",
          requiredCommandFields: ["来源:sourceObjectId*"],
          roles: ["来源"]
        }
      ],
      eventLinks: [],
      objectId: "source-1",
      promptDisabledCount: 0,
      promptEnabledCount: 1,
      stackRoles: [],
      stateLabels: [],
      zone: { kind: "hand", label: "我方手牌" }
    },
    "target-1": {
      candidateLinks: [
        {
          commandFields: ["目标:targetObjectIds*"],
          commandType: "CHOOSE_TARGET",
          enabled: false,
          label: "选择目标",
          reason: "等待来源",
          requiredCommandFields: ["目标:targetObjectIds*"],
          roles: ["目标"]
        }
      ],
      eventLinks: [],
      objectId: "target-1",
      promptDisabledCount: 1,
      promptEnabledCount: 0,
      stackRoles: [],
      stateLabels: [],
      zone: { kind: "battlefield", label: "右战场 / 对方单位" }
    }
  },
  selectedObjectContext: {
    candidateLinks: [],
    eventLinks: [],
    objectId: "source-1",
    promptDisabledCount: 0,
    promptEnabledCount: 1,
    stackRoles: [],
    stateLabels: [],
    zone: { kind: "hand", label: "我方手牌" }
  },
  selectedObjectId: "source-1"
});

assert.equal(plan.headerTitle, "结算链项目");
assert.equal(plan.statusCards[0].value, "规则队列");
assert.equal(plan.statusCards[1].value, "2 / 4 可定位");
assert.equal(plan.statusCards[2].value, "我方手牌");
assert.equal(plan.statusCards[3].value, "已命中详情对象");
assert.equal(plan.statusCards[4].value, "1 可用 / 1 阻断");
assert.equal(plan.inspector.sourceLabel, "规则队列");
assert.equal(plan.inspector.visibleRefCount, 2);
assert.equal(plan.inspector.selectedProjectionCount, 1);
assert.equal(plan.inspector.hiddenRefCount, 1);
assert.equal(plan.inspector.missingRefCount, 1);
assert.equal(plan.inspector.actionCandidateCount, 2);
assert.equal(plan.inspector.projectionRows.find((row) => row.key === "selected")?.count, 1);
assert.equal(plan.inspector.projectionRows.find((row) => row.key === "visible")?.count, 1);
assert.equal(plan.inspector.candidateRows[0].label, "OGN-001/298");
assert.equal(plan.navigationRows.length, 4);
assert.deepEqual(plan.navigationRows.map((row) => row.focusState), ["selected", "focusable", "missing", "hidden"]);
assert.deepEqual(plan.navigationRows.map((row) => row.projectionState), ["selected", "visible", "missing", "hidden"]);
assert.equal(plan.navigationRows[0].objectId, "source-1");
assert.equal(plan.navigationRows[0].zoneLabel, "我方手牌");
assert.equal(plan.navigationRows[0].actionState, "available");
assert.equal(plan.navigationRows[0].actionLabel, "1 可用");
assert.equal(plan.navigationRows[1].objectId, "target-1");
assert.equal(plan.navigationRows[1].zoneLabel, "右战场 / 对方单位");
assert.equal(plan.navigationRows[1].actionState, "blocked");
assert.equal(plan.navigationRows[1].actionLabel, "1 阻断");
assert.equal(plan.navigationRows[2].objectId, undefined);
assert.equal(plan.navigationRows[2].zoneLabel, "未公开");
assert.equal(plan.navigationRows[2].actionState, "none");
assert.equal(plan.navigationRows[3].objectId, undefined);
assert.equal(plan.navigationRows[3].zoneLabel, "隐藏");
assert.deepEqual(plan.projectionRows.map((row) => row.state), ["selected", "visible", "missing", "hidden"]);
assert.equal(JSON.stringify(plan).includes("missing-1"), true);
assert.equal(plan.projectionRows.find((row) => row.state === "missing")?.label, "未公开对象");
assert.equal(plan.projectionRows.find((row) => row.state === "hidden")?.label, "隐藏对象");
assert.equal(plan.actionHintRows.length, 2);
assert.equal(plan.actionHintRows[0].objectId, "source-1");
assert.equal(plan.actionHintRows[0].commandTypes[0], "PLAY_CARD");
assert.deepEqual(plan.actionHintRows[0].selectionRoleLabels, ["来源"]);
assert.deepEqual(plan.actionHintRows[0].commandFieldLabels, ["来源:sourceObjectId*"]);
assert.deepEqual(plan.actionHintRows[0].requiredCommandFieldLabels, ["来源:sourceObjectId*"]);
assert.equal(plan.actionHintRows[1].reasonLabels[0], "等待来源");
assert.deepEqual(plan.actionHintRows[1].selectionRoleLabels, ["目标"]);
assert.deepEqual(plan.actionHintRows[1].requiredCommandFieldLabels, ["目标:targetObjectIds*"]);

console.log("Wire timeline detail plan check passed.");

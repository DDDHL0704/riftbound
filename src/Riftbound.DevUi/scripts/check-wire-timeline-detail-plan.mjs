import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/wireTimelineDetailPlan.ts");
const source = readFileSync(sourcePath, "utf8");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function("exports", "module", output)(moduleShim.exports, moduleShim);

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

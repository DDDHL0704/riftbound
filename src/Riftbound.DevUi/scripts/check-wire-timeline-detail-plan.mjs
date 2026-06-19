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
assert.deepEqual(plan.projectionRows.map((row) => row.state), ["selected", "visible", "missing", "hidden"]);
assert.equal(JSON.stringify(plan).includes("missing-1"), true);
assert.equal(plan.projectionRows.find((row) => row.state === "missing")?.label, "未公开对象");
assert.equal(plan.projectionRows.find((row) => row.state === "hidden")?.label, "隐藏对象");

console.log("Wire timeline detail plan check passed.");

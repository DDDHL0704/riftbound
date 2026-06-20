import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/eventLogPlan.ts");
const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function(
  "exports",
  "module",
  "asArray",
  "asRecord",
  "errorCodeLabel",
  "errorMessageLabel",
  "redactInternalText",
  output
)(
  moduleShim.exports,
  moduleShim,
  asArray,
  asRecord,
  errorCodeLabel,
  errorMessageLabel,
  redactInternalText
);

const { buildEventLogPlan, eventDescriptionLabel, eventKindLabel } = moduleShim.exports;

const emptyPlan = buildEventLogPlan({
  errors: [],
  events: []
});

assert.equal(emptyPlan.state, "empty");
assert.equal(emptyPlan.emptyLabel, "暂无服务端事件。");
assert.equal(emptyPlan.visibleEventCount, 0);

const compactEvents = Array.from({ length: 14 }, (_, index) => ({
  description: `事件 ${index}`,
  kind: index === 13 ? "BATTLEFIELD_CONQUERED" : "MATCH_STARTED",
  payload: { sourceObjectId: "unit-1" }
}));
const compactPlan = buildEventLogPlan({
  density: "compact",
  errors: [],
  events: compactEvents,
  objectIndex: { "unit-1": {} }
});

assert.equal(compactPlan.state, "events");
assert.equal(compactPlan.hiddenEventCount, 2);
assert.equal(compactPlan.visibleEventCount, 12);
assert.equal(compactPlan.events[0].detail.id, "event:MATCH_STARTED:2");
assert.equal(compactPlan.events.at(-1).title, "征服战场");
assert.equal(compactPlan.events.at(-1).refs[0].role, "来源");

const serverRefPlan = buildEventLogPlan({
  errors: [],
  events: [
    {
      description: "serverPaymentState should be hidden",
      kind: "CARD_PLAYED",
      objectRefs: [{ cardNo: "CH-001", objectId: "unit-1", role: "来源" }],
      payload: { targetObjectId: "unit-2" }
    }
  ],
  objectIndex: { "unit-1": {}, "unit-2": {} }
});

assert.equal(serverRefPlan.events[0].refs.length, 1);
assert.equal(serverRefPlan.events[0].refs[0].label, "CH-001");
assert.equal(serverRefPlan.events[0].refs[0].visibility, "visible");
assert.equal(serverRefPlan.events[0].detail.lines.find((line) => line.label === "对象来源").value, "服务端摘要");
assert.equal(serverRefPlan.events[0].detail.lines.find((line) => line.label === "引用边界").value, "可见 1");
assert.equal(serverRefPlan.events[0].description.includes("serverPaymentState"), false);

const hiddenRefPlan = buildEventLogPlan({
  errors: [],
  events: [
    {
      description: "face-down source resolves without revealing card identity",
      kind: "TRIGGER_RESOLVED",
      objectRefs: [{ isFaceDown: true, isHidden: true, objectId: "hidden-standby", role: "来源" }],
      payload: {}
    }
  ],
  objectIndex: { "hidden-standby": { isFaceDown: true } }
});

assert.equal(hiddenRefPlan.events[0].refs[0].label, "隐藏对象");
assert.equal(hiddenRefPlan.events[0].refs[0].visibility, "hidden");
assert.equal(hiddenRefPlan.events[0].detail.lines.find((line) => line.label === "引用边界").value, "可见 0 / 隐藏 1");

const missingRefPlan = buildEventLogPlan({
  errors: [],
  events: [
    {
      description: "server referenced an object outside current snapshot",
      kind: "UNIT_DESTROYED",
      objectRefs: [{ objectId: "missing-object", role: "被摧毁" }],
      payload: {}
    }
  ],
  objectIndex: {}
});

assert.equal(missingRefPlan.events[0].refs[0].visibility, "missing");
assert.equal(missingRefPlan.events[0].detail.lines.find((line) => line.label === "引用边界").value, "可见 0 / 隐藏 0 / 缺失 1");

const fallbackRefPlan = buildEventLogPlan({
  errors: [],
  events: [
    {
      description: "",
      kind: "DAMAGE_APPLIED",
      payload: { nested: { targetObjectIds: ["unit-2", "missing"] } }
    }
  ],
  objectIndex: { "unit-2": {} }
});

assert.equal(fallbackRefPlan.events[0].refs.length, 1);
assert.equal(fallbackRefPlan.events[0].refs[0].id, "unit-2");
assert.equal(fallbackRefPlan.events[0].refs[0].visibility, "visible");
assert.equal(fallbackRefPlan.events[0].detail.lines.find((line) => line.label === "对象来源").value, "事件字段");
assert.equal(fallbackRefPlan.events[0].description, "造成伤害");

const mixedPlan = buildEventLogPlan({
  errors: [{ code: "BAD_COMMAND", message: "invalid payload" }],
  events: compactEvents.slice(0, 1)
});

assert.equal(mixedPlan.state, "mixed");
assert.equal(mixedPlan.errorCount, 1);
assert.equal(mixedPlan.errors[0].title, "错误：BAD_COMMAND");
assert.equal(mixedPlan.errors[0].message, "错误消息：invalid payload");

assert.equal(eventKindLabel("UNKNOWN_EVENT"), "服务端事件");
assert.equal(eventDescriptionLabel({ description: "", kind: "DEV_SCENARIO_SEEDED", payload: {} }), "测试状态已载入");

console.log("Event log plan check passed.");

function asArray(value) {
  return Array.isArray(value) ? value : [];
}

function asRecord(value) {
  return value && typeof value === "object" && !Array.isArray(value) ? value : {};
}

function errorCodeLabel(code) {
  return `错误：${code}`;
}

function errorMessageLabel(error) {
  return `错误消息：${error.message}`;
}

function redactInternalText(value) {
  return String(value).replace(/serverPaymentState/g, "服务端字段");
}

import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/components/match/wireRuleAuthorityPlan.ts");
const source = readFileSync(sourcePath, "utf8");
const output = ts.transpileModule(source, {
  compilerOptions: {
    esModuleInterop: true,
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

function requireShim(id) {
  if (id === "../../utils/collections") {
    return {
      asArray(value) {
        return Array.isArray(value) ? value : [];
      },
      asRecord(value) {
        return value && typeof value === "object" && !Array.isArray(value) ? value : {};
      },
      asString(value, fallback = "") {
        return typeof value === "string" && value.trim().length > 0 ? value : fallback;
      }
    };
  }

  throw new Error(`Unexpected wire rule authority import: ${id}`);
}

new Function("exports", "module", "require", output)(moduleShim.exports, moduleShim, requireShim);
const { buildWireRuleAuthorityPlan } = moduleShim.exports;

const serverPlan = buildWireRuleAuthorityPlan({
  events: [
    event("STACK_ITEM_ADDED", [{ objectId: "spell-1", role: "来源" }, { objectId: "unit-1", role: "目标" }]),
    event("BATTLEFIELD_CONTROL_RESOLVED", [{ objectId: "battlefield-1", role: "战场" }])
  ],
  snapshot: snapshot({
    resolutions: true,
    stack: true,
    tasks: true,
    triggers: true
  })
});
assert.equal(serverPlan.state, "server");
assert.equal(serverPlan.issueCount, 0);
assert.deepEqual(serverPlan.metrics.map((metric) => metric.value), ["1", "1", "1", "2", "3", "0"]);
assert.equal(serverPlan.rows.find((row) => row.key === "task").stateLabel, "结构完整");
assert.equal(serverPlan.rows.find((row) => row.key === "eventRefs").stateLabel, "服务端 objectRefs");

const mixedPlan = buildWireRuleAuthorityPlan({
  events: [{
    description: "payload-only event",
    kind: "BATTLEFIELD_CONTROL_RESOLVED",
    payload: { battlefieldObjectId: "battlefield-1", participantObjectIds: ["unit-1"] }
  }],
  snapshot: snapshot({
    stack: [{ effectKind: "SPELL", stackItemId: "stack-1" }],
    tasks: [{ kind: "START_BATTLE", taskId: "task-1" }],
    triggers: [{ effectKind: "TRIGGER", triggerId: "trigger-1" }],
    resolutions: [{ kind: "HELD", resolutionId: "resolution-1" }]
  })
});
assert.equal(mixedPlan.state, "mixed");
assert.equal(mixedPlan.rows.find((row) => row.key === "stack").state, "mixed");
assert.equal(mixedPlan.rows.find((row) => row.key === "task").state, "mixed");
assert.equal(mixedPlan.rows.find((row) => row.key === "trigger").state, "mixed");
assert.equal(mixedPlan.rows.find((row) => row.key === "resolution").state, "mixed");
assert.equal(mixedPlan.rows.find((row) => row.key === "eventRefs").state, "mixed");
assert.match(mixedPlan.summary, /继续补齐/);

const fallbackEventPlan = buildWireRuleAuthorityPlan({
  events: [{ description: "no object refs", kind: "INFO", payload: { message: "无对象" } }],
  snapshot: snapshot({})
});
assert.equal(fallbackEventPlan.state, "mixed");
assert.equal(fallbackEventPlan.rows.find((row) => row.key === "eventRefs").state, "fallback");

const emptyServerPlan = buildWireRuleAuthorityPlan({
  events: [],
  snapshot: snapshot({})
});
assert.equal(emptyServerPlan.state, "server");
assert.equal(emptyServerPlan.rows.find((row) => row.key === "stack").stateLabel, "服务端空链");
assert.equal(emptyServerPlan.rows.find((row) => row.key === "task").stateLabel, "服务端空队列");

const missingPlan = buildWireRuleAuthorityPlan({});
assert.equal(missingPlan.state, "missing");
assert.equal(missingPlan.rows.find((row) => row.key === "snapshot").state, "missing");

console.log("Wire rule authority plan check passed.");

function snapshot({
  resolutions = false,
  stack = false,
  tasks = false,
  triggers = false
}) {
  return {
    activePlayerId: "P1",
    lanes: {},
    players: {},
    stack: stack === true ? [{
      effectKind: "SPELL",
      sourceObjectId: "spell-1",
      stackItemId: "stack-1",
      targetObjectIds: ["unit-1"]
    }] : Array.isArray(stack) ? stack : [],
    tick: 12,
    timing: {
      battleResolutions: resolutions === true ? [{
        battlefieldId: "battlefield-1",
        kind: "NO_RESULT",
        resolutionId: "battle-resolution-1",
        tick: 11
      }] : Array.isArray(resolutions) ? resolutions : [],
      battlefieldResolutions: resolutions === true ? [{
        battlefieldObjectId: "battlefield-1",
        kind: "HELD",
        resolutionId: "battlefield-resolution-1",
        tick: 10
      }] : [],
      pendingTaskQueue: {
        activeTaskId: tasks ? "task-1" : undefined,
        isBlocking: Boolean(tasks),
        tasks: tasks === true ? [{
          kind: "START_BATTLE",
          status: "READY",
          taskId: "task-1"
        }] : Array.isArray(tasks) ? tasks : []
      },
      triggerQueue: triggers === true ? [{
        effectKind: "TRIGGER",
        sourceObjectId: "unit-1",
        triggerId: "trigger-1"
      }] : Array.isArray(triggers) ? triggers : []
    },
    turnNumber: 1,
    turnState: "MAIN"
  };
}

function event(kind, refs) {
  return {
    description: kind,
    kind,
    objectRefs: refs,
    payload: {}
  };
}

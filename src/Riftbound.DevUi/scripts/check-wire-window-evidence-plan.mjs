import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/wireWindowEvidencePlan.ts");
const source = readFileSync(sourcePath, "utf8");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function("exports", "module", output)(moduleShim.exports, moduleShim);

const { buildWireWindowEvidencePlan } = moduleShim.exports;

const baseSnapshot = {
  activePlayerId: "P1",
  lanes: {},
  players: {},
  stack: [],
  tick: 1,
  timing: {
    phase: "MAIN",
    roomStatus: "IN_PROGRESS",
    turnWindow: { actingPlayerId: "P1", state: "NEUTRAL_OPEN" }
  },
  turnNumber: 2,
  turnState: "MAIN"
};

const mainAction = buildWireWindowEvidencePlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: {
    actionable: true,
    actions: ["PLAY_CARD"],
    candidates: [{ action: "PLAY_CARD", enabled: true, label: "打出卡牌", reason: "可提交" }],
    playerId: "P1",
    reason: "主行动窗口",
    view: { message: "请选择行动", title: "主行动窗口", type: "MAIN_ACTION" }
  },
  snapshot: baseSnapshot
});

assert.equal(mainAction.headline, "你的服务端候选窗口");
assert.equal(row(mainAction, "prompt").state, "mine");
assert.equal(row(mainAction, "priority").mine, true);
assert.equal(row(mainAction, "stack").value, "空");

const taskWindow = buildWireWindowEvidencePlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: { actionable: false, actions: ["WAIT"], candidates: [], playerId: "P1", reason: "等待任务" },
  snapshot: {
    ...baseSnapshot,
    timing: {
      ...baseSnapshot.timing,
      pendingTaskQueue: {
        isBlocking: true,
        tasks: [{ kind: "BATTLEFIELD_CONTESTED", taskId: "task-1" }]
      },
      triggerQueue: [{ effectKind: "TRIGGER_A", triggerId: "trigger-1" }]
    }
  }
});

assert.ok(taskWindow.headline.includes("规则任务"));
assert.equal(row(taskWindow, "tasks").state, "active");
assert.ok(row(taskWindow, "tasks").value.includes("BATTLEFIELD_CONTESTED"));
assert.equal(row(taskWindow, "triggers").state, "active");

const stackResponse = buildWireWindowEvidencePlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: { actionable: false, actions: ["WAIT"], candidates: [], playerId: "P2", reason: "等待响应" },
  snapshot: {
    ...baseSnapshot,
    stack: [{ cardNo: "OGN-001", effectKind: "SPELL_EFFECT", sourceObjectId: "source-1", stackItemId: "stack-1" }]
  }
});

assert.ok(stackResponse.headline.includes("结算链"));
assert.equal(row(stackResponse, "stack").state, "active");
assert.ok(row(stackResponse, "stack").value.includes("SPELL_EFFECT"));

const spellDuel = buildWireWindowEvidencePlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: { actionable: true, actions: ["PASS_FOCUS"], candidates: [], playerId: "P2", reason: "焦点响应" },
  snapshot: {
    ...baseSnapshot,
    timing: {
      ...baseSnapshot.timing,
      spellDuel: { focusPlayerId: "P1", spellDuelId: "duel-1" }
    }
  }
});

assert.ok(spellDuel.headline.includes("法术对决"));
assert.equal(row(spellDuel, "spell-duel").state, "mine");
assert.equal(row(spellDuel, "spell-duel").mine, true);

const battle = buildWireWindowEvidencePlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: { actionable: true, actions: ["ASSIGN_COMBAT_DAMAGE"], candidates: [], playerId: "P1", reason: "战斗" },
  snapshot: {
    ...baseSnapshot,
    timing: {
      ...baseSnapshot.timing,
      battle: { battleId: "battle-1", battlefieldObjectId: "battlefield-1" }
    }
  }
});

assert.ok(battle.headline.includes("战斗证据"));
assert.equal(row(battle, "battle").state, "active");
assert.ok(row(battle, "battle").value.includes("battlefield-1"));

const disconnected = buildWireWindowEvidencePlan({
  connectionStatus: "reconnecting",
  playerId: "P1",
  snapshot: baseSnapshot
});

assert.ok(disconnected.headline.includes("连接证据"));
assert.equal(disconnected.rows.length, 7);

console.log("Wire window evidence plan check passed.");

function row(plan, key) {
  return plan.rows.find((candidate) => candidate.key === key);
}

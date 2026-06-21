import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/wirePriorityRailPlan.ts");
const source = readFileSync(sourcePath, "utf8");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

const promptCandidateCountsModule = {
  promptCandidateCounts(prompt) {
    const candidates = prompt?.candidates ?? [];
    const enabledFallback = candidates.filter((candidate) => candidate.enabled).length;
    const total = finiteCount(prompt?.serverFlow?.candidateCount) ?? candidates.length;
    const enabled = finiteCount(prompt?.serverFlow?.enabledCandidateCount) ?? enabledFallback;
    const disabled = finiteCount(prompt?.serverFlow?.disabledCandidateCount)
      ?? (prompt?.serverFlow ? Math.max(0, total - enabled) : candidates.filter((candidate) => !candidate.enabled).length);
    return {
      candidateCount: total,
      disabledCandidateCount: disabled,
      enabledCandidateCount: enabled,
      source: prompt?.serverFlow ? "server-flow" : "candidates"
    };
  }
};

function finiteCount(value) {
  return typeof value === "number" && Number.isFinite(value) ? Math.max(0, Math.floor(value)) : undefined;
}

new Function("exports", "module", "require", output)(
  moduleShim.exports,
  moduleShim,
  (id) => {
    if (id === "./promptCandidateCounts") {
      return promptCandidateCountsModule;
    }

    throw new Error(`Unexpected priority rail import: ${id}`);
  }
);

const { buildWirePriorityRailPlan } = moduleShim.exports;

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

const mainAction = buildWirePriorityRailPlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: {
    actionable: true,
    actions: ["PLAY_CARD"],
    candidates: [
      { action: "PLAY_CARD", enabled: true, label: "打出卡牌", reason: "可提交" },
      { action: "END_TURN", enabled: true, label: "结束回合", reason: "可提交" }
    ],
    playerId: "P1",
    reason: "主行动窗口",
    serverFlow: { candidateCount: 5, disabledCandidateCount: 2, enabledCandidateCount: 3 },
    view: { message: "请选择行动", title: "主行动窗口", type: "MAIN_ACTION" }
  },
  snapshot: baseSnapshot
});

assert.equal(mainAction.mode, "main-action");
assert.equal(mainAction.activeStepKey, "entry");
assert.ok(mainAction.steps.some((step) => step.key === "entry" && step.mine && step.value.includes("3")));
assert.ok(mainAction.nextInteractionLabel.includes("服务端候选"));
assert.ok(mainAction.nextInteractionLabel.includes("3 项"));

const stackResponse = buildWirePriorityRailPlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: {
    actionable: false,
    actions: ["WAIT"],
    candidates: [],
    playerId: "P2",
    reason: "等待响应"
  },
  snapshot: {
    ...baseSnapshot,
    stack: [{ stackItemId: "stack-1", controllerId: "P2" }],
    timing: {
      ...baseSnapshot.timing,
      turnWindow: { actingPlayerId: "P2", state: "NEUTRAL_CLOSED" }
    }
  }
});

assert.equal(stackResponse.mode, "stack-response");
assert.equal(stackResponse.activeStepKey, "focus");
assert.ok(stackResponse.steps.some((step) => step.key === "focus" && step.value.includes("结算链")));

const spellDuel = buildWirePriorityRailPlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: {
    actionable: true,
    actions: ["PASS_FOCUS"],
    candidates: [{ action: "PASS_FOCUS", enabled: true, label: "让过焦点", reason: "可提交" }],
    playerId: "P2",
    reason: "法术对决焦点",
    view: { message: "焦点响应", title: "法术对决", type: "SPELL_DUEL_FOCUS" }
  },
  snapshot: {
    ...baseSnapshot,
    timing: {
      ...baseSnapshot.timing,
      spellDuel: { focusPlayerId: "P2", spellDuelId: "duel-1" },
      turnWindow: { actingPlayerId: "P2", isSpellDuel: true, state: "SPELL_DUEL_OPEN" }
    }
  }
});

assert.equal(spellDuel.mode, "spell-duel");
assert.equal(spellDuel.activeStepKey, "focus");
assert.ok(spellDuel.steps.some((step) => step.key === "focus" && step.value.includes("P2")));

const battlefieldTask = buildWirePriorityRailPlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: {
    actionable: false,
    actions: ["WAIT"],
    candidates: [],
    playerId: "P1",
    reason: "等待规则任务"
  },
  snapshot: {
    ...baseSnapshot,
    timing: {
      ...baseSnapshot.timing,
      pendingTaskQueue: {
        isBlocking: true,
        phase: "BATTLEFIELD_TASKS",
        tasks: [{ kind: "BATTLEFIELD_CONTESTED", reason: "BATTLEFIELD_CONTESTED", taskId: "task-1" }]
      }
    }
  }
});

assert.equal(battlefieldTask.mode, "battlefield-task");
assert.equal(battlefieldTask.activeStepKey, "tasks");
assert.ok(battlefieldTask.blockingReasonLabel.includes("战场控制检查"));
assert.ok(battlefieldTask.steps.some((step) => step.key === "tasks" && step.state === "blocked"));

const battle = buildWirePriorityRailPlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: {
    actionable: true,
    actions: ["ASSIGN_COMBAT_DAMAGE"],
    candidates: [{ action: "ASSIGN_COMBAT_DAMAGE", enabled: true, label: "分配战斗伤害", reason: "可提交" }],
    playerId: "P1",
    reason: "战斗窗口",
    view: { message: "分配伤害", title: "战斗伤害", type: "ASSIGN_COMBAT_DAMAGE" }
  },
  snapshot: baseSnapshot
});

assert.equal(battle.mode, "battle");
assert.equal(battle.activeStepKey, "focus");
assert.ok(battle.steps.some((step) => step.key === "focus" && step.value.includes("战斗")));

const disconnected = buildWirePriorityRailPlan({
  connectionStatus: "reconnecting",
  playerId: "P1",
  snapshot: baseSnapshot
});

assert.equal(disconnected.mode, "disconnected");
assert.equal(disconnected.activeStepKey, "window");
assert.ok(disconnected.nextInteractionLabel.includes("恢复连接"));

console.log("Wire priority rail plan check passed.");

import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const srcRoot = resolve(scriptDir, "../src");
const moduleCache = new Map();

const { buildTopbarQuickActionPlan } = loadTsModule(resolve(srcRoot, "utils/topbarQuickActionPlan.ts")).exports;

const prompt = {
  actionable: true,
  candidates: [
    { action: "PASS_PRIORITY", enabled: true, label: "让过优先权", reason: "可让过" },
    { action: "END_TURN", enabled: true, label: "结束回合", reason: "可结束" },
    { action: "SURRENDER", enabled: true, label: "投降", reason: "可投降" },
    { action: "READY", enabled: false, label: "准备", reason: "比赛已开始" },
    { action: "SUBMIT_DECK", enabled: false, label: "提交构筑", reason: "已提交" }
  ],
  playerId: "P1",
  promptId: "prompt-1",
  snapshotTick: 7
};

const readyPlan = buildTopbarQuickActionPlan({
  canAct: true,
  connected: true,
  prompt,
  snapshot: {}
});

const readyById = entriesById(readyPlan.entries);
assert.equal(readyById.pass.state, "ready");
assert.equal(readyById.pass.disabled, false);
assert.equal(readyById.pass.candidateAction, "PASS_PRIORITY");
assert.deepEqual(readyById.pass.command, {
  cmdType: "PASS_PRIORITY",
  promptId: "prompt-1",
  snapshotTick: 7
});
assert.equal(readyById.endTurn.state, "ready");
assert.deepEqual(readyById.endTurn.command, {
  cmdType: "END_TURN",
  promptId: "prompt-1",
  snapshotTick: 7
});
assert.equal(readyById.surrender.state, "ready");
assert.equal(readyById.surrender.variant, "danger");
assert.equal(readyById.ready.state, "blocked");
assert.equal(readyById.ready.disabled, true);
assert.equal(readyById.submitDeck.state, "blocked");

const disconnectedPlan = buildTopbarQuickActionPlan({
  canAct: true,
  connected: false,
  prompt,
  snapshot: {}
});
const disconnectedById = entriesById(disconnectedPlan.entries);
assert.equal(disconnectedById.pass.state, "disconnected");
assert.equal(disconnectedById.pass.disabled, true);
assert.equal(disconnectedById.pass.title, "连接恢复前不能提交快捷操作。");

const readonlyPlan = buildTopbarQuickActionPlan({
  canAct: false,
  connected: true,
  prompt,
  snapshot: {}
});
const readonlyById = entriesById(readonlyPlan.entries);
assert.equal(readonlyById.endTurn.state, "readonly");
assert.equal(readonlyById.endTurn.disabled, true);
assert.equal(readonlyById.endTurn.title, "当前不是你的服务端行动窗口。");

const missingPlan = buildTopbarQuickActionPlan({
  canAct: true,
  connected: true,
  prompt: { actionable: true, candidates: [], playerId: "P1" },
  snapshot: {}
});
const missingById = entriesById(missingPlan.entries);
assert.equal(missingById.pass.state, "missing");
assert.equal(missingById.pass.disabled, true);
assert.equal(missingById.pass.command, undefined);
assert.equal(missingById.endTurn.title, "当前服务端没有提供结束回合候选。");

console.log("Topbar quick action plan check passed.");

function entriesById(entries) {
  return Object.fromEntries(entries.map((entry) => [entry.id, entry]));
}

function loadTsModule(filename) {
  const resolved = resolve(filename);
  const cached = moduleCache.get(resolved);
  if (cached) {
    return cached;
  }

  const source = readFileSync(resolved, "utf8");
  const output = ts.transpileModule(source, {
    compilerOptions: {
      esModuleInterop: true,
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022
    }
  }).outputText;
  const module = { exports: {} };
  moduleCache.set(resolved, module);

  const requireShim = (id) => {
    if (id.startsWith(".")) {
      const target = resolve(dirname(resolved), id);
      if (target.endsWith("/types/protocol") || target.endsWith("/types/catalog")) {
        return {};
      }

      return loadTsModule(`${target}.ts`).exports;
    }

    throw new Error(`Unexpected import in topbar quick action plan check: ${id}`);
  };

  new Function("exports", "module", "require", output)(module.exports, module, requireShim);
  return module;
}

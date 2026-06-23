import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const srcRoot = resolve(scriptDir, "../src");
const moduleCache = new Map();

const { buildErrorResolutionPlan } = loadTsModule(resolve(srcRoot, "utils/errorResolutionPlan.ts")).exports;

const clear = buildErrorResolutionPlan({
  connectionStatus: "connected",
  errors: [],
  hasSnapshot: true,
  lastCommandSubmission: undefined,
  surface: "room"
});
assert.equal(clear.state, "clear");
assert.equal(clear.headline, "无阻断错误");
assert.equal(clear.actions.find((action) => action.id === "reviewPrompt")?.state, "secondary");

const invalidDeck = buildErrorResolutionPlan({
  connectionStatus: "connected",
  errors: [{ code: "INVALID_DECK", message: "mainDeck must contain at least 40 cards" }],
  hasSnapshot: true,
  lastCommandSubmission: {
    cmdType: "SUBMIT_DECK",
    errorCode: "INVALID_DECK",
    message: "卡组不合法：主牌堆至少需要 40 张牌。",
    state: "failed"
  },
  surface: "room"
});
assert.equal(invalidDeck.state, "input");
assert.equal(invalidDeck.headline, "卡组不合法");
assert.equal(invalidDeck.nextStep, "回到构筑/导入页修正卡组，然后重新提交。");
assert.equal(invalidDeck.actions.find((action) => action.id === "openDecks")?.state, "primary");
assert.equal(invalidDeck.actions.find((action) => action.id === "resync")?.disabled, false);

const stalePrompt = buildErrorResolutionPlan({
  connectionStatus: "connected",
  errors: [{ code: "PROMPT_EXPIRED", message: "行动快照已过期，请按最新状态重新提交。" }],
  hasSnapshot: true,
  lastCommandSubmission: {
    cmdType: "PLAY_CARD",
    errorCode: "PROMPT_EXPIRED",
    message: "行动快照已过期，请按最新状态重新提交。",
    state: "failed",
    snapshotTick: 7
  },
  surface: "match"
});
assert.equal(stalePrompt.state, "sync");
assert.equal(stalePrompt.headline, "行动窗口已过期");
assert.equal(stalePrompt.nextStep, "同步服务端权威快照，放弃旧 promptId/snapshotTick 后重新选择行动。");
assert.equal(stalePrompt.actions.find((action) => action.id === "resync")?.state, "primary");
assert.equal(stalePrompt.evidenceRows.find((row) => row.label === "提交命令")?.value, "PLAY_CARD");

const reconnect = buildErrorResolutionPlan({
  connectionStatus: "error",
  errors: [{ code: "INVALID_RECONNECT_TOKEN", message: "invalid reconnect token" }],
  hasSnapshot: false,
  lastCommandSubmission: undefined,
  surface: "room"
});
assert.equal(reconnect.state, "connection");
assert.equal(reconnect.headline, "重连凭证失效");
assert.equal(reconnect.actions.find((action) => action.id === "connect")?.state, "primary");
assert.equal(reconnect.actions.find((action) => action.id === "resync")?.disabled, true);

const invalidTarget = buildErrorResolutionPlan({
  connectionStatus: "connected",
  errors: [{ code: "INVALID_TARGET", message: "target is not legal" }],
  hasSnapshot: true,
  lastCommandSubmission: {
    cmdType: "DECLARE_BATTLE",
    errorCode: "INVALID_TARGET",
    message: "请选择服务端行动提示允许的目标。",
    state: "failed"
  },
  surface: "match"
});
assert.equal(invalidTarget.state, "authority");
assert.equal(invalidTarget.nextStep, "按最新服务端候选重新选择目标，不沿用本地推断。");
assert.equal(invalidTarget.actions.find((action) => action.id === "reviewPrompt")?.state, "primary");

console.log("Error resolution plan check passed.");

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

    throw new Error(`Unexpected import in error resolution plan check: ${id}`);
  };

  new Function("exports", "module", "require", output)(module.exports, module, requireShim);
  return module;
}

import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const srcRoot = resolve(scriptDir, "../src");
const moduleCache = new Map();

const { buildConnectionRecoveryPlan } = loadTsModule(resolve(srcRoot, "utils/connectionRecoveryPlan.ts")).exports;

const idle = buildConnectionRecoveryPlan({
  connectionStatus: "idle",
  hasSnapshot: false,
  lastSystemMessage: ""
});
assert.equal(idle.state, "offline");
assert.equal(idle.headline, "未连接服务端");
assert.equal(idle.actions.find((action) => action.id === "connect")?.state, "primary");
assert.equal(idle.actions.find((action) => action.id === "resync")?.disabled, true);
assert.equal(idle.actions.find((action) => action.id === "disconnect")?.disabled, true);
assert.equal(idle.nextStep, "连接并入座，等待服务端发布快照。");

const connected = buildConnectionRecoveryPlan({
  connectionStatus: "connected",
  hasSnapshot: true,
  lastSystemMessage: "P1 已进入房间",
  promptSnapshotTick: 7,
  snapshotTick: 7
});
assert.equal(connected.state, "online");
assert.equal(connected.headline, "连接正常");
assert.equal(connected.detail, "P1 已进入房间");
assert.equal(connected.actions.find((action) => action.id === "connect")?.disabled, true);
assert.equal(connected.actions.find((action) => action.id === "resync")?.state, "secondary");
assert.equal(connected.actions.find((action) => action.id === "disconnect")?.state, "secondary");
assert.equal(connected.tickLabel, "快照 tick 7 / prompt tick 7");

const stale = buildConnectionRecoveryPlan({
  connectionStatus: "connected",
  hasSnapshot: true,
  promptSnapshotTick: 7,
  snapshotTick: 8
});
assert.equal(stale.state, "stale");
assert.equal(stale.headline, "快照需要同步");
assert.equal(stale.actions.find((action) => action.id === "resync")?.state, "primary");
assert.equal(stale.nextStep, "重新同步快照，再提交行动。");

const resyncing = buildConnectionRecoveryPlan({
  connectionStatus: "resyncing",
  hasSnapshot: true,
  promptSnapshotTick: 7,
  snapshotTick: 8
});
assert.equal(resyncing.state, "resyncing");
assert.equal(resyncing.actions.find((action) => action.id === "connect")?.disabled, true);
assert.equal(resyncing.actions.find((action) => action.id === "resync")?.disabled, true);
assert.equal(resyncing.actions.find((action) => action.id === "disconnect")?.disabled, false);

const reconnecting = buildConnectionRecoveryPlan({
  connectionStatus: "reconnecting",
  hasSnapshot: true,
  lastSystemMessage: "自动重连中"
});
assert.equal(reconnecting.state, "recovering");
assert.equal(reconnecting.headline, "连接恢复中");
assert.equal(reconnecting.actions.find((action) => action.id === "connect")?.disabled, true);
assert.equal(reconnecting.actions.find((action) => action.id === "disconnect")?.disabled, false);

const error = buildConnectionRecoveryPlan({
  connectionStatus: "error",
  hasSnapshot: false,
  lastSystemMessage: "入座失败，请稍后重试。"
});
assert.equal(error.state, "error");
assert.equal(error.headline, "连接需要处理");
assert.equal(error.detail, "入座失败，请稍后重试。");
assert.equal(error.actions.find((action) => action.id === "connect")?.state, "primary");
assert.equal(error.actions.find((action) => action.id === "disconnect")?.disabled, true);

console.log("Connection recovery plan check passed.");

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

    throw new Error(`Unexpected import in connection recovery plan check: ${id}`);
  };

  new Function("exports", "module", "require", output)(module.exports, module, requireShim);
  return module;
}

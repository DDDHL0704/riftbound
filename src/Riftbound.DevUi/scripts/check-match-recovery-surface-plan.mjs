import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const srcRoot = resolve(scriptDir, "../src");
const moduleCache = new Map();

const matchPageSource = readFileSync(resolve(srcRoot, "pages/MatchPage.tsx"), "utf8");
const packageJson = JSON.parse(readFileSync(resolve(scriptDir, "../package.json"), "utf8"));
const globalsCss = readFileSync(resolve(srcRoot, "styles/globals.css"), "utf8");
const { buildMatchRecoverySurfacePlan } = loadTsModule(resolve(srcRoot, "utils/matchRecoverySurfacePlan.ts")).exports;

const offline = buildMatchRecoverySurfacePlan({
  connectionState: "offline",
  connectionStatusLabel: "已断开",
  errorCount: 0,
  hasSnapshot: false,
  promptSnapshotTick: null,
  snapshotTick: null,
  submissionGate: { canSubmit: false, reason: "连接状态：已断开，暂不提交行动。", state: "disconnected", stateLabel: "连接未就绪" },
  submissionState: undefined
});
assert.equal(offline.state, "blocked");
assert.equal(offline.activeRegionId, "connection");
assert.equal(offline.summary, "连接：已断开 / 快照：无 / 提交：连接未就绪 / 错误：0");
assert.deepEqual(offline.sections.map((section) => section.id), ["connection", "snapshot", "submission", "errors"]);
assert.equal(offline.sections.find((section) => section.id === "connection")?.source, "server-connection");
assert.equal(offline.sections.find((section) => section.id === "snapshot")?.state, "blocked");

const stale = buildMatchRecoverySurfacePlan({
  connectionState: "stale",
  connectionStatusLabel: "已连接",
  errorCount: 0,
  hasSnapshot: true,
  promptSnapshotTick: 7,
  snapshotTick: 8,
  submissionGate: { canSubmit: false, reason: "提示 tick 与快照 tick 不一致。", state: "stale", stateLabel: "快照过期" },
  submissionState: undefined
});
assert.equal(stale.state, "blocked");
assert.equal(stale.activeRegionId, "snapshot");
assert.equal(stale.sections.find((section) => section.id === "snapshot")?.value, "8 / 7");
assert.equal(stale.sections.find((section) => section.id === "snapshot")?.nextStep, "重新同步服务端快照，再提交行动。");

const failed = buildMatchRecoverySurfacePlan({
  connectionState: "online",
  connectionStatusLabel: "已连接",
  errorCount: 2,
  hasSnapshot: true,
  promptSnapshotTick: 11,
  snapshotTick: 11,
  submissionGate: { canSubmit: true, reason: "服务端入口可提交。", state: "ready", stateLabel: "可提交" },
  submissionState: "failed"
});
assert.equal(failed.state, "blocked");
assert.equal(failed.activeRegionId, "errors");
assert.equal(failed.sections.find((section) => section.id === "errors")?.value, "2 个");
assert.equal(failed.sections.find((section) => section.id === "submission")?.value, "失败");

const ready = buildMatchRecoverySurfacePlan({
  connectionState: "online",
  connectionStatusLabel: "已连接",
  errorCount: 0,
  hasSnapshot: true,
  promptSnapshotTick: 12,
  snapshotTick: 12,
  submissionGate: { canSubmit: true, reason: "服务端入口可提交。", state: "ready", stateLabel: "可提交" },
  submissionState: "sent"
});
assert.equal(ready.state, "ready");
assert.equal(ready.activeRegionId, "submission");
assert.equal(ready.sections.find((section) => section.id === "connection")?.state, "ready");
assert.equal(ready.sections.find((section) => section.id === "snapshot")?.state, "ready");
assert.equal(ready.sections.find((section) => section.id === "submission")?.state, "ready");

assert.ok(matchPageSource.includes("buildMatchRecoverySurfacePlan"), "MatchPage must build the recovery surface from server-derived plans.");
assert.ok(matchPageSource.includes("data-match-recovery-surface"), "MatchPage must expose a recovery surface for browser smoke.");
assert.ok(matchPageSource.includes("data-match-recovery-region"), "MatchPage must expose each recovery region for browser smoke.");
assert.ok(matchPageSource.includes("data-match-recovery-source"), "MatchPage must expose server source labels.");
assert.ok(matchPageSource.includes("data-match-recovery-active-region"), "MatchPage must expose the active recovery region.");
assert.ok(matchPageSource.includes("data-match-recovery-summary"), "MatchPage must expose the summary boundary.");
assert.ok(globalsCss.includes(".match-recovery-surface"), "Match recovery surface styles must be present.");
assert.ok(globalsCss.includes(".match-recovery-region"), "Match recovery region styles must be present.");
assert.ok(packageJson.scripts["check:match-recovery-surface-plan"], "Package scripts must expose the match recovery surface check.");
assert.ok(packageJson.scripts.build.includes("check:match-recovery-surface-plan"), "Build must run the match recovery surface check.");

console.log("Match recovery surface plan check passed.");

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
      if (target.endsWith("/types/protocol")) {
        return {};
      }

      return loadTsModule(`${target}.ts`).exports;
    }

    throw new Error(`Unexpected import in match recovery surface check: ${id}`);
  };

  new Function("exports", "module", "require", output)(module.exports, module, requireShim);
  return module;
}

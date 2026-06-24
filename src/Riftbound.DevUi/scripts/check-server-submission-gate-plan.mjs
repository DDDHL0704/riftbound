import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const srcRoot = resolve(scriptDir, "../src");
const moduleCache = new Map();

const { buildServerSubmissionGatePlan } = loadTsModule(resolve(srcRoot, "utils/serverSubmissionGatePlan.ts")).exports;

const matching = buildServerSubmissionGatePlan({
  connectionStatus: "connected",
  prompt: { actionable: true, candidates: [], playerId: "P1", snapshotTick: 12 },
  snapshot: { tick: 12 }
});
assert.equal(matching.canSubmit, true);
assert.equal(matching.state, "connected");
assert.equal(matching.reason, "行动提示和桌面快照同属 tick 12。");

const noPromptTick = buildServerSubmissionGatePlan({
  connectionStatus: "connected",
  prompt: { actionable: true, candidates: [], playerId: "P1" },
  snapshot: { tick: 13 }
});
assert.equal(noPromptTick.canSubmit, true);
assert.equal(noPromptTick.state, "connected");

const readOnlyPrompt = buildServerSubmissionGatePlan({
  connectionStatus: "connected",
  prompt: { actionable: false, candidates: [], playerId: "P1", snapshotTick: 13 },
  snapshot: { tick: 13 }
});
assert.equal(readOnlyPrompt.canSubmit, false);
assert.equal(readOnlyPrompt.state, "read-only-prompt");
assert.ok(readOnlyPrompt.reason.includes("只读"));

const stale = buildServerSubmissionGatePlan({
  connectionStatus: "connected",
  prompt: { actionable: true, candidates: [], playerId: "P1", snapshotTick: 12 },
  snapshot: { tick: 13 }
});
assert.equal(stale.canSubmit, false);
assert.equal(stale.state, "stale-snapshot");
assert.ok(stale.reason.includes("tick 12"));
assert.ok(stale.reason.includes("tick 13"));

const missingSnapshot = buildServerSubmissionGatePlan({
  connectionStatus: "connected",
  prompt: { actionable: true, candidates: [], playerId: "P1", snapshotTick: 12 }
});
assert.equal(missingSnapshot.canSubmit, false);
assert.equal(missingSnapshot.state, "missing-snapshot");

const resyncing = buildServerSubmissionGatePlan({
  connectionStatus: "resyncing",
  prompt: { actionable: true, candidates: [], playerId: "P1", snapshotTick: 12 },
  snapshot: { tick: 12 }
});
assert.equal(resyncing.canSubmit, false);
assert.equal(resyncing.state, "resyncing");

const disconnected = buildServerSubmissionGatePlan({
  connectionStatus: "disconnected",
  prompt: { actionable: true, candidates: [], playerId: "P1", snapshotTick: 12 },
  snapshot: { tick: 12 }
});
assert.equal(disconnected.canSubmit, false);
assert.equal(disconnected.state, "disconnected");

console.log("Server submission gate plan check passed.");

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

    throw new Error(`Unexpected import in server submission gate plan check: ${id}`);
  };

  new Function("exports", "module", "require", output)(module.exports, module, requireShim);
  return module;
}

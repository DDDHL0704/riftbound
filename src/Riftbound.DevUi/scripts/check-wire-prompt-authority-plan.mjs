import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const authorityExports = loadTsModule(resolve(scriptDir, "../src/components/match/wirePromptAuthorityPlan.ts"));
const { buildWirePromptAuthorityPlan } = authorityExports;

const serverPlan = buildWirePromptAuthorityPlan({
  playerId: "P1",
  prompt: prompt({
    candidates: [
      candidate("PLAY_CARD", { commandTemplate: true }),
      candidate("PASS")
    ],
    contract: true,
    objectContexts: true
  }),
  submissionGate: connectedGate()
});
assert.equal(serverPlan.state, "server");
assert.equal(serverPlan.issueCount, 0);
assert.deepEqual(serverPlan.metrics.map((metric) => metric.value), ["可操作", "2", "2/2", "0"]);
assert.equal(serverPlan.rows.find((row) => row.key === "commandTemplates").stateLabel, "全部可解释");

const mixedPlan = buildWirePromptAuthorityPlan({
  playerId: "P1",
  prompt: prompt({
    candidates: [
      candidate("PLAY_CARD", { commandTemplate: true }),
      candidate("DECLARE_BATTLE")
    ],
    contract: false,
    objectContexts: false
  }),
  submissionGate: connectedGate()
});
assert.equal(mixedPlan.state, "mixed");
assert.equal(mixedPlan.rows.find((row) => row.key === "commandTemplates").value, "1/2");
assert.equal(mixedPlan.rows.find((row) => row.key === "objectContexts").state, "mixed");
assert.equal(mixedPlan.rows.find((row) => row.key === "contract").state, "mixed");
assert.match(mixedPlan.summary, /继续补齐/);

const fallbackPlan = buildWirePromptAuthorityPlan({
  playerId: "P1",
  prompt: {
    actionable: true,
    actions: ["PLAY_CARD"],
    playerId: "P1",
    reason: "legacy action list",
    view: { message: "legacy", title: "legacy", type: "MAIN_ACTION" }
  },
  submissionGate: connectedGate()
});
assert.equal(fallbackPlan.state, "missing");
assert.equal(fallbackPlan.rows.find((row) => row.key === "candidates").state, "fallback");
assert.equal(fallbackPlan.rows.find((row) => row.key === "commandTemplates").state, "missing");

const missingPlan = buildWirePromptAuthorityPlan({ playerId: "P1" });
assert.equal(missingPlan.state, "missing");
assert.equal(missingPlan.rows.find((row) => row.key === "window").state, "missing");

const staleGatePlan = buildWirePromptAuthorityPlan({
  playerId: "P1",
  prompt: prompt({
    candidates: [candidate("PASS")],
    contract: true,
    objectContexts: true
  }),
  submissionGate: {
    canSubmit: false,
    reason: "行动提示属于 tick 7，当前桌面快照是 tick 8。",
    state: "stale-snapshot",
    stateLabel: "等待同步"
  }
});
assert.equal(staleGatePlan.state, "mixed");
assert.equal(staleGatePlan.rows.find((row) => row.key === "submissionGate").state, "mixed");
assert.equal(staleGatePlan.rows.find((row) => row.key === "submissionGate").stateLabel, "等待同步");

console.log("Wire prompt authority plan check passed.");

function prompt({
  candidates,
  contract,
  objectContexts
}) {
  return {
    actionable: true,
    actions: candidates.map((item) => item.action),
    candidates,
    contract: contract ? {
      candidateAction: "PLAY_CARD",
      hiddenMetadata: [],
      legalChoices: ["candidate.sources"],
      promptKind: "MAIN_ACTION",
      requiredPayload: ["sourceObjectId"],
      validationErrors: [],
      visibleMetadata: []
    } : undefined,
    objectContexts: objectContexts ? [{
      candidates: [{
        action: candidates[0]?.action ?? "PASS",
        commandFields: ["cmdType"],
        enabled: true,
        label: candidates[0]?.label ?? "候选",
        reason: "可提交",
        requiredCommandFields: [],
        roles: ["来源"]
      }],
      disabledCandidateCount: 0,
      enabledCandidateCount: 1,
      objectId: "object-1"
    }] : undefined,
    playerId: "P1",
    promptId: "prompt-authority-check",
    reason: "检查行动窗口契约",
    snapshotTick: 1,
    view: {
      message: "检查行动窗口契约。",
      title: "主行动",
      type: "MAIN_ACTION"
    }
  };
}

function candidate(action, { commandTemplate = false } = {}) {
  return {
    action,
    commandTemplate: commandTemplate ? {
      bindings: [{ field: "sourceObjectId", required: true, source: "selectedSource" }],
      cmdType: action
    } : undefined,
    enabled: true,
    label: action,
    reason: "可提交"
  };
}

function connectedGate() {
  return {
    canSubmit: true,
    reason: "行动提示和桌面快照同属 tick 1。",
    state: "connected",
    stateLabel: "可提交"
  };
}

function loadTsModule(sourcePath) {
  const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
  const output = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022
    }
  }).outputText;
  const moduleShim = { exports: {} };
  new Function("exports", "module", output)(moduleShim.exports, moduleShim);
  return moduleShim.exports;
}

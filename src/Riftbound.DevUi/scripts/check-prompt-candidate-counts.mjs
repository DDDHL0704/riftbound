import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/promptCandidateCounts.ts");
const source = readFileSync(sourcePath, "utf8");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function("exports", "module", output)(moduleShim.exports, moduleShim);

const { promptCandidateCounts } = moduleShim.exports;

assert.deepEqual(
  promptCandidateCounts({
    candidates: [
      { action: "PLAY_CARD", enabled: true, label: "打出", reason: "可提交" },
      { action: "ACTIVATE_ABILITY", enabled: false, label: "技能", reason: "无窗口" }
    ]
  }),
  {
    candidateCount: 2,
    disabledCandidateCount: 1,
    enabledCandidateCount: 1,
    source: "candidates"
  },
  "candidate list fallback must count enabled and disabled candidates"
);

assert.deepEqual(
  promptCandidateCounts({
    candidates: [{ action: "PLAY_CARD", enabled: true, label: "打出", reason: "可提交" }],
    serverFlow: {
      candidateCount: 9,
      disabledCandidateCount: 5,
      enabledCandidateCount: 4
    }
  }),
  {
    candidateCount: 9,
    disabledCandidateCount: 5,
    enabledCandidateCount: 4,
    source: "server-flow"
  },
  "serverFlow candidate counts must override local candidate-list fallback"
);

assert.deepEqual(
  promptCandidateCounts({
    candidates: [
      { action: "PLAY_CARD", enabled: true, label: "打出", reason: "可提交" },
      { action: "WAIT", enabled: false, label: "等待", reason: "不可提交" }
    ],
    serverFlow: {
      candidateCount: 7,
      enabledCandidateCount: 3
    }
  }),
  {
    candidateCount: 7,
    disabledCandidateCount: 4,
    enabledCandidateCount: 3,
    source: "server-flow"
  },
  "missing server disabled count should derive from authoritative total and enabled counts"
);

console.log("Prompt candidate counts check passed.");
